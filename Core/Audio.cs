using System.Buffers.Binary;
using System.Runtime.InteropServices;
using VGAudio.Codecs.Atrac9;

namespace NHL27Kit.Core;

/// <summary>
/// EA SPS sound streams (NewWaveAsset audio), as NHL24Kit worked them out for NHL 24:
///   'H' [u24 BE header size][u8 codec (low nibble) | flags][u8 (channels - 1) &lt;&lt; 2][u16 BE rate][u32 BE flags:4 | samples:28]
///   'D' [u24 BE block size][u32 BE samples in block][codec payload]   ... repeated
///   'E' [u24 BE size]
/// NHL 27 uses codec 13 (EA ATRAC9) for music, crowds and effects and codec 15 (uncoupled multistream Opus) for speech.
/// </summary>
public sealed record SpsHeader(int Codec, int Channels, int SampleRate, int Samples, int HeaderSize, byte CodecByte)
{
    public static readonly IReadOnlyDictionary<int, string> CodecNames = new Dictionary<int, string>
    {
        [2] = "PCM16 BE", [3] = "EA-XMA", [4] = "EA-XAS", [5] = "EALayer3 v1", [6] = "EALayer3 v2 PCM", [7] = "EALayer3 v2 Spike",
        [9] = "EASpeex", [11] = "EA-MP3", [12] = "Opus", [13] = "ATRAC9", [14] = "Opus multistream", [15] = "Opus multistream (uncoupled)",
    };

    public string CodecName => CodecNames.GetValueOrDefault(Codec, $"codec {Codec}");
    public double Seconds => SampleRate > 0 ? Samples / (double)SampleRate : 0;

    public static SpsHeader Read(ReadOnlySpan<byte> d)
    {
        if (d.Length < 12 || d[0] != 0x48)
            return null;
        int size = (d[1] << 16) | (d[2] << 8) | d[3];
        uint word = BinaryPrimitives.ReadUInt32BigEndian(d[8..]);
        return new SpsHeader(d[4] & 0xF, (d[5] >> 2) + 1, BinaryPrimitives.ReadUInt16BigEndian(d[6..]), (int)(word & 0x0FFFFFFF), size, d[4]);
    }
}

/// <summary>A decoded sound: interleaved 16-bit PCM.</summary>
public sealed class Sound
{
    public SpsHeader Header { get; init; }
    public short[] Pcm { get; init; }
    public int Channels { get; init; }
    public int SampleRate { get; init; }
    public int Frames => Channels == 0 ? 0 : Pcm.Length / Channels;
    public double Seconds => SampleRate == 0 ? 0 : Frames / (double)SampleRate;

    /// <summary>Decodes an SPS stream (ATRAC9 or Opus).</summary>
    public static Sound Decode(byte[] sps)
    {
        var h = SpsHeader.Read(sps) ?? throw new InvalidDataException("Not an EA SPS stream (no 'H' header).");
        var blocks = Blocks(sps, h);
        short[] pcm = h.Codec switch
        {
            13 => SpsAtrac9.Decode(blocks, h),
            12 or 14 or 15 => SpsOpus.Decode(blocks, h),
            _ => throw new NotSupportedException($"{h.CodecName} sounds can't be decoded yet."),
        };
        // the blocks' sample counts add up to the header's; keep exactly that many
        int want = h.Samples * h.Channels;
        if (pcm.Length > want) Array.Resize(ref pcm, want);
        return new Sound { Header = h, Pcm = pcm, Channels = h.Channels, SampleRate = h.SampleRate };
    }

    /// <summary>The 'D' blocks: (samples, payload).</summary>
    private static List<(int Samples, byte[] Data)> Blocks(byte[] sps, SpsHeader h)
    {
        var blocks = new List<(int, byte[])>();
        int p = h.HeaderSize;
        while (p + 4 <= sps.Length)
        {
            byte type = sps[p];
            int size = (sps[p + 1] << 16) | (sps[p + 2] << 8) | sps[p + 3];
            if (type == 0x45 || size < 4 || p + size > sps.Length)
                break;
            if (type == 0x44 && size >= 8)
                blocks.Add((BinaryPrimitives.ReadInt32BigEndian(sps.AsSpan(p + 4)), sps.AsSpan(p + 8, size - 8).ToArray()));
            p += size;
        }
        return blocks;
    }

    /// <summary>
    /// Stereo (or mono) mix of every channel: the channels come in left/right pairs (one pair per stream), and a pair that
    /// is silent through the whole sound is left out. Streams are averaged so a layered mix does not clip.
    /// </summary>
    public short[] Mixdown()
    {
        if (Channels <= 2) return Pcm;
        int pairs = Channels / 2;
        var active = new List<int>();
        for (int p = 0; p < pairs; p++)
        {
            bool sound = false;
            for (int f = 0; f < Frames && !sound; f++)
                sound = Pcm[f * Channels + p * 2] != 0 || Pcm[f * Channels + p * 2 + 1] != 0;
            if (sound) active.Add(p);
        }
        if (active.Count == 0) active.Add(0);
        var mix = new short[Frames * 2];
        for (int f = 0; f < Frames; f++)
        {
            int l = 0, r = 0;
            foreach (int p in active)
            {
                l += Pcm[f * Channels + p * 2];
                r += Pcm[f * Channels + p * 2 + 1];
            }
            mix[f * 2] = (short)(l / active.Count);
            mix[f * 2 + 1] = (short)(r / active.Count);
        }
        return mix;
    }

