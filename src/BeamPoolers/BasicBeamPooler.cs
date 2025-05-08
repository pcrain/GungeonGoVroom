namespace GGV;

using static BasicBeamController;

//NOTE: this still techincally creates a bunch of LinkedListNodes, so there's room for further optimization
[HarmonyPatch]
internal static class BasicBeamPooler
{
    private static readonly LinkedList<BeamBone> _BonePool = new();
    private static int _TotalBones = 0;
    private static bool _Enabled = false; // cannot be enabled / disabled at run time without causing issues, so lock it in during patching

    [HarmonyPatch(typeof(BeamBone), MethodType.Constructor, new[] {typeof(float), typeof(float), typeof(int)})]
    [HarmonyPatch(typeof(BeamBone), MethodType.Constructor, new[] {typeof(float), typeof(Vector2), typeof(Vector2)})]
    [HarmonyPatch(typeof(BeamBone), MethodType.Constructor, new[] {typeof(BeamBone)})]
    [HarmonyPostfix]
    private static void NewBoneCounter()
    {
      GGVDebug.Log($"constructed {++_TotalBones} bones");
    }

    /// <summary>Replaces all calls to int constructor</summary>
    private static BeamBone RentInt(float posX, float rotationAngle, int subtileNum)
    {
        if (_BonePool.Count == 0)
          _BonePool.AddLast(new BeamBone(0, 0, 0));

        LinkedListNode<BeamBone> node = _BonePool.Last;
        _BonePool.RemoveLast();

        BeamBone bone              = node.Value;
        bone.PosX                  = posX;
        bone.RotationAngle         = rotationAngle;
        bone.Position              = default;
        bone.Velocity              = default;
        bone.SubtileNum            = subtileNum;
        bone.HomingRadius          = default;
        bone.HomingAngularVelocity = default;
        bone.HomingDampenMotion    = default;

        return bone;
    }

    /// <summary>Replaces all calls to Vector2 constructor</summary>
    private static BeamBone RentVec(float posX, Vector2 position, Vector2 velocity)
    {
        if (_BonePool.Count == 0)
          _BonePool.AddLast(new BeamBone(0, 0, 0));

        LinkedListNode<BeamBone> node = _BonePool.Last;
        _BonePool.RemoveLast();

        BeamBone bone              = node.Value;
        bone.PosX                  = posX;
        bone.RotationAngle         = default;
        bone.Position              = position;
        bone.Velocity              = velocity;
        bone.SubtileNum            = default;
        bone.HomingRadius          = default;
        bone.HomingAngularVelocity = default;
        bone.HomingDampenMotion    = default;

        return bone;
    }

    /// <summary>Replaces all calls to BeamBone constructor</summary>
    private static BeamBone RentCopy(BeamBone other)
    {
        if (_BonePool.Count == 0)
          _BonePool.AddLast(new BeamBone(0, 0, 0));

        LinkedListNode<BeamBone> node = _BonePool.Last;
        _BonePool.RemoveLast();

        BeamBone bone              = node.Value;
        bone.PosX                  = other.PosX;
        bone.RotationAngle         = other.RotationAngle;
        bone.Position              = other.Position;
        bone.Velocity              = other.Velocity;
        bone.SubtileNum            = other.SubtileNum;
        bone.HomingRadius          = other.HomingRadius;
        bone.HomingAngularVelocity = other.HomingAngularVelocity;
        bone.HomingDampenMotion    = other.HomingDampenMotion;

        return bone;
    }

    /// <summary>Replaces all calls to RemoveFirst()</summary>
    private static void ReturnFirst(LinkedList<BeamBone> bones)
    {
        if (bones == null || bones.Count == 0)
          return;
        LinkedListNode<BeamBone> node = bones.First;
        bones.Remove(node);
        _BonePool.AddLast(node);
        // GGVDebug.Log($"returned {_BonePool.Count} / {_TotalBones} bones");
    }

    /// <summary>Replaces all calls to RemoveLast()</summary>
    private static void ReturnLast(LinkedList<BeamBone> bones)
    {
        if (bones == null || bones.Count == 0)
          return;
        LinkedListNode<BeamBone> node = bones.Last;
        bones.Remove(node);
        _BonePool.AddLast(node);
        // GGVDebug.Log($"returned {_BonePool.Count} / {_TotalBones} bones");
    }

    /// <summary>Replaces all calls to Clear() and must be called in OnDestroy()</summary>
    private static void ReturnAll(LinkedList<BeamBone> bones)
    {
        if (bones == null)
          return;
        while (bones.Count > 0)
        {
          LinkedListNode<BeamBone> node = bones.Last;
          bones.RemoveLast();
          _BonePool.AddLast(node);
        }
        // GGVDebug.Log($"returned {_BonePool.Count} / {_TotalBones} bones");
    }

