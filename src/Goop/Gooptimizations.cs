namespace GGV;

/* TODO:
    - replace Dictionary iterator in lateupdate with array iterator
    - keep track of gooped chunks directly when adding / removing goop positions and use the gooped chunks in RebuildMeshColors()
    - optimize SetColorDirty()
*/

using static DeadlyDeadlyGoopManager;

[HarmonyPatch]
internal static class Gooptimizations
{
    private class ExtraGoopData
    {
      private static ExtraGoopData _NullEGD       = new ExtraGoopData(null);
      private static ExtraGoopData _CachedEGD     = _NullEGD; // not in _AllEGDs;
      private static List<ExtraGoopData> _AllEGDs = new();

      public DeadlyDeadlyGoopManager manager;
      public UInt64[,,] goopedCellBitfield;
      public GoopPositionData[,] goopedCellGrid; // goop data for the entire level grid
      public List<GoopPositionData> allGoopedCells; // list of all cells gooped by this ddgm

      private int _xChunks;
      private int _yChunks;
      private int _cachedWidth;
      private int _cachedHeight;
      private DungeonData _cachedDungeon;
      private Dictionary<GoopPositionData, int> _allGoopedCellsIndices;

      // private since it should never be constructed outside of Get() and static fields
      private ExtraGoopData(DeadlyDeadlyGoopManager manager)
      {
        this.manager = manager;
        if (manager == null)
          return; // for _NullEGd

        DungeonData d = this._cachedDungeon = GameManager.Instance.Dungeon.data;
        this._cachedWidth = d.m_width;
        this._cachedHeight = d.m_height;
        this._xChunks = Mathf.CeilToInt((float)this._cachedWidth / (float)manager.CHUNK_SIZE);
        this._yChunks = Mathf.CeilToInt((float)this._cachedHeight / (float)manager.CHUNK_SIZE);
        this.goopedCellBitfield = new UInt64[this._xChunks,this._yChunks,7];
        this.goopedCellGrid = new GoopPositionData[(int)(this._cachedWidth / GOOP_GRID_SIZE), (int)(this._cachedHeight / GOOP_GRID_SIZE)];
        this.allGoopedCells = new();
        this._allGoopedCellsIndices = new();
        _AllEGDs.Add(this);
      }

      private void Resize()
      {
        int newWidth           = this._cachedDungeon.m_width;
        int newHeight          = this._cachedDungeon.m_height;
        int newxChunks         = Mathf.CeilToInt((float)newWidth / (float)manager.CHUNK_SIZE);
        int newyChunks         = Mathf.CeilToInt((float)newHeight / (float)manager.CHUNK_SIZE);
        UInt64[,,] newBitfield = new UInt64[newxChunks,newyChunks,7];

        for (int i = 0; i < this._xChunks; ++i)
          for (int j = 0; j < this._yChunks; ++j)
            for (int k = 0; k < 7; ++k)
              newBitfield[i,j,k] = this.goopedCellBitfield[i,j,k];

        this._cachedWidth       = newWidth;
        this._cachedHeight      = newHeight;
        this._xChunks           = newxChunks;
        this._yChunks           = newyChunks;
        this.goopedCellBitfield = newBitfield;

        this.goopedCellGrid = new GoopPositionData[(int)(this._cachedWidth / GOOP_GRID_SIZE), (int)(this._cachedHeight / GOOP_GRID_SIZE)];
        foreach (GoopPositionData gpd in allGoopedCells)
          this.goopedCellGrid[gpd.goopPosition.x, gpd.goopPosition.y] = gpd;
      }

      internal static void ResizeAllBitfields()
      {
        GGVDebug.Log($"Resizing {_AllEGDs.Count} goop bitfields");
        for (int i = _AllEGDs.Count - 1; i >= 0; --i)
          _AllEGDs[i].Resize();
      }

      internal static ExtraGoopData Get(DeadlyDeadlyGoopManager manager)
      {
        if (_CachedEGD.manager == manager)
          return _CachedEGD;
        for (int i = _AllEGDs.Count - 1; i >= 0; --i)
        {
          if (_AllEGDs[i].manager != manager)
            continue;
          return _CachedEGD = _AllEGDs[i];
        }
        return _CachedEGD = new ExtraGoopData(manager);
      }

      internal static void ClearLevelData()
      {
        _AllEGDs.Clear();
        _CachedEGD = _NullEGD;
      }

      internal static bool TestGoopedBit(DeadlyDeadlyGoopManager manager, IntVector2 pos)
      {
        int chunkSize     = (int)(manager.CHUNK_SIZE / GOOP_GRID_SIZE); //NOTE: always 5 / 0.25 == 20 in base game
        int chunkX        = (int)(pos.x / (float)chunkSize);
        int chunkY        = (int)(pos.y / (float)chunkSize);
        int bitOffset     = (pos.x % chunkSize) * chunkSize + (pos.y % chunkSize);
        ExtraGoopData egd = ExtraGoopData.Get(manager);
        if (chunkX < 0 || chunkY < 0 || chunkX >= egd._xChunks || chunkY >= egd._yChunks)
          return false;
        return (egd.goopedCellBitfield[chunkX, chunkY, bitOffset / 64] & (1ul << (bitOffset % 64))) > 0;
      }

      private void SetGoopedBit(IntVector2 pos)
      {
        int chunkSize     = (int)(this.manager.CHUNK_SIZE / GOOP_GRID_SIZE); //NOTE: always 5 / 0.25 == 20 in base game
        int chunkX        = (int)(pos.x / (float)chunkSize);
        int chunkY        = (int)(pos.y / (float)chunkSize);
        int bitOffset     = (pos.x % chunkSize) * chunkSize + (pos.y % chunkSize);
        if (chunkX < 0 || chunkY < 0 || chunkX >= this._xChunks || chunkY >= this._yChunks)
          return;
        this.goopedCellBitfield[chunkX, chunkY, bitOffset / 64] |= (1ul << (bitOffset % 64));
      }

      private void ClearGoopedBit(IntVector2 pos)
      {
        int chunkSize     = (int)(this.manager.CHUNK_SIZE / GOOP_GRID_SIZE); //NOTE: always 5 / 0.25 == 20 in base game
        int chunkX        = (int)(pos.x / (float)chunkSize);
        int chunkY        = (int)(pos.y / (float)chunkSize);
        int bitOffset     = (pos.x % chunkSize) * chunkSize + (pos.y % chunkSize);
        if (chunkX < 0 || chunkY < 0 || chunkX >= this._xChunks || chunkY >= this._yChunks)
          return;
        this.goopedCellBitfield[chunkX, chunkY, bitOffset / 64] &= ~(1ul << (bitOffset % 64));
      }

      internal void AddGoop(GoopPositionData goop)
      {
        IntVector2 pos = goop.goopPosition;
        this.SetGoopedBit(pos);
        this.goopedCellGrid[pos.x, pos.y] = goop;
        int numGoops = this.allGoopedCells.Count;
        this.allGoopedCells.Add(goop);
        this._allGoopedCellsIndices[goop] = numGoops;
      }

      internal void RemoveGoop(GoopPositionData goop)
      {
        if (!this._allGoopedCellsIndices.TryGetValue(goop, out int idx))
        {
          GGVDebug.Log($"goop bookkeeping failure! tried to remove nonexistent goop");
          return;
        }
        IntVector2 pos = goop.goopPosition;
        this.ClearGoopedBit(pos);
        this.goopedCellGrid[pos.x, pos.y] = null;
        this._allGoopedCellsIndices.Remove(goop);
        int lastGoopIdx = this.allGoopedCells.Count - 1;
        this.allGoopedCells[idx] = this.allGoopedCells[lastGoopIdx]; // move last listed goop to position we're removing since we don't care about order
        this._allGoopedCellsIndices[this.allGoopedCells[idx]] = idx;
        this.allGoopedCells.RemoveAt(lastGoopIdx); // remove the last element of the list to avoid duplicates
      }
    }

    private static bool Prepare(MethodBase original)
    {
      if (!GGVConfig.OPT_GOOP)
        return false;
      if (original == null)
        GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
      else
        GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
      return true;
    }

