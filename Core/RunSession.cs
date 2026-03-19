namespace SlayTheStats.Core;

public class RunSession
{
    public DateTime StartTime { get; } = DateTime.UtcNow;
    public DateTime? EndTime { get; private set; }
    public List<CombatSession> Combats { get; } = [];
    public Dictionary<int, PlayerStats> Players { get; } = [];

    public void AddCombat(CombatSession combat)
    {
        Combats.Add(combat);
    }

    public void End()
    {
        EndTime = DateTime.UtcNow;
    }

    public PlayerStats GetOrCreatePlayer(int playerId)
    {
        if (!Players.TryGetValue(playerId, out var stats))
        {
            stats = new PlayerStats(playerId);
            Players[playerId] = stats;
        }
        return stats;
    }

    public int TotalDamageByPlayer(int playerId)
    {
        return Combats.Sum(c => c.TotalDamageByPlayer(playerId));
    }

    public int TotalBlockByPlayer(int playerId)
    {
        return Combats.Sum(c => c.TotalBlockByPlayer(playerId));
    }

    public int TotalCardsPlayedByPlayer(int playerId)
    {
        return Combats.Sum(c => c.TotalCardsPlayedByPlayer(playerId));
    }

    public Dictionary<int, int> RunDamageByPlayer()
    {
        var result = new Dictionary<int, int>();
        foreach (var combat in Combats)
        {
            foreach (var (playerId, dmg) in combat.DamageByPlayer())
            {
                result.TryGetValue(playerId, out var existing);
                result[playerId] = existing + dmg;
            }
        }
        return result;
    }

    public int? OverallMvp()
    {
        var totals = RunDamageByPlayer();
        return totals.Count == 0 ? null : totals.MaxBy(kvp => kvp.Value).Key;
    }
}
