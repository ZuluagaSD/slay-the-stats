namespace SlayTheStats.Models;

public readonly record struct DebuffRecord(
    int TargetId,
    string PowerId,
    string PowerName,
    int Stacks,
    int AppliedByPlayerId,
    int TurnNumber,
    DateTime Timestamp
);
