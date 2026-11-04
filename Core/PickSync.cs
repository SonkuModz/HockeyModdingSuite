namespace NHL27Kit.Core;

/// <summary>One draft pick whose owner differs between the game and PuckPedia, and what applying it does.</summary>
public sealed class PickSyncChange
{
    /// <summary>Stored year: calendar draft year - 1900 (127 = 2027 draft).</summary>
    public int Year { get; init; }
    public int Round { get; init; }
    public int OriginalTeam { get; init; }
    public int FromOwner { get; init; }
    public int ToOwner { get; init; }
    /// <summary>exhibitiondraftpick row, so Apply writes exactly this pick.</summary>
    public int Row { get; init; }
    public string OriginalAbbr { get; init; }
    public string OriginalTeamName { get; init; }
    public string FromAbbr { get; init; }
    public string ToAbbr { get; init; }
    public bool Selected { get; set; } = true;

    public bool CanApply => Row >= 0 && ToOwner >= 0;
    /// <summary>What applying does, in words.</summary>
    public string Description => $"{FromAbbr} → {ToAbbr}";
}

/// <summary>The outcome of comparing the game's picks with PuckPedia's.</summary>
public sealed class PickCompareResult
{
    public List<PickSyncChange> Changes { get; init; }
    /// <summary>Game picks in the synced years that had a PuckPedia match to compare against.</summary>
    public int Compared { get; init; }
    /// <summary>Picks with a live condition on PuckPedia: excluded from sync changes entirely, never auto-proposed.</summary>
    public int SkippedConditional { get; init; }
    /// <summary>PuckPedia picks in the synced years with no matching (year, round, original team) game row: never auto-applied.</summary>
    public int NoGameRow { get; init; }
    /// <summary>Game picks in the synced years with no PuckPedia match (2031 is not listed yet, for example).</summary>
    public int NoPuckPediaPick { get; init; }
    /// <summary>PuckPedia picks whose original or owner slug isn't in the known team map: skipped, never auto-applied.</summary>
    public int SkippedUnmapped { get; init; }
}

/// <summary>
/// Draft pick ownership sync against PuckPedia.
///
/// The game stores picks in exhibitiondraftpick: one row per (year, round, origteamid), 1344 rows = 32 teams x 7 rounds
/// x 6 years. The year is the calendar draft year - 1900 (126 = 2026 draft, already completed, never synced;
/// 127 = 2027 through 131 = 2031). There is no condition field — the game cannot represent conditional picks — so
/// any pick PuckPedia flags with a live active_draft_pick_condition is excluded from sync changes entirely, per the
/// user's rule. Applying writes only the new owner into exhibitiondraftpick; there is no online/custom copy of the
/// picks table, so nothing is mirrored, and player rows are never touched.
/// </summary>
public static class PickSync
{
    public const string Table = "exhibitiondraftpick";
    /// <summary>First draft year the sync touches (stored year 127 = 2027 draft). 126 is the completed 2026 draft.</summary>
    public const int FirstSyncYear = 127;
    /// <summary>Last draft year the game stores (stored year 131 = 2031 draft). PuckPedia lists none of these yet.</summary>
    public const int LastSyncYear = 131;

