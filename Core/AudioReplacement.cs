using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace NHL27Kit.Core;

/// <summary>Reads WAV files: PCM 8/16/24/32-bit and 32-bit float, any channel count, into 16-bit interleaved PCM.</summary>
public static class WavReader
{
    public static (short[] Pcm, int Channels, int Rate) Read(byte[] b)
    {
        if (b.Length < 12 || b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F' || b[8] != 'W' || b[9] != 'A' || b[10] != 'V' || b[11] != 'E')
            throw new InvalidDataException("Not a WAV file.");
        int p = 12, channels = 0, rate = 0, bits = 0, format = 0;
        while (p + 8 <= b.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(b, p, 4);
            int size = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(p + 4));
            int body = p + 8;
            if (id == "fmt ")
            {
                format = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(body));
                channels = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(body + 2));
                rate = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(body + 4));
                bits = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(body + 14));
                if (format == 0xFFFE && size >= 26) format = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(body + 24));
            }
            else if (id == "data")
            {
                if (channels == 0) throw new InvalidDataException("The WAV has data before its format.");
                size = Math.Min(size, b.Length - body);
                int bytes = bits / 8, frames = size / (bytes * channels);
                var pcm = new short[frames * channels];
                for (int i = 0; i < pcm.Length; i++)
                {
                    int o = body + i * bytes;
                    pcm[i] = (format, bits) switch
                    {
                        (1, 8) => (short)((b[o] - 128) << 8),
                        (1, 16) => BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(o)),
                        (1, 24) => (short)((b[o + 1] | (b[o + 2] << 8))),
                        (1, 32) => (short)(BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(o)) >> 16),
                        (3, 32) => (short)Math.Clamp(BitConverter.ToSingle(b, o) * 32767f, -32768f, 32767f),
                        _ => throw new NotSupportedException($"WAV format {format} with {bits}-bit samples isn't supported (use 16-bit PCM)."),
                    };
                }
                return (pcm, channels, rate);
            }
            p = body + size + (size & 1);
        }
        throw new InvalidDataException("The WAV has no data.");
    }

    /// <summary>Linear-interpolation resampling of interleaved PCM.</summary>
    public static short[] Resample(short[] pcm, int channels, int from, int to)
    {
        if (from == to) return pcm;
        int frames = pcm.Length / channels;
        int outFrames = (int)((long)frames * to / from);
        var o = new short[outFrames * channels];
        for (int f = 0; f < outFrames; f++)
        {
            double src = (double)f * from / to;
            int i0 = Math.Min(frames - 1, (int)src), i1 = Math.Min(frames - 1, i0 + 1);
            double t = src - i0;
            for (int c = 0; c < channels; c++)
                o[f * channels + c] = (short)Math.Round(pcm[i0 * channels + c] * (1 - t) + pcm[i1 * channels + c] * t);
        }
        return o;
    }
}

