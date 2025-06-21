namespace GGV;

using static PhysicsEngine;
using static PixelCollider;

/// <summary>Speeds up LinearCast by avoiding function calls, vector math, and property accesses wherever possible.</summary>
[HarmonyPatch]
internal static class LinearCastOptimization
{
  private static bool Prepare(MethodBase original)
  {
    if (!GGVConfig.OPT_LINEAR_CAST)
      return false;
    if (original == null)
      GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
    else
      GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
    return true;
  }

  private static MethodBase TargetMethod() {
    return typeof(PixelCollider).GetMethod(nameof(PixelCollider.LinearCast), new Type[] {
      typeof(PixelCollider), typeof(IntVector2), typeof(List<StepData>), typeof(LinearCastResult).MakeByRefType(), typeof(bool), typeof(float)
    });
  }

  //NOTE: this wasn't any faster, but keeping it around for future reference because it's cool
  // /// <summary>Access a List(StepData)'s underlying array directly</summary>
  // private static Func<List<StepData>, StepData[]> _GetStepArray = "_items".CreateGetter<List<StepData>, StepData[]>();
  // StepData[] stepsInternal = _GetStepArray(stepList); //NOTE: access the underlying array directly

  [HarmonyPrefix]
  private static bool PixelColliderLinearCastPatch(PixelCollider __instance, PixelCollider otherCollider, IntVector2 pixelsToMove, List<StepData> stepList, out LinearCastResult result, bool traverseSlopes, float currentSlope, ref bool __result)
  {
    if (!__instance.Enabled || (otherCollider.DirectionIgnorer != null && otherCollider.DirectionIgnorer(pixelsToMove)))
    {
      result = null;
      __result = false;
      return false; // skip original method
    }

    int xMoved                = 0;
    int yMoved                = 0;
    int myX                   = __instance.m_position.x;
    int myY                   = __instance.m_position.y;
    int myW                   = __instance.m_dimensions.x;
    int myH                   = __instance.m_dimensions.y;
    int theirX                = otherCollider.m_position.x;
    int theirY                = otherCollider.m_position.y;
    int theirW                = otherCollider.m_dimensions.x;
    int theirH                = otherCollider.m_dimensions.y;
    int theirRight            = theirX + theirW - 1;
    int theirTop              = theirY + theirH - 1;
    int sepX                  = theirX - myX;
    int sepY                  = theirY - myY;
    int maxOffsetX            = myW - 1;
    int maxOffsetY            = myH - 1;
    int minStepX              = theirX - maxOffsetX; // point at which the right of our hitbox would overlap the left of theirs
    int minStepY              = theirY - maxOffsetY; // point at which the top of our hitbox would overlap the bottom of theirs
    float timeUsed            = 0f;

    bool[] myPixels           = __instance.m_bestPixels.m_bits;
    int myPixelW              = __instance.m_bestPixels.m_width;
    bool myIsAABB             = __instance.m_bestPixels.IsAabb;

    bool[] otherPixels        = otherCollider.m_bestPixels.m_bits;
    int otherPixelW           = otherCollider.m_bestPixels.m_width;
    bool otherIsAABB          = otherCollider.m_bestPixels.IsAabb;

    result                    = LinearCastResult.Pool.Allocate();
    result.MyPixelCollider    = __instance;

    int numSteps = stepList.Count;
    for (int i = 0; i < numSteps; i++)
    {
      StepData step = stepList[i];
      int deltaX = step.deltaPos.x;
      int deltaY = step.deltaPos.y;
      int nextX = xMoved + deltaX;
      int nextY = yMoved + deltaY;
      int stepX = myX + nextX;
      int stepY = myY + nextY;
      timeUsed += step.deltaTime;
      if (stepX < minStepX || stepX > theirRight || stepY < minStepY || stepY > theirTop)
      {
        xMoved = nextX;
        yMoved = nextY;
        continue;
      }

      int left   = theirX     - stepX; if (left < 0)           left   = 0;
      int bottom = theirY     - stepY; if (bottom < 0)         bottom = 0;
      int right  = theirRight - stepX; if (right > maxOffsetX) right  = maxOffsetX;
      int top    = theirTop   - stepY; if (top > maxOffsetY)   top    = maxOffsetY;

      int baseX = nextX - sepX;
      int baseY = nextY - sepY;

      if (left < -baseX)           left   = -baseX;
      if (bottom < -baseY)         bottom = -baseY;
      if (right >= theirW - baseX) right  = theirW - baseX - 1;
      if (top >= theirH - baseY)   top    = theirH - baseY - 1;

      for (int k = bottom; k <= top; k++)
      {
        int yPixSelf = k * myPixelW;
        int yPixThem = (baseY + k) * otherPixelW + baseX;
        for (int j = left; j <= right; j++)
        {
          if (!myIsAABB && !myPixels[yPixSelf + j])
            continue;
          if (!otherIsAABB && !otherPixels[yPixThem + j])
            continue;

          result.TimeUsed = timeUsed;
          result.CollidedX = deltaX != 0;
          result.CollidedY = deltaY != 0;
          result.NewPixelsToMove = new IntVector2(xMoved, yMoved);
          result.OtherPixelCollider = otherCollider;
          result.Contact = new Vector2(((float)(j + stepX) + 0.5f) / 16f, ((float)(k + stepY) + 0.5f) / 16f);
          result.Normal = new Vector2(-deltaX, -deltaY);
          if (otherCollider.NormalModifier != null)
            result.Normal = otherCollider.NormalModifier(result.Normal);
          __result = true;
          return false; // skip original method
        }
      }
      xMoved = nextX;
      yMoved = nextY;
    }
    result.NewPixelsToMove = new IntVector2(xMoved, yMoved);
    __result = false;
    return false; // skip original method
  }
}

