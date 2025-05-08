namespace GGV;

internal static partial class Patches
{
    // All 3 of Gungeon's list-shuffling implementations are flawed due to off by one errors, so we need to fix them
    //   We can't hook generic methods directly, so focus on GenerationShuffle<int>(), which is the main issue for floor room generation
    [HarmonyPatch]
    private static class RoomShuffleOffByOnePatch
    {
        private static MethodBase TargetMethod() {
            // refer to C# reflection documentation:
            return typeof(BraveUtility).GetMethod("GenerationShuffle", BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(typeof(int));
        }

        [HarmonyILManipulator]
        private static void GenerationShuffleFixIL(ILContext il)
        {
            if (!GGVConfig.FIX_ROOM_SHUFFLE)
                return;

            ILCursor cursor = new ILCursor(il);

            // GenerationRandomRange(0, num) should be GenerationRandomRange(0, num + 1)
            if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdloc(0)))
                return;
            cursor.Emit(OpCodes.Ldc_I4_1);
            cursor.Emit(OpCodes.Add);

            // num > 1 should be num >= 1
            ILLabel forLabel = null;
            if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchBgt(out forLabel)))
                return;
            cursor.Remove();
            cursor.Emit(OpCodes.Bge, forLabel);
            GGVDebug.Log("Fixing Room Shuffling");
        }
    }

    // Duct tape gun ids aren't serialized, so dropping them clears out the duct tape gun list and breaks save serialization
    [HarmonyPatch(typeof(Gun), nameof(Gun.CopyStateFrom))]
    [HarmonyPostfix]
    private static void DuctTapeSaveLoadPatch(Gun __instance, Gun other)
    {
        if (!GGVConfig.FIX_DUCT_TAPE)
            return;
        GGVDebug.Log("Fixing Duct Tape");
        __instance.DuctTapeMergedGunIDs = other.DuctTapeMergedGunIDs;
    }

    // Quick restart doesn't call PreprocessRun(), so once-per-run rooms will never respawn until you return to the Breach
    // GameManager.Instance.GlobalInjectionData.PreprocessRun();
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.QuickRestart))]
    [HarmonyILManipulator]
    private static void QuickRestartRoomCachePatch(ILContext il)
    {
        ILCursor cursor = new ILCursor(il);

        if (!cursor.TryGotoNext(MoveType.After,
            instr => instr.MatchLdstr("Quick Restarting..."),
            instr => instr.MatchCall<UnityEngine.Debug>("Log")))
            return;

        cursor.Emit(OpCodes.Ldarg_0); // load the game manager
        cursor.CallPrivate(typeof(Patches), nameof(ForcePreprocessRunForQuickStart));
    }

    private static void ForcePreprocessRunForQuickStart(GameManager gm)
    {
        if (!gm || !GGVConfig.FIX_QUICK_RESTART)
            return;
        GGVDebug.Log("Fixing Quick Restart");
        gm.GlobalInjectionData.PreprocessRun();
    }

    // Fixes a vanilla bug with the background of final projectiles rendering above the foreground
    // Also fixes a vanilla bug with AmmoBurstVFX rendering below final clip projectiles
    [HarmonyPatch(typeof(GameUIAmmoController), nameof(GameUIAmmoController.UpdateAmmoUIForModule))]
    [HarmonyPostfix]
    private static void GameUIAmmoControllerUpdateAmmoUIForModulePatch(GameUIAmmoController __instance, ref dfTiledSprite currentAmmoFGSprite, ref dfTiledSprite currentAmmoBGSprite, List<dfTiledSprite> AddlModuleFGSprites, List<dfTiledSprite> AddlModuleBGSprites, dfSprite ModuleTopCap, dfSprite ModuleBottomCap, ProjectileModule module, Gun currentGun, ref GameUIAmmoType.AmmoType cachedAmmoTypeForModule, ref string cachedCustomAmmoTypeForModule, ref int cachedShotsInClip, bool didChangeGun, int numberRemaining)
    {
        if (!GGVConfig.FIX_AMMO_UI)
            return;
        // GGVDebug.Log("Fixing Ammo UI"); // this gets spammed frequently so don't print it even in debug mode
        if (AddlModuleBGSprites == null || AddlModuleBGSprites.Count < 1)
            return;
        if (AddlModuleFGSprites == null || AddlModuleFGSprites.Count < 1)
            return;
        AddlModuleFGSprites[0].ZOrder = AddlModuleBGSprites[0].ZOrder + 1;
        if (currentAmmoFGSprite != null)
            currentAmmoFGSprite.ZOrder = AddlModuleFGSprites[0].ZOrder + 1;
    }

    // Fixes a vanilla bug where hovering guns shoot from the wrong place if created while the player is facing left
    [HarmonyPatch(typeof(HoveringGunController), nameof(HoveringGunController.Initialize))]
    [HarmonyPostfix]
    private static void HoveringGunDoesntShootProperlyWhenCreatedWhileFacingLeftPatch(HoveringGunController __instance)
    {
        if (!GGVConfig.FIX_ORBITAL_GUN)
            return;
        GGVDebug.Log("Fixing Hovering Gun");
        if (!__instance.m_owner || !__instance.m_owner.sprite || !__instance.m_owner.sprite.FlipX)
            return;
        Transform t = __instance.m_shootPointTransform;
        t.localPosition = t.localPosition.WithY(-t.localPosition.y);
    }

    // Fix player two not getting Turbo Mode speed buffs in Coop
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.UpdateTurboModeStats))]
    [HarmonyILManipulator]
    private static void CoopTurboModeFixHookIL(ILContext il)
    {
        ILCursor cursor = new ILCursor(il);

        if (!cursor.TryGotoNext(MoveType.After,
          instr => instr.MatchLdfld<PlayerController>("m_turboSpeedModifier"),
          instr => instr.OpCode == OpCodes.Callvirt))  // can't match List<StatModified>::Add() for some reason
            return; // failed to find what we need

        // Recalculate stats after adjusting turbo speed modifier
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.CallPrivate(typeof(Patches), nameof(RecalculateTurboStats));

        if (!cursor.TryGotoNext(MoveType.After,
          instr => instr.MatchLdfld<PlayerController>("m_turboRollSpeedModifier"),
          instr => instr.OpCode == OpCodes.Callvirt))  // can't match List<StatModified>::Add() for some reason
            return; // failed to find what we need

        // Recalculate stats after adjusting turbo roll speed modifier
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.CallPrivate(typeof(Patches), nameof(RecalculateTurboStats));
    }

    private static void RecalculateTurboStats(PlayerController player)
    {
        player.stats.RecalculateStats(player);
    }
}
