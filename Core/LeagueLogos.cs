using System.Buffers.Binary;

namespace NHL27Kit.Core;

/// <summary>
/// Team logos for teams that have none in the game. NHL 24 looks a logo up as fb://ui/textures/common/teamlogos/
/// &lt;style&gt;/t&lt;artid&gt; and, when there's no such texture, as a loose live-content file:
/// nhl/ui/kettle_assets/teamlogos/&lt;style&gt;/t&lt;artid&gt;.gnf (eboot strings "nhl/ui/%s/teamlogos/%s/t%d.%s",
/// "kettle_assets", "gnf"), which the game opens from /download0 (NHL24 PC's user/download/CUSA37933). So a new team gets an art id no texture has (2901 and up) and one GNF per style there.
/// The GNFs are BC7, tiled, one mip, with the header of one of the game's own kettle GNFs patched for each size.
/// </summary>
public static class LeagueLogos
{
    public const int FirstArtId = 2901;
    public const string TitleId = "CUSA37933";
    /// <summary>
    /// Where the game looks for the loose logos, below the game folder's parent (NHL24 PC's user folder). Seen in the
    /// emulator's log: it tries /download0/nhl/ui/kettle_assets/... then /temp0/nhl/ui/kettle_assets/...; /download0 is
    /// user/download/CUSA37933 and stays, /temp0 (user/temp/CUSA37933) is emptied at every start. So the logos go in
    /// /download0; that folder is the one a restore keeps (only what's below it that the mod made goes).
    /// </summary>
    public static readonly string[] TempData = { "../user/download/" + TitleId };
    public static string Folder(string tempData) => tempData + "/nhl/ui/kettle_assets/teamlogos";

    public enum Look { Plain, Mono, Glossy, Left, Right, CardBw, Blue }

    /// <summary>The logo styles of NHL 24's teamlogos folder, at the sizes its own textures have.</summary>
    public static readonly (string Style, int W, int H, Look Look)[] Styles =
    {
        ("extralargemono", 1024, 1024, Look.Mono),
        ("large", 512, 512, Look.Plain), ("largemono", 512, 512, Look.Mono),
        ("largestylized/left", 512, 512, Look.Left), ("largestylized/right", 512, 512, Look.Right),
        ("medium", 256, 256, Look.Plain), ("mediumglossy", 256, 256, Look.Glossy), ("mediummono", 256, 256, Look.Mono),
        ("mediumstylized/left", 256, 256, Look.Left), ("mediumstylized/right", 256, 256, Look.Right), ("hutcardmedium", 256, 256, Look.Plain),
        ("small", 128, 128, Look.Plain), ("smallglossy", 128, 128, Look.Glossy),
        ("smallstylized/left", 128, 128, Look.Left), ("smallstylized/right", 128, 128, Look.Right),
        ("extrasmall", 80, 80, Look.Plain), ("extrasmallmono", 80, 80, Look.Mono), ("hutcardsmalllogo", 80, 80, Look.Plain),
        ("hutcardbw", 332, 464, Look.CardBw),
    };

    /// <summary>Changes when <see cref="Draw"/> or <see cref="Gnf"/> draw differently, so cached logos are built again.</summary>
    private const string DrawVersion = "logos-1";

    public static string FileFor(string tempData, string style, int artId) => $"{Folder(tempData)}/{style}/t{artId}.gnf";

    private const string HeaderCollector = "globals/chunkfilecollectoruikettle_swfassets";

    /// <summary>The 256-byte header of one of the game's own BC7 kettle GNFs (one texture, one mip).</summary>
    public static byte[] TemplateHeader(AssetIndex index)
    {
        var legacy = new Legacy(index, HeaderCollector);
        foreach (var name in legacy.Names.Where(n => n.EndsWith(".gnf", StringComparison.OrdinalIgnoreCase)).OrderBy(n => n, StringComparer.Ordinal))
        {
            var f = legacy.Find(name);
            if (f == null || f.Size < 0x100) continue;
            var h = legacy.Read(f).AsSpan(0, 0x100).ToArray();
            uint w1 = BinaryPrimitives.ReadUInt32LittleEndian(h.AsSpan(0x14));
            uint w3 = BinaryPrimitives.ReadUInt32LittleEndian(h.AsSpan(0x1C));
            // BC7 (data format 41), one texture, one mip (base and last level 0)
            if (BinaryPrimitives.ReadUInt32BigEndian(h) == 0x474E4620 && h[9] == 1 && ((w1 >> 20) & 0x3F) == 41 &&
                BinaryPrimitives.ReadUInt32LittleEndian(h.AsSpan(4)) == 0xF8 && ((w3 >> 12) & 0xFF) == 0)
                return h;
        }
        throw new InvalidDataException("No BC7 GNF found in the game's kettle assets to copy a header from.");
    }

