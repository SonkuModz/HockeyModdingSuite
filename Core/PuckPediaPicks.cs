using System.Net;
using System.Text.Json;

namespace NHL27Kit.Core;

/// <summary>One draft pick as PuckPedia lists it now.</summary>
public sealed record PuckPediaPick(string Id, int DraftYear, int Round, string OriginalSlug, string OwnerSlug, bool HasActiveCondition)
{
    /// <summary>The game's exhibitiondraftpick year: calendar draft year - 1900 (127 = 2027 draft).</summary>
    public int StoredYear => DraftYear - 1900;
}

/// <summary>One team whose PuckPedia page could not be read, and why — shown in the UI status line and the CLI so a failed fetch is never mistaken for "no differences".</summary>
public sealed record PuckPediaFailure(string Slug, string Reason);

/// <summary>The result of fetching every team's picks: the picks, plus the teams whose page could not be read and why.</summary>
public sealed record PuckPediaFetch(IReadOnlyList<PuckPediaPick> Picks, IReadOnlyList<PuckPediaFailure> FailedTeams);

/// <summary>
/// Draft pick ownership from PuckPedia (puckpedia.com/team/{slug}).
///
/// Each team page is server-rendered HTML with the picks embedded as JSON in Alpine.js x-data attributes, in two
/// forms: draftPickSummary([...]) arrays (the team's own picks, 7 per year for 2027-2030) and draftPickDetails({...})
/// blocks (the same picks plus, in the per-year detail tables, picks the team acquired from other teams). The JSON is
/// HTML-escaped, so it is decoded first, then parsed with balanced-bracket extraction; picks are deduped by
/// draft_pick_id, so fetching all 32 pages covers every pick exactly once. No key, no JavaScript, plain HTTP GET.
/// The current owner is the pick's current_team_url_id slug; the original team is team_url_id. A pick whose
/// active_draft_pick_condition is non-empty has a live condition on it (see PickSync, which excludes those).
///
/// Verified live 2026-11-04 (MIN, CGY, CHI, UTA pages): 2031 is absent from PuckPedia entirely, 2030 picks appear
/// only in the draftPickDetails blocks, and traded_away is present in summary objects but absent from details
/// objects — so neither is relied on. Owners are self-consistent across pages (no pick had two different owners).
///
/// 2026-11-04: the same pages returned 200 to curl and loaded in a browser on the user's PC, yet the app's
/// HttpClient (Chrome UA only) failed all 32 teams there. PuckPedia sits behind Cloudflare, which scores a
/// request's TLS fingerprint and header set, so the request now mimics a browser navigation (see Http) and every
/// failure carries its reason instead of just the slug — a total failure must never read as "0 differences".
/// </summary>
public static class PuckPediaPicks
{
    public const string Base = "https://puckpedia.com/team/";

    /// <summary>PuckPedia slug and NHL abbreviation for every team. The abbreviation is resolved to a game team
    /// index at runtime via RosterSync.TeamIndex — never hard-code indexes here.</summary>
    public static readonly (string Slug, string Abbr)[] Teams =
    {
        ("anaheim-ducks", "ANA"), ("boston-bruins", "BOS"), ("buffalo-sabres", "BUF"), ("calgary-flames", "CGY"),
        ("carolina-hurricanes", "CAR"), ("chicago-blackhawks", "CHI"), ("colorado-avalanche", "COL"),
        ("columbus-blue-jackets", "CBJ"), ("dallas-stars", "DAL"), ("detroit-red-wings", "DET"),
        ("edmonton-oilers", "EDM"), ("florida-panthers", "FLA"), ("los-angeles-kings", "LAK"),
        ("minnesota-wild", "MIN"), ("montreal-canadiens", "MTL"), ("nashville-predators", "NSH"),
        ("new-jersey-devils", "NJD"), ("new-york-islanders", "NYI"), ("new-york-rangers", "NYR"),
        ("ottawa-senators", "OTT"), ("philadelphia-flyers", "PHI"), ("pittsburgh-penguins", "PIT"),
        ("san-jose-sharks", "SJS"), ("seattle-kraken", "SEA"), ("st-louis-blues", "STL"),
        ("tampa-bay-lightning", "TBL"), ("toronto-maple-leafs", "TOR"), ("utah-mammoth", "UTA"),
        ("vancouver-canucks", "VAN"), ("vegas-golden-knights", "VGK"), ("washington-capitals", "WSH"),
        ("winnipeg-jets", "WPG"),
    };

