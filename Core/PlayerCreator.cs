using System.Text;

namespace NHL27Kit.Core;

/// <summary>A player the creator added: his new ids, his team and the player whose ratings and gear he copied.</summary>
public sealed record CreatedPlayer(string Name, int GameId, int PlayerIndex, int Team, string TeamName, string Template, int TemplateGameId,
                                   int TemplateOverall, int ArtId, int SurnameId, int Country, int State, PlayerBio Bio);

/// <summary>What a create run did: who was created, who wasn't and why, and the summary line for the mod entry.</summary>
public sealed class CreateResult
{
    public List<CreatedPlayer> Created { get; } = new();
    public List<(string Name, string Reason)> Skipped { get; } = new();
    public string Summary { get; set; } = "";
    public HashSet<int> CreatedIds => Created.Select(c => c.GameId).ToHashSet();
}

/// <summary>
/// Creates the players a sync found missing from the game (SyncKind.Missing with a looked-up PlayerBio), as real
/// entries of nhl_game.db, so every later sync matches them like any other player (PlayerMatcher: name + birth date,
/// both written from the bio).
///
/// A player in NHL 27 is a row in each of: exhibitionplayerbiotable (identity, bio, contract, draft, face ids),
/// exhibitionskaterai or exhibitiongoalieai (ratings), exhibitionskaterequipment or exhibitiongoalieequipment (gear),
/// all keyed by game_id and kept sorted by it; exhibitionplayers (player index -> game_id, sorted by index); and a roster
/// row in exhibitionrostertable + customonlinerostertable by player index (made by RosterSync.Apply, as for a free agent
/// joining a team).
///
/// V1 choices:
/// - Ratings and gear are copied whole from a template: a player at the same level (NHL or AHL team), the same primary
///   position and shooting hand, close in age, with the overall nearest the median of those candidates (a typical player
///   of that profile, not a star).
/// - The bio starts as the template's row and everything known or player-specific is written over it: name, birth date
///   and city, country and province/state, height, weight, hand, positions, number, team, draft, an unsigned contract
///   (ContractSync writes his real one), zeroed career numbers, no Cups.
/// - The face is a generic head: head, hair, eyebrows, eye/skin colour and beard copied from one of the game's own
///   no-scan players (chosen by a hash of the name, so the same player always gets the same face), hasportrait 0.
///   artid/audioid get a fresh value (a later portrait would be p{artid}); surnameid (the commentator's name call) is
///   borrowed from a player with the same last name, else 0 (no name call).
/// - New game ids follow the highest real one (below <see cref="SpecialIdStart"/>, where the game's game-mode players,
///   archetypes and Create-a-Player slots live); new player indexes follow the highest real index.
/// </summary>
public static class PlayerCreator
{
    /// <summary>Game ids and player indexes from here up are the game's own special players (56500+ game modes, 56800+ archetypes, 57000+ Create-a-Player).</summary>
    public const int SpecialIdStart = 50000;
    public const string SkaterEquipmentTable = "exhibitionskaterequipment";
    public const string GoalieEquipmentTable = "exhibitiongoalieequipment";
    /// <summary>The longest first/last name the bio's 25-byte fields hold (UTF-8 bytes, one byte for the terminator).</summary>
    public const int NameBytes = 24;
    public const int BirthplaceBytes = 29;

    /// <summary>Why this Missing change can't be created, or null when it can.</summary>
    public static string Problem(SyncChange c)
    {
        if (c.Kind != SyncKind.Missing) return "not a missing player";
        var b = c.Bio;
        if (b == null) return "his bio couldn't be read";
        if (b.Born == null) return "his bio has no birth date, so later syncs couldn't match him";
        if (c.ToTeam < 0) return "his team isn't in the game";
        if (b.Primary < 0) return $"unknown position '{b.Position}'";
        if (string.IsNullOrWhiteSpace(b.First) || string.IsNullOrWhiteSpace(b.Last)) return "no name";
        if (Encoding.UTF8.GetByteCount(b.First) > NameBytes || Encoding.UTF8.GetByteCount(b.Last) > NameBytes)
            return $"his name is longer than the game's {NameBytes} characters";
        return null;
    }

