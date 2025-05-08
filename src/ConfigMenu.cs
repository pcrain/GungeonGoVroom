namespace GGV;

using Gunfiguration;

internal static class GGVConfig
{
  internal static bool FIX_DUCT_TAPE     = true;
  internal static bool FIX_QUICK_RESTART = true;
  internal static bool FIX_ROOM_SHUFFLE  = true;
  internal static bool FIX_AMMO_UI       = true;
  internal static bool FIX_ORBITAL_GUN   = true;
  internal static bool FIX_COOP_TURBO    = true;
  internal static bool FIX_BULLET_TRAILS = true;
  internal static bool OPT_PROJ_STATUS   = true;
  internal static bool OPT_BEAMS         = true;
  internal static bool OPT_POINTCAST     = true;
  internal static bool OPT_PIT_VFX       = true;

  internal static void Update()
  {
    FIX_DUCT_TAPE     = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.DUCT_TAPE);
    FIX_QUICK_RESTART = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.QUICK_RESTART);
    FIX_ROOM_SHUFFLE  = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.ROOM_SHUFFLE);
    FIX_AMMO_UI       = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.AMMO_UI);
    FIX_ORBITAL_GUN   = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.ORBITAL_GUN);
    FIX_COOP_TURBO    = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.COOP_TURBO);
    FIX_BULLET_TRAILS = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.BULLET_TRAILS);
    OPT_PROJ_STATUS   = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.PROJ_STATUS);
    OPT_BEAMS         = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.BEAMS);
    OPT_POINTCAST     = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.POINTCAST);
    OPT_PIT_VFX       = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.PIT_VFX);

    if (C.DEBUG_BUILD)
    {
      ETGModConsole.Log($"Updated GGV with the following options:");
      ETGModConsole.Log($"      FIX_DUCT_TAPE = {FIX_DUCT_TAPE}");
      ETGModConsole.Log($"  FIX_QUICK_RESTART = {FIX_QUICK_RESTART}");
      ETGModConsole.Log($"   FIX_ROOM_SHUFFLE = {FIX_ROOM_SHUFFLE}");
      ETGModConsole.Log($"        FIX_AMMO_UI = {FIX_AMMO_UI}");
      ETGModConsole.Log($"    FIX_ORBITAL_GUN = {FIX_ORBITAL_GUN}");
      ETGModConsole.Log($"     FIX_COOP_TURBO = {FIX_COOP_TURBO}");
      ETGModConsole.Log($"  FIX_BULLET_TRAILS = {FIX_BULLET_TRAILS}");
      ETGModConsole.Log($"    OPT_PROJ_STATUS = {OPT_PROJ_STATUS}");
      ETGModConsole.Log($"          OPT_BEAMS = {OPT_BEAMS}");
      ETGModConsole.Log($"      OPT_POINTCAST = {OPT_POINTCAST}");
      ETGModConsole.Log($"        OPT_PIT_VFX = {OPT_PIT_VFX}");
    }
  }

  // for callback purposes
  internal static void Update(string _unused1, string _unused2) => Update();
}

public static class ConfigMenu
{
  internal static Gunfig _Gunfig = null;

  internal const string BUG_FIX       = "Bugfixes";
  internal const string DUCT_TAPE     = "Duct Tape Fix";
  internal const string QUICK_RESTART = "Quicksave Fix";
  internal const string ROOM_SHUFFLE  = "Room Shuffle Fix";
  internal const string AMMO_UI       = "Ammo UI Fix";
  internal const string ORBITAL_GUN   = "Orbital Gun Fix";
  internal const string COOP_TURBO    = "Co-op Turbo Mode Fix";
  internal const string BULLET_TRAILS = "Bullet Trail Fix";

  internal const string SAFE_OPT      = "Safe Optimizations";
  internal const string PROJ_STATUS   = "Optimize Projectile Prefabs";

  internal const string AGGR_OPT      = "Aggressive Optimizations";
  internal const string BEAMS         = "Optimize Beams";
  internal const string POINTCAST     = "Optimize Pointcast";
  internal const string PIT_VFX       = "Optimize Pit VFX";

  internal static void Init()
  {
    _Gunfig = Gunfig.Get(modName: C.MOD_NAME.WithColor(C.MOD_COLOR));

    Gunfig sf = _Gunfig.AddSubMenu(BUG_FIX);
    sf.FancyToggle(DUCT_TAPE, "Fixes duct-taped guns sometimes breaking\nwhen using the elevator save button.");
    sf.FancyToggle(QUICK_RESTART, "Fixes once-per-run rooms not properly\nresetting with Quick Restart, preventing them\nfrom respawning until visiting the Breach.");
    sf.FancyToggle(ROOM_SHUFFLE, "Fixes off-by-one error in room randomization,\nmaking certain rooms always / never spawn in\ncertain unintended situations.");
    sf.FancyToggle(AMMO_UI, "Fixes a rendering issue with final projectiles\nin the ammo indicator causing them to render\nabove UI elements they shouldn't.");
    sf.FancyToggle(ORBITAL_GUN, "Fixes orbital guns visually firing from\nthe wrong location if created while the player\nis facing left.");
    sf.FancyToggle(COOP_TURBO, "Fixes co-op partner in turbo mode not\ngetting turbo mode speed buffs until\ntheir stats have changed at least once.");
    sf.FancyToggle(BULLET_TRAILS, "Fixes the trails of projectiles\ndisappearing if they travel too slowly\n(e.g., during timeslow effects).");

    Gunfig so = _Gunfig.AddSubMenu(SAFE_OPT);
    so.FancyToggle(PROJ_STATUS, "Removes prefab effect data (e.g., poison) from\nprojectiles that never apply those effects.\nSaves a small amount of RAM.");

    Gunfig ao = _Gunfig.AddSubMenu(AGGR_OPT);
    ao.FancyToggleOff(BEAMS, "Pools beam bones to reduce lag spikes.\nTakes effect on game restart.\nSaves a modest amount of RAM and CPU.");
    ao.FancyToggleOff(POINTCAST, "Speeds up pointcast physics calculations by\nusing statics instead of delegates.\nSaves a modest amount of CPU.");
    ao.FancyToggleOff(PIT_VFX, "Speeds up pit VFX calculations by skipping\nseveral redundant tile checks.\nSaves a small amount of CPU.");

    GGVConfig.Update();
    Gunfig.OnAllModsLoaded += LateInit;
  }

  private static readonly List<string> DefaultEnabled = ["Enabled", "Disabled"];
  private static readonly List<string> DefaultDisabled = ["Disabled", "Enabled"];
  private static void FancyToggle(this Gunfig gunfig, string toggleName, string toggleDesc)
  {
    string info = toggleDesc.Green();
    gunfig.AddScrollBox(key: toggleName, options: DefaultEnabled, info: [info, info], callback: GGVConfig.Update);
  }
  private static void FancyToggleOff(this Gunfig gunfig, string toggleName, string toggleDesc)
  {
    string info = toggleDesc.Green();
    gunfig.AddScrollBox(key: toggleName, options: DefaultDisabled, info: [info, info], callback: GGVConfig.Update);
  }

  private static void LateInit()
  {
  }
}
