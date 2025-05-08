namespace GGV;

internal static partial class Patches
{
    /// <summary>Optimized version of PhysicsEngine.Pointcast(IntVector2, ...) without unnecessary delegate creation</summary>
    [HarmonyPatch]
    private static class OptimiseIntVectorPointcastPatch
    {
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
          if (!GGVConfig.OPT_POINTCAST)
          {
            result = null; // must be assigned, so leave it null
            return true; // call original
          }
          // GGVDebug.Log($"speed o:");
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
    static class DungeonHandleAmbientPitVFXPatch
    {
        [HarmonyPatch(typeof(Dungeon), nameof(Dungeon.HandleAmbientPitVFX))]
        static IEnumerator Postfix(IEnumerator orig, Dungeon __instance)
        {
            if (GGVConfig.OPT_PIT_VFX)
              return HandleAmbientPitVFXFast(__instance);
            return orig; // disabled for now
        }

        private static IEnumerator HandleAmbientPitVFXFast(Dungeon self)
        {
            const int DISTANCE_THRESH = 3;
            self.m_ambientVFXProcessingActive = true;
            System.Diagnostics.Stopwatch pitWatch = System.Diagnostics.Stopwatch.StartNew();
            while (self.m_ambientVFXProcessingActive)
            {
                pitWatch.Reset();
                pitWatch.Start();
                if (GameManager.Options.ShaderQuality == GameOptions.GenericHighMedLowOption.LOW || GameManager.Options.ShaderQuality == GameOptions.GenericHighMedLowOption.VERY_LOW)
                {
                    // GGVDebug.Log($"  pit handling took {pitWatch.ElapsedTicks,4} ticks");
                    yield return null;
                    continue;
                }
                if (!GameManager.Instance.IsLoadingLevel)
                {
                    CameraController mainCameraController = GameManager.Instance.MainCameraController;
                    if (!mainCameraController || self.data == null)
                    {
                        // GGVDebug.Log($"  pit handling took {pitWatch.ElapsedTicks,4} ticks");
                        continue;
                    }
                    DungeonData allData = self.data;
                    CellData[][] allCells = allData.cellData;
                    IntVector2 camMin = mainCameraController.MinVisiblePoint.ToIntVector2(VectorConversions.Floor);
                    IntVector2 camMax = mainCameraController.MaxVisiblePoint.ToIntVector2(VectorConversions.Ceil);
                    int xMin = Mathf.Max(camMin.x, DISTANCE_THRESH);
                    int yMin = Mathf.Max(camMin.y, DISTANCE_THRESH);
                    int xMax = Mathf.Min(camMax.x, allData.Width - DISTANCE_THRESH - 1);
                    int yMax = Mathf.Min(camMax.y, allData.Height - DISTANCE_THRESH - 1);
                    bool inHell = self.tileIndices.tilesetId == GlobalDungeonData.ValidTilesets.HELLGEON;
                    bool inForge = self.tileIndices.tilesetId == GlobalDungeonData.ValidTilesets.FORGEGEON;
                    for (int i = xMin; i <= xMax; i++)
                    {
                        for (int j = yMin; j <= yMax; j++)
                        {
                            CellData cellData2 = allCells[i][j + 1];
                            if (cellData2 == null || cellData2.type != CellType.PIT)
                            {
                                j++; // we can safely skip the next iteration since it will just check this cell again
                                continue;
                            }

                            CellData cellData = allCells[i][j];
                            if (cellData == null || cellData.type != CellType.PIT || cellData.fallingPrevented || cellData.cellVisualData.precludeAllTileDrawing)
                                continue;

                            CellData cellData3 = allCells[i][j - 1];
                            if (cellData3 == null)
                                continue;

                            if (inHell)
                            {
                                CellData cellData4 = allCells[i][j + 2];
                                if (cellData4 == null || cellData4.type != CellType.PIT)
                                    continue;
                            }

                            DungeonMaterial dungeonMaterial = self.roomMaterialDefinitions[cellData.cellVisualData.roomVisualTypeIndex];
                            if (dungeonMaterial == null)
                                continue;

                            RoomHandler parentRoom = cellData.parentRoom;
                            if (!cellData.cellVisualData.HasTriggeredPitVFX)
                            {
                                cellData.cellVisualData.HasTriggeredPitVFX = true;
                                cellData.cellVisualData.PitVFXCooldown = UnityEngine.Random.Range(1f, dungeonMaterial.PitVFXMaxCooldown / 2f);
                                cellData.cellVisualData.PitParticleCooldown = UnityEngine.Random.Range(0f, 1f);
                            }
                            if (inHell || (inForge && dungeonMaterial.usesFacewallGrids))
                            {
                                cellData.cellVisualData.PitParticleCooldown -= BraveTime.DeltaTime;
                                if (cellData.cellVisualData.PitParticleCooldown <= 0f)
                                {
                                    Vector3 position = BraveUtility.RandomVector2(cellData.position.ToVector2(), cellData.position.ToVector2() + Vector2.one).ToVector3ZisY();
                                    cellData.cellVisualData.PitParticleCooldown = UnityEngine.Random.Range(0.35f, 0.95f);
                                    if (inForge && dungeonMaterial.usesFacewallGrids)
                                    {
                                        GlobalSparksDoer.DoSingleParticle(position, Vector3.zero, null, 0.375f, null, GlobalSparksDoer.SparksType.EMBERS_SWIRLING);
                                    }
                                    else if (inHell && parentRoom != null && parentRoom.area.PrototypeRoomCategory != PrototypeDungeonRoom.RoomCategory.BOSS)
                                    {
                                        GlobalSparksDoer.DoSingleParticle(position, Vector3.up, null, null, null, GlobalSparksDoer.SparksType.BLACK_PHANTOM_SMOKE);
                                    }
                                }
                            }
                            if (!dungeonMaterial.UsePitAmbientVFX || dungeonMaterial.AmbientPitVFX == null || cellData3.type != CellType.PIT)
                            {
                                continue;
                            }
                            if (cellData.cellVisualData.PitVFXCooldown > 0f)
                            {
                                cellData.cellVisualData.PitVFXCooldown -= BraveTime.DeltaTime;
                                continue;
                            }
                            if (UnityEngine.Random.value < dungeonMaterial.ChanceToSpawnPitVFXOnCooldown)
                            {
                                GameObject gameObject = dungeonMaterial.AmbientPitVFX[UnityEngine.Random.Range(0, dungeonMaterial.AmbientPitVFX.Count)];
                                Vector3 position2 = gameObject.transform.position;
                                SpawnManager.SpawnVFX(gameObject, cellData.position.ToVector2().ToVector3ZisY() + position2 + new Vector3(UnityEngine.Random.Range(0.25f, 0.75f), UnityEngine.Random.Range(0.25f, 0.75f), 2f), Quaternion.identity);
                            }
                            cellData.cellVisualData.PitVFXCooldown = UnityEngine.Random.Range(dungeonMaterial.PitVFXMinCooldown, dungeonMaterial.PitVFXMaxCooldown);
                        }
                    }
                    // GGVDebug.Log($"  pit handling for {(camMax.x - camMin.x + 1)*(camMax.y - camMin.y + 1)} cells took {pitWatch.ElapsedTicks,4} ticks");
                }
                yield return null;
            }
            self.m_ambientVFXProcessingActive = false;
        }
    }

}
