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
}
