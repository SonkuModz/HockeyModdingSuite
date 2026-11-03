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
    /// the kit folder ("NHL 27 dump/NHL", "NHL 24 Dump") is found. NHL 27 first.
    /// </summary>
    public static List<string> FindGames()
    {
        var found = new List<string>();
        void Add(string d)
        {
            // a dump has its eboot; FMT's __FMTBackup (a copy of Data) doesn't
            if (!IsGame(d) || !File.Exists(Path.Combine(d, "eboot.bin"))) return;
            string full = Path.GetFullPath(d).TrimEnd('\\', '/');
            if (!found.Contains(full, StringComparer.OrdinalIgnoreCase)) found.Add(full);
        }
        IEnumerable<string> Dirs(string d)
        {
            try { return Directory.GetDirectories(d); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return Array.Empty<string>(); }
        }
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int up = 0; dir != null && up < 5; dir = dir.Parent, up++)
            foreach (string child in Dirs(dir.FullName))
            {
                Add(child);
                foreach (string grand in Dirs(child)) Add(grand);
            }
        return found.OrderBy(d => GameProfile.Detect(d).Kind == GameKind.Nhl27 ? 0 : 1).ThenBy(d => d, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool IsGame(string dir) => !string.IsNullOrWhiteSpace(dir) && File.Exists(Path.Combine(dir, "Data", "layout.toc"));
}
