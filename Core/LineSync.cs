namespace NHL27Kit.Core;

/// <summary>One NHL team's lines: what the game has, what DailyFaceoff's combinations make them, and why they can't be written yet.</summary>
public sealed class LineChange
{
    public string TeamAbbr { get; init; }
    public int Team { get; init; } = -1;
    public string TeamName { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string SourceName { get; init; }
    public string Starter { get; init; }
    /// <summary>Slot → playerindex (-1 nobody), now and after applying.</summary>
    public Dictionary<string, int> Old { get; init; } = new();
    public Dictionary<string, int> New { get; init; } = new();
    public int Changed { get; init; }
    public List<string> Dressed { get; init; } = new();
    public List<string> Scratched { get; init; } = new();
    /// <summary>Players in DailyFaceoff's lineup who aren't on the team in the game (yet: roster moves and new players go in first).</summary>
    public List<string> NotOnTeam { get; init; } = new();
    /// <summary>Why the team's lines couldn't be read at all (page failed, team not in the game); null when they were.</summary>
    public string Error { get; init; }
    public bool Selected { get; set; }

    public bool CanApply => Error == null && Team >= 0;

    public string Description =>
        Error != null ? $"lines couldn't be read: {Error}" :
        NotOnTeam.Count > 0 ? $"{Names(NotOnTeam)} not on the {TeamName} in the game yet; checked again after the roster changes, else this team is skipped" :
        $"{Changed} of {New.Count} line spots change" + (Dressed.Count > 0 ? $"  ·  dresses {Names(Dressed)}" : "") + (Scratched.Count > 0 ? $"  ·  scratches {Names(Scratched)}" : "");

    /// <summary>Where the lines come from, for the row's note.</summary>
    public string Source => UpdatedAt is { } u
        ? $"DailyFaceoff, updated {u.ToLocalTime():MMM d, h:mm tt}" + (string.IsNullOrEmpty(SourceName) ? "" : $" ({SourceName})") + (Starter != null ? $"  ·  starter {Starter}" : "")
        : "DailyFaceoff";

    private static string Names(List<string> n) => n.Count <= 4 ? string.Join(", ", n) : string.Join(", ", n.Take(4)) + $" +{n.Count - 4}";
}

/// <summary>
/// Sets NHL teams' lines from DailyFaceoff's line combinations. DailyFaceoff gives the even-strength lines (F1-F4: LW C
/// RW; D1-D3: LD RD), both goalies (G1 starts), the two power-play units (5 skaters) and two penalty-kill units (4
/// skaters). They go into the game's l1-l4, g1/g2 (and the 3-on-3 goalies), pp_1/pp_2 and pk4_1/pk4_2 slots; a special
/// teams unit's skaters are placed on its LW/C/RW/LD/RD spots by their positions in the game. Everything DailyFaceoff
/// doesn't list (4-on-3, third PK unit, 3-on-3, 3-on-4 and 4-on-4 units, shootout) is filled the way the game's own
/// lines are built (RosterLines.Plan). Players are found on the team's roster in the game by name, with the last name
/// and jersey number as a fallback (DailyFaceoff has no birth dates). If any player in the lineup isn't on the team,
/// that team's lines are left alone and reported; the other teams still go in.
/// </summary>
public static class LineSync
{
    private sealed record TeamPlayer(int PlayerIndex, RosterPlayer Player, int Jersey);

    public static List<LineChange> Compare(T3Db db, IReadOnlyList<(string Abbr, LineTeam Team, string Error)> teams)
    {
        var codes = RosterSync.TeamIndex(db);
        var roster = new Roster(db);
        var changes = new List<LineChange>();
        foreach (var (abbr, lt, error) in teams)
        {
            int team = codes.GetValueOrDefault(abbr, -1);
            string name = team >= 0 && roster.Teams.TryGetValue(team, out var tm) && !string.IsNullOrEmpty(tm.Name) ? tm.Name : abbr;
            if (error != null || lt == null || team < 0)
            {
                changes.Add(new LineChange { TeamAbbr = abbr, Team = team, TeamName = name, Error = error ?? (team < 0 ? "team not in the game" : "no lines") });
                continue;
            }
            var c = Plan(db, roster, team, name, lt);
            if (c.NotOnTeam.Count == 0 && c.Changed == 0) continue;
            c.Selected = c.CanApply;
            changes.Add(c);
        }
        return changes.OrderBy(c => c.TeamName).ToList();
    }

