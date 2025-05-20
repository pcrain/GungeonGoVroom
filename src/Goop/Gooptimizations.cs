namespace GGV;

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

      private int _xChunks;
      private int _yChunks;

      // private since it should never be constructed outside of Get() and static fields
      private ExtraGoopData(DeadlyDeadlyGoopManager manager)
      {
        this.manager = manager;
        if (manager == null)
          return; // for _NullEGd

        DungeonData d = GameManager.Instance.Dungeon.data;
        this._xChunks = Mathf.CeilToInt((float)d.m_width / (float)manager.CHUNK_SIZE);
        this._yChunks = Mathf.CeilToInt((float)d.m_height / (float)manager.CHUNK_SIZE);
        this.goopedCellBitfield = new UInt64[this._xChunks,this._yChunks,7];
        _AllEGDs.Add(this);
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
        int chunkSize     = (int)(manager.CHUNK_SIZE / GOOP_GRID_SIZE);
        int chunkX        = (int)(pos.x / (float)chunkSize);
        int chunkY        = (int)(pos.y / (float)chunkSize);
        int bitOffset     = (pos.x % chunkSize) * chunkSize + (pos.y % chunkSize);
        ExtraGoopData egd = ExtraGoopData.Get(manager);
        return (egd.goopedCellBitfield[chunkX, chunkY, bitOffset / 64] & (1ul << (bitOffset % 64))) > 0;
      }

      internal static void SetGoopedBit(DeadlyDeadlyGoopManager manager, IntVector2 pos)
      {
        int chunkSize     = (int)(manager.CHUNK_SIZE / GOOP_GRID_SIZE);
        int chunkX        = (int)(pos.x / (float)chunkSize);
        int chunkY        = (int)(pos.y / (float)chunkSize);
        int bitOffset     = (pos.x % chunkSize) * chunkSize + (pos.y % chunkSize);
        ExtraGoopData egd = ExtraGoopData.Get(manager);
        egd.goopedCellBitfield[chunkX, chunkY, bitOffset / 64] |= (1ul << (bitOffset % 64));
      }

      internal static void ClearGoopedBit(DeadlyDeadlyGoopManager manager, IntVector2 pos)
      {
        int chunkSize     = (int)(manager.CHUNK_SIZE / GOOP_GRID_SIZE);
        int chunkX        = (int)(pos.x / (float)chunkSize);
        int chunkY        = (int)(pos.y / (float)chunkSize);
        int bitOffset     = (pos.x % chunkSize) * chunkSize + (pos.y % chunkSize);
        ExtraGoopData egd = ExtraGoopData.Get(manager);
        egd.goopedCellBitfield[chunkX, chunkY, bitOffset / 64] &= ~(1ul << (bitOffset % 64));
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

    //NOTE: this doesn't seem to be significantly faster, so it's disabled
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

        UInt64[,,] goopBitfield = ExtraGoopData.Get(__instance).goopedCellBitfield;
        int bitOffset = -1;
        for (int j = xmin; j < xmax; j++)
        {
          goopPos.x = j;
          for (int k = ymin; k < ymax; k++)
          {
            ++bitOffset;
            if ((goopBitfield[chunkX, chunkY, bitOffset / 64] & (1ul << (bitOffset % 64))) == 0)
              continue; // skip dictionary lookup if the cell definitely isn't gooped

            goopPos.y = k;
            if (!goopedCells.TryGetValue(goopPos, out goopData) || goopData.remainingLifespan <= 0f)
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

    private const BindingFlags _ANY_FLAGS
      = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static readonly Type _IntVecHashSetType
      = typeof(HashSet<>).MakeGenericType(typeof(IntVector2));
    private static readonly Type _IntVecHashSetEnumeratorType
      = typeof(HashSet<>).GetNestedType("Enumerator", _ANY_FLAGS).MakeGenericType(typeof(IntVector2));
    private static readonly MethodInfo _IntVecHashSetEnumeratorCurrent
      = _IntVecHashSetEnumeratorType.GetMethod("get_Current", _ANY_FLAGS);
    private static readonly MethodInfo _IntVecHashSetEnumeratorMoveNext
      = _IntVecHashSetEnumeratorType.GetMethod("MoveNext", _ANY_FLAGS);
    private static readonly Type _IntVecDictType
      = typeof(Dictionary<,>).MakeGenericType(typeof(IntVector2), typeof(GoopPositionData));
    private static readonly Type _IntVecKVPType
      = typeof(KeyValuePair<,>).MakeGenericType(typeof(IntVector2), typeof(GoopPositionData));
    private static readonly Type _IntVecDictEnumeratorType
      = typeof(Dictionary<,>).GetNestedType("Enumerator", _ANY_FLAGS).MakeGenericType(typeof(IntVector2), typeof(GoopPositionData));
    private static readonly MethodInfo _IntVecDictEnumeratorCurrent
      = _IntVecDictEnumeratorType.GetMethod("get_Current", _ANY_FLAGS);
    private static readonly MethodInfo _IntVecDictEnumeratorMoveNext
      = _IntVecDictEnumeratorType.GetMethod("MoveNext", _ANY_FLAGS);
    private static readonly MethodInfo _Dispose
      = typeof(IDisposable).GetMethod(nameof(IDisposable.Dispose));

    /// <summary>Replace expensiv hashset iteration -> dictionary lookups with dictionary iteration</summary>
    [HarmonyPatch(typeof(DeadlyDeadlyGoopManager), nameof(DeadlyDeadlyGoopManager.LateUpdate))]
    [HarmonyILManipulator]
    private static void DeadlyDeadlyGoopManagerLateUpdatePatchIL(ILContext il)
    {
        ILCursor cursor = new ILCursor(il);

        if (!cursor.TryGotoNext(MoveType.AfterLabel,
          instr => instr.MatchLdarg(0), // the DeadlyDeadlyGoopManager instance
          instr => instr.MatchLdfld<DeadlyDeadlyGoopManager>("m_goopedPositions"),
          instr => instr.MatchCallvirt(_IntVecHashSetType.GetMethod("GetEnumerator")),
          instr => instr.MatchStloc(5) // m_goopedPositions foreach enumerator
          ))
        {
          GGVDebug.Log($"  ddgm patch failed at point 1");
          return;
        }

        VariableDefinition intVecDictEnumerator = il.DeclareLocal(_IntVecDictEnumeratorType);
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Ldfld, typeof(DeadlyDeadlyGoopManager).GetField("m_goopedCells", _ANY_FLAGS));
        cursor.Emit(OpCodes.Callvirt, _IntVecDictType.GetMethod("GetEnumerator"));
        cursor.Emit(OpCodes.Stloc, intVecDictEnumerator);
        cursor.RemoveRange(4);

        if (!cursor.TryGotoNext(MoveType.AfterLabel,
          instr => instr.MatchLdloca(5), // m_goopedPositions foreach enumerator
          instr => instr.MatchCall(_IntVecHashSetEnumeratorCurrent),
          instr => instr.MatchStloc(4), // IntVector2 value of enumerator == goopedPosition
          instr => instr.MatchLdarg(0), // the DeadlyDeadlyGoopManager instance
          instr => instr.MatchLdfld<DeadlyDeadlyGoopManager>("m_goopedCells"),
          instr => instr.MatchLdloc(4), // IntVector2 value of enumerator
          instr => instr.MatchCallvirt(_IntVecDictType.GetMethod("get_Item")),
          instr => instr.MatchStloc(6) // GoopPositionData for the IntVector2
          ))
        {
          GGVDebug.Log($"  ddgm patch failed at point 2");
          return;
        }
        VariableDefinition intVecDictKVP = il.DeclareLocal(_IntVecKVPType);
        cursor.Emit(OpCodes.Ldloca, intVecDictEnumerator); // m_goopedCells foreach enumerator
        cursor.Emit(OpCodes.Call, _IntVecDictEnumeratorCurrent);
        cursor.Emit(OpCodes.Stloc, intVecDictKVP); // store the kvp

        cursor.Emit(OpCodes.Ldloca, intVecDictKVP); // load the kvp
        cursor.Emit(OpCodes.Call, _IntVecKVPType.GetMethod("get_Key", _ANY_FLAGS));
        cursor.Emit(OpCodes.Stloc, 4); // store the key in goopedPosition

        cursor.Emit(OpCodes.Ldloca, intVecDictKVP); // load the kvp
        cursor.Emit(OpCodes.Call, _IntVecKVPType.GetMethod("get_Value", _ANY_FLAGS));
        cursor.Emit(OpCodes.Stloc, 6); // store the value in goopPositionData

        cursor.RemoveRange(8);

        if (!cursor.TryGotoNext(MoveType.AfterLabel,
          instr => instr.MatchLdloca(5), // m_goopedPositions foreach enumerator
          instr => instr.MatchCall(_IntVecHashSetEnumeratorMoveNext)
          ))
        {
          GGVDebug.Log($"  ddgm patch failed at point 3");
          return;
        }
        cursor.Emit(OpCodes.Ldloca, intVecDictEnumerator); // m_goopedCells foreach enumerator
        cursor.Emit(OpCodes.Call, _IntVecDictEnumeratorMoveNext);
        cursor.RemoveRange(2); // don't remove old instructions until AFTER the loop iteration is over or jump labels get messed up

        if (!cursor.TryGotoNext(MoveType.AfterLabel,
          // instr => instr.MatchLdloca(5), // m_goopedPositions foreach enumerator
          // instr => instr.MatchConstrained(_IntVecHashSetEnumeratorType),
          instr => instr.MatchCallvirt(_Dispose) // no othe Dispose() method, so this is safe (tm)
          ))
        {
          GGVDebug.Log($"  ddgm patch failed at point 4");
          return;
        }
        //WARNING: we get into deep trouble toying with finalizers...just pop the address of the old enumerator and replace it with our own
        cursor.Emit(OpCodes.Pop); // pop the HashSet Ienumerator
        cursor.Emit(OpCodes.Ldloca, intVecDictEnumerator); // load our own m_goopedCells foreach enumerator
        cursor.Emit(OpCodes.Constrained, _IntVecDictEnumeratorType); // load our own constrained type
        // we reuse the old dispose method, so we're done
    }

    private static readonly Color32 _Transparent = new Color32(0, 0, 0, 0);
    //NOTE: I could possibly reuse a tweaked version of the LateUpdate() ILManipulator, but...it's really not worth it
    [HarmonyPatch(typeof(DeadlyDeadlyGoopManager), nameof(DeadlyDeadlyGoopManager.RebuildMeshColors))]
    [HarmonyPrefix]
    private static bool DeadlyDeadlyGoopManagerRebuildMeshColorsPatch(DeadlyDeadlyGoopManager __instance, int chunkX, int chunkY)
    {
        for (int i = 0; i < __instance.m_colorArray.Length; i++)
          __instance.m_colorArray[i] = _Transparent;

        int chunkSize = Mathf.RoundToInt(__instance.CHUNK_SIZE / GOOP_GRID_SIZE);
        int minX      = chunkX * chunkSize;
        int maxX      = minX   + chunkSize;
        int minY      = chunkY * chunkSize;
        int maxY      = minY   + chunkSize;
        VertexColorRebuildResult b = VertexColorRebuildResult.ALL_OK;
        foreach (var kvp in __instance.m_goopedCells)
        {
          IntVector2 goopedPosition = kvp.Key;
          GoopPositionData goopPositionData = kvp.Value;
          if (goopPositionData.remainingLifespan < 0f || goopedPosition.x < minX || goopedPosition.x >= maxX || goopedPosition.y < minY || goopedPosition.y >= maxY)
            continue;

          int bi = goopPositionData.baseIndex;
          if (bi < 0)
            bi = goopPositionData.baseIndex = __instance.GetGoopBaseIndex(goopedPosition, chunkX, chunkY);

          if (__instance.goopDefinition.CanBeFrozen)
          {
            Vector2 v = new Vector2((goopPositionData.IsFrozen ? 1 : 0), 0f);
            __instance.m_uv2Array[bi    ] = v;
            __instance.m_uv2Array[bi + 1] = v;
            __instance.m_uv2Array[bi + 2] = v;
            __instance.m_uv2Array[bi + 3] = v;
          }
          VertexColorRebuildResult a = __instance.AssignVertexColors(goopPositionData, goopedPosition, chunkX, chunkY);
          if ((int)a > (int)b)
            b = a;
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
      }
      __instance.m_goopedPositions.Remove(entry);
      __instance.m_goopedCells.Remove(entry);
      DeadlyDeadlyGoopManager.allGoopPositionMap.Remove(entry);
      __instance.SetDirty(entry);
      ExtraGoopData.ClearGoopedBit(__instance, entry);
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
        ExtraGoopData.SetGoopedBit(__instance, pos);
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

    //                   original: 153,000ns avg
    // with FastGetRadiusFraction:  93,000ns avg
    //         with FastSetCircle:  81,000ns avg
    //         with inline floats:  74,000ns avg
    [HarmonyPatch(typeof(DeadlyDeadlyGoopManager), nameof(DeadlyDeadlyGoopManager.AddGoopPoints))]
    [HarmonyPrefix]
    private static bool DeadlyDeadlyGoopManagerAddGoopPointsPatch(DeadlyDeadlyGoopManager __instance, List<Vector2> points, float radius, Vector2 excludeCenter, float excludeRadius)
    {
      // System.Diagnostics.Stopwatch gooppointsWatch = System.Diagnostics.Stopwatch.StartNew();

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

              float t = Mathf.Sqrt(sqrMag) / s_goopPointRadius;
              float f = t * t * (2 * t * t - 5 * t + 4); // equivalent to BraveMathCollege.SmoothStepToLinearStepInterpolate(0f, 1f, t);
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