    [HarmonyPatch(typeof(DeadlyDeadlyGoopManager), nameof(DeadlyDeadlyGoopManager.ClearPerLevelData))]
    [HarmonyPostfix]
    private static void DeadlyDeadlyGoopManagerClearPerLevelDataPatch()
    {
      ExtraGoopData.ClearLevelData();
    }

    /// <summary>Invalidate cached goop data bitfield.</summary>
    [HarmonyPatch(typeof(DungeonData), nameof(DungeonData.ClearCachedCellData))]
    [HarmonyPostfix]
    private static void ClearCachedCellData(DungeonData __instance)
    {
      //NOTE: next two lines duplicate the patch in DungeonWidthAndHeightPatches, but it's harmless so oh well
      __instance.m_width = __instance.cellData.Length;
      __instance.m_height = __instance.cellData[0].Length;
      ExtraGoopData.ResizeAllBitfields();
    }

    [HarmonyPatch(typeof(DeadlyDeadlyGoopManager), nameof(DeadlyDeadlyGoopManager.RebuildMeshUvsAndColors))]
    [HarmonyPrefix]
    private static bool DeadlyDeadlyGoopManagerRebuildMeshUvsAndColorsPatch(DeadlyDeadlyGoopManager __instance, int chunkX, int chunkY)
    {
        Mesh chunkMesh = __instance.GetChunkMesh(chunkX, chunkY);
        for (int i = 0; i < __instance.m_colorArray.Length; i++)
          __instance.m_colorArray[i].a = 0;

        int chunkSize   = Mathf.RoundToInt(__instance.CHUNK_SIZE / GOOP_GRID_SIZE);
        int xmin        = chunkX * chunkSize;
        int xmax        = xmin   + chunkSize;
        int ymin        = chunkY * chunkSize;
        int ymax        = ymin   + chunkSize;
        var goopedCells = __instance.m_goopedCells;

        DeadlyDeadlyGoopManager.GoopPositionData goopData = default;
        IntVector2 goopPos                                = default;
        Vector2 defaultUv                                 = __instance.m_uvMap[-1];
        int numUvOptions                                  = __instance.m_centerUVOptions.Count;
        Vector2 uvVec                                     = default;

        ExtraGoopData egd = ExtraGoopData.Get(__instance);
        UInt64[,,] goopBitfield = egd.goopedCellBitfield;
        int bitOffset = -1;
        for (int j = xmin; j < xmax; j++)
        {
          goopPos.x = j;
          for (int k = ymin; k < ymax; k++)
          {
            ++bitOffset;
            if ((goopBitfield[chunkX, chunkY, bitOffset / 64] & (1ul << (bitOffset % 64))) == 0)
              continue; // skip grid lookup if the cell definitely isn't gooped

            goopPos.y = k;
            goopData = egd.goopedCellGrid[j, k];
            if (goopData == null || goopData.remainingLifespan <= 0f)
              continue;

            int bi = goopData.baseIndex;
            if (bi < 0)
              bi = goopData.baseIndex = __instance.GetGoopBaseIndex(goopPos, chunkX, chunkY);

            if (goopData.NeighborsAsInt == 255)
              uvVec = __instance.m_centerUVOptions[(int)(numUvOptions * goopPos.GetHashedRandomValue())];
            else if (!__instance.m_uvMap.TryGetValue(goopData.NeighborsAsInt, out uvVec))
              if (!__instance.m_uvMap.TryGetValue(goopData.NeighborsAsIntFuckDiagonals, out uvVec))
                uvVec = defaultUv;

            __instance.m_uvArray[bi]     = uvVec;
            __instance.m_uvArray[bi + 1] = new Vector2(uvVec.x + 0.125f, uvVec.y + 0f);
            __instance.m_uvArray[bi + 2] = new Vector2(uvVec.x + 0f,     uvVec.y + 0.125f);
            __instance.m_uvArray[bi + 3] = new Vector2(uvVec.x + 0.125f, uvVec.y + 0.125f);
            if (__instance.goopDefinition.CanBeFrozen)
            {
              Vector2 frozenVec = new Vector2(goopData.IsFrozen ? 1 : 0, 0f);
              __instance.m_uv2Array[bi]     = frozenVec;
              __instance.m_uv2Array[bi + 1] = frozenVec;
              __instance.m_uv2Array[bi + 2] = frozenVec;
              __instance.m_uv2Array[bi + 3] = frozenVec;
            }
            __instance.AssignVertexColors(goopData, goopPos, chunkX, chunkY);
          }
        }
        chunkMesh.uv       = __instance.m_uvArray;
        chunkMesh.uv2      = __instance.m_uv2Array;
        chunkMesh.colors32 = __instance.m_colorArray;
        return false;    // skip the original method
    }

