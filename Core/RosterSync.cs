using System.Globalization;
using System.Text;

namespace NHL27Kit.Core;

public enum SyncKind
{
    /// <summary>On another NHL team in the game: a trade or signing.</summary>
    Move,
    /// <summary>In the game but on no NHL team (junior, AHL, Europe, national team): a call-up or a new signing.</summary>
    Join,
    /// <summary>On the right team; only the number (or height/weight) differs.</summary>
    Update,
    /// <summary>On an NHL team in the game but on no NHL roster now (sent down, released, retired).</summary>
    Left,
    /// <summary>On an NHL roster but not in the game at all.</summary>
    Missing,
}

/// <summary>One difference between the game's rosters and the NHL's, and what applying it does.</summary>
public sealed class SyncChange
{
    public SyncKind Kind { get; init; }
    public NhlPlayer Api { get; init; }
    public int GameId { get; init; } = -1;
    public string Name { get; init; }
    public string Position { get; init; }
    public int RosterRow { get; init; } = -1;
    public int BioRow { get; init; } = -1;
    public int FromTeam { get; init; } = -1;
    public int ToTeam { get; init; } = -1;
    public string FromTeamName { get; init; }
    public string ToTeamName { get; init; }
    public int OldNumber { get; init; } = -1;
    public int NewNumber { get; init; } = -1;
    public int OldHeight { get; init; }
    public int NewHeight { get; init; }
    public int OldWeight { get; init; }
    public int NewWeight { get; init; }
    /// <summary>True when the player was matched by name only (his birth date differs or is missing).</summary>
    public bool NameOnly { get; init; }
    /// <summary>Ticked by default: moves, joins and number changes are; departures and unmatched players aren't.</summary>
    public bool Selected { get; set; }

    public bool CanApply => Kind != SyncKind.Missing && RosterRow >= 0 && ToTeam >= 0;
    public bool NumberChanges => NewNumber >= 0 && NewNumber != OldNumber;

    /// <summary>What applying does, in words.</summary>
    public string Description => Kind switch
    {
        SyncKind.Move => $"{FromTeamName} → {ToTeamName}" + Extra(),
        SyncKind.Join => $"{FromTeamName} → {ToTeamName}" + Extra(),
        SyncKind.Update => Extra().TrimStart(' ', '·'),
        SyncKind.Left => $"not on {FromTeamName}'s NHL roster" + (ToTeam >= 0 ? $" → {ToTeamName}" : ""),
        _ => $"on {Api?.Team}'s roster, not in the game",
    };

    /// <summary>Whether the description and Apply include height and weight.</summary>
    public bool WithSizes { get; set; }

    private string Extra()
    {
        var parts = new List<string>();
        if (NumberChanges) parts.Add(OldNumber >= 0 ? $"#{OldNumber} → #{NewNumber}" : $"#{NewNumber}");
        if (WithSizes && NewHeight > 0 && NewHeight != OldHeight) parts.Add($"{OldHeight}→{NewHeight} in");
        if (WithSizes && NewWeight > 0 && NewWeight != OldWeight) parts.Add($"{OldWeight}→{NewWeight} lb");
        return parts.Count == 0 ? "" : "  ·  " + string.Join(", ", parts);
    }
}

/// <summary>
/// Compares the game's rosters (nhl_game.db) with the NHL's current ones and applies the differences in place.
/// Every player has one row in exhibitionrostertable, on the team he plays for (an NHL club, an AHL or junior team, a
/// national team…), so a trade or call-up changes that row's team and jersey, and the bio's team and jersey. Players are
/// matched by name and birth date (the game has no NHL ids); a single name match with a different birth date is
/// accepted and flagged. Height and weight go in as the game stores them (inches - 54, pounds - 120).
/// </summary>
public static class RosterSync
{
    public const int HeightOffset = 54, WeightOffset = 120;