/// <summary>Fixes a memory leak in PhysicsUpdate where a pooled LinearCastResult is never properly freed</summary>
[HarmonyPatch]
internal static class LinearCastMemoryLeakFix
{
  private static bool Prepare(MethodBase original)
  {
    if (!GGVConfig.OPT_PHYSICS_LEAK)
      return false;
    if (original == null)
      GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
    else
      GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
    return true;
  }

  [HarmonyPatch(typeof(PhysicsEngine), nameof(PhysicsEngine.SingleCollision))]
  [HarmonyILManipulator]
  private static void PhysicsEngineSingleCollisionPatchIL(ILContext il)
  {
      ILCursor cursor = new ILCursor(il);
      if (!cursor.TryGotoNext(MoveType.After,
        instr => instr.MatchCallvirt<PixelCollider>(nameof(PixelCollider.LinearCast)),
        instr => instr.MatchBrtrue(out ILLabel _)
        ))
          return;

      cursor.Emit(OpCodes.Ldloca, 0);
      cursor.CallPrivate(typeof(LinearCastMemoryLeakFix), nameof(FreeLCR));
  }

  private static void FreeLCR(ref LinearCastResult lcr)
  {
    if (lcr != null)
      LinearCastResult.Pool.Free(ref lcr);
  }
}

/// <summary>Speeds up PixelMovementGenerator by avoiding function calls and optimizing a lot of math.</summary>
[HarmonyPatch]
internal static class PixelMovementGeneratorOptimization
{
  private static bool Prepare(MethodBase original)
  {
    if (!GGVConfig.OPT_PIXEL_MOVE)
      return false;
    if (original == null)
      GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
    else
      GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
    return true;
  }

