using System.Runtime.InteropServices;
using Sts2Watcher;

// Hide console window when launched with --background flag
bool background = args.Contains("--background");
if (background)
{
    var hwnd = GetConsoleWindow();
    if (hwnd != IntPtr.Zero)
        ShowWindow(hwnd, 0); // SW_HIDE
}

Console.Title = "STS2 Combat Watcher";
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("=== Slay the Spire 2 — Combat Watcher ===");
Console.ResetColor();
Console.WriteLine("Watching for game process...\n");

using var reader = new GameReader();
using var emitter = new EventEmitter();
var tracker = new StateTracker();

ulong combatManagerAddr = 0;
ulong runManagerAddr = 0;
CombatSnapshot? lastSnapshot = null;

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

while (!cts.IsCancellationRequested)
{
    try
    {
        if (!reader.IsAttached || !reader.IsProcessAlive())
        {
            if (reader.IsAttached)
            {
                WriteStatus("Game process lost. Waiting for restart...");
                var synth = tracker.GenerateSyntheticRunEnd();
                if (synth != null)
                {
                    emitter.Emit(synth.Value.type, synth.Value.data);
                    EmitRunMeta(emitter, tracker, synth.Value.data as RunEndData);
                }
                reader.Detach();
                combatManagerAddr = 0;
                runManagerAddr = 0;
                lastSnapshot = null;
            }

            if (!reader.TryAttach())
            {
                await Task.Delay(2000, cts.Token);
                continue;
            }

            WriteStatus("Attached to Slay the Spire 2!");
            var modules = reader.GetLoadedModules();
            WriteStatus($"Found {modules.Count} CLR modules");
        }

        reader.Refresh();

        if (combatManagerAddr == 0)
        {
            combatManagerAddr = reader.FindCombatManagerInstance();
            if (combatManagerAddr != 0)
                WriteStatus($"Found CombatManager at 0x{combatManagerAddr:X}");
        }

        if (runManagerAddr == 0)
        {
            runManagerAddr = reader.FindRunManagerInstance();
            if (runManagerAddr != 0)
                WriteStatus($"Found RunManager at 0x{runManagerAddr:X}");
        }

        if (combatManagerAddr == 0 && runManagerAddr == 0)
        {
            await Task.Delay(1000, cts.Token);
            continue;
        }

        var events = tracker.ProcessTick(reader, combatManagerAddr, runManagerAddr);

        foreach (var (type, data) in events)
        {
            if (type == "run_start")
            {
                emitter.StartRun(tracker.CurrentRunId);
                WriteEvent(ConsoleColor.Yellow, "RUN", $"=== Run Started (ID: {tracker.CurrentRunId}) ===");
                if (data is RunStartData rsd)
                {
                    WriteEvent(ConsoleColor.Yellow, "RUN", $"Seed: {rsd.Seed}, Ascension: {rsd.Ascension}");
                    foreach (var p in rsd.Players)
                        WriteEvent(ConsoleColor.Cyan, "RUN", $"  Player {p.NetId}: {p.Character} ({p.Hp}/{p.MaxHp} HP, {p.Gold}g)");
                }
            }

            if (type == "run_end")
            {
                if (data is RunEndData red)
                {
                    string outcome = red.Win ? "VICTORY" : (red.Abandoned ? "ABANDONED" : "DEFEAT");
                    WriteEvent(ConsoleColor.Yellow, "RUN", $"=== Run Ended: {outcome} ===");
                    if (red.KilledByEncounter != null)
                        WriteEvent(ConsoleColor.Red, "RUN", $"  Killed by: {red.KilledByEncounter}");
                }
            }

            emitter.Emit(type, data);

            if (type == "run_end")
                EmitRunMeta(emitter, tracker, data as RunEndData);

            DisplayEventOnConsole(type, data);
        }

        if (tracker.WasInCombat && combatManagerAddr != 0)
        {
            var snap = reader.ReadCombatSnapshot(combatManagerAddr);
            if (snap != null && HasSnapshotChanged(lastSnapshot, snap))
            {
                PrintCompactSnapshot(snap);
                lastSnapshot = snap;
            }
        }
        else
        {
            lastSnapshot = null;
        }

        await Task.Delay(500, cts.Token);
    }
    catch (OperationCanceledException) { break; }
    catch (Exception ex)
    {
        WriteStatus($"Error: {ex.GetType().Name}: {ex.Message}");
        if (ex is not System.Text.Json.JsonException and not NotSupportedException)
        {
            combatManagerAddr = 0;
            runManagerAddr = 0;
        }
        await Task.Delay(2000, cts.Token);
    }
}

