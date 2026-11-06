using System.Text.RegularExpressions;

namespace NHL27Kit.Core;

/// <summary>
/// Builds a team's lines in a roster table the way the game's own rosters have them. A team dresses 20 players
/// (rosterstatus 1: 12 forwards, 6 D, 2 goalies) who hold its 92 line-slot flags, and scratches the rest (rosterstatus 0,
/// no slots). The slots form units: l1-l4 (lw, c, rw; l1-l3 also ld, rd), pp_1/pp_2 (5), pp4_1/2, pk4_1-3 and ev4_1-3
/// (lw, c, ld, rd), pk3_1-3 and ev3_1-3 (c, ld, rd), three (c, w, d, s1, s2, g1, g2), s1-s5 (shootout) and g1/g2. Each
/// slot is held by exactly one player, and a player holds at most one slot per category (lines, PP, PP 4-on-3, PK…).
/// Units are filled best first by overall rating and position.
/// </summary>
public static class RosterLines
{
    private sealed record Player(int Row, int Position, int Overall, bool ShootsLeft)
    {
        public bool Forward => Position is >= 0 and <= 2;
        public bool Defense => Position == 3;
        public bool Goalie => Position == 4;
    }

    /// <summary>The category of a unit (players are used once per category).</summary>
    public static string Category(string slot)
    {
        if (slot.StartsWith("three_")) return "three";
        if (Regex.IsMatch(slot, @"^s\d$")) return "s";
        if (Regex.IsMatch(slot, @"^g\d$")) return "g";
        var m = Regex.Match(slot, @"^(l|pp4?|pk[34]|ev[34])_?\d");
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>The unit a slot is in (l1, pp_2, pk4_3, three, s, g), in the order units are filled.</summary>
    public static string Unit(string slot)
    {
        if (slot.StartsWith("three_")) return "three";
        if (Regex.IsMatch(slot, @"^[sg]\d$")) return slot[..1];
        return Regex.Match(slot, @"^(l\d|pp4?_\d|pk[34]_\d|ev[34]_\d)").Value;
    }

    /// <summary>The order of categories: lines first (they decide who is on which line), then special units.</summary>
    private static readonly string[] CategoryOrder = { "g", "l", "pp", "pp4", "pk4", "pk3", "ev4", "ev3", "three", "s" };

    /// <summary>Rebuilds every slot of one team in one roster table. Returns the number of players dressed.</summary>
    public static int Build(T3Db db, T3Table t, int team)
    {
        var f = t.Fields.ToDictionary(x => x.Name);
        var slots = t.Fields.Where(x => x.Type == T3FieldType.Integer && x.Depth == 1 && Category(x.Name) != null).ToList();
        var info = PlayerInfo(db);
        var players = new List<Player>();
        for (int r = 0; r < t.WrittenRecords; r++)
        {
            if (db.IsInvalid(t, r) || (int)db.Get(t, r, f["team"]) != team) continue;
            var (pos, ovr, left) = info.GetValueOrDefault((int)db.Get(t, r, f["playerindex"]), (-1, 0, true));
            if (pos >= 0) players.Add(new Player(r, pos, ovr, left));
        }
        if (players.Count == 0) return 0;

        // dress the best 2 goalies, 6 D and 12 forwards (a short position is filled from the other skaters)
        var dressed = players.Where(p => p.Goalie).OrderByDescending(p => p.Overall).Take(2).ToList();
        var d = players.Where(p => p.Defense).OrderByDescending(p => p.Overall).Take(6).ToList();
        var fw = players.Where(p => p.Forward).OrderByDescending(p => p.Overall).Take(12).ToList();
        var extra = players.Where(p => !p.Goalie && !d.Contains(p) && !fw.Contains(p)).OrderByDescending(p => p.Overall).ToList();
        while (d.Count < 6 && extra.Count > 0) { d.Add(extra[0]); extra.RemoveAt(0); }
        while (fw.Count < 12 && extra.Count > 0) { fw.Add(extra[0]); extra.RemoveAt(0); }
        dressed.AddRange(d);
        dressed.AddRange(fw);

        foreach (var p in players)
        {
            foreach (var s in slots) db.Set(t, p.Row, s, 0);
            if (f.TryGetValue("rosterstatus", out var st)) db.Set(t, p.Row, st, dressed.Contains(p) ? 1 : 0);
        }

        // units in fill order: categories, then unit number
        var units = slots.GroupBy(s => Unit(s.Name))
            .OrderBy(g => Array.IndexOf(CategoryOrder, Category(g.First().Name)))
            .ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
        var usedIn = new Dictionary<string, HashSet<Player>>();
        foreach (var unit in units)
        {
            string cat = Category(unit.First().Name);
            if (!usedIn.TryGetValue(cat, out var used)) usedIn[cat] = used = new HashSet<Player>();
            // centres first, then wings, then D (left before right), then the rest
            foreach (var slot in unit.OrderBy(s => SlotRank(s.Name)))
            {
                var pick = Best(dressed, used, slot.Name);
                if (pick == null) continue;
                db.Set(t, pick.Row, slot, 1);
                used.Add(pick);
            }
        }
        return dressed.Count;
    }

    /// <summary>
    /// A team's lines as Build would make them, with some slots fixed in advance (slot → playerindex, e.g. from real
    /// line combinations), read from the roster copy the game plays with and returned (slot → playerindex, -1 for
    /// nobody) for WriteTeam instead of written. The fixed players are dressed first, then the team is topped up to
    /// 2 goalies and 18 skaters (D to 6 first) by overall; the slots left open are filled as Build fills them, never
    /// with a player twice in one category; a skater slot no player of its position can take goes to any dressed skater
    /// left (a 7th D on the 4th line when a team dresses 11 forwards). <paramref name="dress"/> are more players to
    /// dress before the top-up (a lineup's extra D). Entries for players not on the team, or slots the table lacks,
    /// are ignored.
    /// </summary>
    public static Dictionary<string, int> Plan(T3Db db, int team, IReadOnlyDictionary<string, int> fixedSlots, IEnumerable<int> dress = null)
    {
        var t = db.Table(RosterSync.OnlineRosterTable) ?? db.Table(Roster.RosterTable);
        var f = t.Fields.ToDictionary(x => x.Name);
        var slots = t.Fields.Where(x => x.Type == T3FieldType.Integer && x.Depth == 1 && Category(x.Name) != null).Select(x => x.Name).ToList();
        var info = PlayerInfo(db);
        var players = new Dictionary<int, Player>();
        for (int r = 0; r < t.WrittenRecords; r++)
        {
            if (db.IsInvalid(t, r) || (int)db.Get(t, r, f["team"]) != team) continue;
            int pi = (int)db.Get(t, r, f["playerindex"]);
            var (pos, ovr, left) = info.GetValueOrDefault(pi, (-1, 0, true));
            if (pos >= 0) players.TryAdd(pi, new Player(pi, pos, ovr, left));
        }
        var map = slots.ToDictionary(s => s, _ => -1);
        if (players.Count == 0) return map;
        var fixedOk = fixedSlots.Where(kv => map.ContainsKey(kv.Key) && players.ContainsKey(kv.Value)).ToDictionary(kv => kv.Key, kv => kv.Value);

        var dressed = fixedOk.Values.Concat((dress ?? Enumerable.Empty<int>()).Where(players.ContainsKey)).Distinct().Select(pi => players[pi]).ToList();
        var rest = players.Values.Where(p => !dressed.Contains(p)).OrderByDescending(p => p.Overall).ToList();
        void Add(Func<Player, bool> fits, Func<bool> more)
        {
            foreach (var p in rest.Where(fits).ToList())
            {
                if (!more()) break;
                dressed.Add(p);
                rest.Remove(p);
            }
        }
        Add(p => p.Goalie, () => dressed.Count(x => x.Goalie) < 2);
        Add(p => p.Defense, () => dressed.Count(x => x.Defense) < 6 && dressed.Count(x => !x.Goalie) < 18);
        Add(p => p.Forward, () => dressed.Count(x => !x.Goalie) < 18);
        Add(p => !p.Goalie, () => dressed.Count(x => !x.Goalie) < 18);

        var usedIn = new Dictionary<string, HashSet<Player>>();
        HashSet<Player> Used(string slot)
        {
            string cat = Category(slot);
            if (!usedIn.TryGetValue(cat, out var u)) usedIn[cat] = u = new HashSet<Player>();
            return u;
        }
        foreach (var (slot, pi) in fixedOk) Used(slot).Add(players[pi]);
        var units = slots.GroupBy(Unit).OrderBy(g => Array.IndexOf(CategoryOrder, Category(g.First()))).ThenBy(g => g.Key, StringComparer.Ordinal);
        foreach (var unit in units)
            foreach (var slot in unit.OrderBy(SlotRank))
            {
                if (fixedOk.TryGetValue(slot, out int pi)) { map[slot] = pi; continue; }
                var used = Used(slot);
                var pick = Best(dressed, used, slot);
                bool goalieSlot = Regex.IsMatch(slot, @"^g\d$") || slot.StartsWith("three_g");
                pick ??= goalieSlot ? null : dressed.Where(p => !used.Contains(p) && !p.Goalie).OrderByDescending(p => p.Overall).FirstOrDefault();
                if (pick == null) continue;
                map[slot] = pick.Row;
                used.Add(pick);
            }
        return map;
    }

    public static int SlotRank(string slot)
    {
        string end = Regex.Match(slot, @"(lw|rw|c|ld|rd|w|d|s\d|g\d|\d)$").Value;
        return end switch { "c" => 0, "lw" => 1, "rw" => 2, "w" => 2, "ld" => 3, "rd" => 4, "d" => 3, _ => 5 };
    }

    /// <summary>The best unused dressed player for a slot: by position (with fallbacks), then overall.</summary>
    private static Player Best(List<Player> dressed, HashSet<Player> used, string slot)
    {
        var free = dressed.Where(p => !used.Contains(p)).ToList();
        IEnumerable<Player> Pick(Func<Player, bool> fits) => free.Where(fits).OrderByDescending(p => p.Overall);
        string end = Regex.Match(slot, @"(lw|rw|c|ld|rd)$").Value;
        if (Regex.IsMatch(slot, @"^g\d$") || slot.StartsWith("three_g")) return Pick(p => p.Goalie).FirstOrDefault();
        if (slot == "three_d") end = "ld";
        if (slot == "three_w") end = "lw";
        if (slot == "three_c") end = "c";
        return end switch
        {
            "c" => Pick(p => p.Position == 0).Concat(Pick(p => p.Forward)).FirstOrDefault(),
            "lw" => Pick(p => p.Position == 1).Concat(Pick(p => p.Forward)).FirstOrDefault(),
            "rw" => Pick(p => p.Position == 2).Concat(Pick(p => p.Forward)).FirstOrDefault(),
            // D: shooting side first
            "ld" => Pick(p => p.Defense && p.ShootsLeft).Concat(Pick(p => p.Defense)).Concat(Pick(p => p.Forward)).FirstOrDefault(),
            "rd" => Pick(p => p.Defense && !p.ShootsLeft).Concat(Pick(p => p.Defense)).Concat(Pick(p => p.Forward)).FirstOrDefault(),
            // shootout and 3-on-3 extra skaters: the best skaters left
            _ => Pick(p => p.Forward).Concat(Pick(p => p.Defense)).FirstOrDefault(),
        };
    }

    /// <summary>The order categories are shown in the editor (as in the game's lineup screens).</summary>
    private static readonly string[] ShowOrder = { "l", "pp", "pp4", "pk4", "pk3", "ev4", "ev3", "three", "s", "g" };

    /// <summary>Spots in a unit as the game lists them: LW, C, RW, then LD, RD.</summary>
    private static int DisplayRank(string slot)
    {
        if (slot.StartsWith("three_")) return Array.IndexOf(new[] { "three_w", "three_c", "three_d", "three_s1", "three_s2", "three_g1", "three_g2" }, slot);
        string end = Regex.Match(slot, @"(lw|rw|c|ld|rd)$").Value;
        return end switch { "lw" => 0, "c" => 1, "rw" => 2, "ld" => 3, "rd" => 4, _ => 5 } * 10 + (slot.Length > 1 && char.IsDigit(slot[^1]) ? slot[^1] - '0' : 0);
    }

    /// <summary>A team's player for the lines editor.</summary>
    public sealed record LinePlayer(int PlayerIndex, string Name, int Position, int Overall, bool ShootsLeft, bool Dressed)
    {
        public string PositionName => Roster.PositionName(Position);
        public override string ToString() => $"{PositionName}  {Overall}  {Name}";
    }

    /// <summary>The slot names of the roster table in the order units are shown (lines, PP, PK…, then position order).</summary>
    public static List<string> SlotNames(T3Db db)
    {
        var t = db.Table(RosterSync.OnlineRosterTable) ?? db.Table(Roster.RosterTable);
        return t.Fields.Where(x => x.Type == T3FieldType.Integer && x.Depth == 1 && Category(x.Name) != null).Select(x => x.Name)
            .OrderBy(n => Array.IndexOf(ShowOrder, Category(n))).ThenBy(Unit, StringComparer.Ordinal).ThenBy(DisplayRank).ToList();
    }

    /// <summary>
    /// A team's players and who holds each slot (slot → playerindex; -1 when nobody), read from the roster copy the game
    /// plays with.
    /// </summary>
    public static (List<LinePlayer> Players, Dictionary<string, int> Slots) ReadTeam(T3Db db, int team)
    {
        var t = db.Table(RosterSync.OnlineRosterTable) ?? db.Table(Roster.RosterTable);
        var f = t.Fields.ToDictionary(x => x.Name);
        var names = SlotNames(db);
        var info = PlayerInfo(db);
        var nameOf = NamesByPlayerIndex(db);
        var players = new List<LinePlayer>();
        var slots = names.ToDictionary(n => n, _ => -1);
        for (int r = 0; r < t.WrittenRecords; r++)
        {
            if (db.IsInvalid(t, r) || (int)db.Get(t, r, f["team"]) != team) continue;
            int pi = (int)db.Get(t, r, f["playerindex"]);
            var (pos, ovr, left) = info.GetValueOrDefault(pi, (-1, 0, true));
            players.Add(new LinePlayer(pi, nameOf.GetValueOrDefault(pi, $"player {pi}"), pos, ovr, left, (int)db.Get(t, r, f["rosterstatus"]) == 1));
            foreach (var n in names)
                if ((int)db.Get(t, r, f[n]) == 1) slots[n] = pi;
        }
        players.Sort((a, b) => a.Position != b.Position ? a.Position.CompareTo(b.Position) : b.Overall.CompareTo(a.Overall));
        return (players, slots);
    }

    /// <summary>
    /// Writes a team's lines (slot → playerindex) into both roster tables: every slot flag of the team's rows is set from
    /// the map, and a player is dressed (rosterstatus 1) when he holds any slot, scratched otherwise.
    /// </summary>
    public static void WriteTeam(T3Db db, int team, IReadOnlyDictionary<string, int> slots)
    {
        using var batch = db.Batch();
        foreach (var name in new[] { Roster.RosterTable, RosterSync.OnlineRosterTable })
        {
            var t = db.Table(name);
            if (t == null) continue;
            var f = t.Fields.ToDictionary(x => x.Name);
            for (int r = 0; r < t.WrittenRecords; r++)
            {
                if (db.IsInvalid(t, r) || (int)db.Get(t, r, f["team"]) != team) continue;
                int pi = (int)db.Get(t, r, f["playerindex"]);
                bool any = false;
                foreach (var (slot, holder) in slots)
                {
                    if (!f.TryGetValue(slot, out var sf)) continue;
                    bool on = holder == pi;
                    any |= on;
                    if ((int)db.Get(t, r, sf) != (on ? 1 : 0)) db.Set(t, r, sf, on ? 1 : 0);
                }
                if (f.TryGetValue("rosterstatus", out var st) && (int)db.Get(t, r, st) != (any ? 1 : 0)) db.Set(t, r, st, any ? 1 : 0);
            }
        }
    }

    /// <summary>Builds a team's lines automatically (as Build does) in both roster tables.</summary>
    public static void BuildTeam(T3Db db, int team)
    {
        using var batch = db.Batch();
        foreach (var name in new[] { Roster.RosterTable, RosterSync.OnlineRosterTable })
            if (db.Table(name) is { } t) Build(db, t, team);
    }

    private static Dictionary<int, string> NamesByPlayerIndex(T3Db db)
    {
        var bio = db.Table(Roster.BioTable);
        var bf = bio.Fields.ToDictionary(x => x.Name);
        var byId = new Dictionary<int, string>();
        for (int r = 0; r < bio.WrittenRecords; r++)
            if (!db.IsInvalid(bio, r))
                byId[(int)db.Get(bio, r, bf["game_id"])] = $"{db.Get(bio, r, bf["firstname"])} {db.Get(bio, r, bf["lastname"])}".Trim();
        var players = db.Table(Roster.PlayersTable);
        var pf = players.Fields.ToDictionary(x => x.Name);
        var map = new Dictionary<int, string>();
        for (int r = 0; r < players.WrittenRecords; r++)
            if (!db.IsInvalid(players, r) && byId.TryGetValue((int)db.Get(players, r, pf["playerid"]), out var n))
                map[(int)db.Get(players, r, pf["index"])] = n;
        return map;
    }

    /// <summary>Each roster playerindex's position, overall rating and whether he shoots left (bio handedness 0).</summary>
    private static Dictionary<int, (int Position, int Overall, bool Left)> PlayerInfo(T3Db db)
    {
        var bio = db.Table(Roster.BioTable);
        var bf = bio.Fields.ToDictionary(x => x.Name);
        var byId = new Dictionary<int, (int, int, bool)>();
        var overall = new Dictionary<int, int>();
        foreach (var name in new[] { Roster.SkaterTable, Roster.GoalieTable })
        {
            var t = db.Table(name);
            var tf = t.Fields.ToDictionary(x => x.Name);
            for (int r = 0; r < t.WrittenRecords; r++)
                if (!db.IsInvalid(t, r)) overall[(int)db.Get(t, r, tf["game_id"])] = (int)db.Get(t, r, tf["skill_overall"]);
        }
        for (int r = 0; r < bio.WrittenRecords; r++)
        {
            if (db.IsInvalid(bio, r)) continue;
            int id = (int)db.Get(bio, r, bf["game_id"]);
            byId[id] = ((int)db.Get(bio, r, bf["position"]), overall.GetValueOrDefault(id), (int)db.Get(bio, r, bf["handedness"]) == 0);
        }
        var players = db.Table(Roster.PlayersTable);
        var pf = players.Fields.ToDictionary(x => x.Name);
        var map = new Dictionary<int, (int, int, bool)>();
        for (int r = 0; r < players.WrittenRecords; r++)
            if (!db.IsInvalid(players, r) && byId.TryGetValue((int)db.Get(players, r, pf["playerid"]), out var v))
                map[(int)db.Get(players, r, pf["index"])] = v;
        return map;
    }

    /// <summary>
    /// The NHL teams whose lines don't look like the game's: a slot held by nobody or by two players, or a dressed
    /// player (rosterstatus 1) without a slot.
    /// </summary>
    public static HashSet<int> TeamsNeedingLines(T3Db db, T3Table t, IEnumerable<int> teams)
    {
        var f = t.Fields.ToDictionary(x => x.Name);
        var slots = t.Fields.Where(x => x.Type == T3FieldType.Integer && x.Depth == 1 && Category(x.Name) != null).ToList();
        var need = new HashSet<int>();
        foreach (int team in teams)
        {
            var rows = Enumerable.Range(0, t.WrittenRecords).Where(r => !db.IsInvalid(t, r) && (int)db.Get(t, r, f["team"]) == team).ToList();
            if (rows.Count == 0) continue;
            bool bad = slots.Any(s => rows.Count(r => (int)db.Get(t, r, s) == 1) != 1) ||
                       rows.Any(r => (int)db.Get(t, r, f["rosterstatus"]) == 1 && !slots.Any(s => (int)db.Get(t, r, s) == 1));
            if (bad) need.Add(team);
        }
        return need;
    }
}
