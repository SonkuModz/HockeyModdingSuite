using System.Diagnostics;
using System.Text;

namespace NHL27Kit.Core;

/// <summary>
/// ffmpeg, when it is installed: NHL27KIT_FFMPEG, ThirdParty\ffmpeg (next to the kit or the program), PATH, or winget's
/// Gyan.FFmpeg. The kit doesn't ship it; it reads other audio formats and encodes and converts movies.
/// </summary>
public static class Ffmpeg
{
    public static string Find()
    {
        var env = Environment.GetEnvironmentVariable("NHL27KIT_FFMPEG");
        if (File.Exists(env)) return env;
        foreach (string root in new[] { KitPaths.Root, AppContext.BaseDirectory })
            foreach (string sub in new[] { Path.Combine("ThirdParty", "ffmpeg", "bin"), Path.Combine("ThirdParty", "ffmpeg"), "ThirdParty", "" })
            {
                string f = Path.Combine(root, sub, "ffmpeg.exe");
                if (File.Exists(f)) return f;
            }
        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            try { string f = Path.Combine(dir.Trim(), "ffmpeg.exe"); if (File.Exists(f)) return f; } catch { }
        }
        string winget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages");
        try
        {
            if (Directory.Exists(winget))
                foreach (var d in Directory.GetDirectories(winget, "*FFmpeg*"))
                {
                    var f = Directory.EnumerateFiles(d, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
                    if (f != null) return f;
                }
        }
        catch { }
        return null;
    }

    public static string Require() => Find() ?? throw new NotSupportedException(
        "ffmpeg isn't installed. Put ffmpeg.exe in a ThirdParty\\ffmpeg folder next to NHL27Kit.exe, or install it (winget install Gyan.FFmpeg).");

    /// <summary>Runs ffmpeg; returns its error output, or throws with it when ffmpeg fails.</summary>
    public static async Task<string> RunAsync(IEnumerable<string> args, CancellationToken ct = default, Action<string> line = null)
    {
        var psi = new ProcessStartInfo(Require()) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (string a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi);
        var err = new StringBuilder();
        var stdout = p.StandardOutput.ReadToEndAsync();
        string l;
        try
        {
            while ((l = await p.StandardError.ReadLineAsync(ct)) != null)
            {
                err.AppendLine(l);
                line?.Invoke(l);
            }
            await p.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(true); } catch { }
            throw;
        }
        await stdout;
        if (p.ExitCode != 0)
        {
            var lines = err.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            throw new InvalidDataException("ffmpeg failed: " + string.Join(" ", lines.TakeLast(3)));
        }
        return err.ToString();
    }

    public static string RunSync(IEnumerable<string> args) => RunAsync(args).GetAwaiter().GetResult();
}
