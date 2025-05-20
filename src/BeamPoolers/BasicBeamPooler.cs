namespace GGV;

using static BasicBeamController;

[HarmonyPatch]
internal static class BasicBeamPooler
{
    private static readonly LinkedList<BeamBone> _BonePool = new();
    private static int _TotalBones = 0;

    private static readonly Type _BoneListType = typeof(LinkedList<>).MakeGenericType(typeof(BeamBone));
    private static readonly MethodInfo _AddFirstBone = _BoneListType.GetMethod("AddFirst", new[]{typeof(BeamBone)});
    private static readonly MethodInfo _AddLastBone = _BoneListType.GetMethod("AddLast", new[]{typeof(BeamBone)});
    private static readonly MethodInfo _AddBeforeBone = _BoneListType.GetMethod("AddBefore", new[]{typeof(LinkedListNode<BeamBone>), typeof(BeamBone)});

    private static bool Prepare(MethodBase original)
    {
      if (!GGVConfig.OPT_BEAMS)
        return false;
      if (original == null)
        GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
      else
        GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
      return true;
    }

    // #if DEBUG
    // [HarmonyPatch(typeof(BeamBone), MethodType.Constructor, new[] {typeof(float), typeof(float), typeof(int)})]
    // [HarmonyPatch(typeof(BeamBone), MethodType.Constructor, new[] {typeof(float), typeof(Vector2), typeof(Vector2)})]
    // [HarmonyPatch(typeof(BeamBone), MethodType.Constructor, new[] {typeof(BeamBone)})]
    // [HarmonyPostfix]
    // private static void NewBoneCounter()
    // {
    //   GGVDebug.Log($"constructed {++_TotalBones} bones");
    // }
    // #endif

    #region Rentals
    /// <summary>Replaces all calls to int constructor</summary>
    private static LinkedListNode<BeamBone> RentInt(float posX, float rotationAngle, int subtileNum)
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