  [HarmonyPatch(typeof(PhysicsEngine), nameof(PhysicsEngine.PixelMovementGenerator), typeof(Vector2), typeof(Vector2), typeof(IntVector2), typeof(List<PixelCollider.StepData>))]
  [HarmonyPrefix]
  private static bool FastPixelMovementGenerator(Vector2 remainder, Vector2 velocity, IntVector2 pixelsToMove, List<PixelCollider.StepData> stepList)
  {
    const float STEP_SIZE = 1f / 32f;

    int movedX        = 0;
    int movedY        = 0;
    int xpixelsToMove = pixelsToMove.x;
    int ypixelsToMove = pixelsToMove.y;
    int xSign         = xpixelsToMove > 0 ? 1 : xpixelsToMove < 0 ? -1 : 0;
    int ySign         = ypixelsToMove > 0 ? 1 : ypixelsToMove < 0 ? -1 : 0;
    float xStep       = STEP_SIZE * xSign;
    float yStep       = STEP_SIZE * ySign;
    float rx          = remainder.x;
    float ry          = remainder.y;
    float vx          = velocity.x;
    float vy          = velocity.y;
    float ivx         = 1f / vx;
    float ivy         = 1f / vy;
    IntVector2 xvec   = new IntVector2(xSign, 0);
    IntVector2 yvec   = new IntVector2(0, ySign);

    stepList.Clear();
    while (movedX != xpixelsToMove || movedY != ypixelsToMove)
    {
      float xdtime = (xStep - rx) * ivx;
      if (xdtime < 0f)
        xdtime = 0f;
      float ydtime = (yStep - ry) * ivy;
      if (ydtime < 0f)
        ydtime = 0f;
      if (movedX != xpixelsToMove && (movedY == ypixelsToMove || xdtime < ydtime))
      {
        movedX += xSign;
        rx = -xStep;
        ry += xdtime * vy;
        stepList.Add(new PixelCollider.StepData(xvec, xdtime));
      }
      else
      {
        movedY += ySign;
        rx += ydtime * vx;
        ry = -yStep;
        stepList.Add(new PixelCollider.StepData(yvec, ydtime));
      }
    }

    return false;
  }
}

/// <summary>Speeds up SetRotationAndScale by avoiding function calls and reusing scratch buffers instead of allocating memory.</summary>
[HarmonyPatch]
internal static class SetRotationAndScaleOptimization
{
  private static bool Prepare(MethodBase original)
  {
    if (!GGVConfig.OPT_PIXEL_ROTATE)
      return false;
    if (original == null)
      GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
    else
      GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
    return true;
  }

  private static readonly Vector2[] _Scratch = new Vector2[4];
  private static readonly int[] _Scratch2 = new int[4];
  private static readonly Vector2[] _ScratchVertices = new Vector2[4];

