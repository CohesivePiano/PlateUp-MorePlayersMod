using HarmonyLib;
using Kitchen;

namespace MorePlayers
{
    // DifficultyHelpers stops scaling at 4 players (the switch default covers 4+), so 5-8 players
    // get the same difficulty as 4. Extend each curve past 4. Vanilla values are untouched for 1-4 players.
    [HarmonyPatch(typeof(DifficultyHelpers))]
    public class DifficultyScalingOverridePatch
    {
        private const int VanillaMaxPlayers = 4;

        private static int ExtraPlayers(int player_count)
        {
            if (!Config.ConfigHelper.getScaleDifficulty())
            {
                return 0;
            }
            return player_count > VanillaMaxPlayers ? player_count - VanillaMaxPlayers : 0;
        }

        // Customers per hour: proportional to player count, matching 4 players = 1.5x (5 = 1.875x, 8 = 3.0x).
        [HarmonyPostfix]
        [HarmonyPatch(nameof(DifficultyHelpers.CustomerPlayersRateModifier))]
        public static void CustomerRate(int player_count, ref float __result)
        {
            if (ExtraPlayers(player_count) > 0)
            {
                __result = 1.5f * player_count / VanillaMaxPlayers;
            }
        }

        // Patience drain: continue the game's own 3->4 player step (+0.05 per player).
        [HarmonyPostfix]
        [HarmonyPatch(nameof(DifficultyHelpers.PatiencePlayerCountModifier))]
        public static void Patience(int player_count, ref float __result)
        {
            __result += 0.05f * ExtraPlayers(player_count);
        }

        // Fire spread: continue the game's own 3->4 player step (+0.3 per player).
        [HarmonyPostfix]
        [HarmonyPatch(nameof(DifficultyHelpers.FireSpreadModifier))]
        public static void FireSpread(int player_count, ref float __result)
        {
            __result += 0.3f * ExtraPlayers(player_count);
        }
    }
}
