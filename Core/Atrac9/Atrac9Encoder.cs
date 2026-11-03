using System;
using System.Collections.Generic;
using System.Linq;
using VGAudio.Utilities;

namespace VGAudio.Codecs.Atrac9;

/// <summary>
/// A plain ATRAC9 encoder (NHL27Kit), the mirror of the decoder in this folder: every field it writes is read back by
/// <see cref="Unpack"/>, and the bit allocation is computed by the decoder's own <see cref="BitAllocation"/>, so the
/// precisions always agree. Per frame and block it uses all bands of the sample rate, no band extension, no intensity
/// stereo, and a flat bit-allocation gradient chosen as the lowest that fits the frame's byte budget. Scale factors are
/// raw (channel 0) and Huffman-coded against channel 0 (channel 1); spectra are Huffman-coded where the decoder does.
/// Low sample rate configs (up to 48 kHz) only.
/// </summary>
public sealed class Atrac9Encoder
{
    private readonly Frame _frame;
    private readonly Mdct[][] _mdct;
    private readonly double[][][] _spectra;

    public Atrac9Config Config { get; }

    public Atrac9Encoder(byte[] configData)
    {
        Config = new Atrac9Config(configData);
        if (Config.HighSampleRate) throw new NotSupportedException("High sample rate ATRAC9 isn't supported by the encoder.");
        _frame = new Frame(Config);
        _mdct = new Mdct[_frame.Blocks.Length][];
        _spectra = new double[_frame.Blocks.Length][][];
        for (int b = 0; b < _frame.Blocks.Length; b++)
        {
            var block = _frame.Blocks[b];
            if (block.BlockType == BlockType.LFE) throw new NotSupportedException("ATRAC9 LFE blocks aren't supported by the encoder.");
            _mdct[b] = new Mdct[block.ChannelCount];
            _spectra[b] = new double[block.ChannelCount][];
            for (int c = 0; c < block.ChannelCount; c++)
            {
                _mdct[b][c] = new Mdct(Config.FrameSamplesPower, Tables.MdctWindow[Config.FrameSamplesPower - 6]);
                _spectra[b][c] = new double[Config.FrameSamples];
            }
        }
    }

    /// <summary>
    /// The MDCT here and the decoder's inverse MDCT differ by a constant factor; this is it (measured by the kit's round
    /// trip test: a full-scale sine comes back at the same level).
    /// </summary>
    public static double SpectrumGain { get; set; } = 1.0 / 128;

    /// <summary>How much less precise the top unit is than the bottom (a straight slope across the units).</summary>
    public static int GradientSlope { get; set; } = 0;

    /// <summary>Bands to code (0 = all the sample rate allows).</summary>
    public static int BandCountOverride { get; set; } = 0;

    /// <summary>Encodes a whole signal superframe by superframe and decodes each one back; the first failure, or "ok".</summary>
    public static string SelfTest(byte[] config, short[][] pcm)
    {
        var enc = new Atrac9Encoder(config);
        var dec = new Atrac9Decoder();
        dec.Initialize(config);
        int sfs = enc.Config.SuperframeSamples, total = pcm[0].Length / sfs;
        var outPcm = Enumerable.Range(0, enc.Config.ChannelCount).Select(_ => new short[sfs]).ToArray();
        for (int f = 0; f < total; f++)
        {
            var frame = pcm.Select(ch => ch.AsSpan(f * sfs, sfs).ToArray()).ToArray();
            try { dec.Decode(enc.EncodeSuperframe(frame), outPcm); }
            catch (Exception e) { return $"superframe {f} of {total} does not decode: {e.Message}"; }
        }
        return $"all {total} superframes decode";
    }

