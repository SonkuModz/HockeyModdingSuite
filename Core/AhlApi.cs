using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace NHL27Kit.Core;

/// <summary>A player on an AHL roster, as the AHL's HockeyTech feed has him now.</summary>
public sealed record AhlPlayer(int Id, string First, string Last, int Number, string Position, string Shoots,
                               int HeightIn, int WeightLb, DateTime? Born, string BirthPlace, string TeamName)
{
    public string Name => $"{First} {Last}";
}

/// <summary>One AHL team, as the feed's teamsForSeason view lists it (id, feed team_code, name).</summary>
public sealed record AhlTeam(int Id, string Code, string Name);

/// <summary>
/// The AHL's rosters from the AHL's public HockeyTech feed (the same feed theahl.com's roster pages use:
/// lscluster.hockeytech.com/feed/index.php?feed=statviewfeed). No HTML scraping: the pages are an Angular app and
/// the data comes back as JSONP. The key/client_code/site_id are public — they are in the site's own JavaScript
/// (statview-1.4.1/js/client/ahl/base.r3.js) — not a secret.
/// Each roster row gives name, AHL player_id, jersey number, position (LW/C/RW/D/G, or generic F for many forwards),
/// shoots (goalies are "None"), height as "6-3", weight in pounds, birth date and birthplace.
/// Position is display-only: the feed cannot say whether an "F" is a C, LW or RW, and RosterSync.Apply never
/// writes position, so CompareAhl never compares it. Goalies' shoots "None" is kept as-is and never used.
/// </summary>
public static class AhlApi
{
    /// <summary>The season to fall back on when the feed doesn't say which is current (2026-27).</summary>
    public const int SeasonId = 94;
    public const int LeagueId = 4;

    private const string Key = "ccb91f29d6744675";
    private const string ClientCode = "ahl";
    private const int SiteId = 3;
    private const string FeedBase = "https://lscluster.hockeytech.com/feed/index.php?feed=statviewfeed";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>Every AHL team in the season, from the feed's teamsForSeason view (32 teams in 2026-27). The "All Teams" row (id -1) is skipped.</summary>
    public static async Task<List<AhlTeam>> TeamsAsync(int seasonId = SeasonId, CancellationToken ct = default)
    {
        string url = $"{FeedBase}&view=teamsForSeason&season_id={seasonId}&key={Key}&client_code={ClientCode}&site_id={SiteId}&lang=en";
        string json = await GetAsync(url, ct);
        using var doc = JsonDocument.Parse(json);
        var list = new List<AhlTeam>();
        if (!doc.RootElement.TryGetProperty("teams", out var teams)) return list;
        foreach (var t in teams.EnumerateArray())
        {
            if (!int.TryParse(Str(t, "id"), out int id) || id < 0) continue;
            string name = Str(t, "name");
            if (name.Length == 0) continue;
            list.Add(new AhlTeam(id, Str(t, "team_code"), name));
        }
        return list;
    }

    /// <summary>The newest regular season, from the feed's bootstrap view (what theahl.com's pages start from); <see cref="SeasonId"/> if that fails.</summary>
    public static async Task<int> CurrentSeasonAsync(CancellationToken ct = default)
    {
        try
        {
            string json = await GetAsync($"{FeedBase}&view=bootstrap&season=latest&pageName=roster&key={Key}&client_code={ClientCode}&site_id={SiteId}&league_id={LeagueId}&lang=en", ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("regularSeasons", out var seasons) && seasons.GetArrayLength() > 0 && int.TryParse(Str(seasons[0], "id"), out int id))
                return id;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested) { }
        return SeasonId;
    }

    /// <summary>
    /// Every AHL team's current roster, one team at a time (the feed turns away bursts with 429, so there is a
    /// 250 ms pause between teams, like NhlApi.AllRostersAsync). Progress is reported per team (the feed's team_code, e.g. "ABB"; the name when a team has none).
    /// Roughly 32 teams x ~25 players, so about 800 players in 2026-27.
    /// </summary>
    public static async Task<List<(AhlTeam Team, List<AhlPlayer> Players)>> AllRostersAsync(Action<string> progress = null, CancellationToken ct = default)
    {
        int season = await CurrentSeasonAsync(ct);
        var teams = await TeamsAsync(season, ct);
        var all = new List<(AhlTeam, List<AhlPlayer>)>();
        foreach (var t in teams)
        {
            all.Add((t, await RosterAsync(t.Id, season, t.Name, ct)));
            progress?.Invoke(string.IsNullOrEmpty(t.Code) ? t.Name : t.Code);
            await Task.Delay(250, ct);
        }
        return all;
    }

    /// <summary>One team's current roster from the feed. The team name is only used for display (the feed's teamName is not trusted for matching).</summary>
    public static async Task<List<AhlPlayer>> RosterAsync(int teamId, int seasonId, string teamName, CancellationToken ct = default)
    {
        string url = $"{FeedBase}&view=roster&team_id={teamId}&season_id={seasonId}&league_id={LeagueId}&key={Key}&client_code={ClientCode}&site_id={SiteId}&lang=en";
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

    /// <summary>
    /// The player's full bio from the feed's player view (what theahl.com/stats/player/{id} shows): number, position,
    /// shoots (catches for goalies), height, weight, birth date, birth city (the feed's "City, Region" line cut to the
    /// city, as the game stores it), and where he was drafted into the NHL. info.drafts lists every draft he went
    /// through (NHL and others); the latest NHL one wins, since a player who went unsigned can be drafted again.
    /// draft_rank is the overall pick. No NHL entry means undrafted. Position may be just "F", as on the roster.
    /// </summary>
    public static async Task<PlayerBio> BioAsync(int playerId, CancellationToken ct = default)
    {
        string url = $"{FeedBase}&view=player&player_id={playerId}&season_id={SeasonId}&league_id={LeagueId}&statsType=standard&key={Key}&client_code={ClientCode}&site_id={SiteId}&lang=en";
        using var doc = JsonDocument.Parse(await GetAsync(url, ct));
        if (!doc.RootElement.TryGetProperty("info", out var info)) throw new InvalidOperationException($"The AHL feed has no player {playerId}.");
        DraftInfo best = DraftInfo.Undrafted;
        if (info.TryGetProperty("drafts", out var drafts) && drafts.ValueKind == JsonValueKind.Array)
            foreach (var d in drafts.EnumerateArray())
            {
                if (!string.Equals(Str(d, "draft_league"), "NHL", StringComparison.OrdinalIgnoreCase)) continue;
                if (!int.TryParse(Str(d, "draft_year"), out int year) || year <= best.Year) continue;
                int.TryParse(Str(d, "draft_round"), out int round);
                int.TryParse(Str(d, "draft_rank"), out int overall);
                best = new DraftInfo(year, NhlApi.TeamAbbr(Str(d, "draft_team")), round, overall);
            }
        string position = Str(info, "position");
        return new PlayerBio(
            Str(info, "firstName"), Str(info, "lastName"),
            int.TryParse(Str(info, "jerseyNumber"), out int n) ? n : -1,
            position, Str(info, position == "G" ? "catches" : "shoots"),
            ParseHeight(Str(info, "height_hyphenated")), int.TryParse(Str(info, "weight"), out int w) ? w : 0,
            DateTime.TryParse(Str(info, "birthDate"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var b) ? b : null,
            MissingPlayers.BirthCity(Str(info, "birthPlace")), best, "AHL");
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
