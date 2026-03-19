using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Watcher;

// --- Envelope ---

public record EventEnvelope(
    [property: JsonPropertyName("v")] int Version,
    [property: JsonPropertyName("ts")] string Timestamp,
    [property: JsonPropertyName("seq")] long Seq,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("runId")] string RunId,
    [property: JsonPropertyName("data")] object Data
);

// --- Run Lifecycle ---

public record RunStartData(
    [property: JsonPropertyName("seed")] string Seed,
    [property: JsonPropertyName("ascension")] int Ascension,
    [property: JsonPropertyName("players")] List<PlayerInfo> Players
);

public record RunEndData(
    [property: JsonPropertyName("win")] bool Win,
    [property: JsonPropertyName("abandoned")] bool Abandoned,
    [property: JsonPropertyName("killedByEncounter")] string? KilledByEncounter,
    [property: JsonPropertyName("killedByEvent")] string? KilledByEvent,
    [property: JsonPropertyName("runTime")] float RunTime,
    [property: JsonPropertyName("finalPlayers")] List<PlayerInfo>? FinalPlayers
);

public record FloorEnteredData(
    [property: JsonPropertyName("actIndex")] int ActIndex,
    [property: JsonPropertyName("totalFloor")] int TotalFloor,
    [property: JsonPropertyName("mapPointType")] string MapPointType,
    [property: JsonPropertyName("rooms")] List<RoomInfo>? Rooms,
    [property: JsonPropertyName("playerSnapshots")] List<PlayerSnapshotInfo>? PlayerSnapshots
);

public record FloorCompletedData(
    [property: JsonPropertyName("actIndex")] int ActIndex,
    [property: JsonPropertyName("totalFloor")] int TotalFloor,
    [property: JsonPropertyName("playerStats")] List<FloorPlayerStats>? PlayerStats
);

// --- Combat Events ---

public record CombatStartData(
    [property: JsonPropertyName("combatId")] string CombatId,
    [property: JsonPropertyName("encounterId")] string? EncounterId,
    [property: JsonPropertyName("creatures")] List<CreatureSnapshotInfo> Creatures
);

public record CombatEndData(
    [property: JsonPropertyName("combatId")] string CombatId,
    [property: JsonPropertyName("victory")] bool Victory,
    [property: JsonPropertyName("finalRound")] int FinalRound,
    [property: JsonPropertyName("creatures")] List<CreatureSnapshotInfo>? Creatures
);

public record TurnStartData(
    [property: JsonPropertyName("combatId")] string CombatId,
    [property: JsonPropertyName("round")] int Round,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("creatures")] List<CreatureSnapshotInfo>? Creatures
);

public record CardPlayedData(
    [property: JsonPropertyName("round")] int Round,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("actor")] string Actor,
    [property: JsonPropertyName("cardId")] string CardId,
    [property: JsonPropertyName("target")] string? Target,
    [property: JsonPropertyName("wasEthereal")] bool WasEthereal
);

public record CardDrawnData(
    [property: JsonPropertyName("round")] int Round,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("actor")] string Actor,
    [property: JsonPropertyName("cardId")] string CardId,
    [property: JsonPropertyName("fromHandDraw")] bool FromHandDraw
);

public record CardDiscardedData(
    [property: JsonPropertyName("round")] int Round,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("actor")] string Actor,
    [property: JsonPropertyName("cardId")] string CardId
);

public record CardExhaustedData(
    [property: JsonPropertyName("round")] int Round,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("actor")] string Actor,
    [property: JsonPropertyName("cardId")] string CardId
);

public record CardGeneratedData(
    [property: JsonPropertyName("round")] int Round,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("actor")] string Actor,
    [property: JsonPropertyName("cardId")] string CardId,
    [property: JsonPropertyName("generatedByPlayer")] bool GeneratedByPlayer
);

public record CardAfflictedData(
    [property: JsonPropertyName("round")] int Round,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("actor")] string Actor,
    [property: JsonPropertyName("cardId")] string CardId,
    [property: JsonPropertyName("afflictionId")] string AfflictionId
);

public record DamageReceivedData(
    [property: JsonPropertyName("round")] int Round,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("receiver")] string Receiver,
    [property: JsonPropertyName("dealer")] string? Dealer,
    [property: JsonPropertyName("cardSource")] string? CardSource,
    [property: JsonPropertyName("unblockedDamage")] int UnblockedDamage,
    [property: JsonPropertyName("blockedDamage")] int BlockedDamage,
    [property: JsonPropertyName("overkillDamage")] int OverkillDamage,
    [property: JsonPropertyName("wasTargetKilled")] bool WasTargetKilled,
    [property: JsonPropertyName("wasBlockBroken")] bool WasBlockBroken
);

public record CreatureAttackedData(
    [property: JsonPropertyName("round")] int Round,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("attacker")] string Attacker,
    [property: JsonPropertyName("hitCount")] int HitCount
);

public record MonsterMoveData(
    [property: JsonPropertyName("round")] int Round,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("monsterId")] string MonsterId,
    [property: JsonPropertyName("moveId")] string MoveId,
    [property: JsonPropertyName("targets")] List<string>? Targets
);

