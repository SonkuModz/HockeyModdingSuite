using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NHL27Kit.Core;

// ---------------------------------------------------------------- the league pack (ncaa_leagues.json format)

public sealed class LeaguePack
{
    public string Title { get; set; }
    public string Season { get; set; }
    public List<PackLeague> Leagues { get; set; } = new();
    /// <summary>The folder the pack was read from (logos are relative to it).</summary>
    [JsonIgnore] public string Dir { get; set; }

    public static LeaguePack Load(string path)
    {
        var pack = JsonSerializer.Deserialize<LeaguePack>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"{path} isn't a league pack.");
        pack.Dir = Path.GetDirectoryName(Path.GetFullPath(path));
        return pack;
    }

    public PackLeague Conference(string key) =>
        Leagues.FirstOrDefault(l => string.Equals(l.Key, key, StringComparison.OrdinalIgnoreCase) || string.Equals(l.Name, key, StringComparison.OrdinalIgnoreCase));
}

public sealed class PackLeague
{
    public string Name { get; set; }
    public string Key { get; set; }
    public string Logo { get; set; }
    public List<PackTeam> Teams { get; set; } = new();
}

public sealed class PackColors
{
    public string Primary { get; set; }
    public string Secondary { get; set; }
    public string Tertiary { get; set; }
}

public sealed class PackKits
{
    public string Main { get; set; }
    public string Second { get; set; }
}

public sealed class PackNhl19Team
{
    public string KitsCopiedFrom { get; set; }
}

public sealed class PackTeam
{
    public string School { get; set; }
    public string ShortName { get; set; }
    public string Nickname { get; set; }
    public string FullName { get; set; }
    public string Abbreviation { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string Arena { get; set; }
    public PackColors Colors { get; set; } = new();
    public string Logo { get; set; }
    public PackKits Kits { get; set; } = new();
    public PackNhl19Team Nhl19 { get; set; } = new();
    public List<PackPlayer> Players { get; set; } = new();
}

public sealed class PackNhl19Player
{
    public int Overall { get; set; }
    public string Birthdate { get; set; }
    public int State { get; set; }
    public int Country { get; set; }
    public long Seed { get; set; }
}

public sealed class PackPlayer
{
    public string First { get; set; }
    public string Last { get; set; }
    public int Number { get; set; }
    public string Position { get; set; }
    public string Shoots { get; set; }
    public int HeightInches { get; set; }
    public int WeightLbs { get; set; }
    public int BirthYear { get; set; }
    public string Birthplace { get; set; }
    public string Captain { get; set; }
    public PackNhl19Player Nhl19 { get; set; } = new();

    public bool Goalie => Position == "G";
    /// <summary>Bio position: 0 C, 1 LW, 2 RW, 3 D, 4 G.</summary>
    public int BioPosition => Position switch { "C" => 0, "LW" => 1, "RW" => 2, "D" => 3, "G" => 4, _ => throw new InvalidDataException($"{First} {Last}: unknown position {Position}") };
}

// ---------------------------------------------------------------- building it into the roster databases

/// <summary>What goes in: which conferences of a pack, under which league id and name.</summary>
public sealed class LeagueOptions
{
    /// <summary>Conference keys of the pack to add, in order (each becomes a conference group of one league).</summary>
    public List<string> Conferences { get; set; } = new();
    /// <summary>The stock league id the teams join: 29 (TXT_USA_LEAGUE) has no teams in NHL 24.</summary>
    public int LeagueId { get; set; } = 29;
    /// <summary>The league's name in the menus (the text of the league's loc key).</summary>
    public string LeagueName { get; set; } = "NCAA";
    /// <summary>
    /// Team groups: the league group, then one conference group per conference (the last one for the rest). Group 24 is
    /// unused in NHL 24 and is the one checked in game; every conference uses it, so the league is one table.
    /// </summary>
    public int[] Groups { get; set; } = { 24, 24 };
    /// <summary>
    /// The league's logo: stockleagues' artid picks the textures ui/textures/common/leaguelogos/*/l&lt;artid&gt; (not the
    /// league id: league 29 has artid 23). Null: the league id, whose own textures the league-tex mod adds.
    /// </summary>
    public int? LeagueArtId { get; set; }
}

/// <summary>
/// One team as it was put in: its slot (team index), template and art abbreviation; Added when its rows are new
/// (past the 16 Aces rows), and its logo's art id (-1: the template's logo).
/// </summary>
public sealed record LeagueTeamPlan(PackLeague Conference, PackTeam Team, int Slot, int TemplateIndex, string TemplateName, string ArtAbbr, int Group, bool Added = false, int ArtId = -1);

/// <summary>
/// Adds a custom league to NHL 24's two Play Now roster databases (nhl_game.db and initial_experience.db) and its
/// English strings, the way the USHL was added (refs/custom-leagues.md):
/// - teams take over the 16 unused "Aces" EASHL rows (883-898, league 17, teamtype 1), each with its own arena,
///   strategy, jersey and equipment rows: a template team's rows are copied over the slot's in place, so every team
///   table keeps its row count (rows added to those tables are never read by the game);
/// - players are added (bio, ratings, equipment, players and both roster tables), with ids no table uses, roster keys
///   team × 40 + slot, and every table put back in its stock sort order; lines are built as the game's are;
/// - names come from loc keys by art abbreviation; new keys take over strings of team abbreviations no team has, so
///   the full LanguageStrings table keeps its row count.
/// Ratings: each player copies the ratings of a stock AHL/CHL/ECHL player of his position with the overall the pack
/// gives him (nhl19.overall, 60-70).
/// </summary>
public static class CustomLeague
{
    public const string GameDb = Roster.Database;
    public const string InitialDb = "gamemodes/roster/initial_experience.db";
    public const string Kind = "league";
    public const int MaxTeamPlayers = 40;
    public const int FirstPlayerId = 40000, LastPlayerId = 56499;

