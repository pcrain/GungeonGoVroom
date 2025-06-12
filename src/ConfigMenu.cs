namespace GGV;

using Gunfiguration;

internal static class GGVConfig
{
  // Bugfixes
  internal static bool FIX_DUCT_TAPE     = true;
  internal static bool FIX_QUICK_RESTART = true;
  internal static bool FIX_SHUFFLE       = true;
  internal static bool FIX_AMMO_UI       = true;
  internal static bool FIX_ORBITAL_GUN   = true;
  internal static bool FIX_COOP_TURBO    = true;
  internal static bool FIX_BULLET_TRAILS = true;
  internal static bool FIX_DAMAGE_CAPS   = true;
  internal static bool FIX_EVOLVER       = true;
  internal static bool FIX_AMMO_DRIFT    = true;
  internal static bool FIX_REPAUSE       = true;

  // Safe Optimizations
  internal static int  PREALLOCATE_HEAP  = 0;
  internal static bool OPT_PROJ_STATUS   = true;
  internal static bool OPT_GUI_EVENTS    = true;
  internal static bool OPT_NUMBERS       = true;
  internal static bool OPT_FLOOD_FILL    = true;
  internal static bool OPT_TRAILS        = true;
  internal static bool OPT_LIGHT_CULL    = true;
  internal static bool OPT_BEAMS         = true;
  internal static bool OPT_PATH_RECALC   = true;
  internal static bool OPT_CHUNK_CHECKS  = true;
  internal static bool OPT_VIS_CHECKS    = true;
  internal static bool OPT_OCCLUSION     = true;
  internal static bool OPT_AMMO_DISPLAY  = true;
  internal static bool OPT_PHYSICS_LEAK  = true;
  internal static bool OPT_PIXEL_MOVE    = true;
  internal static bool OPT_PIXEL_ROTATE  = true;
  internal static bool OPT_PAUSE         = true;

  // Aggressive Optimizations
  internal static bool OPT_MATH          = true;
  internal static bool OPT_CHUNKBUILD    = true;
  internal static bool OPT_LINEAR_CAST   = true;
  internal static bool OPT_POINTCAST     = true;
  internal static bool OPT_PIT_VFX       = true;
  internal static bool OPT_ITEM_LOOKUPS  = true;
  internal static bool OPT_DUNGEON_DIMS  = true;
  internal static bool OPT_DEPTH_CHECKS  = true;
  internal static bool OPT_GOOP          = true;

  // Experimental Optimizations
  internal static bool OPT_MOUSE_EVENTS  = true;
  internal static bool OPT_TITLE_SCREEN  = true;