    public static string Ordinal(int round) => round switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => $"{round}th" };

    public static PickCompareResult Compare(T3Db db, IReadOnlyList<PuckPediaPick> picks)
    {
        var index = RosterSync.TeamIndex(db); // NHL abbreviation -> game team index
        var slugToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (slug, abbr) in PuckPediaPicks.Teams)
            if (index.TryGetValue(abbr, out int i)) slugToIndex[slug] = i;
        var teams = TeamNames(db);

        var t = db.Table(Table);
        var f = t.Fields.ToDictionary(x => x.Name);
        var game = new Dictionary<(int Year, int Round, int Orig), (int Row, int Owner)>();
        for (int r = 0; r < t.WrittenRecords; r++)
        {
            if (db.IsInvalid(t, r)) continue;
            int year = (int)db.Get(t, r, f["year"]);
            if (year < FirstSyncYear || year > LastSyncYear) continue;
            game[(year, (int)db.Get(t, r, f["round"]), (int)db.Get(t, r, f["origteamid"]))] = (r, (int)db.Get(t, r, f["owner"]));
        }

        var changes = new List<PickSyncChange>();
        var seen = new HashSet<(int, int, int)>();
        int conditional = 0, noGameRow = 0, unmapped = 0, compared = 0;
        foreach (var p in picks)
        {
            if (!slugToIndex.TryGetValue(p.OriginalSlug, out int orig) || !slugToIndex.TryGetValue(p.OwnerSlug, out int owner))
            {
                unmapped++;
                continue;
            }
            if (p.StoredYear < FirstSyncYear || p.StoredYear > LastSyncYear) continue;
            var key = (p.StoredYear, p.Round, orig);
            if (!game.TryGetValue(key, out var g))
            {
                noGameRow++;
                continue;
            }
            seen.Add(key);
            if (p.HasActiveCondition)
            {
                conditional++;
                continue;
            }
            compared++;
            if (g.Owner == owner) continue;
            teams.TryGetValue(orig, out var ot);
            teams.TryGetValue(g.Owner, out var ft);
            teams.TryGetValue(owner, out var tt);
            changes.Add(new PickSyncChange
            {
                Year = p.StoredYear, Round = p.Round, OriginalTeam = orig, FromOwner = g.Owner, ToOwner = owner, Row = g.Row,
                OriginalAbbr = ot.Abbr ?? "?", OriginalTeamName = ot.Name ?? "?",
                FromAbbr = g.Owner < 0 ? "—" : ft.Abbr ?? "?", ToAbbr = tt.Abbr ?? "?",
            });
        }

        changes.Sort((a, b) => a.Year != b.Year ? a.Year.CompareTo(b.Year) : a.Round != b.Round ? a.Round.CompareTo(b.Round) : a.OriginalTeam.CompareTo(b.OriginalTeam));
        return new PickCompareResult
        {
            Changes = changes, Compared = compared, SkippedConditional = conditional,
            NoGameRow = noGameRow, NoPuckPediaPick = game.Keys.Count(k => !seen.Contains(k)), SkippedUnmapped = unmapped,
        };
    }

    /// <summary>
    /// Writes the new owner for each change into exhibitiondraftpick. That is the only picks table — there is no
    /// online/custom copy, so nothing is mirrored, and player rows are never touched.
    /// </summary>
    public static string Apply(T3Db db, IEnumerable<PickSyncChange> changes)
    {
        using var batch = db.Batch();
        var t = db.Table(Table);
        var f = t.Fields.ToDictionary(x => x.Name);
        int n = 0;
        foreach (var c in changes)
        {
            if (!c.CanApply) continue;
            // guard against a stale row: the row must still be this pick
            if ((int)db.Get(t, c.Row, f["year"]) != c.Year || (int)db.Get(t, c.Row, f["round"]) != c.Round
                || (int)db.Get(t, c.Row, f["origteamid"]) != c.OriginalTeam) continue;
            db.Set(t, c.Row, f["owner"], c.ToOwner);
            n++;
        }
        return $"Draft picks: {n} owner{(n == 1 ? "" : "s")} updated";
    }

    private static Dictionary<int, (string Name, string Abbr)> TeamNames(T3Db db)
    {
        var t = db.Table(Roster.TeamsTable);
        var f = t.Fields.ToDictionary(x => x.Name);
        var map = new Dictionary<int, (string, string)>();
        for (int r = 0; r < t.WrittenRecords; r++)
        {
            if (db.IsInvalid(t, r)) continue;
            map[(int)db.Get(t, r, f["index"])] = ((string)db.Get(t, r, f["teamname"]), (string)db.Get(t, r, f["abbrname"]));
        }
        return map;
    }
}