/// <summary>Opus multistream encoding into an EA SPS stream (the format NHL 27's speech uses), through opus.dll.</summary>
public static class SpsOpusWriter
{
    [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr opus_multistream_encoder_create(int fs, int channels, int streams, int coupled, byte[] mapping, int application, out int error);
    [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int opus_multistream_encode(IntPtr st, short[] pcm, int frameSize, byte[] data, int maxBytes);
    [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int opus_multistream_encoder_ctl(IntPtr st, int request, int value);
    [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "opus_multistream_encoder_ctl")]
    private static extern int opus_multistream_encoder_ctl_get(IntPtr st, int request, out int value);
    [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void opus_multistream_encoder_destroy(IntPtr st);

    private const int Rate = 48000, Frame = 960, ApplicationAudio = 2049, SetBitrate = 4002, GetLookahead = 4027;

    /// <summary>
    /// Encodes 48 kHz interleaved PCM as an SPS stream: codec 15 (one Opus stream per channel), <paramref name="flags"/> the
    /// top four bits of the header's sample word (copied from the sound it replaces). Each 'D' block is one 20 ms packet
    /// and the samples it really carries; the first skips the encoder's lookahead.
    /// </summary>
    public static byte[] Encode(short[] pcm, int channels, int bitratePerChannel, uint flags, int exactLength = 0)
    {
        var mapping = Enumerable.Range(0, channels).Select(i => (byte)i).ToArray();
        IntPtr enc = opus_multistream_encoder_create(Rate, channels, channels, 0, mapping, ApplicationAudio, out int err);
        if (err != 0) throw new InvalidOperationException($"opus_multistream_encoder_create failed ({err}).");
        try
        {
            opus_multistream_encoder_ctl(enc, SetBitrate, bitratePerChannel * channels);
            opus_multistream_encoder_ctl_get(enc, GetLookahead, out int lookahead);
            int total = pcm.Length / channels;
            int frames = (total + lookahead + Frame - 1) / Frame;
            var padded = new short[frames * Frame * channels];
            Array.Copy(pcm, padded, pcm.Length);

            using var ms = new MemoryStream();
            ms.Write(new byte[] { 0x48, 0, 0, 12, 0x1F, (byte)((channels - 1) << 2), Rate >> 8, Rate & 0xFF });
            var word = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(word, (flags & 0xF0000000u) | (uint)total);
            ms.Write(word);
            var frame = new short[Frame * channels];
            var packet = new byte[1500 * channels];
            int emitted = 0;
            for (int f = 0; f < frames; f++)
            {
                Array.Copy(padded, f * Frame * channels, frame, 0, frame.Length);
                int len = opus_multistream_encode(enc, frame, Frame, packet, packet.Length);
                if (len < 0) throw new InvalidOperationException($"opus_multistream_encode failed ({len}).");
                int start = f == 0 ? lookahead : 0;
                int samples = Math.Max(0, Math.Min(Frame - start, total - emitted));
                emitted += samples;
                int size = 8 + len;
                ms.Write(new byte[] { 0x44, (byte)(size >> 16), (byte)(size >> 8), (byte)size });
                BinaryPrimitives.WriteInt32BigEndian(word, samples);
                ms.Write(word);
                ms.Write(packet, 0, len);
            }
            // the 'E' block's size covers padding: it makes the stream exactly as long as the one it replaces
            int end = exactLength > 0 ? exactLength - (int)ms.Length : 4;
            if (end < 4) return null;
            ms.Write(new byte[] { 0x45, (byte)(end >> 16), (byte)(end >> 8), (byte)end });
            ms.Write(new byte[end - 4]);
            return ms.ToArray();
        }
        finally
        {
            opus_multistream_encoder_destroy(enc);
        }
    }
}

/// <summary>What replacing one variation of a sound would do.</summary>
public sealed class SoundReplacementPlan
{
    public string Name { get; init; }
    public SoundStream Stream { get; init; }
    public SpsHeader Original { get; init; }
    public ChunkPlan Chunk { get; init; }
    /// <summary>The new stream decoded back, to listen to before applying.</summary>
    public Sound Result { get; init; }
    public int BitratePerChannel { get; init; }
    /// <summary>The codec the replacement is written in: "ATRAC9" or "Opus".</summary>
    public string Codec { get; init; } = "Opus";
    public int Bytes { get; init; }
    public int Capacity { get; init; }
    public List<string> Warnings { get; } = new();
}

public static partial class Replacements
{
    private static readonly int[] Bitrates = { 96000, 64000, 48000, 40000, 32000, 24000, 16000, 12000, 8000, 6000 };

    /// <summary>
    /// Plans replacing one chunk (variation) of a sound with <paramref name="pcm"/>. The new stream is Opus (NHL 27's speech
    /// codec), written in place, so it must fit in the original's bytes: the highest bitrate that fits is used. The
    /// replacement goes into the original's first two channels (or its one channel); any others are left silent.
    /// </summary>
    public static SoundReplacementPlan PlanSound(AssetIndex index, string name, SoundStream stream, short[] pcm, int channels, int rate, LocationIndex locations, TocCache tocs)
    {
        byte[] oldRaw = index.Read(stream.Chunk);
        var h = stream.Header ?? throw new InvalidDataException("That variation is not a sound stream.");
        var plan0 = new List<string>();
        if (rate != 48000)
        {
            pcm = WavReader.Resample(pcm, channels, rate, 48000);
            plan0.Add($"resampled from {rate} Hz to 48000 Hz");
        }
        // channels: into the original's layout
        int frames = pcm.Length / channels, target = h.Channels;
        var mapped = new short[frames * target];
        for (int f = 0; f < frames; f++)
        {
            if (target == 1)
            {
                int sum = 0;
                for (int c = 0; c < channels; c++) sum += pcm[f * channels + c];
                mapped[f] = (short)(sum / channels);
            }
            else
            {
                mapped[f * target] = pcm[f * channels];
                mapped[f * target + 1] = pcm[f * channels + Math.Min(1, channels - 1)];
            }
        }
        if (target > 2) plan0.Add($"the original has {target} channels ({target / 2} stereo streams); the replacement is in the first two, the rest are silent");
        if (h.Codec == 13)
            return PlanAtrac9(index, name, stream, oldRaw, h, mapped, target, plan0, locations, tocs);
        if (h.Codec != 15 && h.Codec != 12 && h.Codec != 14)
            throw new NotSupportedException($"{h.CodecName} sounds can't be replaced yet.");

        uint flags = BinaryPrimitives.ReadUInt32BigEndian(oldRaw.AsSpan(stream.Offset + 8)) & 0xF0000000u;
        byte[] sps = null;
        int used = 0;
        foreach (int br in Bitrates)
        {
            var s = SpsOpusWriter.Encode(mapped, target, br, flags, stream.Length);
            if (s != null) { sps = s; used = br; break; }
        }
        if (sps == null)
            throw new InvalidDataException($"Even at 6 kbps the replacement ({mapped.Length / target / 48000.0:0.0} s) does not fit in the original's {stream.Length:N0} bytes ({h.Seconds:0.0} s). Use a shorter sound.");
        if (used < 32000) plan0.Add($"encoded at {used / 1000} kbps per channel to fit the original's space");
        // only this stream's bytes change; the chunk's other streams stay where they are
        var raw = (byte[])oldRaw.Clone();
        sps.CopyTo(raw, stream.Offset);
        var chunkPlan = ChunkPatcher.Plan(index, stream.ChunkId, raw, locations, tocs);
        var result = Sound.Decode(sps);
        var plan = new SoundReplacementPlan { Name = name, Stream = stream, Original = h, Chunk = chunkPlan, Result = result, BitratePerChannel = used, Bytes = sps.Length, Capacity = stream.Length };
        plan.Warnings.AddRange(plan0);
        if (Math.Abs(result.Seconds - h.Seconds) > 0.05)
            plan.Warnings.Add($"the replacement is {result.Seconds:0.0} s, the original {h.Seconds:0.0} s; the sound's asset may still expect the original length");
        return plan;
    }

    /// <summary>
    /// An ATRAC9 sound (music, crowds, most effects) is replaced with ATRAC9, encoded by the kit with the original's own
    /// configs, so it plays where the game expects ATRAC9. ATRAC9 is a constant bit rate: a replacement longer than the
    /// original gets a slightly smaller frame size (down to 75%), and past that it is cut to fit.
    /// </summary>
    private static SoundReplacementPlan PlanAtrac9(AssetIndex index, string name, SoundStream stream, byte[] oldRaw, SpsHeader h, short[] pcm, int channels,
                                                   List<string> notes, LocationIndex locations, TocCache tocs)
    {
        var old = oldRaw.AsSpan(stream.Offset, stream.Length).ToArray();
        var configs = SpsAtrac9Writer.ReadConfigs(old, h);
        var cfg0 = new VGAudio.Codecs.Atrac9.Atrac9Config(configs[0]);
        int gpb = SpsAtrac9Writer.GroupsPerBlock(old, h, cfg0.SuperframeSamples);
        uint flags = BinaryPrimitives.ReadUInt32BigEndian(old.AsSpan(8)) & 0xF0000000u;
        int frames = pcm.Length / channels;
        int fb = SpsAtrac9Writer.FittingFrameBytes(frames, configs, gpb, stream.Length);
        int minFb = cfg0.FrameBytes * 3 / 4;
        if (fb >= cfg0.FrameBytes)
            fb = cfg0.FrameBytes;
        else if (fb >= minFb)
            notes.Add($"the replacement is longer than the original, so it is encoded at {fb} instead of {cfg0.FrameBytes} bytes per frame ({fb * 100 / cfg0.FrameBytes}% of the bit rate) to fit");
        else
        {
            // cut to what fits at 75%
            fb = minFb;
            while (frames > 0 && SpsAtrac9Writer.FittingFrameBytes(frames, configs, gpb, stream.Length) < fb) frames -= cfg0.SuperframeSamples;
            notes.Add($"the replacement is much longer than the original, so it is cut to {frames / (double)h.SampleRate:0.0} s (encoded at {fb} bytes per frame)");
            Array.Resize(ref pcm, frames * channels);
        }
        var useConfigs = fb == cfg0.FrameBytes ? configs : configs.Select(c => SpsAtrac9Writer.WithFrameBytes(c, fb)).ToList();
        var sps = SpsAtrac9Writer.Encode(pcm, channels, useConfigs, old.AsSpan(0, 8).ToArray(), flags, gpb, stream.Length)
                  ?? throw new InvalidDataException("The encoded replacement does not fit the original's space.");
        var raw = (byte[])oldRaw.Clone();
        sps.CopyTo(raw, stream.Offset);
        var chunkPlan = ChunkPatcher.Plan(index, stream.ChunkId, raw, locations, tocs);
        var result = Sound.Decode(sps);
        var plan = new SoundReplacementPlan { Name = name, Stream = stream, Original = h, Chunk = chunkPlan, Result = result, BitratePerChannel = fb * 8 * h.SampleRate / cfg0.SuperframeSamples / Math.Max(1, cfg0.ChannelCount) * cfg0.FramesPerSuperframe, Bytes = sps.Length, Capacity = stream.Length, Codec = "ATRAC9" };
        plan.Warnings.AddRange(notes);
        if (Math.Abs(result.Seconds - h.Seconds) > 0.05)
            plan.Warnings.Add($"the replacement is {result.Seconds:0.0} s, the original {h.Seconds:0.0} s; the sound's asset may still expect the original length");
        return plan;
    }
}