  [HarmonyPatch(typeof(PixelCollider), nameof(PixelCollider.SetRotationAndScale))]
  [HarmonyPrefix]
  private static bool FastSetRotationAndScale(PixelCollider __instance, float rotation, Vector2 scale)
  {
    BitArray2D bitArray2D = ((rotation != 0f || scale.x != 1f || scale.y != 1f) ? __instance.m_modifiedPixels : __instance.m_basePixels);
    if (__instance.m_rotation == rotation && __instance.m_scale.x == scale.x && __instance.m_scale.y == scale.y && __instance.m_bestPixels == bitArray2D && __instance.m_bestPixels != null && __instance.m_bestPixels.IsValid)
      return false;

    __instance.m_rotation = rotation;
    __instance.m_scale.x = scale.x;
    __instance.m_scale.y = scale.y;
    int width = __instance.m_basePixels.m_width;
    int height = __instance.m_basePixels.m_height;
    if (rotation == 0f && scale.x == 1f && scale.y == 1f)
    {
      __instance.m_bestPixels = __instance.m_basePixels;
      __instance.m_dimensions.x = width;
      __instance.m_dimensions.y = height;
      __instance.m_transformOffset.x = 0;
      __instance.m_transformOffset.y = 0;
      return false;
    }

    if (__instance.m_modifiedPixels == null)
      __instance.m_modifiedPixels = new BitArray2D();

    Vector2 basePivot = new Vector2(-__instance.m_offset.x, -__instance.m_offset.y);

    // populate scratch buffer with base corners of sprite BL -> BR -> TR -> TL
    _Scratch[0].x = 0.5f;
    _Scratch[1].x = (float)width - 0.5f; //NOTE: intentionally TR -> TL because that's how the original code is
    _Scratch[2].x = (float)width - 0.5f; //      even though it seems like it should be TL -> TR
    _Scratch[3].x = 0.5f;
    _Scratch[0].y = 0.5f;
    _Scratch[1].y = 0.5f;
    _Scratch[2].y = (float)height - 0.5f;
    _Scratch[3].y = (float)height - 0.5f;

    // apply inlined TransformPixel() logic to get the world coordinates of the collider's corners
    float rotationInRadians = rotation * ((float)Math.PI / 180f);
    float baseCos = Mathf.Cos(rotationInRadians);
    float baseSin = Mathf.Sin(rotationInRadians);
    for (int i = 0; i < 4; ++i)
    {
      float x = _Scratch[i].x - basePivot.x;
      float y = _Scratch[i].y - basePivot.y;
      _Scratch[i].x = (x * baseCos - y * baseSin) * scale.x + basePivot.x;
      _Scratch[i].y = (x * baseSin + y * baseCos) * scale.y + basePivot.y;
    }

    // determine the absolute world bounds of the collider
    float boundLeftF   = _Scratch[0].x;
    float boundRightF  = _Scratch[0].x;
    float boundBottomF = _Scratch[0].y;
    float boundTopF    = _Scratch[0].y;
    for (int i = 1; i < 4; ++i)
    {
      if (_Scratch[i].x < boundLeftF)
        boundLeftF = _Scratch[i].x;
      if (_Scratch[i].x > boundRightF)
        boundRightF = _Scratch[i].x;
      if (_Scratch[i].y < boundBottomF)
        boundBottomF = _Scratch[i].y;
      if (_Scratch[i].y > boundTopF)
        boundTopF = _Scratch[i].y;
    }
    int boundLeft   = (int)Math.Floor(boundLeftF);
    int boundRight  = (int)Math.Ceiling(boundRightF);
    int boundBottom = (int)Math.Floor(boundBottomF);
    int boundTop    = (int)Math.Ceiling(boundTopF);
    int boundsWidth = boundRight - boundLeft;
    int boundHeight = boundTop - boundBottom;

    // reinitialize the collider's pixels (inlined ReinitializeWithDefault())
    BitArray2D mpArray = __instance.m_modifiedPixels;
    mpArray.m_width = boundsWidth;
    mpArray.m_height = boundHeight;
    int numBits = boundsWidth * boundHeight;
    if (mpArray.m_bits == null || numBits > mpArray.m_bits.Length)
      mpArray.m_bits = new bool[(int)((float)numBits * mpArray.c_sizeScalar)];
    else
      Array.Clear(mpArray.m_bits, 0, numBits);
    mpArray.IsValid = true;
    bool[] mpBits = mpArray.m_bits;

    if (__instance.m_basePixels.IsAabb)
    {
      const int NUM_VERTICES = 4;
      _Scratch[0].x -= boundLeft;
      _Scratch[1].x -= boundLeft;
      _Scratch[2].x -= boundLeft;
      _Scratch[3].x -= boundLeft;
      _Scratch[0].y -= boundBottom;
      _Scratch[1].y -= boundBottom;
      _Scratch[2].y -= boundBottom;
      _Scratch[3].y -= boundBottom;
      _Scratch2[0] = _Scratch2[1] = _Scratch2[2] = _Scratch2[3] = 0;
      for (int i = 0; i < boundHeight; i++)
      {
        int num8 = 0;
        int num9 = NUM_VERTICES - 1;
        int j;
        for (j = 0; j < NUM_VERTICES; j++)
        {
          if (((double)_Scratch[j].y < (double)i && (double)_Scratch[num9].y >= (double)i) || ((double)_Scratch[num9].y < (double)i && (double)_Scratch[j].y >= (double)i))
            _Scratch2[num8++] = (int)(_Scratch[j].x + ((float)i - _Scratch[j].y) / (_Scratch[num9].y - _Scratch[j].y) * (_Scratch[num9].x - _Scratch[j].x));
          num9 = j;
        }
        j = 0;
        // sorting
        while (j < num8 - 1)
        {
          if (_Scratch2[j] > _Scratch2[j + 1])
          {
            int tmp = _Scratch2[j];
            _Scratch2[j] = _Scratch2[j + 1];
            _Scratch2[j + 1] = tmp;
            if (j != 0)
              j--;
          }
          else
            j++;
        }
        for (j = 0; j < num8 && _Scratch2[j] < boundsWidth - 1; j += 2)
        {
          if (_Scratch2[j + 1] <= 0)
            continue;
          if (_Scratch2[j] < 0)
            _Scratch2[j] = 0;
          if (_Scratch2[j + 1] > boundsWidth - 1)
            _Scratch2[j + 1] = boundsWidth - 1;
          for (int k = _Scratch2[j]; k < _Scratch2[j + 1]; k++)
            mpBits[k + i * boundsWidth] = true;
        }
      }
    }
    else
    {
      //NOTE: inlining most of the logic from TransformPixel()
      float relPivotX = basePivot.x - boundLeft;
      float relPivotY = basePivot.y - boundBottom;
      float negRotation = -rotation;
      float negRotationInRadians = negRotation * ((float)Math.PI / 180f);
      float cos = Mathf.Cos(negRotationInRadians);
      float sin = Mathf.Sin(negRotationInRadians);
      float invScaleX = 1f / scale.x;
      float invScaleY = 1f / scale.y;
      bool[] basePixels = __instance.m_basePixels.m_bits;
      int bpWidth = __instance.m_basePixels.m_width;
      for (int l = 0; l < boundsWidth; l++)
      {
        float relX = ((float)l + 0.5f) - relPivotX;
        for (int m = 0; m < boundHeight; m++)
        {
          float relY = ((float)m + 0.5f) - relPivotY;
          float tx = (relX * cos - relY * sin) * invScaleX + relPivotX + boundLeft;
          float ty = (relX * sin + relY * cos) * invScaleY + relPivotY + boundBottom;
          if (tx < 0f || (int)tx >= width || ty < 0f || (int)ty >= height)
            mpBits[l + m * boundsWidth] = false;
          else
            mpBits[l + m * boundsWidth] = basePixels[(int)tx + (int)ty * bpWidth];
        }
      }
    }
    __instance.m_transformOffset.x = boundLeft;
    __instance.m_transformOffset.y = boundBottom;
    __instance.m_dimensions.x = boundsWidth;
    __instance.m_dimensions.y = boundHeight;
    __instance.m_bestPixels = __instance.m_modifiedPixels;

    return false;
  }

