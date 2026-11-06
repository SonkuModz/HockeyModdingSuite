namespace NHL27Kit.Core;

/// <summary>A player with no play-by-play name call (surnameid 0) whose last name the game has a recording for.</summary>
public sealed class NameCallChange
{
    public int GameId { get; init; } = -1;
    public int BioRow { get; init; } = -1;
    public string Name { get; init; }
    public string Position { get; init; }
    public string TeamAbbr { get; init; }
    public int SurnameId { get; init; }
    /// <summary>The recording's name as the game lists it ("McQueen").</summary>
    public string Call { get; init; }
    public bool Selected { get; set; } = true;
    public bool CanApply => BioRow >= 0 && SurnameId > 0;
    public string Description => $"play-by-play name: none → {Call}";
}

/// <summary>
/// The commentators' name calls. A player's bio surnameid points into the game's own list of recorded names
/// (nhl_game.db playernames: surnameid, name, isfirstname, isfemale, availability); 0 means no name call (the edit screen
/// then shows the list's first entry, "A.J."). Every name call the game's own players use is an available entry, nearly
/// all of them a surname entry for their own last name, so a last name is looked up the same way: an available male
/// surname entry with that name, else a first-name entry (some surnames are only recorded as first names); when a name has
/// more than one recording, the one most players already use.
/// </summary>
public static class NameCalls
{
    public const string Table = "playernames";

    /// <summary>Normalized last name → (surnameid, the name as listed). Empty when the game has no name list.</summary>
    public static Dictionary<string, (int Id, string Name)> ByLastName(T3Db db)
    {
        var map = new Dictionary<string, (int, string)>();
        var t = db.Table(Table);
        if (t == null) return map;
        var f = t.Fields.ToDictionary(x => x.Name);
        if (!f.ContainsKey("surnameid") || !f.ContainsKey("name")) return map;
        int Int(int r, string n) => f.TryGetValue(n, out var x) ? (int)db.Get(t, r, x) : 0;

        // how many players already use each id
        var bio = db.Table(Roster.BioTable);
        var bf = bio.Fields.ToDictionary(x => x.Name);
        var used = new Dictionary<int, int>();
        if (bf.TryGetValue("surnameid", out var sf))
            for (int r = 0; r < bio.WrittenRecords; r++)
                if (!db.IsInvalid(bio, r) && (int)db.Get(bio, r, sf) is > 0 and var id) used[id] = used.GetValueOrDefault(id) + 1;

        var entries = new List<(string Key, int Id, string Name, bool First)>();
        for (int r = 0; r < t.WrittenRecords; r++)
        {
            if (db.IsInvalid(t, r)) continue;
            int id = Int(r, "surnameid");
            if (id <= 0 || Int(r, "isfemale") != 0 || f.ContainsKey("availability") && Int(r, "availability") != 1) continue;
            string name = (string)db.Get(t, r, f["name"]) ?? "";
            string key = RosterSync.Norm(name);
            if (key.Length > 0) entries.Add((key, id, name, Int(r, "isfirstname") == 1));
        }
        foreach (var g in entries.GroupBy(e => e.Key))
        {
            var best = g.OrderBy(e => e.First).ThenByDescending(e => used.GetValueOrDefault(e.Id)).ThenBy(e => e.Id).First();
            map[g.Key] = (best.Id, best.Name);
        }
        return map;
    }

    /// <summary>Players (men, not special ids) with no name call whose last name has a recording.</summary>
    public static List<NameCallChange> Compare(T3Db db)
    {
        var calls = ByLastName(db);
        var changes = new List<NameCallChange>();
        if (calls.Count == 0) return changes;
        var roster = new Roster(db);
        var bio = db.Table(Roster.BioTable);
        var bf = bio.Fields.ToDictionary(x => x.Name);
        if (!bf.ContainsKey("surnameid")) return changes;
        int Int(int r, string n) => bf.TryGetValue(n, out var x) ? (int)db.Get(bio, r, x) : 0;
        foreach (var p in roster.Players)
        {
            if (p.BioRow < 0 || p.GameId >= PlayerCreator.SpecialIdStart || Int(p.BioRow, "surnameid") != 0) continue;
            if (Int(p.BioRow, "gender") != 0 || Int(p.BioRow, "ismascot") != 0) continue;
            if (!calls.TryGetValue(RosterSync.Norm(p.LastName), out var call)) continue;
            changes.Add(new NameCallChange
            {
                GameId = p.GameId, BioRow = p.BioRow, Name = p.Name, Position = p.PositionDisplay, TeamAbbr = p.TeamAbbr,
                SurnameId = call.Id, Call = call.Name,
            });
        }
        return changes.OrderBy(c => c.Name).ToList();
    }

    public static string Apply(T3Db db, IEnumerable<NameCallChange> changes)
    {
        using var batch = db.Batch();
        var bio = db.Table(Roster.BioTable);
        var f = bio.Fields.First(x => x.Name == "surnameid");
        int n = 0;
        foreach (var c in changes.Where(c => c.CanApply)) { db.Set(bio, c.BioRow, f, c.SurnameId); n++; }
        return $"Name calls: {n} player{(n == 1 ? "" : "s")} given their play-by-play name";
    }
}
