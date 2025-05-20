// comment out to suppress patch loading information
#define LOGPATCHES

namespace GGV;

using System.Diagnostics;

/// <summary>Check which functions call a specific method</summary>
#if DEBUG
// [HarmonyPatch] // uncomment to enable the patch in debug mode
internal static class CallTracker
{
  private static readonly Dictionary<string, int> _Calls = new();
  // Change the next line to patch whatever needs to be checked
  [HarmonyPatch(typeof(tk2dSprite), nameof(tk2dSprite.Awake))]
  [HarmonyPrefix]
  private static void WhoCalledIt()
  {
    GGVDebug.Log($"called from");
    StackTrace s = new StackTrace();
    string caller = string.Empty;
    for (int frame = 1; true; ++frame)
    {
      StackFrame f = s.GetFrame(frame);
      if (f == null)
        break;
      MethodBase m = f.GetMethod();
      string name = $"  -> {m.DeclaringType}::{m.Name}";
      caller += name;
      GGVDebug.Log(name);
    }
    if (!_Calls.TryGetValue(caller, out int val))
      _Calls[caller] = 0;
    ++_Calls[caller];
    GGVDebug.Log($"   ...{_Calls[caller]} times");
  }
}
#endif

internal static class GGVDebug
{
    // Log with the console only in debug mode
    [System.Diagnostics.Conditional("DEBUG")]
    internal static void Log(string text)
    {
        System.Console.WriteLine("[GGV]: " + text);
    }

    // Log patch information with the console only in debug mode
    [System.Diagnostics.Conditional("DEBUG")]
    internal static void LogPatch(string text)
    {
        #if LOGPATCHES
        System.Console.WriteLine("[GGV]: " + text);
        #endif
    }

    // Warn with the console only in debug mode
    [System.Diagnostics.Conditional("DEBUG")]
    internal static void Warn(string text)
    {
        ETGModConsole.Log($"<color=#ffffaaff>{text}</color>");
    }
}

internal static class Dissect // reflection helper methods
{
    internal static void DumpComponents(this GameObject g)
    {
        foreach (var c in g.GetComponents(typeof(object)))
            ETGModConsole.Log("  "+c.GetType().Name);
    }

    internal static void DumpFieldsAndProperties<T>(T o)
    {
        Type type = typeof(T);
        foreach (var f in type.GetFields())
            Console.WriteLine(String.Format("field {0} = {1}", f.Name, f.GetValue(o)));
        foreach(PropertyDescriptor d in TypeDescriptor.GetProperties(o))
            Console.WriteLine(" prop {0} = {1}", d.Name, d.GetValue(o));
    }

    internal static void CompareFieldsAndProperties<T>(T o1, T o2)
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

    internal static void DumpILInstruction(this Instruction c)
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
    internal static void DumpIL(this ILCursor cursor, string key)
    {
        foreach (Instruction c in cursor.Instrs)
            DumpILInstruction(c);
    }
}
