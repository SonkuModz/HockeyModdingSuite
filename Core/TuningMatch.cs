namespace NHL27Kit.Core;

/// <summary>
/// Sets NHL 24's gameplay tuning values to NHL 27's: every value both games have (same name and type, set once in
/// each) whose default, minimum or maximum differs, in the gameplay groups (AI, Physics, Animation; not the coaching
/// and on-the-fly feedback messages). The patched eboot is read back before anything is written: each changed value
/// must read as NHL 27's and every other value as it was.
/// </summary>
public static class TuningMatch
{
    public static readonly string[] GameplayGroups = { "AI", "Physics", "Animation" };
    public static readonly string[] SkipGroups = { "AI/CoachingFeedback", "AI/OTFFeedback" };

    public sealed record Result(TunablePatcher.Plan Plan, int Common, int NotGameplay, int Same, int Different, int OtherType, int Unread,
        int Good, List<string> Wrong)
    {
        public string Summary =>
            $"{Plan.Changed.Count:N0} of {Different:N0} gameplay values that differ from NHL 27 set to NHL 27's; {Plan.Skipped.Count:N0} can't be changed safely";
    }

    public static bool Gameplay(Tunable t, IReadOnlyList<string> groups = null, IReadOnlyList<string> skip = null)
    {
        static bool Under(string g, string p) => g != null && (g.Equals(p, StringComparison.OrdinalIgnoreCase) || g.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase));
        return (groups ?? GameplayGroups).Any(p => Under(t.Group, p)) && !(skip ?? SkipGroups).Any(p => Under(t.Group, p));
    }

    /// <summary>Plans the change of <paramref name="eboot"/> (NHL 24's, as it ships) towards <paramref name="theirs"/> (NHL 27's values).</summary>
    public static Result Build(byte[] eboot, IReadOnlyList<Tunable> theirs, IReadOnlyList<string> groups = null, IReadOnlyList<string> skip = null)
    {
        var mine = Tunables.Read(eboot);
        var one = mine.GroupBy(t => t.Name).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First());
        var other = theirs.GroupBy(t => t.Name).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First());
        var changes = new List<TunablePatcher.Change>();
        int common = 0, same = 0, typeDiff = 0, noValue = 0, notGameplay = 0;
        foreach (var (name, t) in one)
        {
            if (!other.TryGetValue(name, out var o)) continue;
            common++;
            if (!Gameplay(t, groups, skip)) { notGameplay++; continue; }
            if (t.Type != o.Type) { typeDiff++; continue; }
            if (t.Default == null || o.Default == null) { noValue++; continue; }
            if (t.Default == o.Default && t.Max == o.Max && t.Min == o.Min) { same++; continue; }
            changes.Add(new TunablePatcher.Change(name, o.Default.Value, o.Min, o.Max));
        }
        var plan = TunablePatcher.Make(eboot, changes);

        // read it back: changed values are NHL 27's, everything else is as it was
        var after = Tunables.Read(plan.Patched);
        var before = mine.ToDictionary(t => (t.Name, t.Site));
        var want = changes.ToDictionary(c => c.Name);
        int good = 0;
        var wrong = new List<string>();
        foreach (var t in after)
        {
            if (!before.TryGetValue((t.Name, t.Site), out var b)) { wrong.Add($"{t.Name}: appeared after patching"); continue; }
            if (plan.Changed.Contains(t.Name))
            {
                var c = want[t.Name];
                bool valueSet = plan.Done.Any(d => d.StartsWith(t.Name + ": value", StringComparison.Ordinal));
                bool okValue = valueSet ? t.Default == c.Value : t.Default == b.Default;
                bool okMax = t.Max == b.Max || t.Max == c.Max;
                if (okValue && okMax) good++;
                else wrong.Add($"{t.Name}: reads {t.Default} (max {t.Max}), wanted {c.Value} (max {c.Max})");
            }
            else if (t.Default != b.Default || t.Max != b.Max || t.Min != b.Min || t.Type != b.Type)
                wrong.Add($"{t.Name}: moved {b.Default}/{b.Min}/{b.Max} -> {t.Default}/{t.Min}/{t.Max}");
        }
        if (after.Count != mine.Count) wrong.Add($"{mine.Count} values before patching, {after.Count} after");
        return new Result(plan, common, notGameplay, same, changes.Count, typeDiff, noValue, good, wrong);
    }

    /// <summary>The mod list's entry for it.</summary>
    public const string ModKind = "tuning", ModName = "eboot.bin";
}
