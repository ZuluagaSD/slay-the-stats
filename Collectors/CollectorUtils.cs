using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace SlayTheStats.Collectors;

/// <summary>
/// Shared utilities for extracting player identity from game objects.
/// </summary>
public static class CollectorUtils
{
    /// <summary>
    /// Returns a stable player index (0–3) from a Creature that represents a player.
    /// Returns -1 if the creature is not a player.
    /// </summary>
    public static int GetPlayerId(Creature? creature)
    {
        if (creature is null || !creature.IsPlayer || creature.Player is null)
            return -1;

        return GetPlayerId(creature.Player);
    }

    /// <summary>
    /// Returns a stable player index (0–3) from a Player instance using NetId.
    /// </summary>
    public static int GetPlayerId(Player? player)
    {
        if (player is null)
            return -1;

        try
        {
            // Use NetId as the stable identifier across the session.
            // In single-player, there's typically one player (index 0).
            // In multiplayer, we hash the NetId to a small index.
            return (int)(player.NetId % 4);
        }
        catch (Exception ex)
        {
            MainFile.LogError($"CollectorUtils.GetPlayerId failed: {ex.Message}");
            return -1;
        }
    }

    /// <summary>
    /// Gets the display name for a creature.
    /// </summary>
    public static string GetCreatureName(Creature? creature)
    {
        if (creature is null) return "Unknown";
        try
        {
            return creature.Name;
        }
        catch
        {
            return "Unknown";
        }
    }

    /// <summary>
    /// Gets the character class for a player creature.
    /// </summary>
    public static string GetCharacterClass(Creature? creature)
    {
        if (creature?.Player?.Character is null) return "Unknown";
        try
        {
            return creature.Player.Character.Id.Entry;
        }
        catch
        {
            return "Unknown";
        }
    }

    /// <summary>
    /// Registers a player creature with the StatManager if not already registered.
    /// </summary>
    public static void EnsurePlayerRegistered(Player? player)
    {
        if (player is null) return;

        int playerId = GetPlayerId(player);
        if (playerId < 0) return;

        var name = GetCreatureName(player.Creature);
        var charClass = GetCharacterClass(player.Creature);
        Core.StatManager.Instance.RegisterPlayer(playerId, name, charClass);
    }
}
