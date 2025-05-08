namespace GGV;

using Gunfiguration; // Make sure you're using the Gunfiguration API

public static class ConfigMenu
{
  internal static Gunfig _Gunfig = null;

  internal const string SAFE_OPT = "Safe Optimizations";
  internal const string SAFE_FIX = "Safe Bugfixes";
  internal const string AGGR_OPT = "Aggressive Optimizations";
  internal const string AGGR_FIX = "Aggressive Bugfixes";

  internal static void Init()
  {
    _Gunfig = Gunfig.Get(modName: C.MOD_NAME.WithColor(C.MOD_COLOR));

    Gunfig so = _Gunfig.AddSubMenu(SAFE_OPT);
    so.AddToggle("TEST 1");

    Gunfig sf = _Gunfig.AddSubMenu(SAFE_FIX);
    sf.AddToggle("TEST 2");

    Gunfig ao = _Gunfig.AddSubMenu(AGGR_OPT);
    ao.AddToggle("TEST 3");

    Gunfig af = _Gunfig.AddSubMenu(AGGR_FIX);
    af.AddToggle("TEST 4");

    Gunfig.OnAllModsLoaded += LateInit;
  }

  private static void LateInit()
  {

  }
}
