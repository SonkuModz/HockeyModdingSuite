using System.Text.Json;

namespace NHL27Kit.Core;

/// <summary>One draft pick as the snapshot file lists it, mapped onto the game's draft years.</summary>
public sealed record PuckPediaPick(string Id, int DraftYear, int Round, string OriginalSlug, string OwnerSlug, bool HasActiveCondition)
{
    /// <summary>The game's exhibitiondraftpick year: calendar draft year - 1900 (127 = 2027 draft).</summary>
    public int StoredYear => DraftYear - 1900;
}

/// <summary>
/// Draft pick ownership from the bundled draft-picks.json file next to the exe — a static snapshot of
/// PuckPedia's team pages (see PuckPedia/README.md for the pull date). There is no live fetch: the site
/// refuses the app's requests (Cloudflare, HTTP 403), so the checkbox reads this file instead, and the
/// pulling code (live HttpClient fetch, retries, saved HTML pages) was removed. Refresh the data by
/// replacing the file next to the exe, or the repo copy it is copied from.
///
/// The file lists picks by REAL calendar year, and the game stores its own first draft year (2026 for
/// NHL 27, 2023 for NHL 24) — so each pick is mapped through the offset (game first year − snapshot
/// year) before comparing. Conditions are treated as non-existent: the file carries no condition flags,
/// and a conditional pick is just a pick — its listed owner and round stand.
/// </summary>
public static class PickSnapshot
{
    /// <summary>The snapshot file, read from next to the running exe (HMS.exe or nhl27kit.exe) — never a hard-coded drive or user path.</summary>
    public static string FileName => "draft-picks.json";
    public static string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

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

    private static readonly Dictionary<string, string> AbbrToSlug =
        Teams.ToDictionary(t => t.Abbr, t => t.Slug, StringComparer.OrdinalIgnoreCase);

    /// <summary>One pick in the snapshot file: real calendar year, round, original and current-owner abbreviations.</summary>
    public sealed record SnapshotPick(int RealYear, int Round, string OriginalAbbr, string OwnerAbbr);

    /// <summary>The parsed snapshot file: when it was pulled, which draft year was current then, and its picks.</summary>
    public sealed class SnapshotFile
    {
        public string AsOf { get; init; }
        public int SnapshotYear { get; init; }
        public List<SnapshotPick> Picks { get; init; }
    }

    /// <summary>Reads the snapshot file next to the running exe. Returns the file, or null plus the reason it could not be used — a missing file is reported, never a silent empty result.</summary>
    public static (SnapshotFile File, string Error) Load()
    {
        string path = FilePath;
        if (!File.Exists(path))
            return (null, $"draft-picks.json not found next to the exe (looked in {path}). It ships with the app — copy it beside the exe.");
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            if (!root.TryGetProperty("snapshotYear", out var y) || y.ValueKind != JsonValueKind.Number)
                return (null, "draft-picks.json has no snapshotYear — it is not a pick snapshot file.");
            if (!root.TryGetProperty("picks", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return (null, "draft-picks.json has no picks list — it is not a pick snapshot file.");
            string asOf = root.TryGetProperty("asOf", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() : "?";
            var picks = new List<SnapshotPick>();
            foreach (var o in arr.EnumerateArray())
            {
                if (!o.TryGetProperty("y", out var yy) || yy.ValueKind != JsonValueKind.Number) continue;
                if (!o.TryGetProperty("r", out var rr) || rr.ValueKind != JsonValueKind.Number) continue;
                string orig = Str(o, "orig"), owner = Str(o, "owner");
                if (orig == null || owner == null) continue;
                picks.Add(new SnapshotPick(yy.GetInt32(), rr.GetInt32(), orig, owner));
            }
            return (new SnapshotFile { AsOf = asOf, SnapshotYear = y.GetInt32(), Picks = picks }, null);
        }
        catch (Exception ex)
        {
            return (null, "draft-picks.json could not be read: " + ShortDetail(ex.Message));
        }
    }

    /// <summary>
    /// Maps the snapshot's picks onto the game's draft years: a real year Y lands in stored year
    /// Y + offset − 1900, where offset = the game's first draft year − the snapshot year (0 for NHL 27,
    /// −3 for NHL 24). Returns the picks, the game's first draft year (−1 when the picks table is
    /// missing or empty, and the offset is then 0), and the offset. Abbreviations become slugs through
    /// the Teams table; an unknown abbreviation keeps its own text as the slug, so the compare counts it
    /// unmapped instead of crashing.
    /// </summary>
    public static (List<PuckPediaPick> Picks, int GameFirstYear, int Offset) ToPicks(SnapshotFile file, T3Db db)
    {
        int firstStored = DraftPicks.FirstStoredYear(db);
        int gameFirstYear = firstStored < 0 ? -1 : firstStored + 1900;
        int offset = gameFirstYear < 0 ? 0 : gameFirstYear - file.SnapshotYear;
        var byKey = new Dictionary<(int, int, string), PuckPediaPick>();
        foreach (var p in file.Picks)
        {
            string origSlug = AbbrToSlug.TryGetValue(p.OriginalAbbr, out var os) ? os : p.OriginalAbbr;
            string ownerSlug = AbbrToSlug.TryGetValue(p.OwnerAbbr, out var ws) ? ws : p.OwnerAbbr;
            var pick = new PuckPediaPick($"{p.RealYear}-R{p.Round}-{p.OriginalAbbr}", p.RealYear + offset, p.Round, origSlug, ownerSlug, HasActiveCondition: false);
            byKey.TryAdd((pick.StoredYear, pick.Round, origSlug), pick);
        }
        return (byKey.Values.ToList(), gameFirstYear, offset);
    }

    private static string Str(JsonElement o, string name) =>
        o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string ShortDetail(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "";
        string one = message.Replace("\r", " ").Replace("\n", " ").Trim();
        if (one.Length > 80) one = one[..77] + "…";
        return ": " + one;
    }
}
