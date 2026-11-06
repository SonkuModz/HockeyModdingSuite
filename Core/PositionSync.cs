namespace NHL27Kit.Core;

/// <summary>One player whose primary/secondary positions differ from where CapWages says he plays.</summary>
public sealed class PositionChange
{
    public int GameId { get; init; } = -1;
    public int BioRow { get; init; } = -1;
    public string Name { get; init; }
    public string TeamAbbr { get; init; }
    public int OldPrimary { get; init; }
    public int OldSecondary { get; init; }
    public int NewPrimary { get; init; }
    public int NewSecondary { get; init; }
    /// <summary>What CapWages lists, for the row's note ("CapWages: LW, RW · games LW 96, RW 46, C 3").</summary>
    public string Source { get; init; }
    public bool NameOnly { get; init; }
    public bool Selected { get; set; }

    public bool CanApply => BioRow >= 0;
    /// <summary>A forward becoming a defenceman or the other way round.</summary>
    public bool CrossesGroup => PositionSync.IsDefence(OldPrimary) != PositionSync.IsDefence(NewPrimary);
    public string Description => $"{Roster.PositionDisplay(OldPrimary, OldSecondary)} → {Roster.PositionDisplay(NewPrimary, NewSecondary)}"
        + (CrossesGroup ? "  ·  changes between forward and defence: check it" : "");
}

/// <summary>
/// Players' positions from CapWages. Each player's CapWages entry lists the positions he has played ("pos": "LW, RW")
/// and how many games at each; that becomes the game's primary and secondary position (bio positionprimary,
/// positionsecondary, and the collapsed legacy position). The positions are ordered by games played. A player CapWages
/// lists at one position keeps a sensible second one from the game: his old secondary, or else his old primary, as
/// long as it's in the same group (forward or defence). Goalies and players with no positions on CapWages are left alone.
/// Players on AHL contracts come from each team page's AHL Contracts table, which gives one position (or a bare "F",
/// which is skipped).
/// Game position codes: 0 C, 1 LW, 2 RW, 3 LD, 4 RD, 5 G.
/// </summary>
public static class PositionSync
{
    public static bool IsDefence(int p) => p is 3 or 4;

    private static int Code(string s) => s.Trim().ToUpperInvariant() switch
    {
        "C" => 0, "LW" => 1, "RW" => 2, "LD" => 3, "RD" => 4, _ => -1,
    };

    /// <summary>The primary and secondary CapWages gives (secondary -1 when it lists only one), or null when it gives none the game can use.</summary>
    public static (int Primary, int Secondary)? Listed(CapPlayer cp)
    {
        var listed = (cp.Pos ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => Code(s) >= 0).ToList();
        if (listed.Count == 0) return null;
        // by games played, CapWages' own order breaking ties
        var order = listed.Select((s, i) => (s, i)).OrderByDescending(x => cp.PositionGames.GetValueOrDefault(x.s)).ThenBy(x => x.i).Select(x => Code(x.s)).ToList();
        return (order[0], order.Count > 1 ? order[1] : -1);
    }

    /// <summary>What the game should have: CapWages' positions, with the game's own second position kept when CapWages lists one.</summary>
    public static (int Primary, int Secondary)? Target(CapPlayer cp, int oldPrimary, int oldSecondary)
    {
        if (oldPrimary == 5 || Listed(cp) is not { } l) return null;
        int secondary = l.Secondary;
        if (secondary < 0)
        {
            bool Fits(int p) => p is >= 0 and <= 4 && p != l.Primary && IsDefence(p) == IsDefence(l.Primary);
            secondary = Fits(oldSecondary) ? oldSecondary : Fits(oldPrimary) ? oldPrimary : -1;
        }
        return (l.Primary, secondary);
    }

    public static List<PositionChange> Compare(T3Db db, IReadOnlyList<CapTeam> teams)
    {
        var changes = new List<PositionChange>();
        // NHL 24's bio has only the collapsed position (no LD/RD, G is 4): nothing to set there.
        if (db.Table(Roster.BioTable).Fields.All(f => f.Name != "positionprimary")) return changes;
        var roster = new Roster(db);
        var matcher = new PlayerMatcher(db, roster);
        var seen = new HashSet<int>();
        // NHL-contract players first (they have games at each position), then the affiliate's AHL-contract players
        var entries = teams.SelectMany(t => t.Players.Select(cp => (Team: t, Player: cp)))
            .Concat(teams.SelectMany(t => t.AhlPlayers.Select(cp => (Team: t, Player: cp))));
        foreach (var (team, cp) in entries)
        {
            var (p, nameOnly) = matcher.Find(cp.First, cp.Last, cp.Born);
            if (p == null || !seen.Add(p.GameId)) continue;
            if (Target(cp, p.PositionPrimary, p.PositionSecondary) is not { } t) continue;
            if (t.Primary == p.PositionPrimary && t.Secondary == p.PositionSecondary) continue;
            var c = new PositionChange
            {
                GameId = p.GameId, BioRow = p.BioRow, Name = p.Name, TeamAbbr = team.Abbr,
                OldPrimary = p.PositionPrimary, OldSecondary = p.PositionSecondary, NewPrimary = t.Primary, NewSecondary = t.Secondary,
                NameOnly = nameOnly,
                Source = $"CapWages{(cp.Section == "ahl" ? " (AHL contract)" : "")}: {cp.Pos}" + (cp.PositionGames.Count > 0
                    ? "  ·  games " + string.Join(", ", cp.PositionGames.OrderByDescending(g => g.Value).Select(g => $"{g.Key} {g.Value}")) : ""),
            };
            // A name-only match, or a forward turned defenceman (or back), waits for a tick.
            c.Selected = !nameOnly && !c.CrossesGroup;
            changes.Add(c);
        }
        return changes.OrderBy(c => c.TeamAbbr).ThenBy(c => c.Name).ToList();
    }

    /// <summary>Writes the positions: primary, secondary and the collapsed legacy position.</summary>
    public static string Apply(T3Db db, IEnumerable<PositionChange> changes)
    {
        using var batch = db.Batch();
        var bio = db.Table(Roster.BioTable);
        var bf = bio.Fields.ToDictionary(x => x.Name);
        void Set(int row, string name, int v)
        {
            if (!bf.TryGetValue(name, out var f)) return;
            long max = f.RangeLow + (1L << f.Depth) - 1;
            if (v >= f.RangeLow && v <= max) db.Set(bio, row, f, v);
        }
        int n = 0;
        foreach (var c in changes.Where(c => c.CanApply))
        {
            Set(c.BioRow, "positionprimary", c.NewPrimary);
            Set(c.BioRow, "positionsecondary", c.NewSecondary);
            Set(c.BioRow, "position", Roster.CollapsedPosition(c.NewPrimary));
            n++;
        }
        return $"Positions: {n} player{(n == 1 ? "" : "s")} set from CapWages";
    }
}
