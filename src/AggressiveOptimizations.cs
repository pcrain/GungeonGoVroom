namespace GGV;

/* TODO: future optimizations
    - HasPassiveItem / HasActiveItem
*/

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
                int xMax = Mathf.Min(camMax.x, allData.Width - DISTANCE_THRESH - 1);
                int yMax = Mathf.Min(camMax.y, allData.Height - DISTANCE_THRESH - 1);
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

    /// <summary>Optimize ClosestPointOnRectangle() by avoiding function calls and taking advantage of the fact that rectangles are axis-aligned.</summary>
    [HarmonyPatch(typeof(BraveMathCollege), nameof(BraveMathCollege.ClosestPointOnRectangle))]
    [HarmonyPrefix]
    public static bool FastClosestPointOnRectanglePatch(Vector2 point, Vector2 origin, Vector2 dimensions, ref Vector2 __result)
    {
      if (!GGVConfig.OPT_MATH)
        return true; // call original method

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
}