    /// <summary>
    /// The copy of the roster table the game plays with (its "online" roster: same rows as exhibitionrostertable, by
    /// playerindex). Both are written; this one is what the comparison reads, since it's what the game shows.
    /// </summary>
    public const string OnlineRosterTable = "customonlinerostertable";

    /// <summary>The roster tables a player's row is in (exhibition first, then the online copy), by his playerindex.</summary>
    private static List<(T3Table Table, Dictionary<int, int> RowOf)> RosterTables(T3Db db)
    {
        var list = new List<(T3Table, Dictionary<int, int>)>();
        foreach (var name in new[] { Roster.RosterTable, OnlineRosterTable })
        {
            var t = db.Table(name);
            if (t == null) continue;
            var pi = t.Fields.First(f => f.Name == "playerindex");
            var map = new Dictionary<int, int>();
            for (int r = 0; r < t.WrittenRecords; r++)
                if (!db.IsInvalid(t, r)) map.TryAdd((int)db.Get(t, r, pi), r);
            list.Add((t, map));
        }
        return list;
    }

    /// <summary>
    /// The line-slot flags of a roster row (l1lw, pp_1c, pk4_2rd, ev3_1ld, three_s1, s1, g1…): cleared when a player changes
    /// team. rosterstatus, captain, x1/x2 and the rest are not slots.
    /// </summary>
    private static bool IsLineSlot(T3Field f) => f.Type == T3FieldType.Integer && f.Depth == 1 &&
        System.Text.RegularExpressions.Regex.IsMatch(f.Name, @"^(l[1-4](lw|c|rw|ld|rd)|pp4?_\d\w+|pk[34]_\d\w+|ev[34]_\d\w+|three_\w+|s\d|g\d)$");