var finalSynth = tracker.GenerateSyntheticRunEnd();
if (finalSynth != null)
{
    emitter.Emit(finalSynth.Value.type, finalSynth.Value.data);
    EmitRunMeta(emitter, tracker, finalSynth.Value.data as RunEndData);
}
WriteStatus("Watcher stopped.");

// --- Helpers ---

static void EmitRunMeta(EventEmitter emitter, StateTracker tracker, RunEndData? runEnd)
{
    var runInfo = tracker.CurrentRunInfo;
    if (runInfo == null || emitter.CurrentRunId == null) { emitter.EndRun(null); return; }
    emitter.EndRun(new RunMeta(
        RunId: emitter.CurrentRunId, Seed: runInfo.Seed, Ascension: runInfo.Ascension,
        Win: runEnd?.Win ?? false, Abandoned: runEnd?.Abandoned ?? false,
        KilledByEncounter: runEnd?.KilledByEncounter, KilledByEvent: runEnd?.KilledByEvent,
        RunTime: runEnd?.RunTime ?? 0,
        Players: runInfo.Players.Select(p => new RunMetaPlayer(p.NetId, p.Character)).ToList(),
        TotalFloors: tracker.LastFloorCount, EventCount: emitter.EventCount,
        StartedAt: emitter.StartedAt, EndedAt: DateTime.UtcNow.ToString("o")));
}

