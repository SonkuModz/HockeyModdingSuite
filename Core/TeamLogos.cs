using System.Text.RegularExpressions;

namespace NHL27Kit.Core;

/// <summary>One logo texture of a team: the style it is drawn in (a folder of ui/textures/common/teamlogos, or centre ice).</summary>
public sealed record TeamLogo(string Name, string Style, string Label, string Group, bool Main);

/// <summary>
/// Logo textures by the numbers the game gives them. None of these is the team's index (which only matches for the older
/// NHL teams), see <see cref="TeamSummary"/>:
/// - ui/textures/common/teamlogos/&lt;style&gt;/t&lt;artid&gt;: the menu logos (xl/t32 is Vegas, team 30);
/// - content/worlds/rink/textures/centerlogo/ice_centerlogo_&lt;artid&gt;_color: the centre logo, or the team's centericelogo
///   when its centericelogotype is 0 or more (a few special teams; many have no centre logo of their own);
/// - content/worlds/rink/textures/centerline/ice_centerline_&lt;centerline&gt;_color: the red line, often shared (181 teams use 76).
/// </summary>
public sealed class TeamLogoMap
{
    internal Dictionary<int, List<TeamLogo>> Menu { get; } = new();
    internal Dictionary<int, TeamLogo> Ice { get; } = new();
    internal Dictionary<int, TeamLogo> Lines { get; } = new();

    /// <summary>The number of the team's centre logo.</summary>
    public int CenterLogoNumber(TeamSummary team) => team.CenterLogoType >= 0 && Ice.ContainsKey(team.CenterLogo) ? team.CenterLogo : team.ArtId;

    /// <summary>A team's logos, in display order.</summary>
    public List<TeamLogo> For(TeamSummary team)
    {
        var list = Menu.TryGetValue(team.ArtId, out var menu) ? new List<TeamLogo>(menu) : new List<TeamLogo>();
        if (Ice.TryGetValue(CenterLogoNumber(team), out var ice)) list.Add(ice);
        if (Lines.TryGetValue(team.CenterLine, out var line)) list.Add(line);
        TeamLogos.Sort(list);
        return list;
    }
}

/// <summary>Finds the logo textures of the game's index.</summary>
public static class TeamLogos
{
    private const string UiRoot = "ui/textures/common/teamlogos/";
    private const string CenterIce = "content/worlds/rink/textures/centerlogo/ice_centerlogo_";
    private static readonly Regex UiName = new(@"^ui/textures/common/teamlogos/(.+)/t(\d+)$", RegexOptions.IgnoreCase);
    private static readonly Regex IceName = new(@"^content/worlds/rink/textures/centerlogo/ice_centerlogo_(\d+)_color$", RegexOptions.IgnoreCase);
    private const string CenterLine = "content/worlds/rink/textures/centerline/ice_centerline_";
    private static readonly Regex LineName = new(@"^content/worlds/rink/textures/centerline/ice_centerline_(\d+)_color$", RegexOptions.IgnoreCase);

    /// <summary>The plain logo styles, largest first: "Replace all sizes" writes into these (the treated styles keep theirs unless asked).</summary>
    public static readonly string[] MainStyles = { "xl", "large", "medium", "small", "extrasmall" };