    private sealed record Template(RosterPlayer Player, int Age, int Hand, int Level);

    /// <summary>
    /// Creates the given Missing players (each needs a Bio; see <see cref="Problem"/>), places each on his team through
    /// RosterSync.Apply, and returns what was done. Players already in the game (matched now by name + birth date) and
    /// duplicates in the list are skipped (the AHL row wins). <paramref name="cap"/>, when given, decides an AHL player's NHL club (proteam):
    /// the club CapWages lists him under, else none (an AHL-only deal); without it, the AHL team's NHL parent.
    /// </summary>
    public static CreateResult Create(T3Db db, IEnumerable<SyncChange> missing, IReadOnlyList<CapTeam> cap = null)
    {
        var result = new CreateResult();
        using var batch = db.Batch();
        var bio = db.Table(Roster.BioTable);
        var players = db.Table(Roster.PlayersTable);
        var skaterAi = db.Table(Roster.SkaterTable);
        var goalieAi = db.Table(Roster.GoalieTable);
        var skaterEq = db.Table(SkaterEquipmentTable);
        var goalieEq = db.Table(GoalieEquipmentTable);
        if (bio == null || players == null || skaterAi == null || goalieAi == null || skaterEq == null || goalieEq == null)
            throw new InvalidOperationException("This database doesn't have the player tables the creator needs.");
        var bf = bio.Fields.ToDictionary(x => x.Name);
        int BioInt(int row, string f) => bf.TryGetValue(f, out var x) ? (int)db.Get(bio, row, x) : 0;

        var roster = new Roster(db);
        var matcher = new PlayerMatcher(db, roster);
        var nhlByCode = RosterSync.TeamIndex(db);
        var nhlTeams = nhlByCode.Values.ToHashSet();
        var affiliates = RosterSync.AhlAffiliates(db);
        var parentOf = new Dictionary<int, int>();
        foreach (var (nhl, ahl) in affiliates)
            if (nhlTeams.Contains(nhl) && ahl >= 0) parentOf.TryAdd(ahl, nhl);
        int Level(int team) => nhlTeams.Contains(team) ? 0 : parentOf.ContainsKey(team) ? 1 : 2;
        string TeamName(int i) => roster.Teams.TryGetValue(i, out var tm) && !string.IsNullOrEmpty(tm.Name) ? tm.Name : $"team {i}";

        // ---- the pools the new rows draw from
        var today = DateTime.Today;
        var templates = new List<Template>();
        foreach (var p in roster.Players)
        {
            if (p.RatingsRow < 0 || p.RosterRow < 0 || p.GameId >= SpecialIdStart) continue;
            if (BioInt(p.BioRow, "gender") != 0 || BioInt(p.BioRow, "ismascot") != 0 || BioInt(p.BioRow, "islegend") != 0) continue;
            if (matcher.Born(p) is not { } born) continue;
            templates.Add(new Template(p, Age(born, today), BioInt(p.BioRow, "handedness"), Level(p.Team)));
        }
        // generic faces: the game's own players without a face scan (head = head texture, a stock head 100000+)
        var faces = roster.Players.Where(p => !p.HasPortrait && p.GameId < SpecialIdStart && BioInt(p.BioRow, "gender") == 0 &&
                                              BioInt(p.BioRow, "headid") >= 100000 && BioInt(p.BioRow, "headid") == BioInt(p.BioRow, "headtexture"))
                                  .OrderBy(p => p.GameId).ToList();
        var surnames = roster.Players.Where(p => BioInt(p.BioRow, "surnameid") > 0)
            .GroupBy(p => RosterSync.Norm(p.LastName))
            .ToDictionary(g => g.Key, g => g.GroupBy(p => BioInt(p.BioRow, "surnameid")).OrderByDescending(x => x.Count()).ThenBy(x => x.Key).First().Key);
        // where a city is, from the players already born there (for a country the source doesn't give)
        var cities = roster.Players.Where(p => p.GameId < SpecialIdStart)
            .Select(p => (City: RosterSync.Norm((string)db.Get(bio, p.BioRow, bf["birthplace"])), C: BioInt(p.BioRow, "intlcountry"), S: BioInt(p.BioRow, "state")))
            .Where(x => x.City.Length > 0).GroupBy(x => x.City)
            .ToDictionary(g => g.Key, g => g.GroupBy(x => (x.C, x.S)).OrderByDescending(x => x.Count()).First().Key);
        var capOwner = new Dictionary<string, int>();
        if (cap != null)
            foreach (var team in cap)
                if (nhlByCode.TryGetValue(team.Abbr, out int idx))
                    foreach (var cp in team.Players)
                        if (cp.Born is { } b) capOwner.TryAdd(Key(cp.First, cp.Last, b), idx);

        // ---- new ids
        int nextId = MaxBelow(db, bio, "game_id") + 1;
        int nextIndex = MaxBelow(db, players, "index") + 1;
        int nextArt = 1 + Math.Max(MaxOf(db, bio, "artid"), MaxOf(db, bio, "audioid"));
        int idCap = Math.Min(SpecialIdStart, 1 << 16);

        var eqRowOf = new Dictionary<(T3Table, int), int>();
        foreach (var t in new[] { skaterEq, goalieEq })
        {
            var g = t.Fields.First(f => f.Name == "game_id");
            for (int r = 0; r < t.WrittenRecords; r++)
                if (!db.IsInvalid(t, r)) eqRowOf.TryAdd((t, (int)db.Get(t, r, g)), r);
        }
        var rosterTables = RosterSync.RosterTables(db);
        var seen = new HashSet<string>();
        var proteamOf = new Dictionary<int, int>();
        var joins = new List<(SyncChange Join, CreatedPlayer Made)>();

        // A player missing from both an NHL and an AHL roster has two rows: the AHL one wins, as in RosterSync.CompareCombined
        // (the NHL API lists players assigned to the AHL), so he is created where he plays.
        foreach (var c in missing.OrderBy(c => c.IsAhl ? 0 : 1))
        {
            var b = c.Bio;
            string name = b?.Name ?? c.Name;
            if (Problem(c) is { } problem) { result.Skipped.Add((name, problem)); continue; }
            var born = b.Born.Value;
            if (!seen.Add(Key(b.First, b.Last, born))) { result.Skipped.Add((name, "listed twice")); continue; }
            var (already, nameOnly) = matcher.Find(b.First, b.Last, born);
            if (already != null && !nameOnly) { result.Skipped.Add((name, $"already in the game (game id {already.GameId})")); continue; }
            if (rosterTables.Any(x => !RosterSync.HasFreeSlot(db, x.Table, c.ToTeam))) { result.Skipped.Add((name, $"{TeamName(c.ToTeam)} already has 40 players")); continue; }
            bool goalie = b.Primary == 5;
            var ai = goalie ? goalieAi : skaterAi;
            var eq = goalie ? goalieEq : skaterEq;
            string full = new[] { bio, players, ai, eq }.FirstOrDefault(t => t.WrittenRecords >= t.Records)?.Name;
            if (full != null) { result.Skipped.Add((name, $"{full} has no room for another player")); continue; }
            if (nextId >= idCap) { result.Skipped.Add((name, "no free game id")); continue; }
            int hand = b.Shoots == "R" ? 1 : 0;
            uint seed = Hash(Key(b.First, b.Last, born));
            var template = PickTemplate(templates, goalie, b.Primary, hand, Age(born, today), Math.Min(Level(c.ToTeam), 1), seed);
            if (template == null) { result.Skipped.Add((name, "no similar player to copy ratings from")); continue; }
            var t0 = template.Player;
            if (!eqRowOf.TryGetValue((eq, t0.GameId), out int eqRow)) { result.Skipped.Add((name, $"{t0.Name} (template) has no gear row")); continue; }

            int gameId = nextId++, index = nextIndex++, art = nextArt++;
            // ratings and gear: the template's rows, under the new id
            SetInt(db, ai, Append(db, ai, db.RecordBytes(ai, t0.RatingsRow)), "game_id", gameId);
            SetInt(db, eq, Append(db, eq, db.RecordBytes(eq, eqRow)), "game_id", gameId);
            // players: index -> game id (table 0, as for every regular player)
            int pr = Append(db, players, db.RecordBytes(players, FirstValid(db, players)));
            SetInt(db, players, pr, "index", index);
            SetInt(db, players, pr, "playerid", gameId);
            SetInt(db, players, pr, "table", 0);

            // bio: the template's row, with everything known or his own written over it
            int row = Append(db, bio, db.RecordBytes(bio, t0.BioRow));
            void I(string f, int v) => SetInt(db, bio, row, f, v);
            void S(string f, string v, int max) { if (bf.TryGetValue(f, out var x)) db.Set(bio, row, x, Fit(v, max)); }
            I("game_id", gameId);
            S("firstname", b.First, NameBytes);
            S("lastname", b.Last, NameBytes);
            S("nickname", "", NameBytes);
            S("birthplace", CityCase(b.BirthCity), BirthplaceBytes);
            I("year", born.Year - 1900); I("month", born.Month - 1); I("day", born.Day - 1);
            if (b.HeightIn > 0) I("height", b.HeightIn - RosterSync.HeightOffset);
            if (b.WeightLb > 0) I("weight", b.WeightLb - RosterSync.WeightOffset);
            I("handedness", hand);
            I("position", Roster.CollapsedPosition(b.Primary));
            I("positionprimary", b.Primary);
            I("positionsecondary", b.Secondary);
            int number = b.Number is >= 0 and <= 99 ? b.Number : c.Api?.Number ?? c.Ahl?.Number ?? -1;
            if (number is >= 0 and <= 99) I("jersey", number);
            I("team", c.ToTeam);
            int proteam = Level(c.ToTeam) == 0 ? c.ToTeam
                : cap == null ? parentOf.GetValueOrDefault(c.ToTeam, -1)
                : capOwner.GetValueOrDefault(Key(b.First, b.Last, born), -1);
            I("proteam", proteam);
            proteamOf[gameId] = proteam;

            // where he's from
            var (country, state) = MissingPlayers.GameCountry(b.BirthCountry, b.BirthRegion);
            if (country < 0 && cities.TryGetValue(RosterSync.Norm(b.BirthCity), out var known)) (country, state) = known;
            else if (country is 0 or 14 && state == country && cities.TryGetValue(RosterSync.Norm(b.BirthCity), out var k2) && k2.C == country) state = k2.S;
            if (country < 0) (country, state) = (127, 127);
            I("intlcountry", country); I("state", state);

            // draft: an undrafted player has round 0, pick 0, no team, and the year he was first eligible
            var d = b.Draft ?? DraftInfo.Undrafted;
            if (!d.IsUndrafted)
            {
                I("draftyear", d.Year - 1900); I("draftround", d.Round); I("draftposition", d.Overall);
                I("draftteam", DraftTeam(nhlByCode, d.Team));
            }
            else
            {
                I("draftyear", born.Year + (born.Month > 9 || born.Month == 9 && born.Day > 15 ? 19 : 18) - 1900);
                I("draftround", 0); I("draftposition", 0); I("draftteam", -1);
            }
            I("draftleague", -1); I("isdraftable", 0); I("franchisedraftyear", -1); I("franchisestartage", -1);

            // unsigned until ContractSync writes his real deal; free agent status
            I("contractstatus", -1); I("contractlength", 0); I("contractdollars", 0); I("contractis2way", 0);
            I("extensionlength", 0); I("extensiondollars", 0); I("extensionis2way", 0); I("retainedsalarytrades", 0);
            for (int k = 0; k < ContractSync.ClauseSlots; k++) { I($"contractclause{k}", 0); I($"contractclausemntcnumteams{k}", 0); }

            // a new player: no NHL career in the game's numbers, no Cups
            foreach (var f in new[] { "progamesplayedcareer", "nhlcareergoalsregularseason", "nhlcareergamesplayedregularseason", "nhlgamesplayedcareer",
                                      "nhlgamesplayedlastseason", "nhlgamesplayedtwoseasonsago", "nhlgamesplayedthreeseasonsago",
                                      "nhlgamesmissedinjurylastseason", "cupswon" })
                I(f, 0);
            I("cupsyearlastwon", -1); I("yearprodebut", -1); I("yearfirstsigned", -1); I("yearwaivereligible", -1);
            I("yearufa", born.Year + (born.Month <= 6 ? 27 : 28) - 1900);
            I("rookie", 1); I("rookieahl", 1); I("rookiechl", 0);
            I("juniorleague", 0); I("juniorteam", -1); I("expansiondraftstatus", 0);

            // not the template's person: no links, personality, music or casual gear of his
            I("active", 1); I("gender", 0); I("ismascot", 0); I("islegend", 0); I("mascotbodyid", -1); I("mascotheadid", -1);
            I("casualgearfk", -1); I("casualgear", 0); I("friendenemy0", -1); I("friendenemycompat0", 0); I("playercomp", -1);
            I("contractevaltemplateid", -1); I("playerclassid", -1); I("personalitytype", -1); I("personalitysubtype", -1); I("introsongid", -1);

            // a generic face from one of the game's no-scan players, the same one every time for this player
            if (faces.Count > 0)
            {
                var face = faces[(int)(seed / 7 % (uint)faces.Count)];
                foreach (var f in new[] { "headid", "headtexture", "hairid", "haircolorcode", "eyebrowid", "eyebrowcolorcode", "eyecolorcode", "skincolor",
                                          "playoffbeardtype", "playoffbeardcolor", "playoffbeardgrowthrate", "playoffbeardstart", "playoffbeardmax" })
                    if (bf.ContainsKey(f)) I(f, BioInt(face.BioRow, f));
            }
            I("hasportrait", 0);
            I("artid", art); I("audioid", art);
            int surname = surnames.GetValueOrDefault(RosterSync.Norm(b.Last), 0);
            I("surnameid", surname);

            var made = new CreatedPlayer(b.Name, gameId, index, c.ToTeam, TeamName(c.ToTeam), t0.Name, t0.GameId, t0.Overall, art, surname, country, state, b);
            joins.Add((new SyncChange
            {
                Kind = SyncKind.Join, Api = c.Api, Ahl = c.Ahl, GameId = gameId, Name = b.Name, Position = c.Position,
                PlayerIndex = index, FromTeam = -1, FromTeamName = "Created", ToTeam = c.ToTeam, ToTeamName = TeamName(c.ToTeam),
                NewNumber = number, Selected = true,
            }, made));
            result.Created.Add(made);
        }

        if (result.Created.Count == 0)
        {
            result.Summary = result.Skipped.Count == 0 ? "" : $"No players created ({result.Skipped.Count} skipped)";
            return result;
        }

        // the game keeps these tables in id order: the new rows (appended) go to their place
        foreach (var t in new[] { bio, skaterAi, goalieAi, skaterEq, goalieEq }) SortBy(db, t, "game_id");
        SortBy(db, players, "index");

        // roster rows: the same path as a free agent joining a team (both roster tables, lines rebuilt)
        var fresh = new Roster(db);
        var placed = new List<SyncChange>();
        foreach (var (j, made) in joins)
        {
            var p = fresh.Find(made.GameId);
            placed.Add(new SyncChange
            {
                Kind = j.Kind, Api = j.Api, Ahl = j.Ahl, GameId = j.GameId, Name = j.Name, Position = j.Position, BioRow = p.BioRow,
                PlayerIndex = j.PlayerIndex, FromTeam = j.FromTeam, FromTeamName = j.FromTeamName, ToTeam = j.ToTeam, ToTeamName = j.ToTeamName,
                NewNumber = j.NewNumber, Selected = true,
            });
        }
        string rosterSummary = RosterSync.Apply(db, placed);
        // Apply sets proteam only for NHL teams; an AHL player's stays what was worked out above
        var after = new Roster(db);
        var pf = bf["proteam"];
        foreach (var (id, pt) in proteamOf)
            if (after.Find(id) is { } p && (int)db.Get(bio, p.BioRow, pf) != pt) SetInt(db, bio, p.BioRow, "proteam", pt);

        result.Summary = $"Created {result.Created.Count} player{(result.Created.Count == 1 ? "" : "s")}"
            + (result.Skipped.Count > 0 ? $" ({result.Skipped.Count} skipped)" : "") + $"; {rosterSummary}";
        return result;
    }

