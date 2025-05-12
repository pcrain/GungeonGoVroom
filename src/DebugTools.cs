namespace GGV;

using System.Diagnostics;

/// <summary>Check which functions call a specific method</summary>
#if DEBUG
// [HarmonyPatch] // uncomment to enable the patch in debug mode
internal static class CallTracker
{
  private static readonly Dictionary<string, int> _Calls = new();
  // Chacne the next line to patch whatever needs to be checked
  [HarmonyPatch(typeof(Vector2), nameof(Vector2.Distance))]
  [HarmonyPrefix]
  private static void WhoCalledIt()
  {
    StackTrace s = new StackTrace();
    string caller = string.Empty;
    for (int frame = 2; true; ++frame)
    {
      StackFrame f = s.GetFrame(frame);
      if (f == null)
        return;
      caller = f.GetMethod().Name;
      if (caller.Contains("MoveNext"))
        continue;
      break;
    }
    if (caller == "MoveNext")
      caller = s.GetFrame(3).GetMethod().Name;
    if (!_Calls.TryGetValue(caller, out int val))
      _Calls[caller] = 0;
    ++_Calls[caller];
    System.Console.WriteLine($"called from {caller} {_Calls[caller]} times");
  }
}
#endif
