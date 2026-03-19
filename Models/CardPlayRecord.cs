namespace SlayTheStats.Models;

public readonly record struct CardPlayRecord(
    int PlayerId,
    string CardId,
    string CardName,
    int EnergyCost,
    string CardType,
    int TurnNumber,
    DateTime Timestamp
);
