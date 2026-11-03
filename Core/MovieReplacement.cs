using System.Buffers.Binary;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NHL27Kit.Core;

/// <summary>What a replacement for a movie has to be: the original's picture size, frame rate, frame count and bytes.</summary>
public sealed record MovieTarget(int Width, int Height, double Fps, int Frames, long Size, int Keyframes,
                                 string AudioCodec, int AudioChannels, int AudioRate)
{
    public double Seconds => Frames / Fps;
    public bool Audio => AudioCodec != null;
}

/// <summary>How a picture of another shape goes into the movie's frame.</summary>
public enum MovieFit { Fill, Fit, Stretch }

/// <summary>A replacement ready to write: the new WebM and EBX, and the byte patches for both.</summary>
public sealed class MoviePlan
{
    public Movie Movie { get; init; }
    public MovieTarget Target { get; init; }
    public byte[] Webm { get; init; }
    public byte[] Ebx { get; init; }
    public ChunkPlan Chunk { get; init; }
    public ChunkPlan EbxPlan { get; init; }
    public List<string> Notes { get; } = new();
    /// <summary>Where the kit keeps the WebM (the mod's source).</summary>
    public string Source { get; set; }
    public IReadOnlyList<PatchData> Patches => Chunk.Patches.Concat(EbxPlan.Patches).ToList();
}

/// <summary>
/// Replaces a WebM movie in place. The asset (MovieTexture2Asset) keeps a frame table: each video frame's time, and
/// where its data starts and how long it is in the WebM (checked against all 423 WebM movies: every entry matches).
/// The table can't change length in place, and neither can the chunk, so a replacement is encoded (with ffmpeg, VP8)
/// to the original's size, frame rate and exact frame count, at a bitrate that fits the original's bytes; the rest is
/// filled with an EBML Void inside the Segment. Then the table is rewritten for the new frames.
/// </summary>
public static class MovieReplacement
{
    /// <summary>What a replacement for <paramref name="m"/> has to match.</summary>
    public static MovieTarget Target(AssetIndex index, Movie m)
    {
        var webm = Movies.Read(index, m);
        var info = Movies.Probe(webm) ?? throw new NotSupportedException($"{m.Short} isn't a WebM movie (it's EA's older VP6 format), so it can't be replaced yet.");
        if (info.VideoCodec != "V_VP8") throw new NotSupportedException($"{m.Short} is {info.Codec}; only VP8 movies can be replaced.");
        var frames = Movies.Frames(webm);
        var ebx = index.Read(m.Entry);
        if (Movies.FrameTable(ebx, frames) < 0) throw new InvalidDataException($"{m.Short}'s frame table wasn't found in its asset.");
        return new MovieTarget(info.Width, info.Height, FrameRate(frames), frames.Count, webm.Length,
            Math.Max(1, frames.Count(f => f.Key)), info.AudioCodec, info.AudioChannels, (int)Math.Round(info.AudioRate));
    }

    /// <summary>The frame rate the frames' times give, snapped to a usual one.</summary>
    public static double FrameRate(IReadOnlyList<MovieFrame> frames)
    {
        if (frames.Count < 2) return 30;
        double fps = (frames.Count - 1) / (frames[^1].Seconds - frames[0].Seconds);
        double best = new[] { 23.976, 24, 25, 29.97, 30, 48, 50, 59.94, 60 }.MinBy(u => Math.Abs(fps - u) / u);
        return Math.Abs(fps - best) / best < 0.006 ? best : Math.Round(fps, 3);
    }

