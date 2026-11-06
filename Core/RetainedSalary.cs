namespace NHL27Kit.Core;

/// <summary>
/// Retained salary, read-only: who retains part of a player's salary, how much, and his full cap hit.
///
/// What the game stores: the player's bio says how many former teams retain part of his salary (retainedsalarytrades)
/// and what his current team pays (contractdollars, $5,000 units); stockteamsalaryextras has one row per team (index =
/// team index) with 25 yearly slots of the dollars it still carries for players it traded away
/// (retainedtradesalaryyearN, whole dollars) and how many players they cover (numretainedsalariesyearN). Nothing ties
/// a team's dollars to a player, so it's worked out here.
///
/// A retained player with L seasons left (contractlength, this one included) shows up in his retaining team's slots
/// 0..L-1, so each team's drop from year L-1 to year L is the players whose deals end then: how many, and their
/// retained dollars together. All retained players are placed at once, season by season: each one (k teams for
/// retainedsalarytrades = k) on teams other than his own whose totals drop in his last season, so that every team's
/// drop is filled exactly and no retention is more than half the cap hit it was taken from (the CBA's 50% limit:
/// with one retaining team, the retained dollars are at most what his team now pays). Every placement that fits is
/// found; a team, or its dollars, is shown only when all of them agree on it — anything else stays undetermined
/// (shown as —), never guessed. Nothing here is ever written back.
/// </summary>
public static class RetainedSalary
{
    public const string Table = "stockteamsalaryextras";
    public const int Years = 25;
    /// <summary>The bio's cap hit is rounded to $5,000: a retention may be this much over "half" and still fit.</summary>
    private const long Slack = 5000;
    /// <summary>Stop looking past this many placements in one season (then nothing in that season is determined).</summary>
    private const int MaxSolutions = 20000;

    /// <summary>One retaining team's part of a player's salary: the team (game index) and its dollars, each null when the data doesn't pin it down.</summary>
    public sealed record Share(int? Team, long? Dollars);

    /// <summary>A retained player's retention: what his team pays now, and each retaining team's share.</summary>
    public sealed record Attribution(long CurrentDollars, IReadOnlyList<Share> Shares)
    {
        /// <summary>The full cap hit, when every share's dollars are known.</summary>
        public long? FullDollars => Shares.All(s => s.Dollars != null) ? CurrentDollars + Shares.Sum(s => s.Dollars.Value) : null;
        public double? Percent(Share s) => s.Dollars is { } d && FullDollars is { } f && f > 0 ? d * 100.0 / f : null;
    }

    private sealed record Player(int GameId, int Team, int Length, int Count, long Current);
    private sealed record Slot(int Team, int Count, long Dollars);

    /// <summary>Every retained player's retention, by game id. Players the data says nothing about are absent.</summary>
    public static Dictionary<int, Attribution> Solve(T3Db db)
    {
        var result = new Dictionary<int, Attribution>();
        var t = db.Table(Table);
        var bio = db.Table(Roster.BioTable);
        if (t == null || bio == null || Roster.Field(t, "index") == null || Roster.Field(bio, "retainedsalarytrades") == null) return result;

        var roster = new Roster(db);
        var players = new List<Player>();
        foreach (var p in roster.Players)
        {
            int k = Int(db, bio, p.BioRow, "retainedsalarytrades");
            if (k < 1 || p.BioRow < 0) continue;
            int length = Int(db, bio, p.BioRow, "contractlength");
            long current = (long)Int(db, bio, p.BioRow, "contractdollars") * ContractSync.DollarUnit;
            players.Add(new Player(p.GameId, p.Team, length, k, current));
        }

        // each team's drop in each season: the players whose retained deals end then
        var slots = new Dictionary<int, List<Slot>>();
        for (int r = 0; r < t.WrittenRecords; r++)
        {
            if (db.IsInvalid(t, r)) continue;
            int team = Int(db, t, r, "index");
            if (team < 0) continue;
            for (int l = 1; l <= Years; l++)
            {
                int n = Count(db, t, r, l - 1) - Count(db, t, r, l);
                long dollars = Total(db, t, r, l - 1) - Total(db, t, r, l);
                if (n > 0) (slots.TryGetValue(l, out var list) ? list : slots[l] = new List<Slot>()).Add(new Slot(team, n, dollars));
            }
        }

        foreach (var g in players.GroupBy(p => p.Length))
        {
            var ps = g.ToList();
            var ss = slots.GetValueOrDefault(g.Key) ?? new List<Slot>();
            foreach (var (p, a) in SolveSeason(ps, ss)) result[p.GameId] = a;
        }
        return result;
    }

