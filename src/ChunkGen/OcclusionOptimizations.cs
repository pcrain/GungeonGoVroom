namespace GGV;

/// <summary>Optimize occlusion checks by inlining and short circuiting when possible</summary>
[HarmonyPatch]
internal static class OcclusionOptimizations
{
    private static readonly float[] WEIGHTS = new float[25] {
      0.0144f, 0.0300f, 0.0360f, 0.0300f, 0.0144f,
      0.0300f, 0.0625f, 0.0750f, 0.0625f, 0.0300f,
      0.0360f, 0.0750f, 0.0900f, 0.0750f, 0.0360f,
      0.0300f, 0.0625f, 0.0750f, 0.0625f, 0.0300f,
      0.0144f, 0.0300f, 0.0360f, 0.0300f, 0.0144f,
    };
    private static readonly float[] KERNEL = new float[5] { 0.12f, 0.25f, 0.3f, 0.25f, 0.12f };
    private const float KERNEL_TOTAL = 1.0816f; // result of matrix multiplying the kernel with itself
    private const int KW = 2; // kernel width

    private static float[] _CachedOcclusions = null;
    private static float[] _CachedKernels = null;
    private static readonly List<RoomHandler> _NearbyRooms = new();

    // start:   244,000ns to 418,200ns
    // current: 160,000ns to 200.000ns