    /// <summary>A GNF of <paramref name="rgba"/> (w × h): BC7, PS4-tiled, the template header patched for the size.</summary>
    public static byte[] Gnf(byte[] header, byte[] rgba, int w, int h)
    {
        var format = TextureFormat.Of(0x43);
        int bw = (w + 3) / 4, bh = (h + 3) / 4;
        var tiled = Ps4Tiling.Tile(BcEncoder.Encode(rgba, w, h, format), bw, bh, 16);
        var o = new byte[0x100 + tiled.Length];
        header.AsSpan(0, 0x100).CopyTo(o);
        tiled.CopyTo(o, 0x100);
        uint W(int i) => BinaryPrimitives.ReadUInt32LittleEndian(o.AsSpan(0x10 + i * 4));
        void Set(int i, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(0x10 + i * 4), v);
        // word 2: width - 1 (bits 0-13), height - 1 (bits 14-27); word 4: pitch - 1 at bits 13-26 (whole 8-block tiles);
        // word 7: the data's size
        Set(2, (W(2) & 0xF0000000u) | (uint)(w - 1) | ((uint)(h - 1) << 14));
        uint pitch = (uint)((bw + 7) / 8 * 8 * 4);
        Set(4, (W(4) & ~(0x3FFFu << 13)) | ((pitch - 1) << 13));
        Set(7, (uint)tiled.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(12), (uint)o.Length);
        return o;
    }

    /// <summary>The league logo textures (ui/textures/common/leaguelogos/&lt;variant&gt;/l&lt;id&gt;) and how each is drawn.</summary>
    public static readonly (string Variant, Look Look)[] LeagueVariants =
    {
        ("size64", Look.Plain), ("size256", Look.Plain), ("size512", Look.Plain),
        ("smallstylized/left", Look.Left), ("smallstylized/right", Look.Right),
        ("mediumstylized/left", Look.Left), ("mediumstylized/right", Look.Right),
        ("largestylized/left", Look.Left), ("largestylized/right", Look.Right),
        ("bluestylized", Look.Blue),
    };

    public static string LeagueTexture(string variant, int leagueId) => $"ui/textures/common/leaguelogos/{variant}/l{leagueId}";

    /// <summary>
    /// A new league's logo textures, each a copy of stock league <paramref name="templateId"/>'s (it has every variant;
    /// its id must have as many digits) with the PNG drawn at that texture's size.
    /// </summary>
    public static List<TextureClone> LeagueClones(AssetIndex index, int leagueId, string png, int templateId = 20)
    {
        if (leagueId.ToString().Length != templateId.ToString().Length) throw new ArgumentException($"League {leagueId} needs a template league id with as many digits.");
        var (src, sw, sh) = PngReader.Load(png);
        var list = new List<TextureClone>();
        foreach (var (variant, look) in LeagueVariants)
        {
            string from = LeagueTexture(variant, templateId);
            var res = index.Res.TryGetValue(from, out var r) ? r : throw new InvalidDataException($"No texture {from}.");
            var h = TextureHeader.Parse(index.Read(res), ps4: true);
            list.Add(new TextureClone(from, LeagueTexture(variant, leagueId), Draw(src, sw, sh, h.Width, h.Height, look)));
        }
        return list;
    }

    /// <summary>The logo drawn in one style at w × h (straight alpha, RGBA8).</summary>
    public static byte[] Draw(byte[] src, int sw, int sh, int w, int h, Look look)
    {
        (src, sw, sh) = Trim(src, sw, sh);
        if (look == Look.CardBw)
        {
            // the card's black-and-white logo is big and cropped by the card's edges
            var big = Fit(src, sw, sh, w, h, 1.18);
            return Mono(big);
        }
        var img = Fit(src, sw, sh, w, h, 0.94);
        return look switch
        {
            Look.Mono => Mono(img),
            Look.Glossy => Glossy(img, w, h),
            Look.Left => Turned(img, w, h, left: true),
            Look.Right => Turned(img, w, h, left: false),
            Look.Blue => Blue(img),
            _ => img,
        };
    }

    /// <summary>The image cut to its visible pixels.</summary>
    private static (byte[], int, int) Trim(byte[] src, int w, int h)
    {
        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (src[(y * w + x) * 4 + 3] > 8) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
        if (x1 < 0) return (src, w, h);
        int nw = x1 - x0 + 1, nh = y1 - y0 + 1;
        var o = new byte[nw * nh * 4];
        for (int y = 0; y < nh; y++) src.AsSpan(((y + y0) * w + x0) * 4, nw * 4).CopyTo(o.AsSpan(y * nw * 4));
        return (o, nw, nh);
    }

