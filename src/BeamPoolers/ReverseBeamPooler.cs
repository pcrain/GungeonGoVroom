namespace GGV;

using static ReverseBeamController;

[HarmonyPatch]
internal static class ReverseBeamPooler
{
    private static readonly LinkedList<Bone> _BonePool = new();
    private static int _TotalBones = 0;
    private static bool _Enabled = false; // cannot be enabled / disabled at run time without causing issues, so lock it in during patching

    [HarmonyPatch(typeof(Bone), MethodType.Constructor, new[] {typeof(Vector2)})]
    [HarmonyPostfix]
    private static void NewBoneCounter()
    {
      GGVDebug.Log($"constructed {++_TotalBones} bones");
    }

    /// <summary>Replaces all calls to constructor</summary>
    private static void RentAndAdd(LinkedList<Bone> bones, Vector2 pos)
    {
        if (_BonePool.Count == 0)
          _BonePool.AddLast(new Bone(default));

        LinkedListNode<Bone> node = _BonePool.Last;
        _BonePool.RemoveLast();

        Bone bone                  = node.Value;
        bone.pos                   = pos;
        bone.normal                = default;

        bones.AddLast(node);
    }

    /// <summary>Replaces all calls to Clear() and must be called in OnDestroy()</summary>
    private static void ReturnAll(LinkedList<Bone> bones)
    {
        if (bones == null)
          return;
        while (bones.Count > 0)
        {
          LinkedListNode<Bone> node = bones.Last;
          bones.RemoveLast();
          _BonePool.AddLast(node);
        }
        // GGVDebug.Log($"returned {_BonePool.Count} / {_TotalBones} bones");
    }

    private static bool ReplaceConstructor(this ILCursor cursor)
    {
        if (!cursor.TryGotoNext(MoveType.Before,
          instr => instr.MatchNewobj<Bone>(),
          instr => instr.MatchCallvirt(typeof(LinkedList<>).MakeGenericType(typeof(Bone)).GetMethod("AddLast", new[]{typeof(Bone)})),
          instr => instr.MatchPop()))
        {
          GGVDebug.Log($"no dice ):");
          return false;
        }
        // GGVDebug.Log($"replacing bone construction with rental (int)");
        cursor.Remove(); // remove constructor
        cursor.Remove(); // remove AddLast(Bone)
        cursor.Remove(); // remove Pop for ignored LinkedListNode(Bone)
        cursor.CallPrivate(typeof(ReverseBeamPooler), nameof(ReverseBeamPooler.RentAndAdd));
        return true;
    }

    private static bool ReplaceClear(this ILCursor cursor)
    {
        if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCallvirt(typeof(LinkedList<>).MakeGenericType(typeof(Bone)).GetMethod("Clear"))))
          return false;
        // GGVDebug.Log($"replacing Clear() with ReturnAll()");
        cursor.Remove();
        cursor.CallPrivate(typeof(ReverseBeamPooler), nameof(ReverseBeamPooler.ReturnAll));
        return true;
    }

    /// <summary>Replace all construction of Bones with Rents</summary>
    [HarmonyPatch(typeof(ReverseBeamController), nameof(ReverseBeamController.DrawBezierCurve))]
    [HarmonyILManipulator]
    private static void StartPatch(ILContext il)
    {
        _Enabled = GGVConfig.OPT_BEAMS;
        if (!_Enabled)
          return;
        ILCursor cursor = new ILCursor(il);
        if (!cursor.ReplaceConstructor()) return;
        if (!cursor.ReplaceConstructor()) return;
        // GGVDebug.Log("patched ReverseBeamController.DrawBezierCurve!");
    }

    /// <summary>Replace all construction of Bones with Rents</summary>
    [HarmonyPatch(typeof(ReverseBeamController), nameof(ReverseBeamController.HandleBeamFrame))]
    [HarmonyILManipulator]
    private static void HandleBeamFramePatch(ILContext il)
    {
        _Enabled = GGVConfig.OPT_BEAMS;
        if (!_Enabled)
          return;
        ILCursor cursor = new ILCursor(il);
        if (!cursor.ReplaceClear()) return;
        // GGVDebug.Log("patched ReverseBeamController.HandleBeamFrame!");
    }

    [HarmonyPatch(typeof(ReverseBeamController), nameof(ReverseBeamController.OnDestroy))]
    [HarmonyPrefix]
    static void BasicBeamControllerOnDestroyPatch(ReverseBeamController __instance)
    {
        if (!_Enabled)
            return;
        if (__instance.m_bones != null)
            ReturnAll(__instance.m_bones);
    }
}
