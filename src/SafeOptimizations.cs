namespace GGV;

using Pathfinding;

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

    /// <summary>Cache numerical strings.</summary>
    [HarmonyPatch(typeof(SGUI.SGUIIMBackend), nameof(SGUI.SGUIIMBackend.NextComponentName))]
    [HarmonyILManipulator]
    private static void SGUIIMBackendNextComponentNamePatch(ILContext il)
    {
        if (!GGVConfig.OPT_NUMBERS)
          return;
        ILCursor cursor = new ILCursor(il);
        if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCall<int>(nameof(int.ToString))))
          return;

        cursor.Remove();
        cursor.CallPrivate(typeof(Patches), nameof(CachedStringForNumber));
    }
    private static string CachedStringForNumber(ref int n)
    {
      if (n > _MAX_CACHED_NUMBER)
        return n.ToString();
      if (_CachedNumberStrings.TryGetValue(n, out string s))
        return s;
      return _CachedNumberStrings[n] = n.ToString();
    }
    private const int _MAX_CACHED_NUMBER = 100000;
    private static readonly Dictionary<int, string> _CachedNumberStrings = new();

    /// <summary>Skips expensive search for primary player when on the title screen.</summary>
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.PrimaryPlayer), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool PrimaryPlayerPatch(GameManager __instance, ref PlayerController __result)
    {
      if (!__instance.m_player && (Foyer.DoIntroSequence || Foyer.DoMainMenu) && GGVConfig.OPT_TITLE_SCREEN && !_CheckForPlayer)
      {
        __result = null; // if we're on the menu this can never succeed, so don't try
        return false; // skip original method
      }
      _CheckForPlayer = false;
      return true; // call original method
    }

    /// <summary>Force check for the game's primary player every time a PlayerController is instantiated</summary>
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.Start))]
    [HarmonyPostfix]
    private static void PrimaryPlayerPatch(PlayerController __instance)
    {
      _CheckForPlayer = true;
      var dummy = GameManager.Instance.PrimaryPlayer;
    }
    private static bool _CheckForPlayer = false;

    /// <summary>Optimize IrrelevantToGameplay()</summary>
    [HarmonyPatch(typeof(tk2dRuntime.TileMap.SpriteChunk), nameof(tk2dRuntime.TileMap.SpriteChunk.IrrelevantToGameplay), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool SpriteChunkIrrelevantToGameplayPatch(tk2dRuntime.TileMap.SpriteChunk __instance, ref bool __result)
    {
      if (!GGVConfig.OPT_CHUNK_CHECKS)
        return true;

      const float THRESHOLD = 15f;

      DungeonData data = GameManager.Instance.Dungeon.data;
      int width        = data.m_width - 1;
      int height       = data.m_height - 1;
      int xOff         = tk2dRuntime.TileMap.RenderMeshBuilder.CurrentCellXOffset;
      int yOff         = tk2dRuntime.TileMap.RenderMeshBuilder.CurrentCellYOffset;

      int xMin = __instance.startX + xOff;
      if (xMin < 0)
        xMin = 0;
      int yMin = __instance.startY + yOff;
      if (yMin < 0)
        yMin = 0;
      int xMax = __instance.endX + xOff;
      if (xMax > width)
        xMax = width;
      int yMax = __instance.endY + yOff;
      if (yMax > height)
        yMax = height;
      CellData[][] cells = data.cellData;
      for (int i = xMin; i < xMax; i++)
      {
        for (int j = yMin; j < yMax; j++)
        {
          CellData cd = cells[i][j];
          if (cd != null && cd.distanceFromNearestRoom <= THRESHOLD)
          {
            __result = false; // relevant
            return false;
          }
        }
      }
      __result = true; // irrelevant
      return false;
    }
}
