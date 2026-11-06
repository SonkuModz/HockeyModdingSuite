namespace NHL27Kit.Core;

/// <summary>
/// Retained salary (stockteamsalaryextras), read-only. One row per team (index = team index), 25 yearly slots
/// of the dollars the team still carries for players it traded away (retainedtradesalaryyearN) and how many
/// retained players those dollars cover (numretainedsalariesyearN). The bio row only says a player is retained
/// (retainedsalarytrades) and what his current team pays (contractdollars); which team retains him, and how
/// much, is worked out here from the teams' yearly totals — and shown only when the data pins it down to
/// exactly one team. Nothing here is ever written back.
/// </summary>
public static class RetainedSalary
{
    public const string Table = "stockteamsalaryextras";
    public const int Years = 25;

    /// <summary>What the game data pins down about one retained player's retention; null means not determined.</summary>
    public sealed record Attribution(int? RetainingTeam, long? RetainedDollars, long? FullDollars, double? Percent);

    /// <summary>
    /// Attribute one retained player's retention from the game data. A player with L years left has his retained
    /// dollars in years 0..L-1 of his retaining team's totals, so the players of exactly length L are the drop
    /// from year L-1 to year L (count_L and total_L, with year 25 = 0). The retaining team is determined only
    /// when exactly one team other than the player's current team has such a slot; the dollars and % only when
    /// that team has exactly one player in the slot. A flag above 1 (retained by two teams) is out of scope:
    /// nothing is determined.
    /// </summary>
    public static Attribution Attribute(T3Db db, int retainedTrades, int contractLength, long currentHitDollars, int currentTeam)
    {
        var none = new Attribution(null, null, null, null);
        if (retainedTrades != 1 || contractLength < 1 || contractLength > Years) return none;
        var t = db.Table(Table);
        if (t == null || Roster.Field(t, "index") == null) return none;
        int l = contractLength;
        var slots = new Dictionary<int, (int Count, long Total)>();
        foreach (int r in Rows(db, t))
        {
            int idx = Int(db, t, r, "index");
            if (idx < 0 || idx == currentTeam) continue;
            int count = Count(db, t, r, l - 1) - Count(db, t, r, l);
            long total = Total(db, t, r, l - 1) - Total(db, t, r, l);
            if (slots.TryGetValue(idx, out var s)) slots[idx] = (s.Count + count, s.Total + total);
            else slots[idx] = (count, total);
        }
        var candidates = slots.Where(kv => kv.Value.Count >= 1).ToList();
        if (candidates.Count != 1) return none;
        int team = candidates[0].Key;
        var slot = candidates[0].Value;
        if (slot.Count != 1 || slot.Total <= 0) return new Attribution(team, null, null, null);
        long retained = slot.Total;
        long full = currentHitDollars + retained;
        return new Attribution(team, retained, full, full > 0 ? retained * 100.0 / full : 0);
    }

    private static int Count(T3Db db, T3Table t, int r, int year) => year >= Years ? 0 : Int(db, t, r, $"numretainedsalariesyear{year}");
    private static long Total(T3Db db, T3Table t, int r, int year) => year >= Years ? 0 : Int(db, t, r, $"retainedtradesalaryyear{year}");

    private static IEnumerable<int> Rows(T3Db db, T3Table t)
    {
        for (int r = 0; r < t.WrittenRecords; r++)
            if (!db.IsInvalid(t, r)) yield return r;
    }

    private static int Int(T3Db db, T3Table t, int r, string f) => Roster.Field(t, f) is { } x ? (int)db.Get(t, r, x) : 0;
}
