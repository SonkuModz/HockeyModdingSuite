using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NHL27Kit.Core;

/// <summary>
/// One spot of a team's line combinations: category (ev even strength, pp power play, pk penalty kill, oi out/injured),
/// group (f1-f4, d1-d3, g, pp1-pp2, pk1-pk2, ir), position (lw/c/rw, ld/rd, g1/g2, sk1-sk5 for special teams, ir1…),
/// the player's name and number, and his injury status ("ir", "out", "dtd", or "").
/// </summary>
public sealed record LineSpot(string Category, string Group, string Position, string Name, int Number, string Injury, bool GameTimeDecision);

/// <summary>A team's line combinations on DailyFaceoff, as of when they were last updated, and who reported them.</summary>
public sealed record LineTeam(string Abbr, string Name, DateTime? UpdatedAt, string SourceName, List<LineSpot> Spots);

/// <summary>
/// DailyFaceoff's line combinations (dailyfaceoff.com/teams/{slug}/line-combinations), the lines reported for each NHL
/// team's next game. Read from the team pages' embedded Next.js data (__NEXT_DATA__ → props.pageProps.combinations:
/// players with category/group/position identifiers), as a browser shows them; the site's /api/ is disallowed by its
/// robots.txt and isn't used. Each user's own copy reads the pages, one at a time with a pause.
/// </summary>
public static class DailyFaceoffApi
{
    public const string Base = "https://www.dailyfaceoff.com/teams/";

    /// <summary>The 32 teams' page slugs (CapWages' slugs with hyphens: minnesota-wild, st-louis-blues, utah-mammoth).</summary>
    public static IEnumerable<(string Abbr, string Slug)> Teams => CapWagesApi.Teams.Select(t => (t.Abbr, t.Slug.Replace('_', '-')));

    private static readonly HttpClient Http = MakeClient();

    private static HttpClient MakeClient()
    {
        var http = new HttpClient(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("HockeyModdingSuite/1.0");
        return http;
    }

    /// <summary>
    /// Every team's lines, one page at a time with a short pause. A team that fails (page down, layout changed) comes
    /// back with its error and no lines, so the other 31 still sync. Progress is reported per team (abbreviation).
    /// </summary>
    public static async Task<List<(string Abbr, LineTeam Team, string Error)>> AllTeamsAsync(Action<string> progress = null, CancellationToken ct = default)
    {
        var all = new List<(string, LineTeam, string)>();
        foreach (var (abbr, slug) in Teams)
        {
            try { all.Add((abbr, Parse(await GetAsync($"{Base}{slug}/line-combinations", ct), abbr), null)); }
            catch (Exception e) when (e is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException
                                          || e is TaskCanceledException && !ct.IsCancellationRequested)
            {
                all.Add((abbr, null, e.Message));
            }
            progress?.Invoke(abbr);
            await Task.Delay(400, ct);
        }
        return all;
    }

    /// <summary>One team page's line combinations. Throws when the page has no data block or no combinations.</summary>
    public static LineTeam Parse(string html, string abbr)
    {
        var m = Regex.Match(html, "<script id=\"__NEXT_DATA__\"[^>]*>(.*?)</script>", RegexOptions.Singleline);
        if (!m.Success) throw new InvalidOperationException($"DailyFaceoff's {abbr} page has no data block: the site's layout may have changed.");
        using var doc = JsonDocument.Parse(m.Groups[1].Value);
        var pp = doc.RootElement.GetProperty("props").GetProperty("pageProps");
        if (!pp.TryGetProperty("combinations", out var c) || c.ValueKind != JsonValueKind.Object || !c.TryGetProperty("players", out var players))
            throw new InvalidOperationException($"DailyFaceoff has no line combinations for {abbr}.");
        var spots = new List<LineSpot>();
        foreach (var p in players.EnumerateArray())
            spots.Add(new LineSpot(Str(p, "categoryIdentifier"), Str(p, "groupIdentifier"), Str(p, "positionIdentifier"), Str(p, "name"),
                p.TryGetProperty("jerseyNumber", out var n) && n.ValueKind == JsonValueKind.Number ? n.GetInt32() : -1,
                Str(p, "injuryStatus"), p.TryGetProperty("gameTimeDecision", out var g) && g.ValueKind == JsonValueKind.True));
        DateTime? updated = DateTime.TryParse(Str(c, "updatedAt"), System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var u) ? u : null;
        return new LineTeam(abbr, Str(c, "teamName") is { Length: > 0 } name ? name : abbr, updated, Str(c, "sourceName"), spots);
    }

    private static async Task<string> GetAsync(string url, CancellationToken ct)
    {
        for (int attempt = 0; ; attempt++)
        {
            using var r = await Http.GetAsync(url, ct);
            if ((int)r.StatusCode == 429 && attempt < 4)
            {
                await Task.Delay(r.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2 << attempt), ct);
                continue;
            }
            r.EnsureSuccessStatusCode();
            return await r.Content.ReadAsStringAsync(ct);
        }
    }

    private static string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";
}
