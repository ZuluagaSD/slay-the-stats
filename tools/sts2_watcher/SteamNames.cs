using System.Text.Json;

namespace Sts2Watcher;

/// <summary>
/// Resolves Steam IDs to player names via the Steam Web API.
/// Caches results so each ID is only looked up once.
/// </summary>
public static class SteamNames
{
    // Free endpoint, no API key needed
    private const string SteamApiUrl = "https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key=NONE&steamids=";

    private static readonly Dictionary<ulong, string> _cache = new();
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };

    /// <summary>
    /// Get a display name for a Steam ID. Returns cached name or fetches from Steam.
    /// Falls back to last 4 digits if the lookup fails.
    /// </summary>
    public static string Get(ulong steamId)
    {
        if (_cache.TryGetValue(steamId, out var cached))
            return cached;

        // Try to resolve via Steam API (async but we block briefly — only happens once per player)
        try
        {
            var name = FetchSteamName(steamId);
            if (name != null)
            {
                _cache[steamId] = name;
                return name;
            }
        }
        catch { }

        // Fallback: last 4 digits
        var fallback = (steamId % 10000).ToString();
        _cache[steamId] = fallback;
        return fallback;
    }

    private static string? FetchSteamName(ulong steamId)
    {
        // The free endpoint requires an API key, but we can try the community XML profile instead
        try
        {
            var url = $"https://steamcommunity.com/profiles/{steamId}/?xml=1";
            var response = _http.GetStringAsync(url).GetAwaiter().GetResult();

            // Parse the steamID (display name) from XML: <steamID><![CDATA[PlayerName]]></steamID>
            var start = response.IndexOf("<steamID><![CDATA[");
            if (start < 0) return null;
            start += "<steamID><![CDATA[".Length;
            var end = response.IndexOf("]]></steamID>", start);
            if (end < 0) return null;

            var name = response[start..end].Trim();
            return name.Length > 0 ? name : null;
        }
        catch { return null; }
    }
}
