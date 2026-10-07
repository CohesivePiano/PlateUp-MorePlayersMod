using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kitchen;

namespace MorePlayers
{
    // Cosmetic: the "press to join" prompt in the player bar hides once 4 players exist
    // (PlayerModules.Count < 4). Swap that literal 4 for the configured max.
    [HarmonyPatch]
    public class PlayerInfoManagerJoinPromptOverridePatch
    {
        private static readonly MethodInfo GetMaxPlayers = AccessTools.Method(typeof(Config.ConfigHelper), nameof(Config.ConfigHelper.getMaxPlayers));

        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PlayerInfoManager), "EnsureCorrectModules");
            yield return AccessTools.Method(typeof(PlayerInfoManager), "ArrangeModules");
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldc_I4_4)
                {
                    replaced++;
                    yield return new CodeInstruction(OpCodes.Call, GetMaxPlayers).MoveLabelsFrom(instruction);
                }
                else
                {
                    yield return instruction;
                }
            }
            if (replaced != 1)
            {
                MorePlayers.Log.LogWarning($"Expected one player-count literal in PlayerInfoManager.{original.Name}, replaced {replaced}");
            }
        }
    }
}
