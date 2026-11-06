using System.Text.RegularExpressions;

namespace NHL27Kit.Core;

public enum ContractChangeKind
{
    /// <summary>A player's contract fields differ from CapWages: new deal, extension, clauses or retention count.</summary>
    Contract,
    /// <summary>A team's retained-salary yearly totals differ (stockteamsalaryextras): rebuilt from every retained contract.</summary>
    TeamRetention,
}

/// <summary>A player's contract as the game stores it (or should): status, his team's share in dollars, years left, the
/// signed extension, and one clause + no-trade list size per season slot (current deal first, then the extension).</summary>
public sealed record GameContract(int Status, long Dollars, int Length, bool TwoWay, int ExtLength, long ExtDollars, bool ExtTwoWay,
                                  int[] Clauses, int[] ListSizes, int Retained);

/// <summary>One difference between the game's contracts and CapWages', and what applying it writes.</summary>
public sealed class ContractChange
{
    public ContractChangeKind Kind { get; init; }
    public int GameId { get; init; } = -1;
    public int BioRow { get; init; } = -1;
    public string Name { get; init; }
    public string Position { get; init; }
    /// <summary>The team CapWages lists him with (abbreviation), or the team whose totals change.</summary>
    public string TeamAbbr { get; init; }
    public GameContract Old { get; init; }
    public GameContract New { get; init; }
    /// <summary>Contract type and expiry from CapWages ("Two-Way Contract · RFA"), for the row's note.</summary>
    public string Source { get; init; }
    public bool NameOnly { get; init; }

    public int Team { get; init; } = -1;
    public long[] OldTotals { get; init; }
    public long[] NewTotals { get; init; }
    public int[] OldCounts { get; init; }
    public int[] NewCounts { get; init; }

    public bool Selected { get; set; }
    public bool CanApply => Kind == ContractChangeKind.TeamRetention ? Team >= 0 : BioRow >= 0;
    /// <summary>Identifies the change across a fresh re-compare (Apply re-compares, then applies the ticked keys).</summary>
    public (ContractChangeKind, int, int) Key => (Kind, GameId, Team);

    /// <summary>What applying does, in words.</summary>
    public string Description => Kind == ContractChangeKind.TeamRetention
        ? $"retained salary {ContractSync.Millions(OldTotals[0])} → {ContractSync.Millions(NewTotals[0])} this season ({OldCounts[0]} → {NewCounts[0]} player{(NewCounts[0] == 1 ? "" : "s")})"
          + (OldTotals.Skip(1).SequenceEqual(NewTotals.Skip(1)) ? "" : ", later seasons too")
        : string.Join("  ·  ", Parts());

    private IEnumerable<string> Parts()
    {
        string Deal(GameContract c) => c.Status != 0 ? "no contract" : $"{ContractSync.Millions(c.Dollars)} × {c.Length} yr{(c.Length == 1 ? "" : "s")}{(c.TwoWay ? " 2-way" : "")}";
        string Ext(GameContract c) => c.ExtLength == 0 ? "none" : $"{ContractSync.Millions(c.ExtDollars)} × {c.ExtLength} yr{(c.ExtLength == 1 ? "" : "s")}{(c.ExtTwoWay ? " 2-way" : "")}";
        if (Deal(Old) != Deal(New)) yield return $"{Deal(Old)} → {Deal(New)}";
        if (Ext(Old) != Ext(New)) yield return $"extension {Ext(Old)} → {Ext(New)}";
        if (!Old.Clauses.SequenceEqual(New.Clauses) || !Old.ListSizes.SequenceEqual(New.ListSizes))
            yield return $"clauses {ContractSync.ClauseText(Old)} → {ContractSync.ClauseText(New)}";
        if (Old.Retained != New.Retained) yield return $"retained by {Old.Retained} → {New.Retained} team{(New.Retained == 1 ? "" : "s")}";
    }
}

