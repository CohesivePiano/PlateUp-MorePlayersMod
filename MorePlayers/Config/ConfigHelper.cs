using BepInEx.Configuration;

namespace MorePlayers.Config;

public static class ConfigHelper
{
    private const int VanillaMaxPlayers = 4;

    private static ConfigEntry<int> _maxPlayers;

    public static void SetUp(ConfigFile config)
    {
        _maxPlayers = config.Bind("Players", "Max players", 8,
            new ConfigDescription(
                "How many players can join lobbies you host. Restart the game (or re-host) after changing this.",
                new AcceptableValueRange<int>(VanillaMaxPlayers, 16)));
    }

    public static int getMaxPlayers()
    {
        return _maxPlayers?.Value ?? VanillaMaxPlayers;
    }
}
