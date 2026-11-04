using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace NHL27Kit.Core;

public enum Scorebug24Kind { Number, Colour, Flag, Text }

/// <summary>One setting of an NHL 24 score clock: its key, kind and the movie's own default.</summary>
public sealed record Scorebug24Setting(string Key, Scorebug24Kind Kind, string Default)
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.Ordinal)
    {
        ["x"] = "Left edge (px)", ["y"] = "Top edge (px)", ["scale"] = "Scale", ["rowH"] = "Row height", ["leagueW"] = "League box width",
        ["clockW"] = "Clock box width", ["teamW"] = "Team row width", ["shotsW"] = "Shots box width", ["barH"] = "Power play bar height",
        ["logoSize"] = "Team logo size", ["scoreInset"] = "Score from the right", ["gradA"] = "Row gradient start (0-255)",
        ["gradB"] = "Row gradient end (0-255)", ["ppLogoSize"] = "Power play bar logo size", ["ppLogoAlpha"] = "Power play bar logo opacity",
        ["goalW"] = "Goal banner width", ["goalPanelW"] = "Goal banner logo panel", ["goalLogo"] = "Goal banner logo size",
        ["goalLetterX"] = "Goal letters start", ["goalLetterStep"] = "Goal letter spacing",
        ["cLeague"] = "League box", ["cClock"] = "Clock box", ["cClockText"] = "Clock text", ["cDivider"] = "Clock divider",
        ["cBar"] = "Power play bar", ["cHighlight"] = "Power play highlight", ["cText"] = "Text", ["cRowLine"] = "Line between rows",
        ["cGoalText"] = "Goal banner text", ["cSide"] = "Side panel",
        ["sPeriod"] = "Period", ["sPeriodSuffix"] = "Period suffix (ST, ND)", ["sClock"] = "Clock", ["sAbbr"] = "Team abbreviation",
        ["sScore"] = "Score", ["sBar"] = "Power play bar", ["sSide"] = "Side panel", ["sShots"] = "Shots", ["sShotsLabel"] = "Shots label",
        ["sGoal"] = "Goal banner letters",
        ["rowGradient"] = "Team rows fade to the second colour", ["darkenLight"] = "Darken light team colours",
        ["showShield"] = "NHL shield in the league box", ["showEspn"] = "Network logo in the league box", ["showRowLine"] = "Line between the team rows",
        ["ppTeamColour"] = "Power play bar in the team's colour", ["ppLogo"] = "Team logo on the power play bar",
        ["ppHighlightName"] = "Highlight the power play team's name", ["goalBanner"] = "Full-width goal banner",
        ["lPowerPlay"] = "Power play", ["lGoal"] = "Goal", ["lShootout"] = "Shootout",
        ["teamColors"] = "Team colours (ABBR:RRGGBB:RRGGBB|…)",
        ["goalMinHold"] = "Goal banner, shortest (s)", ["goalMaxHold"] = "Goal banner, longest (s)", ["sideHold"] = "Side panel (s)", ["shotsHold"] = "Shots box (s)",
    };

    /// <summary>A plain name for the known keys; else "goalMaxHold" → "Goal max hold" (colours lose their "c" prefix).</summary>
    public string Label
    {
        get
        {
            if (Names.TryGetValue(Key, out var name)) return Regex.IsMatch(Key, "^s[A-Z]") ? name + " text" : name;
            string k = Kind == Scorebug24Kind.Colour && Key.Length > 1 && Key[0] == 'c' && char.IsUpper(Key[1]) ? Key[1..] : Key;
            k = k.Length > 1 && k[0] == 'l' && char.IsUpper(k[1]) ? k[1..] + " label" : k.Length > 1 && k[0] == 's' && char.IsUpper(k[1]) ? k[1..] + " text size" : k;
            var words = Regex.Replace(k, "(?<=[a-z0-9])(?=[A-Z])", " ").ToLowerInvariant();
            return char.ToUpperInvariant(words[0]) + words[1..];
        }
    }

    /// <summary>The section it shows in.</summary>
    public string Group => Kind switch
    {
        Scorebug24Kind.Colour => "Colours",
        Scorebug24Kind.Flag => "Show and hide",
        Scorebug24Kind.Text => "Labels",
        _ when Regex.IsMatch(Key, "^s[A-Z]") => "Text sizes",
        _ when Key.Contains("Hold", StringComparison.Ordinal) => "Timing",
        _ => "Position and size",
    };
}

