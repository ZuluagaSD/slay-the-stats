namespace SlayTheStats.Models;

public readonly record struct DamageRecord(
    int SourcePlayerId,
    int TargetId,
    int Amount,
    int TurnNumber,
    DateTime Timestamp
);