    /// <summary>Places one season's retained players on the teams whose totals drop that season, every way that fits.</summary>
    private static IEnumerable<(Player, Attribution)> SolveSeason(List<Player> players, List<Slot> slots)
    {
        var unknown = players.Select(p => (p, new Attribution(p.Current, Enumerable.Range(0, p.Count).Select(_ => new Share(null, null)).ToList()))).ToList();
        if (players.Count == 0 || slots.Count == 0 || players.Any(p => p.Length < 1 || p.Length > Years)) return unknown;

        // fewest choices first, so dead ends show up early
        var order = players.OrderBy(p => slots.Count(s => s.Team != p.Team)).ThenBy(p => p.GameId).ToList();
        var left = slots.Select(s => s.Count).ToArray();
        var pick = new int[order.Count][];
        var exact = new List<int[][]>();
        var loose = new List<int[][]>();
        bool overflow = false;

        bool Fits(Player p, int[] teams)
        {
            // only shares from a team holding just this one player are known dollars; the 50% limit is checked on those
            var known = teams.Where(i => slots[i].Count == 1).Select(i => slots[i].Dollars).OrderBy(d => d).ToList();
            if (known.Any(d => d <= 0)) return false;
            if (p.Count == 1) return known.Count == 0 || known[0] <= p.Current + Slack;
            // two (or more) retentions: the latest is at most what he pays now, each earlier one at most what was left after it
            long carried = p.Current;
            foreach (long d in known) { if (d > carried + Slack) return false; carried += d; }
            return true;
        }

        void Search(int i)
        {
            if (overflow) return;
            if (i == order.Count)
            {
                var copy = pick.Select(x => (int[])x.Clone()).ToArray();
                (left.All(x => x == 0) ? exact : loose).Add(copy);
                if (exact.Count + loose.Count > MaxSolutions) overflow = true;
                return;
            }
            var p = order[i];
            var open = Enumerable.Range(0, slots.Count).Where(s => left[s] > 0 && slots[s].Team != p.Team).ToList();
            foreach (var combo in Combinations(open, p.Count))
            {
                if (combo.Select(s => slots[s].Team).Distinct().Count() != combo.Length || !Fits(p, combo)) continue;
                foreach (int s in combo) left[s]--;
                pick[i] = combo;
                Search(i + 1);
                foreach (int s in combo) left[s]++;
                if (overflow) return;
            }
        }

        Search(0);
        // A placement that explains every team's drop beats one that leaves some unexplained (a retained player the
        // game doesn't have); with none of either, nothing is known.
        var solutions = exact.Count > 0 ? exact : loose;
        if (overflow || solutions.Count == 0) return unknown;

        var answers = new List<(Player, Attribution)>();
        for (int i = 0; i < order.Count; i++)
        {
            var p = order[i];
            // the teams in a stable order (by team index), so the k shares line up across solutions
            var sets = solutions.Select(sol => sol[i].OrderBy(s => slots[s].Team).ToArray()).ToList();
            var shares = new List<Share>();
            for (int j = 0; j < p.Count; j++)
            {
                var teams = sets.Select(set => slots[set[j]].Team).Distinct().ToList();
                var dollars = sets.Select(set => slots[set[j]].Count == 1 ? slots[set[j]].Dollars : (long?)null).Distinct().ToList();
                shares.Add(new Share(teams.Count == 1 ? teams[0] : null, dollars.Count == 1 ? dollars[0] : null));
            }
            answers.Add((p, new Attribution(p.Current, shares)));
        }
        return answers;
    }

    private static IEnumerable<int[]> Combinations(List<int> items, int k)
    {
        if (k == 0) { yield return Array.Empty<int>(); yield break; }
        for (int i = 0; i <= items.Count - k; i++)
            foreach (var rest in Combinations(items.GetRange(i + 1, items.Count - i - 1), k - 1))
                yield return new[] { items[i] }.Concat(rest).ToArray();
    }

    private static int Count(T3Db db, T3Table t, int r, int year) => year >= Years ? 0 : Int(db, t, r, $"numretainedsalariesyear{year}");
    private static long Total(T3Db db, T3Table t, int r, int year) => year >= Years ? 0 : Int(db, t, r, $"retainedtradesalaryyear{year}");
    private static int Int(T3Db db, T3Table t, int r, string f) => Roster.Field(t, f) is { } x ? (int)db.Get(t, r, x) : 0;

    // ---------------------------------------------------------------- what CapWages says, saved by the sync

    /// <summary>One retaining team's share as CapWages lists it this season: the team (abbreviation) and its dollars.</summary>
    public sealed record NoteShare(string Team, long Dollars);
    /// <summary>A retained player as CapWages listed him when the sync last read it: his game id and name, what his team pays, and who retains the rest.</summary>
    public sealed record Note(int GameId, string Name, long Current, List<NoteShare> Shares);
    /// <summary>The retained players from the sync's last CapWages read (the real season, and when it was read).</summary>
    public sealed record NoteFile(int Season, DateTime Saved, List<Note> Players);

