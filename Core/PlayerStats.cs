namespace SlayTheStats.Core;

public class PlayerStats
{
    public int PlayerId { get; }
    public string PlayerName { get; set; } = "Unknown";
    public string CharacterClass { get; set; } = "Unknown";

    public PlayerStats(int playerId)
    {
        PlayerId = playerId;
    }
}