  internal static void Update()
  {
    PREALLOCATE_HEAP = 0;
    string heapConfig = ConfigMenu._Gunfig.Value(ConfigMenu.PREALLOCATE);
    if (heapConfig != "Default")
    {
      System.Console.WriteLine($"parsing heapConfig={heapConfig}");
      PREALLOCATE_HEAP = Int32.Parse(heapConfig.Split('G')[0]);
    }

    FIX_DUCT_TAPE     = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.DUCT_TAPE);
    FIX_QUICK_RESTART = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.QUICK_RESTART);
    FIX_SHUFFLE       = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.SHUFFLE);
    FIX_AMMO_UI       = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.AMMO_UI);
    FIX_ORBITAL_GUN   = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.ORBITAL_GUN);
    FIX_COOP_TURBO    = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.COOP_TURBO);
    FIX_BULLET_TRAILS = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.BULLET_TRAILS);
    FIX_DAMAGE_CAPS   = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.DAMAGE_CAPS);
    FIX_EVOLVER       = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.EVOLVER);
    FIX_AMMO_DRIFT    = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.AMMO_DRIFT);
    FIX_REPAUSE       = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.REPAUSE);

    OPT_VIS_CHECKS    = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.VIS_CHECKS);
    OPT_OCCLUSION     = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.OCCLUSION);
    OPT_AMMO_DISPLAY  = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.AMMO_DISPLAY);
    OPT_PAUSE         = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.PAUSE);
    OPT_LIGHT_CULL    = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.LIGHT_CULL);
    OPT_BEAMS         = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.BEAMS);
    OPT_PATH_RECALC   = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.PATH_RECALC);
    OPT_GUI_EVENTS    = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.GUI_EVENTS);
    OPT_NUMBERS       = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.NUMBERS);
    OPT_FLOOD_FILL    = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.FLOOD_FILL);
    OPT_TRAILS        = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.TRAILS);
    OPT_PROJ_STATUS   = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.PROJ_STATUS);
    OPT_CHUNK_CHECKS  = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.CHUNK_CHECKS);
    OPT_PHYSICS_LEAK  = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.PHYSICS_LEAK);
    OPT_PIXEL_MOVE    = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.PIXEL_MOVE);
    OPT_PIXEL_ROTATE  = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.PIXEL_ROTATE);

    OPT_MATH          = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.MATH);
    OPT_CHUNKBUILD    = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.CHUNKBUILD);
    OPT_LINEAR_CAST   = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.LINEAR_CAST);
    OPT_POINTCAST     = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.POINTCAST);
    OPT_PIT_VFX       = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.PIT_VFX);
    OPT_ITEM_LOOKUPS  = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.ITEM_LOOKUPS);
    OPT_DUNGEON_DIMS  = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.DUNGEON_DIMS);
    OPT_DEPTH_CHECKS  = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.DEPTH_CHECKS);
    OPT_GOOP          = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.GOOP);

    OPT_MOUSE_EVENTS  = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.MOUSE_EVENTS);
    OPT_TITLE_SCREEN  = "Enabled" == ConfigMenu._Gunfig.Value(ConfigMenu.TITLE_SCREEN);

    WriteLine($"PREALLOCATE_HEAP         = {PREALLOCATE_HEAP} GB");
    WriteLine($"FIX_DUCT_TAPE            = {FIX_DUCT_TAPE}");
    WriteLine($"FIX_QUICK_RESTART        = {FIX_QUICK_RESTART}");
    WriteLine($"FIX_ROOM_SHUFFLE         = {FIX_SHUFFLE}");
    WriteLine($"FIX_AMMO_UI              = {FIX_AMMO_UI}");
    WriteLine($"FIX_ORBITAL_GUN          = {FIX_ORBITAL_GUN}");
    WriteLine($"FIX_COOP_TURBO           = {FIX_COOP_TURBO}");
    WriteLine($"FIX_BULLET_TRAILS        = {FIX_BULLET_TRAILS}");
    WriteLine($"FIX_DAMAGE_CAPS          = {FIX_DAMAGE_CAPS}");
    WriteLine($"FIX_EVOLVER              = {FIX_EVOLVER}");
    WriteLine($"FIX_AMMO_DRIFT           = {FIX_AMMO_DRIFT}");
    WriteLine($"FIX_REPAUSE              = {FIX_REPAUSE}");

    WriteLine($"OPT_OCCLUSION            = {OPT_OCCLUSION}");
    WriteLine($"OPT_AMMO_DISPLAY         = {OPT_AMMO_DISPLAY}");
    WriteLine($"OPT_PAUSE                = {OPT_PAUSE}");
    WriteLine($"OPT_VIS_CHECKS           = {OPT_VIS_CHECKS}");
    WriteLine($"OPT_LIGHT_CULL           = {OPT_LIGHT_CULL}");
    WriteLine($"OPT_BEAMS                = {OPT_BEAMS}");
    WriteLine($"OPT_PATH_RECALC          = {OPT_PATH_RECALC}");
    WriteLine($"OPT_GUI_EVENTS           = {OPT_GUI_EVENTS}");
    WriteLine($"OPT_NUMBERS              = {OPT_NUMBERS}");
    WriteLine($"OPT_FLOOD_FILL           = {OPT_FLOOD_FILL}");
    WriteLine($"OPT_TRAILS               = {OPT_TRAILS}");
    WriteLine($"OPT_PROJ_STATUS          = {OPT_PROJ_STATUS}");
    WriteLine($"OPT_CHUNK_CHECKS         = {OPT_CHUNK_CHECKS}");
    WriteLine($"OPT_PHYSICS_LEAK         = {OPT_PHYSICS_LEAK}");
    WriteLine($"OPT_PIXEL_MOVE           = {OPT_PIXEL_MOVE}");
    WriteLine($"OPT_PIXEL_ROTATE         = {OPT_PIXEL_ROTATE}");

    WriteLine($"OPT_MATH                 = {OPT_MATH}");
    WriteLine($"OPT_CHUNKBUILD           = {OPT_CHUNKBUILD}");
    WriteLine($"OPT_LINEAR_CAST          = {OPT_LINEAR_CAST}");
    WriteLine($"OPT_POINTCAST            = {OPT_POINTCAST}");
    WriteLine($"OPT_PIT_VFX              = {OPT_PIT_VFX}");
    WriteLine($"OPT_ITEM_LOOKUPS         = {OPT_ITEM_LOOKUPS}");
    WriteLine($"OPT_DUNGEON_DIMS         = {OPT_DUNGEON_DIMS}");
    WriteLine($"OPT_DEPTH_CHECKS         = {OPT_DEPTH_CHECKS}");
    WriteLine($"OPT_GOOP                 = {OPT_GOOP}");

    WriteLine($"OPT_MOUSE_EVENTS         = {OPT_MOUSE_EVENTS}");
    WriteLine($"OPT_TITLE_SCREEN         = {OPT_TITLE_SCREEN}");
  }

  private static void WriteLine(string s)
  {
    #if DEBUG
    System.Console.WriteLine(s);
    #endif
    UnityEngine.Debug.Log("[GGV] " + s);
  }

  // for callback purposes
  internal static void Update(string _unused1, string _unused2) => Update();
}

internal static class ConfigMenu
{
  internal static Gunfig _Gunfig = null;

  internal const string BUG_FIX       = "Bugfixes";
  internal const string DUCT_TAPE     = "Duct Tape Fix";
  internal const string QUICK_RESTART = "Quick Restart Fix";
  internal const string SHUFFLE       = "Shuffle Fix";
  internal const string AMMO_UI       = "Ammo UI Fix";
  internal const string ORBITAL_GUN   = "Orbital Gun Fix";
  internal const string COOP_TURBO    = "Co-op Turbo Mode Fix";
  internal const string BULLET_TRAILS = "Bullet Trail Fix";
  internal const string DAMAGE_CAPS   = "Beam Damage Cap Fix";
  internal const string EVOLVER       = "Evolver Devolve Fix";
  internal const string AMMO_DRIFT    = "Ammo Drift Fix";
  internal const string REPAUSE       = "Unpause / Repause Fix";

  internal const string SAFE_OPT      = "Safe Optimizations";
  internal const string PREALLOCATE   = "Preallocate Heap Memory";
  internal const string OCCLUSION     = "Optimize Occlusion";
  internal const string AMMO_DISPLAY  = "Optimize Ammo Display";
  internal const string PAUSE         = "Optimize Pause Menu";
  internal const string VIS_CHECKS    = "Optimize Visibility Checks";
  internal const string LIGHT_CULL    = "Optimize Light Culling";
  internal const string BEAMS         = "Optimize Beams";
  internal const string PATH_RECALC   = "Optimize Path Recalculations";
  internal const string GUI_EVENTS    = "Optimize GUI Events";
  internal const string NUMBERS       = "Optimize Numerical Strings";
  internal const string FLOOD_FILL    = "Optimize Flood Filling";
  internal const string TRAILS        = "Optimize Bullet Trails";
  internal const string PIXEL_ROTATE  = "Optimize Pixel Rotation";
  internal const string PROJ_STATUS   = "Optimize Projectile Prefabs";
  internal const string CHUNK_CHECKS  = "Optimize Chunk Checks";
  internal const string PHYSICS_LEAK  = "Optimize Linear Cast Pool";
  internal const string PIXEL_MOVE    = "Optimize Pixel Movement Gen";

  internal const string AGGR_OPT      = "Aggressive Optimizations";
  internal const string MATH          = "Optimize Math";
  internal const string CHUNKBUILD    = "Optimize Chunk Building";
  internal const string LINEAR_CAST   = "Optimize Linear Cast";
  internal const string POINTCAST     = "Optimize Pointcast";
  internal const string PIT_VFX       = "Optimize Pit VFX";
  internal const string ITEM_LOOKUPS  = "Optimize Item Lookups";
  internal const string DUNGEON_DIMS  = "Optimize Dungeon Size Checks";
  internal const string DEPTH_CHECKS  = "Optimize Sprite Depth Checks";
  internal const string GOOP          = "Optimize Goop Updates";

  internal const string EXPR_OPT      = "Experimental Optimizations";
  internal const string MOUSE_EVENTS  = "Optimize GUI Mouse Events";
  internal const string TITLE_SCREEN  = "Optimize Title Screen";

