using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace NHL27Kit.Core;

/// <summary>A player on Iowa Wild's AHL roster, as the AHL's HockeyTech feed has him now.</summary>
public sealed record AhlPlayer(int Id, string First, string Last, int Number, string Position, string Shoots,
                               int HeightIn, int WeightLb, DateTime? Born, string BirthPlace, string TeamName)
{
    public string Name => $"{First} {Last}";
}

/// <summary>
/// Iowa Wild's roster from the AHL's public HockeyTech feed (the same feed theahl.com's roster page uses:
/// lscluster.hockeytech.com/feed/index.php?feed=statviewfeed&amp;view=roster). No HTML scraping: the page is an
/// Angular app and the roster comes back as JSONP. The key/client_code/site_id are public — they are in the site's
/// own JavaScript (statview-1.4.1/js/client/ahl/base.r3.js) — not a secret.
/// Each row gives name, AHL player_id, jersey number, position (LW/C/RW/D/G, or generic F for many forwards),
/// shoots (goalies are "None"), height as "6-3", weight in pounds, birth date and birthplace.
/// Position is display-only: the feed cannot say whether an "F" is a C, LW or RW, and RosterSync.Apply never
/// writes position, so CompareAhl never compares it. Goalies' shoots "None" is kept as-is and never used.
/// </summary>
public static class AhlApi
{
    /// <summary>Iowa Wild, hard-coded for this prototype: the AHL team Nick follows. Expand via the teamsForSeason feed later.</summary>
    public const int TeamId = 389;
    public const string TeamName = "Iowa Wild";
    /// <summary>2026-27 Regular Season. This WILL go stale: next season the current season_id changes and this constant (and the UI text) must be updated, or resolved from the feed's bootstrap view.</summary>
    public const int SeasonId = 94;
    public const int LeagueId = 4;

    private const string Key = "ccb91f29d6744675";
    private const string ClientCode = "ahl";
    private const int SiteId = 3;
    private const string Base = "https://lscluster.hockeytech.com/feed/index.php?feed=statviewfeed&view=roster";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>Iowa Wild's current roster (25 players in 2026-27: 15 forwards, 8 defenders, 2 goalies).</summary>
    public static Task<List<AhlPlayer>> RosterAsync(int teamId = TeamId, int seasonId = SeasonId, CancellationToken ct = default) =>
        RosterAsync(teamId, seasonId, TeamName, ct);

    /// <summary>One team's current roster from the feed. The team name is only used for display (the feed's teamName is not trusted for matching).</summary>
    public static async Task<List<AhlPlayer>> RosterAsync(int teamId, int seasonId, string teamName, CancellationToken ct = default)
    {
        string url = $"{Base}&team_id={teamId}&season_id={seasonId}&league_id={LeagueId}&key={Key}&client_code={ClientCode}&site_id={SiteId}&lang=en";
        string json = await GetAsync(url, ct);
        using var doc = JsonDocument.Parse(json);
        var list = new List<AhlPlayer>();
        if (!doc.RootElement.TryGetProperty("roster", out var roster) || roster.GetArrayLength() == 0) return list;
        foreach (var section in roster[0].GetProperty("sections").EnumerateArray())
        {
            // "Team Personnel" has no birth date or jersey number: coaches and staff, not players.
            if (Str(section, "title") == "Team Personnel") continue;
            if (!section.TryGetProperty("data", out var data)) continue;
            foreach (var item in data.EnumerateArray())
            {
                if (!item.TryGetProperty("row", out var row)) continue;
                string name = Str(row, "name");
                string born = Str(row, "birthdate");
                if (name.Length == 0 || born.Length == 0) continue;
                // Split on the last word: "Viking Gustafsson Nyberg" -> First "Viking Gustafsson", Last "Nyberg".
                // The matcher only uses First + " " + Last normalised, so any split point compares the same.
                int cut = name.LastIndexOf(' ');
                string first = cut > 0 ? name[..cut] : name, last = cut > 0 ? name[(cut + 1)..] : "";
                int id = int.TryParse(Str(row, "player_id"), out int pid) ? pid : -1;
                int number = int.TryParse(Str(row, "tp_jersey_number"), out int n) ? n : -1;
                int height = ParseHeight(Str(row, "height_hyphenated"));
                int weight = int.TryParse(Str(row, "w"), out int w) ? w : 0;
                DateTime? birth = DateTime.TryParse(born, CultureInfo.InvariantCulture, DateTimeStyles.None, out var b) ? b : null;
                list.Add(new AhlPlayer(id, first, last, number, Str(row, "position"), Str(row, "shoots"),
                                       height, weight, birth, Str(row, "birthplace"), teamName));
            }
        }
        return list;
    }

    /// <summary>"6-3" -> 75 inches. Anything unparsable -> 0 (unknown, never written).</summary>
    private static int ParseHeight(string s)
    {
        var parts = (s ?? "").Split('-');
        return parts.Length == 2 && int.TryParse(parts[0], out int ft) && int.TryParse(parts[1], out int inch) ? ft * 12 + inch : 0;
    }

    /// <summary>A GET that strips the JSONP wrapper — the feed answers ({...}) — and retries on 429, up to five times.</summary>
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
            string text = (await r.Content.ReadAsStringAsync(ct)).Trim();
            // JSONP: ({...}). Plain JSON ({...} with no wrapper) is accepted too.
            if (text.StartsWith("(", StringComparison.Ordinal) && text.EndsWith(")", StringComparison.Ordinal))
                text = text[1..^1].Trim();
            return text;
        }
    }

    private static string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";
}