    private static string Num(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>
    /// Encodes <paramref name="source"/> (any video or picture ffmpeg reads) to fit <paramref name="t"/> exactly: same
    /// size, rate and frame count (a short source holds its last frame), and the original's bytes (padded).
    /// </summary>
    public static async Task<byte[]> EncodeAsync(MovieTarget t, string source, MovieFit fit, bool keepAudio, Action<string> progress = null, CancellationToken ct = default)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "NHL27Kit", "movie_" + Guid.NewGuid().ToString("N") + ".webm"), passlog = tmp[..^5] + "_pass";
        Directory.CreateDirectory(Path.GetDirectoryName(tmp));
        try
        {
            bool sourceAudio = keepAudio && t.Audio && await HasAudioAsync(source, ct);
            bool still = IsStill(source);
            long budget = t.Size - 64;
            double seconds = t.Seconds, audioBits = t.Audio ? 128_000 : 0;
            double bitrate = Math.Clamp((budget * 8 / seconds - audioBits) * 0.9, 100_000, 60_000_000);
            int gop = Math.Clamp(t.Frames / t.Keyframes, 30, 300);
            string scale = fit switch
            {
                MovieFit.Fit => $"scale={t.Width}:{t.Height}:force_original_aspect_ratio=decrease,pad={t.Width}:{t.Height}:(ow-iw)/2:(oh-ih)/2:color=black",
                MovieFit.Stretch => $"scale={t.Width}:{t.Height}",
                _ => $"scale={t.Width}:{t.Height}:force_original_aspect_ratio=increase,crop={t.Width}:{t.Height}",
            };
            for (int attempt = 1; attempt <= 6; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Invoke($"Encoding at {bitrate / 1e6:0.0} Mbps (try {attempt}){(attempt >= 3 ? ", with less detail to fit" : "")}…");
                // two passes: the first measures the clip, so the second hits the bitrate closely
                List<string> Args(int pass)
                {
                    var args = new List<string> { "-y", "-v", "error", "-stats" };
                    if (still) args.AddRange(new[] { "-loop", "1", "-framerate", Num(t.Fps) });
                    args.AddRange(new[] { "-i", source });
                    bool audio = t.Audio && pass == 2;
                    string layout = t.AudioChannels == 1 ? "mono" : "stereo";
                    if (audio && !sourceAudio) args.AddRange(new[] { "-f", "lavfi", "-i", $"anullsrc=r={t.AudioRate}:cl={layout}" });
                    string filter = $"[0:v]{scale},setsar=1{Simplify(t, attempt)},fps={Num(t.Fps)},tpad=stop_mode=clone:stop_duration={Num(seconds + 1)},format=yuv420p[v]";
                    if (audio) filter += sourceAudio ? $";[0:a]aresample={t.AudioRate},apad[a]" : ";[1:a]anull[a]";
                    args.AddRange(new[] { "-filter_complex", filter, "-map", "[v]" });
                    if (audio) args.AddRange(new[] { "-map", "[a]", "-c:a", "libvorbis", "-b:a", "128k", "-ac", t.AudioChannels.ToString(), "-ar", t.AudioRate.ToString() });
                    else args.Add("-an");
                    args.AddRange(new[]
                    {
                        "-frames:v", t.Frames.ToString(), "-t", Num(seconds),
                        "-c:v", "libvpx", "-b:v", ((long)bitrate).ToString(), "-qmin", "0", "-qmax", "63", "-undershoot-pct", "95", "-overshoot-pct", "0",
                        "-deadline", "good", "-cpu-used", pass == 1 ? "4" : "1", "-auto-alt-ref", "0", "-g", gop.ToString(),
                        "-pass", pass.ToString(), "-passlogfile", passlog,
                        "-sn", "-dn", "-map_metadata", "-1", "-f", pass == 1 ? "null" : "webm", pass == 1 ? "NUL" : tmp,
                    });
                    return args;
                }
                for (int pass = 1; pass <= 2; pass++)
                {
                    int pn = pass;
                    await Ffmpeg.RunAsync(Args(pass), ct, l =>
                    {
                        var m = Regex.Match(l, @"frame=\s*(\d+)");
                        if (m.Success) progress?.Invoke($"Encoding at {bitrate / 1e6:0.0} Mbps (try {attempt}, pass {pn} of 2): frame {m.Groups[1].Value} of {t.Frames}");
                    });
                }
                var webm = await File.ReadAllBytesAsync(tmp, ct);
                int frames = Movies.Frames(webm).Count;
                if (frames != t.Frames) throw new InvalidDataException($"ffmpeg made {frames} frames, the movie needs {t.Frames}.");
                if (webm.Length <= budget && Pad(webm, t.Size) is { } padded) return padded;
                progress?.Invoke($"{webm.Length / 1048576.0:0.00} MB is over the {budget / 1048576.0:0.00} MB the movie has.");
                bitrate *= Math.Min(0.97, Math.Pow((double)budget / webm.Length, 1.3) * 0.95);
            }
            throw new InvalidDataException("The video didn't fit the original's size after 6 tries; try a simpler or shorter clip.");
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
            try { foreach (var f in Directory.GetFiles(Path.GetDirectoryName(tmp), Path.GetFileName(passlog) + "*")) File.Delete(f); } catch { }
        }
    }

    /// <summary>
    /// Later tries give the encoder less to spend bits on, for a busy clip and a small movie: softer detail (try 3),
    /// then each picture held for 2 or 3 frames (tries 4-6). The frame count and rate stay the same.
    /// </summary>
    private static string Simplify(MovieTarget t, int attempt)
    {
        string soft = attempt switch { >= 5 => $",scale={t.Width / 4 * 2}:{t.Height / 4 * 2},scale={t.Width}:{t.Height}", >= 3 => $",scale={t.Width / 3 * 2}:{t.Height / 3 * 2},scale={t.Width}:{t.Height}", _ => "" };
        int hold = attempt switch { >= 6 => 3, >= 4 => 2, _ => 1 };
        return soft + (hold > 1 ? $",fps={Num(t.Fps / hold)}" : "");
    }

    private static bool IsStill(string file) =>
        Path.GetExtension(file).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tif" or ".tiff" or ".webp";

    private static async Task<bool> HasAudioAsync(string file, CancellationToken ct)
    {
        try
        {
            string err = await Ffmpeg.RunAsync(new[] { "-hide_banner", "-i", file, "-f", "null", "-t", "0", "-" }, ct);
            return Regex.IsMatch(err, @"Stream #\S+.*: Audio:");
        }
        catch (InvalidDataException) { return false; }
    }

    /// <summary>
    /// Grows a WebM to exactly <paramref name="size"/> bytes with an EBML Void at the end of its Segment (whose size is
    /// rewritten to hold it). Null when it can't (a 1-byte gap, or a Segment that doesn't end the file).
    /// </summary>
    public static byte[] Pad(byte[] webm, long size)
    {
        long gap = size - webm.Length;
        if (gap == 0) return webm;
        if (gap < 2) return null;
        // EBML header, then the Segment: id 18 53 80 67 and an 8-byte size (01 + 7 bytes), as ffmpeg writes it
        int p = 0;
        if (BinaryPrimitives.ReadUInt32BigEndian(webm) != 0x1A45DFA3) return null;
        p = 4;
        int n = 1 + System.Numerics.BitOperations.LeadingZeroCount((uint)webm[p]) - 24;
        long hsize = webm[p] & (0xFF >> n);
        for (int i = 1; i < n; i++) hsize = hsize << 8 | webm[p + i];
        p += n + (int)hsize;
        if (p + 12 > webm.Length || BinaryPrimitives.ReadUInt32BigEndian(webm.AsSpan(p)) != 0x18538067 || webm[p + 4] != 0x01) return null;
        long seg = BinaryPrimitives.ReadInt64BigEndian(webm.AsSpan(p + 4)) & 0x00FFFFFFFFFFFFFF;
        if (p + 12 + seg != webm.Length) return null;
        var output = new byte[size];
        webm.CopyTo(output, 0);
        BinaryPrimitives.WriteInt64BigEndian(output.AsSpan(p + 4), (seg + gap) | 0x0100000000000000);
        int at = webm.Length;
        output[at] = 0xEC;   // Void
        if (gap >= 9) BinaryPrimitives.WriteInt64BigEndian(output.AsSpan(at + 1), (gap - 9) | 0x0100000000000000);
        else output[at + 1] = (byte)(0x80 | (gap - 2));
        return output;
    }

    /// <summary>The asset with its frame table rewritten for <paramref name="webm"/> (same frame count).</summary>
    public static byte[] Retable(byte[] ebx, int at, byte[] webm)
    {
        var frames = Movies.Frames(webm);
        int count = (int)BinaryPrimitives.ReadUInt32LittleEndian(ebx.AsSpan(at));
        if (frames.Count != count) throw new InvalidDataException($"The video has {frames.Count} frames; the movie's table holds {count}.");
        var output = (byte[])ebx.Clone();
        for (int i = 0; i < count; i++)
        {
            int o = at + 4 + 12 * i;
            BinaryPrimitives.WriteSingleLittleEndian(output.AsSpan(o), (float)frames[i].Seconds);
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(o + 4), (uint)frames[i].Offset);
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(o + 8), (uint)frames[i].Size);
        }
        return output;
    }

    /// <summary>Plans writing a ready WebM (from <see cref="EncodeAsync"/> or a pack) over a movie.</summary>
    public static MoviePlan Plan(AssetIndex index, Movie m, byte[] webm, LocationIndex locations, TocCache tocs)
    {
        var original = Movies.Read(index, m);
        if (webm.Length != original.Length) throw new InvalidDataException($"The video is {webm.Length:N0} bytes; the movie's chunk is {original.Length:N0}.");
        var oldFrames = Movies.Frames(original);
        var ebx = index.Read(m.Entry);
        int at = Movies.FrameTable(ebx, oldFrames);
        if (at < 0) throw new InvalidDataException($"{m.Short}'s frame table wasn't found in its asset.");
        var newEbx = Retable(ebx, at, webm);
        var plan = new MoviePlan
        {
            Movie = m,
            Target = Target(index, m),
            Webm = webm,
            Ebx = newEbx,
            Chunk = ChunkPatcher.Plan(index, m.ChunkId, webm, locations, tocs),
            EbxPlan = ResPatcher.PlanEbx(index, m.Name, newEbx, locations, tocs),
        };
        plan.Notes.AddRange(plan.Chunk.Notes);
        plan.Notes.AddRange(plan.EbxPlan.Notes);
        return plan;
    }
}
