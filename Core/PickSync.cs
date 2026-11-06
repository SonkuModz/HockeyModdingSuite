namespace NHL27Kit.Core;

/// <summary>One draft pick whose owner differs between the game and real life (CapWages), and what applying it does.</summary>
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

/// <summary>The outcome of comparing the game's picks with the real-life owners.</summary>
public sealed class PickCompareResult
{
    public List<PickSyncChange> Changes { get; init; }
    /// <summary>Game picks in the synced years that had a real-life owner to compare against.</summary>
    public int Compared { get; init; }
    /// <summary>Real-life picks with no matching (year, round, original team) game row: never auto-applied.</summary>
    public int NoGameRow { get; init; }
    /// <summary>Game picks in the synced years with no real-life owner listed (the source covers fewer years than the game stores, for example).</summary>
    public int NoSourcePick { get; init; }
    /// <summary>Real-life picks whose original or owner abbreviation isn't a game team: skipped, never auto-applied.</summary>
    public int SkippedUnmapped { get; init; }
}

/// <summary>
/// Draft pick ownership sync against the real-life owners (PickOwners, live from CapWages).
///
/// The game stores picks in exhibitiondraftpick: one row per (year, round, origteamid), 1344 rows = 32 teams x 7 rounds
/// x 6 years. The year is the calendar draft year - 1900 (126 = 2026 draft, already completed, never synced;
/// 127 = 2027 through 131 = 2031 in NHL 27). Real years arrive already mapped onto the game's (PickOwners), so
/// NHL 24 works too. The synced years are the ones the source covers — its earliest and latest mapped year —
/// never hard-coded. There is no condition field — the game cannot represent conditional picks — and conditions
/// are treated as non-existent: a conditional pick takes the owner and round the source shows. Applying writes only the new owner into exhibitiondraftpick; there is no
/// online/custom copy of the picks table, so nothing is mirrored, and player rows are never touched.
/// </summary>
public static class PickSync
{
    public const string Table = "exhibitiondraftpick";

    public static string Ordinal(int round) => round switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => $"{round}th" };

    public static PickCompareResult Compare(T3Db db, IReadOnlyList<OwnedPick> picks)
    {
        var index = RosterSync.TeamIndex(db); // NHL abbreviation -> game team index
        var teams = TeamNames(db);

        // The synced years are the ones the source covers — its earliest and latest mapped year —
        // never hard-coded: real years map through the offset for the game in hand.
        int firstSync = picks.Count == 0 ? 0 : picks.Min(p => p.StoredYear);
        int lastSync = picks.Count == 0 ? -1 : picks.Max(p => p.StoredYear);

        var t = db.Table(Table);
        var f = t.Fields.ToDictionary(x => x.Name);
        var game = new Dictionary<(int Year, int Round, int Orig), (int Row, int Owner)>();
        for (int r = 0; r < t.WrittenRecords; r++)
        {
            if (db.IsInvalid(t, r)) continue;
            int year = (int)db.Get(t, r, f["year"]);
            if (year < firstSync || year > lastSync) continue;
            game[(year, (int)db.Get(t, r, f["round"]), (int)db.Get(t, r, f["origteamid"]))] = (r, (int)db.Get(t, r, f["owner"]));
        }

        var changes = new List<PickSyncChange>();
        var seen = new HashSet<(int, int, int)>();
        int noGameRow = 0, unmapped = 0, compared = 0;
        foreach (var p in picks)
        {
            if (!index.TryGetValue(p.OriginalAbbr, out int orig) || !index.TryGetValue(p.OwnerAbbr, out int owner))
            {
                unmapped++;
                continue;
            }
            var key = (p.StoredYear, p.Round, orig);
            if (!game.TryGetValue(key, out var g))
            {
                noGameRow++;
                continue;
            }
            seen.Add(key);
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
            Changes = changes, Compared = compared,
            NoGameRow = noGameRow, NoSourcePick = game.Keys.Count(k => !seen.Contains(k)), SkippedUnmapped = unmapped,
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