    /// <summary>Encodes one superframe. <paramref name="pcm"/> is [channel][sample], SuperframeSamples per channel.</summary>
    public byte[] EncodeSuperframe(short[][] pcm)
    {
        var w = new BitWriterBE(Config.SuperframeBytes);
        var input = new double[Config.FrameSamples];
        for (int f = 0; f < Config.FramesPerSuperframe; f++)
        {
            _frame.FrameIndex = f;
            int ch = 0;
            for (int b = 0; b < _frame.Blocks.Length; b++)
            {
                var block = _frame.Blocks[b];
                for (int c = 0; c < block.ChannelCount; c++, ch++)
                {
                    for (int i = 0; i < Config.FrameSamples; i++) input[i] = pcm[ch][f * Config.FrameSamples + i];
                    _mdct[b][c].RunMdct(input, _spectra[b][c]);
                    if (SpectrumGain != 1.0)
                        for (int i = 0; i < Config.FrameSamples; i++) _spectra[b][c][i] *= SpectrumGain;
                }
            }
            // each block gets its share of the frame's bits
            int frameBits = Config.FrameBytes * 8;
            int used = 0;
            for (int b = 0; b < _frame.Blocks.Length; b++)
            {
                var block = _frame.Blocks[b];
                int share = (frameBits - used) * block.ChannelCount / RemainingChannels(b);
                var bits = EncodeBlock(block, _spectra[b], share - 8); // leave room for the byte alignment
                w.WriteBits(bits);
                w.Align(8);
                used = w.Position - f * frameBits;
            }
            if (w.Position > (f + 1) * frameBits) throw new InvalidOperationException("ATRAC9 frame overflow.");
            w.Position = (f + 1) * frameBits;
        }
        return w.ToArray();
    }

    private int RemainingChannels(int fromBlock)
    {
        int n = 0;
        for (int b = fromBlock; b < _frame.Blocks.Length; b++) n += _frame.Blocks[b].ChannelCount;
        return n;
    }

