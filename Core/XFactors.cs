using System.Text.RegularExpressions;

namespace NHL27Kit.Core;

/// <summary>One X-Factor ability: its id (the xfactorNability value), name and one-line description.</summary>
public sealed record XFactorAbility(int Id, string Name, string Short)
{
    public bool IsGoalie => XFactors.IsGoalieAbility(Id);
    public override string ToString() => Name;
}

/// <summary>
/// X-Factors. A player's are three slots in his ratings row (exhibitionskaterai / exhibitiongoalieai): xfactorNability
/// (-1 for none) and xfactorNtier, 1 Specialist (gold), 2 All-Star (blue), 3 Elite (red). The ability ids are the game's
/// text ids (TXT_XFACTOR_N, TXT_XFACTOR_SHORT_DESC_N) and icon numbers: the icon of ability N at tier T is
/// ui/textures/common/icons/playerabilities/xfactors/{small|medium|large}/xfactorN_T (versions 4 and 5 are other styles
/// the roster doesn't use). Goalie abilities are 26-30.
/// </summary>
public static class XFactors
{
    public const int Slots = 3;
    public const int LastAbility = 31;

    public static bool IsGoalieAbility(int id) => id is >= 26 and <= 30;

    /// <summary>The icon texture of an ability at a tier (size: small 72, medium 128, large 256).</summary>
    public static string Icon(int ability, int tier, string size = "medium") =>
        $"ui/textures/common/icons/playerabilities/xfactors/{size}/xfactor{ability}_{Math.Clamp(tier, 1, 3)}";

    /// <summary>The tier's colour on the icons and in the game's menus.</summary>
    public static string TierColor(int tier) => tier switch { 3 => "red", 2 => "blue", _ => "gold" };

    /// <summary>The game's tier names (TXT_ABILITY_TIER_1..3), with English fallbacks.</summary>
    public static string[] TierNames(T3Db loc)
    {
        var s = Strings(loc, id => id.StartsWith("TXT_ABILITY_TIER_"));
        return new[] { s.GetValueOrDefault("TXT_ABILITY_TIER_1", "Specialist"), s.GetValueOrDefault("TXT_ABILITY_TIER_2", "All-Star"),
                       s.GetValueOrDefault("TXT_ABILITY_TIER_3", "Elite") };
    }

    /// <summary>Every ability with its name and description from a language database read from the game.</summary>
    public static List<XFactorAbility> Abilities(T3Db loc)
    {
        var s = Strings(loc, id => id.StartsWith("TXT_XFACTOR_"));
        var list = new List<XFactorAbility>();
        for (int i = 0; i <= LastAbility; i++)
        {
            string name = s.GetValueOrDefault($"TXT_XFACTOR_{i}");
            if (string.IsNullOrWhiteSpace(name)) continue;
            // the game writes most names in capitals: shown as words
            if (name == name.ToUpperInvariant()) name = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.ToLowerInvariant());
            name = Regex.Replace(name, @"'([A-Z])", m => "'" + m.Groups[1].Value.ToLowerInvariant());
            list.Add(new XFactorAbility(i, name, s.GetValueOrDefault($"TXT_XFACTOR_SHORT_DESC_{i}", "")));
        }
        return list;
    }

    private static Dictionary<string, string> Strings(T3Db loc, Func<string, bool> want)
    {
        var map = new Dictionary<string, string>();
        foreach (var t in loc.Tables)
        {
            var id = t.Fields.FirstOrDefault(f => f.Name == "stringid");
            var text = t.Fields.FirstOrDefault(f => f.Name == "sourcetext");
            if (id == null || text == null) continue;
            for (int r = 0; r < t.WrittenRecords; r++)
            {
                if (loc.IsInvalid(t, r)) continue;
                string k = (string)loc.Get(t, r, id);
                if (k != null && want(k)) map[k] = (string)loc.Get(t, r, text);
            }
        }
        return map;
    }
}
