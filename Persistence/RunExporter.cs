using System.Text.Json;
using System.Text.Json.Serialization;
using SlayTheStats.Core;

namespace SlayTheStats.Persistence;

/// <summary>
/// Exports run data as JSON to %LOCALAPPDATA%/SlayTheStats/runs/.
/// Auto-subscribes to StatManager.OnRunEnded.
/// </summary>
public static class RunExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Initialize()
    {
        StatsFileManager.EnsureDirectories();
        StatManager.Instance.OnRunEnded += ExportRun;
        MainFile.Log("RunExporter initialized.");
    }

    private static void ExportRun(RunSession run)
    {
        try
        {
            var filePath = StatsFileManager.GetRunFilePath(run.StartTime);

            var exportData = new RunExportData
            {
                StartTime = run.StartTime,
                EndTime = run.EndTime,
                TotalCombats = run.Combats.Count,
                Victories = run.Combats.Count(c => c.IsVictory),
                OverallMvpPlayerId = run.OverallMvp(),
                Players = run.Players.Values.Select(p => new PlayerExportData
                {
                    PlayerId = p.PlayerId,
                    PlayerName = p.PlayerName,
                    CharacterClass = p.CharacterClass,
                    TotalDamage = run.TotalDamageByPlayer(p.PlayerId),
                    TotalBlock = run.TotalBlockByPlayer(p.PlayerId),
                    TotalCardsPlayed = run.TotalCardsPlayedByPlayer(p.PlayerId),
                }).ToList(),
                Combats = run.Combats.Select((c, i) => new CombatExportData
                {
                    CombatIndex = i,
                    StartTime = c.StartTime,
                    EndTime = c.EndTime,
                    IsVictory = c.IsVictory,
                    TotalTurns = c.CurrentTurn,
                    TotalDamage = c.DamageRecords.Sum(r => r.Amount),
                    TotalBlock = c.BlockRecords.Sum(r => r.Amount),
                    TotalCardsPlayed = c.CardPlayRecords.Count,
                    MvpPlayerId = c.MvpPlayerId(),
                    DamageByPlayer = c.DamageByPlayer(),
                    BlockByPlayer = c.BlockByPlayer(),
                    CardsPlayedByPlayer = c.CardsPlayedByPlayer(),
                }).ToList(),
            };

            var json = JsonSerializer.Serialize(exportData, JsonOptions);
            File.WriteAllText(filePath, json);

            MainFile.Log($"Run exported to: {filePath}");
        }
        catch (Exception ex)
        {
            MainFile.LogError($"Failed to export run: {ex}");
        }
    }
}

// Export DTOs
internal class RunExportData
{
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int TotalCombats { get; set; }
    public int Victories { get; set; }
    public int? OverallMvpPlayerId { get; set; }
    public List<PlayerExportData> Players { get; set; } = [];
    public List<CombatExportData> Combats { get; set; } = [];
}

internal class PlayerExportData
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    public string CharacterClass { get; set; } = "";
    public int TotalDamage { get; set; }
    public int TotalBlock { get; set; }
    public int TotalCardsPlayed { get; set; }
}

internal class CombatExportData
{
    public int CombatIndex { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool IsVictory { get; set; }
    public int TotalTurns { get; set; }
    public int TotalDamage { get; set; }
    public int TotalBlock { get; set; }
    public int TotalCardsPlayed { get; set; }
    public int? MvpPlayerId { get; set; }
    public Dictionary<int, int> DamageByPlayer { get; set; } = [];
    public Dictionary<int, int> BlockByPlayer { get; set; } = [];
    public Dictionary<int, int> CardsPlayedByPlayer { get; set; } = [];
}