    /// <summary>Inlined and optimized logic for occlusion checks with complete vanilla parity.</summary>
    [HarmonyPatch(typeof(OcclusionLayer), nameof(OcclusionLayer.GenerateOcclusionTexture))]
    [HarmonyPrefix]
    private static bool FastGenerateOcclusionTexture(OcclusionLayer __instance, int baseX, int baseY, DungeonData d, ref Texture2D __result)
    {
      System.Diagnostics.Stopwatch occlusionWatch = System.Diagnostics.Stopwatch.StartNew();

      OcclusionLayer o      = __instance;
      o.m_gameManagerCached = GameManager.Instance;
      o.m_pixelatorCached   = Pixelator.Instance;
      if (o.m_pixelatorCached.UseTexturedOcclusion)
        return true; // only the wild west uses textured occlusion, so if we somehow end up there...let the vanilla game take over

      o.cachedX = baseX;
      o.cachedY = baseY;
      o.m_allPlayersCached  = GameManager.Instance.AllPlayers;
      o.m_playerOneDead     = !o.m_gameManagerCached.PrimaryPlayer || o.m_gameManagerCached.PrimaryPlayer.healthHaver.IsDead;
      if (o.m_gameManagerCached.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
        o.m_playerTwoDead = !o.m_gameManagerCached.SecondaryPlayer || o.m_gameManagerCached.SecondaryPlayer.healthHaver.IsDead;
      else
        o.m_playerTwoDead = true;

      int num            = o.m_pixelatorCached.CurrentMacroResolutionX / 16 + 4;
      int num2           = o.m_pixelatorCached.CurrentMacroResolutionY / 16 + 4;
      int texW           = num * o.textureMultiplier;
      int texH           = num2 * o.textureMultiplier;
      int textureArea    = texW * texH;
      CellData[][] cells = d.cellData;
      int dw             = d.m_width;
      int dh             = d.m_height;
      int dsize          = dw * dh;

      _NearbyRooms.Clear();
      RoomHandler p1Room = o.m_playerOneDead ? null : o.m_allPlayersCached[0].m_currentRoom;
      if (p1Room != null && p1Room.connectedRooms != null)
        for (int rn = p1Room.connectedRooms.Count - 1; rn >= 0; --rn)
          _NearbyRooms.Add(p1Room.connectedRooms[rn]);
      RoomHandler p2Room = o.m_playerTwoDead ? null : o.m_allPlayersCached[1].m_currentRoom;
      if (p2Room != null && p2Room != p1Room && p2Room.connectedRooms != null)
        for (int rn = p2Room.connectedRooms.Count - 1; rn >= 0; --rn)
          _NearbyRooms.Add(p2Room.connectedRooms[rn]); // can count some rooms twice, but ultimately harmless

      if (o.m_occlusionTexture == null)
      {
        o.m_occlusionTexture            = new Texture2D(texW, texH, TextureFormat.ARGB32, false);
        o.m_occlusionTexture.filterMode = FilterMode.Bilinear;
        o.m_occlusionTexture.wrapMode   = TextureWrapMode.Clamp;
      }
      else if (o.m_occlusionTexture.width != texW || o.m_occlusionTexture.height != texH)
        o.m_occlusionTexture.Resize(texW, texH);
      if (o.m_colorCache == null || o.m_colorCache.Length != textureArea)
        o.m_colorCache = new Color[textureArea];

      #region Validate occlusion kernels
      if (_CachedOcclusions == null)
      {
        _CachedOcclusions = new float[dsize];
        _CachedKernels = new float[dsize];
        for (int ki = 0; ki < dsize; ++ki)
        {
          _CachedOcclusions[ki] = -1f;
          _CachedKernels[ki] = -1f;
        }
      }
      else if (_CachedOcclusions.Length < dsize)
      {
        Array.Resize(ref _CachedOcclusions, dsize);
        Array.Resize(ref _CachedKernels, dsize);
      }
      int changedCells = 0;

      int occMinX = (baseX > KW) ? (baseX - KW) : 0;
      int occMinY = (baseY > KW) ? (baseY - KW) : 0;
      int occMaxX = KW + baseX + (texW - 1) / o.textureMultiplier;
      if (occMaxX >= dw)
        occMaxX = dw - 1;
      int occMaxY = KW + baseY + (texH - 1) / o.textureMultiplier;
      if (occMaxY >= dh)
        occMaxY = dh - 1;
      for (int ci = occMinX; ci <= occMaxX; ++ci)
      {
        CellData[] cellColumn = cells[ci];
        for (int cj = occMinY; cj <= occMaxY; ++cj)
        {
          int cacheIndex      = ci * dw + cj;
          CellData cc         = cellColumn[cj];
          float trueOcclusion = cc == null ? 1f : cc.occlusionData.cellOcclusion;
          //NOTE: if our occlusion has changed, we need to invalidate the cached kernels of the 5x5 surrounding area
          if (_CachedOcclusions[cacheIndex] == trueOcclusion)
            continue;

          int kMaxX = ci + KW; if (kMaxX >= dw) kMaxX = dw - 1;
          int kMaxY = cj + KW; if (kMaxY >= dh) kMaxY = dh - 1;
          int kMinX = ci - KW; if (kMinX < 0)   kMinX = 0;
          int kMinY = cj - KW; if (kMinY < 0)   kMinY = 0;
          for (int kx = kMinX; kx <= kMaxX; ++kx)
            for (int ky = kMinY; ky <= kMaxY; ++ky)
              _CachedKernels[kx * dw + ky] = -1f;
          _CachedOcclusions[cacheIndex] = trueOcclusion;
          ++changedCells;
        }
      }
      if (changedCells > 0)
        System.Console.WriteLine($"occlusion changed for {changedCells} cells");
      #endregion

      if (!o.m_gameManagerCached.IsLoadingLevel)
      {
        int pix = 0; // pixel index into color array
        for (int tj = 0; tj < texH; tj++)
        {
          int y = baseY + tj / o.textureMultiplier;
          for (int ti = 0; ti < texW; ti++)
          {
            int x = baseX + ti / o.textureMultiplier;
            if (x < 0 || y < 0 || x >= d.m_width || y >= d.m_height)
            {
              o.m_colorCache[pix++] = Color.clear; //NOTE: clear is, for some reason, full occlusion / completely black
              continue;
            }

            int cacheIndex = x * dw + y;
            CellData cell = cells[x][y];

            // determine the base occlusion for the cell (inlined and optimized from GetCellOcclusion())
            float occlusion = _CachedKernels[cacheIndex];
            // if (occlusion == -1f)
            {
              _CachedOcclusions[cacheIndex] = occlusion = ((cell != null) ? cell.occlusionData.cellOcclusion : 1f);
              if (x >= KW && y >= KW && x < d.m_width - KW && y < d.m_height - KW)
              {
                float occlusionAccum = 0f;
                int k = 0;
                for (int i = -KW; i <= KW; i++)
                {
                  CellData[] cellColumn = cells[x + i];
                  for (int j = -KW; j <= KW; j++)
                  {
                    CellData neighbor = cellColumn[y + j];
                    occlusionAccum += ((neighbor != null) ? (neighbor.occlusionData.cellOcclusion * WEIGHTS[k++]) : WEIGHTS[k++]);
                  }
                }
                float maxOcclusion = occlusionAccum / KERNEL_TOTAL;
                if (maxOcclusion < occlusion)
                  occlusion = maxOcclusion;
              }
              _CachedKernels[cacheIndex] = occlusion;
            }
            if (occlusion >= 1f)
            {
              o.m_colorCache[pix++] = Color.clear;
              continue;
            }

            // determine the R value from the cell (inlined and optimized from GetRValueForCell())
            float r = 0f;
            while (true) // convenience loop we exit out of as soon as we have a definitive value
            {
              if (cell == null || cell.isExitCell || (cell.type == CellType.WALL && !cell.IsAnyFaceWall()))
                break;
              if (y - 2 >= 0 && cells[x][y - 2] != null && cells[x][y - 2].isExitCell)
                break;
              if (x < 1 || x > d.m_width - 2 || y < 3 || y > d.m_height - 2)
                break;

              RoomHandler room = cell.parentRoom ?? cell.nearestRoom;
              if (room == null)
                goto definitelyObscured;

              if (room.visibility != RoomHandler.VisibilityStatus.OBSCURED && room.visibility != RoomHandler.VisibilityStatus.REOBSCURED)
                break;

              for (int rn = _NearbyRooms.Count - 1; rn >= 0; --rn)
                if (_NearbyRooms[rn] == room)
                  goto definitelyObscured;

              if (cell.isExitNonOccluder || cell.isExitCell)
                break;
              if (y > 1 && (cells[x][y - 1]?.isExitCell ?? false))
                break;
              if (y > 2 && (cells[x][y - 2]?.isExitCell ?? false))
                break;
              if (y > 3 && (cells[x][y - 3]?.isExitCell ?? false))
                break;
              if (x > 1 && (cells[x - 1][y]?.isExitCell ?? false))
                break;
              if (x < d.m_width - 1 && (cells[x + 1][y]?.isExitCell ?? false))
                break;

            definitelyObscured:
              r = 1f;
              break;
            }

            // determine the G value from the cell (inlined and optimized from GetGValueForCell())
            float g = 0f;
            if (cell != null && (cell.type != CellType.WALL || cell.IsLowerFaceWall() || cell.IsUpperFacewall()))
            {
              RoomHandler nearestRoom = cell.nearestRoom;
              if (nearestRoom.visibility == RoomHandler.VisibilityStatus.CURRENT)
                g = 1f * (1f - cell.occlusionData.minCellOccluionHistory);
              else if (nearestRoom.hasEverBeenVisited && nearestRoom.visibility != RoomHandler.VisibilityStatus.REOBSCURED)
                g = 1f;
            }

            o.m_colorCache[pix++] = new Color(r, g, 0f, (occlusion < 0) ? 1f : (1f - occlusion * occlusion));
            continue;
          }
        }
      }

      o.m_occlusionTexture.SetPixels(o.m_colorCache);
      o.m_occlusionTexture.Apply();

      __result = o.m_occlusionTexture;
      occlusionWatch.Stop();
      // System.Console.WriteLine($"ran occlusion checks for {textureArea} pixels in {occlusionWatch.ElapsedTicks*100,12:n0}ns");

      return false; // skip original method
    }
}