    [HarmonyPatch(typeof(DeadlyDeadlyGoopManager), nameof(DeadlyDeadlyGoopManager.LateUpdate))]
    [HarmonyPrefix]
    private static bool DeadlyDeadlyGoopManagerLateUpdatePatch(DeadlyDeadlyGoopManager __instance)
    {

      if (Time.timeScale <= 0f || GameManager.Instance.IsPaused)
        return false;

      __instance.m_removalPositions.Clear();
      bool flag = false;
      bool flag2 = false;
      __instance.m_currentUpdateBin = (__instance.m_currentUpdateBin + 1) % 4;
      __instance.m_deltaTimes.Enqueue(BraveTime.DeltaTime);
      float num = 0f;
      for (int i = 0; i < __instance.m_deltaTimes.Count; i++)
        num += __instance.m_deltaTimes[i];

      ExtraGoopData egd = ExtraGoopData.Get(__instance);
      int numGoopedCells = egd.allGoopedCells.Count;
      for (int g = 0; g < numGoopedCells; ++g)
      {
        GoopPositionData goopPositionData = egd.allGoopedCells[g];
        if (goopPositionData.GoopUpdateBin != __instance.m_currentUpdateBin)
          continue;

        IntVector2 goopedPosition = goopPositionData.goopPosition;
        goopPositionData.unfrozeLastFrame = false;
        if (__instance.goopDefinition.usesAmbientGoopFX && goopPositionData.remainingLifespan > 0f && UnityEngine.Random.value < __instance.goopDefinition.ambientGoopFXChance && goopPositionData.SupportsAmbientVFX)
        {
          Vector3 position = goopedPosition.ToVector3(goopedPosition.y) * GOOP_GRID_SIZE;
          __instance.goopDefinition.ambientGoopFX.SpawnAtPosition(position);
        }
        if (!goopPositionData.IsOnFire && !goopPositionData.IsElectrified && !__instance.goopDefinition.usesLifespan && !goopPositionData.lifespanOverridden && !goopPositionData.selfIgnites)
        {
          continue;
        }
        if (goopPositionData.selfIgnites)
        {
          if (goopPositionData.remainingTimeTilSelfIgnition <= 0f)
          {
            goopPositionData.selfIgnites = false;
            __instance.IgniteCell(goopedPosition);
          }
          else
          {
            goopPositionData.remainingTimeTilSelfIgnition -= num;
          }
        }
        if (goopPositionData.remainingLifespan > 0f)
        {
          if (!goopPositionData.IsFrozen)
          {
            goopPositionData.remainingLifespan -= num;
          }
          else
          {
            goopPositionData.remainingFreezeTimer -= num;
            if (goopPositionData.remainingFreezeTimer <= 0f)
            {
              goopPositionData.hasBeenFrozen = 1;
              goopPositionData.remainingLifespan = Mathf.Min(goopPositionData.remainingLifespan, __instance.goopDefinition.fadePeriod);
              goopPositionData.remainingLifespan -= num;
            }
          }
          if (__instance.goopDefinition.usesAcidAudio)
          {
            flag2 = true;
          }
          if (goopPositionData.remainingLifespan < __instance.goopDefinition.fadePeriod && goopPositionData.IsElectrified)
          {
            goopPositionData.remainingLifespan = __instance.goopDefinition.fadePeriod;
          }
          if (goopPositionData.remainingLifespan < __instance.goopDefinition.fadePeriod || goopPositionData.remainingLifespan <= 0f)
          {
            __instance.SetDirty(goopedPosition);
            goopPositionData.IsOnFire = false;
            goopPositionData.IsElectrified = false;
            goopPositionData.HasPlayedFireIntro = false;
            goopPositionData.HasPlayedFireOutro = false;
            if (goopPositionData.remainingLifespan <= 0f)
            {
              __instance.m_removalPositions.Add(goopedPosition);
              continue;
            }
          }
          if (goopPositionData.IsElectrified)
          {
            goopPositionData.remainingElectrifiedTime -= num;
            goopPositionData.remainingElecTimer -= num;
            if (goopPositionData.remainingElectrifiedTime <= 0f)
            {
              goopPositionData.IsElectrified = false;
              goopPositionData.remainingElectrifiedTime = 0f;
            }
            if (goopPositionData.IsElectrified && __instance.m_elecSystem != null && goopPositionData.remainingElecTimer <= 0f && goopedPosition.x % 2 == 0 && goopedPosition.y % 2 == 0)
            {
              Vector3 vector = goopedPosition.ToVector3(goopedPosition.y) * GOOP_GRID_SIZE + new Vector3(UnityEngine.Random.Range(0.125f, 0.375f), UnityEngine.Random.Range(0.125f, 0.375f), 0.125f).Quantize(0.0625f);
              float num2 = UnityEngine.Random.Range(0.75f, 1.5f);
              if (UnityEngine.Random.value < 0.1f)
              {
                #pragma warning disable CS0618
                ParticleSystem.EmitParams emitParams = default(ParticleSystem.EmitParams);
                emitParams.position = vector;
                emitParams.velocity = Vector3.zero;
                emitParams.startSize = __instance.m_fireSystem.startSize;
                emitParams.startLifetime = num2;
                emitParams.startColor = __instance.m_fireSystem.startColor;
                emitParams.randomSeed = (uint)(UnityEngine.Random.value * 4.2949673E+09f);
                #pragma warning restore CS0618
                ParticleSystem.EmitParams emitParams2 = emitParams;
                __instance.m_elecSystem.Emit(emitParams2, 1);
                if (GameManager.Options.ShaderQuality != 0 && GameManager.Options.ShaderQuality != GameOptions.GenericHighMedLowOption.VERY_LOW)
                {
                  int num3 = ((GameManager.Options.ShaderQuality != GameOptions.GenericHighMedLowOption.MEDIUM) ? 10 : 4);
                  GlobalSparksDoer.DoRandomParticleBurst(num3, vector + new Vector3(-0.625f, -0.625f, 0f), vector + new Vector3(0.625f, 0.625f, 0f), Vector3.up, 120f, 0.5f);
                }
              }
              goopPositionData.remainingElecTimer = num2 - 0.1f;
            }
          }
          if (goopPositionData.IsFrozen)
          {
            if (goopPositionData.totalOnFireTime < 0.5f || goopPositionData.remainingLifespan < __instance.goopDefinition.fadePeriod)
            {
              __instance.SetColorDirty(goopedPosition);
            }
            goopPositionData.totalOnFireTime += num;
            if (goopPositionData.totalOnFireTime >= __instance.goopDefinition.freezeSpreadTime)
            {
              for (int j = 0; j < goopPositionData.neighborGoopData.Length; j++)
              {
                if (goopPositionData.neighborGoopData[j] != null && !goopPositionData.neighborGoopData[j].IsFrozen && goopPositionData.neighborGoopData[j].hasBeenFrozen == 0)
                {
                  if (UnityEngine.Random.value < 0.2f)
                  {
                    __instance.FreezeCell(goopPositionData.neighborGoopData[j].goopPosition);
                  }
                  else
                  {
                    goopPositionData.totalFrozenTime = 0f;
                  }
                }
              }
            }
          }
          if (goopPositionData.IsOnFire)
          {
            flag = true;
            __instance.SetColorDirty(goopedPosition);
            goopPositionData.remainingFireTimer -= num;
            goopPositionData.totalOnFireTime += num;
            if (goopPositionData.totalOnFireTime >= __instance.goopDefinition.igniteSpreadTime)
            {
              for (int k = 0; k < goopPositionData.neighborGoopData.Length; k++)
              {
                if (goopPositionData.neighborGoopData[k] != null && !goopPositionData.neighborGoopData[k].IsOnFire)
                {
                  if (UnityEngine.Random.value < 0.2f)
                  {
                    __instance.IgniteCell(goopPositionData.neighborGoopData[k].goopPosition);
                  }
                  else
                  {
                    goopPositionData.totalOnFireTime = 0f;
                  }
                }
              }
            }
          }
          if (!(__instance.m_fireSystem != null) || !goopPositionData.IsOnFire || !(goopPositionData.remainingFireTimer <= 0f) || goopedPosition.x % 2 != 0 || goopedPosition.y % 2 != 0)
          {
            continue;
          }
          Vector3 vector2 = goopedPosition.ToVector3(goopedPosition.y) * GOOP_GRID_SIZE + new Vector3(UnityEngine.Random.Range(0.125f, 0.375f), UnityEngine.Random.Range(0.125f, 0.375f), 0.125f).Quantize(0.0625f);
          float num4 = UnityEngine.Random.Range(1f, 1.5f);
          float num5 = UnityEngine.Random.Range(0.75f, 1f);
          if (!goopPositionData.HasPlayedFireOutro)
          {
            #pragma warning disable CS0618
            if (!goopPositionData.HasPlayedFireOutro && goopPositionData.remainingLifespan <= num5 + __instance.goopDefinition.fadePeriod && __instance.m_fireOutroSystem != null)
            {
              num4 = num5;
              ParticleSystem.EmitParams emitParams = default(ParticleSystem.EmitParams);
              emitParams.position = vector2;
              emitParams.velocity = Vector3.zero;
              emitParams.startSize = __instance.m_fireSystem.startSize;
              emitParams.startLifetime = num5;
              emitParams.startColor = __instance.m_fireSystem.startColor;
              emitParams.randomSeed = (uint)(UnityEngine.Random.value * 4.2949673E+09f);
              ParticleSystem.EmitParams emitParams3 = emitParams;
              __instance.m_fireOutroSystem.Emit(emitParams3, 1);
              goopPositionData.HasPlayedFireOutro = true;
            }
            else if (!goopPositionData.HasPlayedFireIntro && __instance.m_fireIntroSystem != null)
            {
              num4 = UnityEngine.Random.Range(0.75f, 1f);
              ParticleSystem.EmitParams emitParams = default(ParticleSystem.EmitParams);
              emitParams.position = vector2;
              emitParams.velocity = Vector3.zero;
              emitParams.startSize = __instance.m_fireSystem.startSize;
              emitParams.startLifetime = num4;
              emitParams.startColor = __instance.m_fireSystem.startColor;
              emitParams.randomSeed = (uint)(UnityEngine.Random.value * 4.2949673E+09f);
              ParticleSystem.EmitParams emitParams4 = emitParams;
              __instance.m_fireIntroSystem.Emit(emitParams4, 1);
              goopPositionData.HasPlayedFireIntro = true;
            }
            else
            {
              if (UnityEngine.Random.value < 0.5f)
              {
                ParticleSystem.EmitParams emitParams = default(ParticleSystem.EmitParams);
                emitParams.position = vector2;
                emitParams.velocity = Vector3.zero;
                emitParams.startSize = __instance.m_fireSystem.startSize;
                emitParams.startLifetime = num4;
                emitParams.startColor = __instance.m_fireSystem.startColor;
                emitParams.randomSeed = (uint)(UnityEngine.Random.value * 4.2949673E+09f);
                ParticleSystem.EmitParams emitParams5 = emitParams;
                __instance.m_fireSystem.Emit(emitParams5, 1);
              }
              GlobalSparksDoer.DoRandomParticleBurst(UnityEngine.Random.Range(3, 6), vector2, vector2, Vector3.up * 2f, 30f, 1f, null, UnityEngine.Random.Range(0.5f, 1f), (!__instance.goopDefinition.UsesGreenFire) ? Color.red : Color.green);
            }
            #pragma warning restore CS0618
          }
          goopPositionData.remainingFireTimer = num4 - 0.125f;
        }
        else
        {
          __instance.m_removalPositions.Add(goopedPosition);
        }
      }
      if (flag && !__instance.m_isPlayingFireAudio)
      {
        __instance.m_isPlayingFireAudio = true;
        AkSoundEngine.PostEvent("Play_ENV_oilfire_ignite_01", GameManager.Instance.PrimaryPlayer.gameObject);
      }
      else if (!flag && __instance.m_isPlayingFireAudio)
      {
        __instance.m_isPlayingFireAudio = false;
        AkSoundEngine.PostEvent("Stop_ENV_oilfire_loop_01", GameManager.Instance.PrimaryPlayer.gameObject);
      }
      if (flag2 && !__instance.m_isPlayingAcidAudio)
      {
        __instance.m_isPlayingAcidAudio = true;
        AkSoundEngine.PostEvent("Play_ENV_acidsizzle_loop_01", GameManager.Instance.PrimaryPlayer.gameObject);
      }
      else if (!flag2 && __instance.m_isPlayingAcidAudio)
      {
        __instance.m_isPlayingAcidAudio = false;
        AkSoundEngine.PostEvent("Stop_ENV_acidsizzle_loop_01", GameManager.Instance.PrimaryPlayer.gameObject);
      }
      __instance.RemoveGoopedPosition(__instance.m_removalPositions);
      for (int l = 0; l < __instance.m_dirtyFlags.GetLength(0); l++)
      {
        for (int m = 0; m < __instance.m_dirtyFlags.GetLength(1); m++)
        {
          if (__instance.m_dirtyFlags[l, m])
          {
            int num6 = (m * __instance.m_dirtyFlags.GetLength(0) + l) % 3;
            if (num6 == Time.frameCount % 3)
            {
              bool flag3 = __instance.HasGoopedPositionCountForChunk(l, m);
              if (flag3)
              {
                __instance.RebuildMeshUvsAndColors(l, m);
              }
              __instance.m_dirtyFlags[l, m] = false;
              __instance.m_colorDirtyFlags[l, m] = false;
              if (__instance.m_meshes[l, m] != null && !flag3)
              {
                UnityEngine.Object.Destroy(__instance.m_mrs[l, m].gameObject);
                UnityEngine.Object.Destroy(__instance.m_meshes[l, m]);
                __instance.m_mrs[l, m] = null;
                __instance.m_meshes[l, m] = null;
              }
            }
          }
          else
          {
            if (!__instance.m_colorDirtyFlags[l, m])
            {
              continue;
            }
            int num7 = (m * __instance.m_dirtyFlags.GetLength(0) + l) % 3;
            if (num7 == Time.frameCount % 3)
            {
              bool flag4 = __instance.HasGoopedPositionCountForChunk(l, m);
              if (flag4)
              {
                __instance.RebuildMeshColors(l, m);
              }
              __instance.m_colorDirtyFlags[l, m] = false;
              if (__instance.m_meshes[l, m] != null && !flag4)
              {
                UnityEngine.Object.Destroy(__instance.m_mrs[l, m].gameObject);
                UnityEngine.Object.Destroy(__instance.m_meshes[l, m]);
                __instance.m_mrs[l, m] = null;
                __instance.m_meshes[l, m] = null;
              }
            }
          }
        }
      }

      return false;
    }

