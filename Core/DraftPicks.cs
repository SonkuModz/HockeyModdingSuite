namespace NHL27Kit.Core;

/// <summary>One draft pick: a row of exhibitiondraftpick.</summary>
public sealed record DraftPick(int Year, int Round, int Overall, int Owner, int OriginalTeam);

/// <summary>
/// The draft picks of nhl_game.db (exhibitiondraftpick), read-only. One row per pick: (year, round, original
/// team) identifies it, and owner is the team that holds it now. Years are calendar draft years minus 1900
/// (126 = the 2026 draft, which already happened; 127 = 2027, up to 131 = 2031). The game stores no
/// conditions on picks, so none are shown anywhere.
/// </summary>
public sealed class DraftPicks
{
    public const string Table = "exhibitiondraftpick";

    /// <summary>The stored 2026 draft already happened: shown, but de-emphasised in the tab.</summary>
    public const int CompletedYear = 126;

    public List<DraftPick> Picks { get; } = new();

    public DraftPicks(T3Db db)
    {
        var t = db.Table(Table);
        if (t == null) return;
        foreach (int r in Rows(db, t))
            Picks.Add(new DraftPick(Int(db, t, r, "year"), Int(db, t, r, "round"), Int(db, t, r, "pickoverall"),
                                    Int(db, t, r, "owner"), Int(db, t, r, "origteamid")));
    }

    /// <summary>The team's original picks: one per (year, round), in draft order.</summary>
    public IEnumerable<DraftPick> OriginalsOf(int team) => Picks.Where(p => p.OriginalTeam == team)
        .OrderBy(p => p.Year).ThenBy(p => p.Round);

    /// <summary>The picks the team holds now, including ones it acquired from other teams.</summary>
    public IEnumerable<DraftPick> OwnedBy(int team) => Picks.Where(p => p.Owner == team)
        .OrderBy(p => p.Year).ThenBy(p => p.Round);

    /// <summary>The draft years stored in the game, earliest first (2026–2031 in the shipped database).</summary>
    public List<int> Years => Picks.Select(p => p.Year).Distinct().OrderBy(y => y).ToList();

    private IEnumerable<int> Rows(T3Db db, T3Table t)
    {
        for (int r = 0; r < t.WrittenRecords; r++)
            if (!db.IsInvalid(t, r)) yield return r;
    }

    private int Int(T3Db db, T3Table t, int r, string f) => Roster.Field(t, f) is { } x ? (int)db.Get(t, r, x) : 0;
}