    /// <summary>
    /// Encodes one block into at most <paramref name="budget"/> bits. For each candidate bandwidth it finds the lowest
    /// gradient (most precision) that fits, and keeps the bandwidth with the least total error, where the energy of the
    /// bands left out counts as error. So a small frame drops the top bands instead of starving every band.
    /// </summary>
    private BitWriterBE EncodeBlock(Block block, double[][] spectra, int budget)
    {
        int maxBands = Tables.MaxBandCount[Config.SampleRateIndex];
        if (BandCountOverride > 0) maxBands = Math.Min(BandCountOverride, maxBands);
        int minBands = Math.Max(Tables.MinBandCount(false), 8);
        int bestBands = -1, bestG = -1;
        double bestErr = double.MaxValue;
        for (int bands = maxBands; bands >= minBands; bands -= bands > 14 ? 1 : 2)
        {
            SetBands(block, bands, spectra);
            int lo = 0, hi = 31, g = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (Write(block, spectra, mid).Position <= budget) { g = mid; hi = mid - 1; }
                else lo = mid + 1;
            }
            if (g < 0) continue;
            Write(block, spectra, g);
            double err = Error(block, spectra);
            if (err < bestErr) { bestErr = err; bestBands = bands; bestG = g; }
            else if (bestBands > 0) break; // fewer bands only helped while it freed precision
        }
        if (bestBands < 0)
        {
            SetBands(block, minBands, spectra);
            return WriteSilence(block, spectra, budget);
        }
        // write the chosen one last, so the block's state (and the trace) is the one that goes out
        SetBands(block, bestBands, spectra);
        return Write(block, spectra, bestG);
    }

    private void SetBands(Block block, int bandCount, double[][] spectra)
    {
        block.BandCount = bandCount;
        block.QuantizationUnitCount = Tables.BandToQuantUnitCount[bandCount];
        block.StereoBand = bandCount;
        block.StereoQuantizationUnit = block.QuantizationUnitCount;
        block.BandExtensionEnabled = false;
        block.ExtensionBand = bandCount;
        block.ExtensionUnit = block.QuantizationUnitCount;
        block.FirstInSuperframe = block.Frame.FrameIndex == 0;
        block.ReuseBandParams = false;
        block.PrimaryChannelIndex = 0;
        block.HasJointStereoSigns = false;
        Array.Clear(block.JointStereoSigns, 0, block.JointStereoSigns.Length);
        block.HasExtensionData = false;
        block.GradientMode = 0;
        block.GradientBoundary = 0;

        // scale factors: the smallest whose scale covers each quantization unit's largest coefficient
        foreach (var channel in block.Channels)
        {
            var spec = spectra[channel.ChannelIndex];
            for (int u = 0; u < block.QuantizationUnitCount; u++)
            {
                double max = 0;
                for (int k = Tables.QuantUnitToCoeffIndex[u]; k < Tables.QuantUnitToCoeffIndex[u + 1]; k++) max = Math.Max(max, Math.Abs(spec[k]));
                int sf = 0;
                while (sf < 31 && Tables.SpectrumScale[sf] < max) sf++;
                channel.ScaleFactors[u] = sf;
            }
            for (int u = block.QuantizationUnitCount; u < channel.ScaleFactors.Length; u++) channel.ScaleFactors[u] = 0;
        }
    }

    /// <summary>Squared error of the block as written: quantization error in the coded units, the full energy above them.</summary>
    private static double Error(Block block, double[][] spectra)
    {
        double err = 0;
        foreach (var channel in block.Channels)
        {
            var spec = spectra[channel.ChannelIndex];
            int coded = Tables.QuantUnitToCoeffIndex[channel.CodedQuantUnits];
            for (int u = 0; u < channel.CodedQuantUnits; u++)
            {
                double scale = Tables.SpectrumScale[channel.ScaleFactors[u]];
                double step = Tables.QuantizerStepSize[channel.Precisions[u]], fine = Tables.QuantizerFineStepSize[channel.PrecisionsFine[u]];
                for (int k = Tables.QuantUnitToCoeffIndex[u]; k < Tables.QuantUnitToCoeffIndex[u + 1]; k++)
                {
                    double d = spec[k] - (channel.QuantizedSpectra[k] * step + channel.QuantizedSpectraFine[k] * fine) * scale;
                    err += d * d;
                }
            }
            for (int k = coded; k < spec.Length; k++) err += spec[k] * spec[k];
        }
        return err;
    }

    private BitWriterBE WriteSilence(Block block, double[][] spectra, int budget)
    {
        foreach (var channel in block.Channels) Array.Clear(spectra[channel.ChannelIndex], 0, spectra[channel.ChannelIndex].Length);
        foreach (var channel in block.Channels) Array.Clear(channel.ScaleFactors, 0, channel.ScaleFactors.Length);
        var w = Write(block, spectra, 31);
        if (w.Position > budget) throw new InvalidOperationException("The ATRAC9 frame size is too small for even a silent block.");
        return w;
    }

    private BitWriterBE Write(Block block, double[][] spectra, int gradient)
    {
        // a straight slope from the first to the last unit: higher values (less precision) towards the top
        int end = Math.Min(31, gradient + GradientSlope);
        block.GradientStartUnit = 1;
        block.GradientEndUnit = GradientSlope > 0 ? Math.Min(47, block.QuantizationUnitCount) : 1;
        block.GradientStartValue = gradient;
        block.GradientEndValue = GradientSlope > 0 ? end : gradient;
        BitAllocation.CreateGradient(block);
        var w = new BitWriterBE(Config.FrameBytes);

        // block header and band parameters (Unpack.ReadBlockHeader, ReadBandParams)
        w.Write(block.FirstInSuperframe ? 0 : 1, 1);
        w.Write(0, 1); // ReuseBandParams
        int minBand = Tables.MinBandCount(false);
        w.Write(block.BandCount - minBand, 4);
        if (block.BlockType == BlockType.Stereo) w.Write(block.StereoBand - minBand, 4);
        w.Write(0, 1); // no band extension
        // gradient (ReadGradientParams, mode 0)
        w.Write(0, 2);
        w.Write(block.GradientStartUnit, 6);
        w.Write(block.GradientEndUnit - 1, 6);
        w.Write(block.GradientStartValue, 5);
        w.Write(block.GradientEndValue, 5);
        w.Write(block.GradientBoundary, 4);
        // stereo (ReadStereoParams) and extension (ReadExtensionParams)
        if (block.BlockType == BlockType.Stereo) { w.Write(0, 1); w.Write(0, 1); }
        w.Write(0, 1); // no extension data

        foreach (var channel in block.Channels)
        {
            channel.UpdateCodedUnits();
            WriteScaleFactors(w, block, channel);
            BitAllocation.CalculateMask(channel);
            BitAllocation.CalculatePrecisions(channel);
            Unpack.CalculateSpectrumCodebookIndex(channel);
            Quantize(channel, spectra[channel.ChannelIndex]);
            WriteSpectra(w, channel);
        }
        return w;
    }

    private static void WriteScaleFactors(BitWriterBE w, Block block, Channel channel)
    {
        int n = block.ExtensionUnit;
        var sf = channel.ScaleFactors;
        if (channel.ChannelIndex == 0)
        {
            // mode 1: fixed width with an offset (ReadClcOffset)
            int min = 31, max = 0;
            for (int i = 0; i < n; i++) { min = Math.Min(min, sf[i]); max = Math.Max(max, sf[i]); }
            int bits = 2;
            while (bits < 5 && (max - min) >= (1 << bits)) bits++;
            int offset = bits < 5 ? min : 0;
            w.Write(1, 2);
            w.Write(bits - 2, 2);
            if (bits < 5) w.Write(offset, 5);
            for (int i = 0; i < n; i++) w.Write(sf[i] - offset, bits);
        }
        else
        {
            // mode 1 on channel 1: signed Huffman distance to channel 0's scale factors (ReadVlcDistanceToBaseline)
            var base0 = block.Channels[0].ScaleFactors;
            int bits = 5;
            for (int b = 2; b <= 5; b++)
            {
                var hb = Tables.HuffmanScaleFactorsSigned[b];
                bool ok = true;
                for (int i = 0; i < n && ok; i++)
                {
                    int d = ((sf[i] - base0[i] + 16) & 31) - 16;
                    ok = d >= -(1 << (b - 1)) && d < (1 << (b - 1)) && hb.Bits[d & ((1 << hb.ValueBits) - 1)] != 0;
                }
                if (ok) { bits = b; break; }
            }
            var huff = Tables.HuffmanScaleFactorsSigned[bits];
            // a distance the codebook has no code for: raise that scale factor (a larger scale never clips) until it has one
            for (int i = 0; i < n; i++)
            {
                int guard = 0;
                while (huff.Bits[(((sf[i] - base0[i] + 16) & 31) - 16) & ((1 << huff.ValueBits) - 1)] == 0 && guard++ < 64)
                    sf[i] = sf[i] < 31 ? sf[i] + 1 : Math.Max(0, sf[i] - 1);
            }
            w.Write(1, 2);
            w.Write(bits - 2, 2);
            for (int i = 0; i < n; i++)
            {
                int d = ((sf[i] - base0[i] + 16) & 31) - 16;
                int sym = d & ((1 << huff.ValueBits) - 1);
                w.Write(huff.Codes[sym], huff.Bits[sym]);
            }
        }
    }

    /// <summary>Quantizes a channel's spectrum with the precisions the decoder will derive (normal and fine parts).</summary>
    private static void Quantize(Channel channel, double[] spectrum)
    {
        Array.Clear(channel.QuantizedSpectra, 0, channel.QuantizedSpectra.Length);
        Array.Clear(channel.QuantizedSpectraFine, 0, channel.QuantizedSpectraFine.Length);
        int maxHuff = Tables.MaxHuffPrecision(false);
        for (int u = 0; u < channel.CodedQuantUnits; u++)
        {
            int p = channel.Precisions[u], pf = channel.PrecisionsFine[u];
            double scale = Tables.SpectrumScale[channel.ScaleFactors[u]];
            double step = Tables.QuantizerStepSize[p], fine = Tables.QuantizerFineStepSize[pf];
            int lo = -(1 << p), hi = (1 << p) - 1;
            HuffmanCodebook huff = p + 1 <= maxHuff ? Tables.HuffmanSpectrum[channel.CodebookSet[u]][p + 1][Tables.QuantUnitToCodebookIndex[u]] : null;
            for (int k = Tables.QuantUnitToCoeffIndex[u]; k < Tables.QuantUnitToCoeffIndex[u + 1]; k++)
            {
                double v = spectrum[k] / scale;
                int q = Math.Clamp((int)Math.Round(v / step), lo, hi);
                if (huff != null)
                {
                    // the codebook covers values of its own width
                    // symmetric: the most negative value of a width usually has no code
                    int vlo = -((1 << (huff.ValueBits - 1)) - 1), vhi = (1 << (huff.ValueBits - 1)) - 1;
                    q = Math.Clamp(q, vlo, vhi);
                }
                channel.QuantizedSpectra[k] = q;
                if (pf > 0)
                {
                    int flo = -(1 << pf), fhi = (1 << pf) - 1;
                    channel.QuantizedSpectraFine[k] = Math.Clamp((int)Math.Round((v - q * step) / fine), flo, fhi);
                }
            }
        }
    }

    private static void WriteSpectra(BitWriterBE w, Channel channel)
    {
        int maxHuff = Tables.MaxHuffPrecision(false);
        for (int u = 0; u < channel.CodedQuantUnits; u++)
        {
            int count = Tables.QuantUnitToCoeffCount[u], first = Tables.QuantUnitToCoeffIndex[u];
            int bits = channel.Precisions[u] + 1;
            if (bits <= maxHuff)
            {
                var huff = Tables.HuffmanSpectrum[channel.CodebookSet[u]][bits][Tables.QuantUnitToCodebookIndex[u]];
                int groups = count >> huff.ValueCountPower;
                int mask = (1 << huff.ValueBits) - 1;
                for (int g = 0; g < groups; g++)
                {
                    int sym = 0;
                    for (int j = 0; j < huff.ValueCount; j++)
                        sym |= (channel.QuantizedSpectra[first + g * huff.ValueCount + j] & mask) << (j * huff.ValueBits);
                    if (huff.Bits[sym] == 0)
                    {
                        // a combination the codebook can't express: the closest one it can
                        int best = -1; long bestErr = long.MaxValue;
                        for (int s2 = 0; s2 < huff.Bits.Length; s2++)
                        {
                            if (huff.Bits[s2] == 0) continue;
                            long err = 0;
                            for (int j = 0; j < huff.ValueCount; j++)
                            {
                                int v2 = Bit.SignExtend32((s2 >> (j * huff.ValueBits)) & mask, huff.ValueBits);
                                int d = v2 - channel.QuantizedSpectra[first + g * huff.ValueCount + j];
                                err += d * d;
                            }
                            if (err < bestErr) { bestErr = err; best = s2; }
                        }
                        sym = best;
                        for (int j = 0; j < huff.ValueCount; j++)
                            channel.QuantizedSpectra[first + g * huff.ValueCount + j] = Bit.SignExtend32((sym >> (j * huff.ValueBits)) & mask, huff.ValueBits);
                    }
                    w.Write(huff.Codes[sym], huff.Bits[sym]);
                }
            }
            else
            {
                for (int k = first; k < first + count; k++) w.Write(channel.QuantizedSpectra[k] & ((1 << bits) - 1), bits);
            }
        }
        for (int u = 0; u < channel.CodedQuantUnits; u++)
        {
            int pf = channel.PrecisionsFine[u];
            if (pf <= 0) continue;
            for (int k = Tables.QuantUnitToCoeffIndex[u]; k < Tables.QuantUnitToCoeffIndex[u + 1]; k++)
                w.Write(channel.QuantizedSpectraFine[k] & ((1 << (pf + 1)) - 1), pf + 1);
        }
    }
}

/// <summary>A most-significant-bit-first bit writer (the order <see cref="BitReader"/> reads).</summary>
internal sealed class BitWriterBE
{
    private byte[] _buf;

    public BitWriterBE(int bytes) => _buf = new byte[Math.Max(16, bytes * 2)];

    public int Position { get; set; }

    public void Write(int value, int bits)
    {
        for (int i = bits - 1; i >= 0; i--)
        {
            int byteIndex = Position >> 3;
            if (byteIndex >= _buf.Length) Array.Resize(ref _buf, _buf.Length * 2);
            if (((value >> i) & 1) != 0) _buf[byteIndex] |= (byte)(0x80 >> (Position & 7));
            Position++;
        }
    }

    public void WriteBits(BitWriterBE other)
    {
        for (int i = 0; i < other.Position; i++)
            Write((other._buf[i >> 3] >> (7 - (i & 7))) & 1, 1);
    }

    public void Align(int multiple) => Position = (Position + multiple - 1) / multiple * multiple;

    public byte[] ToArray()
    {
        var r = new byte[(Position + 7) / 8];
        Array.Copy(_buf, r, Math.Min(r.Length, _buf.Length));
        return r;
    }
}
