using System.Buffers.Binary;
using System.Text;

namespace NHL27Kit.Core;

/// <summary>What a WebM file holds: its video track (and audio, when it has one), read from the EBML header.</summary>
public sealed record WebmInfo(string DocType, string VideoCodec, int Width, int Height, bool Alpha, double Seconds,
                              string AudioCodec, int AudioChannels, double AudioRate)
{
    public string Codec => VideoCodec switch { "V_VP8" => "VP8", "V_VP9" => "VP9", "V_AV1" => "AV1", null => "?", _ => VideoCodec };
    public string Audio => AudioCodec switch { null => null, "A_VORBIS" => "Vorbis", "A_OPUS" => "Opus", _ => AudioCodec };
}

/// <summary>A video frame in a WebM file: its time, and where its data is.</summary>
public readonly record struct MovieFrame(double Seconds, int Offset, int Size, bool Key);

/// <summary>
/// A movie in the game: a MovieTexture2Asset and its chunks. The biggest is the video (a WebM file, or EA's VP6); any
/// others are small "ST00" subtitle tracks.
/// </summary>
public sealed record Movie(EbxEntry Entry, IReadOnlyList<ChunkEntry> Chunks)
{
    public ChunkEntry Chunk => Chunks.Count == 0 ? null : Chunks.MaxBy(c => c.Ref.Size);
    public Guid ChunkId => Chunk?.Id ?? Guid.Empty;
    public long Size => Chunk?.Ref.Size ?? 0;
    public int Subtitles => Chunks.Count - (Chunk == null ? 0 : 1);
    public string Name => Entry.Name;
    public string Short => Entry.Name[(Entry.Name.LastIndexOf('/') + 1)..];
    public string Folder => Entry.Name.Contains('/') ? Entry.Name[..Entry.Name.LastIndexOf('/')] : "";
}

/// <summary>
/// NHL 27's movies (menus, hype stages, arena scoreboards, ice projections, Training Camp clips...) are
/// MovieTexture2Assets. Each names one chunk, which is a plain WebM file (VP8 so far), followed by the chunk's size.
/// </summary>
public static class Movies
{
    public const string Type = "MovieTexture2Asset";