/// <summary>
/// Contract sync against CapWages. For every game player CapWages lists under contract (matched with PlayerMatcher,
/// like the roster sync), it works out what the game should hold and lists the differences; Apply writes the ticked ones.
///
/// How the game stores contracts (exhibitionplayerbiotable), decoded by matching the shipped NHL 27 data against
/// CapWages (2026-27 = the game's year 0 for 888 of 1269 unchanged deals, 1,351/615/313/102 clause rows agreeing):
/// - An unsigned player has contractlength 0 and contractdollars 0; contractstatus then says whose he is: 0 = his
///   team holds his rights (an unsigned RFA such as Simon Edvinsson), -1 = a free agent. Signed players are 0.
/// - contractdollars = this season's cap hit in $5,000 units (rounded to the nearest, halves up), and for a player
///   whose former team retains part of it, only his current team's share (Blake Coleman: $2.45M of $4.9M).
/// - contractlength = seasons left including this one; extensionlength/extensiondollars = a signed deal that hasn't
///   started (full cap hit, $5,000 units); contractis2way/extensionis2way = a two-way or entry-level deal (by contract type).
/// - contractclauseN + contractclausemntcnumteamsN, N = 0..9: one slot per season, current deal then extension.
///   Values: 0 none, 1 M-NTC (team list), 2 NMC with a modified no-trade list, 3 NTC (full), 4 NMC (full). The list
///   is the no-trade count: a "10 team trade list" (teams he can be traded to) is stored as 22 (McAvoy, Pastrnak).
///   The game's clauses are more precise than CapWages' per-season fields (list sizes that change year to year live
///   only in CapWages' free text), so clauses are written only for a deal that is new or changed; an unchanged deal
///   keeps the game's own clauses.
/// - retainedsalarytrades = how many former teams retain part of his salary.
/// - stockteamsalaryextras: per team, 25 yearly slots of retained dollars (whole dollars) and how many players they cover.
/// Never written, because they don't mean what their names suggest in the shipped data: contractneedsentrylevel (0 for
/// all 213 players on entry-level deals) and contractismultiyear (doesn't follow the deal's length). Buyouts and other
/// dead cap (cbaexpenseyearN) aren't synced: that field mixes buyouts with other charges and isn't decoded yet.
///
/// Seasons map by distance from now, not calendar year: CapWages' current season (CapWagesApi.CurrentSeason) is the
/// game's year 0 in either game, so a 6-year deal is 6 years left in NHL 24 too.
/// </summary>
public static class ContractSync
{
    public const int ClauseSlots = 10;
    public const int RetentionYears = 25;
    public const int DollarUnit = 5000;
    public const string SalaryExtrasTable = "stockteamsalaryextras";

    public static string Millions(long dollars) => dollars % 1_000_000 == 0 ? $"${dollars / 1_000_000}M" : $"${dollars / 1_000_000.0:0.###}M";

    /// <summary>The game's clause value for CapWages' clause text: "M-NTC" 1, "M-NTC, NMC" 2, "NTC" 3, "NMC" / "NTC, NMC" / "M-NMC" 4, "15-team NTC" 1.</summary>
    public static int ClauseValue(string clause)
    {
        string c = (clause ?? "").ToUpperInvariant().Replace(" ", "");
        if (c.Length == 0) return 0;
        bool nmc = c.Contains("NMC"), mntc = c.Contains("M-NTC") || Regex.IsMatch(c, @"^\d+-TEAMNTC");
        if (nmc) return mntc ? 2 : 4;
        if (mntc) return 1;
        return c.Contains("NTC") ? 3 : 0;
    }

    /// <summary>"M-NTC (10) ×3, NMC ×2" — the clause slots in words.</summary>
    public static string ClauseText(GameContract c)
    {
        string Name(int v, int n) => v switch { 1 => $"M-NTC ({n})", 2 => $"NMC+list ({n})", 3 => "NTC", 4 => "NMC", _ => "none" };
        var runs = new List<string>();
        int end = Array.FindLastIndex(c.Clauses, v => v != 0) + 1;
        if (end == 0) return "none";
        for (int i = 0; i < end;)
        {
            int j = i;
            while (j < end && c.Clauses[j] == c.Clauses[i] && c.ListSizes[j] == c.ListSizes[i]) j++;
            runs.Add(Name(c.Clauses[i], c.ListSizes[i]) + (j - i > 1 ? $" ×{j - i}" : ""));
            i = j;
        }
        return string.Join(", ", runs);
    }

