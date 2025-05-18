namespace GGV;

/// <summary>Optimize occlusion checks by inlining and short circuiting when possible</summary>
[HarmonyPatch]
internal static class OcclusionOptimizations
{
    private static readonly float[] KERNEL = new float[5] { 0.12f, 0.25f, 0.3f, 0.25f, 0.12f };
    private const float KERNEL_TOTAL = 1.0816f; // result of matrix multiplying the kernel with itself

    /// <summary>Inlined and optimized logic for occlusion checks with complete vanilla parity.</summary>
    [HarmonyPatch(typeof(OcclusionLayer), nameof(OcclusionLayer.GenerateOcclusionTexture))]
    [HarmonyPrefix]
    private static bool FastGenerateOcclusionTexture(OcclusionLayer __instance, int baseX, int baseY, DungeonData d, ref Texture2D __result)
    {
      System.Diagnostics.Stopwatch occlusionWatch = System.Diagnostics.Stopwatch.StartNew();

      __instance.m_gameManagerCached = GameManager.Instance;
      __instance.m_pixelatorCached   = Pixelator.Instance;
      __instance.m_allPlayersCached  = GameManager.Instance.AllPlayers;
      __instance.m_playerOneDead     = !__instance.m_gameManagerCached.PrimaryPlayer || __instance.m_gameManagerCached.PrimaryPlayer.healthHaver.IsDead;
      if (__instance.m_gameManagerCached.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
        __instance.m_playerTwoDead = !__instance.m_gameManagerCached.SecondaryPlayer || __instance.m_gameManagerCached.SecondaryPlayer.healthHaver.IsDead;

      int num = __instance.m_pixelatorCached.CurrentMacroResolutionX / 16 + 4;
      int num2 = __instance.m_pixelatorCached.CurrentMacroResolutionY / 16 + 4;
      int num3 = num * __instance.textureMultiplier;
      int num4 = num2 * __instance.textureMultiplier;
      if (__instance.m_occlusionTexture == null || __instance.m_occlusionTexture.width != num3 || __instance.m_occlusionTexture.height != num4)
      {
        if (__instance.m_occlusionTexture != null)
        {
          __instance.m_occlusionTexture.Resize(num3, num4);
        }
        else
        {
          __instance.m_occlusionTexture = new Texture2D(num3, num4, TextureFormat.ARGB32, false);
          __instance.m_occlusionTexture.filterMode = FilterMode.Bilinear;
          __instance.m_occlusionTexture.wrapMode = TextureWrapMode.Clamp;
        }
      }
      if (__instance.m_colorCache == null || __instance.m_colorCache.Length != num3 * num4)
      {
        __instance.m_colorCache = new Color[num3 * num4];
      }
      __instance.cachedX = baseX;
      __instance.cachedY = baseY;
      if (!__instance.m_gameManagerCached.IsLoadingLevel)
      {
        for (int i = 0; i < num3; i++)
        {
          for (int j = 0; j < num4; j++)
          {
            int num5 = j * num3 + i;
            float worldX = (float)i / (float)__instance.textureMultiplier;
            float worldY = (float)j / (float)__instance.textureMultiplier;
            __instance.m_colorCache[num5] = FastGetInterpolatedValueAtPoint(__instance, baseX, baseY, worldX, worldY, d);
          }
        }
      }
      __instance.m_occlusionTexture.SetPixels(__instance.m_colorCache);
      __instance.m_occlusionTexture.Apply();
      __result = __instance.m_occlusionTexture;
      occlusionWatch.Stop();
      System.Console.WriteLine($"ran occlusion checks in {occlusionWatch.ElapsedTicks*100,12:n0}ns");
      return false; // skip original method
    }

    // start:   244,000ns to 418,200ns
    // current: 160,000ns to 200.000ns
    private static Color FastGetInterpolatedValueAtPoint(OcclusionLayer __instance, int baseX, int baseY, float worldX, float worldY, DungeonData d)
    {
      int x = baseX + (int)worldX;
      int y = baseY + (int)worldY;
      if (x < 0 || y < 0 || x >= d.m_width || y >= d.m_height)
        return Color.clear; //NOTE: clear is, for some reason, full occlusion / completely black

      CellData[][] cells = d.cellData;
      CellData cell = cells[x][y];
      bool texturedOcclusion = __instance.m_pixelatorCached.UseTexturedOcclusion;

      // determine the base occlusion for the cell (inlined and optimized from GetCellOcclusion())
      float occlusion = ((cell != null) ? cell.occlusionData.cellOcclusion : 1f);
      if (!texturedOcclusion && x >= 2 && y >= 2 && x < d.m_width - 2 && y < d.m_height - 2)
      {
        float occlusionAccum = 0f;
        for (int i = -2; i <= 2; i++)
        {
          for (int j = -2; j <= 2; j++)
          {
            float weight = KERNEL[i + 2] * KERNEL[j + 2];
            CellData neighbor = cells[x + i][y + j];
            occlusionAccum += ((neighbor != null) ? (neighbor.occlusionData.cellOcclusion * weight) : weight);
          }
        }
        float maxOcclusion = occlusionAccum / KERNEL_TOTAL;
        if (maxOcclusion < occlusion)
          occlusion = maxOcclusion;
      }
      if (occlusion >= 1f)
        return Color.clear;

      // determine the R value from the cell (inlined and optimized from GetRValueForCell())
      float r = 0f;
      while (true) // convenience loop we exit out of as soon as we have a definitive value
      {
        if (texturedOcclusion)
          break;
        if (cell == null || cell.isExitCell || (cell.type == CellType.WALL && !cell.IsAnyFaceWall()))
          break;
        if (y - 2 >= 0 && cells[x][y - 2] != null && cells[x][y - 2].isExitCell)
          break;
        if (x < 1 || x > d.m_width - 2 || y < 3 || y > d.m_height - 2)
          break;

        RoomHandler room = cells[x][y].parentRoom ?? cells[x][y].nearestRoom;
        if (room == null)
        {
          r = 1f;
          break;
        }

        if (room.visibility != RoomHandler.VisibilityStatus.OBSCURED && room.visibility != RoomHandler.VisibilityStatus.REOBSCURED)
          break;

        bool isPlayerNearby = false;
        for (int i = 0; i < __instance.m_allPlayersCached.Length; i++)
        {
          if ((i == 0 && __instance.m_playerOneDead) || (i == 1 && __instance.m_playerTwoDead))
            continue;
          RoomHandler proom = __instance.m_allPlayersCached[i].m_currentRoom;
          if (proom == null)
            continue;
          if (proom.connectedRooms != null && proom.connectedRooms.Contains(room))
          {
            isPlayerNearby = true;
            break;
          }
        }
        if (!isPlayerNearby)
        {
          r = 1f;
          break;
        }

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
        if (x < d.m_width - 1 && cells[x + 1][y] != null && cells[x + 1][y].isExitCell)
          break;
        r = 1f;
        break;
      }

      // determine the G value from the cell (inlined and optimized from GetGValueForCell())
      float g = 0f;
      if (cell != null && (cell.type != CellType.WALL || cell.IsLowerFaceWall() || (cell.IsUpperFacewall() && !texturedOcclusion)))
      {
        RoomHandler nearestRoom = cell.nearestRoom;
        if (nearestRoom.visibility == RoomHandler.VisibilityStatus.CURRENT)
          g = 1f * (1f - cell.occlusionData.minCellOccluionHistory);
        else if (nearestRoom.hasEverBeenVisited && nearestRoom.visibility != RoomHandler.VisibilityStatus.REOBSCURED)
          g = 1f;
      }

      if (occlusion < 0)
        return new Color(r, g, 0f, 1f);
      return new Color(r, g, 0f, 1f - occlusion * occlusion);
    }
}
