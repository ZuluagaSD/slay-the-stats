using System.Text.Json;
using Godot;
using SlayTheStats.Persistence;

namespace SlayTheStats.Config;

/// <summary>
/// Persistent configuration. Loaded from and saved to %LOCALAPPDATA%/SlayTheStats/config.json.
/// </summary>
public class StatsConfig
{
    private static StatsConfig? _instance;
    private static readonly string ConfigPath = Path.Combine(
        StatsFileManager.ConfigDirectory, "config.json");

    public static StatsConfig Instance => _instance ??= Load();

    // Settings
    public Key ToggleKey { get; set; } = Key.F7;
    public bool AutoShowCombatSummary { get; set; } = true;
    public bool AutoExportRuns { get; set; } = true;
    public float OverlayOpacity { get; set; } = 0.85f;
    public string OverlayPosition { get; set; } = "TopRight";

    public static StatsConfig Load()
    {
        try
        {
            StatsFileManager.EnsureDirectories();

            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<StatsConfig>(json);
                if (config is not null)
                {
                    MainFile.Log("Config loaded.");
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            MainFile.LogError($"Failed to load config: {ex.Message}");
        }

        var defaultConfig = new StatsConfig();
        defaultConfig.Save();
        return defaultConfig;
    }

    public void Save()
    {
        try
        {
            StatsFileManager.EnsureDirectories();

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            MainFile.LogError($"Failed to save config: {ex.Message}");
        }
    }
}
