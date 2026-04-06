# QuickFreeplay

A [BepInEx](https://github.com/BepInEx/BepInEx) mod for **Ultimate Chicken Horse** that lets the host toggle between party match and freeplay mode mid-game, preserving game state (scores, rounds, placed blocks) across the transition.

Built for tournament and competitive use.

## Features

- **One-key toggle** (default `F3`) to switch between party match and freeplay
- **Score persistence** - all points are restored when returning from freeplay, with a scoreboard animation so UCHScoreboard picks them up
- **Round tracking** - remaining rounds are synced correctly for host and all clients
- **Multiplayer compatible** - only the host needs the mod installed
- **Party box delay** - the scoreboard animation plays before the party box opens, keeping things clean for tournament streams
- **Correct item percentages** - the game calculates block density (which affects what items appear in the party box) during the play phase, so on a fresh session it would be stale until the second round. When returning from freeplay on round 2 or later, density is recalculated immediately from the restored blocks so the very first party box after returning already has the correct item weights

## Requirements

- [Ultimate Chicken Horse](https://store.steampowered.com/app/386940/Ultimate_Chicken_Horse/) (Steam)
- [BepInEx 5.x](https://github.com/BepInEx/BepInEx/releases) (Unity Mono)

## Installation

1. Install BepInEx 5.x into your Ultimate Chicken Horse game folder
2. Download `QuickFreeplay.dll` from [Releases](../../releases)
3. Drop it into `BepInEx/plugins/`
4. Launch the game

## Usage

| Action | Default Key |
|--------|------------|
| Toggle freeplay / return to match | `F3` |

The hotkey can be changed in `BepInEx/config/com.ossie.quickfreeplay.cfg` after first launch.

## Building from Source

### Prerequisites

- [.NET SDK 6.0+](https://dotnet.microsoft.com/download)
- Ultimate Chicken Horse installed via Steam

### Setup

1. Clone this repo
2. Set the `UCHPath` environment variable to your game install folder, **or** edit the path in `QuickFreeplay/QuickFreeplay.csproj`:
   ```xml
   <UCHPath>D:\Program Files\Steam\steamapps\common\Ultimate Chicken Horse</UCHPath>
   ```
3. Close the game (the build copies the DLL to the plugins folder)
4. Run `build.bat` or:
   ```
   dotnet build QuickFreeplay\QuickFreeplay.csproj
   ```

The DLL is automatically deployed to `BepInEx/plugins/` after a successful build.

## Project Structure

```
QuickFreeplay/
  QuickFreeplayPlugin.cs       # BepInEx plugin entry point
  Config/
    QuickFreeplayConfig.cs     # Hotkey configuration
  Core/
    QuickFreeplayManager.cs    # Main state machine & score restore logic
    MatchStateSnapshot.cs      # Serializable match state data
    PlacedBlockData.cs         # Block position/rotation data
    ReflectionCache.cs         # Cached reflection handles for game internals
  Patches/
    VersusControlPatch.cs      # Harmony patches for party match setup
    FreePlayControlPatch.cs    # Harmony patches for freeplay setup
  UI/
    QuickFreeplayOverlay.cs    # IMGUI overlay with toggle button & status
```

## How It Works

1. **Entering freeplay**: Captures a snapshot of the current match state (scores, round number, max rounds, placed blocks), switches to freeplay mode, and reloads the scene
2. **Returning to match**: Restores party mode, reloads the scene, then during the PLACE phase:
   - Recalculates `levelDensity` from the restored blocks (only on round 2+) so the first party box after returning uses correct item weights instead of a stale zero
   - Syncs the reduced max rounds to non-host clients via `MsgApplyRuleset`
   - Waits for UCHScoreboard to initialize
   - Replays all saved points as `MsgPointAwarded` messages through the game's network event pipeline
   - Triggers the scoreboard animation so UCHScoreboard displays the restored scores
   - Opens the party box after the animation finishes

## License

The Unlicense
