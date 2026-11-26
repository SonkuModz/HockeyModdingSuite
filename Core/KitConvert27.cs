using System.Text;

namespace NHL27Kit.Core;

/// <summary>
/// An NHL 27 (PS5) kit's textures and stamps for an NHL 24 kit folder copied from a Center Ice Mod kit (<see cref="KitClone"/>).
/// The NHL 24 copy keeps its own headers (sizes, formats, layer counts); each texture's pixels come from the NHL 27 texture
/// of the same name:
/// - same format: the NHL 27 mips' blocks, untiled from PS5 and tiled for PS4, so nothing is encoded again; a texture
///   NHL 24 keeps at half size (the font colour: 1024 against 2048) starts at NHL 27's mip 1;
/// - another format: decoded and encoded again;
/// - array layers NHL 27 doesn't have (the copy's branding arrays have 7 layers, NHL 27's 6 or fewer): a blank layer of
///   the copy's own;
/// - textures NHL 27 has no file for (the helmet logo, branding layers past NHL 27's count): the copy's own.
/// The stamps (hotspot XML: where the name, numbers and each branding layer go) are NHL 27's, written over the copy's in
/// place and padded with spaces to the same size. Brand hotspots draw the branding layers in order, in both games.
/// </summary>
public sealed class KitConvert27
{
    private readonly AssetIndex _i27;
    private readonly AssetIndex _i24;
    public string Folder27 { get; }
    public string Word27 { get; }
    public string Team { get; }
    public List<string> Log { get; } = new();
    /// <summary>A shared folder's copy (the weave): the NHL 27 name of a copied file, else null for a kit folder.</summary>
    private Func<string, string> _map;
    /// <summary>Layers NHL 27 doesn't have repeat its last one (the weave, whose layers aren't logo slots).</summary>
    private bool _fillLast;
    private bool _ownLayers;
    private (int Twill, int Flat)? _twill;
    /// <summary>The weave copy's folder words (adidas -> fanatc): its own NHL 24 files.</summary>
    private string Word24Copy, Word27Copy;

    /// <summary>
    /// The NHL 27 layer that fills the copy's layer <paramref name="l"/>. Logo arrays: the same layer. The weave: NHL 24's kits
    /// pick weave layers by NHL 24's order, and every NHL 27 Fanatics array is NHL 24's adidas one without layer 1 (the big
    /// perforation holes), so layer 1 takes NHL 27's plain knit (layer 0) and layer k past it NHL 27's k-1. Kept 1:1, the
    /// kits' bodies drew NHL 27's dimple mesh (2026-11-24).
    /// </summary>
    private int SourceLayer(int l, int layers24, int layers27)
    {
        if (_twill is { } tv && l >= tv.Twill) l = tv.Flat;
        // the dot layers (1 holes, 2 dimple mesh) take the plain knit: NHL 24's own (coeff) or NHL 27's (normal, k-1 past them)
        if (_fillLast && _ownLayers) return l is 1 or 2 ? 0 : l;
        if (_fillLast && layers27 == layers24 - 1) return l <= 2 ? 0 : l - 1;
        return Math.Min(l, layers27 - 1);
    }

    /// <summary>NHL 27's Fanatics weave for a copy of NHL 24's adidas one: same names but the brand word.</summary>
    public static KitConvert27 Weave(AssetIndex i27, AssetIndex i24, string word24, string word27) => new(i27, i24, "textile", word27)
    {
        _map = n => n.Replace($"/{word24}/", $"/{word27}/").Replace($"_{word24}_", $"_{word27}_"),
        _fillLast = true,
        Word24Copy = "adidas", Word27Copy = word24,
    };

    public KitConvert27(AssetIndex i27, AssetIndex i24, string team, string word27)
    {
        _i27 = i27; _i24 = i24; Team = team; Word27 = word27;
        Folder27 = $"content/character/skater/league/nhl/{team}/adidas/{word27}";
    }

    /// <summary>The NHL 27 name of one of the copy's files.</summary>
    public string Name27(KitCopy c, string newName)
    {
        if (_map != null) return _map(newName);
        string file = newName[(newName.LastIndexOf('/') + 1)..].Replace($"_{c.Team}_{c.ToWord}", $"_{Team}_{Word27}");
        return Folder27 + "/" + file;
    }

