namespace GGV;

internal static partial class Patches
{
    /// <summary>Optimizations for preventing player projectile prefabs from constructing unnecessary objects</summary>
    [HarmonyPatch(typeof(SpawnManager), nameof(SpawnManager.SpawnProjectile), typeof(GameObject), typeof(Vector3), typeof(Quaternion), typeof(bool))]
    [HarmonyPrefix]
    private static void SpawnManagerSpawnProjectilePatch(SpawnManager __instance, GameObject prefab, Vector3 position, Quaternion rotation, bool ignoresPools)
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

    /// <summary>Optimizations for caching results of reflection in GetAllFields</summary>
    [HarmonyPatch(typeof(dfReflectionExtensions), nameof(dfReflectionExtensions.GetAllFields))]
    [HarmonyPrefix]
    private static bool dfReflectionExtensionsGetAllFieldsPatch(Type type, ref FieldInfo[] __result)
    {
        if (!GGVConfig.OPT_GUI_EVENTS)
          return true;
        if (type == null)
        {
          __result = _NoFieldInfo;
          return false;
        }
        if (_CachedFields.TryGetValue(type, out FieldInfo[] fi))
        {
          __result = fi;
          return false;
        }
        __result = _CachedFields[type] = type
          .GetFields(_FieldFlags)
          .Concat(type.GetBaseType().GetAllFields())
          .Where(f => !f.IsDefined(typeof(HideInInspector), true))
          .ToArray();
        // GGVDebug.Log($"cached fields for type {type} with cache size {_CachedFields.Count}");
        return false;    // skip the original method
    }
    private static readonly FieldInfo[] _NoFieldInfo = new FieldInfo[0];
    private static readonly Dictionary<Type, FieldInfo[]> _CachedFields = new();
    private static readonly BindingFlags _FieldFlags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>Optimize calls to HasPassive() to avoid unnecessary delegate creation.</summary>
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.HasPassiveItem))]
    [HarmonyPrefix]
    private static bool PlayerControllerHasPassiveItemPatch(PlayerController __instance, int pickupId, ref bool __result)
    {
        if (!GGVConfig.OPT_ITEM_LOOKUPS)
          return true;
        for (int i = __instance.passiveItems.Count - 1; i >= 0; --i)
          if (__instance.passiveItems[i].PickupObjectId == pickupId)
          {
            __result = true;
            return false; // skip the original method
          }
        __result = false;
        return false; // skip the original method
    }

    /// <summary>Optimize calls to HasActive() to avoid unnecessary delegate creation.</summary>
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.HasActiveItem))]
    [HarmonyPrefix]
    private static bool PlayerControllerHasActiveItemPatch(PlayerController __instance, int pickupId, ref bool __result)
    {
        if (!GGVConfig.OPT_ITEM_LOOKUPS)
          return true;
        for (int i = __instance.activeItems.Count - 1; i >= 0; --i)
          if (__instance.activeItems[i].PickupObjectId == pickupId)
          {
            __result = true;
            return false; // skip the original method
          }
        __result = false;
        return false; // skip the original method
    }

    /// <summary>Optimize LightCulled() by manually computing / comparing against square distance</summary>
    [HarmonyPatch(typeof(Pixelator), nameof(Pixelator.LightCulled))]
    [HarmonyPrefix]
    private static bool PixelatorLightCulledPatch(Pixelator __instance, Vector2 lightPosition, Vector2 cameraPosition, float lightRange, float orthoSize, float aspect, ref bool __result)
    {
        if (!GGVConfig.OPT_LIGHT_CULL)
          return true;
        float x = lightPosition.x - cameraPosition.x;
        float y = lightPosition.y - cameraPosition.y;
        float cullRadius = lightRange + orthoSize * __instance.LightCullFactor * aspect;
        __result = ((x * x) + (y * y)) > (cullRadius * cullRadius);
        return false;    // skip the original method
    }

    /// <summary>Optimize FloodFillDungeonExterior() to not use a HashSet</summary>
    [HarmonyPatch(typeof(DungeonData), nameof(DungeonData.FloodFillDungeonExterior))]
    [HarmonyPrefix]
    private static bool FloodFillDungeonExteriorPatch(DungeonData __instance)
    {
        if (!GGVConfig.OPT_FLOOD_FILL)
          return true; // call original
        int width = __instance.m_width;
        int height = __instance.m_height;
        // GGVDebug.Log($"processing exterior of size {width} by {height}");
        int amountToClear = Mathf.CeilToInt((width * height) / 64f);
        if (amountToClear > _FloodFillBitfield.Length)
          Array.Resize(ref _FloodFillBitfield, amountToClear);
        for (int i = amountToClear - 1; i >= 0; --i)
          _FloodFillBitfield[i] = 0;

        CellData[][] data = __instance.cellData;
        if (data[0][0] != null)
            data[0][0].isRoomInternal = false;
        _FloodFillBitfield[0] = 1; // set the very first bit for 0,0
        _FloodFillStack.Push(IntVector2.Zero);
        while (_FloodFillStack.Count > 0)
        {
            IntVector2 cellPos = _FloodFillStack.Pop();
            for (int i = 0; i < IntVector2.Cardinals.Length; i++)
            {
                IntVector2 nextPos = cellPos + IntVector2.Cardinals[i];
                int nx = nextPos.x;
                int ny = nextPos.y;
                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                    continue;
                int bitIndex = ny * width + nx;
                int elementIndex = Mathf.FloorToInt(bitIndex / 64f);
                UInt64 bitmask = (1u << (bitIndex % 64));
                if ((_FloodFillBitfield[elementIndex] & bitmask) == bitmask)
                  continue; // already checked
                _FloodFillBitfield[elementIndex] |= bitmask;
                CellData nextData = data[nx][ny];
                if (nextData != null)
                {
                  if ((nextData.type != CellType.WALL || nextData.breakable) && !nextData.isExitCell)
                    continue;
                  nextData.isRoomInternal = false;
                }
                _FloodFillStack.Push(nextPos);
            }
        }
        return false; // skip original method
    }
    private static readonly Stack<IntVector2> _FloodFillStack = new();
    private static UInt64[] _FloodFillBitfield = new UInt64[10000];

    // /// <summary>Fix memory leak in df pooling (don't think there's a real leak, check on this later)</summary>
    // [HarmonyPatch(typeof(dfFont.BitmappedFontRenderer), nameof(dfFont.BitmappedFontRenderer.Obtain))]
    // [HarmonyPrefix]
    // private static void dfFontBitmappedFontRendererObtainPatch(dfFont font)
    // {
    //   if (dfFont.BitmappedFontRenderer.objectPool.Count == 0)
    //     System.Console.WriteLine($"  new renderer being requested");
    // }

    // [HarmonyPatch(typeof(dfMarkupToken), MethodType.Constructor)]
    // [HarmonyPrefix]
    // private static void Newdfmarkuptoken()
    // {
    //   System.Console.WriteLine($"created {++_TokensCreated} tokens");
    // }
    // private static int _TokensCreated = 0;
}
