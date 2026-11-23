namespace NHL27Kit.Core;

/// <summary>
/// Where the kit keeps its own files: the kit folder (the one with NHL27Kit.slnx, found above the running program)
/// when there is one, else %LOCALAPPDATA%\NHL27Kit. The game folder defaults to "NHL 27 dump/NHL" next to it.
/// Each game has its own index cache and mod list: NHL 27's are cache\index.bin and mods\ (where they always were),
/// another game's cache\&lt;key&gt;\index.bin and mods-&lt;key&gt;\ (<see cref="GameProfile.Key"/>).
/// </summary>
public static class KitPaths
{
    public static string Root { get; } = FindRoot();

    public static string Cache => Path.Combine(Root, "cache");
    public static string IndexFile => Path.Combine(Cache, "index.bin");

    /// <summary>The game's index cache.</summary>
    public static string IndexFileFor(GameProfile game) =>
        game == null || game.Kind == GameKind.Nhl27 ? IndexFile : Path.Combine(Cache, game.Key, "index.bin");

    /// <summary>The game's mod list folder.</summary>
    public static string ModsFor(GameProfile game) =>
        Path.Combine(Root, game == null || game.Kind == GameKind.Nhl27 ? "mods" : "mods-" + game.Key);
    public static string Export => Path.Combine(Root, "export");

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "NHL27Kit.slnx")) || File.Exists(Path.Combine(dir.FullName, "NHL27Kit.sln")))
                return dir.FullName;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NHL27Kit");
    }

    /// <summary>"NHL 27 dump/NHL" in or above the program's folder, or null.</summary>
    public static string FindGame()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "NHL 27 dump", "NHL");
            if (IsGame(candidate))
                return candidate;
        }
        return null;
    }

    /// <summary>
    /// Game folders near the kit: in or below (two levels) each of the five folders above the program, so a dump next to
    /// the kit folder ("NHL 27 dump/NHL", "NHL 24 Dump") is found. The same goes for the folders above <paramref name="near"/>
    /// (the games already known), so a kit run from elsewhere (its zip, Desktop) still finds the dumps next to them. NHL 27 first.
    /// </summary>
    public static List<string> FindGames(IEnumerable<string> near = null)
    {
        var found = new List<string>();
        void Add(string d)
        {
            // a dump has its eboot; FMT's __FMTBackup (a copy of Data) doesn't
            if (!IsGame(d) || !File.Exists(Path.Combine(d, "eboot.bin"))) return;
            string full = Path.GetFullPath(Canonical(d)).TrimEnd('\\', '/');
            if (!found.Contains(full, StringComparer.OrdinalIgnoreCase)) found.Add(full);
        }
        IEnumerable<string> Dirs(string d)
        {
            try { return Directory.GetDirectories(d); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return Array.Empty<string>(); }
        }
        var scanned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Scan(string start, int levels)
        {
            var dir = new DirectoryInfo(start);
            for (int up = 0; dir != null && up < levels; dir = dir.Parent, up++)
            {
                // not a drive or a folder right on it: too much below them
                if (dir.Parent == null || dir.Parent.Parent == null || !scanned.Add(dir.FullName)) continue;
                foreach (string child in Dirs(dir.FullName))
                {
                    Add(child);
                    foreach (string grand in Dirs(child)) Add(grand);
                }
            }
        }
        Scan(AppContext.BaseDirectory, 5);
        foreach (string game in near ?? Enumerable.Empty<string>())
            if (IsGame(game)) Scan(game, 3);
        return found.OrderBy(d => GameProfile.Detect(d).Kind == GameKind.Nhl27 ? 0 : 1).ThenBy(d => d, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool IsGame(string dir) => !string.IsNullOrWhiteSpace(dir) && File.Exists(Path.Combine(DataDirOf(dir), "layout.toc"));

    // shadPS4 (NHL24 PC) installs a game's update next to it: <game>-UPDATE holds the update's eboot.bin, sce_sys and
    // Patch, its Data is empty, and the emulator reads <game>\Data underneath. The pair is one game, known by its update
    // folder, so picking either folder opens the same game (and the base folder alone isn't mistaken for the old version).
    private const string UpdateSuffix = "-UPDATE";

    /// <summary>The update folder next to a base game folder (shadPS4's &lt;game&gt;-UPDATE with a Patch), or null.</summary>
    public static string UpdateOf(string dir)
    {
        if (Full(dir) is not { } full || full.EndsWith(UpdateSuffix, StringComparison.OrdinalIgnoreCase)) return null;
        string update = full + UpdateSuffix;
        return File.Exists(Path.Combine(update, "Patch", "layout.toc")) && File.Exists(Path.Combine(full, "Data", "layout.toc")) ? update : null;
    }

    /// <summary>The base game folder an update folder sits on (its Data is the game's data), or null.</summary>
    public static string BaseOf(string dir)
    {
        if (Full(dir) is not { } full) return null;
        if (!full.EndsWith(UpdateSuffix, StringComparison.OrdinalIgnoreCase) || File.Exists(Path.Combine(full, "Data", "layout.toc"))) return null;
        string root = full[..^UpdateSuffix.Length];
        return File.Exists(Path.Combine(root, "Data", "layout.toc")) ? root : null;
    }

    private static string Full(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir)) return null;
        try { return Path.GetFullPath(dir).TrimEnd('\\', '/'); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }

    /// <summary>The folder the kit knows a game by: the update folder of a base game that has one, else the folder itself.</summary>
    public static string Canonical(string dir) => UpdateOf(dir) ?? dir;

    /// <summary>The game's Data folder: its own, or the base game's for an update folder.</summary>
    public static string DataDirOf(string dir) => string.IsNullOrWhiteSpace(dir) ? "Data" :
        !File.Exists(Path.Combine(dir, "Data", "layout.toc")) && BaseOf(dir) is { } root ? Path.Combine(root, "Data") : Path.Combine(dir, "Data");
}
