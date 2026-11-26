namespace NHL27Kit.Core;

/// <summary>One check of an installed league: what was looked at and whether it is right.</summary>
public sealed record LeagueCheck(bool Ok, string What);

/// <summary>What a league install would do, or did.</summary>
public sealed class LeagueResult
{
    public List<LeagueTeamPlan> Teams { get; init; } = new();
    public List<string> Log { get; } = new();
    public List<string> TakenOverKeys { get; set; } = new();
    public ModEntry Entry { get; set; }
    public string Summary { get; set; }
    /// <summary>The edited databases (in memory; written only when the install writes).</summary>
    public T3Db Game { get; set; }
    public T3Db Initial { get; set; }
    public T3Db Loc { get; set; }
}

/// <summary>
/// Puts a custom league into NHL 24 as one mod entry (kind "league"): nhl_game.db, initial_experience.db and
/// loc/eng_us.db share the legacy index chunk, so they're written together and Restore takes all three back out.
/// Applying again restores the earlier entry first and builds from the stock files.
/// </summary>
public static class LeagueInstaller
{
    public static string EntryName(LeagueOptions o) => $"{o.LeagueName} league";

    /// <summary>Mod entries that write the same files: they'd fight over the legacy index chunk.</summary>
    public static List<ModEntry> Conflicts(ModStore store, string game) =>
        store.Entries.Where(e => e.Kind is DbEditor.Kind or LegacyEditor.Kind &&
                                 (string.Equals(e.Name, CustomLeague.GameDb, StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(e.Name, CustomLeague.InitialDb, StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(e.Name, LocDb.English, StringComparison.OrdinalIgnoreCase)) &&
                                 store.State(e, game) is ModState.Applied or ModState.Partial).ToList();

    /// <summary>
    /// Builds the league from the files as the game has them now (without writing) and, when <paramref name="write"/>,
    /// writes it into the game through <paramref name="store"/>.
    /// </summary>
    public static LeagueResult Install(AssetIndex index, ModStore store, string game, LeaguePack pack, string packPath, LeagueOptions o, bool write, Action<string> log = null)
    {
        if (write && Emulators.Running() is { } running) throw new InvalidOperationException($"{running} is running: close it before writing into the game.");
        var conflicts = Conflicts(store, game);
        if (conflicts.Count > 0)
            throw new InvalidOperationException("Restore these first, they write the same files: " + string.Join(", ", conflicts.Select(c => $"{c.Kind} {c.Name}")));
        var existing = store.Find(CustomLeague.Kind, EntryName(o));
        bool restored = false;
        if (existing != null && store.State(existing, game) is ModState.Applied or ModState.Partial)
        {
            if (!write) throw new InvalidOperationException($"\"{existing.Name}\" is in the game: restore it to plan from the stock files (or apply again, which restores it first).");
            log?.Invoke($"restoring the earlier \"{existing.Name}\" first");
            store.Restore(existing, game, log);
            RefreshTocs(index);
            restored = true;
        }
        try
        {
            return Build(index, store, game, pack, packPath, o, write, log);
        }
        catch when (restored && store.Find(CustomLeague.Kind, EntryName(o)) == existing)
        {
            // nothing new went in: the league that was there goes back
            log?.Invoke($"putting the earlier \"{existing.Name}\" back");
            store.Apply(existing, game, log);
            RefreshTocs(index);
            throw;
        }
    }

    private static void RefreshTocs(AssetIndex index)
    {
        var tocs = new TocCache(index);
        foreach (var toc in index.Layout.AllTocPaths()) index.Refresh(tocs.Get(toc));
    }

    private static LeagueResult Build(AssetIndex index, ModStore store, string game, LeaguePack pack, string packPath, LeagueOptions o, bool write, Action<string> log)
    {
        var legacy = new Legacy(index);
        if (!legacy.Ps4) throw new NotSupportedException("Custom leagues are for NHL 24 (PS4).");
        var (gameDb, initial, loc) = CustomLeague.Open(legacy);
        var plans = CustomLeague.Plan(gameDb, initial, pack, o);
        var result = new LeagueResult { Teams = plans };
        var badPngs = LeagueLogos.CheckPngs(pack.Dir, plans);
        if (badPngs.Count > 0) throw new InvalidDataException("These logos can't be read: " + string.Join("; ", badPngs));
        result.Log.Add($"{plans.Count(p => p.ArtId >= 0)} logo PNGs read");
        var used = CustomLeague.UsedIds(gameDb);
        used.UnionWith(CustomLeague.UsedIds(initial));
        var surnames = CustomLeague.Surnames(gameDb);
        // the same template player for each new player in both databases
        var picks = new Dictionary<(int Slot, int N), int>();
        string a = CustomLeague.Build(gameDb, plans, o, new HashSet<int>(used), surnames, picks, result.Log);
        CustomLeague.Build(initial, plans, o, new HashSet<int>(used), surnames, picks, result.Log, source: gameDb);
        var texts = CustomLeague.Texts(gameDb, plans, o);
        var others = CustomLeague.OpenOthers(legacy, result.Log);
        var spare = CustomLeague.SpareKeys(loc, new[] { gameDb, initial }.Concat(others));
        var (locBytes, addedKeys, taken) = LocDb.Set(loc, texts, spare);
        result.TakenOverKeys = taken;
        result.Game = gameDb;
        result.Initial = initial;
        result.Loc = new T3Db(locBytes, System.Text.Encoding.UTF8.GetString(legacy.Read(LocDb.English[..^3] + "-meta.xml")).TrimStart('﻿')) { Source = loc.Source };
        result.Log.Add($"{LocDb.English}: {texts.Count} strings set: {addedKeys.Count} added as new rows, {taken.Count} in rows taken over from team keys no team uses");
        string confs = string.Join(", ", plans.Select(p => p.Conference.Name).Distinct());
        result.Summary = $"{o.LeagueName} (league {o.LeagueId}): {confs}; {a}";
        foreach (var (db, what) in new[] { (gameDb, "nhl_game.db"), (initial, "initial_experience.db") })
        {
            var (ok, total) = db.CheckCrcs();
            if (ok != total) throw new InvalidDataException($"{what}: {total - ok} CRCs don't match after the edit; not writing.");
        }
        if (!write) return result;

        var notes = new List<string>();
        var files = new List<(LegacyFile, byte[])> { (gameDb.Source, gameDb.Data), (initial.Source, initial.Data), (loc.Source, locBytes) };
        var patches = LegacyEditor.PlanPs4(index, legacy, files, notes, log);
        result.Log.AddRange(notes);
        // the logos: loose GNFs in NHL24 PC's user folder, added (and deleted on restore) by the same entry
        if (plans.Any(p => p.ArtId >= 0))
        {
            string user = Path.GetFullPath(Path.Combine(game, "..", "user"));
            if (!Directory.Exists(user)) result.Log.Add($"No NHL24 PC user folder at {user}: logos left out.");
            else
            {
                var logos = LeagueLogos.Build(index, pack.Dir, plans, log);
                foreach (var root in LeagueLogos.TempData)
                    foreach (var (style, art, data) in logos)
                        patches.Add(new PatchData(LeagueLogos.FileFor(root, style, art), 0, Array.Empty<byte>(), data, "logo", NewFile: true, KeepDir: root));
                result.Log.Add($"{logos.Count} logo files ({plans.Count(p => p.ArtId >= 0)} teams x {LeagueLogos.Styles.Length} styles), {logos.Sum(l => (long)l.Data.Length) / 1048576.0:F0} MB, in each of {string.Join(" and ", LeagueLogos.TempData.Select(LeagueLogos.Folder))}");
            }
        }
        var entry = store.Add(CustomLeague.Kind, EntryName(o), packPath, result.Summary, patches, notes.Where(n => n.Contains("grew")));
        store.Apply(entry, game, log);
        result.Entry = entry;
        return result;
    }

    /// <summary>The plans with the art ids their team rows have in <paramref name="game"/> (a league's own logos: 2901 and up).</summary>
    public static List<LeagueTeamPlan> WithArtIds(T3Db game, IEnumerable<LeagueTeamPlan> plans)
    {
        var t = game.Table(Roster.TeamsTable);
        var f = t.Fields.ToDictionary(x => x.Name);
        var art = Enumerable.Range(0, t.WrittenRecords).Where(r => !game.IsInvalid(t, r))
            .ToDictionary(r => (int)game.Get(t, r, f["index"]), r => (int)game.Get(t, r, f["artid"]));
        return plans.Select(p => art.TryGetValue(p.Slot, out int a) && a >= LeagueLogos.FirstArtId && a < 4000 ? p with { ArtId = a } : p).ToList();
    }

    /// <summary>Reads the three files from the game again and checks the league in them.</summary>
    public static List<LeagueCheck> Verify(AssetIndex index, IReadOnlyList<LeagueTeamPlan> plans, LeagueOptions o, string gameDir = null)
    {
        var (game, initial, loc) = CustomLeague.Open(new Legacy(index));
        var checks = Verify(game, initial, loc, plans, o);
        if (gameDir != null)
        {
            // every style's logo file of each team with its own art id
            var withLogo = plans.Where(p => p.ArtId >= 0).ToList();
            foreach (var root in LeagueLogos.TempData)
            {
                int have = 0, want = withLogo.Count * LeagueLogos.Styles.Length;
                foreach (var p in withLogo)
                    foreach (var st in LeagueLogos.Styles)
                    {
                        string f = Path.Combine(gameDir, LeagueLogos.FileFor(root, st.Style, p.ArtId).Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(f) && new FileInfo(f).Length > 0x100) have++;
                    }
                checks.Add(new LeagueCheck(have == want, $"logo files: {have}/{want} ({withLogo.Count} teams) in {LeagueLogos.Folder(root)}"));
            }
        }
        return checks;
    }

    /// <summary>The checks, on databases already read (the game's, or an install's before it writes).</summary>
    public static List<LeagueCheck> Verify(T3Db game, T3Db initial, T3Db loc, IReadOnlyList<LeagueTeamPlan> plans, LeagueOptions o)
    {
        var checks = new List<LeagueCheck>();
        void C(bool ok, string what) => checks.Add(new LeagueCheck(ok, what));
        foreach (var (db, name) in new[] { (game, "nhl_game.db"), (initial, "initial_experience.db"), (loc, "eng_us.db") })
        {
            var (ok, total) = db.CheckCrcs();
            C(ok == total, $"{name}: CRCs {ok}/{total}");
        }
        var strings = LocDb.ReadAll(loc).GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.First().Text);
        C(LocDb.ReadAll(loc).Zip(LocDb.ReadAll(loc).Skip(1)).All(x => x.First.Hash <= x.Second.Hash), "eng_us.db: strings sorted by hash");
        foreach (var (k, v) in CustomLeague.Texts(game, plans, o))
            if (!strings.TryGetValue(k, out var got) || got != v) C(false, $"loc {k} is \"{got}\", should be \"{v}\"");
        C(true, $"eng_us.db: {CustomLeague.Texts(game, plans, o).Count} league strings checked");

        foreach (var (db, name) in new[] { (game, "nhl_game.db"), (initial, "initial_experience.db") })
        {
            var teams = db.Table(Roster.TeamsTable);
            var tf = teams.Fields.ToDictionary(x => x.Name);
            int I(T3Table t, int r, string f) => (int)db.Get(t, r, t.Fields.First(x => x.Name == f));
            var roster = db.Table(Roster.RosterTable);
            var players = db.Table(Roster.PlayersTable);
            var bio = db.Table(Roster.BioTable);
            var ids = Enumerable.Range(0, players.WrittenRecords).Where(r => !db.IsInvalid(players, r)).ToDictionary(r => I(players, r, "index"), r => I(players, r, "playerid"));
            var bios = Enumerable.Range(0, bio.WrittenRecords).Where(r => !db.IsInvalid(bio, r)).Select(r => I(bio, r, "game_id")).ToHashSet();
            var ai = new HashSet<int>();
            foreach (var tn in new[] { Roster.SkaterTable, Roster.GoalieTable })
            {
                var t = db.Table(tn);
                for (int r = 0; r < t.WrittenRecords; r++) if (!db.IsInvalid(t, r)) ai.Add(I(t, r, "game_id"));
            }
            var gear = new HashSet<int>();
            foreach (var tn in new[] { "exhibitionskaterequipment", "exhibitiongoalieequipment" })
            {
                var t = db.Table(tn);
                for (int r = 0; r < t.WrittenRecords; r++) if (!db.IsInvalid(t, r)) gear.Add(I(t, r, "game_id"));
            }
            int teamsOk = 0, playersOk = 0;
            foreach (var p in plans)
            {
                int tr = Enumerable.Range(0, teams.WrittenRecords).First(r => !db.IsInvalid(teams, r) && I(teams, r, "index") == p.Slot);
                bool teamOk = I(teams, tr, "league") == o.LeagueId && (string)db.Get(teams, tr, tf["artabbr"]) == p.ArtAbbr && I(teams, tr, "arenaid") == p.Slot &&
                              (p.ArtId < 0 || I(teams, tr, "artid") == p.ArtId);
                if (!teamOk) C(false, $"{name}: team {p.Slot} ({p.Team.FullName}) isn't set as planned");
                else teamsOk++;
                var rows = Enumerable.Range(0, roster.WrittenRecords).Where(r => !db.IsInvalid(roster, r) && I(roster, r, "team") == p.Slot).ToList();
                int want = Math.Min(p.Team.Players.Count, CustomLeague.MaxTeamPlayers);
                bool keysOk = rows.All(r => I(roster, r, "key") / CustomLeague.MaxTeamPlayers == p.Slot);
                bool rowsOk = rows.All(r => ids.TryGetValue(I(roster, r, "playerindex"), out int id) && bios.Contains(id) && ai.Contains(id) && gear.Contains(id));
                if (rows.Count != want || !keysOk || !rowsOk) C(false, $"{name}: {p.Team.FullName}: {rows.Count}/{want} roster rows, keys {(keysOk ? "ok" : "wrong")}, rows {(rowsOk ? "ok" : "missing bio/ratings/equipment")}");
                else playersOk += rows.Count;
            }
            C(teamsOk == plans.Count, $"{name}: {teamsOk}/{plans.Count} teams in league {o.LeagueId}");
            var lg = db.Table("stockleagues");
            if (lg != null)
            {
                int art = o.LeagueArtId ?? o.LeagueId;
                bool artOk = Enumerable.Range(0, lg.WrittenRecords).Any(r => !db.IsInvalid(lg, r) && I(lg, r, "leagueid") == o.LeagueId && I(lg, r, "artid") == art);
                C(artOk, $"{name}: league {o.LeagueId}'s logo art id is {art}");
            }
            // every team has its strategy, arena, jersey and equipment rows (added teams have new ones)
            var slotSet = plans.Select(p => p.Slot).ToHashSet();
            foreach (var (tn, f, min) in new[] { ("exhibitionteamstrategies", "index", 1), ("customonlineteamstrategies", "index", 1), ("exhibitionarena", "index", 1), ("stockteamjerseys", "teamid", 1), ("stockteamequipmenttable", "team", 1) })
            {
                if (db.Table(tn) is not { } t) continue;
                var count = Enumerable.Range(0, t.WrittenRecords).Where(r => !db.IsInvalid(t, r)).Select(r => I(t, r, f)).Where(slotSet.Contains).GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count());
                int ok = plans.Count(p => count.GetValueOrDefault(p.Slot) >= min && (f != "index" || count.GetValueOrDefault(p.Slot) == 1));
                C(ok == plans.Count, $"{name} {tn}: rows for {ok}/{plans.Count} teams");
                var vals = Enumerable.Range(0, t.WrittenRecords).Where(r => !db.IsInvalid(t, r)).Select(r => I(t, r, f)).ToList();
                C(vals.Zip(vals.Skip(1)).All(x => x.First <= x.Second), $"{name} {tn}: sorted by {f}");
            }
            {
                var t = db.Table(Roster.TeamsTable);
                var vals = Enumerable.Range(0, t.WrittenRecords).Where(r => !db.IsInvalid(t, r)).Select(r => I(t, r, "index")).ToList();
                C(vals.Zip(vals.Skip(1)).All(x => x.First < x.Second), $"{name} {Roster.TeamsTable}: sorted by index, no index twice");
                if (db.Table("stockteamequipmenttable") is { } eq)
                    C(Enumerable.Range(0, eq.WrittenRecords).All(r => I(eq, r, "index") == r), $"{name} stockteamequipmenttable: index = row number");
            }
            C(playersOk == plans.Sum(p => Math.Min(p.Team.Players.Count, CustomLeague.MaxTeamPlayers)), $"{name}: {playersOk} players with roster, players, bio, ratings and equipment rows");
            foreach (var tn in new[] { Roster.RosterTable, RosterSync.OnlineRosterTable })
                if (db.Table(tn) is { } rt)
                {
                    var bad = RosterLines.TeamsNeedingLines(db, rt, plans.Select(p => p.Slot));
                    C(bad.Count == 0, $"{name} {tn}: lines complete for {plans.Count - bad.Count}/{plans.Count} teams");
                }
            foreach (var (tn, f) in new[] { (Roster.BioTable, "game_id"), (Roster.SkaterTable, "game_id"), (Roster.GoalieTable, "game_id"), (Roster.PlayersTable, "index"), (Roster.RosterTable, "key") })
            {
                var t = db.Table(tn);
                var vals = Enumerable.Range(0, t.WrittenRecords).Where(r => !db.IsInvalid(t, r)).Select(r => I(t, r, f)).ToList();
                C(vals.Zip(vals.Skip(1)).All(x => x.First <= x.Second), $"{name} {tn}: sorted by {f}");
            }
        }
        // the two databases give each new player the same ratings and face
        var slots = plans.Select(p => p.Slot).ToHashSet();
        Dictionary<int, (int Overall, int Head)> Looks(T3Db db)
        {
            int I(T3Table t, int r, string f) => (int)db.Get(t, r, t.Fields.First(x => x.Name == f));
            var bio = db.Table(Roster.BioTable);
            var heads = Enumerable.Range(0, bio.WrittenRecords).Where(r => !db.IsInvalid(bio, r) && slots.Contains(I(bio, r, "team")))
                .ToDictionary(r => I(bio, r, "game_id"), r => I(bio, r, "headid"));
            var d = new Dictionary<int, (int, int)>();
            foreach (var tn in new[] { Roster.SkaterTable, Roster.GoalieTable })
            {
                var t = db.Table(tn);
                for (int r = 0; r < t.WrittenRecords; r++)
                    if (!db.IsInvalid(t, r) && heads.TryGetValue(I(t, r, "game_id"), out int h)) d[I(t, r, "game_id")] = (I(t, r, "skill_overall"), h);
            }
            return d;
        }
        var g = Looks(game);
        var ie = Looks(initial);
        int same = g.Count(x => ie.TryGetValue(x.Key, out var y) && y == x.Value);
        C(same == g.Count && g.Count == ie.Count, $"both databases: {same}/{g.Count} new players with the same overall and face");
        return checks;
    }
}
