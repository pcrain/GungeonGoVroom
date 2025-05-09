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
#endregion

global using Component = UnityEngine.Component;

namespace GGV;

public static class C // constants
{
    public static readonly bool DEBUG_BUILD = true; // set to false for release builds (must be readonly instead of const to avoid build warnings)

    public const string MOD_NAME     = "Gungeon Go Vroom";
    public const string MOD_INT_NAME = "GungeonGoVroom";
    public const string MOD_VERSION  = "1.0.0";
    public const string MOD_GUID     = "pretzel.etg.ggv";
    public const string MOD_PREFIX   = "ggv";

    public static readonly Color MOD_COLOR = new Color(0.4f, 0.4f, 0.7f);
}

[BepInPlugin(C.MOD_GUID, C.MOD_INT_NAME, C.MOD_VERSION)]
[BepInDependency(ETGModMainBehaviour.GUID, "1.9.2")]
[BepInDependency(Gunfiguration.C.MOD_GUID, "1.1.6")]
public class Initialisation : BaseUnityPlugin
{
    public static Initialisation Instance;
    public void Start() { ETGModMainBehaviour.WaitForGameManagerStart(GMStart); }
    public void GMStart(GameManager manager)
    {
        try
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Instance  = this;
            ConfigMenu.Init();
            Harmony harmony = new Harmony(C.MOD_GUID);
            harmony.PatchAll();
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

internal static class GGVDebug
{
    // Log with the console only in debug mode
    [System.Diagnostics.Conditional("DEBUG")]
    public static void Log(string text)
    {
        System.Console.WriteLine("[GGV]: " + text);
    }

    // Warn with the console only in debug mode
    [System.Diagnostics.Conditional("DEBUG")]
    public static void Warn(string text)
    {
        ETGModConsole.Log($"<color=#ffffaaff>{text}</color>");
    }
}

internal static class Dissect // reflection helper methods
{
    public static void DumpComponents(this GameObject g)
    {
        foreach (var c in g.GetComponents(typeof(object)))
            ETGModConsole.Log("  "+c.GetType().Name);
    }

    public static void DumpFieldsAndProperties<T>(T o)
    {
        Type type = typeof(T);
        foreach (var f in type.GetFields())
            Console.WriteLine(String.Format("field {0} = {1}", f.Name, f.GetValue(o)));
        foreach(PropertyDescriptor d in TypeDescriptor.GetProperties(o))
            Console.WriteLine(" prop {0} = {1}", d.Name, d.GetValue(o));
    }

    public static void CompareFieldsAndProperties<T>(T o1, T o2)
    {
        // Type type = o.GetType();
        Type type = typeof(T);
        foreach (var f in type.GetFields()) {
            try
            {
                if (f.GetValue(o1) == null)
                {
                    if (f.GetValue(o2) == null)
                        continue;
                }
                else if (f.GetValue(o2) != null && f.GetValue(o1).Equals(f.GetValue(o2)))
                    continue;
                Console.WriteLine(
                    String.Format("field {0} = {1} -> {2}", f.Name, f.GetValue(o1), f.GetValue(o2)));
            }
            catch (Exception)
            {
                Console.WriteLine(" prop {0} = {1} -> {2}", f.Name, "ERROR", "ERROR");
            }
        }
        foreach(PropertyDescriptor f in TypeDescriptor.GetProperties(o1))
        {
            try {
                if (f.GetValue(o1) == null)
                {
                    if (f.GetValue(o2) == null)
                        continue;
                }
                else if (f.GetValue(o2) != null && f.GetValue(o1).Equals(f.GetValue(o2)))
                    continue;
                Console.WriteLine(" prop {0} = {1} -> {2}", f.Name, f.GetValue(o1), f.GetValue(o2));
            }
            catch (Exception)
            {
                Console.WriteLine(" prop {0} = {1} -> {2}", f.Name, "ERROR", "ERROR");
            }
        }
        Console.WriteLine("");
    }

    public static void DumpILInstruction(this Instruction c)
    {
        try
        {
            ETGModConsole.Log($"  {c.ToStringSafe()}");
        }
        catch (Exception)
        {
            try
            {
                ILLabel label = null;
                if (label == null) c.MatchBr(out label);
                if (label == null) c.MatchBeq(out label);
                if (label == null) c.MatchBge(out label);
                if (label == null) c.MatchBgeUn(out label);
                if (label == null) c.MatchBgt(out label);
                if (label == null) c.MatchBgtUn(out label);
                if (label == null) c.MatchBle(out label);
                if (label == null) c.MatchBleUn(out label);
                if (label == null) c.MatchBlt(out label);
                if (label == null) c.MatchBltUn(out label);
                if (label == null) c.MatchBrfalse(out label);
                if (label == null) c.MatchBrtrue(out label);
                if (label == null) c.MatchBneUn(out label);
                if (label != null)
                    ETGModConsole.Log($"  IL_{c.Offset.ToString("x4")}: {c.OpCode.Name} IL_{label.Target.Offset.ToString("x4")}");
                else
                    ETGModConsole.Log($"[UNKNOWN INSTRUCTION]");
                    // ETGModConsole.Log($"  IL_{c.Offset.ToString("x4")}: {c.OpCode.Name} {c.Operand.ToStringSafe()}");
            }
            catch (Exception)
            {
                ETGModConsole.Log($"  <error>");
            }
        }
    }

    // Dump IL instructions for an IL Hook
    public static void DumpIL(this ILCursor cursor, string key)
    {
        foreach (Instruction c in cursor.Instrs)
            DumpILInstruction(c);
    }
}
