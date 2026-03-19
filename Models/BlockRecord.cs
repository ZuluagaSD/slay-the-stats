namespace SlayTheStats.Models;

public readonly record struct BlockRecord(
    int PlayerId,
    int Amount,
    int TurnNumber,
    DateTime Timestamp
);