    public bool Has27(string name) => _i27.GetRes(name) != null;

    /// <summary>The copy's own raw chunk of one of its source textures (for blank layers).</summary>
    private byte[] DonorRaw(KitCopy c, string newName) =>
        Texture.Load(_i24, c.Folder(c.FromWord) + newName[newName.LastIndexOf('/')..].Replace($"_{c.Team}_{c.ToWord}", $"_{c.Team}_{c.FromWord}"), decode: false).Data;

    /// <summary>A layer of an NHL 24 array that's blank in the source kit: the first whose colour layer has no alpha.</summary>
    private readonly Dictionary<string, int> _blank = new();
    private int BlankLayer(KitCopy c, string arrayName)
    {
        // the colour array of the same set decides (normal and coeff layers line up with it)
        string color = System.Text.RegularExpressions.Regex.Replace(arrayName, "_(color|normal|coeff)_array$", "_color_array");
        if (_blank.TryGetValue(color, out int b)) return b;
        string src = c.Folder(c.FromWord) + color[color.LastIndexOf('/')..].Replace($"_{c.Team}_{c.ToWord}", $"_{c.Team}_{c.FromWord}");
        var t = Texture.Load(_i24, src, decode: false);
        b = -1;
        for (int l = 0; l < Math.Max(1, t.Header.Layers) && b < 0; l++)
        {
            t.Layer = l;
            t.DecodeMip(Math.Max(0, t.FirstResidentMip));
            bool empty = true;
            for (int i = 3; i < t.Rgba.Length && empty; i += 4) empty = t.Rgba[i] == 0;
            if (empty) b = l;
        }
        return _blank[color] = b;
    }

