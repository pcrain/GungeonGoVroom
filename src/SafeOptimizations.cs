namespace GGV;

using Pathfinding;
using System.Runtime.CompilerServices;

internal static partial class Patches
{
    private static UInt64[] _Bitfield = new UInt64[10000]; // bitfield used by several patches

    /// <summary>Optimizations for preventing player projectile prefabs from constructing unnecessary objects</summary>
    [HarmonyPatch]
    private static class ProjectileSpawnOptimizations
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.OPT_PROJ_STATUS)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        [HarmonyPatch(typeof(SpawnManager), nameof(SpawnManager.SpawnProjectile), typeof(GameObject), typeof(Vector3), typeof(Quaternion), typeof(bool))]
        [HarmonyPrefix]
        private static void SpawnManagerSpawnProjectilePatch(SpawnManager __instance, GameObject prefab, Vector3 position, Quaternion rotation, bool ignoresPools)
        {
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

    [HarmonyPatch]
    private static class dfGUIEventOptimizations
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.OPT_GUI_EVENTS)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        /// <summary>Optimizations for caching results of reflection in GetAllFields</summary>
        [HarmonyPatch(typeof(dfReflectionExtensions), nameof(dfReflectionExtensions.GetAllFields))]
        [HarmonyPrefix]
        private static bool dfReflectionExtensionsGetAllFieldsPatch(Type type, ref FieldInfo[] __result)
        {
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
    }

    [HarmonyPatch]
    private static class ItemLookupOptimizations
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.OPT_ITEM_LOOKUPS)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        /// <summary>Optimize calls to HasPassive() to avoid unnecessary delegate creation.</summary>
        [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.HasPassiveItem))]
        [HarmonyPrefix]
        private static bool PlayerControllerHasPassiveItemPatch(PlayerController __instance, int pickupId, ref bool __result)
        {
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
            for (int i = __instance.activeItems.Count - 1; i >= 0; --i)
              if (__instance.activeItems[i].PickupObjectId == pickupId)
              {
                __result = true;
                return false; // skip the original method
              }
            __result = false;
            return false; // skip the original method
        }
    }

    /// <summary>Optimize LightCulled() by manually computing / comparing against square distance</summary>
    [HarmonyPatch]
    private static class LightCullingOptimizations
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.OPT_LIGHT_CULL)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        [HarmonyPatch(typeof(Pixelator), nameof(Pixelator.LightCulled))]
        [HarmonyPrefix]
        private static bool PixelatorLightCulledPatch(Pixelator __instance, Vector2 lightPosition, Vector2 cameraPosition, float lightRange, float orthoSize, float aspect, ref bool __result)
        {
            float x = lightPosition.x - cameraPosition.x;
            float y = lightPosition.y - cameraPosition.y;
            float cullRadius = lightRange + orthoSize * __instance.LightCullFactor * aspect;
            __result = ((x * x) + (y * y)) > (cullRadius * cullRadius);
            return false;    // skip the original method
        }
    }

    /// <summary>Optimize FloodFillDungeonExterior() to not use a HashSet</summary>
    [HarmonyPatch]
    private static class FloodFillOptimizations
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.OPT_FLOOD_FILL)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        [HarmonyPatch(typeof(DungeonData), nameof(DungeonData.FloodFillDungeonExterior))]
        [HarmonyPrefix]
        private static bool FloodFillDungeonExteriorPatch(DungeonData __instance)
        {
            int width = __instance.m_width;
            int height = __instance.m_height;
            // GGVDebug.Log($"processing exterior of size {width} by {height}");
            int amountToClear = Mathf.CeilToInt((width * height) / 64f);
            if (amountToClear > _Bitfield.Length)
              Array.Resize(ref _Bitfield, amountToClear);
            for (int i = amountToClear - 1; i >= 0; --i)
              _Bitfield[i] = 0;

            CellData[][] data = __instance.cellData;
            if (data[0][0] != null)
                data[0][0].isRoomInternal = false;
            _Bitfield[0] = 1; // set the very first bit for 0,0
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
                    UInt64 bitmask = (1ul << (bitIndex % 64));
                    if ((_Bitfield[elementIndex] & bitmask) == bitmask)
                      continue; // already checked
                    _Bitfield[elementIndex] |= bitmask;
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
    }

    [HarmonyPatch]
    private static class GUINumberOptimizations
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.OPT_NUMBERS)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        /// <summary>Cache numerical strings.</summary>
        [HarmonyPatch(typeof(SGUI.SGUIIMBackend), nameof(SGUI.SGUIIMBackend.NextComponentName))]
        [HarmonyILManipulator]
        private static void SGUIIMBackendNextComponentNamePatch(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCall<int>(nameof(int.ToString))))
              return;

            cursor.Remove();
            cursor.CallPrivate(typeof(GUINumberOptimizations), nameof(CachedStringForNumber));
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
    }

    [HarmonyPatch]
    private static class TitleScreenOptimizations
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.OPT_TITLE_SCREEN)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        /// <summary>Skips expensive search for primary player when on the title screen.</summary>
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.PrimaryPlayer), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool PrimaryPlayerPatch(GameManager __instance, ref PlayerController __result)
        {
          if (!_CheckForPlayer && !__instance.m_player && !GameManager.Instance.IsLoadingLevel && (Foyer.DoIntroSequence || Foyer.DoMainMenu))
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
    }

    [HarmonyPatch]
    private static class SpriteChunkOptimizations
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.OPT_CHUNK_CHECKS)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        /// <summary>Optimize IrrelevantToGameplay()</summary>
        [HarmonyPatch(typeof(tk2dRuntime.TileMap.SpriteChunk), nameof(tk2dRuntime.TileMap.SpriteChunk.IrrelevantToGameplay), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool SpriteChunkIrrelevantToGameplayPatch(tk2dRuntime.TileMap.SpriteChunk __instance, ref bool __result)
        {
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

    [HarmonyPatch]
    private static class PathingOptimizations
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.OPT_PATH_RECALC)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        private static bool callOriginal = false;
        [HarmonyPatch(typeof(Pathfinding.Pathfinder), nameof(Pathfinding.Pathfinder.RecalculateClearances), new[]{typeof(int), typeof(int), typeof(int), typeof(int)})]
        [HarmonyPrefix]
        private static bool RecalculateClearancesPatch(Pathfinding.Pathfinder __instance, int minX, int minY, int maxX, int maxY)
        {
          #if DEBUG
          if (callOriginal)
            return true;
          #endif

          int numCells = (maxX - minX + 1) * (maxY - minY + 1);

          #if DEBUG
          callOriginal = true;
          System.Diagnostics.Stopwatch tempWatch = System.Diagnostics.Stopwatch.StartNew();
          __instance.RecalculateClearances(minX, minY, maxX, maxY);
          tempWatch.Stop();
          callOriginal = false;
          ulong ohash = HashClearances(__instance);
          System.Diagnostics.Stopwatch tempWatch2 = System.Diagnostics.Stopwatch.StartNew();
          #endif

          var nodes = __instance.m_nodes;
          int w = __instance.m_width;
          int mapXMax = __instance.m_width - 1;
          int mapYMax = __instance.m_height - 1;
          // handle some edge cases where we might reuse stale nodes just barely outside our range
          int borderX = (maxX < mapXMax) ? (maxX + 1) : mapXMax;
          int borderY = (maxY < mapYMax) ? (maxY + 1) : mapYMax;
          for (int i = borderX; i >= minX; i--)
          {
            for (int j = borderY; j >= minY; j--)
            {
              int nodeIndex = i + j * w;
              Pathfinder.PathNode node = nodes[nodeIndex];
              CellData cell = node.CellData;
              if ((cell == null || cell.isOccupied || cell.type == CellType.WALL || (cell.type == CellType.PIT && !cell.fallingPrevented)))
                node.SquareClearance = 0;
              else if (i == mapXMax || j == mapYMax)
                node.SquareClearance = 1;
              else
              {
                int minClearance = nodes[nodeIndex + w + 1].SquareClearance;
                int right = nodes[nodeIndex + 1].SquareClearance;
                if (right < minClearance)
                  minClearance = right;
                int below = nodes[nodeIndex + w].SquareClearance;
                if (below < minClearance)
                  minClearance = below;
                int nextClearance = 1 + minClearance;

                int maxClearanceX = maxX - i + 1;
                int maxClearanceY = maxY - j + 1;
                int maxClearance = (maxClearanceX > maxClearanceY) ? maxClearanceX : maxClearanceY;

                node.SquareClearance = (nextClearance < maxClearance) ? nextClearance : maxClearance;
              }
            }
          }

          #if DEBUG
          tempWatch2.Stop();
          // System.Console.WriteLine($"    new: {tempWatch2.ElapsedTicks * 100,6:n0} ns clearances for {numCells} cells, {tempWatch.ElapsedTicks / (float)tempWatch2.ElapsedTicks}x speedup");

          if (ohash != HashClearances(__instance))
          {
            ETGModConsole.Log("PATH NOTE HASHES DON'T MATCH");
            System.Console.WriteLine($"but hashes don't match! D:");
          }

          static ulong HashClearances(Pathfinding.Pathfinder p)
          {
            ulong hash = 69420;
            foreach (var node in p.m_nodes)
            {
              hash = hash * 1337;
              hash = hash ^ (ulong)node.SquareClearance;
            }
            return hash;
          }
          #endif

          return false;    // skip the original method
        }
    }

    [HarmonyPatch]
    private static class SpriteVisiblityOptimizations
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.OPT_VIS_CHECKS)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        /// <summary>Remove call to HandleVisibilityCheck() in LateUpate() since its value is only used when the sprite is changed</summary>
        [HarmonyPatch(typeof(tk2dSpriteAnimator), nameof(tk2dSpriteAnimator.LateUpdate))]
        [HarmonyILManipulator]
        private static void tk2dSpriteAnimatorLateUpdatePatchIL(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCall<tk2dSpriteAnimator>(nameof(tk2dSpriteAnimator.HandleVisibilityCheck))))
              return;
            cursor.Remove();
            cursor.Emit(OpCodes.Pop);
        }

        /// <summary>Add call to our own FastVisibilityCheck() in SetSprite() with an optimized algorithm</summary>
        [HarmonyPatch(typeof(tk2dSpriteAnimator), nameof(tk2dSpriteAnimator.SetSprite))]
        [HarmonyILManipulator]
        private static void tk2dSpriteAnimatorSetSpritePatchIL(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchLdfld<tk2dSpriteAnimator>(nameof(tk2dSpriteAnimator.m_isCurrentlyVisible))))
              return;
            cursor.Remove();
            cursor.CallPrivate(typeof(SpriteVisiblityOptimizations), nameof(FastVisibilityCheck));
        }

        private static bool FastVisibilityCheck(tk2dSpriteAnimator animator)
        {
          if (!tk2dSpriteAnimator.InDungeonScene)
            return true;
          Vector3 pos = animator.transform.position;
          float x     = pos.x - tk2dSpriteAnimator.CameraPositionThisFrame.x;
          float y     = pos.y - tk2dSpriteAnimator.CameraPositionThisFrame.y;
          return (x * x + y * y * 2.89) < 420f + animator.AdditionalCameraVisibilityRadius * animator.AdditionalCameraVisibilityRadius;
        }
    }
}

