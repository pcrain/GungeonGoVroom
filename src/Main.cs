/* TODO: potential optimizations
  - dfControl.PerformLayout calls GameUiRoot.Instance at startup when HasInstance will suffice, wasting a lot of time
  - DefineEpicenter can be heavily optimized
  - (mtgapi) AddMissingReplacements is called way too aggressively
*/

#region Global Usings
    global using System;
    global using System.Collections;
    global using System.Collections.Generic;
    global using System.Linq;
    global using System.Text;
    global using System.Text.RegularExpressions;
    global using System.Reflection;
    global using System.Runtime;
    global using System.Collections.ObjectModel;
    global using System.IO;
    global using System.Globalization; // CultureInfo
    global using System.ComponentModel;  // Debug stuff

    global using BepInEx;
    global using UnityEngine;
    global using UnityEngine.UI;
    global using UnityEngine.Events; // UnityEventBase
    global using MonoMod.RuntimeDetour;
    global using MonoMod.Utils;
    global using MonoMod.Cil;
    global using Mono.Cecil.Cil; //Instruction (for IL)
    global using HarmonyLib;

    global using Dungeonator;
    global using Gunfiguration;

    global using Component = UnityEngine.Component;
#endregion

namespace GGV;

public static class C // constants
{
    public static readonly bool DEBUG_BUILD = true; // set to false for release builds (must be readonly instead of const to avoid build warnings)

    public const string MOD_NAME     = "Gungeon Go Vroom";
    public const string MOD_INT_NAME = "GungeonGoVroom";
    public const string MOD_VERSION  = "1.1.0";
    public const string MOD_GUID     = "pretzel.etg.ggv";
    public const string MOD_PREFIX   = "ggv";

    public static readonly Color MOD_COLOR = new Color(0.4f, 0.4f, 0.7f);
}

[BepInPlugin(C.MOD_GUID, C.MOD_INT_NAME, C.MOD_VERSION)]
[BepInDependency(ETGModMainBehaviour.GUID, "1.9.2")]
[BepInDependency(Gunfiguration.C.MOD_GUID, "1.1.7")]
public class Initialisation : BaseUnityPlugin
{
    private void Awake()
    {
        try
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            ConfigMenu.Init();
            new Harmony(C.MOD_GUID).PatchAll();
            watch.Stop();
            ETGModConsole.Log($"Initialized <color=#{ColorUtility.ToHtmlStringRGB(C.MOD_COLOR).ToLower()}>{C.MOD_NAME} v{C.MOD_VERSION}</color> in "+(watch.ElapsedMilliseconds/1000.0f)+" seconds");
        }
        catch (Exception e)
        {
            ETGModConsole.Log(e.Message);
            ETGModConsole.Log(e.StackTrace);
        }
    }
}

// stub to make sure Harmony picks up all our patches
[HarmonyPatch]
internal static partial class Patches {}