    /// <summary>
    /// The no-trade list size in the clause texts, as the game stores it: "10 team no trade list" -> 10, and a list of
    /// teams he CAN be traded to ("10 team trade list", "8 team TO trade list") -> 32 - 10 = 22. Null when no size is given.
    /// </summary>
    private static int? ListSize(params string[] texts)
    {
        foreach (var t in texts)
        {
            var m = Regex.Match(t ?? "", @"(\d+)[- ]team\s+(no[- ]trade|to\s+trade|trade)?", RegexOptions.IgnoreCase);
            if (!m.Success || !int.TryParse(m.Groups[1].Value, out int n) || n is < 1 or > 32) continue;
            string kind = m.Groups[2].Value.ToLowerInvariant();
            return kind.StartsWith("no") || kind.Length == 0 ? n : 32 - n;
        }
        return null;
    }

    /// <summary>Two-way by contract type: "Two-Way Contract..." and entry-level deals are; a standard deal isn't, even when
    /// CapWages lists a lower minors salary (the shipped data agrees: 366 of 368 entry-level deals are two-way, no standard ones).</summary>
    private static bool TwoWay(CapContract c) =>
        c.Type.Contains("Two-Way", StringComparison.OrdinalIgnoreCase) || c.Type.Contains("Entry-Level", StringComparison.OrdinalIgnoreCase);

    private static int Units(long dollars) => (int)Math.Round(dollars / (double)DollarUnit, MidpointRounding.AwayFromZero);

    /// <summary>The retention entries of a season that belong to other teams (CapWages lists the player's own share as
    /// "*" or under his own team's name).</summary>
    private static IEnumerable<(string Abbr, long CapHit)> RetainedBy(CapSeason s, string ownAbbr) =>
        s.Retention.Where(r => r.Team != "*").Select(r => (Abbr: NhlApi.TeamAbbr(r.Team), r.CapHit)).Where(r => r.Abbr != ownAbbr);

    public static List<ContractChange> Compare(T3Db db, IReadOnlyList<CapTeam> teams)
    {
        int now = CapWagesApi.CurrentSeason(teams);
        var roster = new Roster(db);
        var matcher = new PlayerMatcher(db, roster);
        var bio = db.Table(Roster.BioTable);
        var bf = bio.Fields.ToDictionary(x => x.Name);
        int Get(int row, string f) => bf.TryGetValue(f, out var x) ? (int)db.Get(bio, row, x) : 0;

        var changes = new List<ContractChange>();
        var seen = new HashSet<int>();
        foreach (var team in teams)
            foreach (var cp in team.Players)
            {
                var (p, nameOnly) = matcher.Find(cp.First, cp.Last, cp.Born);
                // Players missing from the game are the roster sync's Not-in-game rows; a player listed twice keeps his first entry.
                if (p == null || !seen.Add(p.GameId)) continue;
                var target = Target(cp, team.Abbr, now);
                if (target == null) continue;
                var old = new GameContract(
                    Get(p.BioRow, "contractstatus"), (long)Get(p.BioRow, "contractdollars") * DollarUnit, Get(p.BioRow, "contractlength"),
                    Get(p.BioRow, "contractis2way") == 1, Get(p.BioRow, "extensionlength"), (long)Get(p.BioRow, "extensiondollars") * DollarUnit,
                    Get(p.BioRow, "extensionis2way") == 1,
                    Enumerable.Range(0, ClauseSlots).Select(i => Get(p.BioRow, $"contractclause{i}")).ToArray(),
                    Enumerable.Range(0, ClauseSlots).Select(i => Get(p.BioRow, $"contractclausemntcnumteams{i}")).ToArray(),
                    Get(p.BioRow, "retainedsalarytrades"));
                var neu = Merge(old, target);
                if (Same(old, neu)) continue;
                changes.Add(new ContractChange
                {
                    Kind = ContractChangeKind.Contract, GameId = p.GameId, BioRow = p.BioRow, Name = p.Name, Position = p.PositionDisplay,
                    TeamAbbr = team.Abbr, Old = old, New = neu, NameOnly = nameOnly,
                    Source = target.Length > 0 ? Describe(cp, now) : "no contract for this season on CapWages",
                    // Name-only matches, and taking a contract away, wait for a tick.
                    Selected = !nameOnly && !(old.Length > 0 && neu.Length == 0),
                });
            }

        changes.AddRange(CompareRetention(db, teams, now));
        return changes.OrderBy(c => c.Kind).ThenBy(c => c.TeamAbbr).ThenBy(c => c.Name).ToList();
    }

