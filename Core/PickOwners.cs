namespace NHL27Kit.Core;

/// <summary>Who owns one draft pick in real life, mapped onto the game's draft years.</summary>
public sealed record OwnedPick(int DraftYear, int Round, string OriginalAbbr, string OwnerAbbr)
{
    /// <summary>The game's exhibitiondraftpick year: calendar draft year - 1900 (127 = 2027 draft).</summary>
    public int StoredYear => DraftYear - 1900;
}

/// <summary>
/// Real-life draft pick owners from CapWages' team pages, live at sync time. Each page lists the picks the team holds
/// (its own and acquired ones) and its own picks it traded away; a held pick's owner is the page's team, and its
/// original team is the full name CapWages gives (resolved through NhlApi.TeamAbbr). Traded-away entries are skipped:
/// the team that holds the pick lists it. Conditions are treated as non-existent: the game can't store them, so a
/// conditional pick takes the owner and round CapWages shows.
///
/// Real years map onto the game's: offset = the game's first stored draft year (the completed draft: 2026 in NHL 27,
/// 2023 in NHL 24) − the real completed draft (CapWagesApi.CurrentSeason). So real 2027 picks land in NHL 24's 2024 slots.
/// </summary>
public static class PickOwners
{
    public sealed record Result(List<OwnedPick> Picks, int GameFirstYear, int Offset, int RealSeason, int Conflicts);

    public static Result FromCapWages(IReadOnlyList<CapTeam> teams, T3Db db)
    {
        int realSeason = CapWagesApi.CurrentSeason(teams);
        int firstStored = DraftPicks.FirstStoredYear(db);
        int gameFirstYear = firstStored < 0 ? -1 : firstStored + 1900;
        int offset = gameFirstYear < 0 ? 0 : gameFirstYear - realSeason;
        var byKey = new Dictionary<(int, int, string), OwnedPick>();
        int conflicts = 0;
        foreach (var team in teams)
            foreach (var p in team.Picks.Where(p => !p.TradedAway))
            {
                var pick = new OwnedPick(p.Year + offset, p.Round, NhlApi.TeamAbbr(p.OriginalTeam), team.Abbr);
                // Two teams both listing the same pick as theirs would be a CapWages error: the first stands, and it's counted.
                if (!byKey.TryAdd((pick.StoredYear, pick.Round, pick.OriginalAbbr), pick) && byKey[(pick.StoredYear, pick.Round, pick.OriginalAbbr)].OwnerAbbr != pick.OwnerAbbr)
                    conflicts++;
            }
        return new Result(byKey.Values.ToList(), gameFirstYear, offset, realSeason, conflicts);
    }
}