/// <summary>
/// NHL 24's score clock (nhl/ui/swf_assets/kettle/overlays/scoreclock/scoreclock.gfx, and its twin score_clock.gfx): a
/// Scaleform movie ("CFX": a zlib SWF). A score clock made with NHL24Kit (ESPN+, Prime) reads every setting at start
/// from one 4096-character string constant that begins "#NHL24KIT_CFG#" ("key=value;key=value"), so its settings
/// change by rewriting that string, without recompiling the movie. The keys and their defaults are read from the
/// movie's own loadConfig (decompiled with FFDec: cfgNum / cfgFlag / cfgStr calls). The game's own score clock has no
/// such string; it can't be edited this way.
/// </summary>
public sealed class Scorebug24
{
    public const string File = "nhl/ui/swf_assets/kettle/overlays/scoreclock/scoreclock.gfx";
    public const string Twin = "nhl/ui/swf_assets/kettle/overlays/scoreclock/score_clock.gfx";
    public const string Marker = "#NHL24KIT_CFG#";
    public const int BlockLength = 4096;

    public string Collector { get; private init; }
    public List<Scorebug24Setting> Settings { get; } = new();
    /// <summary>The values the movie's settings string sets now (keys it doesn't set use the defaults).</summary>
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);
    /// <summary>The movie's name for itself, from its art (ESPN+, Prime Video…), or null.</summary>
    public string Style { get; private set; }

    public string Value(Scorebug24Setting s)
    {
        string v = Values.TryGetValue(s.Key, out var x) && x != "" ? x : s.Default;
        if (s.Kind == Scorebug24Kind.Colour && v != s.Default)
            try { v = "#" + ParseColour(v).ToString("X6"); } catch (FormatException) { }
        if (s.Kind == Scorebug24Kind.Flag && v is "1" or "0") v = v == "1" ? "true" : "false";
        return v;
    }

    public static Scorebug24 Load(AssetIndex index)
    {
        string collector = LegacyEditor.CollectorOf(index, File) ?? throw new FileNotFoundException($"The game has no {File}.");
        var gfx = new Legacy(index, collector).Read(File);
        var swf = Inflate(gfx);
        int at = FindBlock(swf) ?? throw new NotSupportedException("This is NHL 24's own score clock: it has no settings block. HMS edits score clocks made with NHL24Kit (ESPN+, Prime Video).");
        var sb = new Scorebug24 { Collector = collector };
        string text = Encoding.ASCII.GetString(swf, at + Marker.Length, BlockLength - Marker.Length);
        foreach (var part in text.Split(';'))
        {
            int eq = part.IndexOf('=');
            if (eq > 0) sb.Values[part[..eq].Trim()] = part[(eq + 1)..].Trim();
        }
        sb.ReadSchema(gfx);
        return sb;
    }

    /// <summary>The keys and defaults, from the decompiled ScoreClock class.</summary>
    private void ReadSchema(byte[] gfx)
    {
        string dir = Path.Combine(Path.GetTempPath(), "hms-scorebug24-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            string input = Path.Combine(dir, "scoreclock.gfx");
            System.IO.File.WriteAllBytes(input, gfx);
            Scorebug.Run("-selectclass", "ScoreClock", "-export", "script", Path.Combine(dir, "out"), input);
            string src = System.IO.File.ReadAllText(Directory.EnumerateFiles(Path.Combine(dir, "out"), "ScoreClock.as", SearchOption.AllDirectories).First());
            foreach (Match m in Regex.Matches(src, @"cfg(Num|Flag|Str)\(c,""(\w+)"",(""[^""]*""|[^)]*)\)"))
            {
                string key = m.Groups[2].Value, d = m.Groups[3].Value.Trim().Trim('"');
                if (Settings.Any(s => s.Key == key)) continue;
                var kind = m.Groups[1].Value switch
                {
                    "Flag" => Scorebug24Kind.Flag,
                    "Str" => Scorebug24Kind.Text,
                    _ => Regex.IsMatch(key, "^c[A-Z]") ? Scorebug24Kind.Colour : Scorebug24Kind.Number,
                };
                if (kind == Scorebug24Kind.Colour && double.TryParse(d, NumberStyles.Float, CultureInfo.InvariantCulture, out double c)) d = "#" + ((uint)c).ToString("X6");
                Settings.Add(new Scorebug24Setting(key, kind, d));
            }
            if (src.Contains("ESPN_PLUS", StringComparison.Ordinal)) Style = "ESPN+";
            else if (src.Contains("PRIME", StringComparison.OrdinalIgnoreCase)) Style = "Prime Video";
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
        if (Settings.Count == 0) throw new InvalidDataException("The score clock's settings block was found, but not the code that reads it.");
    }

    /// <summary>The settings string for these values (keys at their default are left out).</summary>
    public string Build(IReadOnlyDictionary<string, string> values)
    {
        var parts = new List<string>();
        foreach (var s in Settings)
        {
            if (!values.TryGetValue(s.Key, out var v)) v = Value(s);
            if (v == null || v == s.Default) continue;
            if (v.Contains(';') || v.Contains('=')) throw new ArgumentException($"{s.Label} can't contain ';' or '='.");
            if (s.Kind == Scorebug24Kind.Colour) v = ParseColour(v).ToString(CultureInfo.InvariantCulture);
            if (s.Kind == Scorebug24Kind.Flag) v = v is "1" or "true" or "True" ? "1" : "0";
            parts.Add($"{s.Key}={v}");
        }
        // keys the movie reads that aren't in its schema stay as they were (teamColors is one)
        foreach (var (k, v) in Values.Where(kv => Settings.All(s => s.Key != kv.Key)))
            parts.Add($"{k}={v}");
        string text = Marker + string.Join(";", parts);
        if (text.Length > BlockLength) throw new ArgumentException($"The settings take {text.Length} characters, more than the {BlockLength} the movie has room for.");
        if (text.Any(ch => ch > 126 || ch < 32)) throw new ArgumentException("Settings can only use plain ASCII characters.");
        return text.PadRight(BlockLength);
    }

    /// <summary>#RRGGBB, 0xRRGGBB or a decimal number.</summary>
    public static uint ParseColour(string s)
    {
        s = s.Trim();
        if (s.StartsWith('#')) return uint.Parse(s[1..], NumberStyles.HexNumber);
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return uint.Parse(s[2..], NumberStyles.HexNumber);
        return (uint)double.Parse(s, CultureInfo.InvariantCulture);
    }

    /// <summary>Writes the settings into both movies as one mod entry (Restore puts the movies back).</summary>
    public ModEntry Save(AssetIndex index, ModStore store, string game, IReadOnlyDictionary<string, string> values, Action<string> log = null)
    {
        string text = Build(values);
        var changed = Settings.Where(s => values.TryGetValue(s.Key, out var v) && v != Value(s)).Select(s => $"{s.Label} = {values[s.Key]}").ToList();
        var existing = store.Find(LegacyEditor.Kind, File);
        string summary = "Score clock" + (Style != null ? $" ({Style})" : "") + ": " + (changed.Count > 0 ? string.Join(", ", changed) : "settings");
        if (existing != null && existing.Summary?.StartsWith("Score clock") == true && changed.Count > 0) summary = existing.Summary + "; " + string.Join(", ", changed);
        if (summary.Length > 600) summary = "…" + summary[^599..];
        if (existing != null && store.State(existing, game) is ModState.Applied or ModState.Partial) store.Restore(existing, game, log);
        try
        {
            var legacy = new Legacy(index, Collector);
            var files = new List<(LegacyFile, byte[])>();
            foreach (var name in new[] { File, Twin })
            {
                var f = legacy.Find(name);
                if (f == null) continue;
                var gfx = legacy.Read(f);
                var swf = Inflate(gfx);
                int at = FindBlock(swf) ?? throw new InvalidDataException($"{name} has no settings block.");
                Encoding.ASCII.GetBytes(text).CopyTo(swf, at);
                files.Add((f, Deflate(gfx, swf)));
            }
            var notes = new List<string>();
            var patches = LegacyEditor.PlanPs4(index, legacy, files, notes, log);
            var entry = store.Add(LegacyEditor.Kind, File, "Scorebug page", summary, patches, notes);
            store.Apply(entry, game, log);
            Values.Clear();
            foreach (var part in text[Marker.Length..].Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq > 0) Values[part[..eq].Trim()] = part[(eq + 1)..].Trim();
            }
            return entry;
        }
        catch
        {
            if (existing != null && store.Entries.Contains(existing)) store.Apply(existing, game, log);
            throw;
        }
    }

    /// <summary>Where the settings string starts: the occurrence stored as a 4096-character constant (u30 length 0x80 0x20).</summary>
    private static int? FindBlock(byte[] swf)
    {
        var marker = Encoding.ASCII.GetBytes(Marker);
        for (int i = 2; i + BlockLength <= swf.Length; i++)
        {
            i = swf.AsSpan(i).IndexOf(marker) is int k and >= 0 ? i + k : -1;
            if (i < 0) return null;
            if (swf[i - 2] == 0x80 && swf[i - 1] == 0x20) return i;
        }
        return null;
    }

    /// <summary>The whole SWF uncompressed (header included), from CFX/CWS (zlib) or FWS/GFX.</summary>
    public static byte[] Inflate(byte[] gfx)
    {
        if (gfx[0] is (byte)'F' or (byte)'G') return (byte[])gfx.Clone();
        if (gfx[0] != 'C') throw new InvalidDataException("Not a Scaleform / SWF movie.");
        using var z = new ZLibStream(new MemoryStream(gfx, 8, gfx.Length - 8), CompressionMode.Decompress);
        using var ms = new MemoryStream();
        ms.Write(gfx, 0, 8);
        z.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>Compresses like the original (its signature and version kept; the length is the new one).</summary>
    public static byte[] Deflate(byte[] original, byte[] swf)
    {
        if (original[0] != 'C') return swf;
        using var ms = new MemoryStream();
        ms.Write(original, 0, 4);
        ms.Write(BitConverter.GetBytes((uint)swf.Length));
        using (var z = new ZLibStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
            z.Write(swf, 8, swf.Length - 8);
        return ms.ToArray();
    }
}
