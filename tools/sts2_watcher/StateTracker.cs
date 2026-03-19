namespace Sts2Watcher;

/// <summary>
/// Tracks game state transitions and produces structured events.
/// Compares current vs previous state each tick to detect changes.
/// </summary>
public class StateTracker
{
    // Previous state
    private bool _wasInRun;
    private bool _wasInCombat;
    private int _lastRound = -1;
    private int _lastSide = -1;
    private int _lastHistoryCount;
    private int _lastFloorCount;
    private int _lastActIndex;
    private Dictionary<string, (int hp, int block)> _lastCreatureState = new();

    // Current run/combat IDs
    private string _currentRunId = "";
    private string _currentCombatId = "";
    private RunStartData? _currentRunInfo;

    // Snapshot state for run start info
    public string CurrentRunId => _currentRunId;
    public string CurrentCombatId => _currentCombatId;
    public RunStartData? CurrentRunInfo => _currentRunInfo;
    public bool WasInRun => _wasInRun;
    public bool WasInCombat => _wasInCombat;
    public int LastFloorCount => _lastFloorCount;

    /// <summary>
    /// Process one tick. Returns a list of events to emit.
    /// </summary>
    public List<(string type, object data)> ProcessTick(
        GameReader reader,
        ulong combatManagerAddr,
        ulong runManagerAddr)
    {
        var events = new List<(string type, object data)>();

        // --- Run lifecycle ---
        bool inRun = runManagerAddr != 0 && reader.ReadRunActive(runManagerAddr);

        if (inRun && !_wasInRun)
        {
            // Run started
            _currentRunId = Guid.NewGuid().ToString("N")[..12];
            _currentRunInfo = reader.ReadRunInfo(runManagerAddr);
            _lastFloorCount = reader.ReadTotalFloorCount(runManagerAddr);
            _lastActIndex = reader.ReadCurrentActIndex(runManagerAddr);

            if (_currentRunInfo != null)
            {
                events.Add(("run_start", _currentRunInfo));

                // Deck snapshot at run start
                var deckSnapshots = reader.ReadDeckSnapshots(runManagerAddr);
                if (deckSnapshots.Count > 0)
                    events.Add(("deck_snapshot", new DeckSnapshotData("run_start", deckSnapshots)));
            }
        }
        else if (!inRun && _wasInRun)
        {
            // Run ended — try to read history
            var runEnd = reader.ReadRunHistory(runManagerAddr);
            if (runEnd != null)
                events.Add(("run_end", runEnd));
            else
                events.Add(("run_end", new RunEndData(false, false, null, null, 0, null)));

            // Reset
            _currentCombatId = "";
            _lastHistoryCount = 0;
            _lastRound = -1;
            _lastSide = -1;
            _lastFloorCount = 0;
            _lastActIndex = 0;
            _lastCreatureState.Clear();
        }

        _wasInRun = inRun;

        // --- Floor tracking (only when run is active) ---
        if (inRun && runManagerAddr != 0)
        {
            int currentFloorCount = reader.ReadTotalFloorCount(runManagerAddr);
            int currentActIndex = reader.ReadCurrentActIndex(runManagerAddr);

            if (currentFloorCount > _lastFloorCount)
            {
                // New floor entered
                // First, emit floor_completed for the previous floor (if there was one)
                if (_lastFloorCount > 0)
                {
                    var completed = reader.ReadLatestFloorStats(runManagerAddr, _lastActIndex, _lastFloorCount);
                    if (completed != null)
                        events.Add(("floor_completed", completed));
                }

                // Then emit floor_entered for the new floor
                var floorData = reader.ReadLatestFloorEntry(runManagerAddr);
                if (floorData != null)
                    events.Add(("floor_entered", floorData));

                // Deck snapshot at floor entry
                var deckSnapshots = reader.ReadDeckSnapshots(runManagerAddr);
                if (deckSnapshots.Count > 0)
                    events.Add(("deck_snapshot", new DeckSnapshotData("floor_entered", deckSnapshots)));

                _lastFloorCount = currentFloorCount;
                _lastActIndex = currentActIndex;
            }
        }

        // --- Combat lifecycle (works independently of run tracking) ---
        if (combatManagerAddr == 0) return events;

        bool inCombat = reader.ReadIsInProgress(combatManagerAddr);

        if (inCombat && !_wasInCombat)
        {
            // Combat started
            _currentCombatId = Guid.NewGuid().ToString("N")[..8];
            _lastHistoryCount = 0;
            _lastRound = -1;
            _lastSide = -1;
            _lastCreatureState.Clear();

            string? encounterId = reader.ReadEncounterId(combatManagerAddr);
            var creatures = reader.ReadCreatureSnapshots(combatManagerAddr);

            events.Add(("combat_start", new CombatStartData(_currentCombatId, encounterId, creatures)));

            // Deck snapshot at combat start
            if (runManagerAddr != 0)
            {
                var deckSnapshots = reader.ReadDeckSnapshots(runManagerAddr);
                if (deckSnapshots.Count > 0)
                    events.Add(("deck_snapshot", new DeckSnapshotData("combat_start", deckSnapshots)));
            }

            // Cache creature state
            CacheCreatureState(creatures);
        }
        else if (!inCombat && _wasInCombat)
        {
            // Combat ended
            var snapshot = reader.ReadCombatSnapshot(combatManagerAddr);
            bool victory = true;
            int finalRound = _lastRound;

            // If all enemies are dead, it's a victory. If players died, it's a loss.
            if (snapshot != null)
            {
                bool allEnemiesDead = snapshot.Enemies.All(e => e.CurrentHp <= 0);
                bool anyPlayerDead = snapshot.Allies.Any(a => a.CurrentHp <= 0);
                victory = allEnemiesDead;
                finalRound = snapshot.Round;
            }

            var finalCreatures = reader.ReadCreatureSnapshots(combatManagerAddr);
            events.Add(("combat_end", new CombatEndData(_currentCombatId, victory, finalRound, finalCreatures)));

            _lastHistoryCount = 0;
            _lastRound = -1;
            _lastSide = -1;
            _lastCreatureState.Clear();
        }

        _wasInCombat = inCombat;

        if (!inCombat) return events;

        // --- Turn tracking ---
        var (currentRound, currentSide) = reader.ReadRoundAndSide(combatManagerAddr);
        if (currentRound >= 0 && (currentRound != _lastRound || currentSide != _lastSide))
        {
            string sideStr = currentSide switch { 1 => "Player", 2 => "Enemy", _ => "None" };
            var creatures = reader.ReadCreatureSnapshots(combatManagerAddr);
            events.Add(("turn_start", new TurnStartData(_currentCombatId, currentRound, sideStr, creatures)));
            CacheCreatureState(creatures);

            _lastRound = currentRound;
            _lastSide = currentSide;
        }

        // --- History entries ---
        int currentCount = reader.ReadHistoryEntryCount(combatManagerAddr);
        if (currentCount > _lastHistoryCount && currentCount > 0)
        {
            var newEvents = reader.ReadStructuredHistoryEntries(combatManagerAddr, _lastHistoryCount);
            foreach (var evt in newEvents)
                events.Add((evt.Type, evt.Data));

            _lastHistoryCount = currentCount;
        }

        // --- Creature snapshot on HP/block change ---
        var currentCreatures = reader.ReadCreatureSnapshots(combatManagerAddr);
        if (HasCreatureStateChanged(currentCreatures))
        {
            events.Add(("creature_snapshot", new CreatureSnapshotData(currentCreatures)));
            CacheCreatureState(currentCreatures);
        }

        return events;
    }

    /// <summary>
    /// Generate a synthetic run_end if the process dies mid-run.
    /// </summary>
    public (string type, object data)? GenerateSyntheticRunEnd()
    {
        if (!_wasInRun) return null;

        _wasInRun = false;
        _wasInCombat = false;
        return ("run_end", new RunEndData(false, true, null, null, 0, null));
    }

    private void CacheCreatureState(List<CreatureSnapshotInfo> creatures)
    {
        _lastCreatureState.Clear();
        foreach (var c in creatures)
            _lastCreatureState[c.Name] = (c.Hp, c.Block);
    }

    private bool HasCreatureStateChanged(List<CreatureSnapshotInfo> current)
    {
        if (_lastCreatureState.Count == 0 && current.Count > 0) return true;
        if (current.Count != _lastCreatureState.Count) return true;

        foreach (var c in current)
        {
            if (!_lastCreatureState.TryGetValue(c.Name, out var prev))
                return true;
            if (prev.hp != c.Hp || prev.block != c.Block)
                return true;
        }

        return false;
    }
}
