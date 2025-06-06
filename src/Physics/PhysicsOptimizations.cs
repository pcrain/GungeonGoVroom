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
