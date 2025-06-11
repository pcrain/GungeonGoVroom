# Changelog

## 1.4.2 (TBD)

- Fixed IndexOutOfRange exception caused by trying to add / remove goop circles outside the bounds of the current map
- Added "Optimize Pause Menu" option, dramatically reducing CPU usage while the game is paused
- Moved "Optimize Path Recalculations" to Safe Optimizations
- Slightly improved goop optimizations

## 1.4.1 (2025-06-07)

- Added "Preallocate Heap Memory" option
- Fixed issue where switching to The Judge on a new floor from a weapon with the same base ammo type would break The Judge's ammo UI rendering
- Fixed Elevator rendering issue caused by "Optimize Visibility Checks" option

## 1.4.0 (2025-06-06)

- Added "Unpause / Repause Fix", fixing an issue where the game continues to run in the background when unpausing and quickly repausing 
- Added "Optimize Linear Cast" option, mitigating lag caused by having lots of very fast moving projectiles on the screen
- Added "Optimize Linear Cast Pool" to fix a memory leak in physics calculations
- Added "Optimize Pixel Movement Gen" and "Optimize Pixel Rotation" options to speed up some physics calculations
- Further improved "Optimize Ammo Display" option to save even more CPU and RAM
- Further improved "Optimize Bullet Trails" option to save even more RAM
- Optimized more various math functions
- Added logging showing which GGV configuration options are enabled on startup

## 1.3.3 (2025-06-01)

- Fixed major UI corruption when switching floors due to caching Blasphemy's non-existent ammo UI render data
- Fixed several minor ammo UI rendering errors caused by Blasphemy's invisible ammo and switching between two guns with the same ammo type
- Re-enabled Optimize Ammo Display option

## 1.3.2 (2025-06-01)

- Temporarily hard-disabled Optimize Ammo Display to investigate major bug

## 1.3.1 (2025-05-31)

- Added "Ammo Drift Fix" option

## 1.3.0 (2025-05-31)

- Added "Optimize Ammo Display" option, fixing major lag spikes when updating ammo displays with a large number of sprites

## 1.2.2 (2025-05-29)

- Fixed player rendering over the elevator when Optimize Visibility Checks is enabled
- Fixed not being able to use mouse on the title screen when Optimize GUI Mouse Events is enabled

## 1.2.1 (2025-05-26)

- Fixed serious error with goop optimizations breaking when floors are resized (e.g., by using the Drill item or opening the trapdoor to the RR's Lair)

## 1.2.0 (2025-05-20)

- Made Gungeon Go Vroom apply patches earlier in the load process so other mods can benefit from them
- Added several more new optimizations (see readme for full list)
- Added "Experimental Optimizations" category
- Added fix for beam weapons not ignoring damage caps even when the flag to do so is set (mostly for modded use, doesn't affect vanilla guns)
- Added fix for Evolver devolving to its 2nd form when dropped, picked up, and used to kill 5 enemies
- Improved fix for room shuffling algorithms to now apply more generally to all shuffling errors (now named "Shuffle Fix")
- Renamed erroneously-labeled "Quicksave Fix" to more-appropriately labeled "Quick Restart Fix" 
- Actually remembered to bundle Optimize IMGUI with the Thunderstore package

## 1.1.0 (2025-05-12)

- Added several new optimizations (see readme for full list)
- Made all option changes now only take effect upon restarting the game (for stability and performance reasons)
  - Added warning to this effect to the Gungeon Go Vroom menu
- Bundled the [Optimize IMGUI](https://github.com/BepInEx/BepInEx.Utility) plugin for convenience and additional performance (see readme for license information) 
- Updated required Gunfig version to 1.1.7 for fixes with some menu issues

## 1.0.0 (2025-05-08)

- Initial Release :D