  internal static void Init()
  {
    _Gunfig = Gunfig.Get(modName: C.MOD_NAME.WithColor(C.MOD_COLOR));
    _Gunfig.AddLabel("All Changes are Applied on Next Restart".Magenta());

    Gunfig sf = _Gunfig.AddSubMenu(BUG_FIX);
    sf.FancyToggle(DUCT_TAPE, "Fixes duct-taped guns sometimes breaking\nwhen using the elevator save button.");
    sf.FancyToggle(QUICK_RESTART, "Fixes once-per-run rooms not properly\nresetting with Quick Restart, preventing them\nfrom respawning until visiting the Breach.");
    sf.FancyToggle(SHUFFLE, "Fixes an off-by-one error in shuffling algorithms,\nmaking rooms always / never spawn in\nunintended situations, among other issues.");
    sf.FancyToggle(AMMO_UI, "Fixes a rendering issue with final projectiles\nin the ammo indicator causing them to render\nabove UI elements they shouldn't.");
    sf.FancyToggle(ORBITAL_GUN, "Fixes orbital guns visually firing from\nthe wrong location if created while the player\nis facing left.");
    sf.FancyToggle(COOP_TURBO, "Fixes co-op partner in turbo mode not\ngetting turbo mode speed buffs until\ntheir stats have changed at least once.");
    sf.FancyToggle(BULLET_TRAILS, "Fixes the trails of projectiles\ndisappearing if they travel too slowly\n(e.g., during timeslow effects).");
    sf.FancyToggle(DAMAGE_CAPS, "Fixes beams not ignoring boss damage caps\neven when set to do so. (No such\nbeam exists in vanilla, mostly for modded use).");
    sf.FancyToggle(EVOLVER, "Fixes Evolver devolving to its 2nd form\nafter dropping it, picking it back up,\nand killing 5 enemies to level it up.");
    sf.FancyToggle(AMMO_DRIFT, "Fixes ammo display drifting to the right when\na gun temporarily gets infinite ammo\n(e.g., from Magazine Rack).");
    sf.FancyToggle(REPAUSE, "Fixes game continuing to run if you\nunpause and quickly repause during menu\nfading animation.");

    Gunfig so = _Gunfig.AddSubMenu(SAFE_OPT);
    so.FancyMemList(PREALLOCATE, "Preallocates RAM to avoid OS requests later.\nDefault uses Gungeon's default of about 200MB.\nHigher values result in fewer lag spikes.");
    so.FancyToggle(OCCLUSION, "Speeds up occlusion calculations by\nusing optimized algorithms and caching.\nSaves a large amount of CPU.");
    so.FancyToggle(AMMO_DISPLAY, "Speeds up ammo display updates by\ncaching render data.\nSaves a large amount of RAM.");
    so.FancyToggle(PAUSE, "Prevents a lot of unnecessary rendering\nwhile the game is paused.\nSaves a large amount of CPU while paused.");
    so.FancyToggle(BEAMS, "Pools beam bones to reduce memory usage and\noptimizes beam mesh rebuilding logic.\nSaves a significant amount of RAM and CPU.");
    so.FancyToggle(LIGHT_CULL, "Uses optimized inlined logic for\ndetermining whether lights should be culled.\nSaves a significant amount of CPU.");
    so.FancyToggle(PATH_RECALC, "Optimizes clearance computations used\nfor enemy pathing logic.\nSaves modest amount of CPU.");
    so.FancyToggle(GUI_EVENTS, "Caches results of expensive lookups\nfor finding GUI event handlers.\nSaves a modest amount of RAM.");
    so.FancyToggle(TRAILS, "Pools bullet trail particles and vertex\ndata to reduce memory usage.\nSaves a modest amount of RAM.");
    so.FancyToggle(PIXEL_ROTATE, "Optimizes pixel movement rotation\nused for pixel-perfect collisions.\nSaves a modest amount of RAM.");
    so.FancyToggle(NUMBERS, "Caches strings for small numbers\nused frequently by SGUI's labels.\nSaves significant RAM while any console is open.");
    so.FancyToggle(VIS_CHECKS, "Speeds up sprite visibility checks\nby using inline arithmetic where possible.\nSaves a small amount of CPU.");
    so.FancyToggle(FLOOD_FILL, "Uses an optimized flood fill algorithm\nfor floor post-processing.\nSaves a small amount of CPU and RAM.");
    so.FancyToggle(PROJ_STATUS, "Removes prefab effect data (e.g., poison) from\nprojectiles that never apply those effects.\nSaves a small amount of RAM.");
    so.FancyToggle(CHUNK_CHECKS, "Optimize checks for whether sprite chunks\nare relevant to gameplay.\nSaves a small amount of CPU.");
    so.FancyToggle(PHYSICS_LEAK, "Fixes a memory leak in Physics\ncalculations for pixel-perfect collisions.\nSaves a small amount of RAM.");
    so.FancyToggle(PIXEL_MOVE, "Optimizes pixel movement generator\nused for pixel-perfect collisions.\nSaves a small amount of CPU.");

    Gunfig ao = _Gunfig.AddSubMenu(AGGR_OPT);
    ao.FancyToggleOff(GOOP, "Speeds up goop updates by using\nfaster iterators and lookup algorithms.\nSaves a large amount of CPU.");
    ao.FancyToggleOff(MATH, "Speeds up some geometry calculations\nby using optimized algorithms.\nSaves a significant amount of CPU.");
    ao.FancyToggleOff(CHUNKBUILD, "Reuses temporary storage structures when\nrebuilding chunk data during level gen.\nSaves a significant amount of RAM.");
    ao.FancyToggleOff(LINEAR_CAST, "Speeds up linear cast physics calculations by\nusing inline arithmetic wherever possible.\nSaves a significant amount of CPU.");
    ao.FancyToggleOff(POINTCAST, "Speeds up pointcast physics calculations by\nusing inlined logic where possible.\nSaves a modest amount of CPU and RAM.");
    ao.FancyToggleOff(DUNGEON_DIMS, "Speeds up dungeon size lookups by\nusing fields instead of properties.\nSaves a modest amount of CPU.");
    ao.FancyToggleOff(DEPTH_CHECKS, "Speeds up attached sprite depth checks\nby caching property accesses.\nSaves a modest amount of CPU.");
    ao.FancyToggleOff(PIT_VFX, "Speeds up pit VFX calculations by skipping\nseveral redundant tile checks.\nSaves a small amount of CPU.");
    ao.FancyToggleOff(ITEM_LOOKUPS, "Speeds up passive / active item lookups\nby skipping delegate creation.\nSaves a small amount of CPU and RAM.");

    Gunfig eo = _Gunfig.AddSubMenu(EXPR_OPT);
    eo.FancyToggleOff(MOUSE_EVENTS, "Prevents checks for whether the mouse is\nover a menu item when no menus are open.\nSaves significant CPU, but may break custom UIs.");
    eo.FancyToggleOff(TITLE_SCREEN, "Prevents scanning for the player on\nthe title screen when no player exists.\nSaves small CPU, but may break floor loads.");

    GGVConfig.Update();
    Gunfig.OnAllModsLoaded += LateInit;
  }

