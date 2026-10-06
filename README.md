# PlateUp-MorePlayersMod
Mod that raises PlateUp's 4-player limit. BepInEx 5 plugin.

Updated for the current PlateUp build (Unity 2020.3.48, game with the built-in Workshop mod loader).

## Install (every player)
1. Download BepInEx 5 (x64, `BepInEx_win_x64_5.4.x.zip`) from https://github.com/BepInEx/BepInEx/releases.
2. Extract it into the folder that contains `PlateUp.exe`.
3. Start the game once and close it at the main menu (this creates `BepInEx/plugins` and `BepInEx/config`).
4. Put `MorePlayers.dll` into `BepInEx/plugins`.
5. Optional: edit `BepInEx/config/MorePlayers.cfg` and set `Max players` (4–8, default 8).

**Everyone should install the mod.** The host's copy is what raises the lobby and player cap; matching versions avoid surprises.

The lobby size is fixed when the lobby is created, so after changing the config the host should restart the game.

## What it patches
| Cap | Where | Patch |
| --- | --- | --- |
| Steam lobby size 4 | `SteamNetworkService.CreateNewLobby` → `SteamMatchmaking.CreateLobbyAsync(4)` | Prefix rewrites `maxMembers` |
| Photon (crossplay) room size 4 | `PhotonNetworkService.CreateNewLobby` → `RoomOptions.MaxPlayers = 4` | Prefix on `LoadBalancingClient.OpCreateRoom` |
| Player slot index < 4 | `PlayerManager.MaxPlayers` (readonly field) | Postfix on `Initialise` sets the field |
| Join prompt hidden at 4 (cosmetic) | `PlayerInfoManager.EnsureCorrectModules` / `ArrangeModules` | Transpiler replaces the literal 4 |
| Difficulty stops scaling at 4 | `DifficultyHelpers` customer rate / patience / fire spread | Postfixes extend the curves past 4 |
| Restaurant size fixed | `CreateLayoutHelper.ConstructLayout` → `LayoutGraph.Build` | Postfix stretches the generated blueprint before decoration |

## Difficulty scaling past 4 players
The base game stops scaling difficulty at 4 players. With `Scale past 4 players = true` (default) the mod continues it:

| Players | Customers | Patience drain | Fire spread |
| --- | --- | --- | --- |
| 4 (vanilla) | 1.5x | 1.15x | 1.5x |
| 5 | 1.875x | 1.20x | 1.8x |
| 6 | 2.25x | 1.25x | 2.1x |
| 7 | 2.625x | 1.30x | 2.4x |
| 8 | 3.0x | 1.35x | 2.7x |

Customers scale in proportion to player count; patience and fire continue the game's own 3→4 player step. The money reward multiplier is unchanged. 1–4 players play exactly like vanilla.

## Bigger restaurants
With `[Layout] Bigger restaurants = true` (default), newly generated restaurant maps grow when more than 4 players are in the lobby. The kitchen + dining area is stretched so its area grows in proportion to player count (each side × √(players ÷ 4), at most +6 tiles per side), by duplicating rows/columns that run through the kitchen or dining room. Walls, doors and hatches stay consistent; the game's usual layout checks and decoration run on the bigger map.

- Maps are sized when they are generated in the HQ, so have everyone join before generating a new restaurant — or set `Size for at least N players` (e.g. 8) to always generate big maps.
- Existing restaurants and saves keep their size.
- If a stretched layout repeatedly fails the game's checks, the mod falls back to a normal-size map rather than leaving you with no map.

## Known limitations
- The HQ only has 4 bedrooms. Players 5+ have no spawn marker, so they appear at the HQ's world origin, and have no bed/outfit station of their own.
- The old "Player confirmation count" setting was removed; ready-ups need everyone, as in the base game.

## Building
```
dotnet build MorePlayers/MorePlayers.csproj -c Release -p:GameDir="<path to folder containing PlateUp.exe>"
```
If BepInEx is installed in `GameDir`, the DLL is copied into `BepInEx/plugins` automatically.
