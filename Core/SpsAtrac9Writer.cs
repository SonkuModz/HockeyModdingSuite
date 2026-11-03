using System.Buffers.Binary;
using VGAudio.Codecs.Atrac9;

namespace NHL27Kit.Core;

/// <summary>
/// Writes an EA SPS stream of ATRAC9 (codec 13), the layout <see cref="SpsAtrac9"/> reads: an 'H' header, 'D' blocks
/// whose first payload starts [00] + one 4-byte config per stream, and in every block groups of [01] + one superframe
/// per stream; an 'E' block padded to the requested length.
/// </summary>
public static class SpsAtrac9Writer
{
    /// <summary>The configs at the start of a stream's first block (one per stereo/mono stream).</summary>
    public static List<byte[]> ReadConfigs(byte[] sps, SpsHeader h)
    {
        int p = h.HeaderSize;
        if (sps[p] != 0x44) throw new InvalidDataException("No data block after the header.");
        int q = p + 8;
        if (sps[q] != 0x00) throw new InvalidDataException("The first ATRAC9 block doesn't start with configs.");
        q++;
        var list = new List<byte[]>();
        int channels = 0;
        while (sps[q] == 0xFE && channels < h.Channels)
        {
            var c = sps.AsSpan(q, 4).ToArray();
            channels += new Atrac9Config(c).ChannelCount;
            list.Add(c);
            q += 4;
        }
        return list;
    }

    /// <summary>Groups (superframes) per 'D' block in a stream: from its first block's sample count.</summary>
    public static int GroupsPerBlock(byte[] sps, SpsHeader h, int superframeSamples) =>
        Math.Max(1, BinaryPrimitives.ReadInt32BigEndian(sps.AsSpan(h.HeaderSize + 4)) / superframeSamples);

    /// <summary>
    /// The largest frame size (bytes per frame, the same for every stream) at which <paramref name="frames"/> samples fit in
    /// <paramref name="capacity"/> bytes, counting the header, block headers, configs, group markers and the end block.
    /// </summary>
    public static int FittingFrameBytes(int frames, IReadOnlyList<byte[]> configs, int groupsPerBlock, int capacity)
    {
        var cfgs = configs.Select(c => new Atrac9Config(c)).ToList();
        int sfs = cfgs[0].SuperframeSamples, fps = cfgs[0].FramesPerSuperframe;
        int groups = (frames + sfs + sfs - 1) / sfs;
        int blocks = (groups + groupsPerBlock - 1) / groupsPerBlock;
        int fixedBytes = 12 + blocks * 8 + 1 + 4 * configs.Count + groups + 4;
        int perFrame = (capacity - fixedBytes) / groups / configs.Count / fps;
        return Math.Min(perFrame, 2048);
    }

    /// <summary>A config with another frame size (bytes per frame, 1-2048), everything else kept.</summary>
    public static byte[] WithFrameBytes(byte[] config, int frameBytes)
    {
        var c = (byte[])config.Clone();
        ushort v = BinaryPrimitives.ReadUInt16BigEndian(c.AsSpan(2));
        v = (ushort)((v & 0x1F) | ((frameBytes - 1) << 5));
        BinaryPrimitives.WriteUInt16BigEndian(c.AsSpan(2), v);
        return c;
    }

    /// <summary>
    /// Encodes interleaved PCM (the stream's channel count, its sample rate) as an SPS ATRAC9 stream. Returns null when
    /// the result would be longer than <paramref name="exactLength"/>.
    /// </summary>
    public static byte[] Encode(short[] pcm, int channels, IReadOnlyList<byte[]> configs, byte[] header8, uint flags, int groupsPerBlock, int exactLength)
    {
        var encoders = configs.Select(c => new Atrac9Encoder(c)).ToList();
        int sfs = encoders[0].Config.SuperframeSamples;
        if (encoders.Any(e => e.Config.SuperframeSamples != sfs)) throw new NotSupportedException("The streams' superframes differ in length.");
        if (encoders.Sum(e => e.Config.ChannelCount) != channels) throw new InvalidDataException("The configs don't add up to the stream's channels.");
        int frames = pcm.Length / channels;
        // the MDCT delays the output by one superframe, so one more is encoded and counted
        int total = frames + sfs;
        int groups = (total + sfs - 1) / sfs;

        using var ms = new MemoryStream();
        ms.Write(header8);
        var word = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(word, (flags & 0xF0000000u) | (uint)total);
        ms.Write(word);

        var bufs = encoders.Select(e => Enumerable.Range(0, e.Config.ChannelCount).Select(_ => new short[sfs]).ToArray()).ToList();
        int g = 0, written = 0;
        bool first = true;
        while (g < groups)
        {
            int n = Math.Min(groupsPerBlock, groups - g);
            using var payload = new MemoryStream();
            if (first)
            {
                payload.WriteByte(0);
                foreach (var c in configs) payload.Write(c);
                first = false;
            }
            for (int k = 0; k < n; k++, g++)
            {
                payload.WriteByte(1);
                int ch = 0;
                for (int s = 0; s < encoders.Count; s++)
                {
                    for (int c = 0; c < encoders[s].Config.ChannelCount; c++, ch++)
                        for (int i = 0; i < sfs; i++)
                        {
                            int src = g * sfs + i;
                            bufs[s][c][i] = src < frames ? pcm[src * channels + ch] : (short)0;
                        }
                    payload.Write(encoders[s].EncodeSuperframe(bufs[s]));
                }
            }
            int samples = Math.Min(n * sfs, total - written);
            written += samples;
            int size = 8 + (int)payload.Length;
            ms.Write(new byte[] { 0x44, (byte)(size >> 16), (byte)(size >> 8), (byte)size });
            BinaryPrimitives.WriteInt32BigEndian(word, samples);
            ms.Write(word);
            payload.Position = 0;
            payload.CopyTo(ms);
        }
        int end = exactLength > 0 ? exactLength - (int)ms.Length : 4;
        if (end < 4) return null;
        ms.Write(new byte[] { 0x45, (byte)(end >> 16), (byte)(end >> 8), (byte)end });
        ms.Write(new byte[end - 4]);
        return ms.ToArray();
    }
}
