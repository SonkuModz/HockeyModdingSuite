namespace NHL27Kit.Core;

/// <summary>How a team value is edited.</summary>
public enum TeamFieldKind { Text, Color, Number, Toggle, Team, Arena }

/// <summary>One editable value: a field of a table, shown under a section with a label.</summary>
public sealed record TeamField(string Section, string Table, string Field, string Label, TeamFieldKind Kind, string Hint = null);

/// <summary>A team row of exhibitionteams, for the team list.</summary>
/// <remarks>
/// The art numbers are not the team's index: <see cref="ArtId"/> numbers its logo textures and kit folders, and
/// <see cref="CenterLine"/> its centre-line texture. Several teams can share either. <see cref="CenterLogo"/> replaces the
/// art number for the centre-ice logo when <see cref="CenterLogoType"/> is 0 or more.
/// </remarks>
public sealed record TeamSummary(int Row, int Index, string Name, string Abbr, string City, int LeagueId, string League, bool Active, int ArenaId,
    int ArtId, int CenterLine, int CenterLogo, int CenterLogoType);

/// <summary>An arena row of exhibitionarena, for the arena picker.</summary>
public sealed record ArenaSummary(int Row, int Index, string Name, string City);

/// <summary>
/// The team editor's view of nhl_game.db: a team's identity and colours (exhibitionteams), its palette
/// (stockteamcolorstable, one row per colour id) and its home arena (exhibitionarena, the row whose index is the team's
/// arenaid; arenas can be shared, so an arena edit shows for every team that plays there).
/// </summary>
public static class TeamEditor
{
    public const string Database = "gamemodes/database/t3db/nhl_game.db";
    public const string Teams = "exhibitionteams", Arenas = "exhibitionarena", Palette = "stockteamcolorstable";

    /// <summary>The values the editor offers, in display order. Palette colours are added per team (see <see cref="PaletteRows"/>).</summary>
    public static readonly TeamField[] Fields =
    {
        new("Identity", Teams, "cityname", "City", TeamFieldKind.Text),
        new("Identity", Teams, "teamname", "Team name", TeamFieldKind.Text, "The full name menus show"),
        new("Identity", Teams, "nickname", "Nickname", TeamFieldKind.Text),
        new("Identity", Teams, "abbrname", "Abbreviation", TeamFieldKind.Text, "Scorebug and short labels"),

        new("Menu colours", Teams, "feprimarycolor", "Primary", TeamFieldKind.Color),
        new("Menu colours", Teams, "fesecondarycolor", "Secondary", TeamFieldKind.Color),
        new("Menu colours", Teams, "fetertiarycolor", "Tertiary", TeamFieldKind.Color),
        new("Broadcast colours", Teams, "presprimarycolor", "Primary", TeamFieldKind.Color, "Presentation: scorebug, lower thirds"),
        new("Broadcast colours", Teams, "pressecondarycolor", "Secondary", TeamFieldKind.Color),
        new("Broadcast colours", Teams, "prestertiarycolor", "Tertiary", TeamFieldKind.Color),
        new("Broadcast colours", Teams, "overlayprimarycolor", "Overlay primary", TeamFieldKind.Color),
        new("Broadcast colours", Teams, "overlaysecondarycolor", "Overlay secondary", TeamFieldKind.Color),
        new("Broadcast colours", Teams, "overlaytertiarycolor", "Overlay tertiary", TeamFieldKind.Color),
        new("Rink colours", Teams, "goalpostcolor", "Goal posts", TeamFieldKind.Color),
        new("Rink colours", Teams, "stanchioncolor", "Stanchions", TeamFieldKind.Color),
        new("Rink colours", Teams, "goalspotlightzone1", "Goal spotlight", TeamFieldKind.Color),
        new("Rink colours", Teams, "skaterspotlightzone1", "Skater spotlight", TeamFieldKind.Color),
        new("Rink colours", Teams, "forceteamcolors", "Force team colours", TeamFieldKind.Toggle),

        new("Home arena", Teams, "arenaid", "Arena", TeamFieldKind.Arena, "Which arena home games are played in"),
        new("Home arena", Arenas, "arenaname", "Arena name", TeamFieldKind.Text),
        new("Home arena", Arenas, "cityname", "Arena city", TeamFieldKind.Text),
        new("Home arena", Arenas, "maxattendance", "Capacity", TeamFieldKind.Number),
        new("Home arena", Arenas, "yearopened", "Year opened", TeamFieldKind.Number),
        new("Home arena", Arenas, "isoutdoor", "Outdoor", TeamFieldKind.Toggle),
        new("Home arena", Arenas, "crowdlightcolor", "Crowd light", TeamFieldKind.Color),

        new("More", Teams, "rivalteam", "Rival", TeamFieldKind.Team),
        new("More", Teams, "rivalteam2", "Second rival", TeamFieldKind.Team),
        new("More", Teams, "ahlaffiliate", "AHL affiliate", TeamFieldKind.Team),
        new("More", Teams, "avgattendance", "Average attendance", TeamFieldKind.Number, "Percent of capacity"),
    };

