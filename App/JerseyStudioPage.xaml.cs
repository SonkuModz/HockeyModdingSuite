using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using NHL27Kit.Core;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace NHL27Kit.App;

/// <summary>A design in the studio's picker: one of the game's create-a-team jerseys, or the kit's own art (no template).</summary>
public sealed class StudioTemplateRow : Observable
{
    private BitmapSource _thumb;
    public StudioTemplate Template { get; init; }
    /// <summary>In a uniform of your own the first row is a plain jersey, not the kit's art.</summary>
    public bool Own { get; init; }
    public string Label => Template?.Label ?? (Own ? "Plain: your main colour" : "The kit's own art");
    public string Short => Template == null ? (Own ? "plain" : "kit")
        : Template.Garment != "jersey" ? Template.Number.ToString() : (Template.Brand == "rbk" ? "R" : "A") + Template.Number;
    public BitmapSource Thumb { get => _thumb; set => Set(ref _thumb, value); }
}

/// <summary>Reads a colour box: "#1A2B3C" or "1A2B3C", null when it isn't one.</summary>
public static class HexColor
{
    public static int? Parse(string text)
    {
        string t = (text ?? "").Trim().TrimStart('#');
        return t.Length == 6 && int.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v) ? v : null;
    }

    public static Brush Brush(int? c) => c is int v ? new SolidColorBrush(Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v)) : Brushes.Transparent;
}

/// <summary>A colour the studio paints: a design's zone (Zone ≥ 0) or a fabric region of the base (Region > 0; blank keeps the art).</summary>
public sealed class StudioZone : Observable
{
    private readonly Action<StudioZone> _changed;
    private string _text;
    private bool _selected;

    public StudioZone(int zone, int region, string label, string meta, int? color, Action<StudioZone> changed)
    {
        Zone = zone;
        Region = region;
        Label = label;
        Meta = meta;
        _text = color is int c ? GearZone.Hex(c) : "";
        _changed = changed;
    }

    public int Zone { get; }
    public int Region { get; }
    public string Label { get; }
    public string Meta { get; }
    public int? Value => HexColor.Parse(_text);
    public bool IsBlank => string.IsNullOrWhiteSpace(_text);
    public bool HasError => !IsBlank && Value == null;
    public bool IsSelected { get => _selected; set { _selected = value; Raise(nameof(IsSelected)); } }
    public Brush Swatch => HexColor.Brush(Value);

    public string Text
    {
        get => _text;
        set
        {
            if (!Set(ref _text, value)) return;
            Raise(nameof(Swatch));
            Raise(nameof(HasError));
            _changed?.Invoke(this);
        }
    }
}

/// <summary>A stripe across the neck band: from (% of its height, from the seam up), how tall, and its colour.</summary>
public sealed class CollarStripeRow : Observable
{
    private readonly Action<CollarStripeRow> _changed;
    private double _from, _width;
    private string _color;
    private bool _selected;

    public CollarStripeRow(CollarStripe s, Action<CollarStripeRow> changed)
    {
        _from = s.From;
        _width = s.Width;
        _color = GearZone.Hex(s.Color);
        _changed = changed;
    }

    private void Changed(params string[] names)
    {
        foreach (var n in names) Raise(n);
        Raise(nameof(HasError));
        _changed?.Invoke(this);
    }

    public double From { get => _from; set { if (Set(ref _from, Math.Round(value))) Changed(nameof(FromText)); } }
    public double Width { get => _width; set { if (Set(ref _width, Math.Round(value))) Changed(nameof(WidthText)); } }
    public string ColorText { get => _color; set { if (Set(ref _color, value)) Changed(nameof(Swatch)); } }
    public bool IsSelected { get => _selected; set { _selected = value; Raise(nameof(IsSelected)); } }
    public string FromText => $"{_from:0}%";
    public string WidthText => $"{_width:0}%";
    public Brush Swatch => HexColor.Brush(HexColor.Parse(_color));
    public bool HasError => HexColor.Parse(_color) == null;
    public CollarStripe ToStripe() => HexColor.Parse(_color) is int c ? new CollarStripe(_from, _width, c) : null;
}

/// <summary>A stripe being designed: where, how far from the hem or cuff, how wide, its colour and its edge.</summary>
public sealed class LineRow : Observable
{
    private readonly Action<LineRow> _changed;
    private int _place;
    private double _from, _width, _edgeWidth;
    private string _color, _edge;
    private bool _selected;
    private StudioStripe _stripe;
    private ImageSource _thumb;

    public LineRow(StudioLine line, Action<LineRow> changed)
    {
        _stripe = line.Image;
        _thumb = Thumbnail(_stripe);
        _place = (int)line.Place;
        _from = line.From;
        _width = line.Width;
        _color = GearZone.Hex(line.Color);
        _edge = line.Edge is int e ? GearZone.Hex(e) : "";
        _edgeWidth = line.EdgeWidth;
        _changed = changed;
    }

    private void Changed(params string[] names)
    {
        foreach (var n in names) Raise(n);
        Raise(nameof(Meta)); Raise(nameof(HasError));
        _changed?.Invoke(this);
    }

    public int PlaceIndex { get => _place; set { if (Set(ref _place, value)) Changed(nameof(FromMax)); } }
    public double FromMax => _place == 0 ? 70 : 45;
    public double From { get => _from; set { if (Set(ref _from, Math.Round(value * 4) / 4)) Changed(nameof(FromText)); } }
    public double Width { get => _width; set { if (Set(ref _width, Math.Round(value * 4) / 4)) Changed(nameof(WidthText)); } }
    public double EdgeWidth { get => _edgeWidth; set { if (Set(ref _edgeWidth, Math.Round(value * 4) / 4)) Changed(nameof(EdgeText)); } }
    public string ColorText { get => _color; set { if (Set(ref _color, value)) Changed(nameof(Swatch)); } }
    public string EdgeColorText { get => _edge; set { if (Set(ref _edge, value)) Changed(nameof(EdgeSwatch), nameof(EdgeText)); } }
    public bool IsSelected { get => _selected; set { _selected = value; Raise(nameof(IsSelected)); } }

    public string FromText => $"{_from:0.##} cm";
    public string WidthText => $"{_width:0.##} cm";
    public string EdgeText => HexColor.Parse(_edge) == null ? "none" : $"{_edgeWidth:0.##} cm";
    public string Meta => (_place == 0 ? "measured up from the hem" : "measured up the arm from the cuff") + (_stripe != null ? "  ·  image" : "");

    /// <summary>The image wrapped round as this line (null: a flat colour).</summary>
    public StudioStripe Stripe
    {
        get => _stripe;
        set
        {
            _stripe = value;
            _thumb = Thumbnail(value);
            Changed(nameof(Stripe), nameof(HasImage), nameof(NoImage), nameof(Thumb), nameof(Repeat), nameof(RepeatText), nameof(Mirror));
        }
    }
    public bool HasImage => _stripe != null;
    public bool NoImage => _stripe == null;
    public ImageSource Thumb => _thumb;
    public double Repeat
    {
        get => _stripe?.Repeat ?? 1;
        set
        {
            int r = Math.Max(1, (int)Math.Round(value));
            if (_stripe == null || r == _stripe.Repeat) return;
            _stripe = _stripe with { Repeat = r };
            Changed(nameof(Repeat), nameof(RepeatText));
        }
    }
    public string RepeatText => $"× {Repeat:0}";
    public bool Mirror
    {
        get => _stripe?.Mirror ?? true;
        set
        {
            if (_stripe == null || value == _stripe.Mirror) return;
            _stripe = _stripe with { Mirror = value };
            Changed(nameof(Mirror));
        }
    }

    private static ImageSource Thumbnail(StudioStripe s)
    {
        if (s == null) return null;
        var bgra = (byte[])s.Rgba.Clone();
        for (int i = 0; i < bgra.Length; i += 4) (bgra[i], bgra[i + 2]) = (bgra[i + 2], bgra[i]);
        var bmp = BitmapSource.Create(s.Width, s.Height, 96, 96, PixelFormats.Bgra32, null, bgra, s.Width * 4);
        bmp.Freeze();
        return bmp;
    }
    public Brush Swatch => HexColor.Brush(HexColor.Parse(_color));
    public Brush EdgeSwatch => HexColor.Brush(HexColor.Parse(_edge));
    public bool HasError => _stripe == null && (HexColor.Parse(_color) == null || (!string.IsNullOrWhiteSpace(_edge) && HexColor.Parse(_edge) == null));

    /// <summary>The line to paint, or null while a colour box isn't a colour.</summary>
    public StudioLine ToLine() => _stripe != null ? new StudioLine((LinePlace)_place, _from, _width, HexColor.Parse(_color) ?? 0, null, 0, _stripe)
        : HexColor.Parse(_color) is int c && !HasError ? new StudioLine((LinePlace)_place, _from, _width, c, HexColor.Parse(_edge), _edgeWidth) : null;
}

/// <summary>
/// A hotspot in the studio: a branding logo of the kit's stamp (Layer ≥ 0), a lettering spot (Layer -1), or a logo added
/// by the design (Decal set; painted into the base).
/// </summary>
public sealed class SpotRow : Observable
{
    private StampHotspot _spot;
    private bool _selected;

    public SpotRow(StampHotspot spot, int layer, StudioDecal decal = null, string name = null)
    {
        Name = name;
        Original = _spot = spot;
        Layer = layer;
        Decal = decal;
    }

    public StampHotspot Original { get; }
    /// <summary>An added logo's own file name (its image is kept under another).</summary>
    public string Name { get; }
    public int Layer { get; }
    public StudioDecal Decal { get; }
    public bool IsDecal => Decal != null;
    public bool IsBrand => Layer >= 0 && !IsDecal;
    public StampHotspot Spot { get => _spot; set { if (Set(ref _spot, value)) Raise(nameof(Enabled)); } }
    public bool Enabled => _spot.Enabled;
    public bool IsSelected { get => _selected; set { _selected = value; Raise(nameof(IsSelected)); } }
    public Brush Dot => (Brush)Application.Current.FindResource(IsDecal ? "Patch" : IsBrand ? "Accent" : "Violet");

    public string Label => IsDecal ? Path.GetFileNameWithoutExtension(Name ?? Decal.File) is { Length: > 16 } n ? n[..15] + "…" : Path.GetFileNameWithoutExtension(Name ?? Decal.File)
        : Original.Name switch
        {
            "0" or "mainlogo" => "Main logo",
            "right_shoulder" or "rightshoulder" => "Right shoulder",
            "left_shoulder" or "leftshoulder" => "Left shoulder",
            "collar_nhl" or "collarlogo" => "Collar NHL",
            "back_adidas" or "adidasback" => "Back adidas",
            "adidas primegreen" or "adidasprimegreen" => "Hem tag",
            "sponsor" => "Sponsor",
            "name" => "Name",
            "number_back" => "Back number",
            "number_front" => "Front number",
            "number_arm_left" => "Left arm no.",
            "number_arm_right" => "Right arm no.",
            "captain" => "Captain",
            "patch" => "Patch",
            "helmet" => "Helmet no.",
            _ when IsBrand => $"Logo {Layer}",
            _ => UniformNames.Pretty(Original.Name),
        };
}

