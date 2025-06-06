## What Is This Mod?

Gungeon Go Vroom fixes several bugs in vanilla Enter the Gungeon's code, and optimizes several other parts of Gungeon's code to reduce CPU usage, RAM usage, and lag spike frequency. Each fix and optimization can be individually toggled via the Mod Config menu (Options -> Mod Config -> Gungeon Go Vroom). *For safety and performance reasons, most changes will only take effect upon restarting the game.*

Gungeon Go Vroom's changes are organized into four main categories:

### Bugfixes

These are changes that modify vanilla Gungeon's behavior to fix various bugs with the game. By their nature, these changes are NOT suitable for players who require unaltered vanilla functionality (e.g., for speedrunning leaderboards), but should otherwise improve the experience for vanilla and modded players alike. All bugfixes are enabled by default.

Currently included bugfixes are:
  - **Duct Tape Fix**: Fixes duct-taped guns sometimes breaking when using the elevator save button.
  - **Quick Restart Fix**: Fixes once-per-run rooms not properly resetting with Quick Restart, preventing them from respawning until visiting the Breach.
  - **Shuffle Fix**: Fixes an off-by-one error in shuffling algorithms, making rooms always / never spawn in unintended situations, among other issues.
  - **Ammo UI Fix**: Fixes a rendering issue with final projectiles in the ammo indicator causing them to render above UI elements they shouldn't.
  - **Orbital Gun Fix**: Fixes orbital guns visually firing from the wrong location if created while the player is facing left.
  - **Co-op Turbo Mode Fix**: Fixes co-op partner in turbo mode not getting turbo mode speed buffs until their stats have changed at least once.
  - **Bullet Trail Fix**: Fixes the trails of projectiles disappearing if they travel too slowly (e.g., during timeslow effects).
  - **Beam Damage Cap Fix**: Fixes beams not ignoring boss damage caps even when set to do so. (No such beam exists in vanilla, mostly for modded use).
  - **Evolver Devolve Fix**: Fixes Evolver devolving to its 2nd form after dropping it, picking it back up, and killing 5 enemies to level it up.
  - **Ammo Drift Fix**: Fixes ammo display drifting to the right when a gun temporarily gets infinite ammo (e.g., from Magazine Rack).
  - **Unpause / Repause Fix**: Fixes game continuing to run if you unpause and quickly repause during menu fading animation.

### Safe Optimizations

These are changes that improve Gungeon's performance without altering vanilla behavior, and which have a negligible chance of interfering with other mods. Safe optimizations should be suitable for all players, and are all enabled by default.

Currently included safe optimizations are:
  - **Optimize Occlusion**: Speeds up occlusion calculations by using optimized algorithms and caching. Saves a large amount of CPU.
  - **Optimize Ammo Display**: Speeds up ammo display updates by caching render data. Saves a large amount of RAM.
  - **Optimize Visibility Checks**: Skips redundant sprite visibility checks when the results aren't actually used. Saves a significant amount of CPU.
  - **Optimize Light Culling**: Uses optimized inlined logic for determining whether lights should be culled. Saves a significant amount of CPU.
  - **Optimize Beams**: Pools beam bones to reduce memory usage.  Saves a modest amount of RAM and CPU.
  - **Optimize GUI Events**: Caches results of expensive lookups for finding GUI event handlers. Saves a modest amount of RAM.
  - **Optimize Bullet Trails**: Pools bullet trail particles and vertex data to reduce memory usage. Saves a modest amount of RAM.
  - **Optimize Numerical Strings**: Caches strings for small numbers used frequently by SGUI's labels. Saves significant RAM while any console is open.
  - **Optimize Flood Filling**: Uses an optimized flood fill algorithm for floor post-processing. Saves a small amount of CPU and RAM.
  - **Optimize Projectile Prefabs**: Removes prefab effect data (e.g., poison) from projectiles that never apply those effects. Saves a small amount of RAM.
  - **Optimize Chunk Checks**: Optimize checks for whether sprite chunks are relevant to gameplay. Saves a small amount of CPU.
  - **Optimize Linear Cast Pool**: Fixes a memory leak in Physics calculations for pixel-perfect collisions. Saves a small amount of RAM.
  - **Optimize Pixel Movement Gen**: Optimizes pixel movement generator used for pixel-perfect collisions. Saves a small amount of CPU.

### Aggressive Optimizations

These are changes that improve Gungeon's performance without altering vanilla behavior, but could theoretically interfere with other mods. While these optimizations currently have no known compatibility issues with other mods and should be suitable for all players, they are all *DISABLED* by default.

Currently included aggressive optimizations are:
  - **Optimize Goop Updates**: Speeds up goop updates by using faster iterators and lookup algorithms. Saves a large amount of CPU.
  - **Optimize Math**: Speeds up some geometry calculations by using optimized algorithms. Saves a significant amount of CPU.
  - **Optimize Chunk Building**: Reuses temporary storage structures when rebuilding chunk data during level gen. Saves a significant amount of RAM.
  - **Optimize Linear Cast**: Speeds up linear cast physics calculations by using inline arithmetic wherever possible. Saves a significant amount of CPU.
  - **Optimize Pointcast**: Speeds up pointcast physics calculations by using statics instead of delegates. Saves a modest amount of CPU and RAM.
  - **Optimize Dungeon Size Checks**: Speeds up dungeon size lookups by using fields instead of properties. Saves a modest amount of CPU.
  - **Optimize Sprite Depth Checks**: Speeds up attached sprite depth checks by caching property accesses. Saves a modest amount of CPU.
  - **Optimize Pit VFX**: Speeds up pit VFX calculations by skipping several redundant tile checks. Saves a small amount of CPU.
  - **Optimize Item Lookups**: Speeds up passive / active item lookups by skipping delegate creation. Saves a small amount of CPU and RAM.

### Experimental Optimizations

These are experimental changes that improve Gungeon's performance in ways that might inadvertently alter vanilla behavior. Each experimental optimization will eventually be reclassified as either safe or aggressive once it has been thoroughly tested to exactly replicate vanilla behavior. These optimizations should still be suitable for most players, but they are all *DISABLED* by default.

Currently included experimental optimizations are:
  - **Optimize GUI Mouse Events**: Prevents checks for whether the mouse is over a menu item when no menus are open. Saves significant CPU, but may break custom UIs.
  - **Optimize Path Recalculations**: Optimizes clearance computations used for enemy pathing logic. Saves modest CPU, but may freeze enemies in place.
  - **Optimize Title Screen**: Prevents scanning for the player on the title screen when no player exists. Saves small CPU, but may break floor loads.

## Issues? Suggestions?

[Feel free to use the Issue Tracker on Github](https://github.com/pcrain/GungeonGoVroom/issues) if you encounter any issues or have any other vanilla bugs you'd like to see fixed. C:

## Additional Credits

For convenience and additional performance, Gungeon Go Vroom's Thunderstore package comes bundled with the [Optimize IMGUI](https://github.com/BepInEx/BepInEx.Utility) plugin, licensed under the [GNU General Public License v3.0](https://github.com/BepInEx/BepInEx.Utility/blob/master/LICENSE). Gungeon Go Vroom does not use or require Optimize IMGUI to function.
