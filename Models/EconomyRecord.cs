namespace SlayTheStats.Models;

public readonly record struct EconomyRecord(
    int PlayerId,
    int GoldChange,
    int TurnNumber,
    DateTime Timestamp
);