    /// <summary>The contract changes CapWages has for the created players (they start unsigned), to apply right after creating them.</summary>
    public static List<ContractChange> Contracts(T3Db db, IReadOnlyList<CapTeam> cap, ISet<int> created) =>
        cap == null || created.Count == 0 ? new List<ContractChange>()
            : ContractSync.Compare(db, cap).Where(c => c.Kind == ContractChangeKind.Contract && created.Contains(c.GameId) && !c.NameOnly).ToList();

    /// <summary>
    /// The template whose ratings and gear a new player copies: a player on a team of the same level (NHL 0 / AHL 1; a
    /// target team in another league takes AHL players),
    /// skater or goalie, primary position and hand, within a year of his age — widened step by step (age ±2, ±3, then
    /// any hand, then any age) until at least 5 candidates — and of those, one of the 5 whose overall is nearest their
    /// median, picked by the new player's hash (the same choice on every run).
    /// </summary>
    private static Template PickTemplate(List<Template> all, bool goalie, int primary, int hand, int age, int level, uint seed)
    {
        var pool = all.Where(t => t.Player.IsGoalie == goalie && t.Level == level).ToList();
        if (pool.Count == 0) pool = all.Where(t => t.Player.IsGoalie == goalie && Math.Min(t.Level, 1) == level).ToList();
        if (pool.Count == 0) pool = all.Where(t => t.Player.IsGoalie == goalie).ToList();
        var samePos = pool.Where(t => goalie || t.Player.PositionPrimary == primary).ToList();
        if (samePos.Count == 0) samePos = pool.Where(t => Roster.CollapsedPosition(t.Player.PositionPrimary) == Roster.CollapsedPosition(primary)).ToList();
        if (samePos.Count == 0) samePos = pool;
        List<Template> pick = null;
        foreach (bool byHand in new[] { true, false })
            foreach (int spread in new[] { 1, 2, 3, 100 })
            {
                var c = samePos.Where(t => (!byHand || t.Hand == hand) && Math.Abs(t.Age - age) <= spread).ToList();
                if (c.Count >= 5 || (spread == 100 && !byHand && c.Count > 0)) { pick = c; goto found; }
            }
        found:
        if (pick == null || pick.Count == 0) return null;
        var overalls = pick.Select(t => t.Player.Overall).OrderBy(x => x).ToList();
        int median = overalls[overalls.Count / 2];
        // the 5 nearest the median, one of them by the player's own hash: typical, without every new player cloning the same one
        var near = pick.OrderBy(t => Math.Abs(t.Player.Overall - median)).ThenBy(t => Math.Abs(t.Age - age)).ThenBy(t => t.Player.GameId).Take(5).ToList();
        return near[(int)(seed % (uint)near.Count)];
    }

