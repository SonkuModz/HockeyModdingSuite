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
    /// <summary>The AHL player, for changes from CompareAhl. Null for NHL changes; Api is null for AHL changes.</summary>
    public AhlPlayer Ahl { get; init; }
    /// <summary>True when this change came from an AHL roster (any AHL team), not an NHL roster.</summary>
    public bool IsAhl => Ahl != null;
    public int GameId { get; init; } = -1;
    public string Name { get; init; }
    public string Position { get; init; }
    public int RosterRow { get; init; } = -1;
    public int BioRow { get; init; } = -1;
    /// <summary>The player's index in exhibitionplayers. -1 when he has no players row, so no roster row can be created for him.</summary>
    public int PlayerIndex { get; init; } = -1;
    /// <summary>True when the player is in the game (bio + players row) but has no roster row yet: Apply creates one.</summary>
    public bool NeedsNewRow => RosterRow < 0;
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

    public bool CanApply => Kind != SyncKind.Missing && BioRow >= 0 && ToTeam >= 0 && (RosterRow >= 0 || PlayerIndex >= 0);
    public bool NumberChanges => NewNumber >= 0 && NewNumber != OldNumber;

    /// <summary>What applying does, in words.</summary>
    public string Description => Kind switch
    {
        SyncKind.Move => $"{FromTeamName} → {ToTeamName}" + Extra(),
        SyncKind.Join => $"{FromTeamName} → {ToTeamName}" + Extra(),
        SyncKind.Update => Extra().TrimStart(' ', '·'),
        SyncKind.Left => $"not on {FromTeamName}'s NHL roster" + (ToTeam >= 0 ? $" → {ToTeamName}" : ""),
        _ => $"on {(Api?.Team ?? Ahl?.TeamName ?? "?")}'s roster, not in the game",
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
    /// <param name="alreadyMatched">
    /// Game ids already matched by another compare (CompareCombined's shared set, filled by the AHL compare
    /// first). A player in this set is skipped outright — no change, no Missing, and no Left — so a player on
    /// both an NHL roster and an AHL roster gets exactly one change, the AHL one (see CompareCombined).
    /// Null for a stand-alone NHL compare (the CLI's roster-sync), which keeps the old behaviour.
    /// </param>
    public static List<SyncChange> Compare(T3Db db, IReadOnlyList<NhlPlayer> api, bool sizes = false, HashSet<int> alreadyMatched = null)
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
        // the game's players by normalised name. Every bio player is a candidate, not only players
        // with a roster row: a bio-only free agent (RosterRow -1) is a Join, not a Missing.
        foreach (var p in roster.Players)
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
        var byLast = roster.Players.GroupBy(p => Norm(p.LastName)).ToDictionary(g => g.Key, g => g.ToList());

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
            // Already matched by the AHL compare (CompareCombined runs it first): skip outright, don't
            // call him Missing and don't build an NHL change for him — his AHL change already covers him.
            if (p != null && alreadyMatched != null && alreadyMatched.Contains(p.GameId)) continue;
            if (p == null || matched.Contains(p.GameId))
            {
                changes.Add(new SyncChange { Kind = SyncKind.Missing, Api = a, Name = a.Name, Position = a.Position, ToTeam = to, ToTeamName = TeamName(to) });
                continue;
            }
            matched.Add(p.GameId);
            // A player with no roster row has no team/jersey in a roster table: his bio team is where he came from.
            int from, oldNo;
            string fromName;
            if (p.RosterRow >= 0) { from = TeamOf(p.RosterRow); oldNo = JerseyOf(p.RosterRow); fromName = TeamName(from); }
            else { from = p.Team; oldNo = -1; fromName = roster.Teams.TryGetValue(from, out var ft) && !string.IsNullOrEmpty(ft.Name) ? ft.Name : "Free agent"; }
            int oldH = (int)db.Get(bio, p.BioRow, bf["height"]) + HeightOffset, oldW = (int)db.Get(bio, p.BioRow, bf["weight"]) + WeightOffset;
            // A player with no roster row is always a Join, even if his bio team already equals the NHL team:
            // an Update with no number/size change would otherwise be skipped and he would stay without a roster row.
            var kind = p.RosterRow < 0 ? SyncKind.Join : from == to ? SyncKind.Update : nhlTeams.Contains(from) ? SyncKind.Move : SyncKind.Join;
            var c = new SyncChange
            {
                Kind = kind, Api = a, GameId = p.GameId, Name = p.Name, Position = a.Position, RosterRow = p.RosterRow, BioRow = p.BioRow,
                PlayerIndex = p.PlayerIndex,
                FromTeam = from, ToTeam = to, FromTeamName = fromName, ToTeamName = TeamName(to),
                OldNumber = oldNo, NewNumber = a.Number, OldHeight = oldH, NewHeight = a.HeightIn, OldWeight = oldW, NewWeight = a.WeightLb,
                NameOnly = nameOnly, WithSizes = sizes,
            };
            bool sizeDiffers = (a.HeightIn > 0 && a.HeightIn != oldH) || (a.WeightLb > 0 && a.WeightLb != oldW);
            if (kind == SyncKind.Update && !c.NumberChanges && !(sizes && sizeDiffers)) continue;
            c.Selected = true;
            changes.Add(c);
        }
        // NHL players in the game who aren't on any NHL roster now: offered a move to the club's AHL affiliate, unticked.
        // Players already matched by the AHL compare are excluded too: a player on an AHL roster who is not on any
        // NHL roster must not also appear here as Left, or the two changes would fight over him.
        foreach (var p in roster.Players.Where(p => p.RosterRow >= 0 && !matched.Contains(p.GameId) &&
            (alreadyMatched == null || !alreadyMatched.Contains(p.GameId))))
        {
            int team = TeamOf(p.RosterRow);
            if (!nhlTeams.Contains(team)) continue;
            int to = ahl.GetValueOrDefault(team, -1);
            changes.Add(new SyncChange
            {
                Kind = SyncKind.Left, GameId = p.GameId, Name = p.Name, Position = p.PositionName, RosterRow = p.RosterRow, BioRow = p.BioRow,
                PlayerIndex = p.PlayerIndex,
                FromTeam = team, FromTeamName = TeamName(team), ToTeam = to, ToTeamName = to >= 0 ? TeamName(to) : null,
                OldNumber = JerseyOf(p.RosterRow),
            });
        }
        return changes.OrderBy(c => c.Kind).ThenBy(c => c.ToTeamName ?? c.FromTeamName).ThenBy(c => c.Name).ToList();
    }

    /// <summary>
    /// One AHL team's exhibitionteams index, resolved from the database at runtime — never hard-coded. Returns -1
    /// when the feed team has no matching team in the game (a renamed, relocated or expansion club, for example):
    /// callers skip such a team and list it, instead of failing the whole sync or writing to the wrong team.
    /// Match order: (1) exact normalised full-name match (Norm: lower case, accents and punctuation removed);
    /// (2) a looser contains-match, restricted to teams that are someone's AHL affiliate (the values of
    /// AhlAffiliates) and only when exactly one such team matches — the affiliates map cannot resolve a name on
    /// its own, so it is used here only to narrow the candidates for the loose pass.
    /// </summary>
    public static int ResolveAhlTeamIndex(T3Db db, string ahlTeamName)
    {
        var roster = new Roster(db);
        string want = Norm(ahlTeamName);
        foreach (var (idx, tm) in roster.Teams)
            if (!string.IsNullOrEmpty(tm.Name) && Norm(tm.Name) == want) return idx;
        var affiliates = AhlAffiliates(db).Values.ToHashSet();
        int found = -1, hits = 0;
        foreach (var (idx, tm) in roster.Teams)
        {
            if (!affiliates.Contains(idx) || string.IsNullOrEmpty(tm.Name)) continue;
            string have = Norm(tm.Name);
            if (have.Contains(want) || want.Contains(have)) { found = idx; hits++; }
        }
        return hits == 1 ? found : -1;
    }

    /// <summary>Iowa Wild's exhibitionteams index (thin wrapper kept from the Iowa-only prototype). Throws a clear error when Iowa isn't in the game's teams.</summary>
    public static int ResolveIowaTeamIndex(T3Db db)
    {
        int idx = ResolveAhlTeamIndex(db, AhlApi.TeamName);
        if (idx < 0) throw new InvalidOperationException("Iowa Wild not found in the game's teams (exhibitionteams). The AHL sync needs the Iowa Wild team in nhl_game.db.");
        return idx;
    }

    /// <summary>
    /// Compares the game's rosters with one AHL team's current AHL roster.
    /// Same matcher as Compare: normalised name + birth date, then last name + birth date, then a single name-only
    /// match, flagged. Same Join/Move/Update/Missing kinds, same ticking (Join/Move/Update ticked, Missing not).
    /// Position is never compared or written: the AHL feed lists many forwards as generic "F", and Apply never
    /// writes position anyway. Height/weight work the same as the NHL path (WithSizes + NewHeight/NewWeight, so
    /// Apply's size code applies unchanged). No reverse Left direction: players on the AHL team in the game who
    /// are no longer on its roster are not listed.
    /// </summary>
    /// <param name="ahlTeam">The AHL team's exhibitionteams index: use ResolveAhlTeamIndex(db, teamName).</param>
    /// <param name="sizes">Also list players whose only difference is height or weight.</param>
    /// <param name="alreadyMatched">
    /// Game ids already matched to another AHL team (CompareAllAhl's shared set; null for a single-team compare).
    /// A feed player whose game player is already in this set is skipped outright — he is in the game and already
    /// has a change for his first-listed team, so reporting him Missing for a second team would be wrong.
    /// Within one team, the v1 behaviour is kept: a second feed player matching the same game player is Missing.
    /// The ids matched by this call are added to the set before returning.
    /// </param>
    public static List<SyncChange> CompareAhl(T3Db db, IReadOnlyList<AhlPlayer> api, int ahlTeam, bool sizes = false, HashSet<int> alreadyMatched = null)
    {
        var teams = TeamIndex(db);
        var nhlTeams = teams.Values.ToHashSet();
        var roster = new Roster(db);
        var bio = db.Table(Roster.BioTable);
        var rt = db.Table(Roster.RosterTable);
        var bf = bio.Fields.ToDictionary(x => x.Name);
        var rf = rt.Fields.ToDictionary(x => x.Name);
        string TeamName(int i) => roster.Teams.TryGetValue(i, out var tm) && !string.IsNullOrEmpty(tm.Name) ? tm.Name : $"team {i}";

        // the game's players by normalised name. Every bio player is a candidate, not only players
        // with a roster row: a bio-only free agent (RosterRow -1) is a Join, not a Missing.
        var byName = new Dictionary<string, List<RosterPlayer>>();
        foreach (var p in roster.Players)
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
        var byLast = roster.Players.GroupBy(p => Norm(p.LastName)).ToDictionary(g => g.Key, g => g.ToList());

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
            RosterPlayer p = null;
            bool nameOnly = false;
            var cands = byName.GetValueOrDefault(Key(a.First, a.Last)) ?? new List<RosterPlayer>();
            p = cands.FirstOrDefault(c => Born(c) == a.Born);
            if (p == null && a.Born != null)
                p = byLast.GetValueOrDefault(Norm(a.Last))?.FirstOrDefault(c => Born(c) == a.Born);
            if (p == null && cands.Count == 1) { p = cands[0]; nameOnly = true; }
            // Already matched to another AHL team (shared set from CompareAllAhl): skip, don't call him Missing.
            if (p != null && alreadyMatched != null && alreadyMatched.Contains(p.GameId)) continue;
            if (p == null || matched.Contains(p.GameId))
            {
                changes.Add(new SyncChange { Kind = SyncKind.Missing, Ahl = a, Name = a.Name, Position = a.Position, ToTeam = ahlTeam, ToTeamName = TeamName(ahlTeam) });
                continue;
            }
            matched.Add(p.GameId);
            // A player with no roster row has no team/jersey in a roster table: his bio team is where he came from.
            int from, oldNo;
            string fromName;
            if (p.RosterRow >= 0) { from = TeamOf(p.RosterRow); oldNo = JerseyOf(p.RosterRow); fromName = TeamName(from); }
            else { from = p.Team; oldNo = -1; fromName = roster.Teams.TryGetValue(from, out var ft) && !string.IsNullOrEmpty(ft.Name) ? ft.Name : "Free agent"; }
            int oldH = (int)db.Get(bio, p.BioRow, bf["height"]) + HeightOffset, oldW = (int)db.Get(bio, p.BioRow, bf["weight"]) + WeightOffset;
            // A player with no roster row is always a Join, even if his bio team already equals Iowa:
            // an Update with no number/size change would otherwise be skipped and he would stay without a roster row.
            var kind = p.RosterRow < 0 ? SyncKind.Join : from == ahlTeam ? SyncKind.Update : nhlTeams.Contains(from) ? SyncKind.Move : SyncKind.Join;
            var c = new SyncChange
            {
                Kind = kind, Ahl = a, GameId = p.GameId, Name = p.Name, Position = a.Position, RosterRow = p.RosterRow, BioRow = p.BioRow,
                PlayerIndex = p.PlayerIndex,
                FromTeam = from, ToTeam = ahlTeam, FromTeamName = fromName, ToTeamName = TeamName(ahlTeam),
                OldNumber = oldNo, NewNumber = a.Number, OldHeight = oldH, NewHeight = a.HeightIn, OldWeight = oldW, NewWeight = a.WeightLb,
                NameOnly = nameOnly, WithSizes = sizes,
            };
            bool sizeDiffers = (a.HeightIn > 0 && a.HeightIn != oldH) || (a.WeightLb > 0 && a.WeightLb != oldW);
            if (kind == SyncKind.Update && !c.NumberChanges && !(sizes && sizeDiffers)) continue;
            c.Selected = true;
            changes.Add(c);
        }
        if (alreadyMatched != null) alreadyMatched.UnionWith(matched);
        return changes.OrderBy(c => c.Kind).ThenBy(c => c.Name).ToList();
    }

    /// <summary>
    /// Compares the game's rosters with every AHL team's current roster (AhlApi.AllRostersAsync). Teams whose feed
    /// name has no matching team in the game are skipped and their names returned in SkippedTeams — a feed team may
    /// be a renamed, relocated or expansion club the game does not have (a candidate in 2026-27 is the Hamilton
    /// Hammers), and one missing team must not fail the other 31. A shared matched set is passed through
    /// CompareAhl so a player listed for two teams is reported once, for his first-listed team, and never as
    /// Missing for the second. Returns the changes (ordered by kind, then team, then name) and the skipped teams.
    /// </summary>
    /// <param name="matchedOut">Optional set the game ids matched by this call are added to (CompareCombined's shared set).</param>
    public static (List<SyncChange> Changes, List<string> SkippedTeams) CompareAllAhl(T3Db db, IReadOnlyList<(AhlTeam Team, List<AhlPlayer> Players)> allRosters, bool sizes = false, HashSet<int> matchedOut = null)
    {
        var changes = new List<SyncChange>();
        var skipped = new List<string>();
        var matched = new HashSet<int>();
        foreach (var (team, players) in allRosters)
        {
            int idx = ResolveAhlTeamIndex(db, team.Name);
            if (idx < 0) { skipped.Add(team.Name); continue; }
            changes.AddRange(CompareAhl(db, players, idx, sizes, matched));
        }
        if (matchedOut != null) matchedOut.UnionWith(matched);
        return (changes.OrderBy(c => c.Kind).ThenBy(c => c.ToTeamName ?? "").ThenBy(c => c.Name).ToList(), skipped);
    }

    /// <summary>
    /// The combined NHL + AHL compare used by the UI (RosterSyncView): AHL first, NHL second, sharing one
    /// matched set, so a player on both an NHL roster and an AHL roster gets exactly one change — the AHL one.
    /// Why AHL wins: the NHL API's roster is not a playing roster. It lists 27 Calgary players (17F/7D/3G),
    /// including men assigned to the AHL, so it cannot override the AHL feed for where a player actually plays.
    /// Ben Jones, 2026-11-04: on the Flames in the NHL API and on the Wranglers in the AHL feed at the same
    /// time. Before this, the UI showed both — NHL CALL-UP Wranglers → Flames 195→187 lb and AHL TRADE
    /// Flames → Wranglers 187→195 lb — and applying one created the other, forever (same for Toninato,
    /// Burns, Groulx, McWard, Noesen and Korpisalo, all NHL-contract players assigned to the AHL). Now the
    /// NHL compare skips him entirely and only the AHL TRADE is listed; applying it leaves no row for him.
    /// Known tradeoff: if the AHL feed is stale for a day after a genuine call-up, the AHL change wins for
    /// that day (he is sent down), and the NHL compare calls him up again once the feed drops him. That is
    /// a one-day lag, not a permanent ping-pong, and the user can untick the row in the meantime.
    /// The returned list keeps the old display order (NHL changes first, then AHL changes), each part in
    /// its own compare's order. A player on an NHL roster who is not in the game at all is still reported
    /// Missing by the NHL compare (the skip only applies to players who were actually matched), and likewise
    /// for the AHL side — Missing rows can't be applied, so they can't ping-pong.
    /// </summary>
    public static (List<SyncChange> Changes, List<string> SkippedAhlTeams) CompareCombined(T3Db db, IReadOnlyList<NhlPlayer> api, IReadOnlyList<(AhlTeam Team, List<AhlPlayer> Players)> allAhl, bool sizes = false)
    {
        var matched = new HashSet<int>();
        var (ahlChanges, skipped) = CompareAllAhl(db, allAhl, sizes, matched);
        var nhlChanges = Compare(db, api, sizes, matched);
        var changes = new List<SyncChange>(nhlChanges.Count + ahlChanges.Count);
        changes.AddRange(nhlChanges);
        changes.AddRange(ahlChanges);
        return (changes, skipped);
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
        var createdPis = new HashSet<int>();
        int moved = 0, numbers = 0, sizes = 0;
        void SetIn(T3Table t, int row, T3Field f, int v)
        {
            long max = f.RangeLow + (1L << f.Depth) - 1;
            if (v >= f.RangeLow && v <= max) db.Set(t, row, f, v);
        }
        foreach (var c in changes.Where(c => c.CanApply))
        {
            // The roster row's playerindex when he has one: that is the row Compare read (its team
            // and jersey are what FromTeam/OldNumber came from), so it is the row Apply must write.
            // The playerindex carried on the change comes from exhibitionplayers' first row for the
            // player (Roster.PlayerIndex = indexOfId, first-wins) and can differ from it: the game
            // can ship two exhibitionplayers rows for one playerid, and then the roster row can use
            // the later index while PlayerIndex holds the first. Apply then updated a different,
            // already-correct row and the change never cleared — Damian Clara, 2026-11-04: his San
            // Diego rows under the first index were already #35, while the row Compare read (#20)
            // was under the later index, so an in-memory Apply printed 0 numbers and the Update
            // stayed. Only when he has no roster row (a bio-only free agent) is there no row to
            // read from, and PlayerIndex is the only source for creating one.
            int pi = c.RosterRow >= 0 ? (int)db.Get(rt, c.RosterRow, rf["playerindex"]) : c.PlayerIndex;
            bool teamChanged = false, numberChanged = false;
            foreach (var (t, rowOf) in tables)
            {
                bool createdRow = false;
                if (!rowOf.TryGetValue(pi, out int row))
                {
                    row = CreateRosterRow(db, t, pi, c);
                    rowOf[pi] = row;
                    createdRow = true;
                    createdPis.Add(pi);
                }
                var f = t.Fields.ToDictionary(x => x.Name);
                int oldTeam = (int)db.Get(t, row, f["team"]);
                if (createdRow)
                {
                    // CreateRosterRow already set team, key, jersey and rosterstatus, and cleared the line slots.
                    arrivals.Add(pi);
                    touched.Add(c.ToTeam);
                    teamChanged = true;
                    if (c.NumberChanges) numberChanged = true;
                }
                else if (oldTeam != c.ToTeam)
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
            // Height and weight are written independently: Compare lists a size-only Update when EITHER
            // differs, so requiring both here would leave a player with only one dimension in the feed
            // listed forever with nothing ever written for him. `sizes` is counted once per player, not
            // once per dimension, so the summary format is unchanged.
            if (c.WithSizes)
            {
                bool wroteSize = false;
                if (c.NewHeight > 0 && c.NewHeight != c.OldHeight) { SetIn(bio, c.BioRow, bf["height"], c.NewHeight - HeightOffset); wroteSize = true; }
                if (c.NewWeight > 0 && c.NewWeight != c.OldWeight) { SetIn(bio, c.BioRow, bf["weight"], c.NewWeight - WeightOffset); wroteSize = true; }
                if (wroteSize) sizes++;
            }
        }
        // every team that gained or lost a player gets its lines built again from its players
        foreach (var (t, _) in tables)
        {
            foreach (int team in touched) RosterLines.Build(db, t, team);
            SortByKey(db, t);
        }
        return $"NHL roster sync: {moved} moved, {numbers} numbers, {sizes} heights/weights, {createdPis.Count} new roster rows, lines rebuilt for {touched.Count} teams";
    }

    /// <summary>
    /// Creates a roster row for a player who has a bio row and a players row but no roster row yet (a bio-only
    /// free agent), in one roster table. The row is a copy of a valid row of the destination team if there is one,
    /// otherwise of any valid row, with playerindex, team, key, jersey, rosterstatus and captaincy set for this
    /// player and every line-slot flag cleared. The free row is an invalid (empty) row inside the written records
    /// first; only when there is none is the table grown with SetWritten, as JerseySlots does. Returns the row.
    /// Throws when the table has no free row, no valid row to copy, or no free key on the team (40 players).
    /// </summary>
    private static int CreateRosterRow(T3Db db, T3Table t, int pi, SyncChange c)
    {
        var f = t.Fields.ToDictionary(x => x.Name);
        if (!f.ContainsKey("playerindex") || !f.ContainsKey("team") || !f.ContainsKey("key") || !f.ContainsKey("jersey"))
            throw new InvalidOperationException($"{t.Name} is missing playerindex, team, key or jersey.");
        void SetIn(int row, T3Field field, int v)
        {
            long max = field.RangeLow + (1L << field.Depth) - 1;
            if (v >= field.RangeLow && v <= max) db.Set(t, row, field, v);
        }
        // FreeKey must be taken before the new row is written: once written, the row itself would count as used.
        int key = FreeKey(db, t, c.ToTeam);
        int template = -1;
        for (int r = 0; r < t.WrittenRecords; r++)
            if (!db.IsInvalid(t, r) && (int)db.Get(t, r, f["team"]) == c.ToTeam) { template = r; break; }
        if (template < 0)
            for (int r = 0; r < t.WrittenRecords; r++)
                if (!db.IsInvalid(t, r)) { template = r; break; }
        if (template < 0) throw new InvalidOperationException($"{t.Name} has no valid row to copy for {c.Name}.");
        int row = -1;
        for (int r = 0; r < t.WrittenRecords; r++)
            if (db.IsInvalid(t, r)) { row = r; break; }
        if (row < 0)
        {
            if (t.WrittenRecords >= t.Records) throw new InvalidOperationException($"{t.Name} has no free roster row for {c.Name}.");
            row = t.WrittenRecords;
            db.SetWritten(t, row + 1);
        }
        db.WriteRecord(t, row, db.RecordBytes(t, template));
        SetIn(row, f["playerindex"], pi);
        SetIn(row, f["team"], c.ToTeam);
        SetIn(row, f["key"], key);
        if (c.NewNumber is >= 0 and <= 99) SetIn(row, f["jersey"], c.NewNumber);
        foreach (var s in t.Fields.Where(IsLineSlot)) db.Set(t, row, s, 0);
        if (f.TryGetValue("captain", out var cap)) SetIn(row, cap, 0);
        if (f.TryGetValue("rosterstatus", out var st)) SetIn(row, st, 1);
        return row;
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