    public static List<Movie> List(AssetIndex index)
    {
        var list = new List<Movie>();
        foreach (var e in index.Ebx.Values.Where(e => e.Type == Type).OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
        {
            var chunks = new List<ChunkEntry>();
            try { chunks.AddRange(Audio.ChunkIds(index, index.Read(e)).Select(index.GetChunk)); } catch { /* unreadable: listed without a chunk */ }
            list.Add(new Movie(e, chunks));
        }
        return list;
    }

    /// <summary>One movie by its asset name, or null.</summary>
    public static Movie Get(AssetIndex index, string name)
    {
        var e = index.GetEbx(name);
        if (e == null || e.Type != Type) return null;
        return new Movie(e, Audio.ChunkIds(index, index.Read(e)).Select(index.GetChunk).ToList());
    }

    /// <summary>What kind of file a movie's video is, from its first bytes: "webm", "vp6" (EA's MVhd) or null.</summary>
    public static string Kind(ReadOnlySpan<byte> d) =>
        d.Length < 4 ? null : BinaryPrimitives.ReadUInt32BigEndian(d) == EbmlId ? "webm" : d[..4].SequenceEqual("MVhd"u8) ? "vp6" : null;

    /// <summary>The movie's WebM file.</summary>
    public static byte[] Read(AssetIndex index, Movie m) =>
        m.Chunk == null ? throw new InvalidDataException($"{m.Name} names no chunk.") : index.Read(m.Chunk);

    // ---------------------------------------------------------------- WebM (EBML) header

    private const uint EbmlId = 0x1A45DFA3, DocTypeId = 0x4282, SegmentId = 0x18538067, InfoId = 0x1549A966,
        TimecodeScaleId = 0x2AD7B1, DurationId = 0x4489, TracksId = 0x1654AE6B, TrackEntryId = 0xAE, TrackTypeId = 0x83,
        CodecIdId = 0x86, VideoId = 0xE0, AudioId = 0xE1, PixelWidthId = 0xB0, PixelHeightId = 0xBA, AlphaModeId = 0x53C0,
        RateId = 0xB5, ChannelsId = 0x9F, ClusterId = 0x1F43B675;

    /// <summary>Reads a WebM file's header (null when it isn't one).</summary>
    public static WebmInfo Probe(ReadOnlySpan<byte> d)
    {
        if (d.Length < 4 || BinaryPrimitives.ReadUInt32BigEndian(d) != EbmlId) return null;
        string docType = null, vcodec = null, acodec = null;
        int w = 0, h = 0, channels = 0;
        bool alpha = false;
        double duration = 0, scale = 1_000_000, rate = 0;
        int p = 0;
        while (p < d.Length)
        {
            if (!Element(d, ref p, out uint id, out long size, out int start)) break;
            long end = size < 0 ? d.Length : Math.Min(d.Length, start + size);
            switch (id)
            {
                case EbmlId:
                    for (int q = start; q < end;)
                    {
                        if (!Element(d, ref q, out uint cid, out long cs, out int cstart)) break;
                        if (cid == DocTypeId) docType = Text(d, cstart, cs);
                        q = (int)(cstart + cs);
                    }
                    p = (int)end;
                    break;
                case SegmentId:
                    p = start;   // walk its children
                    break;
                case InfoId:
                    for (int q = start; q < end;)
                    {
                        if (!Element(d, ref q, out uint cid, out long cs, out int cstart)) break;
                        if (cid == TimecodeScaleId) scale = Uint(d, cstart, cs);
                        else if (cid == DurationId) duration = Float(d, cstart, cs);
                        q = (int)(cstart + cs);
                    }
                    p = (int)end;
                    break;
                case TracksId:
                    for (int q = start; q < end;)
                    {
                        if (!Element(d, ref q, out uint tid, out long ts, out int tstart)) break;
                        if (tid == TrackEntryId)
                        {
                            long type = 0;
                            string codec = null;
                            for (int r = tstart; r < tstart + ts;)
                            {
                                if (!Element(d, ref r, out uint cid, out long cs, out int cstart)) break;
                                if (cid == TrackTypeId) type = Uint(d, cstart, cs);
                                else if (cid == CodecIdId) codec = Text(d, cstart, cs);
                                else if (cid is VideoId or AudioId)
                                    for (int s = cstart; s < cstart + cs;)
                                    {
                                        if (!Element(d, ref s, out uint vid, out long vs, out int vstart)) break;
                                        if (vid == PixelWidthId) w = (int)Uint(d, vstart, vs);
                                        else if (vid == PixelHeightId) h = (int)Uint(d, vstart, vs);
                                        else if (vid == AlphaModeId) alpha = Uint(d, vstart, vs) != 0;
                                        else if (vid == RateId) rate = Float(d, vstart, vs);
                                        else if (vid == ChannelsId) channels = (int)Uint(d, vstart, vs);
                                        s = (int)(vstart + vs);
                                    }
                                r = (int)(cstart + cs);
                            }
                            if (type == 1) vcodec ??= codec;
                            else if (type == 2) acodec ??= codec;
                        }
                        q = (int)(tstart + ts);
                    }
                    p = (int)end;
                    break;
                case ClusterId:
                    p = d.Length;   // the header is done
                    break;
                default:
                    p = (int)end;
                    break;
            }
        }
        return new WebmInfo(docType, vcodec, w, h, alpha, duration * scale / 1e9, acodec, channels, rate);
    }

    private const uint TrackNumberId = 0xD7, ClusterTimecodeId = 0xE7, SimpleBlockId = 0xA3, BlockGroupId = 0xA0, BlockId = 0xA1;

    /// <summary>
    /// Every video frame of a WebM file, in order: its time, where its data starts (after the block's header) and its size.
    /// This is what a MovieTexture2Asset's frame table holds.
    /// </summary>
    public static List<MovieFrame> Frames(ReadOnlySpan<byte> d)
    {
        var frames = new List<MovieFrame>();
        double scale = 1_000_000;
        long videoTrack = -1, cluster = 0;
        int p = 0;
        while (p < d.Length)
        {
            if (!Element(d, ref p, out uint id, out long size, out int start)) break;
            long end = size < 0 ? d.Length : Math.Min(d.Length, start + size);
            switch (id)
            {
                case SegmentId:
                case ClusterId:
                case BlockGroupId:
                    p = start;   // walk its children
                    continue;
                case InfoId:
                    for (int q = start; q < end;)
                    {
                        if (!Element(d, ref q, out uint cid, out long cs, out int cstart)) break;
                        if (cid == TimecodeScaleId) scale = Uint(d, cstart, cs);
                        q = (int)(cstart + cs);
                    }
                    break;
                case TracksId:
                    for (int q = start; q < end;)
                    {
                        if (!Element(d, ref q, out uint tid, out long ts, out int tstart)) break;
                        if (tid == TrackEntryId)
                        {
                            long number = 0, type = 0;
                            for (int r = tstart; r < tstart + ts;)
                            {
                                if (!Element(d, ref r, out uint cid, out long cs, out int cstart)) break;
                                if (cid == TrackNumberId) number = Uint(d, cstart, cs);
                                else if (cid == TrackTypeId) type = Uint(d, cstart, cs);
                                r = (int)(cstart + cs);
                            }
                            if (type == 1 && videoTrack < 0) videoTrack = number;
                        }
                        q = (int)(tstart + ts);
                    }
                    break;
                case ClusterTimecodeId:
                    cluster = Uint(d, start, size);
                    break;
                case SimpleBlockId:
                case BlockId:
                {
                    // track number (a vint), a signed 16-bit timecode relative to the cluster, flags
                    int q = start, n = 1 + System.Numerics.BitOperations.LeadingZeroCount((uint)d[q]) - 24;
                    long track = d[q] & (0xFF >> n);
                    for (int i = 1; i < n; i++) track = track << 8 | d[q + i];
                    q += n;
                    short rel = BinaryPrimitives.ReadInt16BigEndian(d[q..]);
                    byte flags = d[q + 2];
                    q += 3;
                    if (track == videoTrack)
                        frames.Add(new MovieFrame((cluster + rel) * scale / 1e9, q, (int)(end - q), id == SimpleBlockId && (flags & 0x80) != 0));
                    break;
                }
            }
            p = (int)end;
        }
        return frames;
    }

    /// <summary>
    /// Where a MovieTexture2Asset's frame table is: a u32 count, then per frame a float time, a u32 offset and a u32 size
    /// into the WebM file. Found by looking for the movie's first frames; -1 when it isn't there.
    /// </summary>
    public static int FrameTable(ReadOnlySpan<byte> ebx, IReadOnlyList<MovieFrame> frames)
    {
        if (frames.Count == 0) return -1;
        Span<byte> key = stackalloc byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(key[4..], (uint)frames[0].Offset);
        BinaryPrimitives.WriteUInt32LittleEndian(key[8..], (uint)frames[0].Size);
        for (int at = 4; at + 12 * frames.Count <= ebx.Length; at += 4)
        {
            at = ebx[at..].IndexOf(key[4..]) is int k and >= 0 ? at + k - 4 : -1;
            if (at < 4) return -1;
            if (BinaryPrimitives.ReadUInt32LittleEndian(ebx[(at - 4)..]) == frames.Count) return at - 4;
        }
        return -1;
    }

    /// <summary>The frame table as the EBX has it.</summary>
    public static List<MovieFrame> ReadTable(ReadOnlySpan<byte> ebx, int at)
    {
        int n = (int)BinaryPrimitives.ReadUInt32LittleEndian(ebx[at..]);
        var list = new List<MovieFrame>(n);
        for (int i = 0; i < n; i++)
        {
            int o = at + 4 + 12 * i;
            list.Add(new MovieFrame(BinaryPrimitives.ReadSingleLittleEndian(ebx[o..]), (int)BinaryPrimitives.ReadUInt32LittleEndian(ebx[(o + 4)..]),
                                    (int)BinaryPrimitives.ReadUInt32LittleEndian(ebx[(o + 8)..]), false));
        }
        return list;
    }

    /// <summary>Reads an element's id and size at <paramref name="p"/>; size -1 is "unknown" (runs to the end).</summary>
    private static bool Element(ReadOnlySpan<byte> d, ref int p, out uint id, out long size, out int start)
    {
        id = 0; size = 0; start = p;
        if (p >= d.Length || d[p] == 0) return false;
        int n = 1 + System.Numerics.BitOperations.LeadingZeroCount((uint)d[p]) - 24;
        if (n > 4 || p + n > d.Length) return false;
        for (int i = 0; i < n; i++) id = id << 8 | d[p + i];
        p += n;
        if (p >= d.Length || d[p] == 0) return false;
        int m = 1 + System.Numerics.BitOperations.LeadingZeroCount((uint)d[p]) - 24;
        if (p + m > d.Length) return false;
        long v = d[p] & (0xFF >> m);
        bool unknown = v == (0xFF >> m);
        for (int i = 1; i < m; i++) { v = v << 8 | d[p + i]; unknown &= d[p + i] == 0xFF; }
        p += m;
        size = unknown ? -1 : v;
        start = p;
        return true;
    }

    private static long Uint(ReadOnlySpan<byte> d, int p, long n)
    {
        long v = 0;
        for (int i = 0; i < n && p + i < d.Length; i++) v = v << 8 | d[p + i];
        return v;
    }

    private static double Float(ReadOnlySpan<byte> d, int p, long n) => n switch
    {
        4 when p + 4 <= d.Length => BinaryPrimitives.ReadSingleBigEndian(d[p..]),
        8 when p + 8 <= d.Length => BinaryPrimitives.ReadDoubleBigEndian(d[p..]),
        _ => 0,
    };

    private static string Text(ReadOnlySpan<byte> d, int p, long n) =>
        Encoding.ASCII.GetString(d.Slice(p, (int)Math.Min(n, d.Length - p))).TrimEnd('\0');
}
