namespace NHL27Kit.Core;

/// <summary>
/// The game folder's layout (Data/layout.toc): the superbundles and the install chunks ("catalogs"), whose folder
/// names the CAS archive paths: Data/&lt;installBundle&gt;/cas_NN.cas (Patch/... for patched data). PS5 TOCs name an
/// install chunk by its persistentIndex; PS4 TOCs by its position in the list.
/// </summary>
public sealed class GameLayout
{
    public string GameDir { get; }
    public string DataDir { get; }
    public string PatchDir { get; }
    public string Platform { get; }
    public IReadOnlyList<string> SuperBundles { get; }
    /// <summary>Catalog index -> folder below Data (e.g. "Ps5/superbundlelayout/nhl_installpackage_04").</summary>
    public IReadOnlyList<string> Catalogs { get; }

    private readonly Dictionary<int, int> _byPersistentIndex = new();
    /// <summary>Superbundle -> TOCs split into install chunks (the language TOCs: Ps5/loc/en -> .../00/en/loc/en.toc).</summary>
    private readonly Dictionary<string, List<string>> _split = new(StringComparer.OrdinalIgnoreCase);

    public GameLayout(string gameDir)
    {
        GameDir = Path.GetFullPath(gameDir).TrimEnd('\\', '/');
        DataDir = Path.Combine(GameDir, "Data");
        PatchDir = Path.Combine(GameDir, "Patch");
        var layout = DbObject.ReadFile(Path.Combine(DataDir, "layout.toc"));
        SuperBundles = ((List<object>)layout["superBundles"]).Cast<Dictionary<string, object>>()
            .Select(o => (string)o["name"]).ToList();
        var catalogs = new List<string>();
        if (layout.TryGetValue("installManifest", out var im) && im is Dictionary<string, object> manifest)
        {
            Platform = manifest.TryGetValue("platform", out var pf) ? pf as string : null;
            foreach (Dictionary<string, object> chunk in (List<object>)manifest["installChunks"])
            {
                if (chunk.TryGetValue("testDLC", out var t) && t is true)
                    continue;
                string folder = chunk.TryGetValue("installBundle", out var ib) ? (string)ib : (Platform ?? "win32") + "/" + (string)chunk["name"];
                if (chunk.TryGetValue("persistentIndex", out var pi) && pi is int index)
                    _byPersistentIndex[index] = catalogs.Count;
                if (chunk.TryGetValue("splitSuperbundles", out var ss) && ss is List<object> splits)
                    foreach (Dictionary<string, object> s in splits)
                    {
                        string sb = (string)s["superbundle"];
                        string rel = sb.StartsWith(Platform + "/", StringComparison.OrdinalIgnoreCase) ? sb[(Platform.Length + 1)..] : sb;
                        if (!_split.TryGetValue(sb, out var list))
                            _split[sb] = list = new List<string>();
                        list.Add(folder + "/" + rel);
                    }
                catalogs.Add(folder);
            }
        }
        Catalogs = catalogs;
    }

    public bool IsPs5 => string.Equals(Platform, "Ps5", StringComparison.OrdinalIgnoreCase);

    /// <summary>The catalog index of the install chunk with this persistentIndex, or -1.</summary>
    public int CatalogOf(int persistentIndex) => _byPersistentIndex.GetValueOrDefault(persistentIndex, -1);

    /// <summary>The superbundle's TOC in Data.</summary>
    public string TocPath(string superBundle) => Path.Combine(DataDir, Rel(superBundle) + ".toc");

    /// <summary>
    /// The TOCs the game reads for a superbundle: Patch's copy when there is one, else Data's, plus any split TOCs
    /// (each likewise from Patch when patched). Only TOCs that exist.
    /// </summary>
    public IEnumerable<string> LiveTocPaths(string superBundle)
    {
        var rels = new List<string> { superBundle };
        if (_split.TryGetValue(superBundle, out var split))
            rels.AddRange(split);
        foreach (string rel in rels)
        {
            string patched = Path.Combine(PatchDir, Rel(rel) + ".toc");
            string data = Path.Combine(DataDir, Rel(rel) + ".toc");
            if (File.Exists(patched)) yield return patched;
            else if (File.Exists(data)) yield return data;
        }
    }

    /// <summary>
    /// Every TOC of every superbundle in the order the game layers them: all of Data's (with split TOCs), then all of
    /// Patch's, whose bundles and chunks replace Data's.
    /// </summary>
    public List<string> AllTocPaths()
    {
        var rels = new List<string>();
        foreach (string sb in SuperBundles)
        {
            rels.Add(sb);
            if (_split.TryGetValue(sb, out var split))
                rels.AddRange(split);
        }
        return rels.Select(r => Path.Combine(DataDir, Rel(r) + ".toc"))
            .Concat(rels.Select(r => Path.Combine(PatchDir, Rel(r) + ".toc")))
            .Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public bool IsPatchPath(string path) => path.StartsWith(PatchDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    public string CasPath(int catalog, int cas, bool patch = false)
    {
        if (catalog < 0 || catalog >= Catalogs.Count)
            throw new InvalidDataException($"TOC names install chunk {catalog}, which the layout does not have.");
        return Path.Combine(patch ? PatchDir : DataDir, Rel(Catalogs[catalog]), $"cas_{cas:D2}.cas");
    }

    private static string Rel(string path) => path.Replace('/', Path.DirectorySeparatorChar);
}