    private static bool ReplaceIntConstructor(this ILCursor cursor)
    {
        if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchNewobj<BeamBone>()))
          return false;
        // GGVDebug.Log($"replacing bone construction with rental (int)");
        cursor.Remove();
        cursor.CallPrivate(typeof(BasicBeamPooler), nameof(BasicBeamPooler.RentInt));
        return true;
    }

    private static bool ReplaceVecConstructor(this ILCursor cursor)
    {
        if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchNewobj<BeamBone>()))
          return false;
        // GGVDebug.Log($"replacing bone construction with rental (vec)");
        cursor.Remove();
        cursor.CallPrivate(typeof(BasicBeamPooler), nameof(BasicBeamPooler.RentVec));
        return true;
    }

    private static bool ReplaceCopyConstructor(this ILCursor cursor)
    {
        if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchNewobj<BeamBone>()))
          return false;
        // GGVDebug.Log($"replacing bone construction with rental (copy)");
        cursor.Remove();
        cursor.CallPrivate(typeof(BasicBeamPooler), nameof(BasicBeamPooler.RentCopy));
        return true;
    }

    private static bool ReplaceRemoveFirst(this ILCursor cursor)
    {
        if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCallvirt(typeof(LinkedList<>).MakeGenericType(typeof(BeamBone)).GetMethod("RemoveFirst"))))
          return false;
        // GGVDebug.Log($"replacing RemoveFirst() with ReturnFirst()");
        cursor.Remove();
        cursor.CallPrivate(typeof(BasicBeamPooler), nameof(BasicBeamPooler.ReturnFirst));
        return true;
    }

    private static bool ReplaceRemoveLast(this ILCursor cursor)
    {
        if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCallvirt(typeof(LinkedList<>).MakeGenericType(typeof(BeamBone)).GetMethod("RemoveLast"))))
          return false;
        // GGVDebug.Log($"replacing RemoveLast() with ReturnLast()");
        cursor.Remove();
        cursor.CallPrivate(typeof(BasicBeamPooler), nameof(BasicBeamPooler.ReturnLast));
        return true;
    }

    private static bool ReplaceClear(this ILCursor cursor)
    {
        if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCallvirt(typeof(LinkedList<>).MakeGenericType(typeof(BeamBone)).GetMethod("Clear"))))
          return false;
        // GGVDebug.Log($"replacing Clear() with ReturnAll()");
        cursor.Remove();
        cursor.CallPrivate(typeof(BasicBeamPooler), nameof(BasicBeamPooler.ReturnAll));
        return true;
    }

    /// <summary>Replace all construction of BeamBones with Rents</summary>
    [HarmonyPatch(typeof(BasicBeamController), nameof(BasicBeamController.Start))]
    [HarmonyILManipulator]
    private static void StartPatch(ILContext il)
    {
        _Enabled = GGVConfig.OPT_BEAMS;
        if (!_Enabled)
          return;
        ILCursor cursor = new ILCursor(il);
        if (!cursor.ReplaceIntConstructor()) return;
        if (!cursor.ReplaceIntConstructor()) return;
        GGVDebug.Log("patched BasicBeamController.Start!");
    }

    /// <summary>Replace all construction of BeamBones with Rents</summary>
    [HarmonyPatch(typeof(BasicBeamController), nameof(BasicBeamController.SeparateBeam))]
    [HarmonyILManipulator]
    private static void SeparateBeamPatch(ILContext il)
    {
        _Enabled = GGVConfig.OPT_BEAMS;
        if (!_Enabled)
          return;
        ILCursor cursor = new ILCursor(il);
        if (!cursor.ReplaceCopyConstructor()) return;
        GGVDebug.Log("patched BasicBeamController.SeparateBeam!");
    }

    /// <summary>Replace all construction of BeamBones with Rents</summary>
    [HarmonyPatch(typeof(BasicBeamController), nameof(BasicBeamController.HandleBeamFrame))]
    [HarmonyILManipulator]
    private static void HandleBeamFramePatch(ILContext il)
    {
        _Enabled = GGVConfig.OPT_BEAMS;
        if (!_Enabled)
          return;
        ILCursor cursor = new ILCursor(il);
        if (!cursor.ReplaceIntConstructor()) return; //1324
        if (!cursor.ReplaceRemoveLast())     return; //1329
        if (!cursor.ReplaceIntConstructor()) return; //1394
        if (!cursor.ReplaceIntConstructor()) return; //1444
        if (!cursor.ReplaceRemoveFirst())    return; //1487
        if (!cursor.ReplaceRemoveLast())     return; //1491
        if (!cursor.ReplaceClear())          return; //1498
        if (!cursor.ReplaceVecConstructor()) return; //1505
        if (!cursor.ReplaceVecConstructor()) return; //1509
        if (!cursor.ReplaceRemoveLast())     return; //1823
        GGVDebug.Log("patched BasicBeamController.HandleBeamFrame!");
    }

    [HarmonyPatch(typeof(BasicBeamController), nameof(BasicBeamController.OnDestroy))]
    [HarmonyPrefix]
    static void BasicBeamControllerOnDestroyPatch(BasicBeamController __instance)
    {
        if (!_Enabled)
            return;
        if (__instance.m_bones != null)
            ReturnAll(__instance.m_bones);
    }
}
