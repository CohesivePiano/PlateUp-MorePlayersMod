# PlateUp-MorePlayersMod
Mod that raises PlateUp's 4-player limit. BepInEx 5 plugin.

Updated for the current PlateUp build (Unity 2020.3.48, game with the built-in Workshop mod loader).

## Install (every player)
1. Download the latest release from [Releases](https://github.com/austinmiddle1/PlateUp-MorePlayersMod/releases/latest).
2. If you already have BepInEx 5 installed, download the plugin-only ZIP. Otherwise, download the ZIP that includes BepInEx.
3. Extract the ZIP into the folder that contains `PlateUp.exe`.
4. Start the game once and close it at the main menu; this creates `BepInEx/config`.
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
| Only 4 bedrooms; furniture is owner-only | `CreateBedrooms.OnUpdate` | Postfix adds a second furniture set + spawn for players 5–8 in bedrooms 1–4 |
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

- The HQ generates its maps as soon as it loads, before friends join. When the lobby grows past what the maps were sized for, the mod regenerates them (the same refresh the game does when you change the restaurant setting) once the player count has been stable for 3 seconds and nobody is carrying a map. Maps never shrink when players leave.
- To always get big maps regardless of who is in the lobby, set `Size for at least N players` (e.g. 8).
- Daily/weekly seeded-run maps are not regenerated.
- Existing restaurants and saves keep their size.
- If a stretched layout repeatedly fails the game's checks, the mod falls back to a normal-size map rather than leaving you with no map.

## Shared bedrooms
The HQ has 4 bedrooms, and each room's furniture only works for the player it belongs to, so without this players 5–8 couldn't change their outfit or colour. With `[HQ] Shared bedrooms = true` (default), player 5 shares bedroom 1, player 6 bedroom 2, and so on. Each gets their own bed, outfit station, name/profile indicator and spawn point in that room.

Free tiles are found from the HQ's floor plan when it loads. A placement is only used if everything in the room (old and new) can still be reached and the room stays walkable. If a room is too cramped, the bed is dropped first, and as a last resort the player just gets a spawn point there. The log shows what was placed for each player.

## Known limitations
- The old "Player confirmation count" setting was removed; ready-ups need everyone, as in the base game.

## Building
```
dotnet build MorePlayers/MorePlayers.csproj -c Release -p:GameDir="<path to folder containing PlateUp.exe>"
```
The build creates `MorePlayers/bin/Release/MorePlayers.dll`. If BepInEx is installed in `GameDir`, the DLL is also copied into `BepInEx/plugins`.

## Publishing a release
The GitHub Actions workflow packages `MorePlayers/bin/Release/MorePlayers.dll` into downloadable ZIPs when you push a version tag such as `v1.2.0`.

Before tagging a new version, build the project in Release mode, commit the updated `MorePlayers/bin/Release/MorePlayers.dll` along with your changes, then push the tag. The workflow publishes both a plugin-only ZIP and a ZIP that includes BepInEx.
