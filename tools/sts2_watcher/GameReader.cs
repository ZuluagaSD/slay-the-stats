using Microsoft.Diagnostics.Runtime;
using System.Diagnostics;

namespace Sts2Watcher;

/// <summary>
/// Reads game state from STS2 process memory using ClrMD.
/// Navigates managed heap via CombatManager.Instance and RunManager.Instance.
/// </summary>
public sealed class GameReader : IDisposable
{
    private const string ProcessName = "SlayTheSpire2";
    private const string Sts2Module = "sts2";
    private const string CombatManagerTypeName = "MegaCrit.Sts2.Core.Combat.CombatManager";
    private const string RunManagerTypeName = "MegaCrit.Sts2.Core.Runs.RunManager";

    private DataTarget? _target;
    private ClrRuntime? _runtime;
    private int _pid;

    public bool IsAttached => _target != null && _runtime != null;

    public bool TryAttach()
    {
        Detach();

        var procs = Process.GetProcessesByName(ProcessName);
        if (procs.Length == 0)
            return false;

        _pid = procs[0].Id;

        try
        {
            _target = DataTarget.AttachToProcess(_pid, suspend: false);
            _runtime = _target.ClrVersions.FirstOrDefault()?.CreateRuntime();
            return _runtime != null;
        }
        catch
        {
            Detach();
            return false;
        }
    }

    public void Detach()
    {
        _runtime = null;
        _target?.Dispose();
        _target = null;
    }

    public void Refresh()
    {
        _runtime?.FlushCachedData();
    }

    public bool IsProcessAlive()
    {
        try
        {
            return Process.GetProcessById(_pid) != null;
        }
        catch
        {
            return false;
        }
    }

    public List<string> GetLoadedModules()
    {
        if (_runtime == null) return new List<string>();
        return _runtime.EnumerateModules()
            .Select(m => m.Name ?? "(null)")
            .ToList();
    }

    // ========== Singleton Finders ==========

    public ulong FindCombatManagerInstance() => FindSingletonInstance(CombatManagerTypeName);

    public ulong FindRunManagerInstance() => FindSingletonInstance(RunManagerTypeName);

    private ulong FindSingletonInstance(string typeName)
    {
        if (_runtime == null) return 0;

        foreach (var module in _runtime.EnumerateModules())
        {
            if (module.Name == null || !module.Name.Contains(Sts2Module, StringComparison.OrdinalIgnoreCase))
                continue;

            var type = module.GetTypeByName(typeName);
            if (type == null) continue;

            var field = type.GetStaticFieldByName("<Instance>k__BackingField");
            if (field != null)
            {
                foreach (var domain in _runtime.AppDomains)
                {
                    var addr = field.ReadObject(domain);
                    if (addr != 0) return addr;
                }
            }
        }

        return FindOnHeap(typeName);
    }

    public ulong FindOnHeap(string typeName)
    {
        if (_runtime == null) return 0;
        var heap = _runtime.Heap;

        foreach (var obj in heap.EnumerateObjects())
        {
            if (obj.Type?.Name == typeName)
                return obj.Address;
        }

        return 0;
    }

    // ========== CombatManager Reading ==========

    public bool ReadIsInProgress(ulong combatManagerAddr)
    {
        if (_runtime == null) return false;
        var heap = _runtime.Heap;
        var obj = heap.GetObject(combatManagerAddr);
        if (!obj.IsValid) return false;

        try
        {
            return obj.ReadField<bool>("<IsInProgress>k__BackingField");
        }
        catch { return false; }
    }

    public (int Round, int Side) ReadRoundAndSide(ulong combatManagerAddr)
    {
        if (_runtime == null) return (-1, -1);
        var heap = _runtime.Heap;
        var obj = heap.GetObject(combatManagerAddr);
        if (!obj.IsValid) return (-1, -1);

        try
        {
            var stateAddr = obj.ReadObjectField("_state");
            if (stateAddr == 0) return (-1, -1);
            var state = heap.GetObject(stateAddr);
            int round = state.ReadField<int>("<RoundNumber>k__BackingField");
            int side = state.ReadField<int>("<CurrentSide>k__BackingField");
            return (round, side);
        }
        catch { return (-1, -1); }
    }

    public int ReadRoundNumber(ulong combatManagerAddr) => ReadRoundAndSide(combatManagerAddr).Round;

    /// <summary>
    /// Read the encounter ID from CombatManager._state.Encounter.
    /// </summary>
    public string? ReadEncounterId(ulong combatManagerAddr)
    {
        if (_runtime == null) return null;
        var heap = _runtime.Heap;

        try
        {
            var cm = heap.GetObject(combatManagerAddr);
            if (!cm.IsValid) return null;

            var stateAddr = cm.ReadObjectField("_state");
            if (stateAddr == 0) return null;
            var state = heap.GetObject(stateAddr);

            var encounterAddr = state.ReadObjectField("_encounter");
            if (encounterAddr == 0) return null;
            var encounter = heap.GetObject(encounterAddr);
            if (!encounter.IsValid) return null;

            return ReadModelIdEntry(heap, encounter);
        }
        catch { return null; }
    }

    public int ReadHistoryEntryCount(ulong combatManagerAddr)
    {
        if (_runtime == null) return -1;

        try
        {
            var (_, size) = GetHistoryEntriesList(combatManagerAddr);
            return size;
        }
        catch { return -1; }
    }

    /// <summary>
    /// Read combat history entries starting from a given index.
    /// Now returns structured event data objects instead of just text descriptions.
    /// </summary>
    public List<StructuredCombatEvent> ReadStructuredHistoryEntries(ulong combatManagerAddr, int startIndex)
    {
        var events = new List<StructuredCombatEvent>();
        if (_runtime == null) return events;

        try
        {
            var (entriesArrayAddr, size) = GetHistoryEntriesList(combatManagerAddr);
            if (entriesArrayAddr == 0 || size <= startIndex) return events;

            var heap = _runtime.Heap;
            var arrayObj = heap.GetObject(entriesArrayAddr);
            if (!arrayObj.IsValid || !arrayObj.IsArray) return events;

            var arr = arrayObj.AsArray();

            for (int i = startIndex; i < size; i++)
            {
                try
                {
                    var entryAddr = arr.GetObjectValue(i);
                    if (entryAddr == 0) continue;

                    var entry = heap.GetObject(entryAddr);
                    if (!entry.IsValid || entry.Type == null) continue;

                    var evt = ParseStructuredHistoryEntry(entry);
                    if (evt != null) events.Add(evt);
                }
                catch { }
            }
        }
        catch { }

        return events;
    }

    /// <summary>
    /// Legacy: Read combat history entries as text (for console display).
    /// </summary>
    public List<CombatEvent> ReadHistoryEntries(ulong combatManagerAddr, int startIndex)
    {
        var events = new List<CombatEvent>();
        if (_runtime == null) return events;

        try
        {
            var (entriesArrayAddr, size) = GetHistoryEntriesList(combatManagerAddr);
            if (entriesArrayAddr == 0 || size <= startIndex) return events;

            var heap = _runtime.Heap;
            var arrayObj = heap.GetObject(entriesArrayAddr);
            if (!arrayObj.IsValid || !arrayObj.IsArray) return events;

            var arr = arrayObj.AsArray();

            for (int i = startIndex; i < size; i++)
            {
                try
                {
                    var entryAddr = arr.GetObjectValue(i);
                    if (entryAddr == 0) continue;

                    var entry = heap.GetObject(entryAddr);
                    if (!entry.IsValid || entry.Type == null) continue;

                    var evt = ParseHistoryEntry(entry);
                    if (evt != null) events.Add(evt);
                }
                catch { }
            }
        }
        catch { }

        return events;
    }

    /// <summary>
    /// Read creature info with powers for detailed snapshots.
    /// </summary>
    public List<CreatureSnapshotInfo> ReadCreatureSnapshots(ulong combatManagerAddr)
    {
        var result = new List<CreatureSnapshotInfo>();
        if (_runtime == null) return result;

        try
        {
            var heap = _runtime.Heap;
            var cm = heap.GetObject(combatManagerAddr);
            if (!cm.IsValid) return result;

            var stateAddr = cm.ReadObjectField("_state");
            if (stateAddr == 0) return result;
            var state = heap.GetObject(stateAddr);
            if (!state.IsValid) return result;

            ReadCreatureSnapshotList(heap, state, "_allies", true, result);
            ReadCreatureSnapshotList(heap, state, "_enemies", false, result);
        }
        catch { }

        return result;
    }

    public CombatSnapshot? ReadCombatSnapshot(ulong combatManagerAddr)
    {
        if (_runtime == null) return null;

        try
        {
            var heap = _runtime.Heap;
            var cm = heap.GetObject(combatManagerAddr);
            if (!cm.IsValid) return null;

            var stateAddr = cm.ReadObjectField("_state");
            if (stateAddr == 0) return null;

            var state = heap.GetObject(stateAddr);
            if (!state.IsValid) return null;

            int round = state.ReadField<int>("<RoundNumber>k__BackingField");

            var allies = ReadCreatureList(heap, state, "_allies");
            var enemies = ReadCreatureList(heap, state, "_enemies");

            return new CombatSnapshot(round, allies, enemies);
        }
        catch { return null; }
    }