  private static readonly List<string> DefaultEnabled = ["Enabled", "Disabled"];
  private static readonly List<string> DefaultDisabled = ["Disabled", "Enabled"];
  private static void FancyToggle(this Gunfig gunfig, string toggleName, string toggleDesc)
  {
    string info = toggleDesc.Green();
    gunfig.AddScrollBox(key: toggleName, options: DefaultEnabled, info: [info, info], updateType: Gunfig.Update.OnRestart);
  }
  private static void FancyToggleOff(this Gunfig gunfig, string toggleName, string toggleDesc)
  {
    string info = toggleDesc.Green();
    gunfig.AddScrollBox(key: toggleName, options: DefaultDisabled, info: [info, info], updateType: Gunfig.Update.OnRestart);
  }
  private static void FancyMemList(this Gunfig gunfig, string toggleName, string toggleDesc)
  {
    string info = toggleDesc.Green();
    List<string> options = ["Default", "1GB", "2GB", "3GB", "4GB", "5GB", "6GB", "7GB", "8GB", "9GB", "10GB"];
    gunfig.AddScrollBox(key: toggleName, options: options, info: Enumerable.Repeat<string>(info, options.Count).ToList(),
      updateType: Gunfig.Update.OnRestart);
  }

  private static void LateInit()
  {
  }
}
