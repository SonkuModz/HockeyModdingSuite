using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NHL27Kit.Core;

public enum ScorebugValueKind { Number, Color, Text, Bool, Expression }

/// <summary>
/// One value in a score clock style class: a constant (WIDGET_WIDTH) or a key of a props object (bgProps.color), with where
/// its text sits in the decompiled source. Only literals can be edited; an expression (this.WIDGET_HEIGHT / 2,
/// MasterStyle.COLOR_WHITE_0) is shown, and replacing it writes a literal.
/// </summary>
public sealed class ScorebugValue
{
    public string Class { get; init; }
    /// <summary>The props object (bgProps), or "const".</summary>
    public string Group { get; init; }
    public string Key { get; init; }
    public string Text { get; init; }
    public int Start { get; init; }
    public int Length { get; init; }
    public ScorebugValueKind Kind { get; init; }

    public string Id => $"{Class}.{Group}.{Key}";

    public double? Number => double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d
        : Text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && uint.TryParse(Text[2..], NumberStyles.HexNumber, null, out var h) ? h : null;

    /// <summary>The colour as 0xRRGGBB: the literal, or a stand-in for a MasterStyle colour (the game sets those at run time).</summary>
    public uint? Color => Kind != ScorebugValueKind.Color ? null : Number is { } n ? (uint)n : Resolve?.Invoke(Text) is { } r ? (uint)r : Scorebug.MasterColor(Text);

    /// <summary>Evaluates an expression of the class (this.WIDGET_HEIGHT / 2, this.GRAY_COLOR); set by the parser.</summary>
    public Func<string, double?> Resolve { get; set; }

    /// <summary>The value as a number: the literal, or the expression worked out from the class's constants.</summary>
    public double? Evaluate() => Number ?? Resolve?.Invoke(Text);

    /// <summary>The source text to write for <paramref name="value"/>.</summary>
    public string Literal(string value) => Kind switch
    {
        ScorebugValueKind.Text => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
        ScorebugValueKind.Color => "0x" + Scorebug.ParseColor(value).ToString("X6"),
        ScorebugValueKind.Bool => bool.Parse(value.Trim()) ? "true" : "false",
        _ => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>The value as an editor shows it: #RRGGBB for colours, the text without quotes, else the source.</summary>
    public string Display => Kind switch
    {
        ScorebugValueKind.Color => Color is { } c ? $"#{c:X6}" : Text,
        ScorebugValueKind.Text => Text.Trim('"'),
        _ => Text,
    };
}

/// <summary>A style class's decompiled source and its values.</summary>
public sealed class ScorebugClass
{
    public string Name { get; init; }
    public string File { get; init; }
    public string Source { get; init; }
    public List<ScorebugValue> Values { get; } = new();
    public string Short => Name[(Name.LastIndexOf('.') + 1)..];
}

/// <summary>
/// NHL 27's score clock (see docs/SCOREBUGS.md): the AS3 classes nhl.tin.widgets.scoreclock.* in
/// nhl/ui/lib/external/tin_nhl.swf, drawn in code from style classes. This reads the style values out of the game's SWF
/// (decompiled with FFDec), and writes edits back by recompiling the changed classes into the SWF and saving it through
/// <see cref="LegacyEditor"/>. One mod entry holds every edit; the game's SWF is read fresh every time.
/// </summary>
public sealed class Scorebug
{
    public const string File = "nhl/ui/lib/external/tin_nhl.swf";
    public const string Collector = "globals/chunkfilecollectoruicommon_nhluidata";
    public const string Package = "nhl.tin.widgets.scoreclock";
    public const string LogoWidget = Package + ".subwidgets.ScoreclockLogoSubwdgt";
    public const string LogoStyle = "ScoreclockLogoSubwdgtStyle";
    /// <summary>The UI's image names (alias → texture path and size), in the PS5 UI collector.</summary>
    public const string AssetPaths = "nhl/ui/definition.ps5.assetpaths";
    public const string AssetPathsCollector = "globals/chunkfilecollectoruips5_nhluidata";

