using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MorePlayers.Config;

namespace MorePlayers;

[BepInPlugin(Guid, Name, Version)]
[BepInProcess("PlateUp.exe")]
public class MorePlayers : BaseUnityPlugin
{
    private const string Guid = "MorePlayers";
    private const string Name = "MorePlayers";
    private const string Version = "2.2.1";

    internal static ManualLogSource Log;

    private void Awake()
    {
        Log = Logger;

        // Config must be bound before patching, since the patches read it.
        ConfigHelper.SetUp(Config);

        var harmony = new Harmony(Guid);
        harmony.PatchAll();

        Log.LogMessage($"Loaded MorePlayers version: {Version} (max players: {ConfigHelper.getMaxPlayers()})");
    }
}
