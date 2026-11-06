namespace NHL27Kit.Core;

/// <summary>
/// Finds a real player among the game's players. The game has no NHL ids, so every sync source (NHL API, AHL feed,
/// CapWages) is matched the same way, here: (1) normalised full name + birth date; (2) last name + birth date, for
/// players a source spells differently (Teddy / Teodors); (3) the only game player with that full name, accepted but
/// flagged NameOnly (his birth date differs or is missing). Every bio player is a candidate, not only players with a
/// roster row, so a bio-only free agent is found too.
/// </summary>
public sealed class PlayerMatcher
{
    private readonly T3Db _db;
    private readonly T3Table _bio;
    private readonly T3Field _year, _month, _day;
    private readonly Dictionary<string, List<RosterPlayer>> _byName = new();
    private readonly Dictionary<string, List<RosterPlayer>> _byLast;

    public PlayerMatcher(T3Db db, Roster roster)
    {
        _db = db;
        _bio = db.Table(Roster.BioTable);
        var bf = _bio.Fields.ToDictionary(x => x.Name);
        (_year, _month, _day) = (bf["year"], bf["month"], bf["day"]);
        foreach (var p in roster.Players)
        {
            string k = Key(p.FirstName, p.LastName);
            if (!_byName.TryGetValue(k, out var l)) _byName[k] = l = new List<RosterPlayer>();
            l.Add(p);
        }
        _byLast = roster.Players.GroupBy(p => RosterSync.Norm(p.LastName)).ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>The game player for this name and birth date, and whether he was matched by name only; (null, false) when none.</summary>
    public (RosterPlayer Player, bool NameOnly) Find(string first, string last, DateTime? born)
    {
        var cands = _byName.GetValueOrDefault(Key(first, last)) ?? new List<RosterPlayer>();
        var p = cands.FirstOrDefault(c => Born(c) == born);
        if (p == null && born != null)
            p = _byLast.GetValueOrDefault(RosterSync.Norm(last))?.FirstOrDefault(c => Born(c) == born);
        if (p != null) return (p, false);
        return cands.Count == 1 ? (cands[0], true) : (null, false);
    }

    /// <summary>The player's birth date as the bio stores it (year - 1900, month - 1, day - 1); null when it isn't a real date.</summary>
    public DateTime? Born(RosterPlayer p)
    {
        int y = (int)_db.Get(_bio, p.BioRow, _year) + 1900, m = (int)_db.Get(_bio, p.BioRow, _month) + 1, d = (int)_db.Get(_bio, p.BioRow, _day) + 1;
        return y > 1900 && m is >= 1 and <= 12 && d >= 1 && d <= DateTime.DaysInMonth(y, m) ? new DateTime(y, m, d) : null;
    }

    /// <summary>The whole name normalised, wherever the first/last split falls: the AHL roster splits on the last space
    /// ("Eskild Bakke" + "Olsen", "Hunter St." + "Martin"), the game and the AHL player page don't ("Eskild" + "Bakke Olsen").</summary>
    private static string Key(string first, string last) => RosterSync.Norm(first) + RosterSync.Norm(last);
}