  /// <summary>Use a static vertex buffer to avoid memory allocations.</summary>
  [HarmonyPatch(typeof(PixelCollider), nameof(PixelCollider.RegenerateFrom3dCollider))]
  [HarmonyILManipulator]
  private static void PixelColliderRegenerateFrom3dColliderPatchIL(ILContext il)
  {
      ILCursor cursor = new ILCursor(il);
      if (!cursor.TryGotoNext(MoveType.Before,
        instr => instr.MatchLdcI4(4),
        instr => instr.MatchNewarr<Vector2>()
        ))
          return;
      cursor.RemoveRange(2);
      cursor.Emit(OpCodes.Ldsfld, typeof(SetRotationAndScaleOptimization)
        .GetField("_ScratchVertices", BindingFlags.Static | BindingFlags.NonPublic));
  }
}

/// <summary>Various optimizations speeding up Physics calculations that particularly benefit flowing beams (e.g., Fossilized Gun)</summary>
[HarmonyPatch]
internal static class PointcastOptimizations
{
  private static bool Prepare(MethodBase original)
  {
    if (!GGVConfig.OPT_POINTCAST) //NOTE: shared with OptimiseIntVectorPointcastPatch
      return false;
    if (original == null)
      GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
    else
      GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
    return true;
  }

  /// <summary>Inline pixel contains logic.</summary>
  [HarmonyPatch(typeof(PixelCollider), nameof(PixelCollider.AABBContainsPixel))]
  [HarmonyPrefix]
  private static bool PixelColliderAABBContainsPixelPatch(PixelCollider __instance, IntVector2 pixel, ref bool __result)
  {
      __result =
        pixel.x >= __instance.m_position.x &&
        pixel.x <  (__instance.m_position.x + __instance.m_dimensions.x) &&
        pixel.y >= __instance.m_position.y &&
        pixel.y <  (__instance.m_position.y + __instance.m_dimensions.y);
      return false;    // skip the original method
  }