    /// <summary>
    /// The sharper EA SPORTS badge: the match intro's spinning-badge sheet (1080x1080, 3x3 frames of 360x360), whose last
    /// frame is the score clock's badge at 360x360 instead of 64x64.
    /// </summary>
    public const string SharpBadgeAlias = "lower_third_match_ea_logo_anim_end";

    public List<ScorebugClass> Classes { get; } = new();
    public byte[] Swf { get; private init; }
    /// <summary>The widget's own source (where the parts are placed); read only.</summary>
    public string Widget { get; private set; }
    /// <summary>The logo box's source (changed only by the sharp badge option).</summary>
    public string LogoWidgetSource { get; private set; }
    /// <summary>UI image names → (texture path, width, height), from the game's asset path list; empty if it can't be read.</summary>
    public Dictionary<string, (string Texture, int Width, int Height)> Aliases { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The texture an image name (logoProps.path…) points at, or null.</summary>
    public string AliasTexture(string alias) => alias != null && Aliases.TryGetValue(alias.Trim('"'), out var a) ? a.Texture : null;

    /// <summary>True when the logo box already draws the sharp badge (its style has the clip rectangle).</summary>
    public bool HasSharpBadge => Classes.FirstOrDefault(c => c.Short == LogoStyle)?.Source.Contains("logoClipProps") == true;

    public IEnumerable<ScorebugValue> Values => Classes.SelectMany(c => c.Values);
    public ScorebugValue Find(string cls, string group, string key) =>
        Classes.FirstOrDefault(c => c.Short == cls)?.Values.FirstOrDefault(v => v.Group == group && v.Key == key);

    // ---------------------------------------------------------------- FFDec

    /// <summary>FFDec's command-line program: NHL27KIT_FFDEC, ThirdParty\ffdec, or NHL24Kit's C:\dev\tools\ffdec.</summary>
    public static string Ffdec()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("NHL27KIT_FFDEC"),
            Path.Combine(KitPaths.Root, "ThirdParty", "ffdec", "ffdec-cli.exe"),
            Path.Combine(AppContext.BaseDirectory, "ThirdParty", "ffdec", "ffdec-cli.exe"),
            @"C:\dev\tools\ffdec\ffdec-cli.exe",
        };
        return candidates.FirstOrDefault(c => !string.IsNullOrEmpty(c) && System.IO.File.Exists(c))
            ?? throw new FileNotFoundException("FFDec isn't installed: put it in ThirdParty\\ffdec (ffdec-cli.exe) or set NHL27KIT_FFDEC.");
    }

