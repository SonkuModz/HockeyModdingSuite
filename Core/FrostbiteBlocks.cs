using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NHL27Kit.Core;

/// <summary>
/// Frostbite's compressed chunk format: a sequence of blocks, each with an 8-byte header
/// [u32 BE decompressed size][u16 BE code][u16 BE compressed size], where the high byte of the
/// code is the compression type and its low nibble holds bits 16-19 of the compressed size.
///
/// NHL 24 stores texture chunks as Oodle Kraken blocks (type 0x11). FMT writes Oodle Leviathan
/// blocks (type 0x19), which the game does not use. This encoder writes Kraken blocks and, given
/// the original chunk, pads every block to its original compressed size so each block starts at
/// the same offset as before. Offsets the texture header keeps into the compressed data (which
/// FMT cannot update for NHL) then stay valid.
/// </summary>
public static class FrostbiteBlocks
{
    public const byte TypeUncompressed = 0x00;
    public const byte TypeOodleKraken = 0x11;
    private const ushort CodeFlags = 0x70;
    private const int OodleKraken = 8;
    private const int BlockSize = 0x40000;

    public readonly record struct Block(int RawSize, int CompSize, byte Type);

    public static List<Block> Parse(ReadOnlySpan<byte> data)
    {
        var blocks = new List<Block>();
        int p = 0;
        while (p + 8 <= data.Length)
        {
            int raw = (int)(BinaryPrimitives.ReadUInt32BigEndian(data.Slice(p)) & 0xFFFFFF);
            ushort code = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(p + 4));
            int comp = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(p + 6)) | ((code & 0xF) << 16);
            byte type = (byte)(code >> 8);
            if (raw == 0 || p + 8 + comp > data.Length)
                throw new InvalidDataException($"Bad chunk block header at offset {p}.");
            blocks.Add(new Block(raw, comp, type));
            p += 8 + comp;
        }
        return blocks;
    }

    /// <summary>
    /// Encodes <paramref name="raw"/> as Kraken blocks. With <paramref name="layout"/> (the original
    /// chunk's blocks, same total raw size), each block is padded to its original compressed size;
    /// returns null if any block does not fit, so the caller can fall back to the natural layout.
    /// </summary>
    public static byte[] Encode(byte[] raw, IReadOnlyList<Block> layout, int level, out string failure) =>
        Encode(raw, layout, level, pad: true, out failure);

    /// <summary>
    /// Like <see cref="Encode(byte[], IReadOnlyList{Block}, int, out string)"/>, but with
    /// <paramref name="pad"/> false the original's uncompressed block splits are kept without padding,
    /// so every block (and so every mip) still starts on a block boundary, at a new compressed offset.
    /// </summary>
    public static byte[] Encode(byte[] raw, IReadOnlyList<Block> layout, int level, bool pad, out string failure)
    {
        failure = null;
        var sizes = new List<int>();
        if (layout != null)
        {
            long total = 0;
            foreach (var b in layout)
            {
                sizes.Add(b.RawSize);
                total += b.RawSize;
            }
            if (total != raw.Length)
            {
                failure = $"original chunk holds {total} bytes but the new texture has {raw.Length}";
                return null;
            }
        }
        else
        {
            for (int o = 0; o < raw.Length; o += BlockSize)
                sizes.Add(Math.Min(BlockSize, raw.Length - o));
        }

        // Blocks compress independently, so they are compressed on all cores; the result is the same.
        var starts = new int[sizes.Count];
        for (int i = 1; i < sizes.Count; i++)
            starts[i] = starts[i - 1] + sizes[i - 1];
        var compressed = new byte[sizes.Count][];
        System.Threading.Tasks.Parallel.For(0, sizes.Count, i => compressed[i] = Compress(new ReadOnlySpan<byte>(raw, starts[i], sizes[i]), level));

        using var output = new MemoryStream();
        Span<byte> header = stackalloc byte[8];
        int offset = 0;
        for (int i = 0; i < sizes.Count; i++)
        {
            var src = new ReadOnlySpan<byte>(raw, offset, sizes[i]);
            byte[] packed = compressed[i];
            bool stored = packed == null || packed.Length >= src.Length;
            byte type = stored ? TypeUncompressed : TypeOodleKraken;
            ReadOnlySpan<byte> payload = stored ? src : packed;
            int slot = payload.Length;

            if (layout != null && pad)
            {
                Block orig = layout[i];
                if (payload.Length > orig.CompSize)
                {
                    failure = $"block {i} compresses to {payload.Length} bytes, more than the original {orig.CompSize}";
                    return null;
                }
                // An uncompressed block must be exactly its raw size; never pad those.
                if (stored && orig.CompSize != src.Length)
                {
                    failure = $"block {i} does not compress, but the original block was compressed";
                    return null;
                }
                slot = orig.CompSize;
            }

            BinaryPrimitives.WriteUInt32BigEndian(header, (uint)src.Length);
            BinaryPrimitives.WriteUInt16BigEndian(header.Slice(4), (ushort)((type << 8) | CodeFlags | ((slot >> 16) & 0xF)));
            BinaryPrimitives.WriteUInt16BigEndian(header.Slice(6), (ushort)(slot & 0xFFFF));
            output.Write(header);
            output.Write(payload);
            for (int fill = payload.Length; fill < slot; fill++)
                output.WriteByte(0);
            offset += sizes[i];
        }
        return output.ToArray();
    }

    /// <summary>
    /// Stores <paramref name="raw"/> as uncompressed blocks of <paramref name="blockSize"/>. Audio chunks are
    /// kept like this: the game streams them by reading byte ranges straight from the archive.
    /// </summary>
    public static byte[] Store(byte[] raw, int blockSize = BlockSize)
    {
        using var output = new MemoryStream();
        Span<byte> header = stackalloc byte[8];
        for (int o = 0; o < raw.Length; o += blockSize)
        {
            int n = Math.Min(blockSize, raw.Length - o);
            BinaryPrimitives.WriteUInt32BigEndian(header, (uint)n);
            BinaryPrimitives.WriteUInt16BigEndian(header.Slice(4), (ushort)((TypeUncompressed << 8) | CodeFlags | ((n >> 16) & 0xF)));
            BinaryPrimitives.WriteUInt16BigEndian(header.Slice(6), (ushort)(n & 0xFFFF));
            output.Write(header);
            output.Write(raw, o, n);
        }
        return output.ToArray();
    }

    /// <summary>True when every block of a stored chunk is uncompressed.</summary>
    public static bool IsStoredOnly(ReadOnlySpan<byte> stored) => Parse(stored).All(b => b.Type == TypeUncompressed);

    /// <summary>(compressed offset, uncompressed offset) of the start of every block.</summary>
    public static List<(long Compressed, long Raw)> BlockStarts(ReadOnlySpan<byte> data)
    {
        var starts = new List<(long, long)>();
        long comp = 0, raw = 0;
        foreach (var b in Parse(data))
        {
            starts.Add((comp, raw));
            comp += 8 + b.CompSize;
            raw += b.RawSize;
        }
        return starts;
    }

    /// <summary>
    /// Translates a compressed offset into <paramref name="oldData"/> that marks a block start (such as
    /// a texture's mip offset) into the matching offset in <paramref name="newData"/>, via the block's
    /// uncompressed position. Returns null if either side has no block starting there.
    /// </summary>
    public static long? TranslateOffset(ReadOnlySpan<byte> oldData, ReadOnlySpan<byte> newData, long oldOffset)
    {
        var oldStarts = BlockStarts(oldData);
        int i = oldStarts.FindIndex(s => s.Compressed == oldOffset);
        if (i < 0)
            return null;
        long raw = oldStarts[i].Raw;
        var match = BlockStarts(newData).Where(s => s.Raw == raw).ToList();
        return match.Count == 1 ? match[0].Compressed : null;
    }

    /// <summary>Decodes a chunk the way the game does, block by block (used for verification).</summary>
    public static byte[] Decode(byte[] data)
    {
        using var output = new MemoryStream();
        int p = 0;
        foreach (var b in Parse(data))
        {
            var payload = new ReadOnlySpan<byte>(data, p + 8, b.CompSize);
            if (b.Type == TypeUncompressed)
            {
                output.Write(payload.Slice(0, b.RawSize));
            }
            else
            {
                byte[] dst = Oodle.Decompress(payload, b.RawSize);
                output.Write(dst);
            }
            p += 8 + b.CompSize;
        }
        return output.ToArray();
    }

    private static byte[] Compress(ReadOnlySpan<byte> src, int level) => Oodle.Compress(src, OodleKraken, level);
}
