using System.Buffers.Binary;

namespace NHL27Kit.Core;

/// <summary>
/// Centre-ice ads drawn from the centre logo's texture, so each arena can have its own (the game's CenterAd1..4 hotspots
/// are the same four ids in every NHL arena, set at run time by its dynamic advertising, and textures can't be resized).
/// - The logo's texture keeps the logo in its top-left LogoFrac, and the four ads in the strips that frees up: two along
///   the bottom, two turned on their side down the right (<see cref="Boxes"/>, in 1024² units; a 2048² logo keeps more
///   of the logo's detail).
/// - The NHL ice: the ice_centerlogo_mat section's decal UVs are scaled by LogoFrac (the logo looks as before), and the
///   four ice_centerad*_mat sections take the logo's material (the section record's byte +0x1C) with their decal UVs
///   moved from the hotspot's 0..1 into their box. The decals are drawn with UV3 (usage 0x23); UV2 (0x22) is set to the
///   same. The ad sections stay where they are on the ice.
/// So every arena's logo texture has to be laid out again (<see cref="Compose"/>), or its logo comes out cropped.
/// </summary>
public static class LogoAds
{
    public const int Size = CenterIce.LogoSize;
    public const double LogoFrac = 0.79;

    /// <summary>An ad's place in the logo texture (pixels); a turned one holds the ad's u down and its v across.</summary>
    public readonly record struct Box(int X, int Y, int W, int H, bool Turned);

    /// <summary>
    /// Where CenterAd1..4 (<see cref="CenterIce.CenterAds"/>) go: 368×184 each (2:1, as the hotspots), 4 px clear of the
    /// texture's edges and 27 px clear of the logo's 809 px, so filtering doesn't bleed one into another.
    /// </summary>
    public static readonly Box[] Boxes =
    {
        new(24, 836, 368, 184, false),
        new(440, 836, 368, 184, false),
        new(836, 24, 184, 368, true),
        new(836, 440, 184, 368, true),
    };

    public const string LogoMaterial = "ice_centerlogo_mat";
    public const string NhlIce = "content/worlds/rink/ice/components/rink_ice_nhl_mesh";

    /// <summary>The ice meshes with a centre-logo section (they all draw ice_centerlogo_N_color).</summary>
    public static readonly string[] Meshes =
    {
        NhlIce,
        "content/worlds/rink/ice/components/rink_ice_intl_mesh",
        "content/worlds/rink/ice/components/rink_ice_arcade_mesh",
        "content/worlds/rink/ice/components/rink_ice_outdoorfives_mesh",
        "content/worlds/rink/ice/components/rink_ice_outdoorthrees_mesh",
        "content/worlds/rink/ice/components/rink_ice_threes_mesh",
    };

    /// <summary>Hotspot texel (u, v) of ad <paramref name="slot"/> as a UV in the logo texture.</summary>
    public static (double U, double V) ToLogo(int slot, double u, double v)
    {
        var b = Boxes[slot];
        return b.Turned ? ((b.X + v * b.W) / Size, (b.Y + u * b.H) / Size) : ((b.X + u * b.W) / Size, (b.Y + v * b.H) / Size);
    }

    /// <summary>
    /// A logo texture laid out for the ads, <paramref name="size"/>²: <paramref name="logo"/> (RGBA, square,
    /// <paramref name="logoSize"/>²) shrunk into the top-left LogoFrac, and each of <paramref name="ads"/> (RGBA, any
    /// size; null leaves the box clear ice) in its box.
    /// </summary>
    public static byte[] Compose(byte[] logo, int logoSize, IReadOnlyList<(byte[] Rgba, int W, int H)> ads, int size = Size)
    {
        var dst = new byte[size * size * 4];
        int inner = (int)Math.Ceiling(size * LogoFrac);
        double k = (double)logoSize / size / LogoFrac;
        Parallel.For(0, inner, y =>
        {
            for (int x = 0; x < inner; x++)
                Sample(logo, logoSize, logoSize, (x + 0.5) * k - 0.5, (y + 0.5) * k - 0.5, dst, (y * size + x) * 4);
        });
        double s = (double)size / Size;
        for (int i = 0; i < Boxes.Length && i < ads.Count; i++)
        {
            var (img, w, h) = ads[i];
            if (img == null) continue;
            var b = Boxes[i];
            int bx = (int)(b.X * s), by = (int)(b.Y * s), bw = (int)(b.W * s), bh = (int)(b.H * s);
            Parallel.For(by, by + bh, y =>
            {
                for (int x = bx; x < bx + bw; x++)
                {
                    double across = (x - bx + 0.5) / bw, down = (y - by + 0.5) / bh;
                    double u = b.Turned ? down : across, v = b.Turned ? across : down;
                    Sample(img, w, h, u * w - 0.5, v * h - 0.5, dst, (y * size + x) * 4);
                }
            });
        }
        return dst;
    }

