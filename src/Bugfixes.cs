namespace GGV;

internal static partial class Patches
{
    /// <summary>All 3 of Gungeon's list-shuffling implementations are flawed due to off by one errors, so we need to fix them. We can't hook generic methods directly, so focus on GenerationShuffle<int>(), which is the main issue for floor room generation</summary>
    [HarmonyPatch]
    private static class RoomShuffleOffByOneFix
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.FIX_SHUFFLE)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        private static IEnumerable<MethodBase> TargetMethods()
        {
            // refer to C# reflection documentation:
            MethodInfo genShuffle = typeof(BraveUtility).GetMethod("GenerationShuffle", BindingFlags.Static | BindingFlags.Public);
            yield return genShuffle.MakeGenericMethod(typeof(int));
            yield return genShuffle.MakeGenericMethod(typeof(IntVector2));
            //WARN: the tuple genericizer below seems to call PrototypeRoomExit version of the function, causing an indexoutofrange exception
            //      could also have just been an unlucky 1 in 4 billion RNG failure...see screenshot generationshuffle-error.png in _planning folder
            yield return genShuffle.MakeGenericMethod(typeof(Tuple<RuntimeRoomExitData, RuntimeRoomExitData>));
            yield return genShuffle.MakeGenericMethod(typeof(PrototypeRoomExit));

            MethodInfo standardShuffle = typeof(BraveUtility).GetMethod("Shuffle", BindingFlags.Static | BindingFlags.Public);
            yield return standardShuffle.MakeGenericMethod(typeof(int));
            yield return standardShuffle.MakeGenericMethod(typeof(IntVector2));
            yield return standardShuffle.MakeGenericMethod(typeof(AIActor));
            yield return standardShuffle.MakeGenericMethod(typeof(IPaydayItem));
        }

        [HarmonyILManipulator]
        private static void GenerationShuffleFixIL(ILContext il)
        {
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
            // GGVDebug.Log("Fixing Room Shuffling");
        }
    }

    /// <summary>Duct tape gun ids aren't serialized, so dropping them clears out the duct tape gun list and breaks save serialization</summary>
    [HarmonyPatch]
    private static class DuctTapeFix
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.FIX_DUCT_TAPE)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        [HarmonyPatch(typeof(Gun), nameof(Gun.CopyStateFrom))]
        [HarmonyPostfix]
        private static void DuctTapeSaveLoadPatch(Gun __instance, Gun other)
        {
            __instance.DuctTapeMergedGunIDs = other.DuctTapeMergedGunIDs;
        }
    }

    /// <summary>Quick restart doesn't call PreprocessRun(), so once-per-run rooms will never respawn until you return to the Breach</summary>
    [HarmonyPatch]
    private static class QuickRestartFixes
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.FIX_QUICK_RESTART)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

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
            cursor.CallPrivate(typeof(QuickRestartFixes), nameof(ForcePreprocessRunForQuickStart));
        }

        private static void ForcePreprocessRunForQuickStart(GameManager gm)
        {
            if (gm)
                gm.GlobalInjectionData.PreprocessRun();
        }
    }

    /// <summary>Fixes a vanilla bug with the background of final projectiles rendering above the foreground. Also fixes a vanilla bug with AmmoBurstVFX rendering below final clip projectiles.</summary>
    [HarmonyPatch]
    private static class AmmoUIFixes
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.FIX_AMMO_UI)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        [HarmonyPatch(typeof(GameUIAmmoController), nameof(GameUIAmmoController.UpdateAmmoUIForModule))]
        [HarmonyPostfix]
        private static void GameUIAmmoControllerUpdateAmmoUIForModulePatch(GameUIAmmoController __instance, ref dfTiledSprite currentAmmoFGSprite, ref dfTiledSprite currentAmmoBGSprite, List<dfTiledSprite> AddlModuleFGSprites, List<dfTiledSprite> AddlModuleBGSprites, dfSprite ModuleTopCap, dfSprite ModuleBottomCap, ProjectileModule module, Gun currentGun, ref GameUIAmmoType.AmmoType cachedAmmoTypeForModule, ref string cachedCustomAmmoTypeForModule, ref int cachedShotsInClip, bool didChangeGun, int numberRemaining)
        {
            if (AddlModuleBGSprites == null || AddlModuleBGSprites.Count < 1)
                return;
            if (AddlModuleFGSprites == null || AddlModuleFGSprites.Count < 1)
                return;
            AddlModuleFGSprites[0].ZOrder = AddlModuleBGSprites[0].ZOrder + 1;
            if (currentAmmoFGSprite != null)
                currentAmmoFGSprite.ZOrder = AddlModuleFGSprites[0].ZOrder + 1;
        }
    }

    /// <summary>Fixes a vanilla bug where hovering guns shoot from the wrong place if created while the player is facing left</summary>
    [HarmonyPatch]
    private static class OrbitalGunFixes
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.FIX_ORBITAL_GUN)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        [HarmonyPatch(typeof(HoveringGunController), nameof(HoveringGunController.Initialize))]
        [HarmonyPostfix]
        private static void HoveringGunDoesntShootProperlyWhenCreatedWhileFacingLeftPatch(HoveringGunController __instance)
        {
            if (!__instance.m_owner || !__instance.m_owner.sprite || !__instance.m_owner.sprite.FlipX)
                return;
            Transform t = __instance.m_shootPointTransform;
            t.localPosition = t.localPosition.WithY(-t.localPosition.y);
        }
    }


    /// <summary>Fix player two not getting Turbo Mode speed buffs in Coop</summary>
    [HarmonyPatch]
    private static class CoopTurboFixes
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.FIX_COOP_TURBO)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

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
            cursor.CallPrivate(typeof(CoopTurboFixes), nameof(RecalculateTurboStats));

            if (!cursor.TryGotoNext(MoveType.After,
              instr => instr.MatchLdfld<PlayerController>("m_turboRollSpeedModifier"),
              instr => instr.OpCode == OpCodes.Callvirt))  // can't match List<StatModified>::Add() for some reason
                return; // failed to find what we need

            // Recalculate stats after adjusting turbo roll speed modifier
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.CallPrivate(typeof(CoopTurboFixes), nameof(RecalculateTurboStats));
        }

        private static void RecalculateTurboStats(PlayerController player)
        {
            player.stats.RecalculateStats(player);
        }
    }

    /// <summary>Keeps projectile trails from disappearing if projectiles slow down too much</summary>
    [HarmonyPatch]
    private static class BulletTrailFixes
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.FIX_BULLET_TRAILS)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        [HarmonyPatch(typeof(TrailController), nameof(TrailController.Update))]
        [HarmonyILManipulator]
        private static void TrailControllerUpdateIL(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallvirt(typeof(LinkedList<TrailController.Bone>).GetMethod("get_Count"))))
                return;

            cursor.Emit(OpCodes.Ldarg_0); // TrailController
            cursor.CallPrivate(typeof(BulletTrailFixes), nameof(KeepTrailAliveWhenEmpty));
        }

        private static int KeepTrailAliveWhenEmpty(int oldCount, TrailController trail)
        {
            return trail.destroyOnEmpty ? oldCount : 999; // must be nonzero to avoid BrTrue statement
        }
    }

    /// <summary>Fixes ignoreDamageCaps not working on beams.</summary>
    [HarmonyPatch]
    private static class BeamDamageCapPatch
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.FIX_DAMAGE_CAPS)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        [HarmonyPatch(typeof(BasicBeamController), nameof(BasicBeamController.FrameUpdate))]
        [HarmonyILManipulator]
        private static void BasicBeamControllerFrameUpdateIL(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            for (int i = 0; i < 3; ++i) // move right before the 3rd ApplyDamage() call
                if (!cursor.TryGotoNext(i == 2 ? MoveType.Before : MoveType.After,
                  instr => instr.MatchCallvirt<HealthHaver>(nameof(HealthHaver.ApplyDamage))))
                    return;

            if (!cursor.TryGotoPrev(MoveType.After, instr => instr.MatchLdcI4(0))) // hardcoded false for ignoreDamageCaps
                return;

            cursor.Emit(OpCodes.Ldarg_0);
            cursor.CallPrivate(typeof(BeamDamageCapPatch), nameof(BeamShouldIgnoreDamageCaps));
        }

        private static bool BeamShouldIgnoreDamageCaps(bool origVal, BasicBeamController beam)
        {
            return origVal || (beam.projectile && beam.projectile.ignoreDamageCaps);
        }
    }

    /// <summary>Fixes Evolver reverting to its second form after dropping it, picking it back up, and killing 5 enemies.</summary>
    [HarmonyPatch]
    private static class EvolverDevolvePatch
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.FIX_EVOLVER)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        [HarmonyPatch(typeof(EvolverGunController), nameof(EvolverGunController.OnDestroy))]
        [HarmonyPrefix]
        private static bool EvolverGunControllerOnDestroyPatch(EvolverGunController __instance)
        {
            __instance.Disengage();
            return false; // everything else is redundant
        }
    }

    /// <summary>Fixes Magazine Rack causing the ammo display to drift to the right every time it's used.</summary>
    [HarmonyPatch]
    private static class AmmoDriftPatch
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.FIX_AMMO_DRIFT)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        [HarmonyPatch(typeof(GameUIAmmoController), nameof(GameUIAmmoController.UpdateUIGun))]
        [HarmonyILManipulator]
        private static void BasicBeamControllerFrameUpdateIL(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After,
              instr => instr.MatchLdfld<GameUIAmmoController>("m_cachedGun"),
              instr => instr.MatchCallvirt<Gun>("get_InfiniteAmmo")))
                return;

            cursor.Emit(OpCodes.Ldarg_0);
            cursor.CallPrivate(typeof(AmmoDriftPatch), nameof(WasReallyInfiniteAmmo));
        }

        private static bool WasReallyInfiniteAmmo(bool origVal, GameUIAmmoController self)
        {
            return origVal && self.m_cachedMaxAmmo == int.MaxValue;
        }
    }

    /// <summary>Fixes the game continuing to run in the background if you unpause and repause very quickly.</summary>
    [HarmonyPatch]
    private static class RepausePatch
    {
        private static bool Prepare(MethodBase original)
        {
          if (!GGVConfig.FIX_REPAUSE)
            return false;
          if (original == null)
            GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
          else
            GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
          return true;
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.DepixelateCR), MethodType.Enumerator)]
        [HarmonyILManipulator]
        private static void GameManagerDepixelateCRIL(ILContext il, MethodBase original)
        {
            ILCursor cursor = new ILCursor(il);
            Type ot = original.DeclaringType;

            if (!cursor.TryGotoNext(MoveType.After,
              instr => instr.MatchLdarg(0),
              instr => instr.MatchLdfld(ot.GetEnumeratorField("$this")),
              instr => instr.MatchCall<Component>("get_gameObject"),
              instr => instr.MatchCall(typeof(BraveTime), nameof(BraveTime.ClearMultiplier))))
              return;
            ILLabel skipTimeReset = cursor.MarkLabel();

            if (!cursor.TryGotoPrev(MoveType.Before,
              instr => instr.MatchLdarg(0),
              instr => instr.MatchLdfld(ot.GetEnumeratorField("$this")),
              instr => instr.MatchCall<Component>("get_gameObject"),
              instr => instr.MatchCall(typeof(BraveTime), nameof(BraveTime.ClearMultiplier))))
              return;

            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, ot.GetEnumeratorField("$this"));
            cursor.CallPrivate(typeof(RepausePatch), nameof(IsActuallyStillPaused));
            cursor.Emit(OpCodes.Brtrue, skipTimeReset);
        }

        private static bool IsActuallyStillPaused(GameManager instance) => instance.m_paused;
    }

}