    /// <summary>Scaled to fill <paramref name="fill"/> of the canvas' smaller side's fit, centred on a transparent canvas.</summary>
    private static byte[] Fit(byte[] src, int sw, int sh, int w, int h, double fill)
    {
        double s = Math.Min(w / (double)sw, h / (double)sh) * fill;
        int fw = Math.Max(1, (int)Math.Round(sw * s)), fh = Math.Max(1, (int)Math.Round(sh * s));
        var scaled = Scale(src, sw, sh, fw, fh);
        var o = new byte[w * h * 4];
        int ox = (w - fw) / 2, oy = (h - fh) / 2;
        for (int y = 0; y < fh; y++)
        {
            int ty = y + oy;
            if (ty < 0 || ty >= h) continue;
            for (int x = 0; x < fw; x++)
            {
                int tx = x + ox;
                if (tx < 0 || tx >= w) continue;
                scaled.AsSpan((y * fw + x) * 4, 4).CopyTo(o.AsSpan((ty * w + tx) * 4));
            }
        }
        return o;
    }

    /// <summary>Resampled with premultiplied alpha (area average when shrinking, bilinear when growing).</summary>
    private static byte[] Scale(byte[] src, int sw, int sh, int dw, int dh)
    {
        var o = new byte[dw * dh * 4];
        double rx = sw / (double)dw, ry = sh / (double)dh;
        Parallel.For(0, dh, y =>
        {
            for (int x = 0; x < dw; x++)
            {
                double r = 0, g = 0, b = 0, a = 0, n = 0;
                if (rx >= 1 && ry >= 1)
                {
                    int x0 = (int)(x * rx), x1 = Math.Min(sw, Math.Max(x0 + 1, (int)((x + 1) * rx)));
                    int y0 = (int)(y * ry), y1 = Math.Min(sh, Math.Max(y0 + 1, (int)((y + 1) * ry)));
                    for (int yy = y0; yy < y1; yy++)
                        for (int xx = x0; xx < x1; xx++)
                        {
                            int i = (yy * sw + xx) * 4;
                            double al = src[i + 3];
                            r += src[i] * al; g += src[i + 1] * al; b += src[i + 2] * al; a += al; n++;
                        }
                }
                else
                {
                    double fx = Math.Clamp((x + 0.5) * rx - 0.5, 0, sw - 1), fy = Math.Clamp((y + 0.5) * ry - 0.5, 0, sh - 1);
                    int ix = (int)fx, iy = (int)fy, jx = Math.Min(sw - 1, ix + 1), jy = Math.Min(sh - 1, iy + 1);
                    double tx = fx - ix, ty = fy - iy;
                    foreach (var (px, py, wgt) in new[] { (ix, iy, (1 - tx) * (1 - ty)), (jx, iy, tx * (1 - ty)), (ix, jy, (1 - tx) * ty), (jx, jy, tx * ty) })
                    {
                        int i = (py * sw + px) * 4;
                        double al = src[i + 3] * wgt;
                        r += src[i] * al; g += src[i + 1] * al; b += src[i + 2] * al; a += al; n += wgt;
                    }
                }
                int o4 = (y * dw + x) * 4;
                if (a > 0) { o[o4] = (byte)Math.Round(r / a); o[o4 + 1] = (byte)Math.Round(g / a); o[o4 + 2] = (byte)Math.Round(b / a); }
                o[o4 + 3] = (byte)Math.Round(n > 0 ? a / n : 0);
            }
        });
        return o;
    }

    /// <summary>Black, darker parts more solid (the game's mono logos: black shapes, light parts see-through).</summary>
    private static byte[] Mono(byte[] img)
    {
        var o = new byte[img.Length];
        for (int i = 0; i < img.Length; i += 4)
        {
            double lum = (0.2126 * img[i] + 0.7152 * img[i + 1] + 0.0722 * img[i + 2]) / 255.0;
            o[i + 3] = (byte)Math.Round(img[i + 3] * Math.Clamp(1.1 - lum, 0.3, 1.0));
        }
        return o;
    }

    /// <summary>The league screens' blue glass look: brightness kept, every colour turned to the same electric blue.</summary>
    private static byte[] Blue(byte[] img)
    {
        var o = new byte[img.Length];
        for (int i = 0; i < img.Length; i += 4)
        {
            double lum = (0.2126 * img[i] + 0.7152 * img[i + 1] + 0.0722 * img[i + 2]) / 255.0;
            double v = 0.35 + 0.65 * lum;
            o[i] = (byte)Math.Round(70 * v);
            o[i + 1] = (byte)Math.Round(110 * v);
            o[i + 2] = (byte)Math.Round(255 * v);
            o[i + 3] = img[i + 3];
        }
        return o;
    }

