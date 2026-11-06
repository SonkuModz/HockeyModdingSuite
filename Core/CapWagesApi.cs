using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NHL27Kit.Core;

/// <summary>One season of a contract as CapWages lists it. Dollars are whole dollars; StartYear 2026 = the 2026-27 season.</summary>
public sealed record CapSeason(int StartYear, long CapHit, long BaseSalary, long? MinorsSalary, string Clause, int? ListSize,
                               IReadOnlyList<(string Team, long CapHit)> Retention);

/// <summary>One contract: its type ("Entry-Level Contract", "Two-Way Contract (Extension)"…), expiry status ("UFA", "RFA"),
/// the clause text ("10 team no trade list"), and its seasons in order.</summary>
public sealed record CapContract(string Type, string Expiry, string ClauseDetails, IReadOnlyList<CapSeason> Seasons);

/// <summary>A player under contract with a team (NHL roster, minors, injured or inactive). Section is the page's group ("roster", "non-roster", "inactive").</summary>
public sealed record CapPlayer(string First, string Last, DateTime? Born, string Section, string TermsDetails, IReadOnlyList<CapContract> Contracts)
{
    public string Name => $"{First} {Last}";
    /// <summary>The positions CapWages lists him at ("C", "LW, RW", "LD, RD"), from where he has played; "" when none.</summary>
    public string Pos { get; init; } = "";
    /// <summary>Games he has played at each position ("LW" → 96, "RW" → 46, "C" → 3); empty when CapWages has none.</summary>
    public IReadOnlyDictionary<string, int> PositionGames { get; init; } = new Dictionary<string, int>();
}

/// <summary>A draft pick a team's page lists: real draft year, round, the original team's full name, and whether the team traded it away.</summary>
public sealed record CapPick(int Year, int Round, string OriginalTeam, bool TradedAway);

/// <summary>One NHL team's CapWages page, parsed.</summary>
public sealed record CapTeam(string Abbr, string Name, IReadOnlyList<CapPlayer> Players, IReadOnlyList<CapPick> Picks)
{
    /// <summary>The affiliate's players on AHL contracts (the page's "AHL Contracts" table: name, birth date, one position, no
    /// NHL contract). Kept apart from Players so contracts and picks never see them; only their positions are used.</summary>
    public IReadOnlyList<CapPlayer> AhlPlayers { get; init; } = Array.Empty<CapPlayer>();
}

/// <summary>
/// NHL contracts and draft picks from CapWages (capwages.com), read from each team's public page at sync time on the
/// user's own machine — nothing from CapWages is stored in the repo or shipped. Each page (capwages.com/teams/{slug})
/// is a Next.js page whose data is embedded as JSON in its __NEXT_DATA__ script: props.pageProps.data holds the
/// players by group ("roster", "non-roster", "inactive", "dead cap"), each with contracts and per-season cap hits,
/// clauses and retention; props.pageProps.draftPicks holds the picks the team owns or traded away. This class is the
/// only code that knows that layout: if CapWages changes it, parsing fails here and the sync reports CapWages failed.
/// The "dead cap" group (retained salary, buyouts, buried contracts) is skipped: retained players are read from their
/// own team's roster, where each season carries a retention list, so nobody is counted twice.
/// </summary>
public static class CapWagesApi
{
    public const string Base = "https://capwages.com/teams/";

    /// <summary>NHL abbreviation and CapWages page slug for every team. Abbreviations resolve to game teams via RosterSync.TeamIndex.</summary>
    public static readonly (string Abbr, string Slug)[] Teams =
    {
        ("ANA", "anaheim_ducks"), ("BOS", "boston_bruins"), ("BUF", "buffalo_sabres"), ("CGY", "calgary_flames"),
        ("CAR", "carolina_hurricanes"), ("CHI", "chicago_blackhawks"), ("COL", "colorado_avalanche"),
        ("CBJ", "columbus_blue_jackets"), ("DAL", "dallas_stars"), ("DET", "detroit_red_wings"),
        ("EDM", "edmonton_oilers"), ("FLA", "florida_panthers"), ("LAK", "los_angeles_kings"),
        ("MIN", "minnesota_wild"), ("MTL", "montreal_canadiens"), ("NSH", "nashville_predators"),
        ("NJD", "new_jersey_devils"), ("NYI", "new_york_islanders"), ("NYR", "new_york_rangers"),
        ("OTT", "ottawa_senators"), ("PHI", "philadelphia_flyers"), ("PIT", "pittsburgh_penguins"),
        ("SJS", "san_jose_sharks"), ("SEA", "seattle_kraken"), ("STL", "st_louis_blues"),
        ("TBL", "tampa_bay_lightning"), ("TOR", "toronto_maple_leafs"), ("UTA", "utah_mammoth"),
        ("VAN", "vancouver_canucks"), ("VGK", "vegas_golden_knights"), ("WSH", "washington_capitals"),
        ("WPG", "winnipeg_jets"),
    };

