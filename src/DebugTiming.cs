namespace GGV;

using System.Diagnostics;

#if DEBUG
/// <summary>Class for timing how long various methods take to run</summary>
// [HarmonyPatch]
internal static class DebugTiming
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        //NOTE: populate this with a yield return for each method to time
        // yield return AccessTools.Method(typeof(Pathfinding.Pathfinder), nameof(Pathfinding.Pathfinder.Initialize));
        // yield return AccessTools.Method(typeof(DeadlyDeadlyGoopManager), nameof(DeadlyDeadlyGoopManager.LateUpdate));
        // yield return AccessTools.Method(typeof(OcclusionLayer), nameof(OcclusionLayer.GenerateOcclusionTexture));
        // yield return AccessTools.Method(typeof(dfGUIManager), nameof(dfGUIManager.HitTestAll));
        // yield return AccessTools.Method(typeof(Patches), nameof(Patches.ShadowSystemLateUpdatePatch));
        yield break;
    }

    private static void Prefix(State __state, MethodBase __originalMethod)
    {
        if (!_States.TryGetValue(__originalMethod, out State s))
            s = _States[__originalMethod] = new();
        s.timer.Start();
    }

    private static void Postfix(State __state, MethodBase __originalMethod)
    {
        State s = _States[__originalMethod];
        s.timer.Stop();
        long ns = s.timer.ElapsedTicks * 100;
        s.timer.Reset();
        s.totalNs += ns;
        System.Console.WriteLine($"{ns,12:n0}ns {__originalMethod.DeclaringType.Name}.{__originalMethod.Name}, {s.totalNs,12:n0} total");
    }

    private class State
    {
        internal System.Diagnostics.Stopwatch timer = new();
        internal long totalNs                       = 0;
    }

    private static readonly Dictionary<MethodBase, State> _States = new();
}
#endif
