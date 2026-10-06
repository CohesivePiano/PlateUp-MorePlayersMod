using BepInEx.Configuration;

namespace MorePlayers.Config;

public static class ConfigHelper
{
    private const int VanillaMaxPlayers = 4;

    private static ConfigEntry<int> _maxPlayers;
    private static ConfigEntry<bool> _scaleDifficulty;

    public static void SetUp(ConfigFile config)
    {
        _maxPlayers = config.Bind("Players", "Max players", 8,
            new ConfigDescription(
                "How many players can join lobbies you host. Restart the game (or re-host) after changing this.",
                new AcceptableValueRange<int>(VanillaMaxPlayers, 8)));
        _scaleDifficulty = config.Bind("Difficulty", "Scale past 4 players", true,
            "Keep increasing customers, patience drain and fire spread for each player beyond 4 (the base game stops scaling at 4).");
    }

    public static int getMaxPlayers()
    {
        return _maxPlayers?.Value ?? VanillaMaxPlayers;
    }

    public static bool getScaleDifficulty()
    {
        return _scaleDifficulty?.Value ?? false;
    }
}