/// <summary>A team a design can be added to, with its kit folders' art id.</summary>
public sealed record StudioTeamItem(JerseySlots.KitTeam Team, string League, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// The Jersey Studio: designs a jersey on top of a kit the game has, live on the game's models (jersey, pants, socks,
/// helmet and gloves).
/// - Art: the kit's own base, a whole image (NHL 27 or legacy layout), or one of the game's create-a-team designs painted
///   in team colours; flat fills of the base's fabric regions (collar, yoke…); lines round the body and sleeves
///   (measured on the model, so they wrap round it).
/// - Logos and lettering: every hotspot of the kit's stamp can be moved, sized, turned, hidden or shown; a logo's image
///   can be replaced (PNG or DDS); new logos can be added anywhere (painted into the base).
/// - Writing: over the kit itself (base, baked jersey, replaced logos and the edited stamp, each a mod entry), or into
///   another kit folder (a free one: no team's menu shows it) that's then added to a team's jersey menu.
/// </summary>
public partial class JerseyStudioPage : UserControl
{
    private ModService _mods;
    private TextureService _textures;
    private List<TeamRow> _teams = new();
    private TeamRow _team;
    private Kit _kit;
    private int _version;
    private bool _suppress, _busy;
    private const int TextureSize = 2048;
    private readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly DispatcherTimer _letterTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private List<(string Garment, MeshPart Part, ImageSource Tex)> _shown;
    private int[] _helmetZones, _gloveZones;
    private List<int> _teamColors = new();

    private StudioSource _studioSrc;
    private StudioDesign _design;
    private byte[] _studioPainted;
    private bool _artDirty = true, _studioLoading;
    private int _studioVersion, _shuffle, _decalCount;
    private string _studioImageFile;
    private List<StudioTemplate> _templates;
    private SpotRow _spot;
    private Point? _dragOffset;
    /// <summary>What a swatch click fills: the selected colour row's or line's box.</summary>
    private Action<string> _colorTarget;
    private readonly DispatcherTimer _studioTimer = new() { Interval = TimeSpan.FromMilliseconds(90) };
    private static readonly Dictionary<StudioTemplate, (sbyte[] Map, int Size, List<int> Order)> ThumbMaps = new();
    private const double Sheet = 400;

    // a uniform of your own: the create-a-team font, pants and socks in your colours, on a kit's models (the carrier)
    private bool _own = true;
    private Kit _carrier;
    private List<StudioTemplate> _fonts, _pants, _socks;
    private StudioPattern _fontPat, _pantPat, _sockPat;
    private HashSet<int> _outlines;
    private byte[] _fontImg;
    private ImageSource _pantTex, _sockTex;
    private readonly int[] _ownColors = { 0x0B2D6B, 0xFFFFFF, 0xC8102E };
    private int _letterFill = 0xFFFFFF, _letterOutline = 0xC8102E, _pantShift, _sockShift, _thumbGen, _tplGen;
    private bool _ownColorsChanged;
    private readonly DispatcherTimer _ownTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private const int PartSize = 1024;
    private static readonly Dictionary<StudioTemplate, (StudioPattern Pattern, HashSet<int> Outlines)> PartThumbs = new();

    private Dictionary<(string League, string Team), JerseySlots.KitTeam> _kitTeams;
    private HashSet<(int ArtId, int Style, int Variant)> _used;

    public event Action<string> StatusChanged;
    /// <summary>A team's jersey menu was changed here (its id).</summary>
    public event Action<int> SlotsChanged;

    private IEnumerable<SpotRow> Spots => StudioSpotList.ItemsSource as IEnumerable<SpotRow> ?? Enumerable.Empty<SpotRow>();
    private IEnumerable<StudioZone> StudioZoneRows => StudioZones.ItemsSource as IEnumerable<StudioZone> ?? Enumerable.Empty<StudioZone>();
    private IEnumerable<LineRow> LineRows => LineList.ItemsSource as IEnumerable<LineRow> ?? Enumerable.Empty<LineRow>();

    public JerseyStudioPage()
    {
        InitializeComponent();
        _searchTimer.Tick += (_, _) => { _searchTimer.Stop(); ApplyFilter(); };
        _letterTimer.Tick += async (_, _) => { _letterTimer.Stop(); await RedrawGear(); ScheduleStudio(); };
        _studioTimer.Tick += async (_, _) => { _studioTimer.Stop(); await RedrawStudio(); };
        _ownTimer.Tick += async (_, _) => { _ownTimer.Stop(); await ApplyOwn(); };
        _suppress = true;
        LetterName.Text = "PLAYER";
        LetterNumber.Text = "27";
        LetterCaptain.SelectedIndex = 0;
        SlotLabel.ItemsSource = Enumerable.Range(0, 16).Select(i => new PickItem(JerseySlots.LabelName(i), i)).ToList();
        SlotLabel.SelectedIndex = 2;
        LineList.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<LineRow>();
        Viewer.Pick = PickLine;
        _suppress = false;
        ShowOwnColors();
        ModeLayout();
        SlotInfo.Text = "Writes the design into a kit folder, then adds that folder to the team's jersey menu (nhl_game.db). " +
                        "A free folder is one no team's menu shows, so no other jersey changes. Restore on the Mods page undoes each part.";
    }

    public async void SetMods(ModService mods)
    {
        _mods = mods;
        _textures = new TextureService(mods.Index);
        Summary.Text = "Finding the kits…";
        var kits = await Task.Run(() => Kits.Find(mods.Index));
        _teams = kits.Where(k => k.IsStamped("jersey")).GroupBy(k => (k.League, k.Team))
            .Select(g => new TeamRow { League = g.Key.League, Team = g.Key.Team, Kits = g.ToList() })
            .OrderBy(t => t.League == "nhl" ? 0 : 1).ThenBy(t => t.League).ThenBy(t => t.Team).ToList();
        _allKits = kits;
        _carrier = OwnKit.Carrier(kits);
        Summary.Text = "Your own uniform from the game's create-a-team parts, or start from any of " +
                       $"{_teams.Sum(t => t.Kits.Count):N0} team jerseys  ·  lines, logos (PNG or DDS), lettering, pants and socks  ·  added to any team's jersey menu";
        ApplyFilter();
        if (_own) await StartOwn();
    }

    private List<Kit> _allKits = new();

    public void SetSearch(string text) { Search.Text = text; ApplyFilter(); }

    private void ApplyFilter()
    {
        var terms = Search.Text.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var rows = _teams.Where(t => terms.All(q => t.Team.Contains(q) || t.League.Contains(q))).ToList();
        TeamList.ItemsSource = rows;
        TeamCount.Text = $"{rows.Count:N0}";
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) { _searchTimer.Stop(); _searchTimer.Start(); }

    public async Task SelectTeam(int n, int kit = 0)
    {
        if (_own) { ModeTeam.IsChecked = true; await Task.Delay(100); }
        if (TeamList.Items.Count <= n) return;
        TeamList.SelectedIndex = n;
        if (TeamList.SelectedItem is TeamRow t && kit > 0 && kit < t.Kits.Count) await ShowKit(t.Kits[kit]);
    }

    private async void OnTeamSelected(object sender, SelectionChangedEventArgs e)
    {
        if (TeamList.SelectedItem is not TeamRow t) return;
        _team = t;
        await ShowKit(t.Kits.FirstOrDefault(k => k.Name == "home" && k.Brand == "adidas") ?? t.Kits.FirstOrDefault(k => k.Name == "home") ?? t.Kits[0]);
    }

    private async void OnKitPicked(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || KitPicker.SelectedItem is not KitTab tab || tab.Kit == _kit) return;
        await ShowKit(tab.Kit);
    }

    // ================================================================ your own uniform

    /// <summary>Shows what the mode uses: your colours and parts, or the teams, the kit picker and Write over this kit.</summary>
    private void ModeLayout()
    {
        _own = ModeOwn.IsChecked == true;
        OwnPanel.Visibility = _own ? Visibility.Visible : Visibility.Collapsed;
        TeamPanel.Visibility = _own ? Visibility.Collapsed : Visibility.Visible;
        Search.Visibility = _own ? Visibility.Collapsed : Visibility.Visible;
        KitPicker.Visibility = _own ? Visibility.Collapsed : Visibility.Visible;
        StudioWrite.Visibility = _own ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void OnMode(object sender, RoutedEventArgs e)
    {
        if (OwnPanel == null || TeamPanel == null) return;
        ModeLayout();
        if (_mods == null) return;
        if (_own) { await StartOwn(); return; }
        if (_team != null) { await ShowKit(_team.Kits.FirstOrDefault(k => k.Name == "home") ?? _team.Kits[0]); return; }
        ++_version;
        _kit = null;
        _studioSrc = null;
        _shown = null;
        Viewer.Show(Enumerable.Empty<(MeshPart, ImageSource)>());
        TeamTitle.Text = KitTitle.Text = "";
        ViewBusy.Visibility = Visibility.Collapsed;
        ViewEmpty.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Opens a uniform of your own: a plain jersey in your main colour (no team's art or logos) with the lettering spots
    /// of a kit's stamp, the create-a-team font, pants and socks in your colours, on the game's models.
    /// </summary>
    private async Task StartOwn()
    {
        if (_carrier == null) { ViewBusyText.Text = "There's no stamped kit to borrow the models from."; return; }
        int version = ++_version;
        _kit = _carrier;
        TeamTitle.Text = "Your jersey";
        KitTitle.Text = "made from the game's create-a-team parts  ·  no team's art or logos";
        ViewEmpty.Visibility = Visibility.Collapsed;
        ViewBusy.Visibility = Visibility.Visible;
        ViewBusyText.Text = "Reading the create-a-team parts from the game…";
        try
        {
            var index = _mods.Index;
            if (_fonts == null)
            {
                (_fonts, _pants, _socks) = await Task.Run(() => (OwnKit.Parts(index, "font"), OwnKit.Parts(index, "pant"), OwnKit.Parts(index, "sock")));
                _suppress = true;
                OwnFonts.ItemsSource = _fonts.Select(t => new StudioTemplateRow { Template = t }).ToList();
                OwnPants.ItemsSource = _pants.Select(t => new StudioTemplateRow { Template = t }).ToList();
                OwnSocks.ItemsSource = _socks.Select(t => new StudioTemplateRow { Template = t }).ToList();
                OwnFonts.SelectedIndex = 0;
                OwnPants.SelectedIndex = Math.Min(2, _pants.Count - 1);
                OwnSocks.SelectedIndex = Math.Min(1, _socks.Count - 1);
                _suppress = false;
            }
            await PaintOwnParts();
            PaintPartThumbs();
            if (version != _version) return;
            var parts = await KitScene.BuildAsync(index, _textures, _carrier, Lettering(), OwnGear, TextureSize, () => version != _version);
            if (parts == null || version != _version) return;
            _shown = WithOwnParts(parts);
            Viewer.Show(_shown.Select(p => (p.Part, p.Tex)));
            ViewBusy.Visibility = Visibility.Collapsed;
            await EnsureStudio();
        }
        catch (Exception ex)
        {
            ViewBusyText.Text = "Couldn't make the uniform: " + ex.Message;
        }
    }

    /// <summary>The helmet (all main colour) or a glove (your colours in turn).</summary>
    private async Task<ImageSource> OwnGear(string garment)
    {
        var index = _mods.Index;
        var palette = Palette();
        try
        {
            var g = await Task.Run(() => garment == "helmet" ? KitEquipment.Helmet(index, OwnKit.HelmetZones(palette), 1024)
                : KitEquipment.Gloves(index, garment == "glove_left" ? "left" : "right", OwnKit.GloveZones(palette), 1024));
            return TextureService.ToBitmap(g.Rgba, g.Width, g.Height, Channel.Rgb);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException) { return null; }
    }

    /// <summary>The shown parts with your pants and socks.</summary>
    private List<(string Garment, MeshPart Part, ImageSource Tex)> WithOwnParts(List<(string Garment, MeshPart Part, ImageSource Tex)> parts) =>
        parts.Select(p => p.Garment == "pant" && _pantTex != null ? (p.Garment, p.Part, _pantTex)
            : p.Garment == "sock" && _sockTex != null ? (p.Garment, p.Part, _sockTex) : p).ToList();

    private StudioTemplate Picked(ListBox list) => (list.SelectedItem as StudioTemplateRow)?.Template;

    /// <summary>Paints the picked font (your lettering colours), pants and socks (your colours), reading what changed from the game.</summary>
    private async Task PaintOwnParts()
    {
        var index = _mods.Index;
        var palette = Palette();
        var (ft, pt, st) = (Picked(OwnFonts), Picked(OwnPants), Picked(OwnSocks));
        int fill = _letterFill, outline = _letterOutline, ps = _pantShift, ss = _sockShift;
        var (fontPat, outlines, pantPat, sockPat) = (_fontPat, _outlines, _pantPat, _sockPat);
        var r = await Task.Run(() =>
        {
            if (ft != null && fontPat?.Template != ft) { fontPat = OwnKit.LoadFont(index, ft, PartSize); outlines = OwnKit.Outlines(fontPat); }
            if (pt != null && pantPat?.Template != pt) pantPat = JerseyStudio.LoadPattern(index, pt, PartSize);
            if (st != null && sockPat?.Template != st) sockPat = JerseyStudio.LoadPattern(index, st, PartSize);
            return (Font: fontPat != null ? OwnKit.PaintFont(fontPat, outlines, fill, outline) : null,
                Pant: pantPat != null ? OwnKit.PaintPart(pantPat, palette, ps) : null,
                Sock: sockPat != null ? OwnKit.PaintPart(sockPat, palette, ss) : null);
        });
        (_fontPat, _outlines, _pantPat, _sockPat) = (fontPat, outlines, pantPat, sockPat);
        _fontImg = r.Font;
        _pantTex = r.Pant != null ? TextureService.ToBitmap(r.Pant, PartSize, PartSize, Channel.Rgb) : null;
        _sockTex = r.Sock != null ? TextureService.ToBitmap(r.Sock, PartSize, PartSize, Channel.Rgb) : null;
        if (_studioSrc?.Own == true && _fontImg != null) { _studioSrc.Font = _fontImg; _studioSrc.FontSize = PartSize; }
        if (_shown != null && _own) _shown = WithOwnParts(_shown);
        ScheduleStudio();
    }

    /// <summary>The font, pant and sock pictures in your colours, drawn as the designs come in.</summary>
    private void PaintPartThumbs()
    {
        var index = _mods.Index;
        var palette = Palette();
        int fill = _letterFill, outline = _letterOutline, gen = ++_thumbGen;
        var rows = new[] { OwnFonts, OwnPants, OwnSocks }.SelectMany(l => l.ItemsSource as IEnumerable<StudioTemplateRow> ?? Enumerable.Empty<StudioTemplateRow>()).ToList();
        _ = Task.Run(async () =>
        {
            foreach (var row in rows)
            {
                if (gen != _thumbGen) return;
                var t = row.Template;
                (StudioPattern Pattern, HashSet<int> Outlines) c;
                lock (PartThumbs) PartThumbs.TryGetValue(t, out c);
                if (c.Pattern == null)
                {
                    try
                    {
                        var pat = t.Garment == "font" ? OwnKit.LoadFont(index, t, 256) : JerseyStudio.LoadPattern(index, t, 128);
                        c = (pat, t.Garment == "font" ? OwnKit.Outlines(pat) : null);
                    }
                    catch { continue; }
                    lock (PartThumbs) PartThumbs[t] = c;
                }
                var (img, w, h) = t.Garment == "font" ? OwnKit.FontThumb(OwnKit.PaintFont(c.Pattern, c.Outlines, fill, outline), c.Pattern.Size)
                    : OwnKit.Thumb(OwnKit.PaintPart(c.Pattern, palette, 0), c.Pattern.Size);
                var bmp = TextureService.ToBitmap(img, w, h, Channel.Rgb);
                await Dispatcher.InvokeAsync(() => row.Thumb = bmp);
            }
        });
    }

    private void ShowOwnColors()
    {
        _suppress = true;
        OwnColor0.Text = GearZone.Hex(_ownColors[0]);
        OwnColor1.Text = GearZone.Hex(_ownColors[1]);
        OwnColor2.Text = GearZone.Hex(_ownColors[2]);
        LetterFill.Text = GearZone.Hex(_letterFill);
        LetterOutline.Text = GearZone.Hex(_letterOutline);
        _suppress = false;
        OwnSwatches();
    }

    private void OwnSwatches()
    {
        if (LetterOutlineSwatch == null) return;
        OwnColor0Swatch.Background = HexColor.Brush(HexColor.Parse(OwnColor0.Text));
        OwnColor1Swatch.Background = HexColor.Brush(HexColor.Parse(OwnColor1.Text));
        OwnColor2Swatch.Background = HexColor.Brush(HexColor.Parse(OwnColor2.Text));
        LetterFillSwatch.Background = HexColor.Brush(HexColor.Parse(LetterFill.Text));
        LetterOutlineSwatch.Background = HexColor.Brush(HexColor.Parse(LetterOutline.Text));
    }

    private void OnOwnColor(object sender, TextChangedEventArgs e)
    {
        OwnSwatches();
        if (_suppress || sender is not TextBox { Tag: string tag } box || HexColor.Parse(box.Text) is not int c) return;
        _ownColors[int.Parse(tag)] = c;
        _ownColorsChanged = true;
        _ownTimer.Stop();
        _ownTimer.Start();
    }

    private void OnLetterColor(object sender, TextChangedEventArgs e)
    {
        OwnSwatches();
        if (_suppress || sender is not TextBox { Tag: string tag } box || HexColor.Parse(box.Text) is not int c) return;
        if (tag == "fill") _letterFill = c; else _letterOutline = c;
        _ownTimer.Stop();
        _ownTimer.Start();
    }

    private void OnLetterSwap(object sender, RoutedEventArgs e)
    {
        (_letterFill, _letterOutline) = (_letterOutline, _letterFill);
        ShowOwnColors();
        _ownTimer.Stop();
        _ownTimer.Start();
    }

    /// <summary>A colour box of your own takes a swatch click too.</summary>
    private void OnOwnColorFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox box) SelectColor(box, t => box.Text = t);
    }

    private void OnOwnPart(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || !_own || _mods == null) return;
        _ownTimer.Stop();
        _ownTimer.Start();
    }

    private void OnPantShuffle(object sender, RoutedEventArgs e) { _pantShift++; _ownTimer.Stop(); _ownTimer.Start(); }
    private void OnSockShuffle(object sender, RoutedEventArgs e) { _sockShift++; _ownTimer.Stop(); _ownTimer.Start(); }

    /// <summary>Puts a change of your colours, lettering or parts on the model (and the pictures).</summary>
    private async Task ApplyOwn()
    {
        if (!_own || _studioSrc?.Own != true) return;
        bool colors = _ownColorsChanged;
        _ownColorsChanged = false;
        if (colors)
        {
            OwnKit.Fill(_studioSrc.Base, _ownColors[0]);
            var crest = OwnKit.Crest(_ownColors);
            foreach (var (spot, layer) in KitStamp.Brands(_studioSrc.Spots))
                if (OwnKit.Role(spot, layer) is OwnKit.LogoRole.Main or OwnKit.LogoRole.Shoulder) _studioSrc.Logos[layer] = (crest, 512, 512);
            DealColors();
            BuildZones();
            _artDirty = true;
            StudioTeamColors.ItemsSource = SwatchList();
            if (StudioTemplates.ItemsSource is List<StudioTemplateRow> rows && rows.Count > 0)
            {
                rows[0].Thumb = SheetThumb(_studioSrc.Base, _studioSrc.Size);
                PaintTemplateThumbs(rows);
            }
            if (_shown != null)
            {
                var gear = new Dictionary<string, ImageSource>();
                foreach (var g in new[] { "helmet", "glove_left", "glove_right" }) gear[g] = await OwnGear(g);
                _shown = _shown.Select(p => gear.TryGetValue(p.Garment, out var t) && t != null ? (p.Garment, p.Part, t) : p).ToList();
            }
        }
        await PaintOwnParts();
        PaintPartThumbs();
    }

    /// <summary>Sets your uniform (scripted runs and design files): "#MAIN,#SECOND,#THIRD", part numbers, "#FILL,#OUTLINE".</summary>
    public async Task SetOwn(string colors, int? font, int? pant, int? sock, string letter, int pantShift = 0, int sockShift = 0)
    {
        if (!_own) { ModeOwn.IsChecked = true; await Task.Delay(100); }
        await WaitStudio();
        if (colors?.Split(',').Select(HexColor.Parse).ToList() is { } c)
        {
            for (int i = 0; i < Math.Min(3, c.Count); i++)
                if (c[i] is int v) _ownColors[i] = v;
            _ownColorsChanged = true;
        }
        if (letter?.Split(',').Select(HexColor.Parse).ToList() is { Count: 2 } l)
        {
            if (l[0] is int f) _letterFill = f;
            if (l[1] is int o) _letterOutline = o;
        }
        _suppress = true;
        void Pick(ListBox list, int? n)
        {
            if (n is int k && (list.ItemsSource as IEnumerable<StudioTemplateRow>)?.FirstOrDefault(r => r.Template.Number == k) is { } row) list.SelectedItem = row;
        }
        Pick(OwnFonts, font);
        Pick(OwnPants, pant);
        Pick(OwnSocks, sock);
        _suppress = false;
        _pantShift = pantShift;
        _sockShift = sockShift;
        ShowOwnColors();
        await ApplyOwn();
    }

    /// <summary>The name and number drawn on the jersey (and the helmet's number).</summary>
    private KitLettering Lettering() => new(LetterName.Text.Trim(), new string(LetterNumber.Text.Where(char.IsDigit).ToArray()),
        LetterCaptain.SelectedIndex switch { 1 => "C", 2 => "A", _ => null });

    public void SetLettering(string name, string number, string captain)
    {
        _suppress = true;
        if (name != null) LetterName.Text = name;
        if (number != null) LetterNumber.Text = number;
        if (captain != null) LetterCaptain.SelectedIndex = captain.ToUpperInvariant() switch { "C" => 1, "A" => 2, _ => 0 };
        _suppress = false;
    }

    private void OnLetteringChanged(object sender, RoutedEventArgs e)
    {
        if (_suppress || _kit == null) return;
        _letterTimer.Stop();
        _letterTimer.Start();
    }

    public void Turn(double yaw) => Viewer.Face(yaw);
    public void Zoom(double factor, double up) => Viewer.Zoom(factor, up);
    private void OnFront(object sender, RoutedEventArgs e) => Viewer.Face(0);
    private void OnSide(object sender, RoutedEventArgs e) => Viewer.Face(90);
    private void OnBack(object sender, RoutedEventArgs e) => Viewer.Face(180);

    /// <summary>Reads the kit from the game (models, textures, helmet and glove colours) and opens it in the studio.</summary>
    private async Task ShowKit(Kit kit)
    {
        int version = ++_version;
        _kit = kit;
        _suppress = true;
        var tabs = _team?.Kits.Select(k => new KitTab { Kit = k, IsSelected = k == kit }).ToList();
        KitPicker.ItemsSource = tabs;
        KitPicker.SelectedItem = tabs?.FirstOrDefault(t => t.IsSelected);
        _suppress = false;
        TeamTitle.Text = UniformNames.Pretty(kit.Team);
        KitTitle.Text = $"{kit.League.ToUpperInvariant()}  ·  {UniformNames.Pretty(kit.Brand)}  ·  {UniformNames.Pretty(kit.Name)}  ·  the design starts from this kit";
        ViewEmpty.Visibility = Visibility.Collapsed;
        ViewBusy.Visibility = Visibility.Visible;
        ViewBusyText.Text = "Loading the kit from the game…";
        try
        {
            var index = _mods.Index;
            var gear = await Task.Run(() =>
            {
                try
                {
                    var db = new Legacy(index).OpenDb(KitEquipment.Database);
                    var g = KitEquipment.Gear(db, kit);
                    var colors = new List<int>();
                    if (g is { } gg)
                    {
                        var t = db.Table("stockteamcolorstable");
                        var f = t.Fields.ToDictionary(x => x.Name);
                        for (int r = 0; r < t.WrittenRecords; r++)
                            if (!db.IsInvalid(t, r) && (int)db.Get(t, r, f["team"]) == gg.TeamIndex) colors.Add((int)db.Get(t, r, f["color"]));
                    }
                    return (Helmet: g?.Helmet?.Zones, Gloves: g?.Gloves?.Zones, Colors: colors);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException) { return (null, null, new List<int>()); }
            });
            if (version != _version) return;
            _helmetZones = gear.Helmet;
            _gloveZones = gear.Gloves;
            _teamColors = gear.Colors;
            var parts = await KitScene.BuildAsync(index, _textures, kit, Lettering(),
                g => KitScene.GearAsync(index, kit, g, _helmetZones, _gloveZones, Lettering().Number), TextureSize, () => version != _version);
            if (parts == null || version != _version) return;
            _shown = parts;
            Viewer.Show(parts.Select(p => (p.Part, p.Tex)));
            ViewBusy.Visibility = Visibility.Collapsed;
            await EnsureStudio();
        }
        catch (Exception ex)
        {
            ViewBusyText.Text = "Couldn't draw this kit: " + ex.Message;
        }
    }

    /// <summary>Redraws the helmet (its number follows the lettering), keeping the view.</summary>
    private async Task RedrawGear()
    {
        if (_shown == null || _kit == null) return;
        var helmet = await KitScene.GearAsync(_mods.Index, _kit, "helmet", _helmetZones, _gloveZones, Lettering().Number);
        _shown = _shown.Select(p => p.Garment == "helmet" ? (p.Garment, p.Part, helmet) : p).ToList();
    }

    // ================================================================ opening

    /// <summary>Reads the shown kit's jersey sources from the game (when it isn't the one already open) and draws it.</summary>
    private async Task EnsureStudio()
    {
        if (_kit == null || _studioLoading) return;
        if (_studioSrc?.Kit == _kit && _studioSrc.Own == _own) { ScheduleStudio(); return; }
        var kit = _kit;
        bool own = _own;
        var palette = _ownColors.ToArray();
        var font = _fontImg;
        _studioLoading = true;
        StudioWrite.IsEnabled = false;
        StudioInfo.Text = "Reading the jersey's base, stamp, logos and font from the game…";
        try
        {
            var index = _mods.Index;
            _templates ??= await Task.Run(() => JerseyStudio.Templates(index));
            var src = await Task.Run(() => own ? OwnKit.Source(index, kit, TextureSize, palette, font, PartSize) : JerseyStudio.LoadSource(index, kit, TextureSize));
            if (kit != _kit || own != _own) return;
            _studioSrc = src;
            _design = new StudioDesign { Spots = src.Spots.ToList() };
            _studioPainted = null;
            _studioImageFile = null;
            _artDirty = true;
            _shuffle = 0;
            StudioClearImage.Visibility = Visibility.Collapsed;
            StudioInfo.Text = own
                ? "A plain jersey in your main colour, with no team's art or logos. Pick a design, add lines and your own logos (PNG or DDS), " +
                  "then write it into a free kit folder and a team's jersey menu below. Nothing is written until you do."
                : $"Starting from {UniformNames.Pretty(kit.Team)} {UniformNames.Pretty(kit.Name)}: its base color, {src.Spots.Count} stamp hotspots and " +
                  $"{src.Logos.Count} branding logos, read from the game now. Changes show on the model; nothing is written until you write it.";
            var spots = KitStamp.Brands(src.Spots).Select(b => new SpotRow(b.Spot, b.Layer))
                .Concat(src.Spots.Where(s => s.Type == "font" && s.Sx > 0).Select(s => new SpotRow(s, -1))).ToList();
            StudioSpotList.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<SpotRow>(spots);
            SelectSpot(null);
            ((System.Collections.ObjectModel.ObservableCollection<LineRow>)LineList.ItemsSource).Clear();
            LinesEmpty.Visibility = Visibility.Visible;
            BuildTemplateRows(kit);
            BuildZones();
            StudioTeamColors.ItemsSource = SwatchList();
            await LoadSlotTargets();
            ScheduleStudio();
        }
        catch (Exception ex)
        {
            _studioSrc = null;
            StudioInfo.Text = "The studio can't open this kit: " + ex.Message;
            StudioSpotList.ItemsSource = null;
            StudioZones.ItemsSource = null;
            StudioTemplates.ItemsSource = null;
        }
        finally
        {
            _studioLoading = false;
            if (_kit != kit || _own != own) _ = EnsureStudio();
        }
    }

    /// <summary>The swatches: the palette, then white, black, gold and grey.</summary>
    private System.Collections.IEnumerable SwatchList() => Palette().Concat(new[] { 0xFFFFFF, 0x000000, 0xB9975B, 0x8A8D8F })
        .Distinct().Select(c => new { Hex = GearZone.Hex(c), Brush = HexColor.Brush(c) }).ToList();

    /// <summary>Your colours, or the team's colours from the database (with a fallback when it has fewer than two).</summary>
    private List<int> Palette()
    {
        if (_own) return _ownColors.ToList();
        var p = _teamColors.Distinct().ToList();
        if (p.Count == 0) p = new List<int> { 0x1F2A44, 0xC8102E };
        // jerseys almost always carry white: second, unless the team has a near-white already
        static bool Light(int c) => (c >> 16 & 255) + (c >> 8 & 255) + (c & 255) > 690;
        if (!p.Any(Light)) p.Insert(Math.Min(1, p.Count), 0xFFFFFF);
        return p;
    }

    private void BuildTemplateRows(Kit kit)
    {
        var rows = new List<StudioTemplateRow> { new() { Template = null, Own = _studioSrc.Own, Thumb = SheetThumb(_studioSrc.Base, _studioSrc.Size) } };
        rows.AddRange(_templates.Select(t => new StudioTemplateRow { Template = t }));
        _suppress = true;
        StudioTemplates.ItemsSource = rows;
        StudioTemplates.SelectedIndex = 0;
        _suppress = false;
        StudioTemplateCount.Text = $"{_templates.Count} designs";
        PaintTemplateThumbs(rows);
    }

    /// <summary>The design pictures, drawn in the palette as the zone maps come in.</summary>
    private void PaintTemplateThumbs(List<StudioTemplateRow> rows)
    {
        var palette = Palette();
        var index = _mods.Index;
        int gen = ++_tplGen;
        _ = Task.Run(async () =>
        {
            foreach (var row in rows.Skip(1))
            {
                if (gen != _tplGen) return;
                (sbyte[] Map, int Size, List<int> Order) z;
                lock (ThumbMaps)
                    ThumbMaps.TryGetValue(row.Template, out z);
                if (z.Map == null)
                {
                    try { z = JerseyStudio.ThumbZones(index, row.Template); }
                    catch { continue; }
                    lock (ThumbMaps) ThumbMaps[row.Template] = z;
                }
                var img = new byte[z.Size * z.Size * 4];
                for (int i = 0; i < z.Map.Length; i++)
                {
                    int zone = z.Map[i], c = zone < 0 ? 0x2A2F38 : palette[z.Order.IndexOf(zone) % palette.Count];
                    img[i * 4] = (byte)(c >> 16); img[i * 4 + 1] = (byte)(c >> 8); img[i * 4 + 2] = (byte)c; img[i * 4 + 3] = 255;
                }
                var bmp = TextureService.ToBitmap(img, z.Size, z.Size, Channel.Rgb);
                await Dispatcher.InvokeAsync(() => row.Thumb = bmp);
            }
        });
    }

    /// <summary>The front of a jersey sheet, small, for the picker.</summary>
    private static BitmapSource SheetThumb(byte[] rgba, int size)
    {
        const int n = 64;
        var img = new byte[n * n * 4];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            int sx = (int)(size * (0.04 + 0.92 * (x + 0.5) / n)), sy = Math.Min(size - 1, (int)(size * (0.34 + 0.66 * (y + 0.5) / n)));
            Array.Copy(rgba, (sy * size + sx) * 4, img, (y * n + x) * 4, 4);
            img[(y * n + x) * 4 + 3] = 255;
        }
        return TextureService.ToBitmap(img, n, n, Channel.Rgb);
    }

    /// <summary>The colour rows: the design's zones (largest first), then the fabric regions.</summary>
    private void BuildZones()
    {
        var rows = new List<StudioZone>();
        if (_design.Pattern is { } p)
        {
            int i = 0;
            foreach (var (zone, share) in p.Zones)
            {
                _design.ZoneColors.TryGetValue(zone, out int c);
                rows.Add(new StudioZone(zone, 0, $"Colour {++i}", $"{(share < 0.01 ? "under 1%" : share.ToString("P0"))} of the sheet  ·  mask {zone / 3 + 1} {"RGB"[zone % 3]}",
                    _design.ZoneColors.ContainsKey(zone) ? c : null, OnStudioZoneChanged));
            }
        }
        foreach (int r in new[] { 3, 4, 2 })
            rows.Add(new StudioZone(-1, r, JerseyStudio.RegionNames[r], "fabric region  ·  blank keeps the art",
                _design.RegionColors.TryGetValue(r, out int rc) ? rc : null, OnStudioZoneChanged));
        rows.Add(new StudioZone(-1, 1, "Whole body", "fabric region  ·  a flat fill under the lines and logos",
            _design.RegionColors.TryGetValue(1, out int bc) ? bc : null, OnStudioZoneChanged));
        StudioZones.ItemsSource = rows;
        BuildCollar();
    }

    // ================================================================ art

    private async void OnStudioTemplate(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || _studioSrc == null || StudioTemplates.SelectedItem is not StudioTemplateRow row) return;
        await UseTemplate(row.Template);
    }

    private async Task UseTemplate(StudioTemplate t)
    {
        if (t == null)
        {
            _design.Pattern = null;
            _design.ZoneColors.Clear();
        }
        else
        {
            StudioStatus.Text = $"Reading {t.Label} from the game…";
            var index = _mods.Index;
            try
            {
                var p = await Task.Run(() => JerseyStudio.LoadPattern(index, t, TextureSize));
                if (_design == null) return;
                _design.Pattern = p;
                DealColors();
            }
            catch (Exception ex)
            {
                StudioStatus.Text = $"Can't read {t.Label}: {ex.Message}";
                return;
            }
        }
        BuildZones();
        _artDirty = true;
        ScheduleStudio();
    }

    /// <summary>Gives the design's zones the team colours, largest zone first (moved along by Shuffle).</summary>
    private void DealColors()
    {
        if (_design?.Pattern == null) return;
        var palette = Palette();
        _design.ZoneColors.Clear();
        for (int i = 0; i < _design.Pattern.Zones.Count; i++)
            _design.ZoneColors[_design.Pattern.Zones[i].Zone] = palette[(i + _shuffle) % palette.Count];
    }

    private void OnStudioShuffle(object sender, RoutedEventArgs e)
    {
        if (_design?.Pattern == null) return;
        _shuffle++;
        DealColors();
        BuildZones();
        _artDirty = true;
        ScheduleStudio();
    }

    private void OnStudioZoneChanged(StudioZone z)
    {
        if (_design == null) return;
        if (z.Zone >= 0)
        {
            if (z.Value is int c) _design.ZoneColors[z.Zone] = c;
            else if (z.IsBlank) _design.ZoneColors.Remove(z.Zone);
        }
        else if (z.Zone == CollarZone)
        {
            if (z.Value is int c) _design.CollarColors[(CollarPart)z.Region] = c;
            else if (z.IsBlank) _design.CollarColors.Remove((CollarPart)z.Region);
        }
        else
        {
            if (z.Value is int c) _design.RegionColors[z.Region] = c;
            else if (z.IsBlank) _design.RegionColors.Remove(z.Region);
        }
        _artDirty = true;
        ScheduleStudio();
    }

    /// <summary>Marks a colour row or line as the one a swatch fills.</summary>
    private void SelectColor(object row, Action<string> target)
    {
        foreach (var r in StudioZoneRows.Concat(CollarRows)) r.IsSelected = r == row;
        foreach (var l in LineRows) l.IsSelected = l == row;
        foreach (var s in CollarStripeRows) s.IsSelected = s == row;
        _colorTarget = target;
    }

    // ================================================================ tabs

    private void OnStudioTab(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Uid: { Length: > 0 } tab }) ShowTab(tab);
    }

    /// <summary>Shows one of the studio's tabs: design, colours, collar, lines, logos or menu.</summary>
    public void ShowTab(string tab)
    {
        if (TabDesign == null) return;
        var tabs = new (string Name, FrameworkElement Panel)[] { ("design", TabDesign), ("colours", TabColours), ("collar", TabCollar), ("lines", TabLines), ("logos", TabLogos), ("menu", TabMenu) };
        int was = Array.FindIndex(tabs, t => t.Panel.Visibility == Visibility.Visible), now = Array.FindIndex(tabs, t => t.Name == tab);
        foreach (var (name, panel) in tabs)
            panel.Visibility = name == tab ? Visibility.Visible : Visibility.Collapsed;
        StudioScroller.ScrollToTop();
        if (now >= 0 && now != was) Motion.Enter(tabs[now].Panel, dx: was < 0 ? 0 : now > was ? 28 : -28, dy: 0, ms: 240);
        foreach (var rb in FindTabs()) if (rb.Uid == tab && rb.IsChecked != true) rb.IsChecked = true;
    }

    private IEnumerable<RadioButton> FindTabs()
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(this);
        while (stack.Count > 0)
        {
            var d = stack.Pop();
            if (d is RadioButton { GroupName: "StudioTab" } rb) yield return rb;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++) stack.Push(VisualTreeHelper.GetChild(d, i));
        }
    }

    // ================================================================ collar

    /// <summary>StudioZone.Zone for a collar piece's row (its Region is the CollarPart).</summary>
    private const int CollarZone = -2;

    private IEnumerable<StudioZone> CollarRows => CollarZones.ItemsSource as IEnumerable<StudioZone> ?? Enumerable.Empty<StudioZone>();
    private IEnumerable<CollarStripeRow> CollarStripeRows => CollarStripeList.ItemsSource as IEnumerable<CollarStripeRow> ?? Enumerable.Empty<CollarStripeRow>();

    private void BuildCollar()
    {
        CollarZones.ItemsSource = CollarMap.Parts.Select(p => new StudioZone(CollarZone, (int)p.Part, p.Name, p.Hint,
            _design.CollarColors.TryGetValue(p.Part, out int c) ? c : null, OnStudioZoneChanged)).ToList();
        var stripes = new System.Collections.ObjectModel.ObservableCollection<CollarStripeRow>();
        foreach (var s in _design.CollarStripes) stripes.Add(new CollarStripeRow(s, OnCollarStripeChanged));
        CollarStripeList.ItemsSource = stripes;
        CollarStripesEmpty.Visibility = stripes.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    public void SetCollarColor(CollarPart part, string hex)
    {
        if (CollarRows.FirstOrDefault(r => r.Region == (int)part) is { } row) row.Text = hex;
    }

    public CollarStripeRow AddCollarStripe(CollarStripe s)
    {
        if (_design == null || CollarStripeList.ItemsSource is not System.Collections.ObjectModel.ObservableCollection<CollarStripeRow> list) return null;
        var row = new CollarStripeRow(s, OnCollarStripeChanged);
        list.Add(row);
        CollarStripesEmpty.Visibility = Visibility.Collapsed;
        SelectColor(row, t => row.ColorText = t);
        OnCollarStripeChanged(row);
        return row;
    }

    private void OnCollarStripeChanged(CollarStripeRow row)
    {
        if (_design == null) return;
        _design.CollarStripes = CollarStripeRows.Select(r => r.ToStripe()).Where(s => s != null).ToList();
        _artDirty = true;
        ScheduleStudio();
    }

    private void OnAddCollarStripe(object sender, RoutedEventArgs e)
    {
        var p = Palette();
        var last = CollarStripeRows.LastOrDefault();
        double from = last != null ? Math.Min(90, last.From + last.Width + 5) : 80;
        AddCollarStripe(new CollarStripe(from, 12, p.Count > 1 ? p[1] : 0xFFFFFF));
    }

    private void OnRemoveCollarStripe(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not CollarStripeRow row || CollarStripeList.ItemsSource is not System.Collections.ObjectModel.ObservableCollection<CollarStripeRow> list) return;
        list.Remove(row);
        CollarStripesEmpty.Visibility = list.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        OnCollarStripeChanged(row);
    }

    private void OnCollarStripeFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is CollarStripeRow row) SelectColor(row, t => row.ColorText = t);
    }

    /// <summary>Ready-made neck bands in the team colours: piping on top, two-tone, or three stripes.</summary>
    private void OnCollarPreset(object sender, RoutedEventArgs e)
    {
        if (_design == null || CollarStripeList.ItemsSource is not System.Collections.ObjectModel.ObservableCollection<CollarStripeRow> list) return;
        var p = Palette();
        int c0 = p[0], c1 = p.Count > 1 ? p[1] : 0xFFFFFF, c2 = p.Count > 2 ? p[2] : c1;
        list.Clear();
        var preset = (sender as FrameworkElement)?.Tag as string;
        var stripes = preset switch
        {
            "piping" => new[] { new CollarStripe(84, 16, c1) },
            "twotone" => new[] { new CollarStripe(50, 50, c1) },
            "triple" => new[] { new CollarStripe(20, 14, c1), new CollarStripe(43, 14, c2), new CollarStripe(66, 14, c1) },
            "edges" => new[] { new CollarStripe(0, 12, c1), new CollarStripe(88, 12, c1) },
            _ => Array.Empty<CollarStripe>(),
        };
        foreach (var s in stripes) AddCollarStripe(s);
        if (stripes.Length == 0) { CollarStripesEmpty.Visibility = Visibility.Visible; OnCollarStripeChanged(null); }
        foreach (var part in new[] { CollarPart.Front, CollarPart.RingFront, CollarPart.RingBack, CollarPart.NeckFront, CollarPart.NeckBack })
            SetCollarColor(part, GearZone.Hex(c0));
    }

    /// <summary>Turns the model to face the collar up close.</summary>
    private void OnCollarView(object sender, RoutedEventArgs e)
    {
        Viewer.Face((sender as FrameworkElement)?.Tag as string == "back" ? 180 : 0);
        Viewer.Zoom(3.6, 0.42);
    }

    private void OnStudioZoneClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is StudioZone z) SelectColor(z, t => z.Text = t);
    }

    private void OnStudioZoneFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is StudioZone z) SelectColor(z, t => z.Text = t);
    }

    private void OnSwatch(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string hex) _colorTarget?.Invoke(hex);
    }

    private async void OnStudioImage(object sender, RoutedEventArgs e)
    {
        if (_studioSrc == null) return;
        bool legacy = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var dlg = new OpenFileDialog { Title = legacy ? "Legacy jersey texture (converted to NHL 27's layout)" : "Jersey texture in the NHL 27 layout (PNG or DDS)", Filter = ImageLoader.Filter };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        await UseImage(dlg.FileName, legacy);
    }

    /// <summary>Uses a whole jersey image as the art (a legacy one is converted first).</summary>
    public async Task UseImage(string file, bool legacy)
    {
        if (_studioSrc == null) return;
        int size = _studioSrc.Size;
        try
        {
            var rgba = await Task.Run(() =>
            {
                var (src, w, h) = ImageLoader.Load(file);
                return legacy ? LegacyJersey.Convert(src, w, h, size) : JerseyStudio.Resize(src, w, h, size);
            });
            _design.Image = rgba;
            _studioImageFile = file;
            StudioClearImage.Visibility = Visibility.Visible;
            _artDirty = true;
            ScheduleStudio();
        }
        catch (Exception ex)
        {
            StudioStatus.Text = $"Can't use {Path.GetFileName(file)}: {ex.Message}";
        }
    }

    // ================================================================ from a photo

    private SpotRow _photoCrest;

    private PhotoJerseyWindow PhotoWindow()
    {
        if (_studioSrc?.Shape is not { } shape)
        {
            StudioStatus.Text = "The jersey model couldn't be read, so a photo can't be measured against it.";
            return null;
        }
        return new PhotoJerseyWindow((shape.PitY - shape.Hem) * 100, (shape.Cuff + shape.Shoulder) * 100) { Owner = Window.GetWindow(this) };
    }

    private async void OnStudioPhoto(object sender, RoutedEventArgs e)
    {
        if (_studioSrc == null || PhotoWindow() is not { } w) return;
        if (w.ShowDialog() == true && w.Result != null) await ApplyPhoto(w.Result);
    }

    /// <summary>
    /// Makes the jersey what the photo converter read: the kit's own art filled flat (body, shoulders, sides and gussets
    /// in the body colour, the collar in its own), its lines, the crest painted into the base where the crest goes, and
    /// every branding logo of the stamp switched off.
    /// </summary>
    public async Task ApplyPhoto(PhotoJersey p)
    {
        await StudioUseTemplate("kit");
        OnStudioClearImage(null, null);
        string body = GearZone.Hex(p.Body), collar = p.Collar is int c ? GearZone.Hex(c) : body;
        var fills = new Dictionary<int, string> { [1] = body, [2] = body, [3] = p.Yoke is int y ? GearZone.Hex(y) : body, [4] = body };
        foreach (var z in StudioZoneRows.Where(z => z.Zone == -1))
            z.Text = fills.TryGetValue(z.Region, out var hex) ? hex : "";
        foreach (var z in CollarRows)
            z.Text = (CollarPart)z.Region switch
            {
                _ when p.Collar != null => collar,
                _ => body,
            };

        var lines = (System.Collections.ObjectModel.ObservableCollection<LineRow>)LineList.ItemsSource;
        lines.Clear();
        foreach (var l in p.Lines) AddLine(l.Place, l);
        LinesEmpty.Visibility = LineRows.Any() ? Visibility.Collapsed : Visibility.Visible;
        _design.Lines = LineRows.Select(l => l.ToLine()).Where(l => l != null).ToList();

        var spots = (System.Collections.ObjectModel.ObservableCollection<SpotRow>)StudioSpotList.ItemsSource;
        if (_photoCrest != null) spots.Remove(_photoCrest);
        _photoCrest = null;
        var crestSpot = Spots.FirstOrDefault(r => r.IsBrand && r.Layer == 0)?.Spot;
        foreach (var row in Spots.Where(r => r.IsBrand).ToList()) row.Spot = row.Spot with { Enabled = false };
        if (p.Crest != null)
        {
            string file = Path.Combine(StudioFolder(_kit), $"{_kit.Name}_photo_crest.png");
            await Task.Run(() => File.WriteAllBytes(file, ImageFiles.Png(p.Crest, p.CrestWidth, p.CrestHeight)));
            var ci = CultureInfo.InvariantCulture;
            string place = crestSpot != null ? string.Format(ci, "{0},{1},{2},{3}", crestSpot.Tx, crestSpot.Ty, Math.Max(crestSpot.Sx, crestSpot.Sy), crestSpot.R) : null;
            await AddLogo(file, null, place);
            _photoCrest = Spots.LastOrDefault(r => r.IsDecal);
        }
        DrawSpots();
        _artDirty = true;
        ScheduleStudio();
        StudioStatus.Text = $"From the photo: {p.Lines.Count} line(s)" + (p.Crest != null ? ", the crest painted in" : "") + "; branding logos off.";
    }

    /// <summary>
    /// Scripted runs: "PHOTO|body:x0,y0,x1,y1|sleeve:…|crest:x0,y0,x1,y1|round|collar:x,y|yoke:x,y", photos split by ';'.
    /// <paramref name="show"/> leaves the converter open (for a picture of it) instead of using what it read.
    /// </summary>
    public async Task StudioPhoto(string spec, bool show)
    {
        await WaitStudio();
        if (PhotoWindow() is not { } w) return;
        var ci = CultureInfo.InvariantCulture;
        foreach (var photo in spec.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = photo.Split('|');
            w.Open(parts[0]);
            if (parts.Contains("round")) w.RoundCrest.IsChecked = true;
            foreach (var m in parts.Skip(1).Where(x => x.Contains(':')))
            {
                var kv = m.Split(':');
                w.Mark(kv[0], kv[1].Split(',').Select(x => double.Parse(x, ci)).ToArray());
            }
        }
        if (show) { w.Show(); return; }
        if (w.Result != null) await ApplyPhoto(w.Result);
    }

    private void OnStudioClearImage(object sender, RoutedEventArgs e)
    {
        if (_design == null) return;
        _design.Image = null;
        _studioImageFile = null;
        StudioClearImage.Visibility = Visibility.Collapsed;
        _artDirty = true;
        ScheduleStudio();
    }

    // ================================================================ lines

    private void OnAddBodyLine(object sender, RoutedEventArgs e) => AddLine(LinePlace.Body);
    private void OnAddSleeveLine(object sender, RoutedEventArgs e) => AddLine(LinePlace.Sleeves);

    /// <summary>A new line in the team's second colour with the first as its edge, past the last line of its kind.</summary>
    public LineRow AddLine(LinePlace place, StudioLine line = null)
    {
        if (_design == null) return null;
        var p = Palette();
        if (line == null)
        {
            var last = LineRows.Where(l => l.PlaceIndex == (int)place).LastOrDefault();
            double from = last != null ? last.From + last.Width + Math.Max(1.5, last.EdgeWidth * 2 + 1) : place == LinePlace.Body ? 10 : 6;
            line = new StudioLine(place, from, place == LinePlace.Body ? 5 : 4, p.Count > 1 ? p[1] : 0xFFFFFF, p[0] == (p.Count > 1 ? p[1] : 0xFFFFFF) ? null : 0x8A8D8F, 0);
        }
        var row = new LineRow(line, OnLineChanged);
        ((System.Collections.ObjectModel.ObservableCollection<LineRow>)LineList.ItemsSource).Add(row);
        LinesEmpty.Visibility = Visibility.Collapsed;
        SelectColor(row, t => row.ColorText = t);
        OnLineChanged(row);
        return row;
    }

    private void OnLineChanged(LineRow row)
    {
        if (_design == null) return;
        _design.Lines = LineRows.Select(l => l.ToLine()).Where(l => l != null).ToList();
        _artDirty = true;
        ScheduleStudio();
    }

    private void OnRemoveLine(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not LineRow row) return;
        ((System.Collections.ObjectModel.ObservableCollection<LineRow>)LineList.ItemsSource).Remove(row);
        LinesEmpty.Visibility = LineRows.Any() ? Visibility.Collapsed : Visibility.Visible;
        OnLineChanged(row);
    }

    private void OnCopyLine(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not LineRow row || row.ToLine() is not { } l) return;
        AddLine(l.Place, l with { From = l.From + l.Width + Math.Max(1.5, l.EdgeWidth * 2 + 1) });
    }

    private void OnLineRowDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is LineRow row && !row.IsSelected) SelectColor(row, t => row.ColorText = t);
    }

    private void OnLineImage(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not LineRow row) return;
        var dlg = new OpenFileDialog { Title = "An image to wrap round as this line (PNG or DDS; a strip template's magenta guides are left out)", Filter = ImageLoader.Filter };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        SetLineImage(row, dlg.FileName);
    }

    private bool SetLineImage(LineRow row, string file, int repeat = 1, bool mirror = true)
    {
        try
        {
            var (rgba, w, h) = ImageLoader.Load(file);
            row.Stripe = StudioStripe.From(rgba, w, h, file, repeat, mirror);
            return true;
        }
        catch (Exception ex)
        {
            StudioStatus.Text = $"Can't use {Path.GetFileName(file)}: {ex.Message}";
            return false;
        }
    }

    private void OnLineClearImage(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is LineRow row) row.Stripe = null;
    }

    /// <summary>Saves a guide to paint this line's image on: the way round at its height, at 10 px a cm.</summary>
    private void OnLineTemplate(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not LineRow row || _studioSrc?.Shape is not { } shape) return;
        var place = (LinePlace)row.PlaceIndex;
        var dlg = new SaveFileDialog
        {
            Title = "Save a strip template", Filter = "PNG|*.png",
            InitialDirectory = StudioFolder(_kit),
            FileName = string.Create(CultureInfo.InvariantCulture, $"{_kit.Name}_strip_{place.ToString().ToLowerInvariant()}_{row.Width:0.##}cm{(row.Mirror ? "" : "_round")}.png"),
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        SaveStripTemplate(row, dlg.FileName);
    }

    public void SaveStripTemplate(LineRow row, string file)
    {
        if (_studioSrc?.Shape is not { } shape) return;
        var t = shape.StripTemplate((LinePlace)row.PlaceIndex, row.From, row.Width, row.Mirror);
        File.WriteAllBytes(file, ImageFiles.Png(t.Rgba, t.Width, t.Height));
        StudioStatus.Text = string.Create(CultureInfo.InvariantCulture,
            $"Strip template saved: {t.Width} × {t.Height} px at 10 px a cm. It's {t.Girth:0} cm round there; the magenta guides (front, sides, back, a tick every 5 cm) are left out when it's loaded back.");
    }

    /// <summary>Saves the lines on their own over a see-through sheet in the jersey texture's layout.</summary>
    private void OnLinesPng(object sender, RoutedEventArgs e)
    {
        if (_studioSrc?.Shape == null || _design == null) return;
        var dlg = new SaveFileDialog { Title = "Save the lines on their own", Filter = "PNG|*.png", InitialDirectory = StudioFolder(_kit), FileName = $"{_kit.Name}_lines.png" };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        SaveLinesPng(dlg.FileName);
    }

    public void SaveLinesPng(string file)
    {
        var shape = _studioSrc.Shape;
        var layer = shape.LinesLayer(_design.Lines);
        File.WriteAllBytes(file, ImageFiles.Png(layer, shape.Map.Size, shape.Map.Size));
        StudioStatus.Text = $"The lines on their own saved ({shape.Map.Size} × {shape.Map.Size}, see-through elsewhere).";
    }

    /// <summary>Ctrl+drag on the jersey moves the selected line there: its middle where the mouse is, on the body or a sleeve.</summary>
    private bool PickLine(MeshPart part, Point uv)
    {
        var row = LineRows.FirstOrDefault(l => l.IsSelected);
        if (row == null || _studioSrc?.Shape is not { } shape || _shown == null || !_shown.Any(s => s.Part == part && s.Garment == "jersey")) return false;
        int size = shape.Map.Size;
        int tx = Math.Clamp((int)((uv.X - Math.Floor(uv.X)) * size), 0, size - 1), ty = Math.Clamp((int)((uv.Y - Math.Floor(uv.Y)) * size), 0, size - 1);
        var p = shape.Map.Position;
        int i = ty * size + tx;
        if (float.IsNaN(p[i * 3])) return true;
        double x = p[i * 3], y = p[i * 3 + 1];
        bool sleeve = shape.OnSleeve(x, y);
        double d = sleeve ? (shape.Cuff - shape.Along(x, y)) * 100 : (y - shape.Hem) * 100;
        row.PlaceIndex = sleeve ? 1 : 0;
        row.From = Math.Max(0, d - row.Width / 2);
        StudioStatus.Text = string.Create(CultureInfo.InvariantCulture, $"Line moved: {row.From:0.##} cm up from the {(sleeve ? "cuff" : "hem")}.");
        return true;
    }

    private void OnLineColorFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LineRow row } box) return;
        if (box.Tag as string == "edge") SelectColor(row, t => row.EdgeColorText = t);
        else SelectColor(row, t => row.ColorText = t);
    }

    // ================================================================ drawing

    private void ScheduleStudio()
    {
        _studioTimer.Stop();
        _studioTimer.Start();
    }

    /// <summary>A copy of the design the drawing thread can read while the UI goes on editing.</summary>
    private StudioDesign Snapshot()
    {
        var stamp = Spots.Where(s => !s.IsDecal).ToList();
        var d = new StudioDesign
        {
            Pattern = _design.Pattern, Image = _design.Image,
            Spots = stamp.Select(s => s.Spot).Concat(_design.Spots.Where(s => stamp.All(r => r.Original.Id != s.Id))).OrderBy(s => s.Id).ToList(),
            Lines = _design.Lines.ToList(),
            Decals = Spots.Where(s => s.IsDecal && s.Spot.Enabled).Select(s => s.Decal with { Place = s.Spot }).ToList(),
        };
        foreach (var kv in _design.ZoneColors) d.ZoneColors[kv.Key] = kv.Value;
        foreach (var kv in _design.RegionColors) d.RegionColors[kv.Key] = kv.Value;
        foreach (var kv in _design.CollarColors) d.CollarColors[kv.Key] = kv.Value;
        d.CollarStripes = _design.CollarStripes.ToList();
        foreach (var kv in _design.Logos) d.Logos[kv.Key] = kv.Value;
        foreach (var kv in _design.LogoFiles) d.LogoFiles[kv.Key] = kv.Value;
        return d;
    }

    /// <summary>Paints the base (when the art changed), stamps the logos and lettering, and shows it on the model and the sheet.</summary>
    private async Task RedrawStudio()
    {
        if (_studioSrc == null || _design == null || _shown == null) return;
        int version = ++_studioVersion;
        var src = _studioSrc;
        var d = Snapshot();
        _design.Spots = d.Spots;
        bool art = _artDirty;
        _artDirty = false;
        var painted = _studioPainted;
        var text = Lettering();
        var result = await Task.Run(() =>
        {
            if (art || painted == null) painted = JerseyStudio.PaintBase(src, d);
            var drawn = JerseyStudio.Draw(src, d, painted, text);
            return (Painted: painted, Bitmap: TextureService.ToBitmap(drawn, src.Size, src.Size, Channel.Rgb));
        });
        if (version != _studioVersion || src != _studioSrc || _shown == null) { if (art) _artDirty = true; return; }
        _studioPainted = result.Painted;
        Viewer.Show(_shown.Select(p => (p.Part, p.Garment == "jersey" ? (ImageSource)result.Bitmap : p.Tex)));
        StudioSheet.Source = result.Bitmap;
        DrawSpots();
        StudioButtons();
    }

    /// <summary>What the design changes, for the status line (empty when nothing).</summary>
    private List<string> StudioChanges()
    {
        var list = new List<string>();
        if (_design == null || _studioSrc == null) return list;
        if (_studioSrc.Own) list.Add("your own uniform");
        if (_design.Image != null) list.Add("an image as the art");
        if (_design.Pattern != null) list.Add(_design.Pattern.Template.Label.ToLowerInvariant());
        if (_design.RegionColors.Count > 0) list.Add($"{_design.RegionColors.Count} region fill(s)");
        if (_design.Lines.Count > 0) list.Add($"{_design.Lines.Count} line(s)");
        if (_design.CollarColors.Count + _design.CollarStripes.Count > 0) list.Add("the collar");
        int added = Spots.Count(s => s.IsDecal && s.Spot.Enabled);
        if (added > 0) list.Add($"{added} added logo(s)");
        int moved = Spots.Count(s => !s.IsDecal && s.Spot != s.Original);
        if (moved > 0) list.Add($"{moved} hotspot(s) changed");
        if (_design.Logos.Count > 0) list.Add($"{_design.Logos.Count} logo(s) replaced");
        return list;
    }

    /// <summary>True when the design paints the base (not just moves or swaps the stamp's logos).</summary>
    private static bool PaintsArt(StudioDesign d) => d.Image != null || d.Pattern != null || d.RegionColors.Count > 0 || d.Lines.Count > 0 || d.Decals.Count > 0
        || d.CollarColors.Count > 0 || d.CollarStripes.Count > 0;

    private void StudioButtons()
    {
        var changes = StudioChanges();
        bool errors = StudioZoneRows.Any(z => z.HasError) || LineRows.Any(l => l.HasError);
        StudioWrite.IsEnabled = !_busy && changes.Count > 0 && !errors;
        StudioStatus.Text = errors ? "A colour isn't a hex value (like #1A2B3C)." :
            changes.Count == 0 ? "Pick a design, colour its zones, add lines and logos, or drag the logos and lettering on the sheet." :
            _studioSrc?.Own == true ? "Not written yet: " + string.Join(", ", changes) + ". Write it into a team's jersey menu below." :
            "Not written yet: " + string.Join(", ", changes) + ".";
        SlotButtons();
    }

    // ================================================================ hotspots on the sheet

    private void DrawSpots()
    {
        StudioSpots.Children.Clear();
        var accent = (Brush)FindResource("Accent");
        var patch = (Brush)FindResource("Patch");
        foreach (var row in Spots)
        {
            var s = row.Spot;
            if (s.Sx <= 0 || s.Sy <= 0 || (!s.Enabled && !row.IsSelected)) continue;
            double w = Math.Max(4, s.Sx * Sheet), h = Math.Max(4, s.Sy * Sheet);
            var rect = new Rectangle
            {
                Width = w, Height = h,
                Stroke = row.IsSelected ? accent : row.IsDecal ? patch : new SolidColorBrush(Color.FromArgb(row.Enabled ? (byte)170 : (byte)90, 255, 255, 255)),
                StrokeThickness = row.IsSelected ? 2 : 1,
                StrokeDashArray = row.Enabled ? null : new DoubleCollection { 3, 2 },
                Fill = row.IsSelected ? new SolidColorBrush(Color.FromArgb(40, 80, 160, 255)) : Brushes.Transparent,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(-(s.R + 180)),
            };
            Canvas.SetLeft(rect, s.Tx * Sheet - w / 2);
            Canvas.SetTop(rect, (1 - s.Ty) * Sheet - h / 2);
            StudioSpots.Children.Add(rect);
        }
    }

    /// <summary>The spot under a point of the sheet (the smallest when they overlap).</summary>
    private SpotRow SpotAt(Point p)
    {
        SpotRow best = null;
        double bestArea = double.MaxValue;
        foreach (var row in Spots)
        {
            var s = row.Spot;
            if (s.Sx <= 0) continue;
            double a = -(s.R + 180) * Math.PI / 180, dx = p.X - s.Tx * Sheet, dy = p.Y - (1 - s.Ty) * Sheet;
            double u = dx * Math.Cos(a) + dy * Math.Sin(a), v = -dx * Math.Sin(a) + dy * Math.Cos(a);
            double hw = Math.Max(6, s.Sx * Sheet / 2), hh = Math.Max(6, s.Sy * Sheet / 2);
            if (Math.Abs(u) <= hw && Math.Abs(v) <= hh && s.Sx * s.Sy < bestArea) { best = row; bestArea = s.Sx * s.Sy; }
        }
        return best;
    }

    private void OnSheetDown(object sender, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(StudioSpots);
        var row = SpotAt(p);
        SelectSpot(row);
        if (row == null) return;
        _dragOffset = new Point(p.X - row.Spot.Tx * Sheet, p.Y - (1 - row.Spot.Ty) * Sheet);
        StudioSpots.CaptureMouse();
    }

    private void OnSheetMove(object sender, MouseEventArgs e)
    {
        if (_dragOffset is not { } off || _spot == null || e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(StudioSpots);
        double tx = Math.Clamp((p.X - off.X) / Sheet, 0, 1), ty = Math.Clamp(1 - (p.Y - off.Y) / Sheet, 0, 1);
        UpdateSpot(_spot.Spot with { Tx = Math.Round(tx, 4), Ty = Math.Round(ty, 4) });
    }

    private void OnSheetUp(object sender, MouseButtonEventArgs e)
    {
        _dragOffset = null;
        StudioSpots.ReleaseMouseCapture();
    }

    private void OnSheetWheel(object sender, MouseWheelEventArgs e)
    {
        var row = SpotAt(e.GetPosition(StudioSpots)) ?? _spot;
        if (row == null) return;
        if (row != _spot) SelectSpot(row);
        double k = Math.Pow(1.06, e.Delta / 120.0);
        var s = row.Spot;
        UpdateSpot(s with { Sx = Math.Round(Math.Clamp(s.Sx * k, 0.005, 0.6), 4), Sy = Math.Round(Math.Clamp(s.Sy * k, 0.005, 0.6), 4) });
        e.Handled = true;
    }

    private void OnSpotChip(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is SpotRow row) SelectSpot(row);
    }

    private void SelectSpot(SpotRow row)
    {
        foreach (var r in Spots) r.IsSelected = r == row;
        _spot = row;
        SpotEditor.Visibility = row == null ? Visibility.Collapsed : Visibility.Visible;
        if (row != null)
        {
            SpotTitle.Text = row.Label;
            SpotMeta.Text = row.IsDecal ? $"added logo  ·  {row.Name ?? Path.GetFileName(row.Decal.File)}  ·  painted into the base"
                : row.IsBrand ? $"branding layer {row.Layer}" + (_design?.Logos.ContainsKey(row.Layer) == true ? "  ·  new image" : "") + $"  ·  hotspot {row.Original.Id}"
                : $"lettering ({row.Original.Name})  ·  hotspot {row.Original.Id}";
            SpotReplace.Visibility = row.IsBrand || row.IsDecal ? Visibility.Visible : Visibility.Collapsed;
            SpotCrest.Visibility = row.IsBrand ? Visibility.Visible : Visibility.Collapsed;
            SpotResetButton.Visibility = row.IsDecal ? Visibility.Collapsed : Visibility.Visible;
            SpotRemove.Visibility = row.IsDecal ? Visibility.Visible : Visibility.Collapsed;
            SyncSpotEditor();
        }
        DrawSpots();
    }

    private void SyncSpotEditor()
    {
        if (_spot == null) return;
        var s = _spot.Spot;
        _suppress = true;
        SpotEnabled.IsChecked = s.Enabled;
        SpotX.Value = s.Tx;
        SpotY.Value = 1 - s.Ty;
        SpotSize.Value = s.Sx;
        SpotTurn.Value = ((s.R % 360) + 360) % 360;
        _suppress = false;
        SpotXText.Text = s.Tx.ToString("0.000");
        SpotYText.Text = (1 - s.Ty).ToString("0.000");
        SpotSizeText.Text = s.Sx.ToString("0.000");
        SpotTurnText.Text = $"{SpotTurn.Value:0}°";
    }

    private void UpdateSpot(StampHotspot s)
    {
        if (_spot == null) return;
        _spot.Spot = s;
        // an added logo is painted into the base
        if (_spot.IsDecal) _artDirty = true;
        SyncSpotEditor();
        DrawSpots();
        ScheduleStudio();
    }

    private void OnSpotSlider(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppress || _spot == null) return;
        var s = _spot.Spot;
        double ratio = s.Sx > 0 ? s.Sy / s.Sx : 1;
        UpdateSpot(s with
        {
            Tx = Math.Round(SpotX.Value, 4), Ty = Math.Round(1 - SpotY.Value, 4),
            Sx = Math.Round(SpotSize.Value, 4), Sy = Math.Round(SpotSize.Value * ratio, 4),
            R = Math.Round(SpotTurn.Value),
        });
    }

    private void OnSpotEnabled(object sender, RoutedEventArgs e)
    {
        if (_spot != null) UpdateSpot(_spot.Spot with { Enabled = SpotEnabled.IsChecked == true });
    }

    private void OnSpotMirror(object sender, RoutedEventArgs e)
    {
        if (_spot != null) UpdateSpot(_spot.Spot with { Tx = Math.Round(1 - _spot.Spot.Tx, 4), R = (360 - _spot.Spot.R) % 360 });
    }

    private void OnSpotReset(object sender, RoutedEventArgs e)
    {
        if (_spot == null || _spot.IsDecal) return;
        _design.Logos.Remove(_spot.Layer);
        _design.LogoFiles.Remove(_spot.Layer);
        UpdateSpot(_spot.Original);
        SelectSpot(_spot);
    }

    private void OnSpotRemove(object sender, RoutedEventArgs e)
    {
        if (_spot is not { IsDecal: true } row) return;
        ((System.Collections.ObjectModel.ObservableCollection<SpotRow>)StudioSpotList.ItemsSource).Remove(row);
        SelectSpot(null);
        _artDirty = true;
        ScheduleStudio();
    }

    private async void OnSpotReplace(object sender, RoutedEventArgs e)
    {
        if (_spot == null || !(_spot.IsBrand || _spot.IsDecal)) return;
        var dlg = new OpenFileDialog { Title = $"New image for {_spot.Label} (PNG or DDS)", Filter = ImageLoader.Filter };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        if (_spot.IsDecal) await AddLogo(dlg.FileName, _spot);
        else await ReplaceSpotLogo(_spot, dlg.FileName);
    }

    /// <summary>A logo image squared up (transparent padding keeps its shape) at 512 pixels, and saved as a PNG next to the kit's exports.</summary>
    private async Task<(byte[] Rgba, string File)> SquareLogo(string file, string tag)
    {
        string outFile = Path.Combine(StudioFolder(_kit), $"{_kit.Name}_studio_{tag}_{Path.GetFileNameWithoutExtension(file)}.png");
        var img = await Task.Run(() =>
        {
            var (rgba, w, h) = ImageLoader.Load(file);
            int n = Math.Max(w, h);
            var sq = new byte[n * n * 4];
            int ox = (n - w) / 2, oy = (n - h) / 2;
            for (int y = 0; y < h; y++) Array.Copy(rgba, y * w * 4, sq, ((y + oy) * n + ox) * 4, w * 4);
            var res = MipChain.Resize(sq, n, n, 512, 512, true);
            File.WriteAllBytes(outFile, ImageFiles.Png(res, 512, 512));
            return res;
        });
        return (img, outFile);
    }

    /// <summary>Gives one of the stamp's logos a new image (written into its branding layer).</summary>
    public async Task ReplaceSpotLogo(SpotRow row, string file)
    {
        try
        {
            var (img, outFile) = await SquareLogo(file, $"logo{row.Layer}");
            SetLogo(row, img, outFile);
        }
        catch (Exception ex)
        {
            StudioStatus.Text = $"Can't use {Path.GetFileName(file)}: {ex.Message}";
        }
    }

    private void SetLogo(SpotRow row, byte[] rgba, string file)
    {
        _design.Logos[row.Layer] = (rgba, 512, 512);
        _design.LogoFiles[row.Layer] = file;
        if (!row.Spot.Enabled) row.Spot = row.Spot with { Enabled = true };
        SelectSpot(row);
        ScheduleStudio();
    }

    private async void OnAddLogo(object sender, RoutedEventArgs e)
    {
        if (_studioSrc == null) return;
        var dlg = new OpenFileDialog { Title = "A logo to add to the jersey (PNG or DDS; transparency works best)", Filter = ImageLoader.Filter };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        await AddLogo(dlg.FileName);
    }

    /// <summary>
    /// Adds a logo to the jersey (or gives an added one a new image): on the chest, below the crest, until it's moved.
    /// <paramref name="place"/> "tx,ty,size[,turn]" places it (scripted runs).
    /// </summary>
    public async Task AddLogo(string file, SpotRow replacing = null, string place = null)
    {
        if (_studioSrc == null) return;
        try
        {
            var (img, outFile) = await SquareLogo(file, $"added{_decalCount}");
            var spot = replacing?.Spot ?? new StampHotspot(-1000 - _decalCount++, "decal", "added", true, 180, 0.08, 0.08, 0.5, 0.18);
            if (place != null)
            {
                var v = place.Split(',').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                spot = spot with { Tx = v[0], Ty = v[1], Sx = v[2], Sy = v[2], R = v.Length > 3 ? v[3] : spot.R };
            }
            var row = new SpotRow(spot, -2, new StudioDecal(img, 512, 512, outFile, spot), Path.GetFileName(file));
            var list = (System.Collections.ObjectModel.ObservableCollection<SpotRow>)StudioSpotList.ItemsSource;
            if (replacing != null) list[list.IndexOf(replacing)] = row;
            else list.Add(row);
            SelectSpot(row);
            _artDirty = true;
            ScheduleStudio();
        }
        catch (Exception ex)
        {
            StudioStatus.Text = $"Can't use {Path.GetFileName(file)}: {ex.Message}";
        }
    }

    private void OnSpotCrest(object sender, RoutedEventArgs e)
    {
        if (_spot == null || !_spot.IsBrand || _spot.Layer == 0) return;
        if (!(_design.Logos.TryGetValue(0, out var crest) || _studioSrc.Logos.TryGetValue(0, out crest))) return;
        var img = crest.Width == 512 && crest.Height == 512 ? crest.Rgba : MipChain.Resize(crest.Rgba, crest.Width, crest.Height, 512, 512, true);
        string file = Path.Combine(StudioFolder(_kit), $"{_kit.Name}_studio_logo{_spot.Layer}_crest.png");
        File.WriteAllBytes(file, ImageFiles.Png(img, 512, 512));
        SetLogo(_spot, img, file);
    }

    private static string StudioFolder(Kit kit)
    {
        string folder = Path.Combine(KitPaths.Export, "uniforms", kit.League, kit.Team);
        Directory.CreateDirectory(folder);
        return folder;
    }

    // ================================================================ reset, export, designs on disk

    private async void OnStudioReset(object sender, RoutedEventArgs e)
    {
        _studioSrc = null;
        _studioPainted = null;
        await EnsureStudio();
    }

    private void OnStudioExport(object sender, RoutedEventArgs e)
    {
        if (_studioSrc == null || _design == null) return;
        var dlg = new SaveFileDialog
        {
            Title = "Export the designed jersey", Filter = "PNG image|*.png", InitialDirectory = StudioFolder(_kit),
            FileName = $"{_kit.Team}_{_kit.Name}_studio.png",
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        var d = Snapshot();
        var drawn = JerseyStudio.Draw(_studioSrc, d, JerseyStudio.PaintBase(_studioSrc, d), null);
        File.WriteAllBytes(dlg.FileName, ImageFiles.Png(drawn, _studioSrc.Size, _studioSrc.Size));
        StatusChanged?.Invoke($"Exported the designed jersey to {dlg.FileName}");
    }

    private async void OnStudioTemplate(object sender, RoutedEventArgs e)
    {
        if (_kit == null) return;
        var dlg = new SaveFileDialog
        {
            Title = "Save a jersey template", Filter = "DDS (uncompressed)|*.dds|PNG image|*.png", InitialDirectory = StudioFolder(_kit),
            FileName = _studioSrc?.Own == true ? "jersey_template.dds" : $"{_kit.Team}_{_kit.Name}_jersey_template.dds",
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        await SaveTemplate(dlg.FileName);
    }

    /// <summary>Saves the jersey texture guide for the shown kit (the carrier kit for your own uniform) as DDS or PNG.</summary>
    public async Task SaveTemplate(string file)
    {
        var kit = _kit;
        var index = _mods.Index;
        StatusChanged?.Invoke("Drawing the jersey template from the game's jersey model…");
        try
        {
            await Task.Run(() =>
            {
                var img = JerseyTemplate.Build(index, kit, TextureSize);
                File.WriteAllBytes(file, DdsReader.IsDds(file) ? JerseyTemplate.Dds(img, TextureSize, TextureSize) : ImageFiles.Png(img, TextureSize, TextureSize));
            });
            StatusChanged?.Invoke($"Saved the jersey template to {file}");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            StatusChanged?.Invoke($"Can't make the jersey template: {ex.Message}");
        }
    }

    /// <summary>A design on disk (.jersey.json): what it paints and where its spots go, to use on any kit.</summary>
    private sealed class DesignFile
    {
        public string Template { get; set; }
        public Dictionary<string, string> Zones { get; set; } = new();
        public Dictionary<string, string> Regions { get; set; } = new();
        public string Image { get; set; }
        public List<SpotFile> Spots { get; set; } = new();
        public Dictionary<string, string> Logos { get; set; } = new();
        public List<LineFile> Lines { get; set; } = new();
        public List<AddedFile> Added { get; set; } = new();
        /// <summary>Collar piece colours by name, and the neck band's stripes.</summary>
        public Dictionary<string, string> Collar { get; set; } = new();
        public List<CollarStripeFile> CollarStripes { get; set; } = new();
        /// <summary>A uniform of your own: its colours, font, pants and socks.</summary>
        public OwnFile Own { get; set; }
    }

    private sealed record OwnFile(List<string> Colors, int? Font, int? Pant, int? Sock, string LetterFill, string LetterOutline, int PantShift, int SockShift);

    private sealed record SpotFile(int Id, string Name, bool Enabled, double R, double Sx, double Sy, double Tx, double Ty);
    private sealed record CollarStripeFile(double From, double Width, string Color);
    private sealed record LineFile(string Place, double From, double Width, string Color, string Edge, double EdgeWidth, string Image = null, int Repeat = 1, bool Mirror = true);
    private sealed record AddedFile(string File, bool Enabled, double R, double Sx, double Sy, double Tx, double Ty);

    private void OnStudioSaveDesign(object sender, RoutedEventArgs e)
    {
        if (_design == null) return;
        var dlg = new SaveFileDialog
        {
            Title = "Save the design", Filter = "Jersey design|*.jersey.json", InitialDirectory = StudioFolder(_kit),
            FileName = $"{_kit.Team}_{_kit.Name}.jersey.json",
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        var f = new DesignFile
        {
            Template = _design.Pattern is { } p ? $"{p.Template.Brand}:{p.Template.Number}" : null,
            Image = _studioImageFile,
        };
        foreach (var (k, v) in _design.ZoneColors) f.Zones[k.ToString()] = GearZone.Hex(v);
        foreach (var (k, v) in _design.RegionColors) f.Regions[k.ToString()] = GearZone.Hex(v);
        foreach (var (k, v) in _design.LogoFiles) f.Logos[k.ToString()] = v;
        f.Spots = Spots.Where(s => !s.IsDecal).Select(s => s.Spot).Select(s => new SpotFile(s.Id, s.Name, s.Enabled, s.R, s.Sx, s.Sy, s.Tx, s.Ty)).ToList();
        f.Lines = _design.Lines.Select(l => new LineFile(l.Place.ToString(), l.From, l.Width, GearZone.Hex(l.Color), l.Edge is int c ? GearZone.Hex(c) : null, l.EdgeWidth,
            l.Image?.File, l.Image?.Repeat ?? 1, l.Image?.Mirror ?? true)).ToList();
        foreach (var (k, v) in _design.CollarColors) f.Collar[k.ToString()] = GearZone.Hex(v);
        f.CollarStripes = _design.CollarStripes.Select(s => new CollarStripeFile(s.From, s.Width, GearZone.Hex(s.Color))).ToList();
        f.Added = Spots.Where(s => s.IsDecal).Select(s => new AddedFile(s.Decal.File, s.Spot.Enabled, s.Spot.R, s.Spot.Sx, s.Spot.Sy, s.Spot.Tx, s.Spot.Ty)).ToList();
        if (_studioSrc?.Own == true)
            f.Own = new OwnFile(_ownColors.Select(GearZone.Hex).ToList(), Picked(OwnFonts)?.Number, Picked(OwnPants)?.Number, Picked(OwnSocks)?.Number,
                GearZone.Hex(_letterFill), GearZone.Hex(_letterOutline), _pantShift, _sockShift);
        File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(f, new JsonSerializerOptions { WriteIndented = true }));
        StatusChanged?.Invoke($"Saved the design to {dlg.FileName}");
    }

    private async void OnStudioOpen(object sender, RoutedEventArgs e)
    {
        if (_studioSrc == null) return;
        var dlg = new OpenFileDialog { Title = "Open a jersey design", Filter = "Jersey design|*.jersey.json|All files|*.*", InitialDirectory = StudioFolder(_kit) };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        await OpenDesign(dlg.FileName);
    }

    /// <summary>Applies a saved design to the shown kit (spots by name, so it fits other kits' stamps).</summary>
    public async Task OpenDesign(string file)
    {
        try
        {
            var f = JsonSerializer.Deserialize<DesignFile>(File.ReadAllText(file));
            if (f.Own is { } own)
                await SetOwn(string.Join(",", own.Colors ?? new()), own.Font, own.Pant, own.Sock, $"{own.LetterFill},{own.LetterOutline}", own.PantShift, own.SockShift);
            await WaitStudio();
            var t = f.Template?.Split(':') is { Length: 2 } tp ? _templates.FirstOrDefault(x => x.Brand == tp[0] && x.Number.ToString() == tp[1]) : null;
            _suppress = true;
            StudioTemplates.SelectedItem = (StudioTemplates.ItemsSource as IEnumerable<StudioTemplateRow>)?.FirstOrDefault(r => r.Template == t);
            _suppress = false;
            await UseTemplate(t);
            _design.ZoneColors.Clear();
            foreach (var (k, v) in f.Zones) _design.ZoneColors[int.Parse(k)] = Convert.ToInt32(v.TrimStart('#'), 16);
            _design.RegionColors.Clear();
            foreach (var (k, v) in f.Regions) _design.RegionColors[int.Parse(k)] = Convert.ToInt32(v.TrimStart('#'), 16);
            _design.CollarColors.Clear();
            foreach (var (k, v) in f.Collar ?? new())
                if (Enum.TryParse<CollarPart>(k, out var cp)) _design.CollarColors[cp] = Convert.ToInt32(v.TrimStart('#'), 16);
            _design.CollarStripes = (f.CollarStripes ?? new()).Select(s => new CollarStripe(s.From, s.Width, Convert.ToInt32(s.Color.TrimStart('#'), 16))).ToList();
            BuildZones();
            if (f.Image != null && File.Exists(f.Image)) await UseImage(f.Image, false);
            foreach (var row in Spots.Where(r => !r.IsDecal))
                if (f.Spots.FirstOrDefault(s => s.Name == row.Original.Name) is { } s)
                    row.Spot = row.Original with { Enabled = s.Enabled, R = s.R, Sx = s.Sx, Sy = s.Sy, Tx = s.Tx, Ty = s.Ty };
            foreach (var (k, v) in f.Logos)
                if (Spots.FirstOrDefault(r => r.IsBrand && r.Layer == int.Parse(k)) is { } row && File.Exists(v)) await ReplaceSpotLogo(row, v);
            ((System.Collections.ObjectModel.ObservableCollection<LineRow>)LineList.ItemsSource).Clear();
            foreach (var l in f.Lines ?? new())
            {
                var row = AddLine(Enum.Parse<LinePlace>(l.Place), new StudioLine(Enum.Parse<LinePlace>(l.Place), l.From, l.Width, Convert.ToInt32(l.Color.TrimStart('#'), 16),
                    l.Edge != null ? Convert.ToInt32(l.Edge.TrimStart('#'), 16) : null, l.EdgeWidth));
                if (row != null && l.Image != null && File.Exists(l.Image)) SetLineImage(row, l.Image, l.Repeat, l.Mirror);
            }
            LinesEmpty.Visibility = LineRows.Any() ? Visibility.Collapsed : Visibility.Visible;
            foreach (var a in f.Added ?? new())
                if (File.Exists(a.File)) await AddLogo(a.File, null, string.Create(CultureInfo.InvariantCulture, $"{a.Tx},{a.Ty},{a.Sx},{a.R}"));
            SelectSpot(null);
            _artDirty = true;
            ScheduleStudio();
            StatusChanged?.Invoke($"Opened the design {Path.GetFileName(file)}" + (_studioSrc?.Own == true ? "" : $" on {_kit}"));
        }
        catch (Exception ex)
        {
            StudioStatus.Text = $"Can't open {Path.GetFileName(file)}: {ex.Message}";
        }
    }

    // ================================================================ writing over the kit

    private async void OnStudioWrite(object sender, RoutedEventArgs e) => await WriteStudio();

    private bool GameClosed()
    {
        if (!ModService.GameRunning) return true;
        MessageBox.Show(Window.GetWindow(this), "KytyPS5 is running. Close the game first: its files can't be changed while it reads them.", "NHL27Kit", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    /// <summary>
    /// Writes the design over the kit it started from: the stamp (when a hotspot changed), replaced logos, the painted
    /// base and the composited jersey; then reads the kit back from the game.
    /// </summary>
    public async Task WriteStudio()
    {
        if (_studioSrc == null || _studioSrc.Own || _design == null || _busy || StudioChanges().Count == 0 || !GameClosed()) return;
        var kit = _kit;
        string label = $"{UniformNames.Pretty(kit.Team)} {UniformNames.Pretty(kit.Name)}";
        _busy = true;
        StudioButtons();
        try
        {
            var done = await WriteOver(kit);
            StatusChanged?.Invoke($"Jersey studio: {label} written into the game ({string.Join(", ", done)}). Restore on the Mods page undoes each part.");
        }
        catch (Exception ex)
        {
            StudioStatus.Text = "Can't write: " + ex.Message;
            if (!App.Args.ContainsKey("shot"))
                MessageBox.Show(Window.GetWindow(this), ex.Message, "Can't write the jersey", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _busy = false;
        }
        _studioSrc = null;
        await ShowKit(kit);
    }

    /// <summary>The design over its own kit: stamp, replaced logos (branding layers), base and baked jersey.</summary>
    private async Task<List<string>> WriteOver(Kit kit)
    {
        var src = _studioSrc;
        var d = Snapshot();
        var done = new List<string>();
        StudioStatus.Text = "Preparing the textures…";
        string folder = StudioFolder(kit);
        bool art = PaintsArt(d);
        var painted = await Task.Run(() => JerseyStudio.PaintBase(src, d));
        string baseFile = Path.Combine(folder, $"{kit.Name}_studio_base_color.png"), bakedFile = Path.Combine(folder, $"{kit.Name}_studio_color.png");
        await Task.Run(() =>
        {
            if (art) File.WriteAllBytes(baseFile, ImageFiles.Png(painted, src.Size, src.Size));
            File.WriteAllBytes(bakedFile, ImageFiles.Png(JerseyStudio.Draw(src, d, painted, null), src.Size, src.Size));
        });
        // the stamp: where the game puts each logo and the lettering
        byte[] stamp = KitStamp.Write(src.StampEbx, "jersey", d.Spots);
        if (!stamp.AsSpan().SequenceEqual(src.StampEbx))
        {
            StudioStatus.Text = "Writing the stamp (hotspots) into the game…";
            int changed = Spots.Count(s => !s.IsDecal && s.Spot != s.Original);
            await _mods.SaveEbxAsync(kit.Stamps["jersey"], stamp, $"Jersey studio: {kit}, {changed} hotspot(s) moved, sized or switched");
            done.Add("stamp");
        }
        var items = new List<(KitTexture, string)>();
        // replaced logos: the numbered texture (and its array layer), or just the array layer when there's no texture
        foreach (var (layer, file) in d.LogoFiles)
        {
            if (kit.Slot($"branding_jersey_{layer}_color") is { } slot) items.Add((slot, file));
            else if (kit.Slot("branding_jersey_color_array") is { } array)
            {
                StudioStatus.Text = $"Writing logo layer {layer} into the game…";
                await _mods.ReplaceArrayLayerAsync(array.Name, file, layer, null);
                done.Add($"logo layer {layer}");
            }
        }
        if (art && kit.Slot("jersey_base_color") is { } b) items.Add((b, baseFile));
        if (kit.Slot("jersey_color") is { } c) items.Add((c, bakedFile));
        foreach (var (texture, file) in items)
        {
            StatusChanged?.Invoke(await KitWriter.WriteTextureAsync(_mods, kit, texture, file, t => StudioStatus.Text = t));
            done.Add(texture.Slot.Replace("_", " "));
        }
        _textures.ClearThumbs();
        return done;
    }

    /// <summary>
    /// The design into another kit folder: its stamped logos painted into the base (that folder's stamp has its own
    /// logos, which are switched off), the lettering spots placed as designed, and the font. With <paramref name="outfit"/>
    /// also the pants (the folder's pant logos switched off) and socks. From a team's kit the font, pants (logos painted
    /// in) and socks are that kit's; for your own uniform they're the create-a-team font (with its relief and material
    /// maps), pants and socks as painted.
    /// </summary>
    private async Task<List<string>> WriteInto(Kit target, bool outfit)
    {
        var src = _studioSrc;
        var from = src.Kit;
        var d = Snapshot();
        var index = _mods.Index;
        var done = new List<string>();
        string folder = StudioFolder(target);
        string stem = Path.Combine(folder, src.Own ? $"{target.Name}_studio_own" : $"{target.Name}_studio_from_{from.Team}_{from.Name}");
        StudioStatus.Text = "Preparing the textures…";
        var items = new List<(KitTexture Slot, string File)>();
        byte[] stampEbx = null, stampNew = null, pantEbx = null, pantNew = null;
        var palette = Palette();
        var (fontT, pantT, sockT) = (_fontPat?.Template, _pantPat?.Template, _sockPat?.Template);
        var (fontImg, ps, ss) = (_fontImg, _pantShift, _sockShift);
        await Task.Run(() =>
        {
            var painted = JerseyStudio.PaintBase(src, d);
            var spots = target.Stamps.TryGetValue("jersey", out var stampName) && index.GetEbx(stampName) is { } se
                ? KitStamp.Parse(stampEbx = index.Read(se), "jersey") : new List<StampHotspot>();
            var (baked, newSpots) = JerseyStudio.ForKit(src, d, painted, spots);
            if (stampEbx != null) stampNew = KitStamp.Write(stampEbx, "jersey", newSpots);
            File.WriteAllBytes(stem + "_jersey_base_color.png", ImageFiles.Png(baked, src.Size, src.Size));
            var opaque = (byte[])baked.Clone();
            for (int i = 3; i < opaque.Length; i += 4) opaque[i] = 255;
            File.WriteAllBytes(stem + "_jersey_color.png", ImageFiles.Png(opaque, src.Size, src.Size));
            if (target.Slot("jersey_base_color") is { } tb) items.Add((tb, stem + "_jersey_base_color.png"));
            if (target.Slot("jersey_color") is { } tc) items.Add((tc, stem + "_jersey_color.png"));

            // an image for one of the folder's textures, at that texture's size
            void Put(string slot, byte[] rgba, int w, int h, bool srgb)
            {
                if (rgba == null || target.Slot(slot) is not { } ts) return;
                var head = Texture.Load(index, ts.Name, decode: false);
                var img = head.Width == w && head.Height == h ? rgba : MipChain.Resize(rgba, w, h, head.Width, head.Height, srgb);
                string file = $"{stem}_{slot}.png";
                File.WriteAllBytes(file, ImageFiles.Png(img, head.Width, head.Height));
                items.Add((ts, file));
            }
            // a texture of the start kit, as a PNG at its full size
            string Copy(string slot)
            {
                if (from.Slot(slot) is not { } s || target.Slot(slot) == null) return null;
                var t = Texture.Load(index, s.Name);
                if (t.Rgba == null) return null;
                string file = $"{stem}_{slot}.png";
                File.WriteAllBytes(file, ImageFiles.Png(t.Rgba, t.Width, t.Height));
                return file;
            }
            // the folder's pant logos off: the pants carry the design's own
            void PantLogosOff()
            {
                if (!target.Stamps.TryGetValue("pant", out var pantStamp) || index.GetEbx(pantStamp) is not { } pe) return;
                pantEbx = index.Read(pe);
                pantNew = KitStamp.Write(pantEbx, "pant", KitStamp.Parse(pantEbx, "pant").Select(s => s.Type == "brand" ? s with { Enabled = false } : s));
            }

            if (src.Own)
            {
                // the create-a-team font: colours as painted, its glyphs' relief and material
                if (fontT != null)
                {
                    Put("font_color", fontImg, PartSize, PartSize, true);
                    foreach (var (kind, slot) in new[] { ("normal", "font_normal"), ("coeff", "font_coeff") })
                        if (OwnKit.FontMap(index, fontT, kind) is { } t) Put(slot, t.Rgba, t.Width, t.Height, false);
                }
                if (!outfit) return;
                if (pantT != null)
                {
                    var pp = JerseyStudio.LoadPattern(index, pantT, 2048);
                    var pant = OwnKit.PaintPart(pp, palette, ps);
                    // the base keeps the folder's fabric map (alpha)
                    var withAlpha = (byte[])pant.Clone();
                    if (target.Slot("pant_base_color") is { } tpb && Kits.Load(index, tpb.Name, 2048, 0) is { Rgba: not null } pb && pb.Width == pp.Size && pb.Height == pp.Size)
                        for (int i = 3; i < withAlpha.Length; i += 4) withAlpha[i] = pb.Rgba[i];
                    Put("pant_base_color", withAlpha, pp.Size, pp.Size, true);
                    Put("pant_color", pant, pp.Size, pp.Size, true);
                    foreach (var (kind, slots) in new[] { ("normal", new[] { "pant_base_normal", "pant_normal" }), ("coeff", new[] { "pant_base_coeff", "pant_coeff" }) })
                        if (OwnKit.Map(index, pantT, kind) is { } name && Kits.Load(index, name, 4096, 0) is { Rgba: not null } t)
                            foreach (var slot in slots) Put(slot, t.Rgba, t.Width, t.Height, false);
                    PantLogosOff();
                }
                if (sockT != null)
                {
                    var sp = JerseyStudio.LoadPattern(index, sockT, 1024);
                    var sock = OwnKit.PaintPart(sp, palette, ss);
                    Put("sock_base_color", sock, sp.Size, sp.Size, true);
                    Put("sock_color", sock, sp.Size, sp.Size, true);
                }
                return;
            }

            if (from != target)
                foreach (var slot in new[] { "font_color", "font_normal", "font_coeff" })
                    if (Copy(slot) is { } f) items.Add((target.Slot(slot), f));
            if (!outfit || from == target) return;
            // pants: the start kit's as the game draws them (logos stamped on), its fabric map kept
            if (Kits.Compose(index, from, "pant", TextureSize) is { } pc && from.Slot("pant_base_color") is { } pbs)
            {
                var pb = Kits.Load(index, pbs.Name, pc.Width, 0);
                var withAlpha = (byte[])pc.Rgba.Clone();
                if (pb.Width == pc.Width && pb.Height == pc.Height)
                    for (int i = 3; i < withAlpha.Length; i += 4) withAlpha[i] = pb.Rgba[i];
                File.WriteAllBytes(stem + "_pant_base_color.png", ImageFiles.Png(withAlpha, pc.Width, pc.Height));
                File.WriteAllBytes(stem + "_pant_color.png", ImageFiles.Png(pc.Rgba, pc.Width, pc.Height));
                if (target.Slot("pant_base_color") is { } tpb) items.Add((tpb, stem + "_pant_base_color.png"));
                if (target.Slot("pant_color") is { } tpc) items.Add((tpc, stem + "_pant_color.png"));
                PantLogosOff();
            }
            else if (Copy("pant_color") is { } pcf) items.Add((target.Slot("pant_color"), pcf));
            foreach (var slot in new[] { "sock_base_color", "sock_color" })
                if (Copy(slot) is { } f) items.Add((target.Slot(slot), f));
        });
        string label = src.Own ? $"Jersey studio: your own uniform into {target}" : $"Jersey studio: {from.Team} {from.Name} design into {target}";
        if (stampNew != null && !stampNew.AsSpan().SequenceEqual(stampEbx))
        {
            StudioStatus.Text = "Writing the jersey stamp into the game…";
            await _mods.SaveEbxAsync(target.Stamps["jersey"], stampNew, $"{label}: its logos switched off (painted into the base), lettering placed");
            done.Add("jersey stamp");
        }
        if (pantNew != null && !pantNew.AsSpan().SequenceEqual(pantEbx))
        {
            await _mods.SaveEbxAsync(target.Stamps["pant"], pantNew, $"{label}: pant logos switched off");
            done.Add("pant stamp");
        }
        foreach (var (texture, file) in items)
        {
            StatusChanged?.Invoke(await KitWriter.WriteTextureAsync(_mods, target, texture, file, t => StudioStatus.Text = t));
            done.Add(texture.Slot.Replace("_", " "));
        }
        _textures.ClearThumbs();
        return done;
    }

    // ================================================================ adding to a team's jersey menu

    /// <summary>Reads which teams there are and which kit folders their menus show, then fills the pickers.</summary>
    private async Task LoadSlotTargets()
    {
        try
        {
            var db = await _mods.OpenDbAsync(JerseySlots.Database);
            var kits = _allKits;
            (_kitTeams, _used) = await Task.Run(() => (JerseySlots.Teams(db, kits), JerseySlots.Used(db)));
        }
        catch (Exception ex)
        {
            SlotStatus.Text = "Can't read the jersey menus: " + ex.Message;
            return;
        }
        var teams = _kitTeams.GroupBy(x => x.Value.TeamId).Select(g => g.OrderBy(x => x.Key.Team.Length).First())
            .Select(x => new StudioTeamItem(x.Value, x.Key.League, $"{x.Value.Name}  ·  {x.Key.League.ToUpperInvariant()}"))
            .OrderBy(t => t.League == "nhl" ? 0 : 1).ThenBy(t => t.League).ThenBy(t => t.Team.Name).ToList();
        var mine = _studioSrc != null ? _kitTeams.GetValueOrDefault((_studioSrc.Kit.League, _studioSrc.Kit.Team)) : null;
        _suppress = true;
        SlotTeam.ItemsSource = teams;
        SlotTeam.SelectedItem = teams.FirstOrDefault(t => t.Team.TeamId == mine?.TeamId) ?? teams.FirstOrDefault();
        _suppress = false;
        FillFolders();
    }

    /// <summary>The folders the design can be written into: free ones (or all, with the switch), the start kit's maker and team first.</summary>
    private void FillFolders()
    {
        if (_kitTeams == null || _studioSrc == null) return;
        var from = _studioSrc.Kit;
        bool all = SlotShowUsed.IsChecked == true;
        var list = _allKits.Where(k => k.Slot("jersey_base_color") != null || k.Slot("jersey_color") != null)
            .Select(k => (Kit: k, Team: _kitTeams.GetValueOrDefault((k.League, k.Team)), Address: JerseySlots.Address(k)))
            .Where(x => x.Team != null && x.Address != null)
            .Select(x => (x.Kit, x.Team, Free: !_used.Contains((x.Team.ArtId, x.Address.Value.Style, x.Address.Value.Variant))))
            .Where(x => all || x.Free)
            // your own: free folders of the common cut first; from a team: its maker and team first
            .OrderBy(x => _studioSrc.Own ? (x.Free ? 0 : 1) : x.Kit.Brand == from.Brand ? 0 : 1)
            .ThenBy(x => _studioSrc.Own ? (x.Kit.Brand == from.Brand ? 0 : 1) : x.Kit.Team == from.Team ? 0 : 1).ThenBy(x => x.Free ? 0 : 1)
            .ThenBy(x => x.Kit.League == "nhl" ? 0 : 1).ThenBy(x => x.Kit.ToString())
            .Select(x => new SlotKitItem(x.Kit, x.Team, $"{(x.Free ? "free" : "in use")}  ·  {x.Kit.League.ToUpperInvariant()} {UniformNames.Pretty(x.Kit.Team)}  ·  {x.Kit.Brand} {x.Kit.Name}"))
            .ToList();
        var keep = (SlotFolder.SelectedItem as SlotKitItem)?.Kit;
        _suppress = true;
        SlotFolder.ItemsSource = list;
        SlotFolder.SelectedItem = list.FirstOrDefault(i => i.Kit == keep) ?? list.FirstOrDefault();
        _suppress = false;
        // a mostly white body is a light jersey
        SlotLight.IsChecked = LooksLight();
        SlotButtons();
    }

    private bool LooksLight()
    {
        var p = _studioPainted ?? _studioSrc?.Base;
        if (p == null) return false;
        long sum = 0, n = 0;
        for (int i = 0; i < p.Length; i += 4 * 97)
            if (JerseyStudio.Region(p[i + 3]) == 1) { sum += p[i] + p[i + 1] + p[i + 2]; n++; }
        return n > 0 && sum / (double)n > 600;
    }

    private void OnSlotShowUsed(object sender, RoutedEventArgs e) => FillFolders();

    private void OnSlotTargetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_suppress) SlotButtons();
    }

    private void SlotButtons()
    {
        if (SlotWrite == null) return;
        var folder = SlotFolder.SelectedItem as SlotKitItem;
        var team = SlotTeam.SelectedItem as StudioTeamItem;
        bool errors = StudioZoneRows.Any(z => z.HasError) || LineRows.Any(l => l.HasError);
        SlotWrite.IsEnabled = !_busy && _studioSrc != null && folder != null && team != null && !errors;
        if (folder == null) { SlotStatus.Text = _kitTeams == null ? "" : "No folder to write into: switch on Used folders."; return; }
        bool free = folder.Label.StartsWith("free");
        SlotStatus.Text = folder.Kit == _studioSrc?.Kit && !_studioSrc.Own ? "Writes over the start kit itself, then adds it to the menu."
            : free ? $"{folder.Kit} isn't in any menu: only the new slot shows it."
            : $"{folder.Kit} is in a menu already: that jersey changes too.";
    }

    private async void OnAddToTeam(object sender, RoutedEventArgs e) => await AddToTeam(App.Args.ContainsKey("shot"));

    /// <summary>
    /// Writes the design into the picked kit folder and adds that folder to the picked team's jersey menu (a new
    /// stockteamjerseys row, first or last). Every part is its own mod entry.
    /// </summary>
    public async Task AddToTeam(bool noAsk = false)
    {
        if (_busy || _studioSrc == null || SlotFolder.SelectedItem is not SlotKitItem folder || SlotTeam.SelectedItem is not StudioTeamItem team) return;
        if (!GameClosed()) return;
        var label = (SlotLabel.SelectedItem as PickItem) ?? new PickItem("Third", 2);
        bool light = SlotLight.IsChecked == true, top = SlotTop.IsChecked == true, outfit = SlotOutfit.IsChecked == true;
        var target = folder.Kit;
        if (!noAsk && MessageBox.Show(Window.GetWindow(this),
                $"Write this design into the kit folder {target} and add it to {team.Team.Name}'s jersey menu as \"{label.Label}\"?\n\n" +
                (folder.Label.StartsWith("free") ? "No team's menu shows that folder now, so no other jersey changes." : "A team's menu shows that folder already: its jersey changes too.") +
                "\nRestore on the Mods page undoes each part.", "Add to a team", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        _busy = true;
        StudioButtons();
        try
        {
            var done = target == _studioSrc.Kit && !_studioSrc.Own ? await WriteOver(target) : await WriteInto(target, outfit);
            StudioStatus.Text = $"Adding the slot to {team.Team.Name}'s jersey menu…";
            var address = JerseySlots.Address(target).Value;
            int artId = folder.Team.ArtId, teamId = team.Team.TeamId;
            string summary = null;
            await _mods.SaveDbAsync(JerseySlots.Database, db =>
            {
                var slots = JerseySlots.Read(db, teamId);
                var slot = JerseySlots.NewSlot(db, artId, address.Style, address.Variant) with { Label = label.Value, IsLight = light, IsDefault = false };
                if (top) slots.Insert(0, slot); else slots.Add(slot);
                summary = JerseySlots.Write(db, teamId, slots);
                return $"Jersey studio: {team.Team.Name} jersey menu, {target} added as {label.Label} ({summary})";
            }, "Jersey studio");
            done.Add($"{team.Team.Name}'s jersey menu ({summary})");
            SlotsChanged?.Invoke(teamId);
            StatusChanged?.Invoke($"Jersey studio: the design is in {target} and in {team.Team.Name}'s jersey menu ({string.Join(", ", done)}). Restore on the Mods page undoes each part.");
        }
        catch (Exception ex)
        {
            StudioStatus.Text = "Can't add it: " + ex.Message;
            if (!noAsk) MessageBox.Show(Window.GetWindow(this), ex.Message, "Can't add the jersey", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _busy = false;
        }
        await LoadSlotTargets();
        StudioButtons();
    }

    // ================================================================ scripted runs

    /// <summary>Picks a design ("adidas:5", or "kit" for the kit's own art).</summary>
    public async Task StudioUseTemplate(string spec)
    {
        await WaitStudio();
        var t = spec == "kit" ? null : _templates?.FirstOrDefault(x => $"{x.Brand}:{x.Number}" == spec);
        _suppress = true;
        StudioTemplates.SelectedItem = (StudioTemplates.ItemsSource as IEnumerable<StudioTemplateRow>)?.FirstOrDefault(r => r.Template == t);
        _suppress = false;
        await UseTemplate(t);
    }

    /// <summary>Moves a hotspot: "8:0.5,0.27" (id: tx,ty), optionally ",1.2" to scale and ",on"/",off".</summary>
    public async Task StudioSpot(string spec)
    {
        await WaitStudio();
        var p = spec.Split(':', ',');
        var row = Spots.FirstOrDefault(r => r.Original.Id.ToString() == p[0]);
        if (row == null) return;
        SelectSpot(row);
        var ci = CultureInfo.InvariantCulture;
        var s = row.Spot with { Tx = double.Parse(p[1], ci), Ty = double.Parse(p[2], ci), Enabled = !p.Contains("off") };
        if (p.Length > 3 && double.TryParse(p[3], NumberStyles.Float, ci, out double k)) s = s with { Sx = s.Sx * k, Sy = s.Sy * k };
        UpdateSpot(s);
    }

    /// <summary>Sets a colour row: "z0=#FF0000" (design zone) or "r4=#FFFFFF" (fabric region).</summary>
    public async Task StudioColor(string spec)
    {
        await WaitStudio();
        var m = System.Text.RegularExpressions.Regex.Match(spec, @"^([zr])(\d+)=(.*)$");
        if (!m.Success) return;
        int n = int.Parse(m.Groups[2].Value);
        var row = StudioZoneRows.FirstOrDefault(z => m.Groups[1].Value == "z" ? z.Zone == n : z.Region == n);
        if (row != null) { SelectColor(row, t => row.Text = t); row.Text = m.Groups[3].Value; }
    }

    /// <summary>
    /// Adds a line: "body:FROM,WIDTH,#RRGGBB[,#EDGE,EDGEWIDTH]" (cm), or "body:FROM,HEIGHT,image=FILE[,repeat=N][,round]"
    /// to wrap an image round.
    /// </summary>
    public async Task StudioLine(string spec)
    {
        await WaitStudio();
        var im = System.Text.RegularExpressions.Regex.Match(spec, @"^(body|sleeves):([\d.]+),([\d.]+),image=([^,]+)(?:,repeat=(\d+))?(,round)?$");
        if (im.Success)
        {
            var ic = CultureInfo.InvariantCulture;
            var ip = im.Groups[1].Value == "body" ? LinePlace.Body : LinePlace.Sleeves;
            var row = AddLine(ip, new StudioLine(ip, double.Parse(im.Groups[2].Value, ic), double.Parse(im.Groups[3].Value, ic), 0));
            if (row != null) SetLineImage(row, im.Groups[4].Value, im.Groups[5].Success ? int.Parse(im.Groups[5].Value) : 1, !im.Groups[6].Success);
            return;
        }
        var m = System.Text.RegularExpressions.Regex.Match(spec, @"^(body|sleeves):([\d.]+),([\d.]+),#?(\w{6})(?:,#?(\w{6}),([\d.]+))?$");
        if (!m.Success) return;
        var ci = CultureInfo.InvariantCulture;
        var place = m.Groups[1].Value == "body" ? LinePlace.Body : LinePlace.Sleeves;
        AddLine(place, new StudioLine(place, double.Parse(m.Groups[2].Value, ci), double.Parse(m.Groups[3].Value, ci), Convert.ToInt32(m.Groups[4].Value, 16),
            m.Groups[5].Success ? Convert.ToInt32(m.Groups[5].Value, 16) : null, m.Groups[6].Success ? double.Parse(m.Groups[6].Value, ci) : 0));
    }

    /// <summary>Adds a logo: "FILE" or "FILE|tx,ty,size[,turn]"; "FILE@crest" or "FILE@ID" replaces a stamp logo instead.</summary>
    public async Task StudioAddLogo(string spec)
    {
        await WaitStudio();
        int at = spec.LastIndexOf('@');
        if (at > 0)
        {
            string id = spec[(at + 1)..];
            var row = Spots.FirstOrDefault(r => r.IsBrand && (id == "crest" ? r.Layer == 0 : r.Original.Id.ToString() == id));
            if (row != null) await ReplaceSpotLogo(row, spec[..at]);
            return;
        }
        var parts = spec.Split('|');
        await AddLogo(parts[0], null, parts.Length > 1 ? parts[1] : null);
    }

    /// <summary>Picks the team and folder to add to ("TEAM NAME|kit folder text"), without writing.</summary>
    public async Task StudioSlotTarget(string spec)
    {
        await WaitStudio();
        var p = spec.Split('|');
        if (SlotTeam.ItemsSource is IEnumerable<StudioTeamItem> teams && teams.FirstOrDefault(t => t.Team.Name.Contains(p[0], StringComparison.OrdinalIgnoreCase)) is { } team)
            SlotTeam.SelectedItem = team;
        if (p.Length > 1)
        {
            if (!(SlotFolder.ItemsSource as IEnumerable<SlotKitItem>)?.Any(i => i.Kit.ToString().Contains(p[1])) == true) { SlotShowUsed.IsChecked = true; FillFolders(); }
            if ((SlotFolder.ItemsSource as IEnumerable<SlotKitItem>)?.FirstOrDefault(i => i.Kit.ToString().Contains(p[1])) is { } folder) SlotFolder.SelectedItem = folder;
        }
        SlotButtons();
    }

    public void StudioScroll(double offset) => StudioScroller.ScrollToVerticalOffset(offset);

    /// <summary>Scripted runs: collar colours and neck band stripes, "NeckFront=#FF0000;Insert=#0000FF;stripe=0,20,#FFFFFF".</summary>
    public async Task StudioCollar(string spec)
    {
        await WaitStudio();
        var ci = CultureInfo.InvariantCulture;
        foreach (var item in spec.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = item.Split('=');
            if (kv[0] == "stripe")
            {
                var v = kv[1].Split(',');
                AddCollarStripe(new CollarStripe(double.Parse(v[0], ci), double.Parse(v[1], ci), Convert.ToInt32(v[2].TrimStart('#'), 16)));
            }
            else if (Enum.TryParse<CollarPart>(kv[0], out var part)) SetCollarColor(part, kv[1]);
        }
    }

    /// <summary>Scripted runs: saves a strip template for line N (0-based), or the lines on their own.</summary>
    public async Task StudioExportStrip(int line, string file)
    {
        await WaitStudio();
        if (LineRows.ElementAtOrDefault(line) is { } row) SaveStripTemplate(row, file);
    }

    public async Task StudioExportLines(string file)
    {
        await WaitStudio();
        if (_studioSrc?.Shape != null) SaveLinesPng(file);
    }

    /// <summary>Scripted runs: selects line N and moves it as a Ctrl+drag on the jersey at texture point (u, v) would.</summary>
    public async Task StudioPickLine(int line, double u, double v)
    {
        await WaitStudio();
        if (LineRows.ElementAtOrDefault(line) is not { } row || _shown == null) return;
        SelectColor(row, t => row.ColorText = t);
        var part = _shown.FirstOrDefault(s => s.Garment == "jersey").Part;
        if (part != null) PickLine(part, new Point(u, v));
    }

    private async Task WaitStudio()
    {
        for (int i = 0; i < 150 && (_studioSrc == null || _studioLoading); i++) await Task.Delay(100);
    }
}