    private static readonly string[] TemplateLeagues = { "1", "11", "12", "13", "15" };
    private static readonly int[] TemplateLeagueIds = { 1, 11, 12, 13, 15 };

    /// <summary>The loc key families a team's names use, keyed by its art abbreviation.</summary>
    public static readonly string[] LocFamilies =
        { "NHLTeamName_Abbr3_", "NHLTeamName_", "NHLCityName_", "TXT_NICKNAME_ALT_", "TXT_NICKNAME_", "TEAMLINE1_", "TEAMLINE2_", "X_XLA_TEAM_" };

    /// <summary>nhl_game.db and initial_experience.db, read fresh; the second with the first's meta (its own names are scrambled).</summary>
    public static (T3Db Game, T3Db Initial, T3Db Loc) Open(Legacy legacy)
    {
        var game = legacy.OpenDb(GameDb);
        var f = legacy.Find(InitialDb) ?? throw new FileNotFoundException($"No legacy file {InitialDb}.");
        string meta = Encoding.UTF8.GetString(legacy.Read(GameDb[..^3] + "-meta.xml")).TrimStart('﻿');
        var initial = new T3Db(legacy.Read(f), meta) { Source = f };
        var loc = legacy.OpenDb(LocDb.English);
        return (game, initial, loc);
    }

    // ---------------------------------------------------------------- helpers

    private sealed class Tbl
    {
        public T3Db Db;
        public T3Table T;
        public Dictionary<string, T3Field> F;
        public Tbl(T3Db db, string name)
        {
            Db = db;
            T = db.Table(name);
            F = T?.Fields.ToDictionary(x => x.Name, StringComparer.Ordinal);
        }
        public bool Exists => T != null;
        public int Int(int r, string f) => (int)Db.Get(T, r, F[f]);
        public string Str(int r, string f) => (string)Db.Get(T, r, F[f]);
        public bool Has(string f) => F.ContainsKey(f);
        public void Set(int r, string f, object v)
        {
            if (!F.TryGetValue(f, out var x)) return;
            if (x.Type == T3FieldType.Integer)
            {
                long v2 = Convert.ToInt64(v), max = x.RangeLow + (1L << x.Depth) - 1;
                v2 = Math.Clamp(v2, x.RangeLow, max);
                Db.Set(T, r, x, (int)v2);
            }
            else if (x.Type == T3FieldType.String)
            {
                string s = Fit((string)v ?? "", x.Depth / 8 - 1);
                Db.Set(T, r, x, s);
            }
            else Db.Set(T, r, x, v);
        }
        public IEnumerable<int> Rows()
        {
            for (int r = 0; r < T.WrittenRecords; r++)
                if (!Db.IsInvalid(T, r)) yield return r;
        }
        public int Find(string f, int v) => Rows().Where(r => Int(r, f) == v).DefaultIfEmpty(-1).First();
        /// <summary>A new row at the end of the table, a copy of <paramref name="from"/> (a row of this table).</summary>
        public int Add(byte[] from)
        {
            if (T.WrittenRecords >= T.Records) throw new InvalidOperationException($"{T.Name} is full ({T.Records:N0} rows).");
            int row = T.WrittenRecords;
            Db.SetWritten(T, row + 1);
            Db.WriteRecord(T, row, from);
            return row;
        }
        public bool SortedBy(string f)
        {
            int prev = int.MinValue;
            foreach (int r in Rows()) { int v = Int(r, f); if (v < prev) return false; prev = v; }
            return true;
        }
        public void SortBy(string f)
        {
            var order = Enumerable.Range(0, T.WrittenRecords).OrderBy(r => Db.IsInvalid(T, r) ? int.MaxValue : Int(r, f)).ThenBy(r => r).ToList();
            if (!order.Select((r, i) => r == i).All(x => x)) Db.Reorder(T, order);
        }
    }

    /// <summary>Text cut to fit <paramref name="bytes"/> UTF-8 bytes, accents folded to ASCII.</summary>
    public static string Fit(string s, int bytes)
    {
        s = Ascii(s);
        while (Encoding.UTF8.GetByteCount(s) > bytes) s = s[..^1];
        return s;
    }

    /// <summary>Accents removed (Québec → Quebec): the game's fixed strings are plain text.</summary>
    public static string Ascii(string s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? "";
        var d = s.Normalize(NormalizationForm.FormD);
        var b = new StringBuilder();
        foreach (char c in d)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) b.Append(c);
        return b.ToString().Normalize(NormalizationForm.FormC);
    }

    public static int Rgb(string hex) => TeamEditor.ParseHex(hex) ?? 0;

