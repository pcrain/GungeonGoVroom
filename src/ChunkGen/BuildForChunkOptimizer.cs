namespace GGV;

using tk2dRuntime.TileMap;

/// <summary>Optimizes tons of unnecessary allocation out of RenderMeshBuilder.BuildForChunk()</summary>
[HarmonyPatch]
internal static class BuildForChunkOptimizer
{
  private const int _MAX_MATS = 100;
  private static readonly List<Vector3>  vertices             = new List<Vector3>();
  private static readonly List<Color>    colors               = new List<Color>();
  private static readonly List<Vector2>  uvs                  = new List<Vector2>();
  private static readonly List<Material> usedMaterials        = new List<Material>();
  private static readonly List<int>[]    perMaterialTriangles = new List<int>[_MAX_MATS];
  private static readonly Vector3[]      defPositions         = new Vector3[4];
  private static bool firstBuild = true;

  private static int _TotalVertices = 0;

  private static Vector3[] verticesA = new Vector3[0];
  private static Color[]     colorsA = new Color[0];
  private static Vector2[]      uvsA = new Vector2[0];

  private static bool Prepare(MethodBase original)
  {
    if (!GGVConfig.OPT_CHUNKBUILD)
      return false;
    if (original == null)
      GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
    else
      GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
    return true;
  }

