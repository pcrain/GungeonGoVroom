namespace GGV;

using BraveDynamicTree;

internal static partial class Patches
{
    /// <summary>Optimized version of PhysicsEngine.Pointcast(IntVector2, ...) without unnecessary delegate creation</summary>
    [HarmonyPatch]
    private static class OptimiseIntVectorPointcastPatch
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.OPT_POINTCAST) //NOTE: shared with PointcastOptimizations
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        private static MethodBase TargetMethod() {
          return typeof(PhysicsEngine).GetMethod("Pointcast", new Type[] {
            typeof(IntVector2), typeof(SpeculativeRigidbody).MakeByRefType(), typeof(bool),
            typeof(bool), typeof(int), typeof(CollisionLayer?), typeof(bool), typeof(SpeculativeRigidbody[])
          });
        }

        private static bool _collideWithTriggers;
        private static int _rayMask;
        private static CollisionLayer? _sourceLayer;
        private static IntVector2 _point;
        private static ICollidableObject _tempResult;
        private static bool CollideWithRigidBodyStatic(SpeculativeRigidbody rigidbody)
        {
            if (!rigidbody || !rigidbody.enabled)
              return true;
            List<PixelCollider> colliders = rigidbody.GetPixelColliders();
            for (int i = 0; i < colliders.Count; i++)
            {
              PixelCollider pixelCollider = colliders[i];
              if ((_collideWithTriggers || !pixelCollider.IsTrigger) && pixelCollider.CanCollideWith(_rayMask, _sourceLayer) && pixelCollider.ContainsPixel(_point))
              {
                _tempResult = rigidbody;
                return false;
              }
            }
            return true;
        }

        private static bool Prefix(PhysicsEngine __instance, ref bool __result, IntVector2 point, out SpeculativeRigidbody result, bool collideWithTiles,
          bool collideWithRigidbodies, int rayMask, CollisionLayer? sourceLayer, bool collideWithTriggers,
          params SpeculativeRigidbody[] ignoreList)
        {
          if (collideWithTiles && __instance.TileMap)
          {
            __instance.TileMap.GetTileAtPosition(PhysicsEngine.PixelToUnit(point), out var x, out var y);
            int tileMapLayerByName = BraveUtility.GetTileMapLayerByName("Collision Layer", __instance.TileMap);
            PhysicsEngine.Tile tile = __instance.GetTile(x, y, __instance.TileMap, tileMapLayerByName, "Collision Layer", GameManager.Instance.Dungeon.data);
            if (tile != null)
            {
              List<PixelCollider> colliders = tile.GetPixelColliders();
              for (int i = 0; i < colliders.Count; i++)
              {
                PixelCollider pixelCollider = colliders[i];
                if ((collideWithTriggers || !pixelCollider.IsTrigger) && pixelCollider.CanCollideWith(rayMask, sourceLayer) && pixelCollider.ContainsPixel(point))
                {
                  result = null; // tile is not a SpeculativeRigidBody
                  __result = true; // original return value
                  return false; // skip original
                }
              }
            }
          }

          if (collideWithRigidbodies)
          {
            _tempResult          = null;
            _collideWithTriggers = collideWithTriggers;
            _rayMask             = rayMask;
            _sourceLayer         = sourceLayer;
            _point               = point;
            BraveDynamicTree.b2AABB b2aabb = PhysicsEngine.GetSafeB2AABB(point, point);
            __instance.m_rigidbodyTree.Query(b2aabb, CollideWithRigidBodyStatic);
            if (__instance.CollidesWithProjectiles(rayMask, sourceLayer))
              __instance.m_projectileTree.Query(b2aabb, CollideWithRigidBodyStatic);

            result = _tempResult as SpeculativeRigidbody;
            __result = result != null; // original return value
            return false; // skip the original method
          }

          result = null;
          __result = false; // original return value
          return false; // skip the original method
        }
    }

    /// <summary>Speed up slow logic in HandleAmbientPitVFX()</summary>
    [HarmonyPatch]
    private static class DungeonHandleAmbientPitVFXPatch
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.OPT_PIT_VFX)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        [HarmonyPatch(typeof(Dungeon), nameof(Dungeon.HandleAmbientPitVFX))]
        static IEnumerator Postfix(IEnumerator orig, Dungeon __instance)
        {
          return HandleAmbientPitVFXFast(__instance);
        }

        private static IEnumerator HandleAmbientPitVFXFast(Dungeon self)
        {
            const int DISTANCE_THRESH = 3;
            self.m_ambientVFXProcessingActive = true;
            while (self.m_ambientVFXProcessingActive)
            {
                if (GameManager.Instance.IsLoadingLevel || GameManager.Options.ShaderQuality == GameOptions.GenericHighMedLowOption.LOW || GameManager.Options.ShaderQuality == GameOptions.GenericHighMedLowOption.VERY_LOW)
                {
                    yield return null;
                    continue;
                }

                DungeonData allData = self.data;
                if (allData == null)
                {
                    yield return null;
                    continue;
                }

                CameraController mainCameraController = GameManager.Instance.MainCameraController;
                if (!mainCameraController)
                {
                    yield return null;
                    continue;
                }

                CellData[][] allCells = allData.cellData;
                IntVector2 camMin = mainCameraController.MinVisiblePoint.ToIntVector2(VectorConversions.Floor);
                IntVector2 camMax = mainCameraController.MaxVisiblePoint.ToIntVector2(VectorConversions.Ceil);
                int xMin = Mathf.Max(camMin.x, DISTANCE_THRESH);
                int yMin = Mathf.Max(camMin.y, DISTANCE_THRESH);
                int xMax = Mathf.Min(camMax.x, allData.m_width - DISTANCE_THRESH - 1);
                int yMax = Mathf.Min(camMax.y, allData.m_height - DISTANCE_THRESH - 1);
                bool inHell = self.tileIndices.tilesetId == GlobalDungeonData.ValidTilesets.HELLGEON;
                bool inForge = self.tileIndices.tilesetId == GlobalDungeonData.ValidTilesets.FORGEGEON;
                for (int i = xMin; i <= xMax; i++)
                {
                    for (int j = yMin; j <= yMax; j++)
                    {
                        if (inHell)
                        {
                            CellData cellData4 = allCells[i][j + 2];
                            if (cellData4 == null || cellData4.type != CellType.PIT)
                            {
                                j += 2; // we can safely skip the next two iterations since it will just check both cells again
                                continue;
                            }
                        }

                        CellData cellData2 = allCells[i][j + 1];
                        if (cellData2 == null || cellData2.type != CellType.PIT)
                        {
                            j++; // we can safely skip the next iteration since it will just check this cell again
                            continue;
                        }

                        CellData cellData = allCells[i][j];
                        if (cellData == null || cellData.type != CellType.PIT || cellData.fallingPrevented)
                            continue;

                        CellData cellData3 = allCells[i][j - 1];
                        if (cellData3 == null)
                            continue;

                        CellVisualData vis = cellData.cellVisualData;
                        if (vis.precludeAllTileDrawing)
                            continue;

                        DungeonMaterial dungeonMaterial = self.roomMaterialDefinitions[vis.roomVisualTypeIndex];
                        if (dungeonMaterial == null)
                            continue;

                        if (!vis.HasTriggeredPitVFX)
                        {
                            vis.HasTriggeredPitVFX = true;
                            vis.PitVFXCooldown = UnityEngine.Random.Range(1f, dungeonMaterial.PitVFXMaxCooldown / 2f);
                            vis.PitParticleCooldown = UnityEngine.Random.Range(0f, 1f);
                        }

                        if (inHell || (inForge && dungeonMaterial.usesFacewallGrids))
                        {
                            vis.PitParticleCooldown -= BraveTime.DeltaTime;
                            if (vis.PitParticleCooldown <= 0f)
                            {
                                Vector3 position = BraveUtility.RandomVector2(cellData.position.ToVector2(), cellData.position.ToVector2() + Vector2.one).ToVector3ZisY();
                                vis.PitParticleCooldown = UnityEngine.Random.Range(0.35f, 0.95f);
                                if (inForge)
                                    GlobalSparksDoer.DoSingleParticle(position, Vector3.zero, null, 0.375f, null, GlobalSparksDoer.SparksType.EMBERS_SWIRLING);
                                else
                                {
                                    RoomHandler parentRoom = cellData.parentRoom;
                                    if (parentRoom != null && parentRoom.area.PrototypeRoomCategory != PrototypeDungeonRoom.RoomCategory.BOSS)
                                        GlobalSparksDoer.DoSingleParticle(position, Vector3.up, null, null, null, GlobalSparksDoer.SparksType.BLACK_PHANTOM_SMOKE);
                                }
                            }
                        }

                        if (!dungeonMaterial.UsePitAmbientVFX || dungeonMaterial.AmbientPitVFX == null || cellData3.type != CellType.PIT)
                            continue;

                        if (vis.PitVFXCooldown > 0f)
                        {
                            vis.PitVFXCooldown -= BraveTime.DeltaTime;
                            continue;
                        }

                        vis.PitVFXCooldown = UnityEngine.Random.Range(dungeonMaterial.PitVFXMinCooldown, dungeonMaterial.PitVFXMaxCooldown);
                        if (UnityEngine.Random.value >= dungeonMaterial.ChanceToSpawnPitVFXOnCooldown)
                            continue;

                        GameObject gameObject = dungeonMaterial.AmbientPitVFX[UnityEngine.Random.Range(0, dungeonMaterial.AmbientPitVFX.Count)];
                        Vector3 position2 = gameObject.transform.position;
                        SpawnManager.SpawnVFX(gameObject, cellData.position.ToVector2().ToVector3ZisY() + position2 + new Vector3(UnityEngine.Random.Range(0.25f, 0.75f), UnityEngine.Random.Range(0.25f, 0.75f), 2f), Quaternion.identity);
                    }
                }
                yield return null;
            }
            self.m_ambientVFXProcessingActive = false;
        }
    }

    /// <summary>Optimize calls to DungeonData Width and Height properties by caching results of CellData creation and using fields instead</summary>
    [HarmonyPatch]
    private static class DungeonWidthAndHeightPatches
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.OPT_DUNGEON_DIMS)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        /// <summary>Vanilla only ever calls ClearCachedCellData() after resizing Dungeon.data.cellData, so we can just adjust the width and height immediately.</summary>
        [HarmonyPatch(typeof(DungeonData), nameof(DungeonData.ClearCachedCellData))]
        [HarmonyPostfix]
        private static void ClearCachedCellData(DungeonData __instance)
        {
          __instance.m_width = __instance.cellData.Length;
          __instance.m_height = __instance.cellData[0].Length;
        }

        /// <summary>Compute width and height of DungeonData immediately after construction.</summary>
        [HarmonyPatch(typeof(DungeonData), MethodType.Constructor, new[] {typeof(CellData[][])})]
        [HarmonyPostfix]
        private static void ClearCachedCellData(DungeonData __instance, CellData[][] data)
        {
          __instance.m_width = __instance.cellData.Length;
          __instance.m_height = __instance.cellData[0].Length;
        }

        /// <summary>Patch a bunch of methods that use Width and Height to use the m_width and m_height fields instead.</summary>
        [HarmonyPatch(typeof(DungeonData), nameof(DungeonData.CheckInBounds), new[] {typeof(int), typeof(int)})]
        [HarmonyPatch(typeof(DungeonData), nameof(DungeonData.CheckInBounds), new[] {typeof(IntVector2)})]
        [HarmonyPatch(typeof(DungeonData), nameof(DungeonData.CheckInBounds), new[] {typeof(IntVector2), typeof(int)})]
        [HarmonyPatch(typeof(DungeonData), nameof(DungeonData.CheckInBoundsAndValid), new[] {typeof(int), typeof(int)})]
        [HarmonyPatch(typeof(DungeonData), nameof(DungeonData.CheckInBoundsAndValid), new[] {typeof(IntVector2)})]
        [HarmonyPatch(typeof(DungeonData), nameof(DungeonData.FloodFillDungeonExterior))]
        [HarmonyPatch(typeof(DungeonData), nameof(DungeonData.GenerateInterestingVisuals))]
        [HarmonyPatch(typeof(DungeonData), nameof(DungeonData.CheckIntegrity))]
        [HarmonyPatch(typeof(DungeonData), nameof(DungeonData.ExciseElbows))]
        [HarmonyPatch(typeof(DungeonData), nameof(DungeonData.PostGenerationCleanup))]
        [HarmonyPatch(typeof(DungeonData), nameof(DungeonData.GetRoomVisualTypeAtPosition), new[] {typeof(int), typeof(int)})]
        [HarmonyPatch(typeof(Minimap), nameof(Minimap.InitializeMinimap))]
        [HarmonyPatch(typeof(TileSpriteClipper), nameof(TileSpriteClipper.ClipToTileBounds))]
        [HarmonyPatch(typeof(TK2DInteriorDecorator), nameof(TK2DInteriorDecorator.PlaceLightDecoration))]
        [HarmonyPatch(typeof(PhysicsEngine), nameof(PhysicsEngine.LateUpdate))]
        [HarmonyPatch(typeof(OcclusionLayer), nameof(OcclusionLayer.GetRValueForCell))]
        [HarmonyPatch(typeof(OcclusionLayer), nameof(OcclusionLayer.GetGValueForCell))]
        [HarmonyPatch(typeof(OcclusionLayer), nameof(OcclusionLayer.GetCellOcclusion))]
        [HarmonyPatch(typeof(RoomHandler), nameof(RoomHandler.StampCell), new[] {typeof(int), typeof(int), typeof(bool)})]
        [HarmonyPatch(typeof(RoomHandler), nameof(RoomHandler.StampCellAsExit))] // has another "width" field that causes problems
        [HarmonyILManipulator]
        private static void DungeonDataWidthAndHeightPatchesIL(ILContext il, MethodBase original)
        {
            ILCursor cursor = new ILCursor(il);
            Type ot = original.DeclaringType;
            int replacements = 0;
            while (cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCallOrCallvirt<DungeonData>("get_Width")))
            {
              ++replacements;
              cursor.Remove();
              cursor.Emit(OpCodes.Ldfld, typeof(DungeonData).GetField("m_width", BindingFlags.Instance | BindingFlags.NonPublic));
            }

            cursor.Index = 0;
            while (cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCallOrCallvirt<DungeonData>("get_Height")))
            {
              ++replacements;
              cursor.Remove();
              cursor.Emit(OpCodes.Ldfld, typeof(DungeonData).GetField("m_height", BindingFlags.Instance | BindingFlags.NonPublic));
            }
            // GGVDebug.Log($"  made {replacements} Width and Height replacements in {original.Name}");
        }
    }

    /// <summary>Optimize various calculation functions</summary>
    [HarmonyPatch]
    private static class MathOptimizations
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.OPT_MATH)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        /// <summary>Optimize ClosestPointOnRectangle() by avoiding function calls and taking advantage of the fact that rectangles are axis-aligned.</summary>
        [HarmonyPatch(typeof(BraveMathCollege), nameof(BraveMathCollege.ClosestPointOnRectangle))]
        [HarmonyPrefix]
        private static bool FastClosestPointOnRectanglePatch(Vector2 point, Vector2 origin, Vector2 dimensions, ref Vector2 __result)
        {
          float x = point.x;
          float y = point.y;
          float left = origin.x;
          float right = left + dimensions.x;
          float bottom = origin.y;
          float top = bottom + dimensions.y;
          if (x <= left)
            __result = new Vector2(left, (y < bottom) ? bottom : (y > top) ? top : y);
          else if (x >= right)
            __result = new Vector2(right, (y < bottom) ? bottom : (y > top) ? top : y);
          else if (y <= bottom)
            __result = new Vector2((x < left) ? left : (x > right) ? right : x, bottom);
          else if (y >= top)
            __result = new Vector2((x < left) ? left : (x > right) ? right : x, top);
          else // we're inside the rectangle, so find the closest edge
          {

            float midH = 0.5f * (left + right);
            float midV = 0.5f * (bottom + top);
            if (x < midH) // left half
            {
              if (y < midV) // bottom left quadrant
              {
                if ((x - left) < (y - bottom)) // closer to left edge than bottom
                  __result = new Vector2(left, y);
                else // closer to bottom edge than left
                  __result = new Vector2(x, bottom);
              }
              else // top left quadrant
              {
                if ((x - left) < (top - y)) // closer to left edge than top
                  __result = new Vector2(left, y);
                else // closer to top edge than left
                  __result = new Vector2(x, top);
              }
            }
            else // right half
            {
              if (y < midV) // bottom right quadrant
              {
                if ((right - x) < (y - bottom)) // closer to right edge than bottom
                  __result = new Vector2(right, y);
                else // closer to bottom edge than right
                  __result = new Vector2(x, bottom);
              }
              else // top right quadrant
              {
                if ((right - x) < (top - y)) // closer to right edge than top
                  __result = new Vector2(right, y);
                else // closer to top edge than right
                  __result = new Vector2(x, top);
              }
            }
          }
          return false;
        }

        /// <summary>Optimize TransformPixel() by avoiding redundant calls as much as possible</summary>
        [HarmonyPatch(typeof(PixelCollider), nameof(PixelCollider.TransformPixel))]
        [HarmonyPrefix]
        private static bool FastTransformPixelPatch(PixelCollider __instance, Vector2 pixel, Vector2 pivot, float rotation, Vector2 scale, ref Vector2 __result)
        {
          float x = pixel.x - pivot.x;
          float y = pixel.y - pivot.y;
          float rot = rotation * ((float)Math.PI / 180f);
          float cos = Mathf.Cos(rot);
          float sin = Mathf.Sin(rot);
          __result = new Vector2((x * cos - y * sin) * scale.x + pivot.x, (x * sin + y * cos) * scale.y + pivot.y);
          return false;
        }

        /// <summary>Optimize Combine() by avoiding vector math</summary>
        [HarmonyPatch(typeof(b2AABB), nameof(b2AABB.Combine), typeof(b2AABB))]
        [HarmonyPrefix]
        private static bool Fastb2AABBCombine(ref b2AABB __instance, b2AABB aabb)
        {
          __instance.lowerBound.x = (__instance.lowerBound.x < aabb.lowerBound.x) ? __instance.lowerBound.x : aabb.lowerBound.x;
          __instance.lowerBound.y = (__instance.lowerBound.y < aabb.lowerBound.y) ? __instance.lowerBound.y : aabb.lowerBound.y;
          __instance.upperBound.x = (__instance.upperBound.x > aabb.upperBound.x) ? __instance.upperBound.x : aabb.upperBound.x;
          __instance.upperBound.y = (__instance.upperBound.y > aabb.upperBound.y) ? __instance.upperBound.y : aabb.upperBound.y;
          return false;
        }

        /// <summary>Optimize Combine() by avoiding vector math</summary>
        [HarmonyPatch(typeof(b2AABB), nameof(b2AABB.Combine), typeof(b2AABB), typeof(b2AABB))]
        [HarmonyPrefix]
        private static bool Fastb2AABBCombine(ref b2AABB __instance, b2AABB aabb1, b2AABB aabb2)
        {
          __instance.lowerBound.x = (aabb1.lowerBound.x < aabb2.lowerBound.x) ? aabb1.lowerBound.x : aabb2.lowerBound.x;
          __instance.lowerBound.y = (aabb1.lowerBound.y < aabb2.lowerBound.y) ? aabb1.lowerBound.y : aabb2.lowerBound.y;
          __instance.upperBound.x = (aabb1.upperBound.x > aabb2.upperBound.x) ? aabb1.upperBound.x : aabb2.upperBound.x;
          __instance.upperBound.y = (aabb1.upperBound.y > aabb2.upperBound.y) ? aabb1.upperBound.y : aabb2.upperBound.y;
          return false;
        }
    }

    /// <summary>Minimize property invocations when updating sprite z depths</summary>
    [HarmonyPatch]
    private static class DepthCheckOptimizations
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.OPT_DEPTH_CHECKS)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        [HarmonyPatch(typeof(tk2dBaseSprite), nameof(tk2dBaseSprite.UpdateZDepthInternal))]
        [HarmonyPrefix]
        private static bool FastUpdateZDepthInternal(tk2dBaseSprite __instance, float targetZValue, float currentYValue)
        {
          __instance.IsZDepthDirty = false;
          Vector3 position = __instance.m_transform.position;
          if (position.z != targetZValue)
          {
            position.z = targetZValue;
            __instance.m_transform.position = position;
          }
          if (__instance.attachedRenderers == null || __instance.attachedRenderers.Count <= 0)
            return false;

          bool isPerpendicular = __instance.IsPerpendicular;
          //NOTE: iterating backwards through this list has caused ArgumentOutOfRangeExceptions, presumably due to nested UpdateZDepthAttached
          //      calls affecting the list being iterated over
          for (int i = 0; i < __instance.attachedRenderers.Count; ++i)
          {
            tk2dBaseSprite attachedSprite = __instance.attachedRenderers[i];
            if (!attachedSprite || attachedSprite.attachParent != __instance)
            {
              __instance.attachedRenderers.RemoveAt(i--);
              continue;
            }
            attachedSprite.UpdateZDepthAttached(targetZValue, currentYValue, isPerpendicular);
            if (!attachedSprite.independentOrientation && isPerpendicular != attachedSprite.IsPerpendicular)
              attachedSprite.IsPerpendicular = isPerpendicular;
          }
          return false;
        }
    }

    /// <summary>Don't do hit tests for GUI controls when the game isn't even paused or responding to them.</summary>
    [HarmonyPatch]
    private static class GUIHitTestOptimizations
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.OPT_MOUSE_EVENTS)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        [HarmonyPatch(typeof(dfGUIManager), nameof(dfGUIManager.HitTest))]
        [HarmonyPrefix]
        private static bool HitTestPatch(dfGUIManager __instance, Vector2 screenPosition, ref dfControl __result)
        {
            //NOTE: alternate fix is to make controls that don't accept mouse events non-interactive (even though they're marked as interactive)
            //NOTE: DisplayingConversationBar doesn't seem to be a necessary check, the mouse works fine when it's open curiously
            __result = null;
            if (GameManager.HasInstance && !GameManager.Instance.IsPaused && (!Minimap.m_instance || !Minimap.Instance.m_isFullscreen) && !Foyer.DoIntroSequence && !Foyer.DoMainMenu)
              return false; // disable gui stuff completely while the game is not paused
            return true;
        }
    }
}
