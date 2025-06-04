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

  //NOTE: deprecated in favor of full method override below
  // [HarmonyPatch(typeof(CustomTrailRenderer), nameof(CustomTrailRenderer.UpdateMesh))]
  // [HarmonyILManipulator]
  // private static void CustomTrailRendererUpdateMeshPatchIL(ILContext il)
  // {
  //     ILCursor cursor = new ILCursor(il);
  //     if (!cursor.TryGotoNext(MoveType.Before,
  //       instr => instr.MatchLdnull(),
  //       instr => instr.MatchStelemRef()))
  //       return;
  //     cursor.Remove();
  //     cursor.Remove();
  //     cursor.CallPrivate(typeof(CustomTrailPooler), nameof(ReturnElement));

  //     for (int i = 0; i < 2; ++i)
  //     {
  //       if (!cursor.TryGotoNext(MoveType.Before,
  //         instr => instr.MatchNewobj<Point>(),
  //         instr => instr.MatchStelemRef()))
  //         return;

  //       cursor.Remove();
  //       cursor.Remove();
  //       cursor.CallPrivate(typeof(CustomTrailPooler), nameof(RentElement));
  //     }
  // }

  /// <summary>Class for managing scratch buffers for meshes</summary>
  private class ScratchBuffer
  {
    private const int MAX_BUFFER = 20; // shouldn't ever need more tan 2^20 vertices...hopefully
    private static readonly ScratchBuffer[] _Buffers = new ScratchBuffer[MAX_BUFFER];

    private Vector3[] _vertices = null;
    private Vector2[] _uvs      = null;
    private int[] _triangles    = null;
    private Color[] _colors     = null;

    public static void Obtain(int points, out Vector3[] vertices, out Vector2[] uvs, out int[] triangles, out Color[] colors)
    {
      // each scratch buffer holds twice as much vertex data as the one before it, so find the appropriate power of 2
      int bufferIndex = 0;
      int bufferCap = 1;
      while (points > bufferCap)
      {
        ++bufferIndex;
        bufferCap *= 2;
      }
      ScratchBuffer sb = _Buffers[bufferIndex];

      // initialize the buffer if necessary
      if (sb == null)
      {
        sb = _Buffers[bufferIndex] = new();
        sb._vertices  = new Vector3[bufferCap * 2];
        sb._uvs       = new Vector2[bufferCap * 2];
        sb._triangles = new int[(bufferCap - 1) * 6];
        sb._colors    = new Color[bufferCap * 2];
      }

      // set the alphas of unused points to 0 (to hide stray polygons in case the shader supports alpha values)
      for (int i = 2 * points; i < 2 * bufferCap; ++i)
        sb._colors[i].a = 0f;

      // set unsued triangle vertices all to 0 (to avoid weird extraneous lines being rendered)
      int numTriangles = (points - 1) * 6;
      int maxTriangles = (bufferCap - 1) * 6;
      for (int i = numTriangles; i < maxTriangles; ++i)
        sb._triangles[i] = 0;

      // return the buffers
      vertices  = sb._vertices;
      uvs       = sb._uvs;
      triangles = sb._triangles;
      colors    = sb._colors;
    }
  }

  [HarmonyPatch(typeof(CustomTrailRenderer), nameof(CustomTrailRenderer.UpdateMesh))]
  [HarmonyPrefix]
  private static bool FastUpdateMesh(CustomTrailRenderer __instance)
  {
    CustomTrailRenderer self = __instance;

    if (self.specRigidbody && self.specRigidbody.transform.rotation.eulerAngles.z != 0f)
      self.transform.localRotation = Quaternion.Euler(0f, 0f, 0f - self.specRigidbody.transform.rotation.eulerAngles.z);
    if (!self.emit)
      self.emittingDone = true;
    if (self.emittingDone)
      self.emit = false;

    int expiredPoints = 0;
    for (int num2 = self.numPoints - 1; num2 >= 0; num2--)
    {
      Point point = self.points[num2];
      if (point != null && point.timeAlive < self.lifeTime)
        break;
      expiredPoints++;
    }

    if (expiredPoints > 1)
    {
      int num3 = self.numPoints - expiredPoints + 1;
      while (self.numPoints > num3)
      {
        ReturnElement(self.points, self.numPoints - 1);
        self.numPoints--;
      }
    }

    if (self.numPoints > self.optimizeCount)
    {
      self.maxAngle += self.optimizeAngleInterval;
      self.maxVertexDistance += self.optimizeDistanceInterval;
      self.optimizeCount++;
    }

    if (self.emit)
    {
      if (self.numPoints == 0)
      {
        RentElement(self.points, self.numPoints++, self.transform);
        RentElement(self.points, self.numPoints++, self.transform);
      }
      if (self.numPoints == 1)
        self.InsertPoint();

      bool needNewPoint = false;
      float sqrMagnitude = (self.points[1].position - self.transform.position).sqrMagnitude;
      if (sqrMagnitude > self.minVertexDistance * self.minVertexDistance)
      {
        if (sqrMagnitude > self.maxVertexDistance * self.maxVertexDistance)
          needNewPoint = true;
        else if (Quaternion.Angle(self.transform.rotation, self.points[1].rotation) > self.maxAngle)
          needNewPoint = true;
      }
      if (needNewPoint)
      {
        if (self.numPoints == self.points.Length)
          Array.Resize(ref self.points, self.points.Length + 50);
        self.InsertPoint();
      }
      else
        self.points[0].Update(self.transform);
    }
    if (self.numPoints < 2)
    {
      self.renderer.enabled = false;
      return false;
    }

    self.renderer.enabled = true;
    self.lifeTimeRatio = ((self.lifeTime != 0f) ? (1f / self.lifeTime) : 0f);
    if (!self.emit)
      return false;

    //NOTE: the magic! obtain a scratch buffer since it's always going to be copied to the GPU anyway
    ScratchBuffer.Obtain(self.numPoints, out Vector3[] vertices, out Vector2[] uvs, out int[] triangles, out Color[] colors);

    float num4 = 1f / (self.points[self.numPoints - 1].timeAlive - self.points[0].timeAlive);
    for (int i = 0; i < self.numPoints; i++)
    {
      Point point2 = self.points[i];
      float num5 = point2.timeAlive * self.lifeTimeRatio;
      Vector3 pointNormal = ((i == 0 && self.numPoints > 1) ? (self.points[i + 1].position - self.points[i].position) : ((i == self.numPoints - 1 && self.numPoints > 1) ? (self.points[i].position - self.points[i - 1].position) : ((self.numPoints <= 2) ? Vector3.right : ((self.points[i + 1].position - self.points[i].position + (self.points[i].position - self.points[i - 1].position)) * 0.5f))));
      Color color;
      if (self.colors.Length == 0)
        color = Color.Lerp(Color.white, Color.clear, num5);
      else if (self.colors.Length == 1)
        color = Color.Lerp(self.colors[0], Color.clear, num5);
      else if (self.colors.Length == 2)
        color = Color.Lerp(self.colors[0], self.colors[1], num5);
      else if (num5 <= 0f)
        color = self.colors[0];
      else if (num5 >= 1f)
        color = self.colors[self.colors.Length - 1];
      else
      {
        float num6 = num5 * (float)(self.colors.Length - 1);
        int num7 = Mathf.Min(self.colors.Length - 2, (int)Mathf.Floor(num6));
        float num8 = Mathf.InverseLerp(num7, num7 + 1, num6);
        color = Color.Lerp(self.colors[num7], self.colors[num7 + 1], num8);
      }
      colors[i * 2] = color;
      colors[i * 2 + 1] = color;
      Vector3 pointPos = point2.position;
      if (i > 0 && i == self.numPoints - 1)
      {
        float t = Mathf.InverseLerp(self.points[i - 1].timeAlive, point2.timeAlive, self.lifeTime);
        pointPos = Vector3.Lerp(self.points[i - 1].position, point2.position, t);
      }
      float width;
      if (self.widths.Length == 0)
        width = 1f;
      else if (self.widths.Length == 1)
        width = self.widths[0];
      else if (self.widths.Length == 2)
        width = Mathf.Lerp(self.widths[0], self.widths[1], num5);
      else if (num5 <= 0f)
        width = self.widths[0];
      else if (num5 >= 1f)
        width = self.widths[self.widths.Length - 1];
      else
      {
        float num10 = num5 * (float)(self.widths.Length - 1);
        int num11 = (int)Mathf.Floor(num10);
        float t2 = Mathf.InverseLerp(num11, num11 + 1, num10);
        width = Mathf.Lerp(self.widths[num11], self.widths[num11 + 1], t2);
      }
      pointNormal = pointNormal.normalized.RotateBy(Quaternion.Euler(0f, 0f, 90f)) * 0.5f * width;
      vertices[i * 2] = pointPos - self.transform.position + pointNormal;
      vertices[i * 2 + 1] = pointPos - self.transform.position - pointNormal;
      float x = (point2.timeAlive - self.points[0].timeAlive) * num4;
      uvs[i * 2] = new Vector2(x, 0f);
      uvs[i * 2 + 1] = new Vector2(x, 1f);
      if (i > 0)
      {
        int num12 = (i - 1) * 6;
        int num13 = i * 2;
        triangles[num12] = num13 - 2;
        triangles[num12 + 1] = num13 - 1;
        triangles[num12 + 2] = num13;
        triangles[num12 + 3] = num13 + 1;
        triangles[num12 + 4] = num13;
        triangles[num12 + 5] = num13 - 1;
      }
    }

    self.mesh.Clear();
    self.mesh.vertices = vertices;
    self.mesh.colors = colors;
    self.mesh.uv = uvs;
    self.mesh.triangles = triangles;

    return false;
  }
}