  [HarmonyPatch(typeof(RenderMeshBuilder), nameof(RenderMeshBuilder.BuildForChunk))]
  [HarmonyPrefix]
  private static bool RenderMeshBuilderBuildForChunkPatch(tk2dTileMap tileMap, SpriteChunk chunk, bool useColor, bool skipPrefabs, int baseX, int baseY, LayerInfo layerData)
  {
      if (firstBuild)
      {
        for (int i = 0; i < _MAX_MATS; ++i)
          perMaterialTriangles[i] = new List<int>();
        firstBuild = false;
      }

      GameManager instance = GameManager.Instance;
      Dungeon dungeon = instance.Dungeon;
      vertices.Clear();
      colors.Clear();
      uvs.Clear();

      if (layerData.preprocessedFlags == null || layerData.preprocessedFlags.Length == 0)
        layerData.preprocessedFlags = new bool[tileMap.width * tileMap.height];

      int[] spriteIds = chunk.spriteIds;
      Vector3 tileSize = tileMap.data.tileSize;
      int numTileDefs = tileMap.SpriteCollectionInst.spriteDefinitions.Length;
      UnityEngine.Object[] tilePrefabs = tileMap.data.tilePrefabs;
      tk2dSpriteDefinition firstValidDefinition = tileMap.SpriteCollectionInst.FirstValidDefinition;
      bool recalcNormals = firstValidDefinition != null && firstValidDefinition.normals != null && firstValidDefinition.normals.Length > 0;
      Color32 color = ((!useColor || tileMap.ColorChannel == null) ? Color.white : tileMap.ColorChannel.clearColor);
      BuilderUtil.GetLoopOrder(tileMap.data.sortMethod, chunk.Width, chunk.Height, out int x, out int x2, out int dx, out int y, out int y2, out int dy);
      tileMap.data.GetTileOffset(out float x3, out float y3);
      int numTotalMaterials = tileMap.SpriteCollectionInst.materials.Length;
      for (int i = 0; i < numTotalMaterials; i++)
        perMaterialTriangles[i].Clear();

      IntVector2 intVector = new IntVector2(layerData.overrideChunkXOffset, layerData.overrideChunkYOffset);
      int num2 = tileMap.partitionSizeX + 1;
      for (int j = y; j != y2; j += dy)
      {
        float num3 = (float)((baseY + j) & 1) * x3;
        for (int k = x; k != x2; k += dx)
        {
          Vector3 vector = new Vector3(tileSize.x * ((float)k + num3), tileSize.y * (float)j, 0f);
          IntVector2 intVector2 = IntVector2.Zero;
          if (tileMap.isGungeonTilemap)
          {
            intVector2 = vector.IntXY() + new IntVector2(baseX, baseY);
            if ((chunk.roomReference != null && !chunk.roomReference.ContainsPosition(intVector2 - intVector)) || intVector2.y * tileMap.width + intVector2.x >= layerData.preprocessedFlags.Length || layerData.preprocessedFlags[intVector2.y * tileMap.width + intVector2.x])
              continue;
            layerData.preprocessedFlags[intVector2.y * tileMap.width + intVector2.x] = true;
          }
          int rawTile = spriteIds[j * chunk.Width + k];
          int tileFromRawTile = BuilderUtil.GetTileFromRawTile(rawTile);
          if (tileFromRawTile < 0 || tileFromRawTile >= numTileDefs || (skipPrefabs && (bool)tilePrefabs[tileFromRawTile]))
            continue;

          tk2dSpriteDefinition tk2dSpriteDefinition = tileMap.SpriteCollectionInst.spriteDefinitions[tileFromRawTile];
          if (!layerData.ForceNonAnimating && tk2dSpriteDefinition.metadata.usesAnimSequence)
            continue;

          bool flag2 = BuilderUtil.IsRawTileFlagSet(rawTile, tk2dTileFlags.FlipX);
          bool flag3 = BuilderUtil.IsRawTileFlagSet(rawTile, tk2dTileFlags.FlipY);
          bool rot = BuilderUtil.IsRawTileFlagSet(rawTile, tk2dTileFlags.Rot90);
          ColorChunk colorChunk = ((!tileMap.isGungeonTilemap)
            ? tileMap.ColorChannel.GetChunk(Mathf.FloorToInt((float)baseX / (float)tileMap.partitionSizeX), Mathf.FloorToInt((float)baseY / (float)tileMap.partitionSizeY))
            : tileMap.ColorChannel.GetChunk(Mathf.FloorToInt((float)intVector2.x / (float)tileMap.partitionSizeX), Mathf.FloorToInt((float)intVector2.y / (float)tileMap.partitionSizeY)));
          bool reallyUseColor = useColor;
          if (colorChunk == null || (colorChunk.colors.Length == 0 && colorChunk.colorOverrides.GetLength(0) == 0))
            reallyUseColor = false;

          int count = vertices.Count;
          defPositions[0] = tk2dSpriteDefinition.position0;
          defPositions[1] = tk2dSpriteDefinition.position1;
          defPositions[2] = tk2dSpriteDefinition.position2;
          defPositions[3] = tk2dSpriteDefinition.position3;

          IntVector2 cellPos = vector.IntXY() + new IntVector2(baseX + RenderMeshBuilder.CurrentCellXOffset, baseY + RenderMeshBuilder.CurrentCellYOffset);
          bool inBounds = dungeon.data.CheckInBounds(cellPos, 1);
          IntVector2 intVector3 = new IntVector2(k, j);
          if (tileMap.isGungeonTilemap)
            intVector3 = new IntVector2(intVector2.x % tileMap.partitionSizeX, intVector2.y % tileMap.partitionSizeY);
          for (int l = 0; l < defPositions.Length; l++)
          {
            Vector3 vector2 = BuilderUtil.ApplySpriteVertexTileFlags(tileMap, tk2dSpriteDefinition, defPositions[l], flag2, flag3, rot);
            if (reallyUseColor)
            {
              Color32 color2 = colorChunk.colorOverrides[intVector3.y * num2 + intVector3.x, l % 4];
              if (tileMap.isGungeonTilemap && (color2.r != color.r || color2.g != color.g || color2.b != color.b || color2.a != color.a))
              {
                Color item = color2;
                colors.Add(item);
              }
              else
              {
                Color a = colorChunk.colors[intVector3.y * num2 + intVector3.x];
                Color b = colorChunk.colors[intVector3.y * num2 + intVector3.x + 1];
                Color a2 = colorChunk.colors[(intVector3.y + 1) * num2 + intVector3.x];
                Color b2 = colorChunk.colors[(intVector3.y + 1) * num2 + (intVector3.x + 1)];
                Vector3 vector3 = vector2 - tk2dSpriteDefinition.untrimmedBoundsDataCenter;
                Vector3 vector4 = vector3 + tileMap.data.tileSize * 0.5f;
                float t = Mathf.Clamp01(vector4.x / tileMap.data.tileSize.x);
                float t2 = Mathf.Clamp01(vector4.y / tileMap.data.tileSize.y);
                Color item2 = Color.Lerp(Color.Lerp(a, b, t), Color.Lerp(a2, b2, t), t2);
                colors.Add(item2);
              }
            }
            else
              colors.Add(Color.black);

            Vector3 item3 = vector;
            if (tileMap.isGungeonTilemap)
            {
              if (inBounds && dungeon.data.isAnyFaceWall(cellPos.x, cellPos.y))
              {
                Vector3 vector5 = ((!dungeon.data.isFaceWallHigher(cellPos.x, cellPos.y)) ? new Vector3(0f, 0f, 1f) : new Vector3(0f, 0f, -1f));
                CellData cellData = dungeon.data[cellPos];
                if (cellData.diagonalWallType == DiagonalWallType.NORTHEAST)
                  vector5.z += (1f - vector2.x) * 2f;
                else if (cellData.diagonalWallType == DiagonalWallType.NORTHWEST)
                  vector5.z += vector2.x * 2f;
                item3 += new Vector3(0f, 0f, vector.y - vector2.y) + vector2 + vector5;
              }
              else if (inBounds && dungeon.data.isTopDiagonalWall(cellPos.x, cellPos.y) && layerData.name == "Collision Layer")
              {
                Vector3 vector6 = new Vector3(0f, 0f, -3f);
                item3 += new Vector3(0f, 0f, vector.y + vector2.y) + vector2 + vector6;
              }
              else if (layerData.name == "AOandShadows")
              {
                if (inBounds && dungeon.data[cellPos] != null && dungeon.data[cellPos].type == CellType.PIT)
                {
                  Vector3 vector7 = new Vector3(0f, 0f, 2.5f);
                  item3 += new Vector3(0f, 0f, vector.y + vector2.y) + vector2 + vector7;
                }
                else
                {
                  Vector3 vector8 = new Vector3(0f, 0f, 1f);
                  item3 += new Vector3(0f, 0f, vector.y + vector2.y) + vector2 + vector8;
                }
              }
              else if (layerData.name == "Pit Layer")
              {
                Vector3 vector9 = new Vector3(0f, 0f, 2f);
                if (dungeon.data.CheckInBounds(cellPos.x, cellPos.y + 2))
                {
                  if (dungeon.data.cellData[cellPos.x][cellPos.y + 1].type != CellType.PIT || dungeon.data.cellData[cellPos.x][cellPos.y + 2].type != CellType.PIT)
                  {
                    bool flag5 = dungeon.data.cellData[cellPos.x][cellPos.y + 1].type != CellType.PIT;
                    if (dungeon.debugSettings.WALLS_ARE_PITS && dungeon.data.cellData[cellPos.x][cellPos.y + 1].isExitCell)
                      flag5 = false;
                    if (flag5)
                      vector9 = new Vector3(0f, 0f, 0f);
                    item3 += new Vector3(0f, 0f, vector.y - vector2.y) + vector2 + vector9;
                  }
                  else
                    item3 += new Vector3(0f, 0f, vector.y + vector2.y + 1f) + vector2;
                }
                else
                  item3 += new Vector3(0f, 0f, vector.y + vector2.y + 1f) + vector2;
              }
              else
                item3 += new Vector3(0f, 0f, vector.y + vector2.y) + vector2;
            }
            else
              item3 += vector2;
            vertices.Add(item3);
            uvs.Add(tk2dSpriteDefinition.uvs[l]);
          }
          bool flag6 = false;
          if (flag2)
            flag6 = !flag6;

          if (flag3)
            flag6 = !flag6;

          List<int> list4 = perMaterialTriangles[tk2dSpriteDefinition.materialId];
          for (int m = 0; m < tk2dSpriteDefinition.indices.Length; m++)
          {
            int num5 = ((!flag6) ? m : (tk2dSpriteDefinition.indices.Length - 1 - m));
            list4.Add(count + tk2dSpriteDefinition.indices[num5]);
          }
        }
      }

      if (chunk.mesh == null)
        chunk.mesh = tk2dUtil.CreateMesh();
      chunk.mesh.SetVertices(vertices);
      chunk.mesh.SetUVs(0, uvs);
      chunk.mesh.SetColors(colors);
      // GGVDebug.Log($"got {vertices.Count} vertices ({(_TotalVertices += vertices.Count)} total)");

      usedMaterials.Clear();
      int numUsedMasterials = 0;
      for (int n = 0; n < numTotalMaterials; ++n)
      {
        if (perMaterialTriangles[n].Count == 0)
          continue;
        usedMaterials.Add(tileMap.SpriteCollectionInst.materialInsts[n]);
        numUsedMasterials++;
      }

      if (numUsedMasterials > 0)
      {
        chunk.mesh.subMeshCount = numUsedMasterials;
        chunk.gameObject.GetComponent<Renderer>().materials = usedMaterials.ToArray();
        int submesh = 0;
        for (int n = 0; n < numTotalMaterials; ++n)
          if (perMaterialTriangles[n].Count > 0)
            chunk.mesh.SetTriangles(perMaterialTriangles[n], submesh++);
      }

      chunk.mesh.RecalculateBounds();
      if (recalcNormals)
        chunk.mesh.RecalculateNormals();
      if (tileMap.isGungeonTilemap)
        chunk.gameObject.transform.position = chunk.gameObject.transform.position.WithZ((float)baseY + chunk.gameObject.transform.position.z);
      chunk.gameObject.GetComponent<MeshFilter>().sharedMesh = chunk.mesh;

      return false; // skip original method
  }
}