    private static readonly HttpClient Http = MakeClient();

    private static HttpClient MakeClient()
    {
        var http = new HttpClient(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("HockeyModdingSuite/1.0");
        return http;
    }

    /// <summary>
    /// Every team's page, one at a time with a pause between them (a light, browser-like pace). Progress is reported per
    /// team (its abbreviation). All or nothing: any team failing throws, because retention totals and pick owners need
    /// the whole league — half a league must never be compared or written.
    /// </summary>
    public static async Task<List<CapTeam>> AllTeamsAsync(Action<string> progress = null, CancellationToken ct = default)
    {
        var all = new List<CapTeam>();
        foreach (var (abbr, slug) in Teams)
        {
            string html = await GetAsync(Base + slug, ct);
            all.Add(Parse(html, abbr));
            progress?.Invoke(abbr);
            await Task.Delay(500, ct);
        }
        return all;
    }

    /// <summary>
    /// The real season the data is "now", as its start year (2026 = 2026-27): the year before the earliest draft the
    /// teams still hold picks in, since a draft that has happened is no longer listed. Pages also list last season's
    /// contracts, so the earliest contract season would be a year too early. Falls back to the date (July 1 starts a season).
    /// </summary>
    public static int CurrentSeason(IReadOnlyList<CapTeam> teams)
    {
        var years = teams.SelectMany(t => t.Picks).Select(p => p.Year).Where(y => y > 0).ToList();
        if (years.Count > 0) return years.Min() - 1;
        var now = DateTime.Today;
        return now.Month >= 7 ? now.Year : now.Year - 1;
    }

    /// <summary>One team page's embedded data, parsed. Throws when the page has no __NEXT_DATA__ block (CapWages changed its layout).</summary>
    public static CapTeam Parse(string html, string abbr)
    {
        var m = Regex.Match(html, "<script id=\"__NEXT_DATA__\"[^>]*>(.*?)</script>", RegexOptions.Singleline);
        if (!m.Success) throw new InvalidOperationException($"CapWages' {abbr} page has no data block: the site's layout may have changed.");
        using var doc = JsonDocument.Parse(m.Groups[1].Value);
        var pp = doc.RootElement.GetProperty("props").GetProperty("pageProps");
        string name = pp.TryGetProperty("teamName", out var tn) && tn.ValueKind == JsonValueKind.String ? tn.GetString() : abbr;

        var players = new List<CapPlayer>();
        if (pp.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            foreach (var section in data.EnumerateObject())
            {
                if (section.Name == "dead cap" || section.Value.ValueKind != JsonValueKind.Object) continue;
                foreach (var group in section.Value.EnumerateObject())
                    if (group.Value.ValueKind == JsonValueKind.Array)
                        foreach (var p in group.Value.EnumerateArray())
                            if (ParsePlayer(p, section.Name) is { } player) players.Add(player);
            }

        var picks = new List<CapPick>();
        if (pp.TryGetProperty("draftPicks", out var dp) && dp.ValueKind == JsonValueKind.Array)
            foreach (var p in dp.EnumerateArray())
            {
                if (!int.TryParse(Str(p, "year"), out int year) || !p.TryGetProperty("round", out var r) || r.ValueKind != JsonValueKind.Number) continue;
                bool away = p.TryGetProperty("isTradedAway", out var ta) && ta.ValueKind == JsonValueKind.True;
                picks.Add(new CapPick(year, r.GetInt32(), Str(p, "team"), away));
            }
        // the AHL affiliate's AHL-contract players (pageProps.ahlClub.players)
        var ahl = new List<CapPlayer>();
        if (pp.TryGetProperty("ahlClub", out var club) && club.ValueKind == JsonValueKind.Object
            && club.TryGetProperty("players", out var ap) && ap.ValueKind == JsonValueKind.Array)
            foreach (var p in ap.EnumerateArray())
            {
                string full = Str(p, "name");
                if (full.Length == 0) continue;
                int comma = full.IndexOf(',');
                string last = comma > 0 ? full[..comma].Trim() : full, first = comma > 0 ? full[(comma + 1)..].Trim() : "";
                ahl.Add(new CapPlayer(first, last, ParseDate(Str(p, "born")), "ahl", "", Array.Empty<CapContract>()) { Pos = Str(p, "pos") });
            }
        return new CapTeam(abbr, name, players, picks) { AhlPlayers = ahl };
    }

    private static CapPlayer ParsePlayer(JsonElement p, string section)
    {
        string full = Str(p, "name");
        if (full.Length == 0) return null;
        // "Gauthier, Cutter" -> First "Cutter", Last "Gauthier"
        int comma = full.IndexOf(',');
        string last = comma > 0 ? full[..comma].Trim() : full, first = comma > 0 ? full[(comma + 1)..].Trim() : "";
        var contracts = new List<CapContract>();
        if (p.TryGetProperty("contracts", out var cs) && cs.ValueKind == JsonValueKind.Array)
            foreach (var c in cs.EnumerateArray())
            {
                var seasons = new List<CapSeason>();
                if (c.TryGetProperty("details", out var ds) && ds.ValueKind == JsonValueKind.Array)
                    foreach (var d in ds.EnumerateArray())
                        if (ParseSeason(d) is { } s) seasons.Add(s);
                contracts.Add(new CapContract(Str(c, "type").Trim(), Str(c, "expiryStatus"), Str(c, "clauseDetails"), seasons));
            }
        var games = new Dictionary<string, int>();
        if (p.TryGetProperty("positions", out var ps) && ps.ValueKind == JsonValueKind.Object)
            foreach (var g in ps.EnumerateObject())
                if (g.Value.ValueKind == JsonValueKind.Number) games[g.Name] = g.Value.GetInt32();
        return new CapPlayer(first, last, ParseDate(Str(p, "born")), section, Str(p, "termsDetails"), contracts) { Pos = Str(p, "pos"), PositionGames = games };
    }

    private static CapSeason ParseSeason(JsonElement d)
    {
        // "2026-27" -> 2026
        string season = Str(d, "season");
        if (season.Length < 4 || !int.TryParse(season[..4], out int start)) return null;
        var retention = new List<(string, long)>();
        if (d.TryGetProperty("retention", out var ret) && ret.ValueKind == JsonValueKind.Object)
            foreach (var t in ret.EnumerateObject())
                if (t.Value.ValueKind == JsonValueKind.Object && Money(Str(t.Value, "capHit")) is { } hit)
                    retention.Add((t.Name, hit));
        int? size = d.TryGetProperty("clauseListSize", out var ls) && ls.ValueKind == JsonValueKind.Number ? ls.GetInt32() : null;
        return new CapSeason(start, Money(Str(d, "capHit")) ?? -1, Money(Str(d, "baseSalary")) ?? -1, Money(Str(d, "minorsSalary")),
                             Str(d, "clause").Trim(), size, retention);
    }

    /// <summary>"$13,500,000" -> 13500000. Blank or text ("ENTRY-LEVEL SLIDE") -> null.</summary>
    public static long? Money(string s)
    {
        string digits = (s ?? "").Replace("$", "").Replace(",", "").Trim();
        return digits.Length > 0 && digits.All(char.IsDigit) && long.TryParse(digits, out long v) ? v : null;
    }

    /// <summary>"Jan. 19, 2004", "May 5, 2003", "Sept. 3, 2001" -> the date; null when unreadable.</summary>
    public static DateTime? ParseDate(string s)
    {
        string t = (s ?? "").Replace(".", "").Replace("Sept ", "Sep ").Trim();
        foreach (var fmt in new[] { "MMM d, yyyy", "MMMM d, yyyy" })
            if (DateTime.TryParseExact(t, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        return null;
    }

    private static async Task<string> GetAsync(string url, CancellationToken ct)
    {
        for (int attempt = 0; ; attempt++)
        {
            using var r = await Http.GetAsync(url, ct);
            if ((int)r.StatusCode == 429 && attempt < 5)
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