    /// <summary>
    /// What to write: CapWages' deal, with the game's own clauses kept wherever the deal behind them is unchanged: all
    /// slots for an unchanged current deal and extension; the current deal's slots when only the extension changed.
    /// A slot whose list size CapWages doesn't give (-1) keeps the game's size if the clause is the same, else 0.
    /// Unsigned (length 0) keeps the game's status: 0 = rights held, -1 = free agent; a signed deal is always status 0.
    /// </summary>
    private static GameContract Merge(GameContract old, GameContract target)
    {
        bool dealSame = old.Dollars == target.Dollars && old.Length == target.Length;
        bool extSame = old.ExtLength == target.ExtLength && old.ExtDollars == target.ExtDollars;
        var clauses = target.Clauses.ToArray();
        var sizes = target.ListSizes.ToArray();
        int keep = dealSame && extSame ? ClauseSlots : dealSame ? Math.Min(old.Length, ClauseSlots) : 0;
        for (int i = 0; i < ClauseSlots; i++)
        {
            if (i < keep) { clauses[i] = old.Clauses[i]; sizes[i] = old.ListSizes[i]; }
            else if (sizes[i] < 0) sizes[i] = clauses[i] == old.Clauses[i] ? old.ListSizes[i] : 0;
        }
        return target with { Status = target.Length == 0 ? old.Status : 0, Clauses = clauses, ListSizes = sizes };
    }

    private static bool Same(GameContract a, GameContract b) =>
        a.Status == b.Status && a.Dollars == b.Dollars && a.Length == b.Length && a.TwoWay == b.TwoWay && a.ExtLength == b.ExtLength
        && a.ExtDollars == b.ExtDollars && a.ExtTwoWay == b.ExtTwoWay && a.Clauses.SequenceEqual(b.Clauses)
        && a.ListSizes.SequenceEqual(b.ListSizes) && a.Retained == b.Retained;

    /// <summary>"Two-Way Contract · RFA at end" for the current deal.</summary>
    private static string Describe(CapPlayer cp, int now)
    {
        var cur = cp.Contracts.FirstOrDefault(c => c.Seasons.Any(s => s.StartYear == now));
        return cur == null ? "" : $"{cur.Type}{(cur.Expiry.Length > 0 ? $" · {cur.Expiry} at end" : "")}";
    }

