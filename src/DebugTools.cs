namespace GGV;

using System.Diagnostics;

/// <summary>Check which functions call a specific method</summary>
#if DEBUG
// [HarmonyPatch] // uncomment to enable the patch in debug mode
internal static class CallTracker
{
  private static readonly Dictionary<string, int> _Calls = new();
  // Chacne the next line to patch whatever needs to be checked
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