  /// <summary>Inline coarse pass logic.</summary>
  [HarmonyPatch(typeof(PhysicsEngine), nameof(PhysicsEngine.Pointcast_CoarsePass))]
  [HarmonyPrefix]
  private static bool PhysicsEnginePointcast_CoarsePassPatch(PhysicsEngine __instance, ICollidableObject collidable, IntVector2 point, bool collideWithTriggers, int rayMask, CollisionLayer? sourceLayer, ref bool __result)
  {
    List<PixelCollider> colliders = collidable.GetPixelColliders();
    int numColliders = colliders.Count;
    int px = point.x;
    int py = point.y;
    for (int i = 0; i < numColliders; i++)
    {
      PixelCollider pc = colliders[i];
      if (pc.IsTrigger && !collideWithTriggers)
        continue;
      if (px <   pc.m_position.x ||
          px >=  (pc.m_position.x + pc.m_dimensions.x) ||
          py <   pc.m_position.y ||
          py >=  (pc.m_position.y + pc.m_dimensions.y))
        continue;
      if (!pc.CanCollideWith(rayMask, sourceLayer))
        continue;
      if (!pc.m_bestPixels.IsAabb && !pc.m_bestPixels.m_bits[(px - pc.m_position.x) + (py - pc.m_position.y) * pc.m_bestPixels.m_width])
        continue;
      __result = true;
      return false;
    }
    __result = false;
    return false;
  }

  /// <summary>Optimize GetTileFracAtPosition() knowing we only use rectangular tilemaps, the worldToLocalMatrix is always the identity matrix, tileorigin is at 0,0,0, and tilesize is 1,1,0</summary>
  [HarmonyPatch(typeof(tk2dTileMap), nameof(tk2dTileMap.GetTileFracAtPosition))]
  [HarmonyPrefix]
  private static bool tk2dTileMapGetTileFracAtPositionPatch(tk2dTileMap __instance, Vector3 position, out float x, out float y, ref bool __result)
  {
      x = position.x;
      y = position.y;
      __result = x >= 0f && x <= (float)__instance.width && y >= 0f && y <= (float)__instance.height;
      return false;
  }

  /// <summary>Optimize GetTileAtPosition() knowing we only use rectangular tilemaps, the worldToLocalMatrix is always the identity matrix, tileorigin is at 0,0,0, and tilesize is 1,1,0</summary>
  [HarmonyPatch(typeof(tk2dTileMap), nameof(tk2dTileMap.GetTileAtPosition))]
  [HarmonyPrefix]
  private static bool tk2dTileMapGetTileAtPositionPatch(tk2dTileMap __instance, Vector3 position, out int x, out int y, ref bool __result)
  {
      x = (int)position.x;
      y = (int)position.y;
      //NOTE: using position.x because negative values round towards 0, so, e.g., -0.75f >= 0 is false, but (int)(-0.75f) >= 0 is true
      __result = position.x >= 0 && position.x <= __instance.width && y >= 0 && y <= __instance.height;
      return false;
  }

  /// <summary>Inline logic in PixelToUnit to avoid implict function calls</summary>
  [HarmonyPatch(typeof(PhysicsEngine), nameof(PhysicsEngine.PixelToUnit), typeof(IntVector2))]
  [HarmonyPrefix]
  private static bool PhysicsEnginePixelToUnitPatch(IntVector2 pixel, ref Vector2 __result)
  {
    __result = new Vector2(0.0625f * pixel.x, 0.0625f * pixel.y);
    return false;
  }
}
