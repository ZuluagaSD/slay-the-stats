namespace SlayTheStats.Persistence;

/// <summary>
/// Manages the stats output directory at %LOCALAPPDATA%/SlayTheStats/runs/.
/// </summary>
public static class StatsFileManager
{
    private static readonly string BasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SlayTheStats");

    public static string RunsDirectory => Path.Combine(BasePath, "runs");
    public static string ConfigDirectory => BasePath;

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(RunsDirectory);
        Directory.CreateDirectory(ConfigDirectory);
    }

    public static string GetRunFilePath(DateTime startTime)
    {
        var timestamp = startTime.ToString("yyyy-MM-dd_HH-mm-ss");
        return Path.Combine(RunsDirectory, $"run_{timestamp}.json");
    }
}