    // private const BindingFlags _ANY_FLAGS
    //   = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    // private static readonly Type _IntVecHashSetType
    //   = typeof(HashSet<>).MakeGenericType(typeof(IntVector2));
    // private static readonly Type _IntVecHashSetEnumeratorType
    //   = typeof(HashSet<>).GetNestedType("Enumerator", _ANY_FLAGS).MakeGenericType(typeof(IntVector2));
    // private static readonly MethodInfo _IntVecHashSetEnumeratorCurrent
    //   = _IntVecHashSetEnumeratorType.GetMethod("get_Current", _ANY_FLAGS);
    // private static readonly MethodInfo _IntVecHashSetEnumeratorMoveNext
    //   = _IntVecHashSetEnumeratorType.GetMethod("MoveNext", _ANY_FLAGS);
    // private static readonly Type _IntVecDictType
    //   = typeof(Dictionary<,>).MakeGenericType(typeof(IntVector2), typeof(GoopPositionData));
    // private static readonly Type _IntVecKVPType
    //   = typeof(KeyValuePair<,>).MakeGenericType(typeof(IntVector2), typeof(GoopPositionData));
    // private static readonly Type _IntVecDictEnumeratorType
    //   = typeof(Dictionary<,>).GetNestedType("Enumerator", _ANY_FLAGS).MakeGenericType(typeof(IntVector2), typeof(GoopPositionData));
    // private static readonly MethodInfo _IntVecDictEnumeratorCurrent
    //   = _IntVecDictEnumeratorType.GetMethod("get_Current", _ANY_FLAGS);
    // private static readonly MethodInfo _IntVecDictEnumeratorMoveNext
    //   = _IntVecDictEnumeratorType.GetMethod("MoveNext", _ANY_FLAGS);
    // private static readonly MethodInfo _Dispose
    //   = typeof(IDisposable).GetMethod(nameof(IDisposable.Dispose));

    /// <summary>Replace expensiv hashset iteration -> dictionary lookups with dictionary iteration</summary>
    // [HarmonyPatch(typeof(DeadlyDeadlyGoopManager), nameof(DeadlyDeadlyGoopManager.LateUpdate))]
    // [HarmonyILManipulator]
    // private static void DeadlyDeadlyGoopManagerLateUpdatePatchIL(ILContext il)
    // {
    //     ILCursor cursor = new ILCursor(il);

    //     if (!cursor.TryGotoNext(MoveType.AfterLabel,
    //       instr => instr.MatchLdarg(0), // the DeadlyDeadlyGoopManager instance
    //       instr => instr.MatchLdfld<DeadlyDeadlyGoopManager>("m_goopedPositions"),
    //       instr => instr.MatchCallvirt(_IntVecHashSetType.GetMethod("GetEnumerator")),
    //       instr => instr.MatchStloc(5) // m_goopedPositions foreach enumerator
    //       ))
    //     {
    //       GGVDebug.Log($"  ddgm patch failed at point 1");
    //       return;
    //     }

    //     VariableDefinition intVecDictEnumerator = il.DeclareLocal(_IntVecDictEnumeratorType);
    //     cursor.Emit(OpCodes.Ldarg_0);
    //     cursor.Emit(OpCodes.Ldfld, typeof(DeadlyDeadlyGoopManager).GetField("m_goopedCells", _ANY_FLAGS));
    //     cursor.Emit(OpCodes.Callvirt, _IntVecDictType.GetMethod("GetEnumerator"));
    //     cursor.Emit(OpCodes.Stloc, intVecDictEnumerator);
    //     cursor.RemoveRange(4); //NOTE: important to RemoveRange AFTER emitting new instructions...removing the old instructions first causes issues

    //     if (!cursor.TryGotoNext(MoveType.AfterLabel,
    //       instr => instr.MatchLdloca(5), // m_goopedPositions foreach enumerator
    //       instr => instr.MatchCall(_IntVecHashSetEnumeratorCurrent),
    //       instr => instr.MatchStloc(4), // IntVector2 value of enumerator == goopedPosition
    //       instr => instr.MatchLdarg(0), // the DeadlyDeadlyGoopManager instance
    //       instr => instr.MatchLdfld<DeadlyDeadlyGoopManager>("m_goopedCells"),
    //       instr => instr.MatchLdloc(4), // IntVector2 value of enumerator
    //       instr => instr.MatchCallvirt(_IntVecDictType.GetMethod("get_Item")),
    //       instr => instr.MatchStloc(6) // GoopPositionData for the IntVector2
    //       ))
    //     {
    //       GGVDebug.Log($"  ddgm patch failed at point 2");
    //       return;
    //     }
    //     VariableDefinition intVecDictKVP = il.DeclareLocal(_IntVecKVPType);
    //     cursor.Emit(OpCodes.Ldloca, intVecDictEnumerator); // m_goopedCells foreach enumerator
    //     cursor.Emit(OpCodes.Call, _IntVecDictEnumeratorCurrent);
    //     cursor.Emit(OpCodes.Stloc, intVecDictKVP); // store the kvp