    /// <summary>Bilinear, weighted by alpha (so transparent texels' colour doesn't bleed into the edges).</summary>
    private static void Sample(byte[] p, int w, int h, double x, double y, byte[] dst, int o)
    {
        x = Math.Clamp(x, 0, w - 1); y = Math.Clamp(y, 0, h - 1);
        int x0 = (int)x, y0 = (int)y, x1 = Math.Min(x0 + 1, w - 1), y1 = Math.Min(y0 + 1, h - 1);
        double fx = x - x0, fy = y - y0;
        double r = 0, g = 0, b = 0, a = 0;
        void Add(int px, int py, double wt)
        {
            int s = (py * w + px) * 4;
            double al = p[s + 3] * wt;
            r += p[s] * al; g += p[s + 1] * al; b += p[s + 2] * al; a += al;
        }
        Add(x0, y0, (1 - fx) * (1 - fy)); Add(x1, y0, fx * (1 - fy)); Add(x0, y1, (1 - fx) * fy); Add(x1, y1, fx * fy);
        if (a <= 0) return;
        dst[o] = (byte)Math.Round(r / a); dst[o + 1] = (byte)Math.Round(g / a); dst[o + 2] = (byte)Math.Round(b / a);
        dst[o + 3] = (byte)Math.Clamp(Math.Round(a), 0, 255);
    }

    /// <summary>What <see cref="PatchMesh"/> did to one section.</summary>
    public sealed record SectionChange(int Lod, int Index, string Material, string Change, (float U0, float V0, float U1, float V1) Before, (float U0, float V0, float U1, float V1) After);

