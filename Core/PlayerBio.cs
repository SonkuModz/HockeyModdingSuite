namespace NHL27Kit.Core;

/// <summary>
/// Everything about a player the game doesn't have yet that the create-player pipeline needs, from his own player
/// page: NHL roster players from the NHL API (/v1/player/{id}/landing), AHL roster players from the AHL feed's player
/// view (what theahl.com/stats/player/{id} shows). Height is in inches, weight in pounds. Shoots is the catching hand
/// for goalies. BirthCity is all the game's birthplace field holds ("Newmarket", not "Newmarket, ON, CAN").
/// Position is the source's own code (C/LW/RW/D/G, or the AHL's "F"); Primary and Secondary are what the game stores,
/// on its 0-5 scale (see Roster.PositionNameFull), set by MissingPlayers.BioAsync.
/// </summary>
public sealed record PlayerBio(string First, string Last, int Number, string Position, string Shoots, int HeightIn, int WeightLb,
                               DateTime? Born, string BirthCity, DraftInfo Draft, string Source)
{
    public string Name => $"{First} {Last}";

    /// <summary>The game's positionprimary (0=C, 1=LW, 2=RW, 3=LD, 4=RD, 5=G); -1 until mapped or when the source's position is unknown.</summary>
    public int Primary { get; init; } = -1;
    /// <summary>The game's positionsecondary on the same scale, or -1 for none.</summary>
    public int Secondary { get; init; } = -1;
    /// <summary>True when the AHL listed him only as "F", so the split into primary/secondary was worked out, not read (see MissingPlayers.GamePositions).</summary>
    public bool PositionFromF { get; init; }
    /// <summary>Birth country as a 3-letter code ("CAN", "SWE"), or "" when the source doesn't say.</summary>
    public string BirthCountry { get; init; } = "";
    /// <summary>Province or state code for players born in Canada or the USA ("ON", "MN"), or "".</summary>
    public string BirthRegion { get; init; } = "";

    /// <summary>"#81 C · 5'11" 183 lb · shoots R · born 2007-04-19 · birthplace Coquitlam · drafted 2025 PIT, round 1 (#11)".</summary>
    public string Text
    {
        get
        {
            var parts = new List<string>();
            string position = Primary >= 0 ? Roster.PositionDisplay(Primary, Secondary) : Position;
            parts.Add((Number >= 0 ? $"#{Number} " : "") + position + (PositionFromF ? " (AHL lists F)" : ""));
            if (HeightIn > 0 || WeightLb > 0)
                parts.Add(string.Join(" ", new[] { HeightIn > 0 ? $"{HeightIn / 12}'{HeightIn % 12}\"" : "", WeightLb > 0 ? $"{WeightLb} lb" : "" }.Where(s => s.Length > 0)));
            if (!string.IsNullOrEmpty(Shoots)) parts.Add((Position == "G" ? "catches " : "shoots ") + Shoots);
            if (Born is { } b) parts.Add($"born {b:yyyy-MM-dd}");
            if (!string.IsNullOrEmpty(BirthCity)) parts.Add($"birthplace {BirthCity}");
            if (Draft != null) parts.Add(Draft.Text);
            return string.Join("  ·  ", parts);
        }
    }
}

/// <summary>Looks up the full bio of the players a sync found missing from the game.</summary>
public static class MissingPlayers
{
    /// <summary>The player's bio from his own page (the NHL API for NHL roster players, the AHL feed for AHL ones), with
    /// the game's primary/secondary positions filled in.</summary>
    public static async Task<PlayerBio> BioAsync(SyncChange c, CancellationToken ct = default)
    {
        PlayerBio bio;
        if (c.Api != null) bio = await NhlApi.BioAsync(c.Api.Id, ct);
        else if (c.Ahl is { Id: > 0 } a) bio = await AhlApi.BioAsync(a.Id, ct);
        else return null;
        string nhlPosition = bio.Position == "F" ? await NhlPositionAsync(bio, ct) : null;
        var (primary, secondary) = GamePositions(bio.Position, bio.Shoots, nhlPosition);
        return bio with { Primary = primary, Secondary = secondary, PositionFromF = bio.Position == "F" };
    }

