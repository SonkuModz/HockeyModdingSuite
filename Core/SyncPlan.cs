namespace NHL27Kit.Core;

/// <summary>
/// The player part of a roster sync, written the same way by Apply and by the preview: the ticked roster changes
/// (trades, call-ups, info, left) re-compared fresh against the database, then the ticked missing players created.
/// The preview runs it on a copy of the database read from the game, so the contracts and lines it lists are compared
/// with the rosters as Apply will leave them (a new player is on his team, a traded player on his new one), not as
/// they are in the game now.
/// </summary>
public static class SyncPlan
{
    /// <summary>What the player part wrote: its summaries (for the mod list) and the new players' game ids.</summary>
    public sealed record PlayerResult(List<string> Summaries, HashSet<int> Created);

    /// <summary>A Missing player's source and id ("NHL:8485414", "AHL:11419"): every Missing change has game id -1, so this is what tells them apart.</summary>
    public static string BioKey(SyncChange c) => c.Api != null ? $"NHL:{c.Api.Id}" : c.Ahl != null ? $"AHL:{c.Ahl.Id}" : null;

    /// <summary>
    /// Applies the ticked roster changes (<paramref name="picked"/>: game id, kind, to-team, AHL) and creates the ticked
    /// missing players (<paramref name="toCreate"/>: bio key → bio) in <paramref name="db"/>. <paramref name="cap"/> is
    /// given when contracts are synced too (a new player's NHL club comes from his CapWages deal), else null.
    /// </summary>
    public static PlayerResult ApplyPlayers(T3Db db, List<NhlPlayer> api, List<(AhlTeam Team, List<AhlPlayer> Players)> ahl, bool sizes,
        ISet<(int GameId, SyncKind Kind, int ToTeam, bool IsAhl)> picked, IReadOnlyDictionary<string, PlayerBio> toCreate, IReadOnlyList<CapTeam> cap)
    {
        var summaries = new List<string>();
        var created = new HashSet<int>();
        if (api == null) return new PlayerResult(summaries, created);

        // Re-compare fresh with the same combined compare the list uses (AHL first, NHL second, one shared matched
        // set — see CompareCombined), so the list can't hold two changes for the same player that overwrite each
        // other. Safety net: if a duplicate somehow remains, keep one change per player, preferring the AHL one.
        List<SyncChange> fresh = ahl != null ? RosterSync.CompareCombined(db, api, ahl, sizes).Changes : RosterSync.Compare(db, api, sizes);
        var missing = fresh.Where(c => c.Kind == SyncKind.Missing && BioKey(c) is { } k && toCreate.ContainsKey(k)).ToList();
        foreach (var c in missing) c.Bio = toCreate[BioKey(c)];
        fresh = fresh.Where(c => c.Kind != SyncKind.Missing && picked.Contains((c.GameId, c.Kind, c.ToTeam, c.IsAhl))).ToList();
        var deduped = new List<SyncChange>(fresh.Count);
        var byGame = new Dictionary<int, int>(); // game id → index in deduped (game id -1 changes are never deduped)
        foreach (var c in fresh)
        {
            if (c.GameId < 0) { deduped.Add(c); continue; }
            if (!byGame.TryGetValue(c.GameId, out int at)) { byGame[c.GameId] = deduped.Count; deduped.Add(c); }
            else if (c.IsAhl && !deduped[at].IsAhl) deduped[at] = c;
        }
        if (deduped.Count > 0) summaries.Add(RosterSync.Apply(db, deduped));

        // New players after the moves, so a team's 40-player limit counts the moves.
        if (missing.Count > 0)
        {
            var made = PlayerCreator.Create(db, missing, cap);
            created = made.CreatedIds;
            if (made.Summary.Length > 0) summaries.Add(made.Summary);
            if (made.Skipped.Count > 0)
                summaries.Add("not created: " + string.Join(", ", made.Skipped.Take(5).Select(s => $"{s.Name} ({s.Reason})")) + (made.Skipped.Count > 5 ? ", …" : ""));
        }
        return new PlayerResult(summaries, created);
    }
}
