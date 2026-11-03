namespace NHL27Kit.Core;

/// <summary>A team of the roster (exhibitionteams), by its index.</summary>
public sealed record RosterTeam(int Index, string Name, string Abbr, int PrimaryColor);

/// <summary>
/// A player of NHL 27's roster: his bio row (exhibitionplayerbiotable), his ratings row (exhibitionskaterai or
/// exhibitiongoalieai, by game_id) and his roster row (exhibitionrostertable, through exhibitionplayers' index).
/// </summary>
public sealed class RosterPlayer
{
    public int GameId { get; init; }
    public int BioRow { get; init; }
    public T3Table RatingsTable { get; init; }
    public int RatingsRow { get; init; } = -1;
    public int RosterRow { get; init; } = -1;
    public string FirstName { get; init; }
    public string LastName { get; init; }
    public int Team { get; init; }
    public string TeamAbbr { get; init; }
    public int Position { get; init; }
    public int Overall { get; init; }
    public int Jersey { get; init; } = -1;
    public int ArtId { get; init; }
    public bool HasPortrait { get; init; }
    public bool IsGoalie => RatingsTable?.Name == Roster.GoalieTable;

    public string Name => $"{FirstName} {LastName}".Trim();
    public string PositionName => Roster.PositionName(Position);
    /// <summary>The player's portrait texture (ui/textures/common/playerportraits/p + art id).</summary>
    public string Portrait => $"ui/textures/common/playerportraits/p{ArtId}";
}

/// <summary>The roster tables of nhl_game.db, joined. Built from a database read just now from the game.</summary>
public sealed class Roster
{
    public const string Database = "gamemodes/database/t3db/nhl_game.db";
    public const string BioTable = "exhibitionplayerbiotable";
    public const string SkaterTable = "exhibitionskaterai";
    public const string GoalieTable = "exhibitiongoalieai";
    public const string RosterTable = "exhibitionrostertable";
    public const string PlayersTable = "exhibitionplayers";
    public const string TeamsTable = "exhibitionteams";

    public T3Db Db { get; }
    public List<RosterPlayer> Players { get; } = new();
    public Dictionary<int, RosterTeam> Teams { get; } = new();

    public static string PositionName(int p) => p switch { 0 => "C", 1 => "LW", 2 => "RW", 3 => "D", 4 => "G", _ => "?" };

    public Roster(T3Db db)
    {
        Db = db;
        var teams = db.Table(TeamsTable);
        foreach (int r in Rows(teams))
        {
            int idx = Int(teams, r, "index");
            Teams[idx] = new RosterTeam(idx, Str(teams, r, "teamname"), Str(teams, r, "abbrname"), Int(teams, r, "presprimarycolor"));
        }

        var ratings = new Dictionary<int, (T3Table, int)>();
        foreach (var name in new[] { SkaterTable, GoalieTable })
        {
            var t = db.Table(name);
            foreach (int r in Rows(t)) ratings[Int(t, r, "game_id")] = (t, r);
        }
        // roster row by player id: exhibitionrostertable.playerindex → exhibitionplayers.index → playerid
        var players = db.Table(PlayersTable);
        var idOfIndex = new Dictionary<int, int>();
        foreach (int r in Rows(players)) idOfIndex[Int(players, r, "index")] = Int(players, r, "playerid");
        var roster = db.Table(RosterTable);
        var rosterOf = new Dictionary<int, int>();
        foreach (int r in Rows(roster))
            if (idOfIndex.TryGetValue(Int(roster, r, "playerindex"), out int id) && !rosterOf.ContainsKey(id)) rosterOf[id] = r;

        var bio = db.Table(BioTable);
        foreach (int r in Rows(bio))
        {
            int id = Int(bio, r, "game_id");
            ratings.TryGetValue(id, out var rt);
            int team = Int(bio, r, "team");
            int rosterRow = rosterOf.GetValueOrDefault(id, -1);
            Players.Add(new RosterPlayer
            {
                GameId = id, BioRow = r,
                RatingsTable = rt.Item1, RatingsRow = rt.Item1 == null ? -1 : rt.Item2,
                RosterRow = rosterRow,
                FirstName = Str(bio, r, "firstname"), LastName = Str(bio, r, "lastname"),
                Team = team, TeamAbbr = Teams.TryGetValue(team, out var tm) ? tm.Abbr : "FA",
                Position = Int(bio, r, "position"),
                Overall = rt.Item1 == null ? 0 : Int(rt.Item1, rt.Item2, "skill_overall"),
                Jersey = rosterRow < 0 ? -1 : Int(roster, rosterRow, "jersey"),
                ArtId = Int(bio, r, "artid"), HasPortrait = Int(bio, r, "hasportrait") == 1,
            });
        }
    }

    private IEnumerable<int> Rows(T3Table t)
    {
        for (int r = 0; r < t.WrittenRecords; r++)
            if (!Db.IsInvalid(t, r)) yield return r;
    }

    public static T3Field Field(T3Table t, string name) => t.Fields.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private int Int(T3Table t, int r, string f) => Field(t, f) is { } x ? (int)Db.Get(t, r, x) : 0;
    private string Str(T3Table t, int r, string f) => Field(t, f) is { } x ? (string)Db.Get(t, r, x) : "";

    /// <summary>The player with this game id in a (fresh) database: the rows don't move, but they are looked up again.</summary>
    public RosterPlayer Find(int gameId) => Players.FirstOrDefault(p => p.GameId == gameId);
}