    /// <summary>Loudest sample per bucket of the stereo mix, 0..1, for drawing a waveform.</summary>
    public float[] Peaks(int buckets)
    {
        var mix = Mixdown();
        int ch = Channels <= 2 ? Channels : 2;
        var peaks = new float[buckets];
        int frames = mix.Length / ch;
        for (int b = 0; b < buckets; b++)
        {
            int from = (int)((long)b * frames / buckets), to = Math.Max(from + 1, (int)((long)(b + 1) * frames / buckets));
            int max = 0;
            for (int f = from; f < to && f < frames; f++)
                for (int c = 0; c < ch; c++)
                    max = Math.Max(max, Math.Abs((int)mix[f * ch + c]));
            peaks[b] = max / 32768f;
        }
        return peaks;
    }

    /// <summary>A 16-bit PCM WAV: every channel, or (<paramref name="stereo"/>) the stereo mix.</summary>
    public byte[] ToWav(bool stereo = false)
    {
        short[] pcm = stereo ? Mixdown() : Pcm;
        int ch = stereo && Channels > 2 ? 2 : Channels;
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        int bytes = pcm.Length * 2;
        w.Write("RIFF"u8); w.Write(36 + bytes); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)ch); w.Write(SampleRate);
        w.Write(SampleRate * ch * 2); w.Write((short)(ch * 2)); w.Write((short)16);
        w.Write("data"u8); w.Write(bytes);
        w.Write(MemoryMarshal.AsBytes(pcm.AsSpan()));
        w.Flush();
        return ms.ToArray();
    }
}

/// <summary>
/// EA's ATRAC9 in SPS. The first block's payload starts [0x00] then one 4-byte ATRAC9 config (0xFE ...) per stream;
/// then, in every block, groups of [0x01] followed by one superframe per stream. Streams are stereo (or mono), so a
/// 4-channel sound is two stereo streams whose channels are interleaved in order.
/// </summary>
internal static class SpsAtrac9
{
    public static short[] Decode(List<(int Samples, byte[] Data)> blocks, SpsHeader h)
    {
        if (blocks.Count == 0) return Array.Empty<short>();
        var first = blocks[0].Data;
        if (first.Length < 5 || first[0] != 0x00 || first[1] != 0xFE)
            throw new InvalidDataException("No ATRAC9 config at the start of the first block.");
        var configs = new List<Atrac9Config>();
        int p = 1;
        while (p + 4 <= first.Length && first[p] == 0xFE && configs.Sum(c => c.ChannelCount) < h.Channels)
        {
            configs.Add(new Atrac9Config(first.AsSpan(p, 4).ToArray()));
            p += 4;
        }
        if (configs.Count == 0 || configs.Sum(c => c.ChannelCount) != h.Channels)
            throw new InvalidDataException($"The ATRAC9 configs give {configs.Sum(c => c.ChannelCount)} channels, the header {h.Channels}.");

        var decoders = configs.Select(c => { var d = new Atrac9Decoder(); d.Initialize(c.ConfigData); return d; }).ToList();
        var buffers = configs.Select(c => Enumerable.Range(0, c.ChannelCount).Select(_ => new short[c.SuperframeSamples]).ToArray()).ToList();
        int groupBytes = 1 + configs.Sum(c => c.SuperframeBytes);
        int samplesPerGroup = configs[0].SuperframeSamples;
        var output = new List<short>(h.Samples * h.Channels + samplesPerGroup * h.Channels);

        for (int b = 0; b < blocks.Count; b++)
        {
            var data = blocks[b].Data;
            int q = b == 0 ? p : 0;
            while (q + groupBytes <= data.Length)
            {
                q++; // the group's 0x01
                for (int s = 0; s < configs.Count; s++)
                {
                    var frame = data.AsSpan(q, configs[s].SuperframeBytes).ToArray();
                    decoders[s].Decode(frame, buffers[s]);
                    q += configs[s].SuperframeBytes;
                }
                for (int i = 0; i < samplesPerGroup; i++)
                    for (int s = 0; s < configs.Count; s++)
                        for (int c = 0; c < configs[s].ChannelCount; c++)
                            output.Add(buffers[s][c][i]);
            }
        }
        return output.ToArray();
    }
}

