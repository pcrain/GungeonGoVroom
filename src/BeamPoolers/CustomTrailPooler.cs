namespace GGV;

using static CustomTrailRenderer;

[HarmonyPatch]
internal static class CustomTrailPooler
{
  private static readonly LinkedList<Point> _PointPool = new(); // contains Points
  private static readonly LinkedList<Point> _ActiveNodes = new(); // contains empty nodes
  private static readonly LinkedList<Tuple<Transform,Point[]>> _ActiveTransforms = new();
  private static readonly LinkedList<Tuple<Transform,Point[]>> _InactiveTransforms = new();

  private static bool Prepare(MethodBase original)
  {
    if (!GGVConfig.OPT_TRAILS)
      return false;
    if (original == null)
      GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
    else
      GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
    return true;
  }

  // [HarmonyPatch(typeof(CustomTrailRenderer.Point), MethodType.Constructor, new[] {typeof(Transform)})]
  // [HarmonyPrefix]
  // private static void CustomTrailRendererPointPatch(CustomTrailRenderer.Point __instance, Transform trans)
  // {
  //   GGVDebug.Log($"created {++points} points");
  // }
  // private static int points = 0;

  private static void RentElement(Point[] points, int index, Transform t)
  {
      // we don't have an OnDestroy() method to hook, so scan existing transforms to see if we need to clean up any points
      int numTransforms = _ActiveTransforms.Count;
      bool found = false;
      for (int i = 0; i < numTransforms; ++i)
      {
        var outerNode = _ActiveTransforms.First;
        _ActiveTransforms.RemoveFirst();
        var pair = outerNode.Value;
        if (pair.First)
        {
          if (pair.First == t)
            found = true; // current transform is already known, don't add it later
          _ActiveTransforms.AddLast(outerNode);
          continue;
        }
        // parent transform has been destroyed, so return all of our points
        Point[] tpoints = pair.Second;
        int maxPoints = tpoints.Length;
        for (int n = 0; i < maxPoints; ++n)
        {
          if (tpoints[n] == null)
            break;
          Return(tpoints[n]);
        }
        outerNode.Value = null;
        _InactiveTransforms.AddLast(outerNode);
      }
      if (!found)
      {
        if (_InactiveTransforms.Count == 0)
          _InactiveTransforms.AddLast(new LinkedListNode<Tuple<Transform,Point[]>>(null));
        var outerNode = _InactiveTransforms.Last;
        _InactiveTransforms.RemoveLast();
        outerNode.Value = new Tuple<Transform,Point[]>(t, points);
        _ActiveTransforms.AddLast(outerNode);
      }

      // now actually handle renting logic
      if (_PointPool.Count == 0)
        _PointPool.AddLast(new Point(t));

      LinkedListNode<Point> node = _PointPool.Last;
      _PointPool.RemoveLast();

      Point point       = node.Value;
      point.position    = t.position;
      point.rotation    = t.rotation;
      point.timeCreated = BraveTime.ScaledTimeSinceStartup;

      node.Value = null;
      _ActiveNodes.AddLast(node);
      points[index] = point;
  }

  private static Point Return(Point point)
  {
      LinkedListNode<Point> node = _ActiveNodes.Last;
      _ActiveNodes.RemoveLast();
      node.Value = point;
      _PointPool.AddLast(node);
      return null;
  }

  private static void ReturnElement(Point[] points, int index)
  {
    points[index] = Return(points[index]);
  }

  [HarmonyPatch(typeof(CustomTrailRenderer), nameof(CustomTrailRenderer.Clear))]
  [HarmonyPrefix]
  private static void CustomTrailRendererClearPatch(CustomTrailRenderer __instance)
  {
    for (int num = __instance.numPoints - 1; num >= 0; num--)
      __instance.points[num] = Return(__instance.points[num]);
    __instance.numPoints = 0;
  }

  [HarmonyPatch(typeof(CustomTrailRenderer), nameof(CustomTrailRenderer.UpdateMesh))]
  [HarmonyILManipulator]
  private static void CustomTrailRendererUpdateMeshPatchIL(ILContext il)
  {
      ILCursor cursor = new ILCursor(il);
      if (!cursor.TryGotoNext(MoveType.Before,
        instr => instr.MatchLdnull(),
        instr => instr.MatchStelemRef()))
        return;
      cursor.Remove();
      cursor.Remove();
      cursor.CallPrivate(typeof(CustomTrailPooler), nameof(ReturnElement));

      for (int i = 0; i < 2; ++i)
      {
        if (!cursor.TryGotoNext(MoveType.Before,
          instr => instr.MatchNewobj<Point>(),
          instr => instr.MatchStelemRef()))
          return;

        cursor.Remove();
        cursor.Remove();
        cursor.CallPrivate(typeof(CustomTrailPooler), nameof(RentElement));
      }
  }

  [HarmonyPatch(typeof(CustomTrailRenderer), nameof(CustomTrailRenderer.InsertPoint))]
  [HarmonyILManipulator]
  private static void CustomTrailRendererInsertPointPatchIL(ILContext il)
  {
      ILCursor cursor = new ILCursor(il);
      if (!cursor.TryGotoNext(MoveType.Before,
        instr => instr.MatchNewobj<Point>(),
        instr => instr.MatchStelemRef()))
        return;

      cursor.Remove();
      cursor.Remove();
      cursor.CallPrivate(typeof(CustomTrailPooler), nameof(RentElement));
  }
}