    private static int DraftTeam(Dictionary<string, int> byCode, string abbr)
    {
        string a = (abbr ?? "").ToUpperInvariant() switch { "ARI" or "PHX" => "UTA", "ATL" => "WPG", "MDA" => "ANA", var x => x };
        return byCode.TryGetValue(a, out int i) ? i : -1;
    }

    private static int Age(DateTime born, DateTime on) => on.Year - born.Year - (on < born.AddYears(on.Year - born.Year) ? 1 : 0);

    private static string Key(string first, string last, DateTime born) => $"{RosterSync.Norm(first)} {RosterSync.Norm(last)} {born:yyyy-MM-dd}";

    /// <summary>FNV-1a: a hash that is the same on every run and machine (string.GetHashCode isn't).</summary>
    private static uint Hash(string s)
    {
        uint h = 2166136261;
        foreach (byte c in Encoding.UTF8.GetBytes(s)) { h ^= c; h *= 16777619; }
        return h;
    }

    /// <summary>"EDMONTON" -> "Edmonton" (some AHL pages write the city in capitals); any other spelling is kept as it is.</summary>
    private static string CityCase(string city)
    {
        city = (city ?? "").Trim();
        return city.Any(char.IsLower) || !city.Any(char.IsLetter) ? city
            : System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(city.ToLowerInvariant());
    }