    /// <summary>The game's exhibitionteams index of each NHL API team code.</summary>
    public static Dictionary<string, int> TeamIndex(T3Db db)
    {
        var t = db.Table(Roster.TeamsTable);
        var f = t.Fields.ToDictionary(x => x.Name);
        var byAbbr = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int r = 0; r < t.WrittenRecords; r++)
        {
            if (db.IsInvalid(t, r) || (int)db.Get(t, r, f["league"]) != 0) continue;
            int idx = (int)db.Get(t, r, f["index"]);
            if (idx >= 32) continue;
            byAbbr.TryAdd((string)db.Get(t, r, f["abbrname"]), idx);
            byAbbr.TryAdd((string)db.Get(t, r, f["artabbr"]), idx);
        }
        var aliases = new Dictionary<string, string[]> { ["LAK"] = new[] { "LA" }, ["SJS"] = new[] { "SJ" }, ["UTA"] = new[] { "UT" },
                                                          ["TBL"] = new[] { "TB" }, ["NJD"] = new[] { "NJ" }, ["VGK"] = new[] { "LAS" } };
        var map = new Dictionary<string, int>();
        foreach (var code in NhlApi.Teams)
        {
            if (byAbbr.TryGetValue(code, out int i)) { map[code] = i; continue; }
            foreach (var a in aliases.GetValueOrDefault(code, Array.Empty<string>()))
                if (byAbbr.TryGetValue(a, out i)) { map[code] = i; break; }
        }
        return map;
    }

    /// <param name="sizes">Also list players whose only difference is height or weight.</param>
    public static List<SyncChange> Compare(T3Db db, IReadOnlyList<NhlPlayer> api, bool sizes = false)
    {
        var teams = TeamIndex(db);
        var nhlTeams = teams.Values.ToHashSet();
        var roster = new Roster(db);
        var bio = db.Table(Roster.BioTable);
        var rt = db.Table(Roster.RosterTable);
        var bf = bio.Fields.ToDictionary(x => x.Name);
        var rf = rt.Fields.ToDictionary(x => x.Name);
        string TeamName(int i) => roster.Teams.TryGetValue(i, out var tm) && !string.IsNullOrEmpty(tm.Name) ? tm.Name : $"team {i}";
        var ahl = AhlAffiliates(db);

        // the game's players by normalised name
        var byName = new Dictionary<string, List<RosterPlayer>>();
        foreach (var p in roster.Players.Where(p => p.RosterRow >= 0))
        {
            string k = Key(p.FirstName, p.LastName);
            if (!byName.TryGetValue(k, out var l)) byName[k] = l = new List<RosterPlayer>();
            l.Add(p);
        }
        DateTime? Born(RosterPlayer p)
        {
            int y = (int)db.Get(bio, p.BioRow, bf["year"]) + 1900, m = (int)db.Get(bio, p.BioRow, bf["month"]) + 1, d = (int)db.Get(bio, p.BioRow, bf["day"]) + 1;
            return y > 1900 && m is >= 1 and <= 12 && d >= 1 && d <= DateTime.DaysInMonth(y, m) ? new DateTime(y, m, d) : null;
        }
        // last-name-only index for players the API spells differently (Teddy / Teodors)
        var byLast = roster.Players.Where(p => p.RosterRow >= 0).GroupBy(p => Norm(p.LastName)).ToDictionary(g => g.Key, g => g.ToList());

        // what the game shows: the online roster copy's row, else the exhibition one
        var tables = RosterTables(db);
        var piField = rf["playerindex"];
        (T3Table T, int Row) Shown(int exhibitionRow)
        {
            int pi = (int)db.Get(rt, exhibitionRow, piField);
            for (int i = tables.Count - 1; i >= 0; i--)
                if (tables[i].RowOf.TryGetValue(pi, out int r)) return (tables[i].Table, r);
            return (rt, exhibitionRow);
        }
        int TeamOf(int row) { var (t, r) = Shown(row); return (int)db.Get(t, r, t.Fields.First(f => f.Name == "team")); }
        int JerseyOf(int row) { var (t, r) = Shown(row); return (int)db.Get(t, r, t.Fields.First(f => f.Name == "jersey")); }

        var changes = new List<SyncChange>();
        var matched = new HashSet<int>();
        foreach (var a in api)
        {
            if (!teams.TryGetValue(a.Team, out int to)) continue;
            RosterPlayer p = null;
            bool nameOnly = false;
            var cands = byName.GetValueOrDefault(Key(a.First, a.Last)) ?? new List<RosterPlayer>();
            p = cands.FirstOrDefault(c => Born(c) == a.Born);
            if (p == null && a.Born != null)
                p = byLast.GetValueOrDefault(Norm(a.Last))?.FirstOrDefault(c => Born(c) == a.Born);
            if (p == null && cands.Count == 1) { p = cands[0]; nameOnly = true; }
            if (p == null || matched.Contains(p.GameId))
            {
                changes.Add(new SyncChange { Kind = SyncKind.Missing, Api = a, Name = a.Name, Position = a.Position, ToTeam = to, ToTeamName = TeamName(to) });
                continue;
            }
            matched.Add(p.GameId);
            int from = TeamOf(p.RosterRow);
            int oldNo = JerseyOf(p.RosterRow);
            int oldH = (int)db.Get(bio, p.BioRow, bf["height"]) + HeightOffset, oldW = (int)db.Get(bio, p.BioRow, bf["weight"]) + WeightOffset;
            var kind = from == to ? SyncKind.Update : nhlTeams.Contains(from) ? SyncKind.Move : SyncKind.Join;
            var c = new SyncChange
            {
                Kind = kind, Api = a, GameId = p.GameId, Name = p.Name, Position = a.Position, RosterRow = p.RosterRow, BioRow = p.BioRow,
                FromTeam = from, ToTeam = to, FromTeamName = TeamName(from), ToTeamName = TeamName(to),
                OldNumber = oldNo, NewNumber = a.Number, OldHeight = oldH, NewHeight = a.HeightIn, OldWeight = oldW, NewWeight = a.WeightLb,
                NameOnly = nameOnly, WithSizes = sizes,
            };
            bool sizeDiffers = (a.HeightIn > 0 && a.HeightIn != oldH) || (a.WeightLb > 0 && a.WeightLb != oldW);
            if (kind == SyncKind.Update && !c.NumberChanges && !(sizes && sizeDiffers)) continue;
            c.Selected = true;
            changes.Add(c);
        }
        // NHL players in the game who aren't on any NHL roster now: offered a move to the club's AHL affiliate, unticked
        foreach (var p in roster.Players.Where(p => p.RosterRow >= 0 && !matched.Contains(p.GameId)))
        {
            int team = TeamOf(p.RosterRow);
            if (!nhlTeams.Contains(team)) continue;
            int to = ahl.GetValueOrDefault(team, -1);
            changes.Add(new SyncChange
            {
                Kind = SyncKind.Left, GameId = p.GameId, Name = p.Name, Position = p.PositionName, RosterRow = p.RosterRow, BioRow = p.BioRow,
                FromTeam = team, FromTeamName = TeamName(team), ToTeam = to, ToTeamName = to >= 0 ? TeamName(to) : null,
                OldNumber = JerseyOf(p.RosterRow),
            });
        }
        return changes.OrderBy(c => c.Kind).ThenBy(c => c.ToTeamName ?? c.FromTeamName).ThenBy(c => c.Name).ToList();
    }

    /// <summary>
    /// Writes the changes into the database (in place): the player's row in both roster tables (team, jersey; a player
    /// changing team gets his line slots and captaincy cleared and is marked active, so the game places him in its own
    /// lines) and the bio's team, jersey, height and weight. Returns a summary.
    /// </summary>
    public static string Apply(T3Db db, IEnumerable<SyncChange> changes)
    {
        using var batch = db.Batch();
        var bio = db.Table(Roster.BioTable);
        var rt = db.Table(Roster.RosterTable);
        var bf = bio.Fields.ToDictionary(x => x.Name);
        var rf = rt.Fields.ToDictionary(x => x.Name);
        var tables = RosterTables(db);
        var arrivals = new HashSet<int>();
        var touched = new HashSet<int>();
        int moved = 0, numbers = 0, sizes = 0;
        void SetIn(T3Table t, int row, T3Field f, int v)
        {
            long max = f.RangeLow + (1L << f.Depth) - 1;
            if (v >= f.RangeLow && v <= max) db.Set(t, row, f, v);
        }
        foreach (var c in changes.Where(c => c.CanApply))
        {
            int pi = (int)db.Get(rt, c.RosterRow, rf["playerindex"]);
            bool teamChanged = false, numberChanged = false;
            foreach (var (t, rowOf) in tables)
            {
                if (!rowOf.TryGetValue(pi, out int row)) continue;
                var f = t.Fields.ToDictionary(x => x.Name);
                int oldTeam = (int)db.Get(t, row, f["team"]);
                if (oldTeam != c.ToTeam)
                {
                    SetIn(t, row, f["team"], c.ToTeam);
                    SetIn(t, row, f["key"], FreeKey(db, t, c.ToTeam));
                    arrivals.Add(pi);
                    touched.Add(oldTeam);
                    touched.Add(c.ToTeam);
                    foreach (var s in t.Fields.Where(IsLineSlot)) db.Set(t, row, s, 0);
                    if (f.TryGetValue("captain", out var cap)) SetIn(t, row, cap, 0);
                    if (f.TryGetValue("rosterstatus", out var st)) SetIn(t, row, st, 1);
                    teamChanged = true;
                }
                // a player moved by an earlier version of the sync: marked active again (it cleared rosterstatus)
                else if (c.Kind is SyncKind.Move or SyncKind.Join && f.TryGetValue("rosterstatus", out var st2) && (int)db.Get(t, row, st2) == 0)
                    SetIn(t, row, st2, 1);
                if (c.NumberChanges && c.NewNumber is >= 0 and <= 99 && (int)db.Get(t, row, f["jersey"]) != c.NewNumber)
                {
                    SetIn(t, row, f["jersey"], c.NewNumber);
                    numberChanged = true;
                }
            }
            if (teamChanged || (int)db.Get(bio, c.BioRow, bf["team"]) != c.ToTeam)
            {
                SetIn(bio, c.BioRow, bf["team"], c.ToTeam);
                if (bf.TryGetValue("proteam", out var pt) && c.Kind != SyncKind.Left) SetIn(bio, c.BioRow, pt, c.ToTeam);
                moved++;
            }
            if (c.NumberChanges && c.NewNumber is >= 0 and <= 99)
            {
                SetIn(bio, c.BioRow, bf["jersey"], c.NewNumber);
                if (numberChanged) numbers++;
            }
            if (c.WithSizes && c.NewHeight > 0 && c.NewWeight > 0 && (c.NewHeight != c.OldHeight || c.NewWeight != c.OldWeight))
            {
                SetIn(bio, c.BioRow, bf["height"], c.NewHeight - HeightOffset);
                SetIn(bio, c.BioRow, bf["weight"], c.NewWeight - WeightOffset);
                sizes++;
            }
        }
        // every team that gained or lost a player gets its lines built again from its players
        foreach (var (t, _) in tables)
        {
            foreach (int team in touched) RosterLines.Build(db, t, team);
            SortByKey(db, t);
        }
        return $"NHL roster sync: {moved} moved, {numbers} numbers, {sizes} heights/weights, lines rebuilt for {touched.Count} teams";
    }

    // ---------------------------------------------------------------- keys and lines

    /// <summary>
    /// The roster key of a team's free slot: team × 40 + the lowest slot number no row of the team uses (every row's key
    /// is its team × 40 + 0..39, and the table is kept in key order). Throws when the team has 40 players.
    /// </summary>
    private static int FreeKey(T3Db db, T3Table t, int team)
    {
        var kf = t.Fields.First(f => f.Name == "key");
        var used = new HashSet<int>();
        for (int r = 0; r < t.WrittenRecords; r++)
            if (!db.IsInvalid(t, r) && (int)db.Get(t, r, kf) / 40 == team) used.Add((int)db.Get(t, r, kf) % 40);
        for (int s = 0; s < 40; s++)
            if (!used.Contains(s)) return team * 40 + s;
        throw new InvalidOperationException($"Team {team} already has 40 players.");
    }

    /// <summary>Puts the table's rows back in key order (moved whole, nothing else changes).</summary>
    private static void SortByKey(T3Db db, T3Table t)
    {
        var kf = t.Fields.First(f => f.Name == "key");
        var order = Enumerable.Range(0, t.WrittenRecords).OrderBy(r => db.IsInvalid(t, r) ? int.MaxValue : (int)db.Get(t, r, kf)).ThenBy(r => r).ToList();
        if (!order.Select((r, i) => r == i).All(x => x)) db.Reorder(t, order);
    }

    /// <summary>The group a line slot belongs to (a player holds at most one slot per group): l, pp, pp4, pk3, pk4, ev3, ev4, three, s, g.</summary>
    private static string SlotGroup(string slot) =>
        slot.StartsWith("three_") ? "three" : slot[0] == 's' && slot.Length == 2 ? "s" : slot[0] == 'g' && slot.Length == 2 ? "g"
        : System.Text.RegularExpressions.Regex.Match(slot, @"^(l|pp4?|pk[34]|ev[34])").Value;

    /// <summary>Which positions can take a slot (bio position: 0 C, 1 LW, 2 RW, 3 D, 4 G), best first.</summary>
    private static int[] SlotPositions(string slot)
    {
        if (slot is "g1" or "g2" || slot.StartsWith("three_g")) return new[] { 4 };
        string end = System.Text.RegularExpressions.Regex.Match(slot, @"(lw|rw|c|ld|rd)$").Value;
        return end switch
        {
            "lw" => new[] { 1, 0, 2 }, "rw" => new[] { 2, 0, 1 }, "c" => new[] { 0, 1, 2 }, "ld" or "rd" => new[] { 3 },
            // shootout shooters and 3-on-3 skaters: any skater, forwards first
            _ => new[] { 0, 1, 2, 3 },
        };
    }

    /// <summary>
    /// Fills the line slots no player of a team holds (a team's 92 slots are each held by exactly one player): players
    /// who just arrived first, then players without a slot (scratches), then anyone, each by position and never twice in
    /// the same group. Returns how many slots were filled.
    /// </summary>
    private static int FillLines(T3Db db, T3Table t, IEnumerable<int> teams, ISet<int> arrivals)
    {
        var f = t.Fields.ToDictionary(x => x.Name);
        var slots = t.Fields.Where(IsLineSlot).ToList();
        var position = PositionsByPlayerIndex(db);
        int filled = 0;
        foreach (int team in teams)
        {
            var rows = Enumerable.Range(0, t.WrittenRecords).Where(r => !db.IsInvalid(t, r) && (int)db.Get(t, r, f["team"]) == team).ToList();
            if (rows.Count == 0) continue;
            int Pi(int r) => (int)db.Get(t, r, f["playerindex"]);
            bool Has(int r, T3Field s) => (int)db.Get(t, r, s) == 1;
            foreach (var slot in slots.Where(s => !rows.Any(r => Has(r, s))))
            {
                string group = SlotGroup(slot.Name);
                var groupSlots = slots.Where(s => SlotGroup(s.Name) == group).ToList();
                var free = rows.Where(r => !groupSlots.Any(s => Has(r, s))).ToList();
                int pick = -1;
                foreach (int pos in SlotPositions(slot.Name))
                {
                    // spread the spots: whoever holds the fewest so far first
                    var byPos = free.Where(r => position.GetValueOrDefault(Pi(r), -1) == pos).OrderBy(r => slots.Count(s => Has(r, s))).ToList();
                    pick = byPos.Where(r => arrivals.Contains(Pi(r))).Concat(byPos.Where(r => !slots.Any(s => Has(r, s)))).Concat(byPos).DefaultIfEmpty(-1).First();
                    if (pick >= 0) break;
                }
                if (pick < 0) continue;
                db.Set(t, pick, slot, 1);
                if (f.TryGetValue("rosterstatus", out var st)) db.Set(t, pick, st, 1);
                filled++;
            }
        }
        return filled;
    }

    /// <summary>Each roster playerindex's position (exhibitionplayers.index → playerid = bio game_id → bio position).</summary>
    private static Dictionary<int, int> PositionsByPlayerIndex(T3Db db)
    {
        var players = db.Table(Roster.PlayersTable);
        var pf = players.Fields.ToDictionary(x => x.Name);
        var bio = db.Table(Roster.BioTable);
        var bf = bio.Fields.ToDictionary(x => x.Name);
        var posOfId = new Dictionary<int, int>();
        for (int r = 0; r < bio.WrittenRecords; r++)
            if (!db.IsInvalid(bio, r)) posOfId[(int)db.Get(bio, r, bf["game_id"])] = (int)db.Get(bio, r, bf["position"]);
        var map = new Dictionary<int, int>();
        for (int r = 0; r < players.WrittenRecords; r++)
            if (!db.IsInvalid(players, r) && posOfId.TryGetValue((int)db.Get(players, r, pf["playerid"]), out int p))
                map[(int)db.Get(players, r, pf["index"])] = p;
        return map;
    }

    /// <summary>
    /// Repairs rosters an earlier sync left inconsistent: rows whose key isn't in their team get a free key there, every
    /// NHL team's empty line slots are filled (players who changed team first), and the rows go back in key order, in
    /// both roster tables. Returns a summary, or null when nothing needed fixing.
    /// </summary>
    public static string Repair(T3Db db)
    {
        using var batch = db.Batch();
        var nhl = TeamIndex(db).Values.ToHashSet();
        int keys = 0, filled = 0;
        foreach (var (t, _) in RosterTables(db))
        {
            var f = t.Fields.ToDictionary(x => x.Name);
            var arrivals = new HashSet<int>();
            for (int r = 0; r < t.WrittenRecords; r++)
            {
                if (db.IsInvalid(t, r)) continue;
                int team = (int)db.Get(t, r, f["team"]);
                if ((int)db.Get(t, r, f["key"]) / 40 == team) continue;
                db.Set(t, r, f["key"], FreeKey(db, t, team));
                arrivals.Add((int)db.Get(t, r, f["playerindex"]));
                keys++;
            }
            var teamsOfArrivals = Enumerable.Range(0, t.WrittenRecords)
                .Where(r => !db.IsInvalid(t, r) && arrivals.Contains((int)db.Get(t, r, f["playerindex"]))).Select(r => (int)db.Get(t, r, f["team"]));
            foreach (int team in RosterLines.TeamsNeedingLines(db, t, nhl).Concat(teamsOfArrivals).ToHashSet())
            {
                RosterLines.Build(db, t, team);
                filled++;
            }
            SortByKey(db, t);
        }
        return keys == 0 && filled == 0 ? null : $"NHL roster sync repair: {keys} keys moved to their team, lines rebuilt {filled} times (both roster tables)";
    }

    /// <summary>
    /// Brings the online roster copy in line with exhibitionrostertable where they differ in team or jersey (the first
    /// version of the sync wrote only the exhibition table): the online row takes the team and jersey, its line slots
    /// and captaincy are cleared, and both rows are marked active (that version cleared rosterstatus). Returns a summary,
    /// or null when the two already agree.
    /// </summary>
    public static string Mirror(T3Db db)
    {
        using var batch = db.Batch();
        var tables = RosterTables(db);
        if (tables.Count < 2) return null;
        var (ex, exRows) = tables[0];
        var (on, onRows) = tables[1];
        var ef = ex.Fields.ToDictionary(x => x.Name);
        var of = on.Fields.ToDictionary(x => x.Name);
        int n = 0;
        foreach (var (pi, er) in exRows)
        {
            if (!onRows.TryGetValue(pi, out int or)) continue;
            int team = (int)db.Get(ex, er, ef["team"]), jersey = (int)db.Get(ex, er, ef["jersey"]);
            bool teamDiffers = (int)db.Get(on, or, of["team"]) != team;
            if (!teamDiffers && (int)db.Get(on, or, of["jersey"]) == jersey) continue;
            if (teamDiffers)
            {
                db.Set(on, or, of["team"], team);
                db.Set(on, or, of["key"], (int)db.Get(ex, er, ef["key"]));
                foreach (var s in on.Fields.Where(IsLineSlot)) db.Set(on, or, s, 0);
                if (of.TryGetValue("captain", out var cap)) db.Set(on, or, cap, 0);
                if (of.TryGetValue("rosterstatus", out var st)) db.Set(on, or, st, 1);
                if (ef.TryGetValue("rosterstatus", out var st2)) db.Set(ex, er, st2, 1);
            }
            db.Set(on, or, of["jersey"], jersey);
            n++;
        }
        return n == 0 ? null : $"NHL roster sync: {n} players copied into the game's online roster";
    }

    /// <summary>Each NHL team's AHL affiliate (exhibitionteams.ahlaffiliate).</summary>
    private static Dictionary<int, int> AhlAffiliates(T3Db db)
    {
        var t = db.Table(Roster.TeamsTable);
        var f = t.Fields.ToDictionary(x => x.Name);
        var map = new Dictionary<int, int>();
        if (!f.ContainsKey("ahlaffiliate")) return map;
        for (int r = 0; r < t.WrittenRecords; r++)
            if (!db.IsInvalid(t, r)) map[(int)db.Get(t, r, f["index"])] = (int)db.Get(t, r, f["ahlaffiliate"]);
        return map;
    }

    private static string Key(string first, string last) => Norm(first) + " " + Norm(last);

    /// <summary>Lower case, accents and punctuation removed (Stützle = stutzle, O'Reilly = oreilly).</summary>
    public static string Norm(string s)
    {
        var d = (s ?? "").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (char c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }
}