    /// <summary>
    /// What the game should hold for this player: the deal covering the current season and the next deal after it. Null
    /// when this season's cap hit can't be read (CapWages shows text such as "ENTRY-LEVEL SLIDE"), so nothing is guessed.
    /// List sizes of -1 mean "not given": Compare keeps the game's own size for an unchanged clause.
    /// </summary>
    private static GameContract Target(CapPlayer cp, string ownAbbr, int now)
    {
        var cur = cp.Contracts.FirstOrDefault(c => c.Seasons.Any(s => s.StartYear == now));
        // Unsigned: no contract this season. The extension is cleared too: a deal starting later would be "cur" next season.
        if (cur == null)
            return new GameContract(0, 0, 0, false, 0, 0, false, new int[ClauseSlots], new int[ClauseSlots], 0);
        var s0 = cur.Seasons.First(s => s.StartYear == now);
        if (s0.CapHit < 0) return null;
        var left = cur.Seasons.Where(s => s.StartYear >= now).OrderBy(s => s.StartYear).ToList();
        int lastYear = left[^1].StartYear;
        var next = cp.Contracts.Where(c => c.Seasons.Count > 0 && c.Seasons.Min(s => s.StartYear) > lastYear)
                               .OrderBy(c => c.Seasons.Min(s => s.StartYear)).FirstOrDefault();
        var nextSeasons = next?.Seasons.OrderBy(s => s.StartYear).ToList() ?? new List<CapSeason>();

        var retained = RetainedBy(s0, ownAbbr).ToList();
        long share = s0.CapHit - retained.Sum(r => r.CapHit);

        var clauses = new int[ClauseSlots];
        var sizes = new int[ClauseSlots];
        var slots = left.Select(s => (Contract: cur, Season: s)).Concat(nextSeasons.Select(s => (Contract: next, Season: s))).Take(ClauseSlots).ToList();
        for (int i = 0; i < slots.Count; i++)
        {
            var (c, s) = slots[i];
            clauses[i] = ClauseValue(s.Clause);
            // Full NTC/NMC slots keep the game's size (-1): the shipped data stores 0 or an old list there.
            sizes[i] = clauses[i] is 1 or 2 ? s.ListSize ?? ListSize(c.ClauseDetails, cp.TermsDetails) ?? -1 : clauses[i] is 3 or 4 ? -1 : 0;
        }
        var ns0 = nextSeasons.FirstOrDefault();
        return new GameContract(0, (long)Units(share) * DollarUnit, left.Count, TwoWay(cur),
                                nextSeasons.Count, ns0 == null || ns0.CapHit < 0 ? 0 : (long)Units(ns0.CapHit) * DollarUnit,
                                ns0 != null && TwoWay(next), clauses, sizes, retained.Count);
    }

    /// <summary>
    /// Every team's retained-salary totals rebuilt from all of CapWages' contracts: for each season from now, the
    /// dollars each former team still carries for players it traded away, and how many players. Built from the players'
    /// own contracts (not the dead-cap lists), so a player retained by two teams counts once for each. Teams whose
    /// 25 yearly slots already match get no row.
    /// </summary>
    private static IEnumerable<ContractChange> CompareRetention(T3Db db, IReadOnlyList<CapTeam> teams, int now)
    {
        var t = db.Table(SalaryExtrasTable);
        if (t == null) yield break;
        var f = t.Fields.ToDictionary(x => x.Name);
        if (!f.ContainsKey("index") || !f.ContainsKey("retainedtradesalaryyear0")) yield break;
        var index = RosterSync.TeamIndex(db);
        var totals = new Dictionary<int, (long[] Dollars, int[] Count)>();
        var seen = new HashSet<string>();
        foreach (var team in teams)
            foreach (var cp in team.Players)
            {
                // One contract set per player: a player listed by two teams is counted once.
                if (!seen.Add($"{cp.Name}|{cp.Born:yyyy-MM-dd}")) continue;
                foreach (var s in cp.Contracts.SelectMany(c => c.Seasons).Where(s => s.StartYear >= now && s.StartYear < now + RetentionYears))
                    foreach (var (abbr, hit) in RetainedBy(s, team.Abbr))
                    {
                        if (!index.TryGetValue(abbr, out int ti)) continue;
                        if (!totals.TryGetValue(ti, out var tt)) totals[ti] = tt = (new long[RetentionYears], new int[RetentionYears]);
                        tt.Dollars[s.StartYear - now] += hit;
                        tt.Count[s.StartYear - now]++;
                    }
            }
        var names = new Roster(db).Teams;
        for (int r = 0; r < t.WrittenRecords; r++)
        {
            if (db.IsInvalid(t, r)) continue;
            int ti = (int)db.Get(t, r, f["index"]);
            if (!index.ContainsValue(ti)) continue;
            var oldD = Enumerable.Range(0, RetentionYears).Select(y => (long)(int)db.Get(t, r, f[$"retainedtradesalaryyear{y}"])).ToArray();
            var oldC = Enumerable.Range(0, RetentionYears).Select(y => (int)db.Get(t, r, f[$"numretainedsalariesyear{y}"])).ToArray();
            var (newD, newC) = totals.TryGetValue(ti, out var nt) ? nt : (new long[RetentionYears], new int[RetentionYears]);
            if (oldD.SequenceEqual(newD) && oldC.SequenceEqual(newC)) continue;
            string abbr = names.TryGetValue(ti, out var tm) ? tm.Abbr : $"team {ti}";
            yield return new ContractChange
            {
                Kind = ContractChangeKind.TeamRetention, Team = ti, TeamAbbr = abbr, Name = names.TryGetValue(ti, out var tn) ? tn.Name : abbr,
                OldTotals = oldD, NewTotals = newD, OldCounts = oldC, NewCounts = newC, Selected = true,
                Source = "rebuilt from every retained contract on CapWages",
            };
        }
    }

