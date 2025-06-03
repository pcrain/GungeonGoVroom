namespace GGV;

using static PhysicsEngine;
using static PixelCollider;

[HarmonyPatch]
internal static class PhysicsOptimizations
{
  private static MethodBase TargetMethod() {
    return typeof(PixelCollider).GetMethod(nameof(PixelCollider.LinearCast), new Type[] {
      typeof(PixelCollider), typeof(IntVector2), typeof(List<StepData>), typeof(LinearCastResult).MakeByRefType(), typeof(bool), typeof(float)
    });
  }

  [HarmonyPrefix]
  private static bool PixelColliderLinearCastPatch(PixelCollider __instance, PixelCollider otherCollider, IntVector2 pixelsToMove, List<StepData> stepList, out LinearCastResult result, bool traverseSlopes, float currentSlope, ref bool __result)
  {
    if (!__instance.Enabled || (otherCollider.DirectionIgnorer != null && otherCollider.DirectionIgnorer(pixelsToMove)))
    {
      result = null;
      __result = false;
      return false; // skip original method
    }

    System.Diagnostics.Stopwatch castWatch = System.Diagnostics.Stopwatch.StartNew();

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
    float timeUsed            = 0f;

    bool[] myPixels           = __instance.m_bestPixels.m_bits;
    int myPixelW              = __instance.m_bestPixels.m_width;
    bool myIsAABB             = __instance.m_bestPixels.IsAabb;

    bool[] otherPixels        = otherCollider.m_bestPixels.m_bits;
    int otherPixelW           = otherCollider.m_bestPixels.m_width;
    bool otherIsAABB          = otherCollider.m_bestPixels.IsAabb;

    result                    = LinearCastResult.Pool.Allocate();
    result.MyPixelCollider    = __instance;
    result.OtherPixelCollider = null;
    result.TimeUsed           = 0f;
    result.CollidedX          = false;
    result.CollidedY          = false;
    result.NewPixelsToMove.x  = 0;
    result.NewPixelsToMove.y  = 0;
    result.Overlap            = false;

    for (int i = 0; i < stepList.Count; i++)
    {
      StepData step = stepList[i];
      int deltaX = step.deltaPos.x;
      int deltaY = step.deltaPos.y;
      timeUsed += step.deltaTime;

      int stepX = myX + xMoved + deltaX;
      int stepY = myY + yMoved + deltaY;
      if (stepX + myW - 1 < theirX || stepX > theirX + theirW - 1 || stepY + myH - 1 < theirY || stepY > theirY + theirH - 1)
      {
        xMoved += deltaX;
        yMoved += deltaY;
        continue;
      }

      int left   = theirX     - stepX; if (left < 0)           left   = 0;
      int bottom = theirY     - stepY; if (bottom < 0)         bottom = 0;
      int right  = theirRight - stepX; if (right > maxOffsetX) right  = maxOffsetX;
      int top    = theirTop   - stepY; if (top > maxOffsetY)   top    = maxOffsetY;

      for (int j = left; j <= right; j++)
      {
        int posX = j + xMoved + deltaX - sepX;
        if (posX < 0 || posX >= theirW)
          continue;

        for (int k = bottom; k <= top; k++)
        {
          if (!myIsAABB && !myPixels[j + k * myPixelW])
            continue;

          int posY = k + yMoved + deltaY - sepY;
          if (posY < 0 || posY >= theirH)
            continue;
          if (!otherIsAABB && !otherPixels[posX + posY * otherPixelW])
            continue;

          result.TimeUsed = timeUsed;
          result.CollidedX = deltaX != 0;
          result.CollidedY = deltaY != 0;
          result.NewPixelsToMove = new IntVector2(xMoved, yMoved);
          result.MyPixelCollider = __instance;
          result.OtherPixelCollider = otherCollider;
          result.Contact = new Vector2(
            ((float)(j + xMoved + deltaX + myX) + 0.5f) / 16f,
            ((float)(k + yMoved + deltaY + myY) + 0.5f) / 16f);
          result.Normal = new Vector2(-deltaX, -deltaY); //TODO: potential opportunity to fix vanilla bug with seams
          if (otherCollider.NormalModifier != null)
            result.Normal = otherCollider.NormalModifier(result.Normal);
          __result = true;
          castWatch.Stop(); System.Console.WriteLine($"    {castWatch.ElapsedTicks,6} ticks cast ({((float)(totalTicks += castWatch.ElapsedTicks) / ++totalCasts)} avg)");
          return false; // skip original method
        }
      }
      xMoved += deltaX;
      yMoved += deltaY;
    }
    result.NewPixelsToMove = new IntVector2(xMoved, yMoved);
    __result = false;
    castWatch.Stop(); System.Console.WriteLine($"    {castWatch.ElapsedTicks,6} ticks cast ({((float)(totalTicks += castWatch.ElapsedTicks) / ++totalCasts)} avg)");
    return false; // skip original method
  }

  private static long totalTicks = 0;
  private static int totalCasts = 0;
}