        return node;
    }

    /// <summary>Replaces all calls to int constructor</summary>
    private static void RentIntAndAddFirst(LinkedList<BeamBone> bones, float posX, float rotationAngle, int subtileNum)
    {
        bones.AddFirst(RentInt(posX, rotationAngle, subtileNum));
    }

    /// <summary>Replaces all calls to int constructor</summary>
    private static void RentIntAndAddLast(LinkedList<BeamBone> bones, float posX, float rotationAngle, int subtileNum)
    {
        bones.AddLast(RentInt(posX, rotationAngle, subtileNum));
    }

    /// <summary>Replaces all calls to Vector2 constructor</summary>
    private static LinkedListNode<BeamBone> RentVec(float posX, Vector2 position, Vector2 velocity)
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

        return node;
    }

    /// <summary>Replaces all calls to Vector2 constructor</summary>
    private static void RentVecAndAddLast(LinkedList<BeamBone> bones, float posX, Vector2 position, Vector2 velocity)
    {
        bones.AddLast(RentVec(posX, position, velocity));
    }

    /// <summary>Replaces all calls to BeamBone constructor</summary>
    private static LinkedListNode<BeamBone> RentCopy(BeamBone other)
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

        return node;
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
    #endregion

    #region Patches
    /// <summary>Replace all construction of BeamBones with Rents</summary>
    [HarmonyPatch(typeof(BasicBeamController), nameof(BasicBeamController.Start))]
    [HarmonyILManipulator]
    private static void StartPatch(ILContext il)
    {
        ILCursor cursor = new ILCursor(il);
        if (!cursor.ReplaceNextIntConstructorAndAddFirst()) return;
        if (!cursor.ReplaceNextIntConstructorAndAddLast()) return;
        // GGVDebug.Log("patched BasicBeamController.Start!");
    }

    /// <summary>Replace all construction of BeamBones with Rents</summary>
    [HarmonyPatch(typeof(BasicBeamController), nameof(BasicBeamController.SeparateBeam))]
    [HarmonyILManipulator]
    private static void SeparateBeamPatch(ILContext il)
    {
        ILCursor cursor = new ILCursor(il);

        if (cursor.ReplaceNextConstructionWithRental(il, nameof(BasicBeamPooler.RentCopy)) is not VariableDefinition node)
          return;

        if (!cursor.TryGotoNext(MoveType.Before,
          instr => instr.MatchLdloc(2),
          instr => instr.MatchCallvirt(_AddFirstBone),
          instr => instr.MatchPop()))
          return;

        cursor.Remove(); // remove store to local BeamBone since we need a LinkedListNode(BeamBone)
        cursor.Remove(); // remove call to AddFirst
        cursor.Remove(); // remove pop of linkedlistnode from the stack
        cursor.Emit(OpCodes.Ldloc, node); // load the node for our bone
        cursor.CallPrivate(typeof(BasicBeamPooler), nameof(BasicBeamPooler.AddFirstNode));

        // GGVDebug.Log("patched BasicBeamController.SeparateBeam!");
    }

    /// <summary>Replace all construction of BeamBones with Rents</summary>
    [HarmonyPatch(typeof(BasicBeamController), nameof(BasicBeamController.HandleBeamFrame))]
    [HarmonyILManipulator]
    private static void HandleBeamFramePatch(ILContext il)
    {
        ILCursor cursor = new ILCursor(il);
        if (!cursor.ReplaceNextIntConstructorAndAddFirst()) return; // line 1324
        if (!cursor.ReplaceNextRemoveLast())                return; // line 1329

        if (cursor.ReplaceNextConstructionWithRental(il, nameof(BasicBeamPooler.RentInt)) is not VariableDefinition node) // line 1394
          return;
        if (!cursor.TryGotoNext(MoveType.Before,
          instr => instr.MatchLdloc(23), // BeamBone
          instr => instr.MatchCallvirt(_AddBeforeBone)))
          return;
        cursor.Remove(); // remove load for local BeamBone since we need a LinkedListNode(BeamBone)
        cursor.Remove(); // remove call to AddBefore(BeamBone)
        cursor.Emit(OpCodes.Ldloc, node); // load the node for our bone
        cursor.CallPrivate(typeof(BasicBeamPooler), nameof(BasicBeamPooler.AddBeforeNode));
        cursor.Emit(OpCodes.Ldloc, node); // prepare for upcoming stloc(22)

        if (!cursor.ReplaceNextIntConstructorAndAddFirst()) return; // line 1444
        if (!cursor.ReplaceNextRemoveFirst())               return; // line 1487
        if (!cursor.ReplaceNextRemoveLast())                return; // line 1491
        if (!cursor.ReplaceNextClear())                     return; // line 1498
        if (!cursor.ReplaceVecConstructorAndAddLast())      return; // line 1505

        if (cursor.ReplaceNextConstructionWithRental(il, nameof(BasicBeamPooler.RentVec)) is not VariableDefinition node2) // line 1509
          return;
        if (!cursor.TryGotoNext(MoveType.Before,
          instr => instr.MatchLdloc(49), // BeamBone
          instr => instr.MatchCallvirt(_AddLastBone),
          instr => instr.MatchPop()))
          return;
        cursor.Remove(); // remove load for local BeamBone since we need a LinkedListNode(BeamBone)
        cursor.Remove(); // remove call to AddLast(BeamBone)
        cursor.Remove(); // remove pop of linkedlistnode from the stack
        cursor.Emit(OpCodes.Ldloc, node2); // load the node for our bone
        cursor.CallPrivate(typeof(BasicBeamPooler), nameof(BasicBeamPooler.AddLastNode));

        if (!cursor.ReplaceNextRemoveLast()) return; // line 1823
        // GGVDebug.Log("patched BasicBeamController.HandleBeamFrame!");
    }

    [HarmonyPatch(typeof(BasicBeamController), nameof(BasicBeamController.OnDestroy))]
    [HarmonyPrefix]
    static void BasicBeamControllerOnDestroyPatch(BasicBeamController __instance)
    {
        if (__instance.m_bones != null)
            ReturnAll(__instance.m_bones);
    }
    #endregion

    #region Helpers
    private static bool ReplaceNextConstructor(this ILCursor cursor, MethodInfo oldAdd, string newAdd)
    {
        if (!cursor.TryGotoNext(MoveType.Before,
          instr => instr.MatchNewobj<BeamBone>(),
          instr => instr.MatchCallvirt(oldAdd),
          instr => instr.MatchPop()))
            return false;

        // GGVDebug.Log($"replacing bone construction with rental (int)");
        cursor.Remove(); // remove constructor
        cursor.Remove(); // remove Add[First|Last]<BeamBone>
        cursor.Remove(); // remove Pop for ignored LinkedListNode(BeamBone)
        cursor.CallPrivate(typeof(BasicBeamPooler), newAdd);
        return true;
    }

    private static bool ReplaceNextMethod(this ILCursor cursor, string oldMethod, string newMethod)
    {
        if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCallvirt(_BoneListType.GetMethod(oldMethod))))
          return false;
        // GGVDebug.Log($"replacing RemoveFirst() with ReturnFirst()");
        cursor.Remove();
        cursor.CallPrivate(typeof(BasicBeamPooler), newMethod);
        return true;
    }

    private static VariableDefinition ReplaceNextConstructionWithRental(this ILCursor cursor, ILContext il, string rentalMethod)
    {
        // replace BeamBone constructor with LinkedListNode<BeamBone> rental
        if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchNewobj<BeamBone>()))
        {
          GGVDebug.Log("no good ):");
          return null;
        }
        cursor.Remove(); // remove new BeamBone()
        cursor.CallPrivate(typeof(BasicBeamPooler), rentalMethod); // rent a bone
        VariableDefinition node = il.DeclareLocal<LinkedListNode<BeamBone>>(); // declare a local for our LinkedListNode
        cursor.Emit(OpCodes.Stloc, node); // store the node in our local
        cursor.Emit(OpCodes.Ldloc, node); // load the node from our local
        cursor.CallPrivate(typeof(BasicBeamPooler), nameof(BasicBeamPooler.GetValue)); // get the BeamBone value of the node for the stack
        // next opcode is Stloc which will store the BeamBone locally
        return node; // return the VariableDefinition for our new node
    }

    private static BeamBone GetValue(LinkedListNode<BeamBone> node)
      => node.Value;
    private static void AddFirstNode(LinkedList<BeamBone> list, LinkedListNode<BeamBone> node)
      => list.AddFirst(node);
    private static void AddLastNode(LinkedList<BeamBone> list, LinkedListNode<BeamBone> node)
      => list.AddLast(node);
    private static void AddBeforeNode(LinkedList<BeamBone> list, LinkedListNode<BeamBone> prev, LinkedListNode<BeamBone> node)
      => list.AddBefore(prev, node);
    private static bool ReplaceNextRemoveFirst(this ILCursor cursor)
      => cursor.ReplaceNextMethod("RemoveFirst", nameof(BasicBeamPooler.ReturnFirst));
    private static bool ReplaceNextRemoveLast(this ILCursor cursor)
      => cursor.ReplaceNextMethod("RemoveLast", nameof(BasicBeamPooler.ReturnLast));
    private static bool ReplaceNextClear(this ILCursor cursor)
      => cursor.ReplaceNextMethod("Clear", nameof(BasicBeamPooler.ReturnAll));
    private static bool ReplaceNextIntConstructorAndAddFirst(this ILCursor cursor)
      => cursor.ReplaceNextConstructor(_AddFirstBone, nameof(RentIntAndAddFirst));
    private static bool ReplaceNextIntConstructorAndAddLast(this ILCursor cursor)
      => cursor.ReplaceNextConstructor(_AddLastBone, nameof(RentIntAndAddLast));
    private static bool ReplaceVecConstructorAndAddLast(this ILCursor cursor)
      => cursor.ReplaceNextConstructor(_AddLastBone, nameof(RentVecAndAddLast));

    #endregion
}
