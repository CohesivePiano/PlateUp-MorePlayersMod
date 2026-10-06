using BepInEx.Configuration;

namespace MorePlayers.Config;

public static class ConfigHelper
{
    private const int VanillaMaxPlayers = 4;

    private static ConfigEntry<int> _maxPlayers;
    private static ConfigEntry<bool> _scaleDifficulty;
    private static ConfigEntry<bool> _biggerLayouts;
    private static ConfigEntry<int> _layoutSizePlayers;

    public static void SetUp(ConfigFile config)
    {
        _maxPlayers = config.Bind("Players", "Max players", 8,
            new ConfigDescription(
                "How many players can join lobbies you host. Restart the game (or re-host) after changing this.",
                new AcceptableValueRange<int>(VanillaMaxPlayers, 8)));
        _scaleDifficulty = config.Bind("Difficulty", "Scale past 4 players", true,
            "Keep increasing customers, patience drain and fire spread for each player beyond 4 (the base game stops scaling at 4).");
        _biggerLayouts = config.Bind("Layout", "Bigger restaurants", true,
            "Make newly generated restaurant maps bigger when there are more than 4 players (area grows in proportion to player count). Existing restaurants keep their size.");
        _layoutSizePlayers = config.Bind("Layout", "Size for at least N players", 0,
            new ConfigDescription(
                "Size new maps for at least this many players even if fewer are in the lobby when the map is generated. 0 = use the number of players currently in the lobby.",
                new AcceptableValueRange<int>(0, 8)));
    }

    public static int getMaxPlayers()
    {
        return _maxPlayers?.Value ?? VanillaMaxPlayers;
    }

    public static bool getScaleDifficulty()
    {
        return _scaleDifficulty?.Value ?? false;
    }

    public static bool getBiggerLayouts()
    {
        return _biggerLayouts?.Value ?? false;
    }

    public static int getLayoutSizePlayers()
    {
        return _layoutSizePlayers?.Value ?? 0;
    }
}