    /// <summary>Writes the ticked teams' lines, planned again from the database as it is now (after the roster changes and
    /// new players). A team with a lineup player not on it in the game is skipped and named in the summary.</summary>
    public static string Apply(T3Db db, IReadOnlyList<(string Abbr, LineTeam Team, string Error)> teams, ISet<string> picked)
    {
        using var batch = db.Batch();
        var fresh = Compare(db, teams.Where(t => picked.Contains(t.Abbr)).ToList());
        int written = 0;
        var failed = new List<string>();
        foreach (var c in fresh)
        {
            if (!c.CanApply) { failed.Add($"{c.TeamAbbr} ({c.Error})"); continue; }
            if (c.NotOnTeam.Count > 0) { failed.Add($"{c.TeamAbbr} ({string.Join(", ", c.NotOnTeam)} not on the team)"); continue; }
            try { RosterLines.WriteTeam(db, c.Team, c.New); written++; }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException) { failed.Add($"{c.TeamAbbr} ({e.Message})"); }
        }
        return $"Lines: {written} team{(written == 1 ? "" : "s")} set from DailyFaceoff"
            + (failed.Count > 0 ? $"; not set for {string.Join("; ", failed)}" : "");
    }

    private static LineChange Plan(T3Db db, Roster roster, int team, string teamName, LineTeam lt)
    {
        var players = TeamPlayers(db, roster, team);
        var notOnTeam = new List<string>();
        var found = new Dictionary<LineSpot, TeamPlayer>();
        foreach (var s in lt.Spots.Where(s => s.Category is "ev" or "pp" or "pk"))
        {
            var p = Find(players, s);
            if (p != null) found[s] = p;
            else if (!notOnTeam.Contains(s.Name)) notOnTeam.Add(s.Name);
        }

        var fixedSlots = new Dictionary<string, int>();
        var usedIn = new Dictionary<string, HashSet<int>>();
        void Fix(string slot, TeamPlayer p)
        {
            string cat = RosterLines.Category(slot);
            if (!usedIn.TryGetValue(cat, out var used)) usedIn[cat] = used = new HashSet<int>();
            if (fixedSlots.ContainsKey(slot) || !used.Add(p.PlayerIndex)) return; // one player per category, one per slot
            fixedSlots[slot] = p.PlayerIndex;
        }
        foreach (var (s, p) in found.Where(x => x.Key.Category == "ev"))
        {
            if (s.Group.Length == 2 && s.Group[0] is 'f' or 'd' && char.IsDigit(s.Group[1]) && s.Position is "lw" or "c" or "rw" or "ld" or "rd")
                Fix($"l{s.Group[1]}{s.Position}", p);
            else if (s.Group == "g" && s.Position is "g1" or "g2")
            {
                Fix(s.Position, p);
                Fix($"three_{s.Position}", p);
            }
        }
        foreach (var unit in found.Where(x => x.Key.Category is "pp" or "pk").GroupBy(x => x.Key.Group))
        {
            string g = unit.Key;
            if (g.Length != 3 || !char.IsDigit(g[2])) continue;
            var ends = g.StartsWith("pp") ? new[] { "c", "lw", "rw", "ld", "rd" } : new[] { "c", "lw", "ld", "rd" };
            string prefix = g.StartsWith("pp") ? $"pp_{g[2]}" : $"pk4_{g[2]}";
            var members = unit.OrderBy(x => x.Key.Position, StringComparer.Ordinal).Select(x => x.Value).DistinctBy(p => p.PlayerIndex).Take(ends.Length).ToList();
            foreach (var (end, p) in Assign(ends, members)) Fix(prefix + end, p);
        }

        // everyone in the even-strength lineup is dressed, even without a game slot (a 7th D, listed as D4)
        var lineup = found.Where(x => x.Key.Category == "ev").Select(x => x.Value.PlayerIndex);
        var plan = RosterLines.Plan(db, team, fixedSlots, lineup);
        var (_, old) = RosterLines.ReadTeam(db, team);
        var nameOf = players.ToDictionary(p => p.PlayerIndex, p => p.Player.Name);
        var before = old.Values.Where(v => v >= 0).ToHashSet();
        var after = plan.Values.Where(v => v >= 0).ToHashSet();
        var g1 = found.FirstOrDefault(x => x.Key.Category == "ev" && x.Key.Position == "g1").Value;
        return new LineChange
        {
            TeamAbbr = lt.Abbr, Team = team, TeamName = teamName, UpdatedAt = lt.UpdatedAt, SourceName = lt.SourceName,
            Starter = g1?.Player.Name, Old = old, New = plan,
            Changed = plan.Count(kv => old.GetValueOrDefault(kv.Key, -1) != kv.Value),
            Dressed = after.Except(before).Select(pi => nameOf.GetValueOrDefault(pi, $"player {pi}")).ToList(),
            Scratched = before.Except(after).Select(pi => nameOf.GetValueOrDefault(pi, $"player {pi}")).ToList(),
            NotOnTeam = notOnTeam,
        };
    }