    private static readonly Dictionary<string, (string Label, string Group)> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["xl"] = ("XL", "Logo"), ["large"] = ("Large", "Logo"), ["medium"] = ("Medium", "Logo"), ["small"] = ("Small", "Logo"),
        ["extrasmall"] = ("Extra small", "Logo"),
        ["xllightbg"] = ("XL, light background", "On backgrounds"), ["largelightbg"] = ("Large, light background", "On backgrounds"),
        ["mediumlightbg"] = ("Medium, light background", "On backgrounds"), ["smalllightbg"] = ("Small, light background", "On backgrounds"),
        ["extrasmalllightbg"] = ("Extra small, light background", "On backgrounds"),
        ["xlteamcolorbg"] = ("XL, team-colour background", "On backgrounds"), ["largeteamcolorbg"] = ("Large, team-colour background", "On backgrounds"),
        ["mediumteamcolorbg"] = ("Medium, team-colour background", "On backgrounds"), ["smallteamcolorbg"] = ("Small, team-colour background", "On backgrounds"),
        ["extrasmallteamcolorbg"] = ("Extra small, team-colour background", "On backgrounds"),
        ["xlstylized"] = ("XL stylized", "Stylized"), ["largestylized"] = ("Large stylized", "Stylized"),
        ["largestylized/left"] = ("Large stylized, left", "Stylized"), ["largestylized/right"] = ("Large stylized, right", "Stylized"),
        ["mediumstylized/left"] = ("Medium stylized, left", "Stylized"), ["mediumstylized/right"] = ("Medium stylized, right", "Stylized"),
        ["smallstylized/left"] = ("Small stylized, left", "Stylized"), ["smallstylized/right"] = ("Small stylized, right", "Stylized"),
        ["mediumglossy"] = ("Medium glossy", "Stylized"), ["smallglossy"] = ("Small glossy", "Stylized"), ["hypestage"] = ("Hype stage", "Stylized"),
        ["extralargemono"] = ("XL mono", "Mono"), ["largemono"] = ("Large mono", "Mono"), ["mediummono"] = ("Medium mono", "Mono"),
        ["extrasmallmono"] = ("Extra small mono", "Mono"),
        ["hutcardrare"] = ("HUT card, rare", "HUT cards"), ["hutcardbw"] = ("HUT card, black and white", "HUT cards"),
        ["hutcardmedium"] = ("HUT card, medium", "HUT cards"), ["hutcardsmalllogo"] = ("HUT card, small", "HUT cards"),
        ["hutcardcroppedteamlogo"] = ("HUT card, cropped", "HUT cards"), ["hutcardheritage"] = ("HUT card, heritage", "HUT cards"),
        ["hutcardplayoffs"] = ("HUT card, playoffs", "HUT cards"),
    };

    private static readonly string[] GroupOrder = { "Logo", "Rink", "On backgrounds", "Stylized", "Mono", "HUT cards", "Other" };

    /// <summary>Every logo texture, by its number (one pass over the index's texture names).</summary>
    public static TeamLogoMap All(AssetIndex index)
    {
        var map = new TeamLogoMap();
        foreach (string name in index.Res.Keys)
        {
            if (name.StartsWith(UiRoot, StringComparison.OrdinalIgnoreCase) && UiName.Match(name) is { Success: true } m)
            {
                int art = int.Parse(m.Groups[2].Value);
                string style = m.Groups[1].Value.ToLowerInvariant();
                var (label, group) = Known.TryGetValue(style, out var k) ? k : (style, "Other");
                if (!map.Menu.TryGetValue(art, out var list)) map.Menu[art] = list = new List<TeamLogo>();
                list.Add(new TeamLogo(name, style, label, group, Array.IndexOf(MainStyles, style) >= 0));
            }
            else if (name.StartsWith(CenterIce, StringComparison.OrdinalIgnoreCase) && IceName.Match(name) is { Success: true } c)
            {
                map.Ice[int.Parse(c.Groups[1].Value)] = new TeamLogo(name, "centerice", "Centre ice", "Rink", false);
            }
            else if (name.StartsWith(CenterLine, StringComparison.OrdinalIgnoreCase) && LineName.Match(name) is { Success: true } l)
            {
                // the red line across centre ice (64×4096)
                map.Lines[int.Parse(l.Groups[1].Value)] = new TeamLogo(name, "centerline", "Centre line", "Rink", false);
            }
        }
        return map;
    }

    internal static void Sort(List<TeamLogo> list) => list.Sort((a, b) =>
    {
        int g = Array.IndexOf(GroupOrder, a.Group).CompareTo(Array.IndexOf(GroupOrder, b.Group));
        if (g != 0) return g;
        int ka = Order(a.Style), kb = Order(b.Style);
        return ka != kb ? ka.CompareTo(kb) : string.Compare(a.Style, b.Style, StringComparison.Ordinal);
    });

    /// <summary>The other teams that draw the same logo art (often a team's alumni).</summary>
    public static List<TeamSummary> ArtSharers(IEnumerable<TeamSummary> teams, TeamSummary team) =>
        teams.Where(t => t.Row != team.Row && t.ArtId == team.ArtId).ToList();

    /// <summary>The other teams that draw the same centre line.</summary>
    public static List<TeamSummary> LineSharers(IEnumerable<TeamSummary> teams, TeamSummary team) =>
        teams.Where(t => t.Row != team.Row && t.CenterLine == team.CenterLine).ToList();

    /// <summary>"A, B, C and 4 more".</summary>
    public static string Names(IReadOnlyCollection<TeamSummary> teams, int show = 3)
    {
        var names = teams.Select(t => string.IsNullOrWhiteSpace(t.Name) ? $"team {t.Index}" : t.Name).Distinct().ToList();
        return names.Count <= show ? string.Join(", ", names) : $"{string.Join(", ", names.Take(show))} and {names.Count - show} more";
    }

    private static int Order(string style)
    {
        int i = Array.IndexOf(MainStyles, style);
        if (i >= 0) return i;
        int k = 0;
        foreach (var key in Known.Keys) { if (key == style) return 10 + k; k++; }
        return 1000;
    }
}
