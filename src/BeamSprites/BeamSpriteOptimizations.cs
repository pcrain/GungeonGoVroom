namespace GGV;

using static BasicBeamController;

[HarmonyPatch]
internal static class BeamSpriteOptimizations
{
  private static bool Prepare(MethodBase original)
  {
    if (!GGVConfig.OPT_BEAMS) // NOTE: shared with beam poolers
      return false;
    if (original == null)
      GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
    else
      GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
    return true;
  }

  /// <summary>Optimize SetTiledSpriteGeom() using as much inline logic as possible.</summary>
  [HarmonyPatch(typeof(BasicBeamController), nameof(BasicBeamController.SetTiledSpriteGeom))]
  [HarmonyPrefix]
  private static bool BasicBeamControllerSetTiledSpriteGeomPatch(BasicBeamController __instance, Vector3[] pos, Vector2[] uv, int offset, out Vector3 boundsCenter, out Vector3 boundsExtents, tk2dSpriteDefinition spriteDef, Vector3 scale, Vector2 dimensions, tk2dBaseSprite.Anchor anchor, float colliderOffsetZ, float colliderExtentZ)
  {
    boundsCenter = Vector3.zero;
    boundsExtents = Vector3.zero;
    Vector2 baseXY = __instance.transform.position.XY();
    int num = Mathf.RoundToInt(spriteDef.untrimmedBoundsDataExtents.x / spriteDef.texelSize.x);
    int num2 = num / __instance.m_beamQuadPixelWidth;
    int num3 = Mathf.CeilToInt(dimensions.x / (float)__instance.m_beamQuadPixelWidth);
    int num4 = Mathf.CeilToInt((float)num3 / (float)num2);
    if (__instance.TileType == BeamTileType.Flowing)
    {
      num3 = __instance.m_bones.Count - 1;
      LinkedList<BeamBone> bones = __instance.m_bones;
      num4 = 0;
      for (LinkedListNode<BeamBone> next = __instance.m_bones.First; next != null; next = next.Next)
        if (next.Value.SubtileNum == 0)
          ++num4;
      if (__instance.m_bones.First.Value.SubtileNum != 0)
        num4++;
      if (__instance.m_bones.Last.Value.SubtileNum == 0)
        num4--;
    }
    Vector2 vector = new Vector2(dimensions.x * spriteDef.texelSize.x * scale.x, dimensions.y * spriteDef.texelSize.y * scale.y);
    Vector2 vector2 = Vector2.Scale(spriteDef.texelSize, scale) * 0.1f;
    Vector3 a = new Vector3((float)__instance.m_beamQuadPixelWidth * spriteDef.texelSize.x, spriteDef.untrimmedBoundsDataExtents.y, spriteDef.untrimmedBoundsDataExtents.z);
    a = Vector3.Scale(a, scale);
    float subtileLength = 0f;
    Quaternion quaternion = Quaternion.Euler(0f, 0f, __instance.Direction.ToAngle());
    LinkedListNode<BeamBone> curNode = __instance.m_bones.First;
    LinkedListNode<BeamBone> nextNode = curNode != null ? curNode.Next : null;
    int idx = offset;
    float invSubtileWidth = 1f / __instance.m_beamSpriteSubtileWidth;
    float xScale = scale.x;
    float yScale = scale.y * __instance.m_projectileScale;
    float zScale = scale.z;

    tk2dSpriteAnimationClip startClip = __instance.UsesBeamStartAnimation ? __instance.spriteAnimator.GetClipByName(__instance.CurrentBeamStartAnimation) : null;
    tk2dSpriteAnimationClip endClip   = __instance.UsesBeamEndAnimation   ? __instance.spriteAnimator.GetClipByName(__instance.CurrentBeamEndAnimation)   : null;

    for (int i = 0; i < num4; i++)
    {
      int num6 = 0;
      int num7 = num2 - 1;
      if (__instance.TileType == BeamTileType.GrowAtBeginning)
      {
        if (i == 0 && num3 % num2 != 0)
          num6 = num2 - num3 % num2;
      }
      else if (__instance.TileType == BeamTileType.GrowAtEnd)
      {
        if (i == num4 - 1 && num3 % num2 != 0)
          num7 = num3 % num2 - 1;
      }
      else if (__instance.TileType == BeamTileType.Flowing)
      {
        if (i == 0)
          num6 = curNode.Value.SubtileNum;
        if (i == num4 - 1)
          num7 = __instance.m_bones.Last.Previous.Value.SubtileNum;
      }
      tk2dSpriteDefinition curDef = spriteDef;
      if (startClip != null && i == 0)
        curDef = __instance.m_beamSprite.Collection.spriteDefinitions[startClip.frames[Mathf.Min(startClip.frames.Length - 1, __instance.spriteAnimator.CurrentFrame)].spriteId];
      if (endClip != null && i == num4 - 1)
        curDef = __instance.m_beamSprite.Collection.spriteDefinitions[endClip.frames[Mathf.Min(endClip.frames.Length - 1, __instance.spriteAnimator.CurrentFrame)].spriteId];
      float num8 = 0f;
      if (i == 0)
      {
        if (__instance.TileType == BeamTileType.GrowAtBeginning)
          num8 = 1f - Mathf.Abs(vector.x % (a.x * (float)num2)) / (a.x * (float)num2);
        else if (__instance.TileType == BeamTileType.Flowing)
          num8 = __instance.m_uvOffset;
      }
      for (int j = num6; j <= num7; j++)
      {
        BeamBone curBone = null;
        BeamBone nextBone = null;
        if (curNode != null)
        {
          curBone = curNode.Value;
          if (nextNode != null)
            nextBone = nextNode.Value;
        }
        float num9 = 1f;
        if (__instance.TileType == BeamTileType.GrowAtBeginning)
        {
          if (i == 0 && j == 0 && (float)num3 * a.x >= Mathf.Abs(vector.x) + vector2.x)
            num9 = Mathf.Abs(vector.x / a.x) - (float)(num3 - 1);
        }
        else if (__instance.TileType == BeamTileType.GrowAtEnd)
        {
          if (Mathf.Abs(subtileLength + a.x) > Mathf.Abs(vector.x) + vector2.x)
            num9 = vector.x % a.x / a.x;
        }
        else if (__instance.TileType == BeamTileType.Flowing)
        {
          if (i == 0 && curNode == __instance.m_bones.First)
            num9 = (nextBone.PosX - curBone.PosX) / __instance.m_beamQuadUnitWidth;
          else if (i == num4 - 1 && nextNode.Next == null)
            num9 = (nextBone.PosX - curBone.PosX) / __instance.m_beamQuadUnitWidth;
        }
        float z = 0f;
        if (__instance.RampHeightOffset != 0f && subtileLength < 5f)
          z = zScale * (1f - subtileLength / 5f) * (0f - __instance.RampHeightOffset); //NOTE: pre-scaled
        if (__instance.UsesBones && nextBone != null)
        {
          float rotationAngle = curBone.RotationAngle;
          float nextRotationAngle = nextBone.RotationAngle;
          float deltaRot = BraveMathCollege.ClampAngle180(nextRotationAngle - rotationAngle);
          if (deltaRot > 90f || deltaRot < -90f)
            nextRotationAngle = BraveMathCollege.ClampAngle360(nextRotationAngle + 180f);
          Vector2 curOff = curBone.Position;
          Vector2 nextOff = nextBone.Position;
          if (__instance.ProjectileAndBeamMotionModule != null)
          {
            bool inverted = __instance.projectile.Inverted;
            curOff += __instance.ProjectileAndBeamMotionModule.GetBoneOffset(curBone, __instance, inverted);
            nextOff += __instance.ProjectileAndBeamMotionModule.GetBoneOffset(nextBone, __instance, inverted);
          }
          curOff = new Vector2(curOff.x - baseXY.x, curOff.y - baseXY.y);
          nextOff = new Vector2(nextOff.x - baseXY.x, nextOff.y - baseXY.y);

          float rotRad = rotationAngle * (Mathf.PI / 180f);
          float sinR = Mathf.Sin(rotRad);
          float cosR = Mathf.Cos(rotRad);
          float nextRad = nextRotationAngle * (Mathf.PI / 180f);
          float sinN = Mathf.Sin(nextRad);
          float cosN = Mathf.Cos(nextRad);
          float vy0 = curDef.position0.y * yScale;
          float vy1 = curDef.position1.y * yScale;
          float vy2 = curDef.position2.y * yScale;
          float vy3 = curDef.position3.y * yScale;
          pos[idx + 0] = new Vector3(-sinR * vy0 + curOff.x,  cosR * vy0 + curOff.y,  z);
          pos[idx + 1] = new Vector3(-sinN * vy1 + nextOff.x, cosN * vy1 + nextOff.y, z);
          pos[idx + 2] = new Vector3(-sinR * vy2 + curOff.x,  cosR * vy2 + curOff.y,  z);
          pos[idx + 3] = new Vector3(-sinN * vy3 + nextOff.x, cosN * vy3 + nextOff.y, z);
        }
        else if (__instance.boneType == BeamBoneType.Straight)
        {
          //TODO: optimize out quaternion logic
          pos[idx + 0] = quaternion * new Vector3(subtileLength,                       curDef.position0.y * yScale, z);
          pos[idx + 1] = quaternion * new Vector3(subtileLength + xScale * num9 * a.x, curDef.position1.y * yScale, z);
          pos[idx + 2] = quaternion * new Vector3(subtileLength,                       curDef.position2.y * yScale, z);
          pos[idx + 3] = quaternion * new Vector3(subtileLength + xScale * num9 * a.x, curDef.position3.y * yScale, z);
        }
        Vector2 uvMin = Vector2.Lerp(curDef.uvs[0], curDef.uvs[1], num8);
        Vector2 uvMax = Vector2.Lerp(curDef.uvs[2], curDef.uvs[3], num8 + num9 / (float)num2);
        if (__instance.FlipBeamSpriteLocal && __instance.Direction.x < 0f)
        {
          float y = uvMin.y;
          uvMin.y = uvMax.y;
          uvMax.y = y;
        }
        if (curDef.flipped == tk2dSpriteDefinition.FlipMode.Tk2d)
        {
          uv[idx + 0] = uvMin;
          uv[idx + 1] = new Vector2(uvMin.x, uvMax.y);
          uv[idx + 2] = new Vector2(uvMax.x, uvMin.y);
          uv[idx + 3] = uvMax;
        }
        else
        {
          uv[idx + 0] = uvMin;
          uv[idx + 1] = new Vector2(uvMax.x, uvMin.y);
          uv[idx + 2] = new Vector2(uvMin.x, uvMax.y);
          uv[idx + 3] = uvMax;
        }
        idx += 4;
        subtileLength += a.x * num9;
        num8 += num9 * invSubtileWidth;
        curNode = nextNode;
        if (nextNode != null)
          nextNode = nextNode.Next;
      }
    }

    float minPosX = float.MaxValue;
    float minPosY = float.MaxValue;
    float minPosZ = float.MaxValue;
    float maxPosX = float.MinValue;
    float maxPosY = float.MinValue;
    float maxPosZ = float.MinValue;
    if (__instance.RampHeightOffset != 0f)
    {
      for (int k = 0; k < pos.Length; k++)
      {
        if (pos[k].x < minPosX) minPosX = pos[k].x;
        if (pos[k].y < minPosY) minPosY = pos[k].y;
        if (pos[k].z < minPosZ) minPosZ = pos[k].z;
        if (pos[k].x > maxPosX) maxPosX = pos[k].x;
        if (pos[k].y > maxPosY) maxPosY = pos[k].y;
        if (pos[k].z > maxPosZ) maxPosZ = pos[k].z;
      }
    }
    else // fast track -- z is always 0
    {
      minPosZ = 0f;
      maxPosZ = 0f;
      for (int k = 0; k < pos.Length; k++)
      {
        if (pos[k].x < minPosX) minPosX = pos[k].x;
        if (pos[k].y < minPosY) minPosY = pos[k].y;
        if (pos[k].x > maxPosX) maxPosX = pos[k].x;
        if (pos[k].y > maxPosY) maxPosY = pos[k].y;
      }
    }
    boundsExtents = new Vector3(0.5f * (maxPosX - minPosX), 0.5f * (maxPosY - minPosY), 0.5f * (maxPosZ - minPosZ));
    boundsCenter = new Vector3(minPosX + boundsExtents.x, minPosY + boundsExtents.y, minPosZ + boundsExtents.z);

    return false;
  }
}
