using HarmonyLib;
using Photon.Realtime;
using Steamworks;

namespace MorePlayers
{
    // SteamNetworkService.CreateNewLobby calls SteamMatchmaking.CreateLobbyAsync(4).
    // That call lives inside an async state machine, so patch the Facepunch method it calls instead.
    [HarmonyPatch(typeof(SteamMatchmaking), nameof(SteamMatchmaking.CreateLobbyAsync))]
    public class SteamCreateLobbyOverridePatch
    {
        [HarmonyPrefix]
        public static void Prefix(ref int maxMembers)
        {
            int max = Config.ConfigHelper.getMaxPlayers();
            if (maxMembers < max)
            {
                MorePlayers.Log.LogInfo($"Creating Steam lobby with {max} slots (game asked for {maxMembers})");
                maxMembers = max;
            }
        }
    }

    // PhotonNetworkService.CreateNewLobby (used for crossplay) creates rooms with RoomOptions.MaxPlayers = 4.
    [HarmonyPatch(typeof(LoadBalancingClient), nameof(LoadBalancingClient.OpCreateRoom))]
    public class PhotonCreateRoomOverridePatch
    {
        [HarmonyPrefix]
        public static void Prefix(EnterRoomParams enterRoomParams)
        {
            RoomOptions options = enterRoomParams?.RoomOptions;
            int max = Config.ConfigHelper.getMaxPlayers();
            if (options != null && options.MaxPlayers > 0 && options.MaxPlayers < max)
            {
                MorePlayers.Log.LogInfo($"Creating Photon room with {max} slots (game asked for {options.MaxPlayers})");
                options.MaxPlayers = max;
            }
        }
    }
}