public record BlockGainedData(
    [property: JsonPropertyName("round")] int Round,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("receiver")] string Receiver,
    [property: JsonPropertyName("amount")] int Amount,
    [property: JsonPropertyName("cardSource")] string? CardSource
);

public record EnergySpentData(
    [property: JsonPropertyName("round")] int Round,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("actor")] string Actor,
    [property: JsonPropertyName("amount")] int Amount
);

public record PowerReceivedData(
    [property: JsonPropertyName("round")] int Round,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("receiver")] string Receiver,
    [property: JsonPropertyName("powerId")] string PowerId,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("applier")] string? Applier
);

public record PotionUsedData(
    [property: JsonPropertyName("round")] int Round,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("actor")] string Actor,
    [property: JsonPropertyName("potionId")] string PotionId,
    [property: JsonPropertyName("target")] string? Target
);

public record OrbChanneledData(
    [property: JsonPropertyName("round")] int Round,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("actor")] string Actor,
    [property: JsonPropertyName("orbId")] string OrbId
);

public record StarsModifiedData(
    [property: JsonPropertyName("round")] int Round,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("actor")] string Actor,
    [property: JsonPropertyName("amount")] int Amount
);

public record SummonedData(
    [property: JsonPropertyName("round")] int Round,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("actor")] string Actor,
    [property: JsonPropertyName("amount")] int Amount
);

// --- Snapshots ---

public record DeckSnapshotData(
    [property: JsonPropertyName("trigger")] string Trigger,
    [property: JsonPropertyName("players")] List<PlayerDeckSnapshot> Players
);

public record CreatureSnapshotData(
    [property: JsonPropertyName("creatures")] List<CreatureSnapshotInfo> Creatures
);

// --- Shared sub-records ---

public record PlayerInfo(
    [property: JsonPropertyName("netId")] ulong NetId,
    [property: JsonPropertyName("character")] string Character,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("maxHp")] int MaxHp,
    [property: JsonPropertyName("gold")] int Gold,
    [property: JsonPropertyName("maxEnergy")] int MaxEnergy
);

public record PlayerSnapshotInfo(
    [property: JsonPropertyName("netId")] ulong NetId,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("maxHp")] int MaxHp,
    [property: JsonPropertyName("gold")] int Gold,
    [property: JsonPropertyName("deckSize")] int DeckSize
);

public record PlayerDeckSnapshot(
    [property: JsonPropertyName("netId")] ulong NetId,
    [property: JsonPropertyName("character")] string Character,
    [property: JsonPropertyName("deck")] List<CardInfo> Deck,
    [property: JsonPropertyName("relics")] List<string> Relics,
    [property: JsonPropertyName("potions")] List<string> Potions
);

public record CardInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("upgradeLevel")] int UpgradeLevel
);

public record RoomInfo(
    [property: JsonPropertyName("roomType")] string RoomType,
    [property: JsonPropertyName("modelId")] string? ModelId,
    [property: JsonPropertyName("monsterIds")] List<string>? MonsterIds
);

public record CreatureSnapshotInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("maxHp")] int MaxHp,
    [property: JsonPropertyName("block")] int Block,
    [property: JsonPropertyName("isPlayer")] bool IsPlayer,
    [property: JsonPropertyName("isAlive")] bool IsAlive,
    [property: JsonPropertyName("powers")] List<PowerInfo>? Powers
);

public record PowerInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("amount")] int Amount
);

public record FloorPlayerStats(
    [property: JsonPropertyName("netId")] ulong NetId,
    [property: JsonPropertyName("goldGained")] int GoldGained,
    [property: JsonPropertyName("goldSpent")] int GoldSpent,
    [property: JsonPropertyName("currentGold")] int CurrentGold,
    [property: JsonPropertyName("currentHp")] int CurrentHp,
    [property: JsonPropertyName("maxHp")] int MaxHp,
    [property: JsonPropertyName("damageTaken")] int DamageTaken,
    [property: JsonPropertyName("hpHealed")] int HpHealed
);

public record RunMeta(
    [property: JsonPropertyName("runId")] string RunId,
    [property: JsonPropertyName("seed")] string Seed,
    [property: JsonPropertyName("ascension")] int Ascension,
    [property: JsonPropertyName("win")] bool Win,
    [property: JsonPropertyName("abandoned")] bool Abandoned,
    [property: JsonPropertyName("killedByEncounter")] string? KilledByEncounter,
    [property: JsonPropertyName("killedByEvent")] string? KilledByEvent,
    [property: JsonPropertyName("runTime")] float RunTime,
    [property: JsonPropertyName("players")] List<RunMetaPlayer> Players,
    [property: JsonPropertyName("totalFloors")] int TotalFloors,
    [property: JsonPropertyName("eventCount")] long EventCount,
    [property: JsonPropertyName("startedAt")] string StartedAt,
    [property: JsonPropertyName("endedAt")] string EndedAt
);

public record RunMetaPlayer(
    [property: JsonPropertyName("netId")] ulong NetId,
    [property: JsonPropertyName("character")] string Character
);

