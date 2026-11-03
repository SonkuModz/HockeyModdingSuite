namespace NHL27Kit.Core;

/// <summary>
/// Where the kit keeps its own files: the kit folder (the one with NHL27Kit.slnx, found above the running program)
/// when there is one, else %LOCALAPPDATA%\NHL27Kit. The game folder defaults to "NHL 27 dump/NHL" next to it.
/// </summary>
public static class KitPaths
{
    public static string Root { get; } = FindRoot();

    public static string Cache => Path.Combine(Root, "cache");
    public static string IndexFile => Path.Combine(Cache, "index.bin");
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

    public static bool IsGame(string dir) => !string.IsNullOrWhiteSpace(dir) && File.Exists(Path.Combine(dir, "Data", "layout.toc"));
}