    public static List<TeamSummary> ListTeams(T3Db db)
    {
        var leagues = Leagues(db);
        var t = db.Table(Teams);
        var f = t.Fields.ToDictionary(x => x.Name);
        var list = new List<TeamSummary>();
        for (int r = 0; r < t.WrittenRecords; r++)
        {
            if (db.IsInvalid(t, r)) continue;
            int league = (int)db.Get(t, r, f["league"]);
            int index = (int)db.Get(t, r, f["index"]);
            // older games may not have these fields: their textures are taken to be numbered by team
            int Number(string field, int otherwise) => f.TryGetValue(field, out var x) ? (int)db.Get(t, r, x) : otherwise;
            list.Add(new TeamSummary(r, (int)db.Get(t, r, f["index"]), (string)db.Get(t, r, f["teamname"]), (string)db.Get(t, r, f["abbrname"]),
                (string)db.Get(t, r, f["cityname"]), league, leagues.GetValueOrDefault(league, $"League {league}"),
                (int)db.Get(t, r, f["active"]) == 1, (int)db.Get(t, r, f["arenaid"]),
                Number("artid", index), Number("centerline", index), Number("centericelogo", 0), Number("centericelogotype", -1)));
        }
        return list;
    }

    public static List<ArenaSummary> ListArenas(T3Db db)
    {
        var t = db.Table(Arenas);
        var f = t.Fields.ToDictionary(x => x.Name);
        var list = new List<ArenaSummary>();
        for (int r = 0; r < t.WrittenRecords; r++)
            if (!db.IsInvalid(t, r))
                list.Add(new ArenaSummary(r, (int)db.Get(t, r, f["index"]), (string)db.Get(t, r, f["arenaname"]), (string)db.Get(t, r, f["cityname"])));
        return list;
    }

    /// <summary>stockleagues' names by league id (TXT_LEAGUE_DEL shown as DEL).</summary>
    public static Dictionary<int, string> Leagues(T3Db db)
    {
        var t = db.Table("stockleagues");
        var f = t.Fields.ToDictionary(x => x.Name);
        var map = new Dictionary<int, string>();
        for (int r = 0; r < t.WrittenRecords; r++)
            if (!db.IsInvalid(t, r))
                map[(int)db.Get(t, r, f["leagueid"])] = ((string)db.Get(t, r, f["name"])).Replace("TXT_LEAGUE_", "");
        return map;
    }

    /// <summary>The row of <paramref name="table"/> a field of team <paramref name="team"/> lives in (-1 if none: no arena).</summary>
    public static int RowFor(T3Db db, TeamSummary team, string table, int? arenaId = null)
    {
        if (table == Teams) return team.Row;
        if (table != Arenas) return -1;
        int id = arenaId ?? (int)db.Get(db.Table(Teams), team.Row, db.Table(Teams).Fields.First(x => x.Name == "arenaid"));
        var t = db.Table(Arenas);
        var fi = t.Fields.First(x => x.Name == "index");
        for (int r = 0; r < t.WrittenRecords; r++)
            if (!db.IsInvalid(t, r) && (int)db.Get(t, r, fi) == id) return r;
        return -1;
    }

    /// <summary>The team's palette: (row, colour id, colour) of stockteamcolorstable, by colour id.</summary>
    public static List<(int Row, int ColorId, int Color)> PaletteRows(T3Db db, int teamIndex)
    {
        var t = db.Table(Palette);
        var f = t.Fields.ToDictionary(x => x.Name);
        var list = new List<(int, int, int)>();
        for (int r = 0; r < t.WrittenRecords; r++)
            if (!db.IsInvalid(t, r) && (int)db.Get(t, r, f["team"]) == teamIndex)
                list.Add((r, (int)db.Get(t, r, f["colorid"]), (int)db.Get(t, r, f["color"])));
        return list.OrderBy(x => x.Item2).ToList();
    }

    /// <summary>The teams whose home is arena <paramref name="arenaId"/>.</summary>
    public static List<TeamSummary> TeamsAt(IEnumerable<TeamSummary> teams, int arenaId) => teams.Where(t => t.ArenaId == arenaId).ToList();

    /// <summary>Bytes a fixed string field holds (its width less the terminating zero).</summary>
    public static int MaxBytes(T3Db db, string table, string field) =>
        db.Table(table).Fields.First(x => x.Name == field) is { Type: T3FieldType.String } f ? f.Depth / 8 - 1 : int.MaxValue;

    public static string Hex(int rgb) => $"#{rgb & 0xFFFFFF:X6}";

    public static int? ParseHex(string s)
    {
        s = (s ?? "").Trim().TrimStart('#');
        return s.Length == 6 && int.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out int v) ? v : null;
    }
}