    //     cursor.Emit(OpCodes.Ldloca, intVecDictKVP); // load the kvp
    //     cursor.Emit(OpCodes.Call, _IntVecKVPType.GetMethod("get_Key", _ANY_FLAGS));
    //     cursor.Emit(OpCodes.Stloc, 4); // store the key in goopedPosition

    //     cursor.Emit(OpCodes.Ldloca, intVecDictKVP); // load the kvp
    //     cursor.Emit(OpCodes.Call, _IntVecKVPType.GetMethod("get_Value", _ANY_FLAGS));
    //     cursor.Emit(OpCodes.Stloc, 6); // store the value in goopPositionData

    //     cursor.RemoveRange(8);

    //     if (!cursor.TryGotoNext(MoveType.AfterLabel,
    //       instr => instr.MatchLdloca(5), // m_goopedPositions foreach enumerator
    //       instr => instr.MatchCall(_IntVecHashSetEnumeratorMoveNext)
    //       ))
    //     {
    //       GGVDebug.Log($"  ddgm patch failed at point 3");
    //       return;
    //     }
    //     cursor.Emit(OpCodes.Ldloca, intVecDictEnumerator); // m_goopedCells foreach enumerator
    //     cursor.Emit(OpCodes.Call, _IntVecDictEnumeratorMoveNext);
    //     cursor.RemoveRange(2); // don't remove old instructions until AFTER the loop iteration is over or jump labels get messed up

    //     if (!cursor.TryGotoNext(MoveType.AfterLabel,
    //       // instr => instr.MatchLdloca(5), // m_goopedPositions foreach enumerator
    //       // instr => instr.MatchConstrained(_IntVecHashSetEnumeratorType),
    //       instr => instr.MatchCallvirt(_Dispose) // no othe Dispose() method, so this is safe (tm)
    //       ))
    //     {
    //       GGVDebug.Log($"  ddgm patch failed at point 4");
    //       return;
    //     }
    //     //WARNING: we get into deep trouble toying with finalizers...just pop the address of the old enumerator and replace it with our own
    //     cursor.Emit(OpCodes.Pop); // pop the HashSet Ienumerator
    //     cursor.Emit(OpCodes.Ldloca, intVecDictEnumerator); // load our own m_goopedCells foreach enumerator
    //     cursor.Emit(OpCodes.Constrained, _IntVecDictEnumeratorType); // load our own constrained type
    //     // we reuse the old dispose method, so we're done
    // }

    private static readonly Color32 _Transparent = new Color32(0, 0, 0, 0);
    //NOTE: I could possibly reuse a tweaked version of the LateUpdate() ILManipulator, but...it's really not worth it
    [HarmonyPatch(typeof(DeadlyDeadlyGoopManager), nameof(DeadlyDeadlyGoopManager.RebuildMeshColors))]
    [HarmonyPrefix]
    private static bool DeadlyDeadlyGoopManagerRebuildMeshColorsPatch(DeadlyDeadlyGoopManager __instance, int chunkX, int chunkY)
    {
        for (int i = 0; i < __instance.m_colorArray.Length; i++)
          __instance.m_colorArray[i] = _Transparent;

        int chunkSize = Mathf.RoundToInt(__instance.CHUNK_SIZE / GOOP_GRID_SIZE);
        int xmin      = chunkX * chunkSize;
        int xmax      = xmin   + chunkSize;
        int ymin      = chunkY * chunkSize;
        int ymax      = ymin   + chunkSize;
        VertexColorRebuildResult b = VertexColorRebuildResult.ALL_OK;

        ExtraGoopData egd = ExtraGoopData.Get(__instance);
        UInt64[,,] goopBitfield = egd.goopedCellBitfield;
        int bitOffset = -1;
        for (int j = xmin; j < xmax; j++)
        {
          for (int k = ymin; k < ymax; k++)
          {
            ++bitOffset;
            if ((goopBitfield[chunkX, chunkY, bitOffset / 64] & (1ul << (bitOffset % 64))) == 0)
              continue; // skip grid lookup if the cell definitely isn't gooped

            GoopPositionData goopPositionData = egd.goopedCellGrid[j, k];
            if (goopPositionData == null || goopPositionData.remainingLifespan < 0f)
              continue;

            int bi = goopPositionData.baseIndex;
            if (bi < 0)
              bi = goopPositionData.baseIndex = __instance.GetGoopBaseIndex(goopPositionData.goopPosition, chunkX, chunkY);

            if (__instance.goopDefinition.CanBeFrozen)
            {
              Vector2 v = new Vector2((goopPositionData.IsFrozen ? 1 : 0), 0f);
              __instance.m_uv2Array[bi    ] = v;
              __instance.m_uv2Array[bi + 1] = v;
              __instance.m_uv2Array[bi + 2] = v;
              __instance.m_uv2Array[bi + 3] = v;
            }
            VertexColorRebuildResult a = __instance.AssignVertexColors(goopPositionData, goopPositionData.goopPosition, chunkX, chunkY);
            if ((int)a > (int)b)
              b = a;
          }
        }

        Mesh chunkMesh = __instance.GetChunkMesh(chunkX, chunkY);
        if (__instance.goopDefinition.CanBeFrozen)
          chunkMesh.uv2 = __instance.m_uv2Array;
        chunkMesh.colors32 = __instance.m_colorArray;

        return false; // skip the original method
    }

    [HarmonyPatch(typeof(DeadlyDeadlyGoopManager), nameof(DeadlyDeadlyGoopManager.RemoveGoopedPosition), new[]{typeof(IntVector2)})]
    [HarmonyPrefix]
    private static bool DeadlyDeadlyGoopManagerRemoveGoopedPositionPatch(DeadlyDeadlyGoopManager __instance, IntVector2 entry)
    {
      // if goop data is defined for the current position, we can just look at our neighbors and don't need 8 dictionary lookups
      if (__instance.m_goopedCells.TryGetValue(entry, out GoopPositionData current))
      {
        for (int i = 0; i < 8; ++i)
        {
          GoopPositionData neighbor = current.neighborGoopData[i];
          if (neighbor == null)
            continue;
          int ni = (i + 4) % 8;
          neighbor.neighborGoopData[ni] = null;
          neighbor.NeighborsAsIntFuckDiagonals = (neighbor.NeighborsAsInt &= ~(1 << 7 - ni)) & 0xAA;
        }
      }
      else // revert to vanilla behavior
      {
        for (int i = 0; i < 8; i++)
        {
          if (!__instance.m_goopedCells.TryGetValue(entry + IntVector2.CardinalsAndOrdinals[i], out GoopPositionData neighbor))
            continue;
          int ni = (i + 4) % 8;
          neighbor.neighborGoopData[ni] = null;
          neighbor.NeighborsAsIntFuckDiagonals = (neighbor.NeighborsAsInt &= ~(1 << 7 - ni)) & 0xAA;
        }
        current = null;
      }
      __instance.m_goopedPositions.Remove(entry);
      __instance.m_goopedCells.Remove(entry);
      DeadlyDeadlyGoopManager.allGoopPositionMap.Remove(entry);
      __instance.SetDirty(entry);
      if (current != null)
        ExtraGoopData.Get(__instance).RemoveGoop(current);
      return false;    // skip the original method
    }

    [HarmonyPatch(typeof(DeadlyDeadlyGoopManager), nameof(DeadlyDeadlyGoopManager.HasGoopedPositionCountForChunk))]
    [HarmonyPrefix]
    private static bool FastHasGoopedPositionCountForChunk(DeadlyDeadlyGoopManager __instance, int chunkX, int chunkY, ref bool __result)
    {
      ExtraGoopData egd = ExtraGoopData.Get(__instance);
      for (int i = 0; i < 7; ++i)
        if (egd.goopedCellBitfield[chunkX, chunkY, i] > 0)
        {
          __result = true;
          return false;
        }

      __result = false;
      return false;
    }

