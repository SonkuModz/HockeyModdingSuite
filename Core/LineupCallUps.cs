namespace NHL27Kit.Core;

/// <summary>
/// Call-ups the NHL's roster lists miss. NHL.com's team rosters (/roster/{team}/current) sometimes leave out a player
/// its own player pages put on the team, with games played for it this week (2026-11-06: Trey Taylor, Philip Tomasino,
/// Sammy Blais, Arsenii Sergeev). DailyFaceoff's lineup for the team has him, so his team's lines were left alone as
/// "not on the team". After the roster changes, each lineup player who still isn't on that team in the game, isn't on
/// any NHL roster list, and is the only game player with that name becomes a call-up (or move) to the team:
/// - ticked when no AHL roster lists him and CapWages lists him under contract with that team;
/// - unticked when an AHL roster lists him (the sources disagree: often an emergency recall), or CapWages wasn't read
///   or doesn't list him with that team. Ticking it moves him, and the next check builds the lines with him.
/// The next sync's AHL compare moves him back if the AHL roster still lists him then.
/// </summary>
public static class LineupCallUps
{
    public static List<SyncChange> Compare(T3Db db, IReadOnlyList<(string Abbr, LineTeam Team, string Error)> lines,
        IReadOnlyList<NhlPlayer> api, IReadOnlyList<(AhlTeam Team, List<AhlPlayer> Players)> ahl, IReadOnlyList<CapTeam> cap)
    {
        var changes = new List<SyncChange>();
        if (lines == null) return changes;
        var codes = RosterSync.TeamIndex(db);
        var nhlTeams = codes.Values.ToHashSet();
        var roster = new Roster(db);
        var matcher = new PlayerMatcher(db, roster);
        string TeamName(int i) => roster.Teams.TryGetValue(i, out var tm) && !string.IsNullOrEmpty(tm.Name) ? tm.Name : $"team {i}";

        // who the NHL rosters place (a player NHL.com has elsewhere is left to the roster sync), and who an AHL roster has
        var onNhl = new HashSet<int>();
        foreach (var a in api ?? Array.Empty<NhlPlayer>())
            if (matcher.Find(a.First, a.Last, a.Born).Player is { } p) onNhl.Add(p.GameId);
        var onAhl = new Dictionary<int, string>();
        foreach (var (team, players) in ahl ?? Array.Empty<(AhlTeam, List<AhlPlayer>)>())
            foreach (var a in players)
                if (matcher.Find(a.First, a.Last, a.Born).Player is { } p) onAhl.TryAdd(p.GameId, team.Name);

        // each player's team and jersey as the game shows them (the online roster copy first, like the lines)
        var shown = new Dictionary<int, (int Team, int Jersey)>();
        foreach (var name in new[] { RosterSync.OnlineRosterTable, Roster.RosterTable })
        {
            var t = db.Table(name);
            if (t == null) continue;
            var f = t.Fields.ToDictionary(x => x.Name);
            for (int r = 0; r < t.WrittenRecords; r++)
                if (!db.IsInvalid(t, r)) shown.TryAdd((int)db.Get(t, r, f["playerindex"]), ((int)db.Get(t, r, f["team"]), (int)db.Get(t, r, f["jersey"])));
        }
        var rt = db.Table(Roster.RosterTable);
        var piField = rt.Fields.First(x => x.Name == "playerindex");
        var byName = roster.Players.GroupBy(p => RosterSync.Norm(p.FirstName) + RosterSync.Norm(p.LastName)).ToDictionary(g => g.Key, g => g.ToList());

        var seen = new HashSet<int>();
        foreach (var lc in LineSync.Compare(db, lines).Where(c => c.CanApply && c.NotOnTeam.Count > 0))
        {
            var lt = lines.First(l => l.Abbr == lc.TeamAbbr).Team;
            var capTeam = cap?.FirstOrDefault(t => t.Abbr == lc.TeamAbbr);
            foreach (string name in lc.NotOnTeam)
            {
                if (!byName.TryGetValue(RosterSync.Norm(name), out var same) || same.Count != 1) continue;
                var p = same[0];
                if (onNhl.Contains(p.GameId) || !seen.Add(p.GameId)) continue;
                int pi = p.RosterRow >= 0 ? (int)db.Get(rt, p.RosterRow, piField) : p.PlayerIndex;
                var (from, jersey) = shown.TryGetValue(pi, out var s) ? s : (p.Team, -1);
                if (from == lc.Team) continue;
                var spot = lt.Spots.FirstOrDefault(x => x.Name == name);
                bool onCap = capTeam?.Players.Any(cp => RosterSync.Norm(cp.First) + RosterSync.Norm(cp.Last) == RosterSync.Norm(name)) == true;
                string ahlTeam = onAhl.GetValueOrDefault(p.GameId);
                string note = $"In {lc.TeamAbbr}'s DailyFaceoff lineup, not on NHL.com's {lc.TeamAbbr} roster list  ·  "
                    + (cap == null ? "CapWages not read" : onCap ? $"CapWages: under contract with {lc.TeamAbbr}" : $"CapWages: not with {lc.TeamAbbr}")
                    + (ahlTeam != null ? $"  ·  the AHL lists him with the {ahlTeam}: tick to call him up anyway" : "");
                changes.Add(new SyncChange
                {
                    Kind = nhlTeams.Contains(from) ? SyncKind.Move : SyncKind.Join,
                    GameId = p.GameId, Name = p.Name, Position = p.PositionDisplay, RosterRow = p.RosterRow, BioRow = p.BioRow, PlayerIndex = p.PlayerIndex,
                    FromTeam = from, ToTeam = lc.Team, FromTeamName = from >= 0 ? TeamName(from) : "Free agent", ToTeamName = lc.TeamName,
                    OldNumber = jersey, NewNumber = spot is { Number: >= 0 and <= 99 } ? spot.Number : -1,
                    Lineup = note,
                    Selected = ahlTeam == null && onCap,
                });
            }
        }
        return changes.OrderBy(c => c.ToTeamName).ThenBy(c => c.Name).ToList();
    }
}