    /// <summary>A soft white sheen over the logo's top half.</summary>
    private static byte[] Glossy(byte[] img, int w, int h)
    {
        var o = (byte[])img.Clone();
        for (int y = 0; y < h * 55 / 100; y++)
        {
            double k = 0.38 * (1 - y / (h * 0.55));
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                if (o[i + 3] == 0) continue;
                for (int c = 0; c < 3; c++) o[i + c] = (byte)Math.Round(o[i + c] + (255 - o[i + c]) * k);
            }
        }
        return o;
    }

    /// <summary>The logo turned a little to one side: narrower, the far edge shorter and darker.</summary>
    private static byte[] Turned(byte[] img, int w, int h, bool left)
    {
        var o = new byte[img.Length];
        double squeeze = 0.74;
        int cw = (int)(w * squeeze), x0 = (w - cw) / 2;
        for (int x = 0; x < cw; x++)
        {
            double t = x / (double)Math.Max(1, cw - 1);
            double near = left ? 1 - t : t;           // 1 at the near edge
            double sy = 0.86 + 0.14 * near;            // the far edge is shorter
            double shade = 0.72 + 0.28 * near;
            int sx = Math.Clamp((int)(t * (w - 1)), 0, w - 1);
            for (int y = 0; y < h; y++)
            {
                double fy = (y - h / 2.0) / sy + h / 2.0;
                int iy = (int)Math.Round(fy);
                if (iy < 0 || iy >= h) continue;
                int si = (iy * w + sx) * 4, di = (y * w + x0 + x) * 4;
                o[di] = (byte)(img[si] * shade); o[di + 1] = (byte)(img[si + 1] * shade); o[di + 2] = (byte)(img[si + 2] * shade);
                o[di + 3] = img[si + 3];
            }
        }
        return o;
    }

    /// <summary>
    /// Every style's file for each team that has a logo: (path below the game folder, GNF bytes). Teams whose plan has
    /// no art id, or whose logo file is missing, are skipped.
    /// </summary>
    public static List<(string Style, int ArtId, byte[] Data)> Build(AssetIndex index, string packDir, IEnumerable<LeagueTeamPlan> plans, Action<string> log = null)
    {
        var header = TemplateHeader(index);
        var files = new List<(string, int, byte[])>();
        foreach (var p in plans)
        {
            if (p.ArtId < 0 || LogoPath(packDir, p.Team) is not { } path) continue;
            // encoding takes seconds a team: kept by the PNG's and header's hash (and this drawing's version), built once
            byte[] png = File.ReadAllBytes(path);
            string key = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData([.. png, .. header, .. System.Text.Encoding.ASCII.GetBytes(DrawVersion)]))[..16];
            string cache = Path.Combine(KitPaths.Root, "cache", "league-logos", key);
            (byte[], int, int)? img = null;
            int built = 0;
            foreach (var s in Styles)
            {
                string cf = Path.Combine(cache, s.Style.Replace('/', '_') + ".gnf");
                byte[] gnf = File.Exists(cf) ? File.ReadAllBytes(cf) : null;
                if (gnf == null)
                {
                    img ??= PngReader.Read(png, path);
                    var (rgba, w, h) = img.Value;
                    gnf = Gnf(header, Draw(rgba, w, h, s.W, s.H, s.Look), s.W, s.H);
                    Directory.CreateDirectory(cache);
                    File.WriteAllBytes(cf, gnf);
                    built++;
                }
                files.Add((s.Style, p.ArtId, gnf));
            }
            log?.Invoke($"logo t{p.ArtId}: {p.Team.FullName} ({Styles.Length} styles{(built < Styles.Length ? $", {Styles.Length - built} from the cache" : "")})");
        }
        return files;
    }

    /// <summary>Reads every team's logo PNG (quick), so a file the reader can't take fails before minutes of encoding.</summary>
    public static List<string> CheckPngs(string packDir, IEnumerable<LeagueTeamPlan> plans)
    {
        var bad = new List<string>();
        foreach (var p in plans.Where(p => p.ArtId >= 0))
        {
            try { PngReader.Load(LogoPath(packDir, p.Team) ?? throw new FileNotFoundException("missing")); }
            catch (Exception e) { bad.Add($"{p.Team.FullName}: {p.Team.Logo}: {e.Message}"); }
        }
        return bad;
    }

    public static string LogoPath(string packDir, PackTeam t)
    {
        if (string.IsNullOrEmpty(t.Logo) || packDir == null) return null;
        string path = Path.Combine(packDir, t.Logo.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? path : null;
    }

    /// <summary>A preview of one style (for the app and checks): the PNG drawn as the GNF would show it.</summary>
    public static byte[] PreviewRgba(string file, Look look, int w, int h)
    {
        var (rgba, sw, sh) = PngReader.Load(file);
        return Draw(rgba, sw, sh, w, h, look);
    }
}