    /// <summary>
    /// Lays one ice mesh out for the ads: <paramref name="res"/> (changed in place) and its chunks (through
    /// <paramref name="chunkOf"/>, changed in place). <paramref name="moveAds"/> gives the centre-ad sections the logo's
    /// material and box UVs (the NHL ice). Throws if a section isn't as expected, before anything is changed.
    /// </summary>
    public static List<SectionChange> PatchMesh(byte[] res, Func<Guid, byte[]> chunkOf, bool moveAds)
    {
        var sections = MeshWriter.Sections(res);
        var work = new List<(MeshWriter.Section S, int Slot, int Material)>();
        foreach (var lod in sections.GroupBy(s => s.Lod))
        {
            var logo = lod.FirstOrDefault(s => s.Material.Equals(LogoMaterial, StringComparison.OrdinalIgnoreCase));
            if (logo == null) continue;
            work.Add((logo, -1, -1));
            if (!moveAds) continue;
            for (int i = 0; i < CenterIce.CenterAds.Length; i++)
            {
                var ad = lod.FirstOrDefault(s => s.Material.Equals(CenterIce.CenterAds[i].Material, StringComparison.OrdinalIgnoreCase));
                if (ad == null) continue;
                if (res[ad.At + 0x1C] == res[logo.At + 0x1C])
                    throw new InvalidDataException($"The ice is already laid out for the ads ({ad.Material} has the logo's material). Restore its mod entry first.");
                if (!Same(ad.Elements, logo.Elements))
                    throw new InvalidDataException($"LOD{lod.Key} {ad.Material}'s vertices aren't laid out as the logo's, so it can't take its material.");
                work.Add((ad, i, res[logo.At + 0x1C]));
            }
        }
        if (work.Count == 0) throw new InvalidDataException("The mesh has no centre-logo section.");
        foreach (var (s, _, _) in work)
            if (Uv(s, 0x22) is not { Format: 3 or 6 } || Uv(s, 0x23) is not { Format: 3 or 6 })
                throw new InvalidDataException($"LOD{s.Lod} {s.Material} hasn't a float2/half2 UV2 and UV3.");

        var changes = new List<SectionChange>();
        foreach (var (s, slot, material) in work)
        {
            var chunk = chunkOf(s.Chunk);
            var uv2 = Uv(s, 0x22).Value;
            var uv3 = Uv(s, 0x23).Value;
            var before = Range(chunk, uv3.At, uv3.Format, uv3.Size, s.VertexCount);
            for (int v = 0; v < s.VertexCount; v++)
            {
                int p3 = uv3.At + v * uv3.Size, p2 = uv2.At + v * uv2.Size;
                if (slot < 0)
                {
                    // the logo: its UV3 scaled into the top-left LogoFrac (and the NHL ice's UV2, which there is the
                    // same 0..1; the other ice meshes' UV2 isn't)
                    foreach (var (at, format, size) in moveAds ? new[] { uv2, uv3 } : new[] { uv3 })
                        for (int c = 0; c < 2; c++)
                            Write(chunk, at + v * size, format, c, Read(chunk, at + v * size, format, c) * LogoFrac);
                    continue;
                }
                // an ad: its hotspot texel (UV3, as the game draws it) into its box, in both UVs
                var (u, w) = ToLogo(slot, Read(chunk, p3, uv3.Format, 0), Read(chunk, p3, uv3.Format, 1));
                Write(chunk, p3, uv3.Format, 0, u); Write(chunk, p3, uv3.Format, 1, w);
                Write(chunk, p2, uv2.Format, 0, u); Write(chunk, p2, uv2.Format, 1, w);
            }
            string change = slot < 0 ? (moveAds ? $"UV2, UV3 × {LogoFrac}" : $"UV3 × {LogoFrac}") : $"material {res[s.At + 0x1C]} -> {material} (logo), UV3 into box {slot + 1}";
            if (slot >= 0) res[s.At + 0x1C] = (byte)material;
            changes.Add(new SectionChange(s.Lod, s.Index, s.Material, change, before, Range(chunk, uv3.At, uv3.Format, uv3.Size, s.VertexCount)));
        }
        return changes;
    }

    private static bool Same(List<(int Usage, int Format, int Size)> a, List<(int Usage, int Format, int Size)> b) => a.SequenceEqual(b);

    /// <summary>Where a section's element <paramref name="usage"/> starts in its chunk: each element is its own stream.</summary>
    private static (int At, int Format, int Size)? Uv(MeshWriter.Section s, int usage)
    {
        int at = s.VertexOffset;
        foreach (var (u, format, size) in s.Elements)
        {
            if (u == usage) return (at, format, size);
            at += size * s.VertexCount;
        }
        return null;
    }

    private static double Read(byte[] b, int p, int format, int c) =>
        format == 3 ? BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(p + c * 4)) : (double)BinaryPrimitives.ReadHalfLittleEndian(b.AsSpan(p + c * 2));

    private static void Write(byte[] b, int p, int format, int c, double value)
    {
        if (format == 3) BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(p + c * 4), (float)value);
        else BinaryPrimitives.WriteHalfLittleEndian(b.AsSpan(p + c * 2), (Half)value);
    }

    private static (float, float, float, float) Range(byte[] b, int at, int format, int size, int n)
    {
        float u0 = float.MaxValue, v0 = float.MaxValue, u1 = float.MinValue, v1 = float.MinValue;
        for (int v = 0; v < n; v++)
        {
            float u = (float)Read(b, at + v * size, format, 0), w = (float)Read(b, at + v * size, format, 1);
            u0 = Math.Min(u0, u); u1 = Math.Max(u1, u); v0 = Math.Min(v0, w); v1 = Math.Max(v1, w);
        }
        return (u0, v0, u1, v1);
    }
}