    /// <summary>The text cut to at most <paramref name="max"/> UTF-8 bytes, never in the middle of a character.</summary>
    private static string Fit(string s, int max)
    {
        s ??= "";
        while (Encoding.UTF8.GetByteCount(s) > max) s = s[..^1];
        return s;
    }

    /// <summary>Sets an integer field when the table has it and the value is in its range (out-of-range values are left as they were).</summary>
    private static void SetInt(T3Db db, T3Table t, int row, string field, int v)
    {
        var f = t.Fields.FirstOrDefault(x => x.Name == field);
        if (f == null) return;
        long max = f.RangeLow + (1L << f.Depth) - 1;
        if (v >= f.RangeLow && v <= max) db.Set(t, row, f, v);
    }

    /// <summary>Adds a record at the end of the table's written records (within its room) and returns its row.</summary>
    private static int Append(T3Db db, T3Table t, byte[] bytes)
    {
        if (t.WrittenRecords >= t.Records) throw new InvalidOperationException($"{t.Name} has no room for another record.");
        int row = t.WrittenRecords;
        db.SetWritten(t, row + 1);
        db.WriteRecord(t, row, bytes);
        return row;
    }

    private static int FirstValid(T3Db db, T3Table t)
    {
        for (int r = 0; r < t.WrittenRecords; r++) if (!db.IsInvalid(t, r)) return r;
        throw new InvalidOperationException($"{t.Name} has no rows to copy.");
    }

