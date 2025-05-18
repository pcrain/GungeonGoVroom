namespace GGV;

// [HarmonyPatch] //NOTE: this doesn't seem to be significantly faster, so it's disabled
internal static class GoopPatches
{
    [HarmonyPatch(typeof(DeadlyDeadlyGoopManager), nameof(DeadlyDeadlyGoopManager.RebuildMeshUvsAndColors))]
    [HarmonyPrefix]
    private static bool DeadlyDeadlyGoopManagerRebuildMeshUvsAndColorsPatch(DeadlyDeadlyGoopManager __instance, int chunkX, int chunkY)
    {
        // System.Diagnostics.Stopwatch colorsWatch = System.Diagnostics.Stopwatch.StartNew();
        Mesh chunkMesh = __instance.GetChunkMesh(chunkX, chunkY);
        for (int i = 0; i < __instance.m_colorArray.Length; i++)
          __instance.m_colorArray[i].a = 0;

        int chunkBase   = Mathf.RoundToInt(__instance.CHUNK_SIZE / DeadlyDeadlyGoopManager.GOOP_GRID_SIZE);
        int xmin        = chunkX * chunkBase;
        int xmax        = xmin + chunkBase;
        int ymin        = chunkY * chunkBase;
        int ymax        = ymin + chunkBase;
        var goopedCells = __instance.m_goopedCells;

        DeadlyDeadlyGoopManager.GoopPositionData goopData = default;
        IntVector2 goopPos                                = default;
        Vector2 defaultUv                                 = __instance.m_uvMap[-1];
        int numUvOptions                                  = __instance.m_centerUVOptions.Count;
        Vector2 uvVec                                     = default;

        for (int j = xmin; j < xmax; j++)
        {
          goopPos.x = j;
          for (int k = ymin; k < ymax; k++)
          {
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
        // colorsWatch.Stop(); System.Console.WriteLine($"    {colorsWatch.ElapsedTicks,-10} ticks to update goops");
        return false;    // skip the original method
    }
}
