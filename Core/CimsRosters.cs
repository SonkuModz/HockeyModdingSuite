namespace NHL27Kit.Core;

/// <summary>
/// Takes the rosters out of a newer Center Ice Mod (its nhl_game.db / initial_experience.db, read with tools/cims-view)
/// into the game's own databases, and nothing else: the player tables (bios, ratings, equipment, players, both roster
/// tables, free agents, resort extras, goalie gear, career stats) become the package's rows, while the players of a
/// custom league (ids <see cref="CustomLeague.FirstPlayerId"/>-<see cref="CustomLeague.LastPlayerId"/>) and their roster
/// rows stay as they are. Teams, jerseys, arenas, leagues and strings aren't touched. Records are copied whole (the two
/// files must lay the table out alike), and a table the package keeps sorted stays sorted.
/// </summary>
public static class CimsRosters
{
    public sealed record TableSpec(string Table, string Id, string Sort);

    public static readonly TableSpec[] Tables =
    {
        new("exhibitionplayerbiotable", "game_id", "game_id"),
        new("exhibitionskaterai", "game_id", "game_id"),
        new("exhibitiongoalieai", "game_id", "game_id"),
        new("exhibitionskaterequipment", "game_id", "game_id"),
        new("exhibitiongoalieequipment", "game_id", "game_id"),
        new("exhibitionplayers", "index", "index"),
        new("exhibitionrostertable", "playerindex", "key"),
        new("customonlinerostertable", "playerindex", "key"),
        new("exhibitionfreeagents", "playerindex", "playerindex"),
        new("exhibitionresortskaterextras", "game_id", "index"),
        new("exhibitionresortgoalieextras", "game_id", "index"),
        new("stockgoalieequipment", "game_id", "key"),
        new("stockcareermiscstats", "game_id", null),
        new("stockcareerseasonplayerstats", "game_id", null),
        new("stockcareerseasongoaliestats", "game_id", null),
        new("stockcareerplayoffplayerstats", "game_id", null),
        new("stockcareerplayoffgoaliestats", "game_id", null),
    };

    public static bool IsLeaguePlayer(int id) => id >= CustomLeague.FirstPlayerId && id <= CustomLeague.LastPlayerId;

    /// <summary>Puts <paramref name="package"/>'s rosters into <paramref name="db"/> (in memory). Returns a line per table.</summary>
    public static List<string> Merge(T3Db db, T3Db package)
    {
        var log = new List<string>();
        string what = db.Source?.Name ?? "database";
        // the custom league's teams: every team a kept player is rostered on
        var leagueTeams = new HashSet<int>();
        foreach (var rt in new[] { "exhibitionrostertable", "customonlinerostertable" })
            if (db.Table(rt) is { } t)
            {
                var pi = Field(t, "playerindex"); var team = Field(t, "team");
                foreach (int r in Rows(db, t))
                    if (IsLeaguePlayer((int)db.Get(t, r, pi))) leagueTeams.Add((int)db.Get(t, r, team));
            }
        // check everything before changing anything
        var plans = new List<(T3Table T, List<byte[]> Rows, string Line)>();
        foreach (var s in Tables)
        {
            var t = db.Table(s.Table);
            var pt = package.Table(s.Table);
            if (t == null && pt == null) continue;
            if (t == null || pt == null) throw new InvalidDataException($"{what}: {s.Table} is in only one of the two files.");
            if (t.RecordSize != pt.RecordSize || !t.Fields.Select(f => (f.Name, f.BitOffset, f.Depth, f.Type)).SequenceEqual(pt.Fields.Select(f => (f.Name, f.BitOffset, f.Depth, f.Type))))
                throw new InvalidDataException($"{what}: {s.Table} is laid out differently in the package.");
            var id = Field(t, s.Id); var pid = Field(pt, s.Id);
            var team = t.Fields.FirstOrDefault(f => f.Name == "team" && s.Table.EndsWith("rostertable"));
            var kept = Rows(db, t).Where(r => IsLeaguePlayer((int)db.Get(t, r, id))).Select(r => db.RecordBytes(t, r)).ToList();
            int clash = 0;
            var rows = new List<byte[]>();
            foreach (int r in Rows(package, pt))
            {
                if (IsLeaguePlayer((int)package.Get(pt, r, pid)) || team != null && leagueTeams.Contains((int)package.Get(pt, r, team))) { clash++; continue; }
                rows.Add(package.RecordBytes(pt, r));
            }
            int fromPackage = rows.Count;
            rows.AddRange(kept);
            if (rows.Count > t.Records) throw new InvalidDataException($"{what}: {s.Table} would need {rows.Count:N0} rows, it has room for {t.Records:N0}.");
            if (s.Sort != null && Sorted(package, pt, Field(pt, s.Sort)))
            {
                var sf = Field(t, s.Sort);
                rows = rows.Select((b, i) => (b, i, k: ReadInt(b, sf))).OrderBy(x => x.k).ThenBy(x => x.i).Select(x => x.b).ToList();
            }
            int before = Rows(db, t).Count();
            plans.Add((t, rows, $"{what}: {s.Table}: {before:N0} rows -> {rows.Count:N0} ({fromPackage:N0} from the package, {kept.Count:N0} league rows kept{(clash > 0 ? $", {clash} package rows on league ids/teams left out" : "")})"));
        }
        using (db.Batch())
            foreach (var (t, rows, line) in plans)
            {
                db.SetWritten(t, rows.Count);
                for (int i = 0; i < rows.Count; i++) db.WriteRecord(t, i, rows[i]);
                log.Add(line);
            }
        return log;
    }

    private static T3Field Field(T3Table t, string name) => t.Fields.FirstOrDefault(f => f.Name == name) ?? throw new InvalidDataException($"{t.Name} has no {name} field.");

    private static IEnumerable<int> Rows(T3Db db, T3Table t)
    {
        for (int r = 0; r < t.WrittenRecords; r++)
            if (!db.IsInvalid(t, r)) yield return r;
    }

    private static bool Sorted(T3Db db, T3Table t, T3Field f)
    {
        long prev = long.MinValue;
        foreach (int r in Rows(db, t)) { long v = (int)db.Get(t, r, f); if (v < prev) return false; prev = v; }
        return true;
    }

    // an integer field from a record's bytes (as T3Db reads it: little-endian bits from the field's offset, plus its low end)
    private static long ReadInt(byte[] rec, T3Field f)
    {
        long v = 0;
        for (int i = 0; i < f.Depth; i++)
        {
            int bit = f.BitOffset + i;
            if ((rec[bit >> 3] >> (bit & 7) & 1) != 0) v |= 1L << i;
        }
        return v + f.RangeLow;
    }
}