    /// <summary>Writes the ticked changes: a player's contract fields, or a team's 25 yearly retention slots. Values out of a
    /// field's range are skipped, never wrapped.</summary>
    public static string Apply(T3Db db, IEnumerable<ContractChange> changes)
    {
        using var batch = db.Batch();
        var bio = db.Table(Roster.BioTable);
        var bf = bio.Fields.ToDictionary(x => x.Name);
        var st = db.Table(SalaryExtrasTable);
        var sf = st?.Fields.ToDictionary(x => x.Name);
        void SetIn(T3Table t, int row, Dictionary<string, T3Field> fields, string name, long v)
        {
            if (!fields.TryGetValue(name, out var f)) return;
            long max = f.RangeLow + (1L << f.Depth) - 1;
            if (v >= f.RangeLow && v <= max) db.Set(t, row, f, (int)v);
        }
        int players = 0, teams = 0;
        foreach (var c in changes.Where(c => c.CanApply))
        {
            if (c.Kind == ContractChangeKind.TeamRetention)
            {
                if (st == null) continue;
                int row = -1;
                for (int r = 0; r < st.WrittenRecords && row < 0; r++)
                    if (!db.IsInvalid(st, r) && (int)db.Get(st, r, sf["index"]) == c.Team) row = r;
                if (row < 0) continue;
                for (int y = 0; y < RetentionYears; y++)
                {
                    SetIn(st, row, sf, $"retainedtradesalaryyear{y}", c.NewTotals[y]);
                    SetIn(st, row, sf, $"numretainedsalariesyear{y}", c.NewCounts[y]);
                }
                teams++;
                continue;
            }
            var n = c.New;
            SetIn(bio, c.BioRow, bf, "contractstatus", n.Status);
            SetIn(bio, c.BioRow, bf, "contractdollars", n.Dollars / DollarUnit);
            SetIn(bio, c.BioRow, bf, "contractlength", n.Length);
            SetIn(bio, c.BioRow, bf, "contractis2way", n.TwoWay ? 1 : 0);
            SetIn(bio, c.BioRow, bf, "extensionlength", n.ExtLength);
            SetIn(bio, c.BioRow, bf, "extensiondollars", n.ExtDollars / DollarUnit);
            SetIn(bio, c.BioRow, bf, "extensionis2way", n.ExtTwoWay ? 1 : 0);
            for (int i = 0; i < ClauseSlots; i++)
            {
                SetIn(bio, c.BioRow, bf, $"contractclause{i}", n.Clauses[i]);
                SetIn(bio, c.BioRow, bf, $"contractclausemntcnumteams{i}", n.ListSizes[i]);
            }
            SetIn(bio, c.BioRow, bf, "retainedsalarytrades", n.Retained);
            players++;
        }
        var parts = new List<string>();
        if (players > 0) parts.Add($"{players} contract{(players == 1 ? "" : "s")} updated");
        if (teams > 0) parts.Add($"retained salary rebuilt for {teams} team{(teams == 1 ? "" : "s")}");
        return "Contracts: " + (parts.Count == 0 ? "nothing written" : string.Join(", ", parts));
    }
}
