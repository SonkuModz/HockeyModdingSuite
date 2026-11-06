namespace NHL27Kit.Core;

/// <summary>One draft pick: a row of exhibitiondraftpick.</summary>
public sealed record DraftPick(int Year, int Round, int Overall, int Owner, int OriginalTeam);

/// <summary>
/// The draft picks of nhl_game.db (exhibitiondraftpick), read-only. One row per pick: (year, round, original
/// team) identifies it, and owner is the team that holds it now. Years are calendar draft years minus 1900
/// (126 = the 2026 draft, which already happened; 127 = 2027, up to 131 = 2031 in the shipped NHL 27
/// database). The game stores no conditions on picks, so none are shown anywhere. The years are read from
/// the table, never hard-coded — the earliest one is the completed draft, and other game versions store
/// different years.
/// </summary>
public sealed class DraftPicks
{
    public const string Table = "exhibitiondraftpick";

    /// <summary>The earliest stored draft year in the game (the completed draft); -1 when no picks were read.</summary>
    public int FirstYear => Picks.Count == 0 ? -1 : Picks.Min(p => p.Year);

    public List<DraftPick> Picks { get; } = new();

    public DraftPicks(T3Db db)
    {
        var t = db.Table(Table);
        if (t == null) return;
        foreach (int r in Rows(db, t))
            Picks.Add(new DraftPick(Int(db, t, r, "year"), Int(db, t, r, "round"), Int(db, t, r, "pickoverall"),
                                    Int(db, t, r, "owner"), Int(db, t, r, "origteamid")));
    }

    /// <summary>The earliest stored draft year in exhibitiondraftpick, or -1 when the table is missing or empty. The picks tab and the CapWages pick sync both derive their years from this, never from a hard-coded year.</summary>
    public static int FirstStoredYear(T3Db db)
    {
        var t = db.Table(Table);
        if (t == null) return -1;
        int min = int.MaxValue;
        foreach (int r in Rows(db, t))
        {
            int y = Int(db, t, r, "year");
            if (y < min) min = y;
        }
        return min == int.MaxValue ? -1 : min;
    }

    /// <summary>The team's original picks: one per (year, round), in draft order.</summary>
    public IEnumerable<DraftPick> OriginalsOf(int team) => Picks.Where(p => p.OriginalTeam == team)
        .OrderBy(p => p.Year).ThenBy(p => p.Round);

    /// <summary>The picks the team holds now, including ones it acquired from other teams.</summary>
    public IEnumerable<DraftPick> OwnedBy(int team) => Picks.Where(p => p.Owner == team)
        .OrderBy(p => p.Year).ThenBy(p => p.Round);

    /// <summary>The draft years stored in the game, earliest first (2026–2031 in the shipped database).</summary>
    public List<int> Years => Picks.Select(p => p.Year).Distinct().OrderBy(y => y).ToList();

    private static IEnumerable<int> Rows(T3Db db, T3Table t)
    {
        for (int r = 0; r < t.WrittenRecords; r++)
            if (!db.IsInvalid(t, r)) yield return r;
    }

    private static int Int(T3Db db, T3Table t, int r, string f) => Roster.Field(t, f) is { } x ? (int)db.Get(t, r, x) : 0;
}
