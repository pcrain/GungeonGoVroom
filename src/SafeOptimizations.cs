namespace GGV;

internal static partial class Patches
{
    /// <summary>Optimizations for preventing player projectile prefabs from constructing unnecessary objects</summary>
    [HarmonyPatch(typeof(SpawnManager), nameof(SpawnManager.SpawnProjectile), typeof(GameObject), typeof(Vector3), typeof(Quaternion), typeof(bool))]
    [HarmonyPrefix]
    static void SpawnManagerSpawnProjectilePatch(SpawnManager __instance, GameObject prefab, Vector3 position, Quaternion rotation, bool ignoresPools)
    {
        if (!GGVConfig.OPT_PROJ_STATUS)
          return;
        if (_ProcessedProjPrefabs.Contains(prefab))
          return;
        if (prefab.GetComponent<Projectile>() is not Projectile proj)
          return;
        if (!proj.AppliesPoison)                        { proj.healthEffect               = null; }
        if (!proj.AppliesSpeedModifier)                 { proj.speedEffect                = null; }
        if (!proj.AppliesCharm)                         { proj.charmEffect                = null; }
        if (!proj.AppliesFreeze)                        { proj.freezeEffect               = null; }
        if (!proj.AppliesCheese)                        { proj.cheeseEffect               = null; }
        if (!proj.AppliesBleed)                         { proj.bleedEffect                = null; }
        if (!proj.AppliesFire)                          { proj.fireEffect                 = null; }
        if (!proj.baseData.UsesCustomAccelerationCurve) { proj.baseData.AccelerationCurve = null; }
        _ProcessedProjPrefabs.Add(prefab);
    }
    private static HashSet<GameObject> _ProcessedProjPrefabs = new();
}
