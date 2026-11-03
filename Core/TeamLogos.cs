using System.Text.RegularExpressions;

namespace NHL27Kit.Core;

/// <summary>One logo texture of a team: the style it is drawn in (a folder of ui/textures/common/teamlogos, or centre ice).</summary>
public sealed record TeamLogo(string Name, string Style, string Label, string Group, bool Main);

/// <summary>
/// A team's logos. The UI keeps one texture per team in each style folder, named t&lt;team index&gt;
/// (ui/textures/common/teamlogos/xl/t27 is Toronto's menu logo); the rink's centre-ice logo is
/// content/worlds/rink/textures/centerlogo/ice_centerlogo_&lt;index&gt;_color (exhibitionteams' arenaid equals the team index),
/// and its red line …/centerline/ice_centerline_&lt;index&gt;_color (fewer teams have one).
/// </summary>
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

    /// <summary>Every team's logos, by team index (one pass over the index's texture names).</summary>
    public static Dictionary<int, List<TeamLogo>> All(AssetIndex index)
    {
        var map = new Dictionary<int, List<TeamLogo>>();
        foreach (string name in index.Res.Keys)
        {
            TeamLogo logo;
            int team;
            if (name.StartsWith(UiRoot, StringComparison.OrdinalIgnoreCase) && UiName.Match(name) is { Success: true } m)
            {
                team = int.Parse(m.Groups[2].Value);
                string style = m.Groups[1].Value.ToLowerInvariant();
                var (label, group) = Known.TryGetValue(style, out var k) ? k : (style, "Other");
                logo = new TeamLogo(name, style, label, group, Array.IndexOf(MainStyles, style) >= 0);
            }
            else if (name.StartsWith(CenterIce, StringComparison.OrdinalIgnoreCase) && IceName.Match(name) is { Success: true } c)
            {
                team = int.Parse(c.Groups[1].Value);
                logo = new TeamLogo(name, "centerice", "Centre ice", "Rink", false);
            }
            else if (name.StartsWith(CenterLine, StringComparison.OrdinalIgnoreCase) && LineName.Match(name) is { Success: true } l)
            {
                // the red line across centre ice (64×4096), numbered like the centre logos
                team = int.Parse(l.Groups[1].Value);
                logo = new TeamLogo(name, "centerline", "Centre line", "Rink", false);
            }
            else continue;
            if (!map.TryGetValue(team, out var list)) map[team] = list = new List<TeamLogo>();
            list.Add(logo);
        }
        foreach (var list in map.Values)
            list.Sort((a, b) =>
            {
                int g = Array.IndexOf(GroupOrder, a.Group).CompareTo(Array.IndexOf(GroupOrder, b.Group));
                if (g != 0) return g;
                int ka = Order(a.Style), kb = Order(b.Style);
                return ka != kb ? ka.CompareTo(kb) : string.Compare(a.Style, b.Style, StringComparison.Ordinal);
            });
        return map;
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