    /// <summary>The new raw chunk for one of the copy's textures, or null to keep the copy's own.</summary>
    public byte[] Pixels(KitCopy c, string newName, TextureHeader h24)
    {
        string n27 = Name27(c, newName);
        // NHL 27's base coeff maps turn NHL 24's jerseys black in gameplay: the copy keeps its own
        if (newName.EndsWith("_base_coeff", StringComparison.OrdinalIgnoreCase)) { Log.Add($"{Short(newName)}: kept (base coeff stays NHL 24's)"); return null; }
        // the same for the weave's coeff arrays (roughness, metalness): NHL 27's make the fabric shine like metal in NHL 24;
        // the weave's pattern is its normal arrays
        // so they stay NHL 24's adidas ones, but with the dot layers (1 the holes, 2 the dimple mesh) as its plain knit (layer 0)
        bool ownCoeff = _fillLast && newName.EndsWith("_coeff_array", StringComparison.OrdinalIgnoreCase);
        // the tie weaves end with a blotchy stitch layer (after a flat one) that logos on tie kits draw with: NHL 27's
        // Fanatics kits use the default weave, which has none, so it takes the flat layer before it (copy layers, NHL 24's order)
        _twill = !_fillLast ? null : newName.Contains("_metal_tie_") ? (7, 6) : newName.Contains("_tie_") ? (6, 5) : null;
        var srcIndex = _i27;
        if (ownCoeff)
        {
            (srcIndex, n27) = (_i24, newName.Replace($"/{Word27Copy}/", $"/{Word24Copy}/").Replace($"_{Word27Copy}_", $"_{Word24Copy}_"));
            _ownLayers = true;
        }
        else _ownLayers = false;
        var res27 = srcIndex.GetRes(n27);
        if (res27 == null) { Log.Add($"{Short(newName)}: kept (NHL 27 has no {Short(n27)})"); return null; }
        var t = Texture.Load(srcIndex, n27, decode: false);
        var h27 = t.Header;
        int layers24 = Math.Max(1, h24.Layers), layers27 = Math.Max(1, h27.Layers);
        if (layers27 > layers24)
        {
            // a branding array's layers past what its stamp's brand hotspots draw are never seen: those are left out
            int used = BrandHotspots(_i27, n27);
            if (used < 0 || used > layers24) throw new InvalidDataException($"{n27} has {layers27} layers; the NHL 24 copy has room for {layers24}.");
            Log.Add($"{Short(newName)}: NHL 27's first {layers24} of {layers27} layers (its stamp draws {used})");
            layers27 = layers24;
        }
        int shift = 0;
        while ((h27.Width >> shift) > h24.Width) shift++;
        if ((h27.Width >> shift) != h24.Width || (h27.Height >> shift) != h24.Height)
            throw new InvalidDataException($"{n27} is {h27.Width}x{h27.Height}; NHL 24's {Short(newName)} is {h24.Width}x{h24.Height}.");
        int n = TextureBuilder.StoredMips(h24);
        bool sameFormat = h27.Format.Name == h24.Format.Name && h27.Format.Known;
        bool tiled = h24.Flags.HasFlag(TextureFlags.Swizzle);
        byte[] donor = null;
        int blank = -1;
        if (layers27 < layers24 && !_fillLast)
        {
            donor = DonorRaw(c, newName);
            blank = BlankLayer(c, newName);
            if (blank < 0) throw new InvalidDataException($"{Short(newName)}: the source kit has no blank layer to fill layers {layers27}-{layers24 - 1} with.");
        }
        long DonorAt(int mip, int layer)
        {
            long at = 0;
            for (int k = 0; k < mip; k++) at += (long)h24.MipSizes[k] * layers24;
            return at + (long)layer * h24.MipSizes[mip];
        }
        if (donor != null && donor.Length < DonorAt(n - 1, layers24 - 1) + h24.MipSizes[n - 1])
            throw new InvalidDataException($"{Short(newName)}: the source kit's chunk ({donor.Length:N0} bytes) isn't its full chain.");
        if (_ownLayers) Log.Add($"{Short(newName)}: NHL 24's own, layers 1-2 (holes, dimple mesh) as its plain knit");
        if (_twill is { } tw) Log.Add($"{Short(newName)}: layer {tw.Twill} (blotchy stitch) as layer {tw.Flat} (flat)");
        if (layers27 < layers24) Log.Add(_fillLast ? (layers27 == layers24 - 1 ? $"{Short(newName)}: NHL 27's {layers27} layers in NHL 24's order (layers 1-2, the holes and dimple mesh, as NHL 27's plain knit)" : $"{Short(newName)}: NHL 27's {layers27} layers, the last repeated for {layers24 - layers27} more")
            : $"{Short(newName)}: NHL 27's {layers27} layers, {layers24 - layers27} blank (the source kit's layer {blank})");
        using var ms = new MemoryStream();
        if (!sameFormat)
        {
            var imgs = new byte[layers24][];
            for (int l = 0; l < layers24; l++)
            {
                if (l >= layers27 && !_fillLast) { imgs[l] = null; continue; }
                t.Layer = SourceLayer(l, layers24, layers27);
                t.DecodeMip(shift);
                if (t.Problem != null) throw new InvalidDataException($"{n27}: {t.Problem}");
                imgs[l] = t.Rgba;
            }
            if (imgs.Any(x => x == null)) throw new InvalidDataException($"{n27}: another format and fewer layers isn't handled.");
            Log.Add($"{Short(newName)}: encoded again ({h27.Format.Name} -> {h24.Format.Name})");
            return KitClone.BuildPs4(h24, imgs);
        }
        for (int i = 0; i < n; i++)
        {
            var (mw, mh) = h24.MipSize(i);
            int bw = (mw + 3) / 4, bh = (mh + 3) / 4;
            for (int l = 0; l < layers24; l++)
            {
                if (l >= layers27 && !_fillLast)
                {
                    ms.Write(donor.AsSpan((int)DonorAt(i, blank), (int)h24.MipSizes[i]));
                    continue;
                }
                t.Layer = SourceLayer(l, layers24, layers27);
                t.DecodeMip(i + shift);
                if (t.Problem != null || t.Blocks == null) throw new InvalidDataException($"{n27} mip {i + shift}: {t.Problem ?? "no blocks"}");
                if (t.Width != mw || t.Height != mh) throw new InvalidDataException($"{n27} mip {i + shift} is {t.Width}x{t.Height}, NHL 24's mip {i} {mw}x{mh}.");
                var blocks = t.Blocks.Length == bw * bh * h24.Format.BlockBytes ? t.Blocks : t.Blocks[..(bw * bh * h24.Format.BlockBytes)];
                var mip = tiled ? Ps4Tiling.Tile(blocks, bw, bh, h24.Format.BlockBytes) : blocks;
                if (mip.Length != h24.MipSizes[i]) throw new InvalidDataException($"{Short(newName)} mip {i}: {mip.Length} bytes, the header says {h24.MipSizes[i]}.");
                ms.Write(mip);
            }
        }
        return ms.ToArray();
    }