    // ========== RunManager Reading ==========

    /// <summary>
    /// Check if a run is active by reading RunManager.State != null.
    /// </summary>
    public bool ReadRunActive(ulong runManagerAddr)
    {
        if (_runtime == null) return false;
        var heap = _runtime.Heap;

        try
        {
            var rm = heap.GetObject(runManagerAddr);
            if (!rm.IsValid) return false;

            var stateAddr = rm.ReadObjectField("<State>k__BackingField");
            return stateAddr != 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// Read run info: seed, ascension, players with character/HP/gold.
    /// </summary>
    public RunStartData? ReadRunInfo(ulong runManagerAddr)
    {
        if (_runtime == null) return null;
        var heap = _runtime.Heap;

        try
        {
            var rm = heap.GetObject(runManagerAddr);
            if (!rm.IsValid) return null;

            var stateAddr = rm.ReadObjectField("<State>k__BackingField");
            if (stateAddr == 0) return null;
            var state = heap.GetObject(stateAddr);
            if (!state.IsValid) return null;

            int ascension = state.ReadField<int>("<AscensionLevel>k__BackingField");

            // Read seed from Rng.StringSeed (the human-readable seed string)
            string seed = "?";
            var rngAddr = state.ReadObjectField("<Rng>k__BackingField");
            if (rngAddr != 0)
            {
                var rng = heap.GetObject(rngAddr);
                if (rng.IsValid)
                    seed = ReadStringField(heap, rng, "<StringSeed>k__BackingField") ?? "?";
            }

            var players = ReadPlayerInfoList(heap, state);

            return new RunStartData(seed, ascension, players);
        }
        catch { return null; }
    }

    /// <summary>
    /// Read the total floor count (sum of all act MapPointHistory counts).
    /// </summary>
    public int ReadTotalFloorCount(ulong runManagerAddr)
    {
        if (_runtime == null) return 0;
        var heap = _runtime.Heap;

        try
        {
            var rm = heap.GetObject(runManagerAddr);
            if (!rm.IsValid) return 0;

            var stateAddr = rm.ReadObjectField("<State>k__BackingField");
            if (stateAddr == 0) return 0;
            var state = heap.GetObject(stateAddr);
            if (!state.IsValid) return 0;

            // _mapPointHistory is List<List<MapPointHistoryEntry>>
            var mphAddr = state.ReadObjectField("_mapPointHistory");
            if (mphAddr == 0) return 0;
            var mph = heap.GetObject(mphAddr);
            if (!mph.IsValid) return 0;

            int outerSize = mph.ReadField<int>("_size");
            var outerItemsAddr = mph.ReadObjectField("_items");
            if (outerItemsAddr == 0 || outerSize <= 0) return 0;

            var outerItems = heap.GetObject(outerItemsAddr);
            if (!outerItems.IsValid || !outerItems.IsArray) return 0;
            var outerArr = outerItems.AsArray();

            int total = 0;
            for (int act = 0; act < outerSize; act++)
            {
                try
                {
                    var innerListAddr = outerArr.GetObjectValue(act);
                    if (innerListAddr == 0) continue;
                    var innerList = heap.GetObject(innerListAddr);
                    if (!innerList.IsValid) continue;
                    total += innerList.ReadField<int>("_size");
                }
                catch { }
            }
            return total;
        }
        catch { return 0; }
    }

    /// <summary>
    /// Read the current act index.
    /// </summary>
    public int ReadCurrentActIndex(ulong runManagerAddr)
    {
        if (_runtime == null) return -1;
        var heap = _runtime.Heap;

        try
        {
            var rm = heap.GetObject(runManagerAddr);
            if (!rm.IsValid) return -1;

            var stateAddr = rm.ReadObjectField("<State>k__BackingField");
            if (stateAddr == 0) return -1;
            var state = heap.GetObject(stateAddr);
            if (!state.IsValid) return -1;

            return state.ReadField<int>("_currentActIndex");
        }
        catch { return -1; }
    }

    /// <summary>
    /// Read the latest MapPointHistoryEntry (last entry of last act's list).
    /// </summary>
    public FloorEnteredData? ReadLatestFloorEntry(ulong runManagerAddr)
    {
        if (_runtime == null) return null;
        var heap = _runtime.Heap;

        try
        {
            var rm = heap.GetObject(runManagerAddr);
            if (!rm.IsValid) return null;

            var stateAddr = rm.ReadObjectField("<State>k__BackingField");
            if (stateAddr == 0) return null;
            var state = heap.GetObject(stateAddr);
            if (!state.IsValid) return null;

            int actIndex = state.ReadField<int>("_currentActIndex");

            // Navigate to _mapPointHistory[actIndex].Last()
            var mphAddr = state.ReadObjectField("_mapPointHistory");
            if (mphAddr == 0) return null;
            var mph = heap.GetObject(mphAddr);
            if (!mph.IsValid) return null;

            int outerSize = mph.ReadField<int>("_size");
            if (outerSize <= 0) return null;

            // Use the last act that has entries
            var outerItemsAddr = mph.ReadObjectField("_items");
            if (outerItemsAddr == 0) return null;
            var outerItems = heap.GetObject(outerItemsAddr);
            if (!outerItems.IsValid || !outerItems.IsArray) return null;
            var outerArr = outerItems.AsArray();

            int targetAct = Math.Min(actIndex, outerSize - 1);
            var innerListAddr = outerArr.GetObjectValue(targetAct);
            if (innerListAddr == 0) return null;
            var innerList = heap.GetObject(innerListAddr);
            if (!innerList.IsValid) return null;

            int innerSize = innerList.ReadField<int>("_size");
            if (innerSize <= 0) return null;

            var innerItemsAddr = innerList.ReadObjectField("_items");
            if (innerItemsAddr == 0) return null;
            var innerItems = heap.GetObject(innerItemsAddr);
            if (!innerItems.IsValid || !innerItems.IsArray) return null;
            var innerArr = innerItems.AsArray();

            // Read last entry
            var entryAddr = innerArr.GetObjectValue(innerSize - 1);
            if (entryAddr == 0) return null;
            var entry = heap.GetObject(entryAddr);
            if (!entry.IsValid) return null;

            // MapPointType enum
            int mapPointType = entry.ReadField<int>("<MapPointType>k__BackingField");
            string mapPointTypeStr = mapPointType switch
            {
                1 => "Unknown",
                2 => "Shop",
                3 => "Treasure",
                4 => "RestSite",
                5 => "Monster",
                6 => "Elite",
                7 => "Boss",
                8 => "Ancient",
                _ => "Unassigned"
            };

            // Read rooms
            var rooms = ReadRoomList(heap, entry);

            // Read player snapshots from PlayerStats
            var playerSnapshots = ReadPlayerSnapshots(heap, entry);

            // Compute total floor
            int totalFloor = 0;
            for (int a = 0; a < outerSize; a++)
            {
                try
                {
                    var ilAddr = outerArr.GetObjectValue(a);
                    if (ilAddr == 0) continue;
                    var il = heap.GetObject(ilAddr);
                    if (il.IsValid) totalFloor += il.ReadField<int>("_size");
                }
                catch { }
            }

            return new FloorEnteredData(actIndex, totalFloor, mapPointTypeStr, rooms, playerSnapshots);
        }
        catch { return null; }
    }

    /// <summary>
    /// Read the latest floor's per-player stats.
    /// </summary>
    public FloorCompletedData? ReadLatestFloorStats(ulong runManagerAddr, int actIndex, int totalFloor)
    {
        if (_runtime == null) return null;
        var heap = _runtime.Heap;

        try
        {
            var rm = heap.GetObject(runManagerAddr);
            if (!rm.IsValid) return null;

            var stateAddr = rm.ReadObjectField("<State>k__BackingField");
            if (stateAddr == 0) return null;
            var state = heap.GetObject(stateAddr);
            if (!state.IsValid) return null;

            var mphAddr = state.ReadObjectField("_mapPointHistory");
            if (mphAddr == 0) return null;
            var mph = heap.GetObject(mphAddr);
            if (!mph.IsValid) return null;

            int outerSize = mph.ReadField<int>("_size");
            if (outerSize <= 0) return null;

            var outerItemsAddr = mph.ReadObjectField("_items");
            if (outerItemsAddr == 0) return null;
            var outerItems = heap.GetObject(outerItemsAddr);
            if (!outerItems.IsValid || !outerItems.IsArray) return null;
            var outerArr = outerItems.AsArray();

            int targetAct = Math.Min(actIndex, outerSize - 1);
            var innerListAddr = outerArr.GetObjectValue(targetAct);
            if (innerListAddr == 0) return null;
            var innerList = heap.GetObject(innerListAddr);
            if (!innerList.IsValid) return null;

            int innerSize = innerList.ReadField<int>("_size");
            if (innerSize <= 0) return null;

            var innerItemsAddr = innerList.ReadObjectField("_items");
            if (innerItemsAddr == 0) return null;
            var innerItems = heap.GetObject(innerItemsAddr);
            if (!innerItems.IsValid || !innerItems.IsArray) return null;
            var innerArr = innerItems.AsArray();

            // Read latest entry's PlayerStats
            var entryAddr = innerArr.GetObjectValue(innerSize - 1);
            if (entryAddr == 0) return null;
            var entry = heap.GetObject(entryAddr);
            if (!entry.IsValid) return null;

            var stats = ReadFloorPlayerStats(heap, entry);
            return new FloorCompletedData(actIndex, totalFloor, stats);
        }
        catch { return null; }
    }

    /// <summary>
    /// Read RunHistory after run ends (if available on RunManager).
    /// </summary>
    public RunEndData? ReadRunHistory(ulong runManagerAddr)
    {
        if (_runtime == null) return null;
        var heap = _runtime.Heap;

        try
        {
            var rm = heap.GetObject(runManagerAddr);
            if (!rm.IsValid) return null;

            bool abandoned = false;
            try { abandoned = rm.ReadField<bool>("<IsAbandoned>k__BackingField"); } catch { }

            // Try to read RunTime from WinTime or _prevRunTime
            float runTime = 0;
            try
            {
                long winTime = rm.ReadField<long>("<WinTime>k__BackingField");
                if (winTime > 0)
                    runTime = winTime;
                else
                {
                    long prevRunTime = rm.ReadField<long>("_prevRunTime");
                    runTime = prevRunTime;
                }
            }
            catch { }

            // Try to read RunHistory if available
            var historyAddr = rm.ReadObjectField("<History>k__BackingField");
            if (historyAddr != 0)
            {
                var history = heap.GetObject(historyAddr);
                if (history.IsValid)
                {
                    try { abandoned = history.ReadField<bool>("<WasAbandoned>k__BackingField"); } catch { }

                    bool win = false;
                    try { win = history.ReadField<bool>("<Win>k__BackingField"); } catch { }

                    float histRunTime = 0;
                    try { histRunTime = history.ReadField<float>("<RunTime>k__BackingField"); } catch { }
                    if (histRunTime > 0) runTime = histRunTime;

                    string? killedByEncounter = ReadModelIdFromField(heap, history, "<KilledByEncounter>k__BackingField");
                    string? killedByEvent = ReadModelIdFromField(heap, history, "<KilledByEvent>k__BackingField");

                    // Read final player state
                    List<PlayerInfo>? finalPlayers = null;
                    try
                    {
                        var stateAddr = rm.ReadObjectField("<State>k__BackingField");
                        if (stateAddr != 0)
                        {
                            var state = heap.GetObject(stateAddr);
                            if (state.IsValid)
                                finalPlayers = ReadPlayerInfoList(heap, state);
                        }
                    }
                    catch { }

                    return new RunEndData(win, abandoned, killedByEncounter, killedByEvent, runTime, finalPlayers);
                }
            }

            // No RunHistory yet — check if game is over (all players dead)
            bool isGameOver = ReadIsGameOver(runManagerAddr);
            return new RunEndData(false, abandoned, null, null, runTime, null);
        }
        catch { return null; }
    }

    private string? ReadModelIdFromField(ClrHeap heap, ClrObject obj, string fieldName)
    {
        try
        {
            var addr = obj.ReadObjectField(fieldName);
            if (addr == 0) return null;
            var mid = heap.GetObject(addr);
            if (!mid.IsValid) return null;
            var entry = ReadStringField(heap, mid, "<Entry>k__BackingField");
            if (entry != null && entry != "" && entry != "NONE") return entry;
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Check if the run is game over (all players dead).
    /// </summary>
    public bool ReadIsGameOver(ulong runManagerAddr)
    {
        if (_runtime == null) return false;
        var heap = _runtime.Heap;

        try
        {
            var rm = heap.GetObject(runManagerAddr);
            if (!rm.IsValid) return false;
            return rm.ReadField<bool>("<IsGameOver>k__BackingField");
        }
        catch
        {
            // IsGameOver is a computed property, not a backing field
            // Try alternative: check if State.IsGameOver
            try
            {
                var rm = heap.GetObject(runManagerAddr);
                if (!rm.IsValid) return false;

                var stateAddr = rm.ReadObjectField("<State>k__BackingField");
                if (stateAddr == 0) return false;
                var state = heap.GetObject(stateAddr);
                if (!state.IsValid) return false;

                // Check all players dead by reading _players list
                var playersAddr = state.ReadObjectField("_players");
                if (playersAddr == 0) return false;
                var players = heap.GetObject(playersAddr);
                if (!players.IsValid) return false;

                int size = players.ReadField<int>("_size");
                if (size <= 0) return false;

                var itemsAddr = players.ReadObjectField("_items");
                if (itemsAddr == 0) return false;
                var items = heap.GetObject(itemsAddr);
                if (!items.IsValid || !items.IsArray) return false;
                var arr = items.AsArray();

                bool allDead = true;
                for (int i = 0; i < size; i++)
                {
                    var playerAddr = arr.GetObjectValue(i);
                    if (playerAddr == 0) continue;
                    var player = heap.GetObject(playerAddr);
                    if (!player.IsValid) continue;

                    var creatureAddr = player.ReadObjectField("<Creature>k__BackingField");
                    if (creatureAddr == 0) continue;
                    var creature = heap.GetObject(creatureAddr);
                    if (!creature.IsValid) continue;

                    int hp = creature.ReadField<int>("_currentHp");
                    if (hp > 0) { allDead = false; break; }
                }
                return allDead;
            }
            catch { return false; }
        }
    }

    /// <summary>
    /// Read deck snapshots for all players in the current run.
    /// </summary>
    public List<PlayerDeckSnapshot> ReadDeckSnapshots(ulong runManagerAddr)
    {
        var result = new List<PlayerDeckSnapshot>();
        if (_runtime == null) return result;
        var heap = _runtime.Heap;

        try
        {
            var rm = heap.GetObject(runManagerAddr);
            if (!rm.IsValid) return result;

            var stateAddr = rm.ReadObjectField("<State>k__BackingField");
            if (stateAddr == 0) return result;
            var state = heap.GetObject(stateAddr);
            if (!state.IsValid) return result;

            var playersAddr = state.ReadObjectField("_players");
            if (playersAddr == 0) return result;
            var players = heap.GetObject(playersAddr);
            if (!players.IsValid) return result;

            int size = players.ReadField<int>("_size");
            var itemsAddr = players.ReadObjectField("_items");
            if (itemsAddr == 0 || size <= 0) return result;
            var items = heap.GetObject(itemsAddr);
            if (!items.IsValid || !items.IsArray) return result;
            var arr = items.AsArray();

            for (int i = 0; i < size; i++)
            {
                try
                {
                    var playerAddr = arr.GetObjectValue(i);
                    if (playerAddr == 0) continue;
                    var player = heap.GetObject(playerAddr);
                    if (!player.IsValid) continue;

                    ulong netId = player.ReadField<ulong>("<NetId>k__BackingField");
                    string character = ReadPlayerCharacterName(heap, player);

                    var deck = ReadPlayerDeck(heap, player);
                    var relics = ReadPlayerRelics(heap, player);
                    var potions = ReadPlayerPotions(heap, player);

                    result.Add(new PlayerDeckSnapshot(netId, character, deck, relics, potions));
                }
                catch { }
            }
        }
        catch { }

        return result;
    }

    // ========== Internal Helpers ==========

    private List<PlayerInfo> ReadPlayerInfoList(ClrHeap heap, ClrObject runState)
    {
        var result = new List<PlayerInfo>();

        try
        {
            var playersAddr = runState.ReadObjectField("_players");
            if (playersAddr == 0) return result;
            var players = heap.GetObject(playersAddr);
            if (!players.IsValid) return result;

            int size = players.ReadField<int>("_size");
            var itemsAddr = players.ReadObjectField("_items");
            if (itemsAddr == 0 || size <= 0) return result;
            var items = heap.GetObject(itemsAddr);
            if (!items.IsValid || !items.IsArray) return result;
            var arr = items.AsArray();

            for (int i = 0; i < size; i++)
            {
                try
                {
                    var playerAddr = arr.GetObjectValue(i);
                    if (playerAddr == 0) continue;
                    var player = heap.GetObject(playerAddr);
                    if (!player.IsValid) continue;

                    ulong netId = player.ReadField<ulong>("<NetId>k__BackingField");
                    int gold = player.ReadField<int>("_gold");
                    int maxEnergy = player.ReadField<int>("<MaxEnergy>k__BackingField");
                    string character = ReadPlayerCharacterName(heap, player);

                    var creatureAddr = player.ReadObjectField("<Creature>k__BackingField");
                    int hp = 0, maxHp = 0;
                    if (creatureAddr != 0)
                    {
                        var creature = heap.GetObject(creatureAddr);
                        if (creature.IsValid)
                        {
                            hp = creature.ReadField<int>("_currentHp");
                            maxHp = creature.ReadField<int>("_maxHp");
                        }
                    }

                    result.Add(new PlayerInfo(netId, character, hp, maxHp, gold, maxEnergy));
                }
                catch { }
            }
        }
        catch { }

        return result;
    }

    private string ReadPlayerCharacterName(ClrHeap heap, ClrObject player)
    {
        try
        {
            var charAddr = player.ReadObjectField("<Character>k__BackingField");
            if (charAddr == 0) return "?";
            var character = heap.GetObject(charAddr);
            if (!character.IsValid) return "?";
            return ReadModelIdEntry(heap, character);
        }
        catch { return "?"; }
    }

    private List<CardInfo> ReadPlayerDeck(ClrHeap heap, ClrObject player)
    {
        var result = new List<CardInfo>();
        try
        {
            // Player.Deck is a CardPile, CardPile._cards is List<CardModel>
            var deckAddr = player.ReadObjectField("<Deck>k__BackingField");
            if (deckAddr == 0) return result;
            var deck = heap.GetObject(deckAddr);
            if (!deck.IsValid) return result;

            var cardsAddr = deck.ReadObjectField("_cards");
            if (cardsAddr == 0) return result;
            var cards = heap.GetObject(cardsAddr);
            if (!cards.IsValid) return result;

            int size = cards.ReadField<int>("_size");
            var itemsAddr = cards.ReadObjectField("_items");
            if (itemsAddr == 0 || size <= 0) return result;
            var items = heap.GetObject(itemsAddr);
            if (!items.IsValid || !items.IsArray) return result;
            var arr = items.AsArray();

            for (int i = 0; i < size; i++)
            {
                try
                {
                    var cardAddr = arr.GetObjectValue(i);
                    if (cardAddr == 0) continue;
                    var card = heap.GetObject(cardAddr);
                    if (!card.IsValid) continue;

                    string id = ReadModelIdEntry(heap, card);
                    // Try to read upgrade level
                    int upgradeLevel = 0;
                    try { upgradeLevel = card.ReadField<int>("<CurrentUpgradeLevel>k__BackingField"); } catch { }

                    result.Add(new CardInfo(id, upgradeLevel));
                }
                catch { }
            }
        }
        catch { }

        return result;
    }

    private List<string> ReadPlayerRelics(ClrHeap heap, ClrObject player)
    {
        var result = new List<string>();
        try
        {
            var relicsAddr = player.ReadObjectField("_relics");
            if (relicsAddr == 0) return result;
            var relics = heap.GetObject(relicsAddr);
            if (!relics.IsValid) return result;

            int size = relics.ReadField<int>("_size");
            var itemsAddr = relics.ReadObjectField("_items");
            if (itemsAddr == 0 || size <= 0) return result;
            var items = heap.GetObject(itemsAddr);
            if (!items.IsValid || !items.IsArray) return result;
            var arr = items.AsArray();

            for (int i = 0; i < size; i++)
            {
                try
                {
                    var relicAddr = arr.GetObjectValue(i);
                    if (relicAddr == 0) continue;
                    var relic = heap.GetObject(relicAddr);
                    if (!relic.IsValid) continue;
                    result.Add(ReadModelIdEntry(heap, relic));
                }
                catch { }
            }
        }
        catch { }

        return result;
    }

    private List<string> ReadPlayerPotions(ClrHeap heap, ClrObject player)
    {
        var result = new List<string>();
        try
        {
            var potionsAddr = player.ReadObjectField("_potionSlots");
            if (potionsAddr == 0) return result;
            var potions = heap.GetObject(potionsAddr);
            if (!potions.IsValid) return result;

            int size = potions.ReadField<int>("_size");
            var itemsAddr = potions.ReadObjectField("_items");
            if (itemsAddr == 0 || size <= 0) return result;
            var items = heap.GetObject(itemsAddr);
            if (!items.IsValid || !items.IsArray) return result;
            var arr = items.AsArray();

            for (int i = 0; i < size; i++)
            {
                try
                {
                    var potionAddr = arr.GetObjectValue(i);
                    if (potionAddr == 0) { result.Add("empty"); continue; }
                    var potion = heap.GetObject(potionAddr);
                    if (!potion.IsValid) { result.Add("empty"); continue; }
                    result.Add(ReadModelIdEntry(heap, potion));
                }
                catch { result.Add("empty"); }
            }
        }
        catch { }

        return result;
    }

    private List<RoomInfo>? ReadRoomList(ClrHeap heap, ClrObject mapPointEntry)
    {
        try
        {
            var roomsAddr = mapPointEntry.ReadObjectField("<Rooms>k__BackingField");
            if (roomsAddr == 0) return null;
            var rooms = heap.GetObject(roomsAddr);
            if (!rooms.IsValid) return null;

            int size = rooms.ReadField<int>("_size");
            if (size <= 0) return null;
            var itemsAddr = rooms.ReadObjectField("_items");
            if (itemsAddr == 0) return null;
            var items = heap.GetObject(itemsAddr);
            if (!items.IsValid || !items.IsArray) return null;
            var arr = items.AsArray();

            var result = new List<RoomInfo>();
            for (int i = 0; i < size; i++)
            {
                try
                {
                    var roomAddr = arr.GetObjectValue(i);
                    if (roomAddr == 0) continue;
                    var room = heap.GetObject(roomAddr);
                    if (!room.IsValid) continue;

                    int roomType = room.ReadField<int>("<RoomType>k__BackingField");
                    string roomTypeStr = roomType switch
                    {
                        1 => "Monster",
                        2 => "Elite",
                        3 => "Boss",
                        4 => "Treasure",
                        5 => "Shop",
                        6 => "Event",
                        7 => "RestSite",
                        8 => "Map",
                        _ => "Unassigned"
                    };

                    string? modelId = null;
                    var midAddr = room.ReadObjectField("<ModelId>k__BackingField");
                    if (midAddr != 0)
                    {
                        var mid = heap.GetObject(midAddr);
                        if (mid.IsValid)
                        {
                            var entry = ReadStringField(heap, mid, "<Entry>k__BackingField");
                            if (entry != null && entry != "") modelId = entry;
                        }
                    }

                    // Read monster IDs
                    List<string>? monsterIds = null;
                    var midsAddr = room.ReadObjectField("<MonsterIds>k__BackingField");
                    if (midsAddr != 0)
                    {
                        var mids = heap.GetObject(midsAddr);
                        if (mids.IsValid)
                        {
                            int msize = mids.ReadField<int>("_size");
                            if (msize > 0)
                            {
                                var mItemsAddr = mids.ReadObjectField("_items");
                                if (mItemsAddr != 0)
                                {
                                    var mItems = heap.GetObject(mItemsAddr);
                                    if (mItems.IsValid && mItems.IsArray)
                                    {
                                        var mArr = mItems.AsArray();
                                        monsterIds = new List<string>();
                                        for (int j = 0; j < msize; j++)
                                        {
                                            try
                                            {
                                                var idAddr = mArr.GetObjectValue(j);
                                                if (idAddr == 0) continue;
                                                var id = heap.GetObject(idAddr);
                                                if (!id.IsValid) continue;
                                                var e = ReadStringField(heap, id, "<Entry>k__BackingField");
                                                if (e != null) monsterIds.Add(e);
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    result.Add(new RoomInfo(roomTypeStr, modelId, monsterIds));
                }
                catch { }
            }

            return result.Count > 0 ? result : null;
        }
        catch { return null; }
    }

    private List<PlayerSnapshotInfo>? ReadPlayerSnapshots(ClrHeap heap, ClrObject mapPointEntry)
    {
        try
        {
            var statsAddr = mapPointEntry.ReadObjectField("<PlayerStats>k__BackingField");
            if (statsAddr == 0) return null;
            var stats = heap.GetObject(statsAddr);
            if (!stats.IsValid) return null;

            int size = stats.ReadField<int>("_size");
            if (size <= 0) return null;
            var itemsAddr = stats.ReadObjectField("_items");
            if (itemsAddr == 0) return null;
            var items = heap.GetObject(itemsAddr);
            if (!items.IsValid || !items.IsArray) return null;
            var arr = items.AsArray();

            var result = new List<PlayerSnapshotInfo>();
            for (int i = 0; i < size; i++)
            {
                try
                {
                    var entryAddr = arr.GetObjectValue(i);
                    if (entryAddr == 0) continue;
                    var entry = heap.GetObject(entryAddr);
                    if (!entry.IsValid) continue;

                    ulong playerId = entry.ReadField<ulong>("<PlayerId>k__BackingField");
                    int currentHp = entry.ReadField<int>("<CurrentHp>k__BackingField");
                    int maxHp = entry.ReadField<int>("<MaxHp>k__BackingField");
                    int currentGold = entry.ReadField<int>("<CurrentGold>k__BackingField");
                    // Deck size isn't directly on the entry, so we set 0
                    result.Add(new PlayerSnapshotInfo(playerId, currentHp, maxHp, currentGold, 0));
                }
                catch { }
            }

            return result.Count > 0 ? result : null;
        }
        catch { return null; }
    }

    private List<FloorPlayerStats>? ReadFloorPlayerStats(ClrHeap heap, ClrObject mapPointEntry)
    {
        try
        {
            var statsAddr = mapPointEntry.ReadObjectField("<PlayerStats>k__BackingField");
            if (statsAddr == 0) return null;
            var stats = heap.GetObject(statsAddr);
            if (!stats.IsValid) return null;

            int size = stats.ReadField<int>("_size");
            if (size <= 0) return null;
            var itemsAddr = stats.ReadObjectField("_items");
            if (itemsAddr == 0) return null;
            var items = heap.GetObject(itemsAddr);
            if (!items.IsValid || !items.IsArray) return null;
            var arr = items.AsArray();

            var result = new List<FloorPlayerStats>();
            for (int i = 0; i < size; i++)
            {
                try
                {
                    var entryAddr = arr.GetObjectValue(i);
                    if (entryAddr == 0) continue;
                    var entry = heap.GetObject(entryAddr);
                    if (!entry.IsValid) continue;

                    ulong playerId = entry.ReadField<ulong>("<PlayerId>k__BackingField");
                    int goldGained = entry.ReadField<int>("<GoldGained>k__BackingField");
                    int goldSpent = entry.ReadField<int>("<GoldSpent>k__BackingField");
                    int currentGold = entry.ReadField<int>("<CurrentGold>k__BackingField");
                    int currentHp = entry.ReadField<int>("<CurrentHp>k__BackingField");
                    int maxHp = entry.ReadField<int>("<MaxHp>k__BackingField");
                    int damageTaken = entry.ReadField<int>("<DamageTaken>k__BackingField");
                    int hpHealed = entry.ReadField<int>("<HpHealed>k__BackingField");

                    result.Add(new FloorPlayerStats(playerId, goldGained, goldSpent, currentGold, currentHp, maxHp, damageTaken, hpHealed));
                }
                catch { }
            }

            return result.Count > 0 ? result : null;
        }
        catch { return null; }
    }

    private void ReadCreatureSnapshotList(ClrHeap heap, ClrObject state, string fieldName, bool isPlayerSide, List<CreatureSnapshotInfo> result)
    {
        try
        {
            var listAddr = state.ReadObjectField(fieldName);
            if (listAddr == 0) return;
            var list = heap.GetObject(listAddr);
            if (!list.IsValid) return;

            int size = list.ReadField<int>("_size");
            var itemsAddr = list.ReadObjectField("_items");
            if (itemsAddr == 0 || size <= 0) return;
            var items = heap.GetObject(itemsAddr);
            if (!items.IsValid || !items.IsArray) return;
            var arr = items.AsArray();

            for (int i = 0; i < size; i++)
            {
                try
                {
                    var creatureAddr = arr.GetObjectValue(i);
                    if (creatureAddr == 0) continue;
                    var creature = heap.GetObject(creatureAddr);
                    if (!creature.IsValid) continue;

                    int hp = creature.ReadField<int>("_currentHp");
                    int maxHp = creature.ReadField<int>("_maxHp");
                    int block = creature.ReadField<int>("_block");
                    string name = ReadCreatureName(heap, creature);
                    bool isAlive = hp > 0;

                    // Read powers
                    var powers = ReadCreaturePowers(heap, creature);

                    result.Add(new CreatureSnapshotInfo(name, hp, maxHp, block, isPlayerSide, isAlive, powers));
                }
                catch { }
            }
        }
        catch { }
    }

    private List<PowerInfo>? ReadCreaturePowers(ClrHeap heap, ClrObject creature)
    {
        try
        {
            var powersAddr = creature.ReadObjectField("_powers");
            if (powersAddr == 0) return null;
            var powers = heap.GetObject(powersAddr);
            if (!powers.IsValid) return null;

            int size = powers.ReadField<int>("_size");
            if (size <= 0) return null;
            var itemsAddr = powers.ReadObjectField("_items");
            if (itemsAddr == 0) return null;
            var items = heap.GetObject(itemsAddr);
            if (!items.IsValid || !items.IsArray) return null;
            var arr = items.AsArray();

            var result = new List<PowerInfo>();
            for (int i = 0; i < size; i++)
            {
                try
                {
                    var powerAddr = arr.GetObjectValue(i);
                    if (powerAddr == 0) continue;
                    var power = heap.GetObject(powerAddr);
                    if (!power.IsValid) continue;

                    string id = ReadModelIdEntry(heap, power);
                    int amount = power.ReadField<int>("_amount");
                    result.Add(new PowerInfo(id, amount));
                }
                catch { }
            }

            return result.Count > 0 ? result : null;
        }
        catch { return null; }
    }

    // ========== History Entry Parsing ==========

    private (ulong arrayAddr, int size) GetHistoryEntriesList(ulong combatManagerAddr)
    {
        var heap = _runtime!.Heap;
        var cm = heap.GetObject(combatManagerAddr);
        if (!cm.IsValid) return (0, -1);

        var historyAddr = cm.ReadObjectField("<History>k__BackingField");
        if (historyAddr == 0) return (0, -1);
        var history = heap.GetObject(historyAddr);
        if (!history.IsValid) return (0, -1);

        var entriesAddr = history.ReadObjectField("_entries");
        if (entriesAddr == 0) return (0, -1);
        var entries = heap.GetObject(entriesAddr);
        if (!entries.IsValid) return (0, -1);

        int size = entries.ReadField<int>("_size");
        var itemsAddr = entries.ReadObjectField("_items");

        return (itemsAddr, size);
    }

    // --- Common entry fields ---

    private (int round, string side, string actorName) ReadEntryCommon(ClrHeap heap, ClrObject entry)
    {
        int round = entry.ReadField<int>("<RoundNumber>k__BackingField");
        int side = entry.ReadField<int>("<CurrentSide>k__BackingField");
        string sideStr = side switch { 1 => "Player", 2 => "Enemy", _ => "None" };

        var actorAddr = entry.ReadObjectField("<Actor>k__BackingField");
        string actorName = "Unknown";
        if (actorAddr != 0)
        {
            var actor = heap.GetObject(actorAddr);
            if (actor.IsValid) actorName = ReadCreatureName(heap, actor);
        }

        return (round, sideStr, actorName);
    }

    /// <summary>
    /// Parse a history entry into a structured event with typed data.
    /// Handles all 17 combat history entry types.
    /// </summary>
    private StructuredCombatEvent? ParseStructuredHistoryEntry(ClrObject entry)
    {
        var heap = _runtime!.Heap;
        string typeName = entry.Type!.Name ?? "";
        var (round, side, actor) = ReadEntryCommon(heap, entry);

        try
        {
            if (typeName.Contains("CardPlayFinishedEntry"))
            {
                var cardPlayAddr = entry.ReadObjectField("<CardPlay>k__BackingField");
                if (cardPlayAddr == 0) return null;
                var cardPlay = heap.GetObject(cardPlayAddr);
                if (!cardPlay.IsValid) return null;

                var cardAddr = cardPlay.ReadObjectField("<Card>k__BackingField");
                if (cardAddr == 0) return null;
                var card = heap.GetObject(cardAddr);
                if (!card.IsValid) return null;

                string cardId = ReadModelIdEntry(heap, card);
                bool wasEthereal = false;
                try
                {
                    wasEthereal = entry.ReadField<bool>("<WasEthereal>k__BackingField");
                }
                catch { }

                string? target = null;
                var targetAddr = cardPlay.ReadObjectField("<Target>k__BackingField");
                if (targetAddr != 0)
                {
                    var t = heap.GetObject(targetAddr);
                    if (t.IsValid) target = ReadCreatureName(heap, t);
                }

                return new StructuredCombatEvent("card_played",
                    new CardPlayedData(round, side, actor, cardId, target, wasEthereal));
            }
            else if (typeName.Contains("CardPlayStartedEntry"))
            {
                return null; // Skip, we track finished
            }
            else if (typeName.Contains("CardDrawnEntry"))
            {
                var cardAddr = entry.ReadObjectField("<Card>k__BackingField");
                if (cardAddr == 0) return null;
                var card = heap.GetObject(cardAddr);
                if (!card.IsValid) return null;
                string cardId = ReadModelIdEntry(heap, card);
                bool fromHandDraw = false;
                try { fromHandDraw = entry.ReadField<bool>("<FromHandDraw>k__BackingField"); } catch { }
                return new StructuredCombatEvent("card_drawn",
                    new CardDrawnData(round, side, actor, cardId, fromHandDraw));
            }
            else if (typeName.Contains("CardDiscardedEntry"))
            {
                var cardAddr = entry.ReadObjectField("<Card>k__BackingField");
                if (cardAddr == 0) return null;
                var card = heap.GetObject(cardAddr);
                if (!card.IsValid) return null;
                string cardId = ReadModelIdEntry(heap, card);
                return new StructuredCombatEvent("card_discarded",
                    new CardDiscardedData(round, side, actor, cardId));
            }
            else if (typeName.Contains("CardExhaustedEntry"))
            {
                var cardAddr = entry.ReadObjectField("<Card>k__BackingField");
                if (cardAddr == 0) return null;
                var card = heap.GetObject(cardAddr);
                if (!card.IsValid) return null;
                string cardId = ReadModelIdEntry(heap, card);
                return new StructuredCombatEvent("card_exhausted",
                    new CardExhaustedData(round, side, actor, cardId));
            }
            else if (typeName.Contains("CardGeneratedEntry"))
            {
                var cardAddr = entry.ReadObjectField("<Card>k__BackingField");
                if (cardAddr == 0) return null;
                var card = heap.GetObject(cardAddr);
                if (!card.IsValid) return null;
                string cardId = ReadModelIdEntry(heap, card);
                bool genByPlayer = false;
                try { genByPlayer = entry.ReadField<bool>("<GeneratedByPlayer>k__BackingField"); } catch { }
                return new StructuredCombatEvent("card_generated",
                    new CardGeneratedData(round, side, actor, cardId, genByPlayer));
            }
            else if (typeName.Contains("CardAfflictedEntry"))
            {
                var cardAddr = entry.ReadObjectField("<Card>k__BackingField");
                if (cardAddr == 0) return null;
                var card = heap.GetObject(cardAddr);
                if (!card.IsValid) return null;
                string cardId = ReadModelIdEntry(heap, card);

                string afflictionId = "?";
                var affAddr = entry.ReadObjectField("<Affliction>k__BackingField");
                if (affAddr != 0)
                {
                    var aff = heap.GetObject(affAddr);
                    if (aff.IsValid) afflictionId = ReadModelIdEntry(heap, aff);
                }
                return new StructuredCombatEvent("card_afflicted",
                    new CardAfflictedData(round, side, actor, cardId, afflictionId));
            }
            else if (typeName.Contains("DamageReceivedEntry"))
            {
                var resultAddr = entry.ReadObjectField("<Result>k__BackingField");
                if (resultAddr == 0) return null;
                var result = heap.GetObject(resultAddr);
                if (!result.IsValid) return null;

                int unblocked = result.ReadField<int>("<UnblockedDamage>k__BackingField");
                int blocked = result.ReadField<int>("<BlockedDamage>k__BackingField");
                int overkill = 0;
                try { overkill = result.ReadField<int>("<OverkillDamage>k__BackingField"); } catch { }
                bool killed = result.ReadField<bool>("<WasTargetKilled>k__BackingField");
                bool blockBroken = false;
                try { blockBroken = result.ReadField<bool>("<WasBlockBroken>k__BackingField"); } catch { }

                string? dealer = null;
                var dealerAddr = entry.ReadObjectField("<Dealer>k__BackingField");
                if (dealerAddr != 0)
                {
                    var d = heap.GetObject(dealerAddr);
                    if (d.IsValid) dealer = ReadCreatureName(heap, d);
                }

                string? cardSource = null;
                var csAddr = entry.ReadObjectField("<CardSource>k__BackingField");
                if (csAddr != 0)
                {
                    var cs = heap.GetObject(csAddr);
                    if (cs.IsValid) cardSource = ReadModelIdEntry(heap, cs);
                }

                return new StructuredCombatEvent("damage_received",
                    new DamageReceivedData(round, side, actor, dealer, cardSource, unblocked, blocked, overkill, killed, blockBroken));
            }
            else if (typeName.Contains("CreatureAttackedEntry"))
            {
                int hitCount = 0;
                try
                {
                    var drsAddr = entry.ReadObjectField("<DamageResults>k__BackingField");
                    if (drsAddr != 0)
                    {
                        var drs = heap.GetObject(drsAddr);
                        if (drs.IsValid)
                            hitCount = drs.ReadField<int>("_size");
                    }
                }
                catch { }
                return new StructuredCombatEvent("creature_attacked",
                    new CreatureAttackedData(round, side, actor, hitCount));
            }
            else if (typeName.Contains("MonsterPerformedMoveEntry"))
            {
                string monsterId = actor;
                var monsterAddr = entry.ReadObjectField("<Monster>k__BackingField");
                if (monsterAddr != 0)
                {
                    var monster = heap.GetObject(monsterAddr);
                    if (monster.IsValid) monsterId = ReadModelIdEntry(heap, monster);
                }

                string moveId = "unknown";
                var moveAddr = entry.ReadObjectField("<Move>k__BackingField");
                if (moveAddr != 0)
                {
                    var move = heap.GetObject(moveAddr);
                    if (move.IsValid)
                    {
                        moveId = ReadStringField(heap, move, "<StateId>k__BackingField")
                              ?? ReadStringField(heap, move, "StateId")
                              ?? "unknown";
                    }
                }

                List<string>? targets = null;
                var targetsAddr = entry.ReadObjectField("<Targets>k__BackingField");
                if (targetsAddr != 0)
                {
                    var targetsObj = heap.GetObject(targetsAddr);
                    if (targetsObj.IsValid)
                    {
                        targets = ReadCreatureNameList(heap, targetsObj);
                    }
                }

                return new StructuredCombatEvent("monster_move",
                    new MonsterMoveData(round, side, monsterId, moveId, targets));
            }
            else if (typeName.Contains("BlockGainedEntry"))
            {
                int amount = entry.ReadField<int>("<Amount>k__BackingField");
                string? cardSource = null;
                var cpAddr = entry.ReadObjectField("<CardPlay>k__BackingField");
                if (cpAddr != 0)
                {
                    var cp = heap.GetObject(cpAddr);
                    if (cp.IsValid)
                    {
                        var cardAddr = cp.ReadObjectField("<Card>k__BackingField");
                        if (cardAddr != 0)
                        {
                            var card = heap.GetObject(cardAddr);
                            if (card.IsValid) cardSource = ReadModelIdEntry(heap, card);
                        }
                    }
                }
                return new StructuredCombatEvent("block_gained",
                    new BlockGainedData(round, side, actor, amount, cardSource));
            }
            else if (typeName.Contains("EnergySpentEntry"))
            {
                int amount = entry.ReadField<int>("<Amount>k__BackingField");
                return new StructuredCombatEvent("energy_spent",
                    new EnergySpentData(round, side, actor, amount));
            }
            else if (typeName.Contains("PowerReceivedEntry"))
            {
                string powerId = "?";
                var powerAddr = entry.ReadObjectField("<Power>k__BackingField");
                if (powerAddr != 0)
                {
                    var power = heap.GetObject(powerAddr);
                    if (power.IsValid) powerId = ReadModelIdEntry(heap, power);
                }

                // PowerReceivedEntry.Amount is decimal in the game
                decimal amount = 0;
                try { amount = entry.ReadField<decimal>("<Amount>k__BackingField"); } catch { }

                string? applier = null;
                var applierAddr = entry.ReadObjectField("<Applier>k__BackingField");
                if (applierAddr != 0)
                {
                    var a = heap.GetObject(applierAddr);
                    if (a.IsValid) applier = ReadCreatureName(heap, a);
                }

                return new StructuredCombatEvent("power_received",
                    new PowerReceivedData(round, side, actor, powerId, amount, applier));
            }
            else if (typeName.Contains("PotionUsedEntry"))
            {
                string potionId = "?";
                var potionAddr = entry.ReadObjectField("<Potion>k__BackingField");
                if (potionAddr != 0)
                {
                    var potion = heap.GetObject(potionAddr);
                    if (potion.IsValid) potionId = ReadModelIdEntry(heap, potion);
                }

                string? target = null;
                var targetAddr = entry.ReadObjectField("<Target>k__BackingField");
                if (targetAddr != 0)
                {
                    var t = heap.GetObject(targetAddr);
                    if (t.IsValid) target = ReadCreatureName(heap, t);
                }

                return new StructuredCombatEvent("potion_used",
                    new PotionUsedData(round, side, actor, potionId, target));
            }
            else if (typeName.Contains("OrbChanneledEntry"))
            {
                string orbId = "?";
                var orbAddr = entry.ReadObjectField("<Orb>k__BackingField");
                if (orbAddr != 0)
                {
                    var orb = heap.GetObject(orbAddr);
                    if (orb.IsValid) orbId = ReadModelIdEntry(heap, orb);
                }
                return new StructuredCombatEvent("orb_channeled",
                    new OrbChanneledData(round, side, actor, orbId));
            }
            else if (typeName.Contains("StarsModifiedEntry"))
            {
                int amount = entry.ReadField<int>("<Amount>k__BackingField");
                return new StructuredCombatEvent("stars_modified",
                    new StarsModifiedData(round, side, actor, amount));
            }
            else if (typeName.Contains("SummonedEntry"))
            {
                int amount = entry.ReadField<int>("<Amount>k__BackingField");
                return new StructuredCombatEvent("summoned",
                    new SummonedData(round, side, actor, amount));
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Legacy parser for console display (returns text descriptions).
    /// </summary>
    private CombatEvent? ParseHistoryEntry(ClrObject entry)
    {
        var heap = _runtime!.Heap;
        string typeName = entry.Type!.Name ?? "";
        var (round, side, actor) = ReadEntryCommon(heap, entry);

        if (typeName.Contains("CardPlayFinishedEntry"))
            return ParseCardPlayFinished(heap, entry, round, side, actor);
        else if (typeName.Contains("CardPlayStartedEntry"))
            return null;
        else if (typeName.Contains("DamageReceivedEntry"))
            return ParseDamageReceived(heap, entry, round, side, actor);
        else if (typeName.Contains("CreatureAttackedEntry"))
            return new CombatEvent(CombatEventType.Attack, round, side, $"{actor} attacked");
        else if (typeName.Contains("MonsterPerformedMoveEntry"))
            return ParseMonsterMove(heap, entry, round, side, actor);
        else if (typeName.Contains("BlockGainedEntry"))
            return ParseBlockGained(heap, entry, round, side, actor);
        else if (typeName.Contains("CardDrawnEntry"))
            return ParseCardDrawn(heap, entry, round, side, actor);
        else if (typeName.Contains("EnergySpentEntry"))
            return ParseEnergySpent(heap, entry, round, side, actor);
        else if (typeName.Contains("CardDiscardedEntry"))
            return ParseCardSimple(heap, entry, CombatEventType.CardDiscarded, "discarded", actor, round, side);
        else if (typeName.Contains("CardExhaustedEntry"))
            return ParseCardSimple(heap, entry, CombatEventType.CardExhausted, "exhausted", actor, round, side);
        else if (typeName.Contains("CardGeneratedEntry"))
            return ParseCardSimple(heap, entry, CombatEventType.CardGenerated, "generated", actor, round, side);
        else if (typeName.Contains("CardAfflictedEntry"))
            return ParseCardAfflicted(heap, entry, round, side, actor);
        else if (typeName.Contains("PowerReceivedEntry"))
            return ParsePowerReceived(heap, entry, round, side, actor);
        else if (typeName.Contains("PotionUsedEntry"))
            return ParsePotionUsed(heap, entry, round, side, actor);
        else if (typeName.Contains("OrbChanneledEntry"))
            return ParseOrbChanneled(heap, entry, round, side, actor);
        else if (typeName.Contains("StarsModifiedEntry"))
        {
            int amount = entry.ReadField<int>("<Amount>k__BackingField");
            return new CombatEvent(CombatEventType.StarsModified, round, side, $"{actor} {(amount < 0 ? "lost" : "gained")} {Math.Abs(amount)} star(s)");
        }
        else if (typeName.Contains("SummonedEntry"))
        {
            int amount = entry.ReadField<int>("<Amount>k__BackingField");
            return new CombatEvent(CombatEventType.Summoned, round, side, $"{actor} summoned {amount}");
        }

        return null;
    }

    private CombatEvent? ParseCardPlayFinished(ClrHeap heap, ClrObject entry, int round, string side, string actor)
    {
        try
        {
            var cardPlayAddr = entry.ReadObjectField("<CardPlay>k__BackingField");
            if (cardPlayAddr == 0) return null;
            var cardPlay = heap.GetObject(cardPlayAddr);
            if (!cardPlay.IsValid) return null;

            var cardAddr = cardPlay.ReadObjectField("<Card>k__BackingField");
            if (cardAddr == 0) return null;
            var card = heap.GetObject(cardAddr);
            if (!card.IsValid) return null;

            string cardId = ReadModelIdEntry(heap, card);
            string? targetName = null;
            var targetAddr = cardPlay.ReadObjectField("<Target>k__BackingField");
            if (targetAddr != 0)
            {
                var target = heap.GetObject(targetAddr);
                if (target.IsValid) targetName = ReadCreatureName(heap, target);
            }

            string desc = targetName != null
                ? $"{actor} played {cardId} targeting {targetName}"
                : $"{actor} played {cardId}";

            return new CombatEvent(CombatEventType.CardPlayed, round, side, desc);
        }
        catch { return null; }
    }

    private CombatEvent? ParseDamageReceived(ClrHeap heap, ClrObject entry, int round, string side, string receiver)
    {
        try
        {
            var resultAddr = entry.ReadObjectField("<Result>k__BackingField");
            if (resultAddr == 0) return null;
            var result = heap.GetObject(resultAddr);
            if (!result.IsValid) return null;

            int unblocked = result.ReadField<int>("<UnblockedDamage>k__BackingField");
            int blocked = result.ReadField<int>("<BlockedDamage>k__BackingField");
            bool killed = result.ReadField<bool>("<WasTargetKilled>k__BackingField");

            string dealerName = "unknown source";
            var dealerAddr = entry.ReadObjectField("<Dealer>k__BackingField");
            if (dealerAddr != 0)
            {
                var dealer = heap.GetObject(dealerAddr);
                if (dealer.IsValid) dealerName = ReadCreatureName(heap, dealer);
            }

            string desc;
            if (unblocked > 0 && blocked > 0)
                desc = $"{dealerName} dealt {unblocked} damage to {receiver} ({blocked} blocked)";
            else if (unblocked > 0)
                desc = $"{dealerName} dealt {unblocked} damage to {receiver}";
            else
                desc = $"{dealerName} dealt 0 damage to {receiver} ({blocked} blocked)";

            if (killed) desc += " [KILLED]";
            return new CombatEvent(CombatEventType.DamageDealt, round, side, desc);
        }
        catch { return null; }
    }

    private CombatEvent? ParseMonsterMove(ClrHeap heap, ClrObject entry, int round, string side, string actor)
    {
        try
        {
            var monsterAddr = entry.ReadObjectField("<Monster>k__BackingField");
            string monsterName = actor;
            if (monsterAddr != 0)
            {
                var monster = heap.GetObject(monsterAddr);
                if (monster.IsValid) monsterName = ReadModelIdEntry(heap, monster);
            }

            string moveId = "unknown";
            var moveAddr = entry.ReadObjectField("<Move>k__BackingField");
            if (moveAddr != 0)
            {
                var move = heap.GetObject(moveAddr);
                if (move.IsValid)
                {
                    moveId = ReadStringField(heap, move, "<StateId>k__BackingField")
                          ?? ReadStringField(heap, move, "StateId")
                          ?? "unknown";
                }
            }

            return new CombatEvent(CombatEventType.MonsterMove, round, side, $"{monsterName} performed {moveId}");
        }
        catch { return null; }
    }

    private CombatEvent? ParseBlockGained(ClrHeap heap, ClrObject entry, int round, string side, string actor)
    {
        try
        {
            int amount = entry.ReadField<int>("<Amount>k__BackingField");
            return new CombatEvent(CombatEventType.BlockGained, round, side, $"{actor} gained {amount} block");
        }
        catch { return null; }
    }

    private CombatEvent? ParseCardDrawn(ClrHeap heap, ClrObject entry, int round, string side, string actor)
    {
        try
        {
            var cardAddr = entry.ReadObjectField("<Card>k__BackingField");
            if (cardAddr == 0) return null;
            var card = heap.GetObject(cardAddr);
            if (!card.IsValid) return null;
            string cardId = ReadModelIdEntry(heap, card);
            return new CombatEvent(CombatEventType.CardDrawn, round, side, $"{actor} drew {cardId}");
        }
        catch { return null; }
    }

    private CombatEvent? ParseEnergySpent(ClrHeap heap, ClrObject entry, int round, string side, string actor)
    {
        try
        {
            int amount = entry.ReadField<int>("<Amount>k__BackingField");
            return new CombatEvent(CombatEventType.EnergySpent, round, side, $"{actor} spent {amount} energy");
        }
        catch { return null; }
    }

    private CombatEvent? ParseCardSimple(ClrHeap heap, ClrObject entry, CombatEventType type, string verb, string actor, int round, string side)
    {
        try
        {
            var cardAddr = entry.ReadObjectField("<Card>k__BackingField");
            if (cardAddr == 0) return null;
            var card = heap.GetObject(cardAddr);
            if (!card.IsValid) return null;
            string cardId = ReadModelIdEntry(heap, card);
            return new CombatEvent(type, round, side, $"{actor} {verb} {cardId}");
        }
        catch { return null; }
    }

    private CombatEvent? ParseCardAfflicted(ClrHeap heap, ClrObject entry, int round, string side, string actor)
    {
        try
        {
            var cardAddr = entry.ReadObjectField("<Card>k__BackingField");
            if (cardAddr == 0) return null;
            var card = heap.GetObject(cardAddr);
            if (!card.IsValid) return null;
            string cardId = ReadModelIdEntry(heap, card);

            string affId = "?";
            var affAddr = entry.ReadObjectField("<Affliction>k__BackingField");
            if (affAddr != 0)
            {
                var aff = heap.GetObject(affAddr);
                if (aff.IsValid) affId = ReadModelIdEntry(heap, aff);
            }
            return new CombatEvent(CombatEventType.CardAfflicted, round, side, $"{actor} afflicted {cardId} with {affId}");
        }
        catch { return null; }
    }

    private CombatEvent? ParsePowerReceived(ClrHeap heap, ClrObject entry, int round, string side, string actor)
    {
        try
        {
            string powerId = "?";
            var powerAddr = entry.ReadObjectField("<Power>k__BackingField");
            if (powerAddr != 0)
            {
                var power = heap.GetObject(powerAddr);
                if (power.IsValid) powerId = ReadModelIdEntry(heap, power);
            }

            decimal amount = 0;
            try { amount = entry.ReadField<decimal>("<Amount>k__BackingField"); } catch { }

            string? applier = null;
            var applierAddr = entry.ReadObjectField("<Applier>k__BackingField");
            if (applierAddr != 0)
            {
                var a = heap.GetObject(applierAddr);
                if (a.IsValid) applier = ReadCreatureName(heap, a);
            }

            string desc = applier != null
                ? $"{applier} applied {amount} {powerId} to {actor}"
                : $"{actor} received {amount} {powerId}";
            return new CombatEvent(CombatEventType.PowerReceived, round, side, desc);
        }
        catch { return null; }
    }

    private CombatEvent? ParsePotionUsed(ClrHeap heap, ClrObject entry, int round, string side, string actor)
    {
        try
        {
            string potionId = "?";
            var potionAddr = entry.ReadObjectField("<Potion>k__BackingField");
            if (potionAddr != 0)
            {
                var potion = heap.GetObject(potionAddr);
                if (potion.IsValid) potionId = ReadModelIdEntry(heap, potion);
            }

            string? target = null;
            var targetAddr = entry.ReadObjectField("<Target>k__BackingField");
            if (targetAddr != 0)
            {
                var t = heap.GetObject(targetAddr);
                if (t.IsValid) target = ReadCreatureName(heap, t);
            }

            string desc = target != null
                ? $"{actor} used {potionId} targeting {target}"
                : $"{actor} used {potionId}";
            return new CombatEvent(CombatEventType.PotionUsed, round, side, desc);
        }
        catch { return null; }
    }

    private CombatEvent? ParseOrbChanneled(ClrHeap heap, ClrObject entry, int round, string side, string actor)
    {
        try
        {
            string orbId = "?";
            var orbAddr = entry.ReadObjectField("<Orb>k__BackingField");
            if (orbAddr != 0)
            {
                var orb = heap.GetObject(orbAddr);
                if (orb.IsValid) orbId = ReadModelIdEntry(heap, orb);
            }
            return new CombatEvent(CombatEventType.OrbChanneled, round, side, $"{actor} channeled {orbId}");
        }
        catch { return null; }
    }

    // ========== Shared Helpers ==========

    private List<string>? ReadCreatureNameList(ClrHeap heap, ClrObject listOrEnumerable)
    {
        // Try as a List<Creature>
        try
        {
            int size = listOrEnumerable.ReadField<int>("_size");
            if (size <= 0) return null;
            var itemsAddr = listOrEnumerable.ReadObjectField("_items");
            if (itemsAddr == 0) return null;
            var items = heap.GetObject(itemsAddr);
            if (!items.IsValid || !items.IsArray) return null;
            var arr = items.AsArray();

            var names = new List<string>();
            for (int i = 0; i < size; i++)
            {
                try
                {
                    var addr = arr.GetObjectValue(i);
                    if (addr == 0) continue;
                    var c = heap.GetObject(addr);
                    if (c.IsValid) names.Add(ReadCreatureName(heap, c));
                }
                catch { }
            }
            return names.Count > 0 ? names : null;
        }
        catch { return null; }
    }

    private string ReadCreatureName(ClrHeap heap, ClrObject creature)
    {
        // Try Player path: Creature.Player.Character.Id.Entry
        try
        {
            var playerAddr = creature.ReadObjectField("<Player>k__BackingField");
            if (playerAddr != 0)
            {
                var player = heap.GetObject(playerAddr);
                if (player.IsValid)
                {
                    var charAddr = player.ReadObjectField("<Character>k__BackingField");
                    if (charAddr != 0)
                    {
                        var character = heap.GetObject(charAddr);
                        if (character.IsValid)
                        {
                            var name = ReadModelIdEntry(heap, character);
                            if (name != "?") return name;
                        }
                    }
                }
            }
        }
        catch { }

        // Try Monster path: Creature.Monster.Id.Entry
        try
        {
            var monsterAddr = creature.ReadObjectField("<Monster>k__BackingField");
            if (monsterAddr != 0)
            {
                var monster = heap.GetObject(monsterAddr);
                if (monster.IsValid)
                {
                    var name = ReadModelIdEntry(heap, monster);
                    if (name != "?") return name;
                }
            }
        }
        catch { }

        // Fallback: try reading the creature's own ModelId directly
        // (some creature refs, e.g. power.Owner, may need this)
        try
        {
            var name = ReadModelIdEntry(heap, creature);
            if (name != "?") return name;
        }
        catch { }

        return "Unknown";
    }

    private string ReadModelIdEntry(ClrHeap heap, ClrObject model)
    {
        try
        {
            var idAddr = model.ReadObjectField("<Id>k__BackingField");
            if (idAddr == 0) return "?";

            var id = heap.GetObject(idAddr);
            if (!id.IsValid) return "?";

            var entryAddr = id.ReadObjectField("<Entry>k__BackingField");
            if (entryAddr != 0)
            {
                var entryObj = heap.GetObject(entryAddr);
                if (entryObj.IsValid)
                    return entryObj.AsString() ?? "?";
            }

            return ReadStringField(heap, id, "Entry") ?? "?";
        }
        catch { return "?"; }
    }

    private string? ReadStringField(ClrHeap heap, ClrObject obj, string fieldName)
    {
        try
        {
            var addr = obj.ReadObjectField(fieldName);
            if (addr == 0) return null;
            var strObj = heap.GetObject(addr);
            return strObj.IsValid ? strObj.AsString() : null;
        }
        catch { return null; }
    }

    private List<CreatureInfo> ReadCreatureList(ClrHeap heap, ClrObject parent, string fieldName)
    {
        var result = new List<CreatureInfo>();

        try
        {
            var listAddr = parent.ReadObjectField(fieldName);
            if (listAddr == 0) return result;
            var list = heap.GetObject(listAddr);
            if (!list.IsValid) return result;

            var itemsAddr = list.ReadObjectField("_items");
            int size = list.ReadField<int>("_size");
            if (itemsAddr == 0 || size <= 0) return result;

            var items = heap.GetObject(itemsAddr);
            if (!items.IsValid || !items.IsArray) return result;
            var arr = items.AsArray();

            for (int i = 0; i < size; i++)
            {
                try
                {
                    var creatureAddr = arr.GetObjectValue(i);
                    if (creatureAddr == 0) continue;
                    var creature = heap.GetObject(creatureAddr);
                    if (!creature.IsValid) continue;

                    int hp = creature.ReadField<int>("_currentHp");
                    int maxHp = creature.ReadField<int>("_maxHp");
                    int block = creature.ReadField<int>("_block");
                    string name = ReadCreatureName(heap, creature);
                    result.Add(new CreatureInfo(name, hp, maxHp, block));
                }
                catch { }
            }
        }
        catch { }

        return result;
    }

    public void Dispose()
    {
        Detach();
    }
}

// ========== Data Types ==========

public enum CombatEventType
{
    CardPlayed,
    CardDrawn,
    DamageDealt,
    Attack,
    MonsterMove,
    BlockGained,
    EnergySpent,
    CardDiscarded,
    CardExhausted,
    CardGenerated,
    CardAfflicted,
    PowerReceived,
    PotionUsed,
    OrbChanneled,
    StarsModified,
    Summoned
}

public record CombatEvent(CombatEventType Type, int Round, string Side, string Description);

public record StructuredCombatEvent(string Type, object Data);

public record CreatureInfo(string Name, int CurrentHp, int MaxHp, int Block);

public record CombatSnapshot(int Round, List<CreatureInfo> Allies, List<CreatureInfo> Enemies);
