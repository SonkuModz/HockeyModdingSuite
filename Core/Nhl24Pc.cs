using System.Diagnostics;

namespace NHL27Kit.Core;

/// <summary>
/// NHL24 PC's game folder: install\game, usually a junction to the extracted NHL 24 dump. <see cref="Relink"/> points it
/// at another dump, and only ever replaces a junction (a real folder there is left alone).
/// </summary>
public static class Nhl24Pc
{
    public static string GameLink(string install) => Path.Combine(install, "game");

    /// <summary>Where install\game points, without "\??\", or null when it isn't a link.</summary>
    public static string LinkTarget(string install)
    {
        try
        {
            string target = new DirectoryInfo(GameLink(install)).LinkTarget;
            return target != null && target.StartsWith(@"\??\") ? target[4..] : target;
        }
        catch (IOException) { return null; }
    }

    /// <summary>
    /// Makes install\game a junction to <paramref name="dump"/>. Returns the old target (or null when there was no link),
    /// and refuses when game is a real folder, the dump has no eboot.bin, or NHL24 PC is running.
    /// </summary>
    public static string Relink(string install, string dump)
    {
        if (Emulators.Running() is { } emu) throw new InvalidOperationException($"{emu} is running. Close the game first.");
        if (!File.Exists(Path.Combine(dump, "eboot.bin"))) throw new InvalidOperationException($"No eboot.bin in {dump}.");
        string link = GameLink(install);
        string old = LinkTarget(install);
        if (Directory.Exists(link) || old != null)
        {
            if (old == null) throw new InvalidOperationException($"{link} is a real folder, not a link: move it away first.");
            // deleting a junction (not recursive) removes the link only, never what it points at
            Directory.Delete(link, recursive: false);
        }
        // a junction needs no admin rights, unlike a symbolic link
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string a in new[] { "/c", "mklink", "/J", link, Path.GetFullPath(dump) })
            start.ArgumentList.Add(a);
        using var p = Process.Start(start);
        string error = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0 || LinkTarget(install) == null)
            throw new IOException($"Couldn't make the link: {error.Trim()}" + (old != null ? $" (it pointed at {old})" : ""));
        return old;
    }
}
