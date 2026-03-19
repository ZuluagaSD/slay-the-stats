using System.Text.Json;

namespace Sts2Watcher;

/// <summary>
/// Writes structured events as JSONL to per-run directories.
/// </summary>
public sealed class EventEmitter : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = null // Use the JsonPropertyName attributes as-is
    };

    private readonly string _baseDir;
    private StreamWriter? _writer;
    private string? _currentRunId;
    private string? _currentRunDir;
    private long _seq;
    private string _startedAt = "";

    public string? CurrentRunId => _currentRunId;
    public long EventCount => _seq;

    public EventEmitter()
    {
        _baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SlayTheStats", "watcher", "runs");
        Directory.CreateDirectory(_baseDir);
    }

    public void StartRun(string runId)
    {
        EndRun(null); // Close any previous run

        _currentRunId = runId;
        _currentRunDir = Path.Combine(_baseDir, runId);
        Directory.CreateDirectory(_currentRunDir);

        var eventsPath = Path.Combine(_currentRunDir, "events.jsonl");
        _writer = new StreamWriter(eventsPath, append: true) { AutoFlush = true };
        _seq = 0;
        _startedAt = DateTime.UtcNow.ToString("o");
    }

    public void Emit(string type, object data)
    {
        if (_writer == null || _currentRunId == null) return;

        _seq++;
        var envelope = new EventEnvelope(
            Version: 1,
            Timestamp: DateTime.UtcNow.ToString("o"),
            Seq: _seq,
            Type: type,
            RunId: _currentRunId,
            Data: data
        );

        // Use System.Text.Json with the source-generated context for the envelope
        string json = JsonSerializer.Serialize(envelope, JsonOpts);
        _writer.WriteLine(json);
    }

    public void EndRun(RunMeta? meta)
    {
        if (_currentRunDir != null && meta != null)
        {
            var metaPath = Path.Combine(_currentRunDir, "meta.json");
            var metaJson = JsonSerializer.Serialize(meta, JsonOpts);
            File.WriteAllText(metaPath, metaJson);
        }

        _writer?.Dispose();
        _writer = null;
        _currentRunId = null;
        _currentRunDir = null;
        _seq = 0;
    }

    public string StartedAt => _startedAt;

    public void Dispose()
    {
        _writer?.Dispose();
    }
}