    private static void Run(params string[] args)
    {
        var psi = new ProcessStartInfo(Ffdec()) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi);
        var err = p.StandardError.ReadToEndAsync();
        string output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        string all = output + err.Result;
        if (p.ExitCode != 0 || all.Contains("Exception", StringComparison.Ordinal) || Regex.IsMatch(all, @"(?im)^\s*error"))
            throw new InvalidOperationException("FFDec failed: " + string.Join(" | ", all.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).TakeLast(4)));
    }

    // ---------------------------------------------------------------- reading

    /// <summary>Reads the score clock from the game's SWF now: decompiles the style classes and the widget.</summary>
    public static Scorebug Load(AssetIndex index)
    {
        var swf = new Legacy(index, Collector).Read(File);
        var sb = FromSwf(swf);
        try { sb.Aliases = ParseAliases(new Legacy(index, AssetPathsCollector).Read(AssetPaths)); }
        catch (Exception e) when (e is IOException or InvalidDataException) { }
        return sb;
    }

    /// <summary>"name": "img://fb/ui/textures/...&amp;[WxH]" entries of the asset path list.</summary>
    public static Dictionary<string, (string Texture, int Width, int Height)> ParseAliases(byte[] json)
    {
        var map = new Dictionary<string, (string, int, int)>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in Regex.Matches(Encoding.UTF8.GetString(json), @"""([^""]+)""\s*:\s*""img://fb(?:sync)?/([^""&]+)(?:&\[(\d+)X(\d+)\])?"""))
            map[m.Groups[1].Value] = (m.Groups[2].Value.ToLowerInvariant(),
                m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0, m.Groups[4].Success ? int.Parse(m.Groups[4].Value) : 0);
        return map;
    }

    public static Scorebug FromSwf(byte[] swf)
    {
        string dir = Path.Combine(Path.GetTempPath(), "nhl27kit-scorebug-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            string input = Path.Combine(dir, "tin_nhl.swf");
            System.IO.File.WriteAllBytes(input, swf);
            Run("-selectclass", $"{Package}.subwidgets.styles.*,{Package}.styles.*,{Package}.ScoreclockWdgt,{LogoWidget}", "-export", "script", Path.Combine(dir, "out"), input);
            var sb = new Scorebug { Swf = swf };
            string root = Path.Combine(dir, "out", "scripts");
            foreach (var f in Directory.EnumerateFiles(root, "*.as", SearchOption.AllDirectories).OrderBy(f => f))
            {
                string name = Path.GetRelativePath(root, f)[..^3].Replace(Path.DirectorySeparatorChar, '.');
                string src = System.IO.File.ReadAllText(f);
                if (name == Package + ".ScoreclockWdgt") { sb.Widget = src; continue; }
                if (name == LogoWidget) { sb.LogoWidgetSource = src; continue; }
                sb.Classes.Add(Parse(name, src));
            }
            if (sb.Classes.Count == 0) throw new InvalidDataException("No score clock style classes in the game's tin_nhl.swf.");
            return sb;
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    private static readonly Regex Const = new(@"public const (\w+):(?:Number|int|uint) = (-?[0-9.]+|0x[0-9A-Fa-f]+);", RegexOptions.Compiled);
    private static readonly Regex Props = new(@"this\.(\w+) = \{", RegexOptions.Compiled);
    private static readonly Regex Entry = new(@"""(\w+)"":((?:""(?:[^""\\]|\\.)*"")|[^,\r\n}]+)", RegexOptions.Compiled);

    public static ScorebugClass Parse(string name, string src)
    {
        var c = new ScorebugClass { Name = name, Source = src };
        foreach (Match m in Const.Matches(src))
            c.Values.Add(new ScorebugValue { Class = c.Short, Group = "const", Key = m.Groups[1].Value, Text = m.Groups[2].Value, Start = m.Groups[2].Index, Length = m.Groups[2].Length,
                Kind = m.Groups[1].Value.Contains("COLOR", StringComparison.Ordinal) ? ScorebugValueKind.Color : ScorebugValueKind.Number });
        foreach (Match m in Props.Matches(src))
        {
            int open = m.Index + m.Length - 1, close = Close(src, open);
            if (close < 0) continue;
            foreach (Match e in Entry.Matches(src[..close], open))
            {
                string key = e.Groups[1].Value, text = e.Groups[2].Value.TrimEnd();
                var kind = text.StartsWith('"') ? ScorebugValueKind.Text
                    : key.Contains("color", StringComparison.OrdinalIgnoreCase) ? ScorebugValueKind.Color
                    : Regex.IsMatch(text, @"^-?[0-9.]+$") ? ScorebugValueKind.Number
                    : text is "true" or "false" ? ScorebugValueKind.Bool
                    : ScorebugValueKind.Expression;
                c.Values.Add(new ScorebugValue { Class = c.Short, Group = m.Groups[1].Value, Key = key, Text = text, Start = e.Groups[2].Index, Length = text.Length, Kind = kind });
            }
        }
        foreach (var v in c.Values) v.Resolve = expr => Evaluate(c, expr);
        return c;
    }

    /// <summary>Works out "this.NAME", "this.NAME / 2", "this.NAME - 8" from the class's constants (else null).</summary>
    public static double? Evaluate(ScorebugClass c, string expr)
    {
        var m = Regex.Match(expr.Trim(), @"^this\.(\w+)(?:\s*([-+*/])\s*(-?[0-9.]+))?$");
        if (!m.Success) return null;
        var k = c.Values.FirstOrDefault(v => v.Group == "const" && v.Key == m.Groups[1].Value)?.Number;
        if (k is not { } a) return null;
        if (!m.Groups[2].Success) return a;
        double b = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        return m.Groups[2].Value switch { "+" => a + b, "-" => a - b, "*" => a * b, _ => a / b };
    }

    private static int Close(string s, int open)
    {
        int depth = 0;
        bool str = false;
        for (int i = open; i < s.Length; i++)
        {
            char ch = s[i];
            if (str) { if (ch == '\\') i++; else if (ch == '"') str = false; continue; }
            if (ch == '"') str = true;
            else if (ch == '{') depth++;
            else if (ch == '}' && --depth == 0) return i;
        }
        return -1;
    }

    /// <summary>Where the widget mounts a part (_timeMount → x, y), read from ScoreclockWdgt's source; null if not found.</summary>
    public (double X, double Y)? Mount(string mount)
    {
        if (Widget == null) return null;
        var m = Regex.Match(Widget, $@"this\.{Regex.Escape(mount)} = new TinMount\([^,{{]+,\{{(?<body>[^}}]*)\}}");
        if (!m.Success) return Widget.Contains($"this.{mount} = new TinMount(") ? (0, 0) : null;
        double Get(string k) => Regex.Match(m.Groups["body"].Value, $@"""{k}"":(-?[0-9.]+)") is { Success: true } g ? double.Parse(g.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        return (Get("x"), Get("y"));
    }

    // ---------------------------------------------------------------- writing

    /// <summary>
    /// Recompiles the classes with these edits (value id → new value as an editor shows it) into a copy of the SWF.
    /// Throws if a value can't be parsed or FFDec can't compile it.
    /// </summary>
    public byte[] Build(IReadOnlyDictionary<string, string> edits, bool sharpBadge = false)
    {
        sharpBadge &= !HasSharpBadge;
        var byClass = Values.Where(v => edits.ContainsKey(v.Id)).GroupBy(v => v.Class).Select(g => g.Key).ToList();
        if (sharpBadge && !byClass.Contains(LogoStyle)) byClass.Add(LogoStyle);
        if (byClass.Count == 0) return Swf;
        string dir = Path.Combine(Path.GetTempPath(), "nhl27kit-scorebug-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            string input = Path.Combine(dir, "in.swf"), output = Path.Combine(dir, "out.swf");
            System.IO.File.WriteAllBytes(input, Swf);
            var args = new List<string> { "-replace", input, output };
            foreach (string shortName in byClass)
            {
                var cls = Classes.First(c => c.Short == shortName);
                var src = new StringBuilder(cls.Source);
                foreach (var v in cls.Values.Where(v => edits.ContainsKey(v.Id)).OrderByDescending(v => v.Start))
                    src.Remove(v.Start, v.Length).Insert(v.Start, v.Literal(edits[v.Id]));
                string text = src.ToString();
                if (sharpBadge && shortName == LogoStyle) text = SharpBadgeStyle(text);
                string file = Path.Combine(dir, cls.Short + ".as");
                System.IO.File.WriteAllText(file, text);
                args.Add(cls.Name);
                args.Add(file);
            }
            if (sharpBadge)
            {
                string file = Path.Combine(dir, "ScoreclockLogoSubwdgt.as");
                System.IO.File.WriteAllText(file, SharpBadgeWidget(LogoWidgetSource ?? throw new InvalidDataException("The logo box's code wasn't read.")));
                args.Add(LogoWidget);
                args.Add(file);
            }
            Run(args.ToArray());
            return System.IO.File.ReadAllBytes(output);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    /// <summary>Builds the edits and writes the SWF into the game (one mod entry; Restore puts the game's own back).</summary>
    public static ModEntry Save(AssetIndex index, ModStore store, string game, Scorebug current, IReadOnlyDictionary<string, string> edits, Action<string> log = null, bool sharpBadge = false)
    {
        var swf = current.Build(edits, sharpBadge);
        var changed = current.Values.Where(v => edits.ContainsKey(v.Id)).Select(v => $"{v.Class.Replace("Scoreclock", "").Replace("SubwdgtStyle", "")}.{v.Group}.{v.Key} = {edits[v.Id]}").ToList();
        if (sharpBadge && !current.HasSharpBadge) changed.Insert(0, "sharp EA SPORTS badge (360x360)");
        var existing = store.Find(LegacyEditor.Kind, File);
        string summary = "Score clock: " + string.Join(", ", changed);
        if (existing != null && !string.IsNullOrEmpty(existing.Summary) && existing.Summary.StartsWith("Score clock: "))
            summary = existing.Summary + "; " + string.Join(", ", changed);
        if (summary.Length > 600) summary = "…" + summary[^599..];
        return LegacyEditor.Save(index, store, game, Collector, File, swf, summary, log);
    }

    // ---------------------------------------------------------------- the sharp badge

    /// <summary>
    /// The logo style drawing the sheet's last frame where the 64x64 badge was: the sheet at 190x190 from (-118.7, -121.1)
    /// puts that frame's badge (pixels 34..330 x 45..346 of 360) on the old badge's 52 pixels around (40, 40), and the new
    /// logoClipProps rectangle (the frame) hides the other eight frames.
    /// </summary>
    public static string SharpBadgeStyle(string src)
    {
        var m = Regex.Match(src, @"this\.logoProps = \{[^}]*\};");
        if (!m.Success) throw new InvalidDataException("The logo style has no logoProps.");
        string props = $@"this.logoProps = {{
            ""x"":-118.7,
            ""y"":-121.1,
            ""width"":190,
            ""height"":190,
            ""path"":""{SharpBadgeAlias}""
         }};
         this.logoClipProps = {{
            ""x"":8,
            ""y"":5.6,
            ""width"":63.3,
            ""height"":63.3
         }};";
        src = src[..m.Index] + props + src[(m.Index + m.Length)..];
        var d = Regex.Match(src, @"public var logoProps:Object;");
        if (!d.Success) throw new InvalidDataException("The logo style has no logoProps field.");
        return src.Insert(d.Index + d.Length, "\n      \n      public var logoClipProps:Object;");
    }

    /// <summary>The logo box clipping its logo to the style's logoClipProps (when there is one).</summary>
    public static string SharpBadgeWidget(string src)
    {
        var field = Regex.Match(src, @"protected var _logoMask:TinMask;");
        var make = Regex.Match(src, @"this\._logo = new ImageLoaderOnData\([^;]*\);");
        if (!field.Success || !make.Success) throw new InvalidDataException("The logo box's code isn't what the sharp badge expects.");
        src = src.Insert(make.Index + make.Length, @"
         if(this._ss.logoClipProps)
         {
            this._logoClip = new TinMask(_janitor,this._logoMount,this._ss.logoClipProps);
         }");
        return src.Insert(field.Index + field.Length, "\n      \n      protected var _logoClip:TinMask;");
    }

    /// <summary>The team rows' shading image (4x40, stretched over each row); only the score clock uses it.</summary>
    public const string RowShading = "ui/textures/common/scoreclock/scoreclockgradient";

    // ---------------------------------------------------------------- colours

    public static uint ParseColor(string s)
    {
        s = s.Trim().TrimStart('#');
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        if (s.Length == 3) s = string.Concat(s.Select(ch => $"{ch}{ch}"));
        if (s.Length != 6 || !uint.TryParse(s, NumberStyles.HexNumber, null, out uint v))
            throw new FormatException($"'{s}' isn't a colour (use #RRGGBB).");
        return v;
    }

    /// <summary>Stand-ins for MasterStyle colours, which the game fills in at run time (for the preview only).</summary>
    public static uint? MasterColor(string expr)
    {
        var m = Regex.Match(expr, @"COLOR_([A-Z]+)(?:_(\d))?");
        if (!m.Success) return null;
        return m.Groups[1].Value switch
        {
            "WHITE" => 0xFFFFFF,
            "BLACK" => 0x111214,
            "GREY" => m.Groups[2].Value switch { "0" => 0x2A2D31, "1" => 0x5A5F66, _ => 0x9AA0A8 },
            "BLUE" => 0x00AEEF,
            "GOLD" => 0xFFEA8A,
            "GREEN" => 0x39FC28,
            "ORANGE" => 0xFA6A2F,
            "NEG" => 0xFF265A,
            "POS" => 0x39FC28,
            _ => null,
        };
    }
}