    /// <summary>The team's players in the game (the roster copy the game plays with), with their bio and jersey.</summary>
    private static List<TeamPlayer> TeamPlayers(T3Db db, Roster roster, int team)
    {
        var players = db.Table(Roster.PlayersTable);
        var pf = players.Fields.ToDictionary(x => x.Name);
        var idOf = new Dictionary<int, int>();
        for (int r = 0; r < players.WrittenRecords; r++)
            if (!db.IsInvalid(players, r)) idOf.TryAdd((int)db.Get(players, r, pf["index"]), (int)db.Get(players, r, pf["playerid"]));
        var byId = roster.Players.GroupBy(p => p.GameId).ToDictionary(g => g.Key, g => g.First());
        var t = db.Table(RosterSync.OnlineRosterTable) ?? db.Table(Roster.RosterTable);
        var f = t.Fields.ToDictionary(x => x.Name);
        var list = new List<TeamPlayer>();
        for (int r = 0; r < t.WrittenRecords; r++)
        {
            if (db.IsInvalid(t, r) || (int)db.Get(t, r, f["team"]) != team) continue;
            int pi = (int)db.Get(t, r, f["playerindex"]);
            if (idOf.TryGetValue(pi, out int id) && byId.TryGetValue(id, out var p)) list.Add(new TeamPlayer(pi, p, (int)db.Get(t, r, f["jersey"])));
        }
        return list;
    }

    /// <summary>
    /// A lineup name on the team: the full name (accents, punctuation and spacing ignored), the jersey number deciding
    /// between two; else a player whose last name ends the listed name ("Matt Boldy" / "Matthew Boldy") with the same
    /// number, or the only one with that last name and first initial. Null when none.
    /// </summary>
    private static TeamPlayer Find(List<TeamPlayer> players, LineSpot s)
    {
        string full = RosterSync.Norm(s.Name);
        var exact = players.Where(p => RosterSync.Norm(p.Player.FirstName) + RosterSync.Norm(p.Player.LastName) == full).ToList();
        if (exact.Count == 1) return exact[0];
        if (exact.Count > 1) return exact.FirstOrDefault(p => p.Jersey == s.Number) ?? exact[0];
        var byLast = players.Where(p => RosterSync.Norm(p.Player.LastName) is { Length: > 0 } l && full.EndsWith(l)).ToList();
        if (s.Number >= 0 && byLast.Where(p => p.Jersey == s.Number).ToList() is { Count: 1 } num) return num[0];
        char initial = full.Length > 0 ? full[0] : ' ';
        var byInitial = byLast.Where(p => RosterSync.Norm(p.Player.FirstName).StartsWith(initial)).ToList();
        return byInitial.Count == 1 ? byInitial[0] : null;
    }

    /// <summary>
    /// Places a special-teams unit's skaters on its spots (c, lw, rw, ld, rd) by their positions in the game, trying
    /// every arrangement: a player's primary position costs 0, his secondary 1, the same group (forward or D) 2,
    /// anything else 4; the cheapest arrangement wins, and of equal ones, the one with the best-rated player at C (he
    /// takes the faceoffs), then DailyFaceoff's order.
    /// </summary>
    private static List<(string End, TeamPlayer Player)> Assign(string[] ends, List<TeamPlayer> members)
    {
        static int Pos(string end) => end switch { "c" => 0, "lw" => 1, "rw" => 2, "ld" => 3, _ => 4 };
        static bool D(int pos) => pos is 3 or 4;
        int Cost(string end, TeamPlayer p)
        {
            int want = Pos(end), prim = p.Player.PositionPrimary, sec = p.Player.PositionSecondary;
            if (prim == want) return 0;
            if (sec == want) return 1;
            return D(prim) == D(want) && prim is >= 0 and <= 4 ? 2 : 4;
        }
        // each member gets a distinct spot (a short unit leaves the worst-fitting spots open for RosterLines.Plan)
        int n = Math.Min(members.Count, ends.Length);
        int best = int.MaxValue;
        int[] bestSpot = null;
        var spotOf = new int[n];
        var taken = new bool[ends.Length];
        int cAt = Array.IndexOf(ends, "c");
        void Try(int m, int cost)
        {
            if (cost >= best) return;
            if (m == n)
            {
                int atC = Array.IndexOf(spotOf, cAt);
                int total = cost + (cAt >= 0 && atC >= 0 ? 99 - members[atC].Player.Overall : 0);
                if (total < best) { best = total; bestSpot = (int[])spotOf.Clone(); }
                return;
            }
            for (int s = 0; s < ends.Length; s++)
            {
                if (taken[s]) continue;
                taken[s] = true; spotOf[m] = s;
                Try(m + 1, cost + 1000 * Cost(ends[s], members[m]));
                taken[s] = false;
            }
        }
        Try(0, 0);
        return bestSpot == null ? new() : Enumerable.Range(0, n).Select(m => (ends[bestSpot[m]], members[m])).ToList();
    }
}
