using Sts2Watcher;

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

// Legacy console state (for colored output alongside JSONL)
CombatSnapshot? lastSnapshot = null;

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

while (!cts.IsCancellationRequested)
{
    try
    {
        // Attach to game if not connected
        if (!reader.IsAttached || !reader.IsProcessAlive())
        {
            if (reader.IsAttached)
            {
                WriteStatus("Game process lost. Waiting for restart...");

                // Emit synthetic run_end if we were mid-run
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
            WriteStatus($"Found {modules.Count} CLR modules:");
            foreach (var m in modules.Where(m => !m.Contains("System.") && !m.Contains("Microsoft.")))
                WriteStatus($"  {m}");
        }

        // Refresh cached data for new reads
        reader.Refresh();

        // Find singletons if not yet found
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

        // === StateTracker produces structured events ===
        var events = tracker.ProcessTick(reader, combatManagerAddr, runManagerAddr);

        foreach (var (type, data) in events)
        {
            // Start/end JSONL run files
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

            // Emit to JSONL
            emitter.Emit(type, data);

            // End run file after run_end
            if (type == "run_end")
            {
                EmitRunMeta(emitter, tracker, data as RunEndData);
            }

            // Console display for combat events
            DisplayEventOnConsole(type, data);
        }

        // === Legacy console: compact HP snapshot ===
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
    catch (OperationCanceledException)
    {
        break;
    }
    catch (Exception ex)
    {
        WriteStatus($"Error: {ex.GetType().Name}: {ex.Message}");

        // Only reset singletons for memory-read errors, not serialization errors
        if (ex is not System.Text.Json.JsonException and not NotSupportedException)
        {
            combatManagerAddr = 0;
            runManagerAddr = 0;
        }

        await Task.Delay(2000, cts.Token);
    }
}

// Clean shutdown
var finalSynth = tracker.GenerateSyntheticRunEnd();
if (finalSynth != null)
{
    emitter.Emit(finalSynth.Value.type, finalSynth.Value.data);
    EmitRunMeta(emitter, tracker, finalSynth.Value.data as RunEndData);
}

WriteStatus("Watcher stopped.");

// --- Helper methods ---

static void EmitRunMeta(EventEmitter emitter, StateTracker tracker, RunEndData? runEnd)
{
    var runInfo = tracker.CurrentRunInfo;
    if (runInfo == null || emitter.CurrentRunId == null) { emitter.EndRun(null); return; }

    var meta = new RunMeta(
        RunId: emitter.CurrentRunId,
        Seed: runInfo.Seed,
        Ascension: runInfo.Ascension,
        Win: runEnd?.Win ?? false,
        Abandoned: runEnd?.Abandoned ?? false,
        KilledByEncounter: runEnd?.KilledByEncounter,
        KilledByEvent: runEnd?.KilledByEvent,
        RunTime: runEnd?.RunTime ?? 0,
        Players: runInfo.Players.Select(p => new RunMetaPlayer(p.NetId, p.Character)).ToList(),
        TotalFloors: tracker.LastFloorCount,
        EventCount: emitter.EventCount,
        StartedAt: emitter.StartedAt,
        EndedAt: DateTime.UtcNow.ToString("o")
    );

    emitter.EndRun(meta);
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
        "card_discarded" when data is CardDiscardedData cdi =>
            $"R{cdi.Round} ({cdi.Side}): {cdi.Actor} discarded {cdi.CardId}",
        "card_exhausted" when data is CardExhaustedData cex =>
            $"R{cex.Round} ({cex.Side}): {cex.Actor} exhausted {cex.CardId}",
        "card_generated" when data is CardGeneratedData cg =>
            $"R{cg.Round} ({cg.Side}): {cg.Actor} generated {cg.CardId}",
        "card_afflicted" when data is CardAfflictedData ca =>
            $"R{ca.Round} ({ca.Side}): {ca.Actor} afflicted {ca.CardId} with {ca.AfflictionId}",
        "damage_received" when data is DamageReceivedData dr =>
            FormatDamage(dr),
        "creature_attacked" when data is CreatureAttackedData cat =>
            $"R{cat.Round} ({cat.Side}): {cat.Attacker} attacked ({cat.HitCount} hits)",
        "monster_move" when data is MonsterMoveData mm =>
            $"R{mm.Round} ({mm.Side}): {mm.MonsterId} performed {mm.MoveId}",
        "block_gained" when data is BlockGainedData bg =>
            $"R{bg.Round} ({bg.Side}): {bg.Receiver} gained {bg.Amount} block",
        "energy_spent" when data is EnergySpentData es =>
            $"R{es.Round} ({es.Side}): {es.Actor} spent {es.Amount} energy",
        "power_received" when data is PowerReceivedData pr =>
            pr.Applier != null ? $"R{pr.Round} ({pr.Side}): {pr.Applier} applied {pr.Amount} {pr.PowerId} to {pr.Receiver}" : $"R{pr.Round} ({pr.Side}): {pr.Receiver} received {pr.Amount} {pr.PowerId}",
        "potion_used" when data is PotionUsedData pu =>
            pu.Target != null ? $"R{pu.Round} ({pu.Side}): {pu.Actor} used {pu.PotionId} → {pu.Target}" : $"R{pu.Round} ({pu.Side}): {pu.Actor} used {pu.PotionId}",
        "orb_channeled" when data is OrbChanneledData oc =>
            $"R{oc.Round} ({oc.Side}): {oc.Actor} channeled {oc.OrbId}",
        "stars_modified" when data is StarsModifiedData sm =>
            $"R{sm.Round} ({sm.Side}): {sm.Actor} {(sm.Amount < 0 ? "lost" : "gained")} {Math.Abs(sm.Amount)} star(s)",
        "summoned" when data is SummonedData su =>
            $"R{su.Round} ({su.Side}): {su.Actor} summoned {su.Amount}",
        "floor_entered" when data is FloorEnteredData fe =>
            $"Floor {fe.TotalFloor} (Act {fe.ActIndex + 1}): {fe.MapPointType}",
        _ => (string?)null // Skip console for snapshots and other events
    };

    if (msg != null)
        WriteEvent(color, tag, msg);
}

static string FormatDamage(DamageReceivedData dr)
{
    string desc;
    if (dr.UnblockedDamage > 0 && dr.BlockedDamage > 0)
        desc = $"{dr.Dealer ?? "?"} dealt {dr.UnblockedDamage} to {dr.Receiver} ({dr.BlockedDamage} blocked)";
    else if (dr.UnblockedDamage > 0)
        desc = $"{dr.Dealer ?? "?"} dealt {dr.UnblockedDamage} to {dr.Receiver}";
    else
        desc = $"{dr.Dealer ?? "?"} dealt 0 to {dr.Receiver} ({dr.BlockedDamage} blocked)";

    if (dr.WasTargetKilled) desc += " [KILLED]";
    return $"R{dr.Round} ({dr.Side}): {desc}";
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
    {
        if (old.Allies[i].CurrentHp != current.Allies[i].CurrentHp ||
            old.Allies[i].Block != current.Allies[i].Block)
            return true;
    }

    for (int i = 0; i < old.Enemies.Count && i < current.Enemies.Count; i++)
    {
        if (old.Enemies[i].CurrentHp != current.Enemies[i].CurrentHp ||
            old.Enemies[i].Block != current.Enemies[i].Block)
            return true;
    }

    return false;
}
