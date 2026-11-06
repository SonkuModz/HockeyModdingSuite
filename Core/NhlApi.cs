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

    /// <summary>
    /// The player's full bio from /v1/player/{id}/landing: number, position, shoots/catches, height, weight, birth date,
    /// birth city (all the game's birthplace field holds), and where he was drafted (draftDetails; none means undrafted). The API's wing
    /// codes L/R become LW/RW, as the AHL writes them.
    /// </summary>
    public static async Task<PlayerBio> BioAsync(int playerId, CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(await GetAsync($"{Base}player/{playerId}/landing", ct));
        var p = doc.RootElement;
        int Int(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
        var draft = DraftInfo.Undrafted;
        if (p.TryGetProperty("draftDetails", out var d) && d.ValueKind == JsonValueKind.Object && Int(d, "year") > 0)
            draft = new DraftInfo(Int(d, "year"), Str(d, "teamAbbrev"), Int(d, "round"), Int(d, "overallPick"));
        return new PlayerBio(
            Text(p, "firstName"), Text(p, "lastName"),
            p.TryGetProperty("sweaterNumber", out var n) && n.ValueKind == JsonValueKind.Number ? n.GetInt32() : -1,
            Str(p, "position") switch { "L" => "LW", "R" => "RW", var pos => pos },
            Str(p, "shootsCatches"), Int(p, "heightInInches"), Int(p, "weightInPounds"),
            DateTime.TryParse(Str(p, "birthDate"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var b) ? b : null,
            Text(p, "birthCity"), draft, "NHL")
        {
            BirthCountry = Str(p, "birthCountry"), BirthRegion = Text(p, "birthStateProvince"),
        };
    }

    /// <summary>NHL player ids whose full name matches exactly (case-insensitive), from the NHL's player search
    /// (search.d3.nhle.com, the search box on nhl.com). The search is fuzzy, so non-exact names are dropped.</summary>
    public static async Task<List<int>> SearchAsync(string name, CancellationToken ct = default)
    {
        string url = $"https://search.d3.nhle.com/api/v1/search/player?culture=en-us&limit=20&q={Uri.EscapeDataString(name)}";
        using var doc = JsonDocument.Parse(await GetAsync(url, ct));
        var ids = new List<int>();
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return ids;
        foreach (var p in doc.RootElement.EnumerateArray())
            if (string.Equals(Str(p, "name"), name, StringComparison.OrdinalIgnoreCase) && int.TryParse(Str(p, "playerId"), out int id))
                ids.Add(id);
        return ids;
    }

    /// <summary>Full NHL team names (as other sources write them) to abbreviations. Current teams come from CapWages'
    /// page slugs; relocated and renamed franchises are listed here so older draft years still resolve.</summary>
    private static readonly Dictionary<string, string> FormerTeams = new(StringComparer.OrdinalIgnoreCase)
    {
        ["arizona_coyotes"] = "ARI", ["phoenix_coyotes"] = "PHX", ["utah_hockey_club"] = "UTA", ["utah_hc"] = "UTA",
        ["atlanta_thrashers"] = "ATL", ["mighty_ducks_of_anaheim"] = "ANA",
    };

    /// <summary>"Minnesota Wild" -> "MIN". The name as given when it isn't a known NHL team.</summary>
    public static string TeamAbbr(string fullName)
    {
        string slug = Slug(fullName);
        foreach (var (abbr, s) in CapWagesApi.Teams)
            if (s == slug) return abbr;
        return FormerTeams.TryGetValue(slug, out var former) ? former : fullName;
    }

    /// <summary>"Montréal Canadiens" -> "montreal_canadiens", matching CapWages' team slugs.</summary>
    private static string Slug(string name)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in (name ?? "").Normalize(System.Text.NormalizationForm.FormD))
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            else if (sb.Length > 0 && sb[^1] != '_') sb.Append('_');
        }
        return sb.ToString().Trim('_');
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