    /// <summary>Removes a lot of unnecessary function calls.</summary>
    [HarmonyPatch(typeof(DeadlyDeadlyGoopManager), nameof(DeadlyDeadlyGoopManager.SetDirty))]
    [HarmonyPrefix]
    private static bool DeadlyDeadlyGoopManagerSetDirtyPatch(DeadlyDeadlyGoopManager __instance, IntVector2 goopPosition)
    {
      int x = ((int)(goopPosition.x * GOOP_GRID_SIZE)) / __instance.CHUNK_SIZE;
      int y = ((int)(goopPosition.y * GOOP_GRID_SIZE)) / __instance.CHUNK_SIZE;
      bool[,] dirtyFlags = __instance.m_dirtyFlags;
      int w = dirtyFlags.GetLength(0);
      int h = dirtyFlags.GetLength(1);
      if (x < 0 || x >= w || y < 0 || y >= h)
        return false;

      int chunkSize    = (int)(__instance.CHUNK_SIZE / GOOP_GRID_SIZE);
      bool leftDirty   = x > 0     && goopPosition.x % chunkSize == 0;
      bool rightDirty  = x < w - 1 && goopPosition.x % chunkSize == chunkSize - 1;
      bool bottomDirty = y > 0     && goopPosition.y % chunkSize == 0;
      bool topDirty    = y < h - 1 && goopPosition.y % chunkSize == chunkSize - 1;
      dirtyFlags[x, y] = true;

      if (leftDirty)                 dirtyFlags[x - 1, y    ] = true;
      if (rightDirty)                dirtyFlags[x + 1, y    ] = true;
      if (bottomDirty)               dirtyFlags[x,     y - 1] = true;
      if (topDirty)                  dirtyFlags[x,     y + 1] = true;
      if (leftDirty && bottomDirty)  dirtyFlags[x - 1, y - 1] = true;
      if (leftDirty && topDirty)     dirtyFlags[x - 1, y + 1] = true;
      if (rightDirty && bottomDirty) dirtyFlags[x + 1, y - 1] = true;
      if (rightDirty && topDirty)    dirtyFlags[x + 1, y + 1] = true;

      return false;
    }

    /// <summary>Inline a bunch of function calls for performance.</summary>
    [HarmonyPatch(typeof(DeadlyDeadlyGoopManager), nameof(DeadlyDeadlyGoopManager.AssignVertexColors))]
    [HarmonyPrefix]
    private static bool DeadlyDeadlyGoopManagerAssignVertexColorsPatch(DeadlyDeadlyGoopManager __instance, GoopPositionData goopData, IntVector2 goopPos, int chunkX, int chunkY, ref VertexColorRebuildResult __result)
    {
      bool onFireOrFrozenNeighbor = false;
      Color32 color  = __instance.goopDefinition.baseColor32;
      Color32 color2 = color;
      Color32 color3 = color;
      Color32 color4 = color;
      int bi = goopData.baseIndex;

      if (goopData.IsOnFire)
        color = __instance.goopDefinition.fireColor32;
      else
      {
        Color32 igniteColor = __instance.goopDefinition.igniteColor32;
        for (int i = 0; i < 8; i++)
        {
          if (goopData.neighborGoopData[i] == null || !goopData.neighborGoopData[i].IsOnFire)
            continue;

          onFireOrFrozenNeighbor = true;
          switch (i)
          {
          case 0:
            color3 = igniteColor;
            color4 = igniteColor;
            break;
          case 1:
            color4 = igniteColor;
            break;
          case 2:
            color4 = igniteColor;
            color2 = igniteColor;
            break;
          case 3:
            color2 = igniteColor;
            break;
          case 4:
            color2 = igniteColor;
            color = igniteColor;
            break;
          case 5:
            color = igniteColor;
            break;
          case 6:
            color = igniteColor;
            color3 = igniteColor;
            break;
          case 7:
            color3 = igniteColor;
            break;
          }
        }
      }

      if (!goopData.IsOnFire && !onFireOrFrozenNeighbor)
      {
        if (goopData.IsFrozen)
          color = __instance.goopDefinition.frozenColor32;
        else
        {
          Vector2 uvVec = new Vector2(0.5f, 0f);
          Color32 prefreezeColor = __instance.goopDefinition.prefreezeColor32;
          for (int j = 0; j < 8; j++)
          {
            if (goopData.neighborGoopData[j] == null || !goopData.neighborGoopData[j].IsFrozen)
              continue;

            onFireOrFrozenNeighbor = true;
            switch (j)
            {
            case 0:
              __instance.m_uv2Array[bi + 2] = uvVec;
              color3 = prefreezeColor;
              __instance.m_uv2Array[bi + 3] = uvVec;
              color4 = prefreezeColor;
              break;
            case 1:
              __instance.m_uv2Array[bi + 3] = uvVec;
              color4 = prefreezeColor;
              break;
            case 2:
              __instance.m_uv2Array[bi + 3] = uvVec;
              color4 = prefreezeColor;
              __instance.m_uv2Array[bi + 1] = uvVec;
              color2 = prefreezeColor;
              break;
            case 3:
              __instance.m_uv2Array[bi + 1] = uvVec;
              color2 = prefreezeColor;
              break;
            case 4:
              __instance.m_uv2Array[bi + 1] = uvVec;
              color2 = prefreezeColor;
              __instance.m_uv2Array[bi] = uvVec;
              color = prefreezeColor;
              break;
            case 5:
              __instance.m_uv2Array[bi] = uvVec;
              color = prefreezeColor;
              break;
            case 6:
              __instance.m_uv2Array[bi] = uvVec;
              color = prefreezeColor;
              __instance.m_uv2Array[bi + 2] = uvVec;
              color3 = prefreezeColor;
              break;
            case 7:
              __instance.m_uv2Array[bi + 2] = uvVec;
              color3 = prefreezeColor;
              break;
            }
          }
        }
      }

      if (goopData.remainingLifespan < __instance.goopDefinition.fadePeriod)
      {
        float t = goopData.remainingLifespan / __instance.goopDefinition.fadePeriod;
        Color32 fc = __instance.goopDefinition.fadeColor32;
        // inlined lerping woo
        color = new Color32(
          (byte)(fc.r + (color.r - fc.r) * t),
          (byte)(fc.g + (color.g - fc.g) * t),
          (byte)(fc.b + (color.b - fc.b) * t),
          (byte)(fc.a + (color.a - fc.a) * t));
        if (onFireOrFrozenNeighbor)
        {
          color2 = new Color32(
            (byte)(fc.r + (color2.r - fc.r) * t),
            (byte)(fc.g + (color2.g - fc.g) * t),
            (byte)(fc.b + (color2.b - fc.b) * t),
            (byte)(fc.a + (color2.a - fc.a) * t));
          color3 = new Color32(
            (byte)(fc.r + (color3.r - fc.r) * t),
            (byte)(fc.g + (color3.g - fc.g) * t),
            (byte)(fc.b + (color3.b - fc.b) * t),
            (byte)(fc.a + (color3.a - fc.a) * t));
          color4 = new Color32(
            (byte)(fc.r + (color4.r - fc.r) * t),
            (byte)(fc.g + (color4.g - fc.g) * t),
            (byte)(fc.b + (color4.b - fc.b) * t),
            (byte)(fc.a + (color4.a - fc.a) * t));
        }
      }
      if (onFireOrFrozenNeighbor)
      {
        __instance.m_colorArray[bi] = color;
        __instance.m_colorArray[bi + 1] = color2;
        __instance.m_colorArray[bi + 2] = color3;
        __instance.m_colorArray[bi + 3] = color4;
      }
      else
      {
        __instance.m_colorArray[bi] = color;
        __instance.m_colorArray[bi + 1] = color;
        __instance.m_colorArray[bi + 2] = color;
        __instance.m_colorArray[bi + 3] = color;
      }

      __result = VertexColorRebuildResult.ALL_OK;
      return false;    // skip the original method
    }

