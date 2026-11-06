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
}