    private static readonly Dictionary<string, string> States = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Alabama"] = "AL", ["Alaska"] = "AK", ["Arizona"] = "AZ", ["California"] = "CA", ["Colorado"] = "CO", ["Connecticut"] = "CT",
        ["Illinois"] = "IL", ["Indiana"] = "IN", ["Maine"] = "ME", ["Massachusetts"] = "MA", ["Michigan"] = "MI", ["Minnesota"] = "MN",
        ["Missouri"] = "MO", ["Nebraska"] = "NE", ["New Hampshire"] = "NH", ["New Jersey"] = "NJ", ["New York"] = "NY", ["North Dakota"] = "ND",
        ["Ohio"] = "OH", ["Pennsylvania"] = "PA", ["Rhode Island"] = "RI", ["Vermont"] = "VT", ["Wisconsin"] = "WI",
    };

    public static string ArenaCity(PackTeam t) => States.TryGetValue(t.State ?? "", out var st) ? $"{t.City}, {st}" : t.City;

    /// <summary>The team's art abbreviation (its loc keys' suffix): "NC" + its abbreviation, unique among the teams.</summary>
    public static string ArtAbbr(PackTeam t) => Fit("NC" + t.Abbreviation, 11).ToUpperInvariant();

    // ---------------------------------------------------------------- plan

    /// <summary>The stock "Aces" placeholder rows: league 17, teamtype 1, named Aces, with nobody on their roster.</summary>
    public static List<int> FreeSlots(T3Db db)
    {
        var teams = new Tbl(db, Roster.TeamsTable);
        var roster = new Tbl(db, Roster.RosterTable);
        var withPlayers = roster.Rows().Select(r => roster.Int(r, "team")).ToHashSet();
        return teams.Rows()
            .Where(r => teams.Int(r, "league") == 17 && teams.Int(r, "teamtype") == 1 && teams.Str(r, "teamname") == "Aces" && !withPlayers.Contains(teams.Int(r, "index")))
            .Select(r => teams.Int(r, "index")).OrderBy(i => i).ToList();
    }

    /// <summary>The tables with one row per team (keyed by team index) that an added team needs a row in.</summary>
    public static readonly string[] TeamRowTables = { Roster.TeamsTable, "exhibitionteamstrategies", "customonlineteamstrategies", "exhibitionarena" };

    /// <summary>Team indices no team, strategy, arena, jersey, equipment or roster row of either database uses (the custom-team range 400-599 left out).</summary>
    public static List<int> FreeIndices(T3Db game, T3Db initial)
    {
        var used = new HashSet<int>();
        foreach (var db in new[] { game, initial })
            foreach (var (name, field) in new[]
                     {
                         (Roster.TeamsTable, "index"), (Roster.TeamsTable, "arenaid"), ("customteams", "index"), ("customteams", "arenaid"), ("exhibitionteamstrategies", "index"),
                         ("customonlineteamstrategies", "index"), ("exhibitionarena", "index"), ("customarena", "index"), ("stockteamjerseys", "teamid"),
                         ("stockteamequipmenttable", "team"), ("stockgoalieequipment", "team"), (Roster.RosterTable, "team"), (Roster.BioTable, "team"),
                     })
                if (new Tbl(db, name) is { Exists: true } t && t.Has(field))
                    foreach (int r in t.Rows()) used.Add(t.Int(r, field));
        // the stock teams' indices end at 996: 600 and up first, then any gap below the custom teams
        return Enumerable.Range(600, 424).Concat(Enumerable.Range(0, 400)).Where(i => !used.Contains(i)).ToList();
    }

    public static List<LeagueTeamPlan> Plan(T3Db game, T3Db initial, LeaguePack pack, LeagueOptions o)
    {
        var confs = o.Conferences.Select(k => pack.Conference(k) ?? throw new ArgumentException($"The pack has no conference {k}.")).ToList();
        var teams = confs.SelectMany((c, ci) => c.Teams.Where(t => t.Players.Count > 0).Select(t => (c, t, ci))).ToList();
        var slots = FreeSlots(game);
        var slotsIe = FreeSlots(initial).ToHashSet();
        slots = slots.Where(slotsIe.Contains).ToList();
        int aces = slots.Count;
        // teams past the Aces rows get new rows, at team indices nothing uses
        int more = Math.Max(0, teams.Count - aces);
        if (more > 0)
        {
            foreach (var db in new[] { game, initial })
                foreach (var name in TeamRowTables)
                    if (db.Table(name) is { } t && t.Records - t.WrittenRecords < more)
                        throw new InvalidOperationException($"{teams.Count} teams: {aces} go in the Aces rows and {more} need new rows, but {(db == game ? "nhl_game.db" : "initial_experience.db")} {name} has room for {t.Records - t.WrittenRecords} more.");
            var free = FreeIndices(game, initial);
            if (free.Count < more) throw new InvalidOperationException($"Only {free.Count} free team indices for {more} new teams.");
            slots.AddRange(free.Take(more));
        }
        var tt = new Tbl(game, Roster.TeamsTable);
        var byName = new Dictionary<string, (int Index, string Name)>(StringComparer.OrdinalIgnoreCase);
        foreach (int r in tt.Rows())
            if (TemplateLeagueIds.Contains(tt.Int(r, "league")))
                byName.TryAdd(Ascii(tt.Str(r, "teamname")), (tt.Int(r, "index"), tt.Str(r, "teamname")));
        var fallback = tt.Rows().Where(r => tt.Int(r, "league") == 13).Select(r => (Index: tt.Int(r, "index"), Name: tt.Str(r, "teamname"))).First();
        var abbrs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var db in new[] { game, initial })
            foreach (var name in new[] { Roster.TeamsTable, "customteams" })
                if (new Tbl(db, name) is { Exists: true } x)
                    foreach (int r in x.Rows()) abbrs.Add(x.Str(r, "artabbr"));
        var plans = new List<LeagueTeamPlan>();
        for (int i = 0; i < teams.Count; i++)
        {
            var (c, t, ci) = teams[i];
            var tpl = byName.TryGetValue(Ascii(t.Nhl19?.KitsCopiedFrom ?? ""), out var x) ? x : fallback;
            string art = ArtAbbr(t);
            if (abbrs.Contains(art) || plans.Any(p => p.ArtAbbr == art)) throw new InvalidOperationException($"Art abbreviation {art} ({t.FullName}) is already used.");
            int group = o.Groups.Length > ci + 1 ? o.Groups[ci + 1] : o.Groups[^1];
            int artId = LeagueLogos.LogoPath(pack.Dir, t) != null ? LeagueLogos.FirstArtId + i : -1;
            plans.Add(new LeagueTeamPlan(c, t, slots[i], tpl.Index, tpl.Name, art, group, i >= aces, artId));
        }
        return plans;
    }

    // ---------------------------------------------------------------- build

    /// <summary>Puts the teams and players into one roster database. Returns a short summary.</summary>
    public static string Build(T3Db db, IReadOnlyList<LeagueTeamPlan> plans, LeagueOptions o, HashSet<int> usedIds, IReadOnlyDictionary<string, int> surnames, Dictionary<(int Slot, int N), int> picks, List<string> log, T3Db source = null)
    {
        using var batch = db.Batch();
        var teams = new Tbl(db, Roster.TeamsTable);
        var strat = new Tbl(db, "exhibitionteamstrategies");
        var arena = new Tbl(db, "exhibitionarena");
        var jerseys = new Tbl(db, "stockteamjerseys");
        var equip = new Tbl(db, "stockteamequipmenttable");
        var palette = new Tbl(db, "stockteamcolorstable");
        var onlineStrat = new Tbl(db, "customonlineteamstrategies");
        int jerseysCopied = 0, equipCopied = 0, teamsAdded = 0;
        var leagues = new Tbl(db, "stockleagues");
        if (leagues.Exists && leagues.Find("leagueid", o.LeagueId) is int lr && lr >= 0)
        {
            int art = o.LeagueArtId ?? o.LeagueId;
            if (leagues.Int(lr, "artid") != art) log.Add($"{db.Source?.Name}: league {o.LeagueId}'s logo art id {leagues.Int(lr, "artid")} -> {art}");
            leagues.Set(lr, "artid", art);
        }

        // the sort orders the team tables ship in, put back after rows are added
        var teamSorts = new List<(Tbl T, string F)> { (teams, "index"), (strat, "index"), (onlineStrat, "index"), (arena, "index"), (jerseys, "teamid"), (equip, "team") }
            .Where(x => x.T.Exists && x.T.SortedBy(x.F)).ToList();
        // the equipment table's index is its row number in stock: renumbered after a sort
        bool equipNumbered = equip.Exists && equip.Rows().All(r => equip.Int(r, "index") == r);

        foreach (var p in plans.Where(p => p.Added))
        {
            if (teams.Find("index", p.Slot) >= 0) throw new InvalidDataException($"Team index {p.Slot} is already used in {db.Source?.Name}.");
            int tt = teams.Find("index", p.TemplateIndex);
            if (tt < 0) throw new InvalidDataException($"Template team {p.TemplateIndex} is missing in {db.Source?.Name}.");
            int tr = teams.Add(teams.Db.RecordBytes(teams.T, tt));
            teams.Set(tr, "index", p.Slot);
            foreach (var st in new[] { strat, onlineStrat })
            {
                if (!st.Exists) continue;
                int f = st.Find("index", p.TemplateIndex);
                if (f < 0) throw new InvalidDataException($"{st.T.Name} has no row for template team {p.TemplateIndex}.");
                st.Set(st.Add(st.Db.RecordBytes(st.T, f)), "index", p.Slot);
            }
            if (arena.Exists)
            {
                int f = arena.Find("index", teams.Int(tt, "arenaid"));
                if (f < 0) throw new InvalidDataException($"No arena row {teams.Int(tt, "arenaid")} for template team {p.TemplateIndex}.");
                arena.Set(arena.Add(arena.Db.RecordBytes(arena.T, f)), "index", p.Slot);
            }
            if (jerseys.Exists)
                foreach (int r in jerseys.Rows().Where(r => jerseys.Int(r, "teamid") == p.TemplateIndex).ToList())
                    jerseys.Set(jerseys.Add(jerseys.Db.RecordBytes(jerseys.T, r)), "teamid", p.Slot);
            if (equip.Exists)
                foreach (int r in equip.Rows().Where(r => equip.Int(r, "team") == p.TemplateIndex).ToList())
                    equip.Set(equip.Add(equip.Db.RecordBytes(equip.T, r)), "team", p.Slot);
            teamsAdded++;
        }

        foreach (var p in plans)
        {
            int tr = teams.Find("index", p.Slot), tt = teams.Find("index", p.TemplateIndex);
            if (tr < 0 || tt < 0) throw new InvalidDataException($"Team row {p.Slot} or template {p.TemplateIndex} is missing in {db.Source?.Name}.");
            int templateArena = teams.Int(tt, "arenaid");
            teams.Db.WriteRecord(teams.T, tr, teams.Db.RecordBytes(teams.T, tt));
            var t = p.Team;
            teams.Set(tr, "index", p.Slot);
            teams.Set(tr, "league", o.LeagueId);
            teams.Set(tr, "leaguegroup", o.Groups[0]);
            teams.Set(tr, "conferencegroup", p.Group);
            teams.Set(tr, "divisiongroup", p.Group);
            teams.Set(tr, "teamname", t.FullName);
            teams.Set(tr, "cityname", t.ShortName);
            teams.Set(tr, "nickname", t.Nickname);
            teams.Set(tr, "abbrname", t.Abbreviation);
            teams.Set(tr, "artabbr", p.ArtAbbr);
            teams.Set(tr, "arenaid", p.Slot);
            teams.Set(tr, "rivalteam", -1);
            teams.Set(tr, "rivalteam2", -1);
            teams.Set(tr, "ahlaffiliate", -1);
            teams.Set(tr, "active", 1);
            if (p.ArtId >= 0) teams.Set(tr, "artid", p.ArtId);
            int c1 = Rgb(t.Colors.Primary), c2 = Rgb(t.Colors.Secondary), c3 = string.IsNullOrEmpty(t.Colors.Tertiary) ? c2 : Rgb(t.Colors.Tertiary);
            foreach (var f in teams.T.Fields)
            {
                if (f.Name.EndsWith("primarycolor")) teams.Set(tr, f.Name, c1);
                else if (f.Name.EndsWith("secondarycolor")) teams.Set(tr, f.Name, c2);
                else if (f.Name.EndsWith("tertiarycolor")) teams.Set(tr, f.Name, c3);
            }

            // the slot's own strategy and arena rows, overwritten with the template's
            CopyOver(strat, "index", p.Slot, p.TemplateIndex, keep: null);
            CopyOver(onlineStrat, "index", p.Slot, p.TemplateIndex, keep: null);
            if (CopyOver(arena, "index", p.Slot, templateArena, keep: null) is int ar and >= 0)
            {
                arena.Set(ar, "arenaname", t.Arena);
                arena.Set(ar, "cityname", ArenaCity(t));
            }
            // jerseys by jersey index, equipment by (equipment, variant): template rows past the slot's are skipped
            if (jerseys.Exists)
            {
                var tpl = jerseys.Rows().Where(r => jerseys.Int(r, "teamid") == p.TemplateIndex).ToList();
                foreach (int r in jerseys.Rows().Where(r => jerseys.Int(r, "teamid") == p.Slot).ToList())
                {
                    int from = tpl.FirstOrDefault(x => jerseys.Int(x, "jerseyindex") == jerseys.Int(r, "jerseyindex"), -1);
                    if (from < 0) continue;
                    jerseys.Db.WriteRecord(jerseys.T, r, jerseys.Db.RecordBytes(jerseys.T, from));
                    jerseys.Set(r, "teamid", p.Slot);
                    jerseysCopied++;
                }
            }
            if (equip.Exists)
            {
                var tpl = equip.Rows().Where(r => equip.Int(r, "team") == p.TemplateIndex).ToList();
                foreach (int r in equip.Rows().Where(r => equip.Int(r, "team") == p.Slot).ToList())
                {
                    // the template's row for this item and jersey, else its row for every jersey (variant -1)
                    int item = equip.Int(r, "equipment"), variant = equip.Int(r, "variant");
                    int from = tpl.FirstOrDefault(x => equip.Int(x, "equipment") == item && equip.Int(x, "variant") == variant && equip.Int(x, "era") == -1, -1);
                    if (from < 0) from = tpl.FirstOrDefault(x => equip.Int(x, "equipment") == item && equip.Int(x, "variant") == -1 && equip.Int(x, "era") == -1, -1);
                    if (from < 0) continue;
                    int keepIndex = equip.Int(r, "index"), keepEra = equip.Int(r, "era");
                    equip.Db.WriteRecord(equip.T, r, equip.Db.RecordBytes(equip.T, from));
                    equip.Set(r, "team", p.Slot);
                    equip.Set(r, "index", keepIndex);
                    equip.Set(r, "variant", variant);
                    equip.Set(r, "era", keepEra);
                    equipCopied++;
                }
            }
            if (palette.Exists)
            {
                var tpl = palette.Rows().Where(r => palette.Int(r, "team") == p.TemplateIndex).ToList();
                foreach (int r in palette.Rows().Where(r => palette.Int(r, "team") == p.Slot).ToList())
                {
                    int from = tpl.FirstOrDefault(x => palette.Int(x, "colorid") == palette.Int(r, "colorid"), -1);
                    if (from >= 0) palette.Set(r, "color", palette.Int(from, "color"));
                }
            }
        }

        foreach (var (t, f) in teamSorts) t.SortBy(f);
        if (equipNumbered && teamsAdded > 0)
            foreach (int r in equip.Rows().ToList()) equip.Set(r, "index", r);
        if (teamsAdded > 0) log?.Add($"{db.Source?.Name}: {teamsAdded} teams added as new rows (team, strategy, arena, jersey and equipment rows), tables sorted again");

        int players = AddPlayers(db, plans, usedIds, surnames, picks, log, source);
        foreach (var name in new[] { Roster.RosterTable, RosterSync.OnlineRosterTable })
            if (db.Table(name) is { } rt)
                foreach (var p in plans) RosterLines.Build(db, rt, p.Slot);
        return $"{plans.Count} teams ({plans.Count(p => p.Added)} in new rows), {players} players, {jerseysCopied} jersey rows and {equipCopied} equipment rows from their templates";
    }

    /// <summary>The row of <paramref name="t"/> whose <paramref name="key"/> is <paramref name="slot"/>, overwritten with the row keyed <paramref name="from"/> (the key kept).</summary>
    private static int CopyOver(Tbl t, string key, int slot, int from, string keep)
    {
        if (!t.Exists) return -1;
        int r = t.Find(key, slot), f = t.Find(key, from);
        if (r < 0 || f < 0) return -1;
        t.Db.WriteRecord(t.T, r, t.Db.RecordBytes(t.T, f));
        t.Set(r, key, slot);
        return r;
    }

    /// <summary>Every player id and players-table index a database uses (bios, ratings, equipment, players, rosters).</summary>
    public static HashSet<int> UsedIds(T3Db db)
    {
        var used = new HashSet<int>();
        foreach (var (name, field) in new[]
                 {
                     (Roster.BioTable, "game_id"), (Roster.SkaterTable, "game_id"), (Roster.GoalieTable, "game_id"),
                     ("exhibitionskaterequipment", "game_id"), ("exhibitiongoalieequipment", "game_id"),
                     (Roster.PlayersTable, "index"), (Roster.PlayersTable, "playerid"),
                     (Roster.RosterTable, "playerindex"), (RosterSync.OnlineRosterTable, "playerindex"),
                 })
            if (new Tbl(db, name) is { Exists: true } t && t.Has(field))
                foreach (int r in t.Rows()) used.Add(t.Int(r, field));
        return used;
    }

    /// <summary>Commentary surname ids by surname (nhl_game.db's playernames; initial_experience.db has none of its own).</summary>
    public static Dictionary<string, int> Surnames(T3Db game)
    {
        var names = new Tbl(game, "playernames");
        var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (names.Exists)
            foreach (int r in names.Rows())
                if (names.Int(r, "isfirstname") == 0 && names.Int(r, "surnameid") > 0) d.TryAdd(names.Str(r, "name"), names.Int(r, "surnameid"));
        return d;
    }

    private sealed record Template(int BioRow, int GameId, int Position, int Overall, int AiRow, int EquipRow, int PlayersRow, int RosterRow, int OnlineRow);

    private static int AddPlayers(T3Db db, IReadOnlyList<LeagueTeamPlan> plans, HashSet<int> usedIds, IReadOnlyDictionary<string, int> surnames, Dictionary<(int Slot, int N), int> picks, List<string> log, T3Db source)
    {
        var bio = new Tbl(db, Roster.BioTable);
        var skAi = new Tbl(db, Roster.SkaterTable);
        var gAi = new Tbl(db, Roster.GoalieTable);
        var skEq = new Tbl(db, "exhibitionskaterequipment");
        var gEq = new Tbl(db, "exhibitiongoalieequipment");
        var players = new Tbl(db, Roster.PlayersTable);
        var roster = new Tbl(db, Roster.RosterTable);
        var online = new Tbl(db, RosterSync.OnlineRosterTable);
        var teams = new Tbl(db, Roster.TeamsTable);

        // sort orders as the game ships them, to put back after adding
        var allSorts = new List<(Tbl, string)> { (bio, "game_id"), (skAi, "game_id"), (gAi, "game_id"), (skEq, "game_id"), (gEq, "game_id"), (players, "index"), (roster, "key"), (online, "key") }
            .Where(x => x.Item1.Exists).ToList();
        var sorts = allSorts.Where(x => x.Item1.SortedBy(x.Item2)).ToList();
        var unsorted = allSorts.Except(sorts).Select(x => $"{x.Item1.T.Name} by {x.Item2}").ToList();
        if (unsorted.Count > 0) log?.Add($"{db.Source?.Name}: not sorted in stock, new rows left at the end: {string.Join(", ", unsorted)}");

        var leagueOf = teams.Rows().ToDictionary(r => teams.Int(r, "index"), r => teams.Int(r, "league"));
        Dictionary<int, int> RowBy(Tbl t, string f) { var d = new Dictionary<int, int>(); if (t.Exists) foreach (int r in t.Rows()) d.TryAdd(t.Int(r, f), r); return d; }
        var skAiRow = RowBy(skAi, "game_id"); var gAiRow = RowBy(gAi, "game_id");
        var skEqRow = RowBy(skEq, "game_id"); var gEqRow = RowBy(gEq, "game_id");
        var playersRowById = RowBy(players, "playerid");
        var rosterRow = RowBy(roster, "playerindex"); var onlineRow = online.Exists ? RowBy(online, "playerindex") : new();

        // template players: dressed AHL, CHL and ECHL players with a full set of rows
        var pool = new List<Template>();
        foreach (int r in bio.Rows())
        {
            if (!leagueOf.TryGetValue(bio.Int(r, "team"), out int lg) || !TemplateLeagueIds.Contains(lg) || bio.Int(r, "active") != 1) continue;
            int id = bio.Int(r, "game_id"), pos = bio.Int(r, "position");
            var (ai, ait) = pos == 4 ? (gAiRow, gAi) : (skAiRow, skAi);
            var eq = pos == 4 ? gEqRow : skEqRow;
            if (!ai.TryGetValue(id, out int air) || !eq.TryGetValue(id, out int eqr) || !playersRowById.TryGetValue(id, out int pr)) continue;
            int pi = players.Int(pr, "index");
            if (!rosterRow.TryGetValue(pi, out int rr)) continue;
            pool.Add(new Template(r, id, pos, ait.Int(air, "skill_overall"), air, eqr, pr, rr, onlineRow.GetValueOrDefault(pi, -1)));
        }
        if (pool.Count == 0) throw new InvalidDataException("No template players found.");

        // a new player's finished rows in the database built first, by id: copied as they are, so both databases
        // give him the same ratings, face and equipment (only when the two tables are laid out alike)
        Dictionary<int, int> SourceRows(Tbl t, string key)
        {
            if (source == null || !t.Exists || source.Table(t.T.Name) is not { } st || st.RecordSize != t.T.RecordSize ||
                !st.Fields.Select(f => (f.Name, f.BitOffset, f.Depth, f.Type)).SequenceEqual(t.T.Fields.Select(f => (f.Name, f.BitOffset, f.Depth, f.Type))))
                return null;
            var d = new Dictionary<int, int>();
            for (int r = 0; r < st.WrittenRecords; r++)
                if (!source.IsInvalid(st, r)) d.TryAdd((int)source.Get(st, r, st.Fields.First(f => f.Name == key)), r);
            return d;
        }
        var srcRows = new Dictionary<Tbl, Dictionary<int, int>>();
        foreach (var (t, key) in new[] { (bio, "game_id"), (skAi, "game_id"), (gAi, "game_id"), (skEq, "game_id"), (gEq, "game_id"), (players, "index"), (roster, "playerindex"), (online, "playerindex") })
            if (SourceRows(t, key) is { } d) srcRows[t] = d;
        int copied = 0;
        byte[] From(Tbl t, int id, int fallbackRow)
        {
            if (srcRows.TryGetValue(t, out var d) && d.TryGetValue(id, out int r) && id >= FirstPlayerId) { copied++; return source.RecordBytes(source.Table(t.T.Name), r); }
            return t.Db.RecordBytes(t.T, fallbackRow);
        }

        int nextId = FirstPlayerId;
        int NewId()
        {
            while (usedIds.Contains(nextId)) nextId++;
            if (nextId > LastPlayerId) throw new InvalidOperationException("No free player ids left.");
            usedIds.Add(nextId);
            return nextId;
        }

        int added = 0, noSurname = 0;
        var useCount = new Dictionary<int, int>();
        foreach (var plan in plans)
        {
            int slotKey = 0, n = 0;
            var poolById = pool.GroupBy(x => x.GameId).ToDictionary(g => g.Key, g => g.First());
            foreach (var pl in plan.Team.Players.Take(MaxTeamPlayers))
            {
                var pickKey = (plan.Slot, n++);
                int pos = pl.BioPosition;
                int target = pl.Nhl19?.Overall is > 0 and var ov ? ov : 65;
                var cands = pool.Where(x => x.Position == pos).ToList();
                if (cands.Count == 0) cands = pool.Where(x => (x.Position == 4) == (pos == 4)).ToList();
                int best = cands.Min(x => Math.Abs(x.Overall - target));
                var near = cands.Where(x => Math.Abs(x.Overall - target) == best).OrderBy(x => useCount.GetValueOrDefault(x.GameId)).ThenBy(x => x.GameId).ToList();
                int least = useCount.GetValueOrDefault(near[0].GameId);
                near = near.Where(x => useCount.GetValueOrDefault(x.GameId) == least).ToList();
                var tp = near[(int)(Math.Abs(pl.Nhl19?.Seed ?? 0) % near.Count)];
                // the template the other database used for him, when this one has it too
                if (picks.TryGetValue(pickKey, out int earlier) && poolById.TryGetValue(earlier, out var same) && same.Position == tp.Position) tp = same;
                picks[pickKey] = tp.GameId;
                useCount[tp.GameId] = useCount.GetValueOrDefault(tp.GameId) + 1;

                int id = NewId();
                // bio
                int b = bio.Add(From(bio, id, tp.BioRow));
                bio.Set(b, "game_id", id);
                bio.Set(b, "firstname", pl.First);
                bio.Set(b, "lastname", pl.Last);
                bio.Set(b, "nickname", "");
                bio.Set(b, "birthplace", pl.Birthplace);
                if (pl.HeightInches > 0) bio.Set(b, "height", pl.HeightInches - RosterSync.HeightOffset);
                if (pl.WeightLbs > 0) bio.Set(b, "weight", pl.WeightLbs - RosterSync.WeightOffset);
                if (pl.BirthYear > 0) bio.Set(b, "year", pl.BirthYear - 1900);
                if (DateTime.TryParse(pl.Nhl19?.Birthdate, out var bd)) { bio.Set(b, "month", bd.Month - 1); bio.Set(b, "day", bd.Day - 1); }
                bio.Set(b, "state", pl.Nhl19?.State ?? 0);
                bio.Set(b, "intlcountry", pl.Nhl19?.Country ?? 0);
                bool left = !string.Equals(pl.Shoots, "R", StringComparison.OrdinalIgnoreCase);
                bio.Set(b, "handedness", left ? 0 : 1);
                bio.Set(b, "position", pos);
                bio.Set(b, "positionprimary", pos switch { 3 => left ? 3 : 4, 4 => 5, _ => pos });
                bio.Set(b, "positionsecondary", -1);
                bio.Set(b, "jersey", pl.Number);
                bio.Set(b, "team", plan.Slot);
                if (surnames.TryGetValue(Ascii(pl.Last), out int sn)) bio.Set(b, "surnameid", sn);
                else { bio.Set(b, "surnameid", -1); noSurname++; }
                bio.Set(b, "audioid", 0);
                bio.Set(b, "hasportrait", 0);
                foreach (var f in new[] { "juniorleague", "juniorteam", "proteam", "draftteam" }) bio.Set(b, f, -1);
                foreach (var f in new[] { "draftround", "draftposition", "cupswon", "progamesplayedcareer", "nhlcareergoalsregularseason", "nhlcareergamesplayedregularseason",
                             "nhlgamesplayedcareer", "nhlgamesplayedlastseason", "nhlgamesplayedtwoseasonsago", "nhlgamesplayedthreeseasonsago", "nhlgamesmissedinjurylastseason" })
                    bio.Set(b, f, 0);
                bio.Set(b, "cupsyearlastwon", -1);
                bio.Set(b, "active", 1);

                // ratings and equipment: the template's
                var (ai, eq) = pos == 4 ? (gAi, gEq) : (skAi, skEq);
                int a = ai.Add(From(ai, id, tp.AiRow));
                ai.Set(a, "game_id", id);
                int e = eq.Add(From(eq, id, tp.EquipRow));
                eq.Set(e, "game_id", id);

                // players: index = id
                int pr = players.Add(From(players, id, tp.PlayersRow));
                players.Set(pr, "index", id);
                players.Set(pr, "playerid", id);

                // both roster tables
                int key = plan.Slot * MaxTeamPlayers + slotKey++;
                foreach (var (rt, from) in new[] { (roster, tp.RosterRow), (online, tp.OnlineRow) })
                {
                    if (!rt.Exists) continue;
                    // the template's own row of this table, or any row of it
                    int src = rt == roster ? tp.RosterRow : from >= 0 ? from : rt.Rows().First();
                    int rr = rt.Add(From(rt, id, src));
                    rt.Set(rr, "playerindex", id);
                    rt.Set(rr, "team", plan.Slot);
                    rt.Set(rr, "key", key);
                    rt.Set(rr, "jersey", pl.Number);
                    rt.Set(rr, "captain", pl.Captain switch { "C" => 1, "A" => 2, _ => 0 });
                    rt.Set(rr, "rosterstatus", 1);
                    foreach (var s in rt.T.Fields.Where(x => x.Type == T3FieldType.Integer && x.Depth == 1 && RosterLines.Category(x.Name) != null))
                        rt.Db.Set(rt.T, rr, s, 0);
                }
                added++;
            }
        }
        foreach (var (t, f) in sorts) t.SortBy(f);
        log?.Add($"{db.Source?.Name}: {added} players added (ids from {FirstPlayerId}), {noSurname} without a commentary surname" +
                 (source != null ? $", {copied} rows copied from {source.Source?.Name}" : ""));
        return added;
    }

    // ---------------------------------------------------------------- strings

    /// <summary>The loc texts the league needs: each team's names by its art abbreviation, and the league's name.</summary>
    public static Dictionary<string, string> Texts(T3Db game, IReadOnlyList<LeagueTeamPlan> plans, LeagueOptions o)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        var leagues = new Tbl(game, "stockleagues");
        int lr = leagues.Find("leagueid", o.LeagueId);
        if (lr >= 0) d[leagues.Str(lr, "name")] = o.LeagueName;
        foreach (var p in plans)
        {
            var t = p.Team;
            string a = p.ArtAbbr;
            d["NHLTeamName_" + a] = t.FullName;
            d["NHLCityName_" + a] = t.ShortName;
            d["TXT_NICKNAME_" + a] = t.Nickname;
            d["TXT_NICKNAME_ALT_" + a] = t.Nickname;
            d["TEAMLINE1_" + a] = t.ShortName;
            d["TEAMLINE2_" + a] = t.Nickname;
            d["NHLTeamName_Abbr3_" + a] = t.Abbreviation;
            d["X_XLA_TEAM_" + a] = t.Abbreviation;
        }
        return d.ToDictionary(x => x.Key, x => Ascii(x.Value));
    }

    /// <summary>The other roster databases (drafts, threes campaign): their teams' abbreviations are kept off the spare list.</summary>
    public static readonly string[] OtherRosterDbs = { "gamemodes/roster/entry_draft.db", "gamemodes/roster/expansion_draft.db", "gamemodes/roster/threes_campaign.db" };

    /// <summary>The other roster databases that can be read (with nhl_game.db's meta), for <see cref="SpareKeys"/>.</summary>
    public static List<T3Db> OpenOthers(Legacy legacy, List<string> log = null)
    {
        string meta = Encoding.UTF8.GetString(legacy.Read(GameDb[..^3] + "-meta.xml")).TrimStart('\uFEFF');
        var list = new List<T3Db>();
        foreach (var name in OtherRosterDbs)
        {
            if (legacy.Find(name) is not { } f) continue;
            try { list.Add(new T3Db(legacy.Read(f), meta) { Source = f }); }
            catch (Exception e) { log?.Add($"{name} can't be read ({e.Message}); its team abbreviations aren't checked"); }
        }
        return list;
    }

    private static readonly System.Text.RegularExpressions.Regex Special =
        new(@"\b(Team|All-?Stars?|Icons?|Alumni|Free Agents?|Europe|North America|NHL|NHLr|Legends?|Pro-Am|Uprise|Yetis|Nutcrackers|Elves)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>
    /// Loc keys that can be taken over, safest first: team-name keys whose abbreviation no team of any roster database
    /// (nor the custom teams) has.
    /// 1. Old circuit teams (1T03, 5T18): fixed per-event teams no database has any more.
    /// 2. Defunct clubs: an abbreviation with a full set of name keys (5 or more families) none of whose texts reads like
    ///    a special team (Team USA, All-Stars, Icons...). Abbreviations with only a key or two (LAK, SJS, ARZ) are spares
    ///    of live teams' other names and are never taken.
    /// Numbered keys (NHLCityName_0 = Anaheim) are a by-number family the game may still use, so they're never taken.
    /// </summary>
    public static List<string> SpareKeys(T3Db loc, IEnumerable<T3Db> rosterDbs)
    {
        var abbrs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var db in rosterDbs)
            foreach (var name in new[] { Roster.TeamsTable, "customteams" })
                if (new Tbl(db, name) is { Exists: true } x && x.Has("artabbr"))
                    foreach (int r in x.Rows()) abbrs.Add(x.Str(r, "artabbr"));
        var byAbbr = new Dictionary<string, List<LocRow>>(StringComparer.Ordinal);
        foreach (var row in LocDb.ReadAll(loc))
        {
            var fam = LocFamilies.FirstOrDefault(f => row.Id.StartsWith(f, StringComparison.Ordinal));
            if (fam == null) continue;
            string abbr = row.Id[fam.Length..];
            if (abbr.Length == 0 || abbr != abbr.Trim() || abbrs.Contains(abbr) || abbr.StartsWith("NC", StringComparison.Ordinal) ||
                abbr.StartsWith("ABBR3_", StringComparison.OrdinalIgnoreCase) || abbr.All(char.IsDigit)) continue;
            if (!byAbbr.TryGetValue(abbr, out var l)) byAbbr[abbr] = l = new();
            l.Add(row);
        }
        var list = new List<(string Key, int Rank)>();
        foreach (var (abbr, rows) in byAbbr)
        {
            int rank;
            if (System.Text.RegularExpressions.Regex.IsMatch(abbr, @"^\d[TF]\d+$")) rank = 1;
            else if (rows.Select(r => LocFamilies.First(f => r.Id.StartsWith(f, StringComparison.Ordinal))).Distinct().Count() >= 5 &&
                     !rows.Any(r => r.Text != null && Special.IsMatch(r.Text))) rank = 2;
            else continue;
            list.AddRange(rows.Select(r => (r.Id, rank)));
        }
        return list.OrderBy(x => x.Rank).ThenBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key).ToList();
    }
}