    [HarmonyPatch(typeof(DeadlyDeadlyGoopManager), nameof(DeadlyDeadlyGoopManager.HandleRecursiveElectrification))]
    [HarmonyPostfix]
    private static IEnumerator HandleRecursiveElectrificationPatch(IEnumerator orig, DeadlyDeadlyGoopManager __instance, IntVector2 cellIndex)
    {
      return HandleRecursiveElectrificationFast(__instance, cellIndex);
    }

    private static Queue<GoopPositionData> _GoopsToElectrify = new();
    private static IEnumerator HandleRecursiveElectrificationFast(DeadlyDeadlyGoopManager __instance, IntVector2 cellIndex)
    {
      const float MAX_CELLS_PER_ITER = 200;

      GoopPositionData initialGoopData = __instance.m_goopedCells[cellIndex];
      if (!__instance.goopDefinition.CanBeElectrified || initialGoopData.IsFrozen || initialGoopData.remainingLifespan < __instance.goopDefinition.fadePeriod)
        yield break;

      uint currentSemaphore = initialGoopData.elecTriggerSemaphore = ++__instance.m_lastElecSemaphore;
      int enumeratorCounter = 0;
      _GoopsToElectrify.Enqueue(initialGoopData);
      while (_GoopsToElectrify.Count > 0)
      {
        GoopPositionData curGoopData = _GoopsToElectrify.Dequeue();
        if (curGoopData == null)
          continue;

        if (!curGoopData.IsFrozen && curGoopData.remainingLifespan >= __instance.goopDefinition.fadePeriod)
        {
          if (!curGoopData.IsElectrified)
          {
            curGoopData.IsElectrified = true;
            curGoopData.remainingElecTimer = 0f;
          }
          curGoopData.remainingElectrifiedTime = __instance.goopDefinition.electrifiedTime;
        }

        for (int i = 0; i < 8; i++)
        {
          GoopPositionData neighbor = curGoopData.neighborGoopData[i];
          if (neighbor != null && neighbor.elecTriggerSemaphore < currentSemaphore && (!neighbor.IsElectrified || neighbor.remainingElectrifiedTime < __instance.goopDefinition.electrifiedTime - 0.01f))
          {
            neighbor.elecTriggerSemaphore = currentSemaphore;
            _GoopsToElectrify.Enqueue(neighbor);
          }
        }

        if (++enumeratorCounter > MAX_CELLS_PER_ITER)
        {
          yield return null;
          enumeratorCounter = 0;
        }
      }
    }

    /// <summary>Cache lookup value for __instance.m_goopedCells[pos] and inline a lot of other expensive logic</summary>
    [HarmonyPatch(typeof(DeadlyDeadlyGoopManager), nameof(DeadlyDeadlyGoopManager.AddGoopedPosition))]
    [HarmonyPrefix]
    private static bool DeadlyDeadlyGoopManagerAddGoopedPositionPatch(DeadlyDeadlyGoopManager __instance, IntVector2 pos, float radiusFraction = 0f, bool suppressSplashes = false, int sourceId = -1, int sourceFrameCount = -1)
    {
      if (GameManager.Instance.IsLoadingLevel)
        return false;

      Vector2 worldPos       = new Vector2(pos.x * GOOP_GRID_SIZE, pos.y * GOOP_GRID_SIZE);
      Vector2 worldCenterPos = new Vector2(worldPos.x + GOOP_GRID_SIZE * 0.5f, worldPos.y + GOOP_GRID_SIZE * 0.5f);
      for (int i = 0; i < m_goopExceptions.Count; i++)
      {
        if (m_goopExceptions[i] == null)
          continue;
        Vector2 v = m_goopExceptions[i].First;
        float dx = (v.x - worldCenterPos.x);
        float dy = (v.y - worldCenterPos.y);
        if ((dx * dx + dy * dy) < m_goopExceptions[i].Second)
          return false;
      }

      if (!__instance.m_goopedCells.TryGetValue(pos, out GoopPositionData currentGoop)) // cache currentGoop for the future
      {
        IntVector2 cellPosition = new IntVector2((int)worldPos.x, (int)worldPos.y);
        DungeonData dd = GameManager.Instance.Dungeon.data;
        if (cellPosition.x < 0 || cellPosition.y < 0 || cellPosition.x >= dd.m_width || cellPosition.y >= dd.m_height)
          return false;

        CellData cellData = dd[cellPosition];
        if (cellData == null || cellData.forceDisallowGoop || (cellData.cellVisualData.absorbsDebris && __instance.goopDefinition.CanBeFrozen))
          return false;

        if (__instance.goopDefinition.CanBeFrozen && cellData.doesDamage) // inlined SolidifyLavaInCell() and removed redundant checks
        {
          cellData.doesDamage = false;
          if (dd.m_sizzleSystem == null)
            dd.InitializeSizzleSystem();
          Vector3 particlePos = cellPosition.ToCenterVector3(cellPosition.y);
          dd.SpawnWorldParticle(dd.m_sizzleSystem, particlePos + UnityEngine.Random.insideUnitCircle.ToVector3ZUp() / 3f);
          if (UnityEngine.Random.value < 0.5f)
            dd.SpawnWorldParticle(dd.m_sizzleSystem, particlePos + UnityEngine.Random.insideUnitCircle.ToVector3ZUp() / 3f);
        }

        if (cellData.type != CellType.FLOOR && !cellData.forceAllowGoop) // defer face wall and hash checks until we know we're not on a floor
        {
          if (!cellData.IsLowerFaceWall())
            return false;
          if (pos.GetHashedRandomValue() > 0.75f)
            return false;
        }

        bool wasOnFire = false;
        if (allGoopPositionMap.TryGetValue(pos, out DeadlyDeadlyGoopManager otherGoopManager))
        {
          GoopPositionData goopPositionData = otherGoopManager.m_goopedCells[pos];
          int frameCount = ((sourceFrameCount == -1) ? Time.frameCount : sourceFrameCount);
          if (goopPositionData.frameGooped > frameCount || goopPositionData.eternal)
            return false;
          if (goopPositionData.IsOnFire)
            wasOnFire = true;
          otherGoopManager.RemoveGoopedPosition(pos);
        }

        GoopPositionData newGoop = new GoopPositionData(pos, __instance.m_goopedCells, __instance.goopDefinition.GetLifespan(radiusFraction));
        newGoop.frameGooped = ((sourceFrameCount == -1) ? Time.frameCount : sourceFrameCount);
        newGoop.lastSourceID = sourceId;
        if (!suppressSplashes && m_DoGoopSpawnSplashes && UnityEngine.Random.value < 0.02f)
        {
          if (__instance.m_genericSplashPrefab == null)
            __instance.m_genericSplashPrefab = ResourceCache.Acquire("Global VFX/Generic_Goop_Splash") as GameObject;

          GameObject gameObject = SpawnManager.SpawnVFX(__instance.m_genericSplashPrefab, worldPos.ToVector3ZUp(worldPos.y), Quaternion.identity);
          gameObject.GetComponent<tk2dBaseSprite>().usesOverrideMaterial = true;
          gameObject.GetComponent<Renderer>().material.SetColor(TintColorPropertyID, __instance.goopDefinition.baseColor32);
        }
        newGoop.eternal = __instance.goopDefinition.eternal;
        newGoop.selfIgnites = __instance.goopDefinition.SelfIgnites;
        newGoop.remainingTimeTilSelfIgnition = __instance.goopDefinition.selfIgniteDelay;
        __instance.m_goopedPositions.Add(pos);
        __instance.m_goopedCells.Add(pos, newGoop);
        allGoopPositionMap.Add(pos, __instance);
        ExtraGoopData egd = ExtraGoopData.Get(__instance);
        egd.AddGoop(newGoop);
        RoomHandler absoluteRoomFromPosition = GameManager.Instance.Dungeon.data.GetAbsoluteRoomFromPosition(cellPosition);
        absoluteRoomFromPosition.RegisterGoopManagerInRoom(__instance);

        if (cellData.OnCellGooped != null)
          cellData.OnCellGooped(cellData);
        if (cellData.cellVisualData.floorType == CellVisualData.CellFloorType.Ice)
          __instance.FreezeCell(pos);
        if (wasOnFire && __instance.goopDefinition.CanBeIgnited)
          __instance.IgniteCell(pos);
        __instance.SetDirty(pos);

        return false;
      }

      if (currentGoop.remainingLifespan < __instance.goopDefinition.fadePeriod)
        __instance.SetDirty(pos);

      if (currentGoop.IsOnFire && __instance.goopDefinition.ignitionChangesLifetime)
      {
        if (currentGoop.remainingLifespan > 0f)
          currentGoop.remainingLifespan = __instance.goopDefinition.ignitedLifetime;
      }
      else
      {
        if (!suppressSplashes && m_DoGoopSpawnSplashes && (currentGoop.lastSourceID < 0 || currentGoop.lastSourceID != sourceId) && UnityEngine.Random.value < 0.001f)
        {
          if (__instance.m_genericSplashPrefab == null)
            __instance.m_genericSplashPrefab = ResourceCache.Acquire("Global VFX/Generic_Goop_Splash") as GameObject;
          GameObject gameObject2 = SpawnManager.SpawnVFX(__instance.m_genericSplashPrefab, worldPos.ToVector3ZUp(worldPos.y), Quaternion.identity);
          gameObject2.GetComponent<tk2dBaseSprite>().usesOverrideMaterial = true;
          gameObject2.GetComponent<Renderer>().material.SetColor(TintColorPropertyID, __instance.goopDefinition.baseColor32);
        }
        float newMaxLifespan = __instance.goopDefinition.GetLifespan(radiusFraction);
        if (newMaxLifespan > currentGoop.remainingLifespan)
          currentGoop.remainingLifespan = newMaxLifespan;
        currentGoop.lifespanOverridden = true;
        currentGoop.HasPlayedFireOutro = false;
        currentGoop.hasBeenFrozen = 0;
      }
      currentGoop.lastSourceID = sourceId;

      return false;
    }

