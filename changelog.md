# Changelog

## 1.3.4 (TBD)

- Added logging showing which GGV configuration options are enabled on startup
- Further improved "Optimize Ammo Display" option
- Added "Optimize Linear Cast" option, mitigating lag caused by having lots of very fast moving projectiles on the screen
- Added "Unpause / Repause Fix"

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