    /// <summary>
    /// The game's primary and secondary positions (0=C, 1=LW, 2=RW, 3=LD, 4=RD, 5=G; secondary -1 for none) for a
    /// source position. C/LW/RW/G map straight across; D becomes LD or RD from the shooting hand. The AHL's "F" means a
    /// forward who plays both centre and wing, so he gets both: the primary is the NHL's position for him when known
    /// (<paramref name="nhlPosition"/>), else C; the secondary is the other kind — the wing on his shooting side
    /// for a centre, C for a winger. (-1, -1) when the position is unknown.
    /// </summary>
    public static (int Primary, int Secondary) GamePositions(string position, string shoots, string nhlPosition = null)
    {
        int shotWing = shoots == "L" ? 1 : shoots == "R" ? 2 : -1;
        return position switch
        {
            "C" => (0, -1),
            "LW" or "L" => (1, -1),
            "RW" or "R" => (2, -1),
            "D" => (shoots == "R" ? 4 : 3, -1),
            "G" => (5, -1),
            "F" => nhlPosition switch
            {
                "LW" => (1, 0),
                "RW" => (2, 0),
                _ => (0, shotWing),
            },
            _ => (-1, -1),
        };
    }

    /// <summary>
    /// The position the NHL lists for an AHL player the AHL calls just "F": the NHL's player search by name, then each
    /// same-name candidate's landing page, taken only when the birth date matches too (two players can share a name).
    /// Null when he isn't in the NHL's records or nothing matches.
    /// </summary>
    private static async Task<string> NhlPositionAsync(PlayerBio bio, CancellationToken ct)
    {
        if (bio.Born == null) return null;
        foreach (int id in await NhlApi.SearchAsync(bio.Name, ct))
        {
            var nhl = await NhlApi.BioAsync(id, ct);
            if (nhl.Born == bio.Born && nhl.Position is "C" or "LW" or "RW") return nhl.Position;
        }
        return null;
    }

    /// <summary>The city from the AHL's one-line birthplace: "Rosemount, MN" -> "Rosemount", "Minsk, Belarus" -> "Minsk".</summary>
    public static string BirthCity(string place) => (place ?? "").Split(',')[0].Trim();

    private static readonly string[] Provinces = { "AB", "BC", "MB", "NB", "NL", "NT", "NS", "NU", "ON", "PE", "QC", "SK", "YT" };
    private static readonly string[] States =
    {
        "AL", "AK", "AZ", "AR", "CA", "CO", "CT", "DE", "DC", "FL", "GA", "HI", "ID", "IL", "IN", "IA", "KS", "KY", "LA", "ME", "MD",
        "MA", "MI", "MN", "MS", "MO", "MT", "NE", "NV", "NH", "NJ", "NM", "NY", "NC", "ND", "OH", "OK", "OR", "PA", "RI", "SC", "SD",
        "TN", "TX", "UT", "VT", "VA", "WA", "WV", "WI", "WY",
    };

    // the same order as Provinces and States: the full names the NHL API sometimes gives instead of the code
    private static readonly string[] ProvinceNames =
    {
        "Alberta", "British Columbia", "Manitoba", "New Brunswick", "Newfoundland and Labrador", "Northwest Territories", "Nova Scotia",
        "Nunavut", "Ontario", "Prince Edward Island", "Quebec", "Saskatchewan", "Yukon",
    };
    private static readonly string[] StateNames =
    {
        "Alabama", "Alaska", "Arizona", "Arkansas", "California", "Colorado", "Connecticut", "Delaware", "District of Columbia", "Florida",
        "Georgia", "Hawaii", "Idaho", "Illinois", "Indiana", "Iowa", "Kansas", "Kentucky", "Louisiana", "Maine", "Maryland",
        "Massachusetts", "Michigan", "Minnesota", "Mississippi", "Missouri", "Montana", "Nebraska", "Nevada", "New Hampshire",
        "New Jersey", "New Mexico", "New York", "North Carolina", "North Dakota", "Ohio", "Oklahoma", "Oregon", "Pennsylvania",
        "Rhode Island", "South Carolina", "South Dakota", "Tennessee", "Texas", "Utah", "Vermont", "Virginia", "Washington",
        "West Virginia", "Wisconsin", "Wyoming",
    };