    private static int MaxBelow(T3Db db, T3Table t, string field)
    {
        var f = t.Fields.First(x => x.Name == field);
        int max = -1;
        for (int r = 0; r < t.WrittenRecords; r++)
            if (!db.IsInvalid(t, r) && (int)db.Get(t, r, f) is var v && v < SpecialIdStart && v > max) max = v;
        return max;
    }

    private static int MaxOf(T3Db db, T3Table t, string field)
    {
        var f = t.Fields.FirstOrDefault(x => x.Name == field);
        if (f == null) return 0;
        int max = 0;
        for (int r = 0; r < t.WrittenRecords; r++)
            if (!db.IsInvalid(t, r)) max = Math.Max(max, (int)db.Get(t, r, f));
        return max;
    }

    /// <summary>Puts a table's written records in order of an integer field (invalid records last); nothing moves when already sorted.</summary>
    private static void SortBy(T3Db db, T3Table t, string field)
    {
        var f = t.Fields.First(x => x.Name == field);
        var order = Enumerable.Range(0, t.WrittenRecords).OrderBy(r => db.IsInvalid(t, r) ? int.MaxValue : (int)db.Get(t, r, f)).ThenBy(r => r).ToList();
        if (!order.Select((r, i) => r == i).All(x => x)) db.Reorder(t, order);
    }
}
