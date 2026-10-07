using System.Reflection;
using HarmonyLib;
using Kitchen;

namespace MorePlayers
{
    // PlayerManager.GetLeastUnusedIndex only hands out indices below the readonly MaxPlayers field (4),
    // so a 5th player is silently refused even when the lobby has room. Raise the field once the system exists.
    [HarmonyPatch(typeof(PlayerManager), "Initialise")]
    public class PlayerManagerInitialiseOverridePatch
    {
        private static readonly FieldInfo MaxPlayersField = AccessTools.Field(typeof(PlayerManager), nameof(PlayerManager.MaxPlayers));

        [HarmonyPostfix]
        public static void Postfix(PlayerManager __instance)
        {
            int max = Config.ConfigHelper.getMaxPlayers();
            MaxPlayersField.SetValue(__instance, max);
            MorePlayers.Log.LogInfo($"PlayerManager.MaxPlayers set to {__instance.MaxPlayers}");
        }
    }
}
