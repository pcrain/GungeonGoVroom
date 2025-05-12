## What Is This Mod?

Gungeon Go Vroom fixes several bugs in vanilla Enter the Gungeon's code, and optimizes several other parts of Gungeon's code to reduce CPU usage, RAM usage, and lag spike frequency. Each fix and optimization can be individually toggled via the Mod Config menu (Options -> Mod Config -> Gungeon Go Vroom). **NOTE:** For safety and performance reasons, most changes will only take effect upon restarting the game.

Gungeon Go Vroom's changes are organized into three main categories:

### Bugfixes

These are changes that modify vanilla Gungeon's behavior to fix various bugs with the game. By their nature, these changes are NOT suitable for players who require unaltered vanilla functionality (e.g., for speedrunning leaderboards), but should otherwise improve the experience for vanilla and modded players alike. All bugfixes are enabled by default.

Currently included bugfixes are:
  - **Duct Tape Fix**: Fixes duct-taped guns sometimes breaking when using the elevator save button.
  - **Quicksave Fix**: Fixes once-per-run rooms not properly resetting with Quick Restart, preventing them from respawning until visiting the Breach.
  - **Room Shuffle Fix**: Fixes an off-by-one error in room randomization, making certain rooms always / never spawn in certain unintended situations.
  - **Ammo UI Fix**: Fixes a rendering issue with final projectiles in the ammo indicator causing them to render above UI elements they shouldn't.
  - **Orbital Gun Fix**: Fixes orbital guns visually firing from the wrong location if created while the player is facing left.
  - **Co-op Turbo Mode Fix**: Fixes co-op partner in turbo mode not getting turbo mode speed buffs until their stats have changed at least once.
  - **Bullet Trail Fix**: Fixes the trails of projectiles disappearing if they travel too slowly (e.g., during timeslow effects).

### Safe Optimizations

These are changes that improve Gungeon's performance without altering vanilla behavior, and which have a negligible chance of interfering with other mods. Safe optimizations should be suitable for all players, and are all enabled by default.

Currently included safe optimizations are:
  - **Optimize Light Culling**: Uses optimized inlined logic for determining whether lights should be culled. Saves a significant amount of CPU.
  - **Optimize Beams**: Pools beam bones to reduce memory usage.  Saves a modest amount of RAM and CPU.
  - **Optimize GUI Events**: Caches results of expensive lookups for finding GUI event handlers. Saves a modest amount of RAM.
  - **Optimize Flood Filling**: Uses an optimized flood fill algorithm for floor post-processing. Saves a small amount of CPU and RAM.
  - **Optimize Bullet Trails**: Pools bullet trail particles to reduce memory usage. Saves a small amount of RAM.
  - **Optimize Projectile Prefabs**: Removes prefab effect data (e.g., poison) from projectiles that never apply those effects. Saves a small amount of RAM.

### Aggressive Optimizations

These are changes that improve Gungeon's performance without altering vanilla behavior, but could theoretically interfere with other mods. While these optimizations currently have no known compatibility issues with other mods and should be suitable for all players, they are all *DISABLED* by default.

Currently included aggressive optimizations are:
  - **Optimize Math**: Speeds up some geometry calculations by using optimized algorithms. Saves a significant amount of CPU.
  - **Optimize Chunk Building**: Reuses temporary storage structures when rebuilding chunk data during level gen. Saves a significant amount of RAM.
  - **Optimize Pointcast**: Speeds up pointcast physics calculations by using statics instead of delegates. Saves a modest amount of CPU.
  - **Optimize Dungeon Size Checks**: Speeds up dungeon size lookups by using fields instead of properties. Saves a modest amount of CPU.
  - **Optimize Pit VFX**: Speeds up pit VFX calculations by skipping several redundant tile checks. Saves a small amount of CPU.
  - **Optimize Item Lookups**: Speeds up passive / active item lookups by skipping delegate creation. Saves a small amount of RAM.

## Issues? Suggestions?

[Feel free to use the Issue Tracker on Github](https://github.com/pcrain/GungeonGoVroom/issues) if you encounter any issues or have any other vanilla bugs you'd like to see fixed. C:
