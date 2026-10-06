# PlateUp-MorePlayersMod
Mod that raises PlateUp's 4-player limit. BepInEx 5 plugin.

Updated for the current PlateUp build (Unity 2020.3.48, game with the built-in Workshop mod loader).

## Install (every player)
1. Download BepInEx 5 (x64, `BepInEx_win_x64_5.4.x.zip`) from https://github.com/BepInEx/BepInEx/releases.
2. Extract it into the folder that contains `PlateUp.exe`.
3. Start the game once and close it at the main menu (this creates `BepInEx/plugins` and `BepInEx/config`).
4. Put `MorePlayers.dll` into `BepInEx/plugins`.
5. Optional: edit `BepInEx/config/MorePlayers.cfg` and set `Max players` (4–16, default 8).

**Everyone should install the mod.** The host's copy is what raises the lobby and player cap; matching versions avoid surprises.

The lobby size is fixed when the lobby is created, so after changing the config the host should restart the game.

## What it patches
| Cap | Where | Patch |
| --- | --- | --- |
| Steam lobby size 4 | `SteamNetworkService.CreateNewLobby` → `SteamMatchmaking.CreateLobbyAsync(4)` | Prefix rewrites `maxMembers` |
| Photon (crossplay) room size 4 | `PhotonNetworkService.CreateNewLobby` → `RoomOptions.MaxPlayers = 4` | Prefix on `LoadBalancingClient.OpCreateRoom` |
| Player slot index < 4 | `PlayerManager.MaxPlayers` (readonly field) | Postfix on `Initialise` sets the field |
| Join prompt hidden at 4 (cosmetic) | `PlayerInfoManager.EnsureCorrectModules` / `ArrangeModules` | Transpiler replaces the literal 4 |

## Known limitations
- The HQ only has 4 bedrooms. Players 5+ spawn at the default HQ spawn point and have no bed/outfit station of their own.
- The old "Player confirmation count" setting was removed; ready-ups need everyone, as in the base game.

## Building
```
dotnet build MorePlayers/MorePlayers.csproj -c Release -p:GameDir="<path to folder containing PlateUp.exe>"
```
If BepInEx is installed in `GameDir`, the DLL is copied into `BepInEx/plugins` automatically.