    private static float _InvSqrRadius = 1f;

    //                   original: 153,000ns avg
    // with FastGetRadiusFraction:  93,000ns avg
    //         with FastSetCircle:  81,000ns avg
    //         with inline floats:  74,000ns avg
    //           with no division:  62,000ns avg
    [HarmonyPatch(typeof(DeadlyDeadlyGoopManager), nameof(DeadlyDeadlyGoopManager.AddGoopPoints))]
    [HarmonyPrefix]
    private static bool DeadlyDeadlyGoopManagerAddGoopPointsPatch(DeadlyDeadlyGoopManager __instance, List<Vector2> points, float radius, Vector2 excludeCenter, float excludeRadius)
    {
      // System.Diagnostics.Stopwatch gooppointsWatch = System.Diagnostics.Stopwatch.StartNew();
      if (radius == 0f)
        return false;

      float minPointX = points[0].x;
      float maxPointX = points[0].x;
      float minPointY = points[0].y;
      float maxPointY = points[0].y;
      for (int i = 1; i < points.Count; i++)
      {
        if      (points[i].x < minPointX) minPointX = points[i].x;
        else if (points[i].x > maxPointX) maxPointX = points[i].x;
        if      (points[i].y < minPointY) minPointY = points[i].y;
        else if (points[i].y > maxPointY) maxPointY = points[i].y;
      }

      //NOTE: GOOP_GRID_SIZE == 0.25f

      int minX   = Mathf.FloorToInt((minPointX - radius) / GOOP_GRID_SIZE);
      int maxX   = Mathf.CeilToInt((maxPointX + radius) / GOOP_GRID_SIZE);
      int minY   = Mathf.FloorToInt((minPointY - radius) / GOOP_GRID_SIZE);
      int maxY   = Mathf.CeilToInt((maxPointY + radius) / GOOP_GRID_SIZE);
      int width  = maxX - minX + 1;
      int height = maxY - minY + 1;

      int goopRadius          = Mathf.RoundToInt(radius / GOOP_GRID_SIZE);
      s_goopPointRadius       = radius / GOOP_GRID_SIZE;
      s_goopPointRadiusSquare = s_goopPointRadius * s_goopPointRadius;
      _InvSqrRadius = 1f / s_goopPointRadiusSquare;
      m_pointsArray.ReinitializeWithDefault(width, height, false, 1f);

      bool usesLifespan = __instance.goopDefinition.usesLifespan; // the floats don't even get used unless the goop uses lifespan
      for (int j = 0; j < points.Count; j++)
      {
        s_goopPointCenter.x = (int)(points[j].x / GOOP_GRID_SIZE) - minX;
        s_goopPointCenter.y = (int)(points[j].y / GOOP_GRID_SIZE) - minY;
        FastSetCircle(m_pointsArray, s_goopPointCenter.x, s_goopPointCenter.y, goopRadius, true, updateFractions: usesLifespan);
      }

      int x2 = (int)(excludeCenter.x / GOOP_GRID_SIZE) - minX;
      int y2 = (int)(excludeCenter.y / GOOP_GRID_SIZE) - minY;
      int innerExcludeRadius = Mathf.RoundToInt(excludeRadius / GOOP_GRID_SIZE);
      FastSetCircle(m_pointsArray, x2, y2, innerExcludeRadius, false, updateFractions: false);

      bool[] bits = m_pointsArray.m_bits;
      float[] floats = m_pointsArray.m_floats;
      for (int k = 0; k < width; k++)
        for (int l = 0; l < height; l++)
        {
          int index = k + l * m_pointsArray.m_width;
          if (bits[index])
            __instance.AddGoopedPosition(new IntVector2(minX + k, minY + l), floats[index]);
        }

      // gooppointsWatch.Stop();
      // long nanos = gooppointsWatch.ElapsedTicks * 100;
      // _totalNanos += nanos;
      // _totalGoops++;
      // GGVDebug.Log($"    {nanos,10:n0}ns gooppoints, {_totalNanos,16:n0}ns total, {(double)_totalNanos / _totalGoops,10:n0}ns average");
      return false;
    }
    // private static long _totalNanos = 0;
    // private static long _totalGoops = 0;

    //NOTE: apparently this is called the midpoint circle algorithm, neat
    private static void FastSetCircle(BitArray2D bitArray, int xMid, int yMid, int radius, bool value, bool updateFractions)
    {
      int xOff          = radius;
      int yOff          = 0;
      int midpointError = 1 - xOff;
      float[] floats    = bitArray.m_floats;
      bool[] bits       = bitArray.m_bits;
      int bitsW         = bitArray.m_width;
      int bitsH         = bitArray.m_height;

      while (yOff <= xOff)
      {
        for (int i = 0; i < 2; ++i)
        {
          int xRad = (i == 0) ? xOff : yOff;
          int yRad = (i == 0) ? yOff : xOff;
          int yMin = yMid - yRad;
          if (yMin < 0)
            yMin = 0;
          int yMax = yMid + yRad;
          if (yMax >= bitsH)
            yMax = bitsH - 1;

          for (int j = 0; j < 2; ++j)
          {
            int x = xMid + ((j == 0) ? xRad : -xRad);
            if (x < 0 || x >= bitsW)
              continue;

            float xDist = s_goopPointCenter.x - x;
            float xDistSqr = xDist * xDist;
            for (int y = yMin; y <= yMax; y++)
            {
              int bit = x + y * bitsW;
              bits[bit] = value;
              if (!updateFractions)
                continue;

              float yDist  = s_goopPointCenter.y - y;
              float sqrMag = xDistSqr + yDist * yDist;
              if (sqrMag >= s_goopPointRadiusSquare)
                continue;
              if (sqrMag < 0.25f)
              {
                floats[bit] = 0f;
                continue;
              }

              float tt = sqrMag * _InvSqrRadius;
              float f = tt * (2 * tt - 5 * Mathf.Sqrt(tt) + 4); // equivalent to BraveMathCollege.SmoothStepToLinearStepInterpolate(0f, 1f, t);
              if (f < floats[bit])
                floats[bit] = f;
            }
          }
        }

        yOff++;
        if (midpointError <= 0)
        {
          midpointError += 2 * yOff + 1;
          continue;
        }
        xOff--;
        midpointError += 2 * (yOff - xOff) + 1;
      }
    }
}
