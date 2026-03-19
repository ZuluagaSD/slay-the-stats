using SlayTheStats.Models;

namespace SlayTheStats.Core;

public class StatManager
{
    private static StatManager? _instance;
    private static readonly object _initLock = new();

    public static StatManager Instance
    {
        get
        {
            if (_instance is null)
                throw new InvalidOperationException("StatManager not initialized.");
            return _instance;
        }
    }

    public static void EnsureInitialized()
    {
        lock (_initLock)
        {
            _instance ??= new StatManager();
        }
    }

    public RunSession? CurrentRun { get; private set; }
    public CombatSession? CurrentCombat { get; private set; }

    // Events for UI updates
    public event Action<DamageRecord>? OnDamageRecorded;
    public event Action<BlockRecord>? OnBlockRecorded;
    public event Action<CardPlayRecord>? OnCardPlayRecorded;
    public event Action<BuffRecord>? OnBuffRecorded;
    public event Action<DebuffRecord>? OnDebuffRecorded;
    public event Action<EconomyRecord>? OnEconomyRecorded;
    public event Action<CombatSession>? OnCombatStarted;
    public event Action<CombatSession>? OnCombatEnded;
    public event Action<int>? OnTurnChanged;
    public event Action<RunSession>? OnRunStarted;
    public event Action<RunSession>? OnRunEnded;

    public void StartRun()
    {
        CurrentRun = new RunSession();
        MainFile.Log("Run started.");
        OnRunStarted?.Invoke(CurrentRun);
    }

    public void EndRun()
    {
        if (CurrentRun is null) return;
        CurrentRun.End();
        MainFile.Log("Run ended.");
        OnRunEnded?.Invoke(CurrentRun);
    }

    public void StartCombat()
    {
        CurrentCombat = new CombatSession();
        CurrentRun?.AddCombat(CurrentCombat);
        MainFile.Log("Combat started.");
        OnCombatStarted?.Invoke(CurrentCombat);
    }

    public void EndCombat(bool victory)
    {
        if (CurrentCombat is null) return;
        CurrentCombat.End(victory);
        MainFile.Log($"Combat ended. Victory: {victory}");
        OnCombatEnded?.Invoke(CurrentCombat);
        CurrentCombat = null;
    }

    public void SetTurn(int turn)
    {
        CurrentCombat?.SetTurn(turn);
        OnTurnChanged?.Invoke(turn);
    }

    public void IncrementTurn()
    {
        CurrentCombat?.IncrementTurn();
        if (CurrentCombat is not null)
            OnTurnChanged?.Invoke(CurrentCombat.CurrentTurn);
    }

    public void RecordDamage(int sourcePlayerId, int targetId, int amount)
    {
        if (CurrentCombat is null || amount <= 0) return;
        var record = new DamageRecord(sourcePlayerId, targetId, amount,
            CurrentCombat.CurrentTurn, DateTime.UtcNow);
        CurrentCombat.AddDamage(record);
        OnDamageRecorded?.Invoke(record);
    }

    public void RecordBlock(int playerId, int amount)
    {
        if (CurrentCombat is null || amount <= 0) return;
        var record = new BlockRecord(playerId, amount,
            CurrentCombat.CurrentTurn, DateTime.UtcNow);
        CurrentCombat.AddBlock(record);
        OnBlockRecorded?.Invoke(record);
    }

    public void RecordCardPlay(int playerId, string cardId, string cardName, int energyCost, string cardType)
    {
        if (CurrentCombat is null) return;
        var record = new CardPlayRecord(playerId, cardId, cardName, energyCost, cardType,
            CurrentCombat.CurrentTurn, DateTime.UtcNow);
        CurrentCombat.AddCardPlay(record);
        OnCardPlayRecorded?.Invoke(record);
    }

    public void RecordBuff(int playerId, string powerId, string powerName, int stacks)
    {
        if (CurrentCombat is null) return;
        var record = new BuffRecord(playerId, powerId, powerName, stacks,
            CurrentCombat.CurrentTurn, DateTime.UtcNow);
        CurrentCombat.AddBuff(record);
        OnBuffRecorded?.Invoke(record);
    }

    public void RecordDebuff(int targetId, string powerId, string powerName, int stacks, int appliedByPlayerId)
    {
        if (CurrentCombat is null) return;
        var record = new DebuffRecord(targetId, powerId, powerName, stacks, appliedByPlayerId,
            CurrentCombat.CurrentTurn, DateTime.UtcNow);
        CurrentCombat.AddDebuff(record);
        OnDebuffRecorded?.Invoke(record);
    }

    public void RecordEconomy(int playerId, int goldChange)
    {
        if (CurrentCombat is null || goldChange == 0) return;
        var record = new EconomyRecord(playerId, goldChange,
            CurrentCombat.CurrentTurn, DateTime.UtcNow);
        CurrentCombat.AddEconomy(record);
        OnEconomyRecorded?.Invoke(record);
    }

    public void RegisterPlayer(int playerId, string playerName, string characterClass)
    {
        if (CurrentRun is null) return;
        var stats = CurrentRun.GetOrCreatePlayer(playerId);
        stats.PlayerName = playerName;
        stats.CharacterClass = characterClass;
        MainFile.Log($"Player registered: {playerName} ({characterClass}) [ID={playerId}]");
    }
}