    // A bare HttpClient (Chrome UA only, no Accept/Accept-Language, HTTP/1.1) gets refused by PuckPedia's
    // Cloudflare fronting on some machines — the same pages load fine in a real browser on the same PC — so
    // the request is made to look like a browser navigation: full Accept and Accept-Language, Sec-Fetch
    // headers, HTTP/2 when the server offers it, and automatic decompression (the handler then sends
    // Accept-Encoding itself). 30 s timeout and the 429 retry in GetAsync are unchanged; 403 is never retried.
    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
    })
    {
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestVersion = HttpVersion.Version20,
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
    };

    static PuckPediaPicks()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36");
        Http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        Http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        Http.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
        Http.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
        Http.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
        Http.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
        Http.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
    }

    /// <summary>One team's picks, as listed on its PuckPedia page.</summary>
    public static async Task<List<PuckPediaPick>> TeamPicksAsync(string slug, CancellationToken ct = default)
    {
        string html = await GetAsync(Base + slug, ct);
        return Parse(html);
    }

    /// <summary>
    /// All 32 team pages, one at a time with a ~250 ms pause, deduped by pick id. A team whose page fails is
    /// reported in FailedTeams with its reason and skipped — the compare still runs on the teams that loaded.
    /// A page that loads but yields no picks is also a failure ("page had no pick data"): a silent empty result
    /// would otherwise read as "0 differences". Progress is called with each slug as its page is fetched.
    /// </summary>
    public static async Task<PuckPediaFetch> AllPicksAsync(Action<string> progress = null, CancellationToken ct = default)
    {
        var byId = new Dictionary<string, PuckPediaPick>();
        var failed = new List<PuckPediaFailure>();
        foreach (var (slug, _) in Teams)
        {
            progress?.Invoke(slug);
            try
            {
                var team = await TeamPicksAsync(slug, ct);
                if (team.Count == 0) failed.Add(new PuckPediaFailure(slug, "page had no pick data"));
                else foreach (var p in team) byId.TryAdd(p.Id, p);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                failed.Add(new PuckPediaFailure(slug, FailureReason(ex)));
            }
            await Task.Delay(250, ct);
        }
        return new PuckPediaFetch(byId.Values.ToList(), failed);
    }

    /// <summary>Short human-readable reason for a failed team fetch: HTTP status, timeout, connection error, or the exception type.</summary>
    public static string FailureReason(Exception ex)
    {
        switch (ex)
        {
            case HttpRequestException hre when hre.StatusCode is HttpStatusCode status:
                return $"HTTP {(int)status}";
            case TaskCanceledException:
                return "timed out";
            case HttpRequestException hre:
                return "connection failed" + ShortDetail(hre.InnerException?.Message);
            default:
                return $"unexpected error ({ex.GetType().Name})";
        }
    }

    private static string ShortDetail(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "";
        string one = message.Replace("\r", " ").Replace("\n", " ").Trim();
        if (one.Length > 80) one = one[..77] + "…";
        return ": " + one;
    }

    /// <summary>Parses the picks out of one team page's HTML. Public so the shape can be checked without fetching.</summary>
    public static List<PuckPediaPick> Parse(string html)
    {
        string s = WebUtility.HtmlDecode(html);
        var picks = new Dictionary<string, PuckPediaPick>();

        void Add(JsonElement o)
        {
            string id = Str(o, "draft_pick_id");
            string orig = Str(o, "team_url_id");
            string owner = Str(o, "current_team_url_id");
            int year = Int(o, "draft_year"), round = Int(o, "draft_round");
            if (id == null || orig == null || owner == null || year < 0 || round < 0) return;
            picks.TryAdd(id, new PuckPediaPick(id, year, round, orig, owner, HasActiveCondition(o)));
        }

        for (int i = 0;;)
        {
            int j = s.IndexOf("draftPickSummary([", i, StringComparison.Ordinal);
            if (j < 0) break;
            int open = s.IndexOf('[', j);
            string arr = ExtractBalanced(s, open, '[', ']');
            if (arr == null) break;
            i = open + arr.Length;
            try
            {
                using var doc = JsonDocument.Parse(arr);
                foreach (var o in doc.RootElement.EnumerateArray()) Add(o);
            }
            catch (JsonException) { /* a malformed block is skipped; the details blocks usually carry the same picks */ }
        }

        for (int i = 0;;)
        {
            int j = s.IndexOf("draftPickDetails({", i, StringComparison.Ordinal);
            if (j < 0) break;
            int open = s.IndexOf('{', j);
            string obj = ExtractBalanced(s, open, '{', '}');
            if (obj == null) break;
            i = open + obj.Length;
            try
            {
                using var doc = JsonDocument.Parse(obj);
                Add(doc.RootElement);
            }
            catch (JsonException) { /* skip a malformed block */ }
        }

        return picks.Values.ToList();
    }

    /// <summary>The bracketed value starting at s[start] (which must be the opening bracket), or null when unbalanced.</summary>
    private static string ExtractBalanced(string s, int start, char open, char close)
    {
        int depth = 0;
        bool inString = false, escaped = false;
        for (int i = start; i < s.Length; i++)
        {
            char c = s[i];
            if (inString)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') inString = false;
            }
            else
            {
                if (c == '"') inString = true;
                else if (c == open) depth++;
                else if (c == close && --depth == 0) return s[start..(i + 1)];
            }
        }
        return null;
    }

    private static async Task<string> GetAsync(string url, CancellationToken ct = default)
    {
        for (int attempt = 0;; attempt++)
        {
            using var res = await Http.GetAsync(url, ct);
            if ((int)res.StatusCode == 429 && attempt < 5)
            {
                await Task.Delay(1000 * (attempt + 1), ct);
                continue;
            }
            res.EnsureSuccessStatusCode();
            return await res.Content.ReadAsStringAsync(ct);
        }
    }

    private static string Str(JsonElement o, string name) =>
        o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int Int(JsonElement o, string name)
    {
        if (!o.TryGetProperty(name, out var v)) return -1;
        if (v.ValueKind == JsonValueKind.Number) return v.GetInt32();
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out int n)) return n;
        return -1;
    }

    private static bool HasActiveCondition(JsonElement o) =>
        o.TryGetProperty("active_draft_pick_condition", out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString());
}
