using System.Net.Http;
using System.Text.Json;

namespace NHL27Kit.Core;

/// <summary>A player on an NHL roster, as the NHL's public API has him now.</summary>
public sealed record NhlPlayer(int Id, string First, string Last, string Team, int Number, string Position, string Shoots,
                               int HeightIn, int WeightLb, DateTime? Born, string BirthCity, string BirthCountry, string Headshot)
{
    public string Name => $"{First} {Last}";
}

/// <summary>
/// The NHL's public web API (api-web.nhle.com; reference: github.com/Zmalski/NHL-API-Reference). No key is needed.
/// Rosters come from /v1/roster/{TEAM}/current: forwards, defensemen and goalies with id, names, number, position,
/// shoots, height, weight, birth date and place, and a headshot URL.
/// </summary>
public static class NhlApi
{
    public const string Base = "https://api-web.nhle.com/v1/";

    /// <summary>The 32 teams' API codes.</summary>
    public static readonly string[] Teams =
    {
        "ANA", "BOS", "BUF", "CGY", "CAR", "CHI", "COL", "CBJ", "DAL", "DET", "EDM", "FLA", "LAK", "MIN", "MTL", "NSH",
        "NJD", "NYI", "NYR", "OTT", "PHI", "PIT", "SJS", "SEA", "STL", "TBL", "TOR", "UTA", "VAN", "VGK", "WSH", "WPG",
    };

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>One team's current roster.</summary>
    public static async Task<List<NhlPlayer>> RosterAsync(string team, CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(await GetAsync($"{Base}roster/{team}/current", ct));
        var list = new List<NhlPlayer>();
        foreach (var group in new[] { "forwards", "defensemen", "goalies" })
        {
            if (!doc.RootElement.TryGetProperty(group, out var arr)) continue;
            foreach (var p in arr.EnumerateArray())
                list.Add(new NhlPlayer(
                    p.GetProperty("id").GetInt32(), Text(p, "firstName"), Text(p, "lastName"), team,
                    p.TryGetProperty("sweaterNumber", out var n) && n.ValueKind == JsonValueKind.Number ? n.GetInt32() : -1,
                    Str(p, "positionCode"), Str(p, "shootsCatches"),
                    p.TryGetProperty("heightInInches", out var h) && h.ValueKind == JsonValueKind.Number ? h.GetInt32() : 0,
                    p.TryGetProperty("weightInPounds", out var w) && w.ValueKind == JsonValueKind.Number ? w.GetInt32() : 0,
                    DateTime.TryParse(Str(p, "birthDate"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var b) ? b : null,
                    Text(p, "birthCity"), Str(p, "birthCountry"), Str(p, "headshot")));
        }
        return list;
    }

    /// <summary>Every team's current roster, one team at a time (the API turns away bursts with 429).</summary>
    public static async Task<List<NhlPlayer>> AllRostersAsync(Action<string> progress = null, CancellationToken ct = default)
    {
        var all = new List<NhlPlayer>();
        foreach (var t in Teams)
        {
            all.AddRange(await RosterAsync(t, ct));
            progress?.Invoke(t);
            await Task.Delay(250, ct);
        }
        return all;
    }

    /// <summary>A GET that waits and tries again when the API says too many requests (429), up to five times.</summary>
    private static async Task<string> GetAsync(string url, CancellationToken ct)
    {
        for (int attempt = 0; ; attempt++)
        {
            using var r = await Http.GetAsync(url, ct);
            if ((int)r.StatusCode == 429 && attempt < 5)
            {
                var wait = r.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2 << attempt);
                await Task.Delay(wait, ct);
                continue;
            }
            r.EnsureSuccessStatusCode();
            return await r.Content.ReadAsStringAsync(ct);
        }
    }

    private static string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";

    /// <summary>A localised name object's "default" text ({"default": "Teddy", "cs": …}).</summary>
    private static string Text(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return "";
        if (v.ValueKind == JsonValueKind.String) return v.GetString();
        return v.ValueKind == JsonValueKind.Object && v.TryGetProperty("default", out var d) ? d.GetString() : "";
    }
}