/// <summary>
/// EA's Opus in SPS: each 'D' block is one multistream Opus packet (20 ms) and the samples it really carries; the
/// first block skips the decoder's pre-skip. Codec 15 has one stream per channel (uncoupled); 14 couples them in pairs.
/// Uses the native opus.dll (from FMT) next to the kit.
/// </summary>
internal static class SpsOpus
{
    [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr opus_multistream_decoder_create(int fs, int channels, int streams, int coupled, byte[] mapping, out int error);
    [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int opus_multistream_decode(IntPtr st, byte[] data, int len, short[] pcm, int frameSize, int decodeFec);
    [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void opus_multistream_decoder_destroy(IntPtr st);

    public static short[] Decode(List<(int Samples, byte[] Data)> blocks, SpsHeader h)
    {
        int ch = h.Channels;
        int coupled = h.Codec == 15 ? 0 : ch / 2;
        int streams = ch - coupled;
        var mapping = Enumerable.Range(0, ch).Select(i => (byte)i).ToArray();
        IntPtr dec = opus_multistream_decoder_create(48000, ch, streams, coupled, mapping, out int err);
        if (err != 0)
            throw new InvalidOperationException($"opus_multistream_decoder_create failed ({err}).");
        try
        {
            var output = new List<short>(h.Samples * ch);
            var buf = new short[5760 * ch];
            bool first = true;
            foreach (var (samples, packet) in blocks)
            {
                int n = opus_multistream_decode(dec, packet, packet.Length, buf, 5760, 0);
                if (n < 0)
                    throw new InvalidDataException($"opus_multistream_decode failed ({n}).");
                int skip = first ? Math.Max(0, n - samples) : 0;
                int keep = Math.Min(samples, n - skip);
                for (int i = skip * ch; i < (skip + keep) * ch; i++)
                    output.Add(buf[i]);
                first = false;
            }
            return output.ToArray();
        }
        finally
        {
            opus_multistream_decoder_destroy(dec);
        }
    }
}

/// <summary>One sound stream: the chunk that holds it and where in that chunk's data it is.</summary>
public sealed record SoundStream(int Number, Guid ChunkId, ChunkEntry Chunk, int Offset, int Length, SpsHeader Header);

public static class Audio
{
    /// <summary>
    /// The chunks a sound asset uses. Its field layout isn't known, so the raw EBX is scanned for GUIDs the index knows as
    /// chunks (as NHL24Kit does); each GUID is followed by the chunk's size as a u32.
    /// </summary>
    public static List<Guid> ChunkIds(AssetIndex index, byte[] ebx)
    {
        var ids = new List<Guid>();
        for (int i = 0; i + 16 <= ebx.Length; i++)
        {
            var g = new Guid(ebx.AsSpan(i, 16));
            if (g == Guid.Empty || ids.Contains(g)) continue;
            if (index.GetChunk(g) != null)
            {
                ids.Add(g);
                i += 15;
            }
        }
        return ids;
    }

    /// <summary>
    /// The SPS streams stored back to back in a chunk (a speech chunk can hold dozens of takes of one line). Each stream
    /// runs from its 'H' header through its 'E' block, whose size includes the padding up to the next stream.
    /// </summary>
    public static List<(int Offset, int Length, SpsHeader Header)> Streams(byte[] raw)
    {
        var list = new List<(int, int, SpsHeader)>();
        int p = 0;
        while (p + 12 <= raw.Length)
        {
            var h = SpsHeader.Read(raw.AsSpan(p));
            if (h == null) break;
            int q = p + h.HeaderSize;
            bool ended = false;
            while (q + 4 <= raw.Length)
            {
                byte t = raw[q];
                int size = (raw[q + 1] << 16) | (raw[q + 2] << 8) | raw[q + 3];
                if (size < 4 || q + size > raw.Length) break;
                q += size;
                if (t == 0x45) { ended = true; break; }
                if (t != 0x44) break;
            }
            if (!ended) { if (list.Count == 0) list.Add((p, raw.Length - p, h)); break; }
            list.Add((p, q - p, h));
            p = q;
        }
        return list;
    }

    /// <summary>Every stream of every chunk a NewWaveAsset uses, numbered from 1 (a chunk that isn't audio is listed with no header).</summary>
    public static List<SoundStream> Variations(AssetIndex index, EbxEntry wave)
    {
        var list = new List<SoundStream>();
        foreach (var id in ChunkIds(index, index.Read(wave)))
        {
            var c = index.GetChunk(id);
            byte[] raw;
            try { raw = index.Read(c); }
            catch { list.Add(new SoundStream(list.Count + 1, id, c, 0, 0, null)); continue; }
            var streams = Streams(raw);
            if (streams.Count == 0) list.Add(new SoundStream(list.Count + 1, id, c, 0, raw.Length, null));
            foreach (var (off, len, h) in streams)
                list.Add(new SoundStream(list.Count + 1, id, c, off, len, h));
        }
        return list;
    }

    /// <summary>Decodes one stream of a sound.</summary>
    public static Sound Decode(AssetIndex index, SoundStream s)
    {
        var raw = index.Read(s.Chunk);
        return Sound.Decode(raw.AsSpan(s.Offset, s.Length).ToArray());
    }
}