static void DisplayEventOnConsole(string type, object data)
{
    var (color, tag) = type switch
    {
        "combat_start" => (ConsoleColor.Yellow, "CMBT"),
        "combat_end" => (ConsoleColor.Yellow, "CMBT"),
        "turn_start" => (ConsoleColor.Cyan, "TURN"),
        "card_played" => (ConsoleColor.Green, "CARD"),
        "card_drawn" => (ConsoleColor.DarkGray, "DRAW"),
        "card_discarded" => (ConsoleColor.DarkGray, "DISC"),
        "card_exhausted" => (ConsoleColor.DarkMagenta, "EXHT"),
        "card_generated" => (ConsoleColor.DarkGreen, "GENR"),
        "card_afflicted" => (ConsoleColor.DarkYellow, "AFFL"),
        "damage_received" => (ConsoleColor.Red, "DMG"),
        "creature_attacked" => (ConsoleColor.DarkRed, "ATK"),
        "monster_move" => (ConsoleColor.Magenta, "MOVE"),
        "block_gained" => (ConsoleColor.Blue, "BLK"),
        "energy_spent" => (ConsoleColor.DarkYellow, "NRG"),
        "power_received" => (ConsoleColor.DarkCyan, "PWR"),
        "potion_used" => (ConsoleColor.Green, "POT"),
        "orb_channeled" => (ConsoleColor.Cyan, "ORB"),
        "stars_modified" => (ConsoleColor.Yellow, "STAR"),
        "summoned" => (ConsoleColor.Yellow, "SUMM"),
        "floor_entered" => (ConsoleColor.White, "FLOR"),
        "deck_snapshot" or "creature_snapshot" => (ConsoleColor.DarkGray, "SNAP"),
        _ => (ConsoleColor.Gray, "???")
    };

    string? msg = type switch
    {
        "combat_start" when data is CombatStartData cs =>
            $"=== Combat Started: {cs.EncounterId ?? "unknown"} ({cs.Creatures.Count} creatures) ===",
        "combat_end" when data is CombatEndData ce =>
            $"=== Combat Ended: {(ce.Victory ? "Victory" : "Defeat")} (R{ce.FinalRound}) ===",
        "turn_start" when data is TurnStartData ts =>
            $"Round {ts.Round} — {ts.Side} turn",
        "card_played" when data is CardPlayedData cp =>
            cp.Target != null ? $"R{cp.Round} ({cp.Side}): {cp.Actor} played {cp.CardId} → {cp.Target}" : $"R{cp.Round} ({cp.Side}): {cp.Actor} played {cp.CardId}",
        "card_drawn" when data is CardDrawnData cd =>
            $"R{cd.Round} ({cd.Side}): {cd.Actor} drew {cd.CardId}",
        "damage_received" when data is DamageReceivedData dr =>
            $"R{dr.Round} ({dr.Side}): {dr.Dealer ?? "?"} dealt {dr.UnblockedDamage} to {dr.Receiver}{(dr.WasTargetKilled ? " [KILLED]" : "")}",
        "block_gained" when data is BlockGainedData bg =>
            $"R{bg.Round} ({bg.Side}): {bg.Receiver} gained {bg.Amount} block",
        "energy_spent" when data is EnergySpentData es =>
            $"R{es.Round} ({es.Side}): {es.Actor} spent {es.Amount} energy",
        "power_received" when data is PowerReceivedData pr =>
            $"R{pr.Round} ({pr.Side}): {pr.Receiver} received {pr.Amount} {pr.PowerId}",
        "monster_move" when data is MonsterMoveData mm =>
            $"R{mm.Round} ({mm.Side}): {mm.MonsterId} performed {mm.MoveId}",
        "floor_entered" when data is FloorEnteredData fe =>
            $"Floor {fe.TotalFloor} (Act {fe.ActIndex + 1}): {fe.MapPointType}",
        _ => (string?)null
    };

    if (msg != null)
        WriteEvent(color, tag, msg);
}

static void WriteStatus(string msg)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"  [{DateTime.Now:HH:mm:ss}] {msg}");
    Console.ResetColor();
}

static void WriteEvent(ConsoleColor color, string tag, string msg)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write($"  [{DateTime.Now:HH:mm:ss}] ");
    Console.ForegroundColor = color;
    Console.Write($"[{tag,-4}] ");
    Console.ResetColor();
    Console.WriteLine(msg);
}

static void PrintCompactSnapshot(CombatSnapshot snap)
{
    var parts = new List<string>();
    foreach (var a in snap.Allies)
        parts.Add($"\u001b[32m{a.Name}:{a.CurrentHp}/{a.MaxHp}\u001b[0m");
    foreach (var e in snap.Enemies)
        parts.Add($"\u001b[31m{e.Name}:{e.CurrentHp}/{e.MaxHp}\u001b[0m");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write($"  [{DateTime.Now:HH:mm:ss}] ");
    Console.ResetColor();
    Console.WriteLine($"HP: {string.Join(" | ", parts)}");
}

static bool HasSnapshotChanged(CombatSnapshot? old, CombatSnapshot? current)
{
    if (old == null || current == null) return true;
    if (old.Allies.Count != current.Allies.Count || old.Enemies.Count != current.Enemies.Count) return true;
    for (int i = 0; i < old.Allies.Count && i < current.Allies.Count; i++)
        if (old.Allies[i].CurrentHp != current.Allies[i].CurrentHp || old.Allies[i].Block != current.Allies[i].Block) return true;
    for (int i = 0; i < old.Enemies.Count && i < current.Enemies.Count; i++)
        if (old.Enemies[i].CurrentHp != current.Enemies[i].CurrentHp || old.Enemies[i].Block != current.Enemies[i].Block) return true;
    return false;
}

// P/Invoke to hide console window
[DllImport("kernel32.dll")]
static extern IntPtr GetConsoleWindow();

[DllImport("user32.dll")]
static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