    /// <summary>Where a province/state code or full name ("ON", "Ontario", "Québec", "Newfoundland") is in the list; -1 if it isn't.</summary>
    private static int RegionIndex(string[] codes, string[] names, string region)
    {
        string r = RosterSync.Norm(region);
        if (r.Length == 0) return -1;
        for (int i = 0; i < codes.Length; i++)
            if (r == codes[i].ToLowerInvariant() || r == RosterSync.Norm(names[i]) || RosterSync.Norm(names[i]).StartsWith(r) && r.Length >= 4) return i;
        return -1;
    }

    private static readonly Dictionary<string, string> CountryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Canada"] = "CAN", ["USA"] = "USA", ["United States"] = "USA", ["Sweden"] = "SWE", ["Finland"] = "FIN", ["Russia"] = "RUS",
        ["Czechia"] = "CZE", ["Czech Republic"] = "CZE", ["Slovakia"] = "SVK", ["Germany"] = "DEU", ["Switzerland"] = "CHE",
        ["Austria"] = "AUT", ["Denmark"] = "DNK", ["Norway"] = "NOR", ["Latvia"] = "LVA", ["Belarus"] = "BLR", ["France"] = "FRA",
        ["Estonia"] = "EST", ["Kazakhstan"] = "KAZ", ["Lithuania"] = "LTU", ["Ukraine"] = "UKR", ["England"] = "GBR",
        ["Scotland"] = "GBR", ["United Kingdom"] = "GBR", ["Great Britain"] = "GBR", ["Hungary"] = "HUN", ["Italy"] = "ITA",
        ["Netherlands"] = "NLD", ["Poland"] = "POL", ["Slovenia"] = "SVN", ["China"] = "CHN", ["Japan"] = "JPN",
        ["South Korea"] = "KOR", ["Korea"] = "KOR",
    };

    /// <summary>
    /// The country and province/state of an AHL birthplace line: "Rosemount, MN" -> ("USA", "MN"), "Calgary, AB" ->
    /// ("CAN", "AB"), "Minsk, Belarus" -> ("BLR", ""). ("", "") when the last part is none of those.
    /// </summary>
    public static (string Country, string Region) BirthCountry(string place)
    {
        var parts = (place ?? "").Split(',');
        if (parts.Length < 2) return ("", "");
        string last = parts[^1].Trim();
        if (Provinces.Contains(last, StringComparer.OrdinalIgnoreCase)) return ("CAN", last.ToUpperInvariant());
        if (States.Contains(last, StringComparer.OrdinalIgnoreCase)) return ("USA", last.ToUpperInvariant());
        if (CountryNames.TryGetValue(last, out var iso)) return (iso, "");
        return last.Length == 3 && last.All(char.IsLetter) ? (last.ToUpperInvariant(), "") : ("", "");
    }

    /// <summary>
    /// The game's intlcountry and state values for a birth country and province/state. Both fields use one list:
    /// Canada 0, its provinces and territories 1-13 (alphabetical by name), USA 14, its states 15-65 (alphabetical by
    /// name, DC included), then other countries (Sweden 69, Finland 67, …), 127 for any other. A player born outside
    /// North America has state = intlcountry. Decoded from 757 NHL players matched against the NHL API.
    /// (-1, -1) when the country is unknown.
    /// </summary>
    public static (int Country, int State) GameCountry(string country, string region)
    {
        switch ((country ?? "").ToUpperInvariant())
        {
            case "CAN":
            {
                int i = RegionIndex(Provinces, ProvinceNames, region);
                return (0, i >= 0 ? i + 1 : 0);
            }
            case "USA":
            {
                int i = RegionIndex(States, StateNames, region);
                return (14, i >= 0 ? i + 15 : 14);
            }
            case "": return (-1, -1);
        }
        int v = (country ?? "").ToUpperInvariant() switch
        {
            "DNK" => 66, "FIN" => 67, "NOR" => 68, "SWE" => 69, "BLR" => 70, "EST" => 71, "KAZ" => 72, "LVA" => 74, "LTU" => 75,
            "RUS" => 76, "UKR" => 77, "AUT" => 78, "CZE" => 82, "GBR" => 83, "FRA" => 84, "DEU" => 85, "HUN" => 86, "ITA" => 87,
            "NLD" => 89, "POL" => 91, "SVK" => 95, "SVN" => 96, "CHE" => 97, "CHN" => 103, "JPN" => 109, "KOR" => 118,
            _ => 127,
        };
        return (v, v);
    }
}
