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
        int chunkSize     = (int)(manager.CHUNK_SIZE / DeadlyDeadlyGoopManager.GOOP_GRID_SIZE);
        int chunkX        = (int)(pos.x / (float)chunkSize);
        int chunkY        = (int)(pos.y / (float)chunkSize);
        int bitOffset     = (pos.x % chunkSize) * chunkSize + (pos.y % chunkSize);
        ExtraGoopData egd = ExtraGoopData.Get(manager);
        return (egd.goopedCellBitfield[chunkX, chunkY, bitOffset / 64] & (1ul << (bitOffset % 64))) > 0;
      }

      internal static void SetGoopedBit(DeadlyDeadlyGoopManager manager, IntVector2 pos)
      {
        int chunkSize     = (int)(manager.CHUNK_SIZE / DeadlyDeadlyGoopManager.GOOP_GRID_SIZE);
        int chunkX        = (int)(pos.x / (float)chunkSize);
        int chunkY        = (int)(pos.y / (float)chunkSize);
        int bitOffset     = (pos.x % chunkSize) * chunkSize + (pos.y % chunkSize);
        ExtraGoopData egd = ExtraGoopData.Get(manager);
        egd.goopedCellBitfield[chunkX, chunkY, bitOffset / 64] |= (1ul << (bitOffset % 64));
      }

      internal static void ClearGoopedBit(DeadlyDeadlyGoopManager manager, IntVector2 pos)
      {
        int chunkSize     = (int)(manager.CHUNK_SIZE / DeadlyDeadlyGoopManager.GOOP_GRID_SIZE);
        int chunkX        = (int)(pos.x / (float)chunkSize);
        int chunkY        = (int)(pos.y / (float)chunkSize);
        int bitOffset     = (pos.x % chunkSize) * chunkSize + (pos.y % chunkSize);
        ExtraGoopData egd = ExtraGoopData.Get(manager);
        egd.goopedCellBitfield[chunkX, chunkY, bitOffset / 64] &= ~(1ul << (bitOffset % 64));
      }
    }

    [HarmonyPatch(typeof(DeadlyDeadlyGoopManager), nameof(DeadlyDeadlyGoopManager.ClearPerLevelData))]
    [HarmonyPostfix]
    private static void DeadlyDeadlyGoopManagerClearPerLevelDataPatch()
    {
      if (GGVConfig.OPT_GOOP)
        ExtraGoopData.ClearLevelData();
    }

    //NOTE: this doesn't seem to be significantly faster, so it's disabled
    [HarmonyPatch(typeof(DeadlyDeadlyGoopManager), nameof(DeadlyDeadlyGoopManager.RebuildMeshUvsAndColors))]
    [HarmonyPrefix]
    private static bool DeadlyDeadlyGoopManagerRebuildMeshUvsAndColorsPatch(DeadlyDeadlyGoopManager __instance, int chunkX, int chunkY)
    {
        if (!GGVConfig.OPT_GOOP)
          return true;

        Mesh chunkMesh = __instance.GetChunkMesh(chunkX, chunkY);
        for (int i = 0; i < __instance.m_colorArray.Length; i++)
          __instance.m_colorArray[i].a = 0;

        int chunkSize   = Mathf.RoundToInt(__instance.CHUNK_SIZE / DeadlyDeadlyGoopManager.GOOP_GRID_SIZE);
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
        if (!GGVConfig.OPT_GOOP)
          return;

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
        if (!GGVConfig.OPT_GOOP)
          return true; // call the original method

        for (int i = 0; i < __instance.m_colorArray.Length; i++)
          __instance.m_colorArray[i] = _Transparent;

        int chunkSize = Mathf.RoundToInt(__instance.CHUNK_SIZE / DeadlyDeadlyGoopManager.GOOP_GRID_SIZE);
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
      if (!GGVConfig.OPT_GOOP)
        return true;

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

    [HarmonyPatch(typeof(DeadlyDeadlyGoopManager), nameof(DeadlyDeadlyGoopManager.AddGoopedPosition))]
    [HarmonyILManipulator]
    private static void DeadlyDeadlyGoopManagerAddGoopedPositionPatchIL(ILContext il)
    {
      if (!GGVConfig.OPT_GOOP)
        return;

      ILCursor cursor = new ILCursor(il);
      if (!cursor.TryGotoNext(MoveType.Before,
        instr => instr.MatchLdsfld<DeadlyDeadlyGoopManager>("allGoopPositionMap"),
        instr => instr.MatchLdarg(1), // the IntVector2 goop poistion
        instr => instr.MatchLdarg(0))) // the DeadlyDeadlyGoopManager instance
        return;

      cursor.Emit(OpCodes.Ldarg_0);
      cursor.Emit(OpCodes.Ldarg_1);
      cursor.CallPrivate(typeof(ExtraGoopData), nameof(ExtraGoopData.SetGoopedBit));
    }

    [HarmonyPatch(typeof(DeadlyDeadlyGoopManager), nameof(DeadlyDeadlyGoopManager.HasGoopedPositionCountForChunk))]
    [HarmonyPrefix]
    private static bool FastHasGoopedPositionCountForChunk(DeadlyDeadlyGoopManager __instance, int chunkX, int chunkY, ref bool __result)
    {
      if (!GGVConfig.OPT_GOOP)
        return true;

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
      if (!GGVConfig.OPT_GOOP)
        return true;

      int x = ((int)(goopPosition.x * DeadlyDeadlyGoopManager.GOOP_GRID_SIZE)) / __instance.CHUNK_SIZE;
      int y = ((int)(goopPosition.y * DeadlyDeadlyGoopManager.GOOP_GRID_SIZE)) / __instance.CHUNK_SIZE;
      bool[,] dirtyFlags = __instance.m_dirtyFlags;
      int w = dirtyFlags.GetLength(0);
      int h = dirtyFlags.GetLength(1);
      if (x < 0 || x >= w || y < 0 || y >= h)
        return false;

      int chunkSize    = (int)(__instance.CHUNK_SIZE / DeadlyDeadlyGoopManager.GOOP_GRID_SIZE);
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

    [HarmonyPatch(typeof(DeadlyDeadlyGoopManager), nameof(DeadlyDeadlyGoopManager.AssignVertexColors))]
    [HarmonyPrefix]
    private static bool DeadlyDeadlyGoopManagerAssignVertexColorsPatch(DeadlyDeadlyGoopManager __instance, GoopPositionData goopData, IntVector2 goopPos, int chunkX, int chunkY, ref VertexColorRebuildResult __result)
    {
      if (!GGVConfig.OPT_GOOP)
        return true;

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
}
