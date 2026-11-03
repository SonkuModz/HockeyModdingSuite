using System.Buffers.Binary;

namespace NHL27Kit.Core;

[Flags]
public enum TextureFlags : ushort
{
    Streaming = 0x1,
    SrgbGamma = 0x2,
    CpuResource = 0x4,
    OnDemandLoaded = 0x8,
    Mutable = 0x10,
    NoSkipmip = 0x20,
    StreamingAlways = 0x400,
    Swizzle = 0x800,
}

public enum TextureType : uint
{
    TT_2d = 0,
    TT_Cube = 1,
    TT_3d = 2,
    TT_2dArray = 3,
    TT_1dArray = 4,
    TT_1d = 5,
    TT_CubeArray = 6,
}

/// <summary>
/// An NHL 27 PS5 texture header (the texture's RES, 184 bytes). Same fields as NHL 24's PS4 header, except two small u8
/// fields now sit at +32/+33, which moves the chunk id to +40 and everything after it 8 bytes on (layout as worked out in
/// NHL27TextureViewer and checked against real headers):
///   +0 mip offsets[2], +8 type, +12 pixel format, +16 pool id, +20 flags, +22 width, +24 height, +26 depth,
///   +28 slice count, +30 mip count, +31 first mip, +32/+33 unknown u8s, +40 chunk id, +56 mip sizes[15],
///   +116 chunk size, +120 hash of the texture's RES name.
/// NHL 24's PS4 header (<see cref="Ps4"/>) lacks the two u8s: chunk id at +32, mip sizes at +48, chunk size at +108,
/// name hash at +112, texture group at +116.
/// </summary>
public sealed class TextureHeader
{
    public uint[] MipOffsets { get; } = new uint[2];
    public TextureType Type { get; private set; }

    /// <summary>Layers of a 2D array (its slices); 1 for anything else. The chunk holds them one after another, each
    /// laid out like a plain 2D texture of <see cref="ChunkSize"/> bytes.</summary>
    public int Layers => Type == TextureType.TT_2dArray ? Math.Max(1, (int)(Ps4 ? Depth : SliceCount)) : 1;
    public int PixelFormatId { get; private set; }
    public TextureFormat Format => TextureFormat.Of(PixelFormatId);
    public uint PoolId { get; private set; }
    public TextureFlags Flags { get; private set; }
    public ushort Width { get; private set; }
    public ushort Height { get; private set; }
    public ushort Depth { get; private set; }
    public ushort SliceCount { get; private set; }
    public byte MipCount { get; private set; }
    public byte FirstMip { get; private set; }
    public byte Unknown32 { get; private set; }
    public byte Unknown33 { get; private set; }
    public Guid ChunkId { get; private set; }
    public uint[] MipSizes { get; } = new uint[15];
    public uint ChunkSize { get; private set; }
    public uint NameHash { get; private set; }

    /// <summary>
    /// NHL 24's PS4 layout: mips stored largest first from mip 0 (the bundle copy of a texture with
    /// <see cref="FirstMip"/> &gt; 0 holds only the tail), arrays mip by mip, 8x8 Morton tiles (<see cref="Ps4Tiling"/>).
    /// </summary>
    public bool Ps4 { get; private set; }

    public static TextureHeader Parse(byte[] res, bool ps4 = false)
    {
        if (res == null || res.Length < 124)
            throw new InvalidDataException($"A texture header is at least 124 bytes, this RES has {res?.Length ?? 0}.");
        var t = new TextureHeader();
        uint U(int p) => BinaryPrimitives.ReadUInt32LittleEndian(res.AsSpan(p));
        ushort S(int p) => BinaryPrimitives.ReadUInt16LittleEndian(res.AsSpan(p));
        t.MipOffsets[0] = U(0);
        t.MipOffsets[1] = U(4);
        t.Type = (TextureType)U(8);
        t.PixelFormatId = (int)U(12);
        t.PoolId = U(16);
        t.Flags = (TextureFlags)S(20);
        t.Width = S(22);
        t.Height = S(24);
        t.Depth = S(26);
        t.SliceCount = S(28);
        t.MipCount = res[30];
        t.FirstMip = res[31];
        t.Ps4 = ps4;
        int at = ps4 ? 32 : 40;
        if (!ps4)
        {
            t.Unknown32 = res[32];
            t.Unknown33 = res[33];
        }
        t.ChunkId = new Guid(res.AsSpan(at, 16));
        for (int i = 0; i < 15; i++) t.MipSizes[i] = U(at + 16 + 4 * i);
        t.ChunkSize = U(at + 76);
        t.NameHash = U(at + 80);
        return t;
    }

    /// <summary>Width and height of mip level <paramref name="mip"/>.</summary>
    public (int W, int H) MipSize(int mip) => (Math.Max(1, Width >> mip), Math.Max(1, Height >> mip));
}

/// <summary>A PS5 pixel format code, and how its blocks decode.</summary>
public sealed record TextureFormat(int Code, string Name, int BlockBytes, int DxgiFormat, string FourCC)
{
    public bool Known => BlockBytes > 0;

    /// <summary>
    /// The codes NHL 27 uses, from NHL27TextureViewer (confirmed against an FMT profile for this build):
    /// 0x36/0x37 BC1, 0x3C/0x3D BC3, 0x3F BC5, 0x42/0x43 BC7. The odd code of each pair is the sRGB variant.
    /// </summary>
    public static TextureFormat Of(int code) => code switch
    {
        0x36 => new(code, "BC1", 8, 71, "DXT1"),
        0x37 => new(code, "BC1 sRGB", 8, 72, "DXT1"),
        0x38 => new(code, "BC1A", 8, 71, "DXT1"),
        0x39 => new(code, "BC1A sRGB", 8, 72, "DXT1"),
        0x3E => new(code, "BC4", 8, 80, "ATI1"),
        0x3C => new(code, "BC3", 16, 77, "DXT5"),
        0x3D => new(code, "BC3 sRGB", 16, 78, "DXT5"),
        0x3F => new(code, "BC5", 16, 83, "ATI2"),
        0x40 => new(code, "BC6H HDR", 0, 95, null),
        0x42 => new(code, "BC7", 16, 98, null),
        0x43 => new(code, "BC7 sRGB", 16, 99, null),
        _ => new(code, $"0x{code:X2}", 0, 0, null),
    };
}