    public const string NotesFile = "retained-salary.json";

    /// <summary>Every retained player CapWages lists this season that's in the game (matched by name and birth date).</summary>
    public static NoteFile FromCapWages(T3Db db, IReadOnlyList<CapTeam> teams)
    {
        int now = CapWagesApi.CurrentSeason(teams);
        var matcher = new PlayerMatcher(db, new Roster(db));
        var notes = new List<Note>();
        var seen = new HashSet<int>();
        foreach (var team in teams)
            foreach (var cp in team.Players)
            {
                var s0 = cp.Contracts.SelectMany(c => c.Seasons).FirstOrDefault(s => s.StartYear == now);
                if (s0 == null || s0.CapHit <= 0) continue;
                var shares = s0.Retention.Where(r => r.Team != "*").Select(r => new NoteShare(NhlApi.TeamAbbr(r.Team), r.CapHit))
                    .Where(r => r.Team != team.Abbr && r.Dollars > 0).ToList();
                if (shares.Count == 0) continue;
                var (p, nameOnly) = matcher.Find(cp.First, cp.Last, cp.Born);
                if (p == null || nameOnly || !seen.Add(p.GameId)) continue;
                notes.Add(new Note(p.GameId, p.Name, s0.CapHit - shares.Sum(s => s.Dollars), shares));
            }
        return new NoteFile(now, DateTime.Now, notes);
    }

    public static void SaveNotes(string dir, NoteFile notes)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, NotesFile), System.Text.Json.JsonSerializer.Serialize(notes, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>The saved notes, or null when there are none (or the file can't be read).</summary>
    public static NoteFile LoadNotes(string dir)
    {
        try
        {
            string file = Path.Combine(dir ?? "", NotesFile);
            return File.Exists(file) ? System.Text.Json.JsonSerializer.Deserialize<NoteFile>(File.ReadAllText(file)) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { return null; }
    }

    /// <summary>A player's retention as the profile shows it, and where it comes from.</summary>
    public sealed record Shown(Attribution Attribution, bool FromCapWages);

    /// <summary>
    /// What the profile shows for a retained player: CapWages' answer from the sync's notes when it still fits the game
    /// (same number of retaining teams, the same amount his team pays, and every retaining team's totals dropping in his
    /// last season — by exactly that amount when the drop is his alone), else what the game's data proves on its own
    /// (Solve). Null when he isn't retained in the game.
    /// </summary>
    public static Shown For(T3Db db, int gameId, NoteFile notes)
    {
        var solved = Solve(db);
        if (!solved.TryGetValue(gameId, out var own)) return null;
        var note = notes?.Players.FirstOrDefault(n => n.GameId == gameId);
        if (note != null && Fits(db, gameId, note, own) is { } fromNote) return new Shown(fromNote, true);
        return new Shown(own, false);
    }

    private static Attribution Fits(T3Db db, int gameId, Note note, Attribution own)
    {
        if (note.Shares.Count != own.Shares.Count) return null;
        // the bio's amount is in $5,000 units
        if (Math.Abs(note.Current - own.CurrentDollars) > ContractSync.DollarUnit / 2) return null;
        var codes = RosterSync.TeamIndex(db);
        var roster = new Roster(db);
        var p = roster.Players.FirstOrDefault(x => x.GameId == gameId);
        var bio = db.Table(Roster.BioTable);
        var t = db.Table(Table);
        if (p == null || t == null) return null;
        int length = Int(db, bio, p.BioRow, "contractlength");
        if (length < 1 || length > Years) return null;
        var shares = new List<Share>();
        foreach (var s in note.Shares)
        {
            if (!codes.TryGetValue(s.Team, out int team) || team == p.Team) return null;
            int row = -1;
            for (int r = 0; r < t.WrittenRecords && row < 0; r++)
                if (!db.IsInvalid(t, r) && Int(db, t, r, "index") == team) row = r;
            if (row < 0) return null;
            int n = Count(db, t, row, length - 1) - Count(db, t, row, length);
            long dollars = Total(db, t, row, length - 1) - Total(db, t, row, length);
            if (n < 1 || n == 1 && Math.Abs(dollars - s.Dollars) > 1000) return null;
            // and what the game's own data does pin down must agree
            if (own.Shares.Any(o => o.Team is { } ot && !note.Shares.Any(x => codes.GetValueOrDefault(x.Team, -1) == ot))) return null;
            shares.Add(new Share(team, s.Dollars));
        }
        return new Attribution(own.CurrentDollars, shares);
    }
}
