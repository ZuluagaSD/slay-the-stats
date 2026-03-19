namespace SlayTheStats.Models;

public readonly record struct BuffRecord(
    int PlayerId,
    string PowerId,
    string PowerName,
    int Stacks,
    int TurnNumber,
    DateTime Timestamp
);
