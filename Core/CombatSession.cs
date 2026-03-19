using SlayTheStats.Models;

namespace SlayTheStats.Core;

public class CombatSession
{
    private readonly object _lock = new();

    public DateTime StartTime { get; } = DateTime.UtcNow;
    public DateTime? EndTime { get; private set; }
    public bool IsVictory { get; private set; }
    public int CurrentTurn { get; private set; }

    public List<DamageRecord> DamageRecords { get; } = [];
    public List<BlockRecord> BlockRecords { get; } = [];
    public List<BuffRecord> BuffRecords { get; } = [];
    public List<DebuffRecord> DebuffRecords { get; } = [];
    public List<CardPlayRecord> CardPlayRecords { get; } = [];
    public List<EconomyRecord> EconomyRecords { get; } = [];

    public void SetTurn(int turn)
    {
        lock (_lock) CurrentTurn = turn;
    }

    public void IncrementTurn()
    {
        lock (_lock) CurrentTurn++;
    }

    public void End(bool victory)
    {
        lock (_lock)
        {
            EndTime = DateTime.UtcNow;
            IsVictory = victory;
        }
    }

    public void AddDamage(DamageRecord record)
    {
        lock (_lock) DamageRecords.Add(record);
    }

    public void AddBlock(BlockRecord record)
    {
        lock (_lock) BlockRecords.Add(record);
    }

    public void AddBuff(BuffRecord record)
    {
        lock (_lock) BuffRecords.Add(record);
    }

    public void AddDebuff(DebuffRecord record)
    {
        lock (_lock) DebuffRecords.Add(record);
    }

    public void AddCardPlay(CardPlayRecord record)
    {
        lock (_lock) CardPlayRecords.Add(record);
    }

    public void AddEconomy(EconomyRecord record)
    {
        lock (_lock) EconomyRecords.Add(record);
    }

    public int TotalDamageByPlayer(int playerId)
    {
        lock (_lock)
            return DamageRecords.Where(r => r.SourcePlayerId == playerId).Sum(r => r.Amount);
    }

    public int TotalBlockByPlayer(int playerId)
    {
        lock (_lock)
            return BlockRecords.Where(r => r.PlayerId == playerId).Sum(r => r.Amount);
    }

    public int TotalCardsPlayedByPlayer(int playerId)
    {
        lock (_lock)
            return CardPlayRecords.Count(r => r.PlayerId == playerId);
    }

    public Dictionary<int, int> DamageByPlayer()
    {
        lock (_lock)
            return DamageRecords
                .GroupBy(r => r.SourcePlayerId)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));
    }

    public Dictionary<int, int> BlockByPlayer()
    {
        lock (_lock)
            return BlockRecords
                .GroupBy(r => r.PlayerId)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));
    }

    public Dictionary<int, int> CardsPlayedByPlayer()
    {
        lock (_lock)
            return CardPlayRecords
                .GroupBy(r => r.PlayerId)
                .ToDictionary(g => g.Key, g => g.Count());
    }

    public int? MvpPlayerId()
    {
        var damageByPlayer = DamageByPlayer();
        return damageByPlayer.Count == 0
            ? null
            : damageByPlayer.MaxBy(kvp => kvp.Value).Key;
    }
}
