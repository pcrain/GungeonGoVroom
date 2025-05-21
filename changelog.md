# Changelog

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