    /// <summary>
    /// A stamp (RawFileDataAsset with the hotspot XML inline): NHL 27's XML, its index="-1" attributes dropped and the
    /// whitespace between tags taken out, written over the copy's and padded with spaces to the same size. Null for other files.
    /// </summary>
    public byte[] Stamp(KitCopy c, string newName, byte[] ebx)
    {
        string file = newName[(newName.LastIndexOf('/') + 1)..];
        // the stamp itself, not its bundle's blueprint (stamp_..._player_team_brt)
        if (!file.StartsWith("stamp_") || !Encoding.Latin1.GetString(ebx).Contains("<hotspots>")) return null;
        var e27 = _i27.GetEbx(Name27(c, newName));
        if (e27 == null) { Log.Add($"{file}: kept (NHL 27 has none)"); return null; }
        var s27 = Encoding.Latin1.GetString(_i27.Read(e27));
        int a = s27.IndexOf("<?xml", StringComparison.Ordinal), z = s27.LastIndexOf("</hotspots>", StringComparison.Ordinal);
        if (a < 0 || z < 0) throw new InvalidDataException($"{Name27(c, newName)} holds no hotspot XML.");
        string xml = s27[a..(z + "</hotspots>".Length)];
        xml = System.Text.RegularExpressions.Regex.Replace(xml, @"\s+index=""-?\d+""", "");
        xml = System.Text.RegularExpressions.Regex.Replace(xml, @">\s+<", "><");
        xml = xml.Replace("\"/>", "\" />");
        // the copy's XML and the spaces after it: the room there is
        var s = Encoding.Latin1.GetString(ebx);
        int b = s.IndexOf("<?xml", StringComparison.Ordinal), y = s.LastIndexOf("</hotspots>", StringComparison.Ordinal);
        if (b < 0 || y < 0) throw new InvalidDataException($"{newName} holds no hotspot XML.");
        int end = y + "</hotspots>".Length;
        while (end < s.Length && s[end] == ' ') end++;
        int room = end - b;
        // too long: the numbers without their trailing zeros (1.0000 -> 1.0, same values)
        if (xml.Length > room)
            xml = System.Text.RegularExpressions.Regex.Replace(xml, @"=""(-?\d+\.\d+?)0+""", "=\"$1\"");
        if (xml.Length > room) throw new InvalidDataException($"{file}: NHL 27's hotspots take {xml.Length} bytes, the copy has room for {room}.");
        var o = (byte[])ebx.Clone();
        var bytes = Encoding.Latin1.GetBytes(xml.PadRight(room));
        bytes.CopyTo(o, b);
        Log.Add($"{file}: NHL 27's hotspots ({xml.Length} of {room} bytes)");
        return o;
    }

    /// <summary>How many layers of a branding array (branding_&lt;part&gt;_…_array) its stamp draws: up to its last brand hotspot
    /// switched on. -1 when it has no stamp.</summary>
    public static int BrandHotspots(AssetIndex index, string arrayName)
    {
        var m = System.Text.RegularExpressions.Regex.Match(arrayName, @"^(.*/)branding_([a-z]+)_(.*?)_(color|normal|coeff)_array$");
        if (!m.Success) return -1;
        var e = index.GetEbx($"{m.Groups[1].Value}stamp_{m.Groups[2].Value}_{m.Groups[3].Value}");
        if (e == null) return -1;
        // brand hotspots take the layers in order; past the last one switched on, none is drawn
        var hs = System.Text.RegularExpressions.Regex.Matches(Encoding.Latin1.GetString(index.Read(e)), @"<hotspot [^>]*type=""brand""[^>]*>");
        int used = 0;
        for (int i = 0; i < hs.Count; i++) if (!hs[i].Value.Contains("enable=\"0.0\"")) used = i + 1;
        return used;
    }

    private static string Short(string name) => name[(name.LastIndexOf('/') + 1)..];
}
