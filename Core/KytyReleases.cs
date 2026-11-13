using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

namespace NHL27Kit.Core;

/// <summary>One GitHub release of the KytyPS5 NHL fork, with its Windows build.</summary>
public sealed record KytyRelease(string Repo, string Tag, DateTime Published, bool Prerelease, string Page, string AssetName, string AssetUrl, long Size)
{
    /// <summary>The folder the build unpacks to: the zip's name, as the builds next to the kit are named.</summary>
    public string FolderName => Path.GetFileNameWithoutExtension(AssetName);

    /// <summary>The commit in "KytyPS5-2026-10-28-e4d6616".</summary>
    public string Commit => Tag.Split('-') is { Length: > 1 } p ? p[^1] : Tag;

    /// <summary>The fork's owner, "pengui07" in "pengui07/KytyPS5-nhl".</summary>
    public string Owner => Repo.Split('/')[0];

    /// <summary>Who to name for the build: its creator where that isn't the repo owner, else the owner.</summary>
    public string Maker => KytyReleases.Makers.TryGetValue(Repo, out var m) ? m : Owner;
}

/// <summary>
/// Lists and installs the Windows builds of the KytyPS5 NHL forks in <see cref="Sources"/>. A build unpacks into
/// <see cref="BuildsDir"/> as KytyPS5-&lt;tag&gt;-Windows-x64, the folder the kit already looks in for emulators.
/// </summary>
public static class KytyReleases
{
    /// <summary>The forks whose releases are offered, as owner/name.</summary>
    public static readonly string[] Sources = { "pengui07/KytyPS5-nhl", "duy64/KytyPS5-nhl", "MatusRiedl/nhl27-pc-faceoff" };

    /// <summary>How a fork's builds are credited when that isn't the repo owner's name.</summary>
    public static readonly IReadOnlyDictionary<string, string> Makers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Faceoff (formerly nhl27-enhanced) is made by Iksperial
        ["MatusRiedl/nhl27-pc-faceoff"] = "Iksperial (Faceoff)",
    };

    /// <summary>The folder name patterns of builds next to the kit: KytyPS5-... and Faceoff-....</summary>
    public static readonly string[] FolderPatterns = { "KytyPS5-*", "Faceoff-*" };

    public static string ReleasesPage(string repo) => $"https://github.com/{repo}/releases";

    private static readonly HttpClient Http = MakeClient();

    private static HttpClient MakeClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        // GitHub's API refuses requests without a User-Agent
        http.DefaultRequestHeaders.UserAgent.ParseAdd("NHL27Kit");
        return http;
    }

    /// <summary>The folder holding the kit, where KytyPS5 builds sit side by side.</summary>
    public static string BuildsDir => Directory.GetParent(KitPaths.Root)?.FullName ?? KitPaths.Root;

    public static string EmulatorIn(string folder) => Path.Combine(folder, "kyty_emulator.exe");

    /// <summary>
    /// kyty_emulator.exe of a build folder, or null: at its top, or for Faceoff under builds\&lt;name&gt;\ (the newest
    /// one when its updater has added more than one).
    /// </summary>
    public static string FindEmulator(string folder)
    {
        if (folder == null || !Directory.Exists(folder)) return null;
        string top = EmulatorIn(folder);
        if (File.Exists(top)) return top;
        string builds = Path.Combine(folder, "builds");
        if (!Directory.Exists(builds)) return null;
        return Directory.GetDirectories(builds).Select(EmulatorIn).Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }

    /// <summary>The build folders next to the kit (or in <paramref name="dir"/>), newest name first.</summary>
    public static List<string> BuildFolders(string dir = null)
    {
        dir ??= BuildsDir;
        if (!Directory.Exists(dir)) return new();
        return FolderPatterns.SelectMany(p => Directory.GetDirectories(dir, p))
            .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// The name of the build an emulator belongs to: its folder, or for builds\&lt;name&gt;\kyty_emulator.exe the folder
    /// holding builds (so a Faceoff install reads "Faceoff-v0.10.0", not "rc6").
    /// </summary>
    public static string BuildName(string emulator)
    {
        if (emulator == null) return null;
        var dir = new DirectoryInfo(Path.GetDirectoryName(emulator));
        if (dir.Parent is { } p && p.Name.Equals("builds", StringComparison.OrdinalIgnoreCase) && p.Parent != null)
            return p.Parent.Name;
        return dir.Name;
    }

    /// <summary>
    /// The release's Windows build: a "-Windows-x64.zip", else (Faceoff and the nhl27-enhanced releases) the zip that
    /// isn't the source or another platform.
    /// </summary>
    private static JsonElement? PickAsset(JsonElement assets)
    {
        var zips = assets.EnumerateArray().Where(a => a.GetProperty("name").GetString() is { } n
            && n.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var a in zips)
            if (a.GetProperty("name").GetString().EndsWith("-Windows-x64.zip", StringComparison.OrdinalIgnoreCase)) return a;
        foreach (var a in zips)
        {
            string n = a.GetProperty("name").GetString().ToLowerInvariant();
            if (n.EndsWith("-src.zip") || n.Contains("source") || n.Contains("macos") || n.Contains("linux")) continue;
            return a;
        }
        return null;
    }

    /// <summary>
    /// Every source's releases with a Windows build, newest first, and a message per source that could not be read.
    /// A tag both forks publish is listed once (the folder name is the same).
    /// </summary>
    public static async Task<(List<KytyRelease> Releases, List<string> Errors)> ListAllAsync(CancellationToken ct = default)
    {
        var tasks = Sources.Select(repo => (repo, task: ListAsync(repo, ct))).ToList();
        var all = new List<KytyRelease>();
        var errors = new List<string>();
        foreach (var (repo, task) in tasks)
        {
            try { all.AddRange(await task); }
            catch (Exception ex) when (ex is not OperationCanceledException) { errors.Add($"{repo}: {ex.Message}"); }
        }
        var list = all.OrderByDescending(r => r.Published).DistinctBy(r => r.FolderName, StringComparer.OrdinalIgnoreCase).ToList();
        return (list, errors);
    }

    /// <summary>One fork's releases with a Windows build, newest first.</summary>
    public static async Task<List<KytyRelease>> ListAsync(string repo, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repo}/releases?per_page=30");
        req.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var res = await Http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"GitHub answered {(int)res.StatusCode} {res.ReasonPhrase}");
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var list = new List<KytyRelease>();
        foreach (var r in doc.RootElement.EnumerateArray())
        {
            if (r.TryGetProperty("draft", out var d) && d.GetBoolean()) continue;
            if (PickAsset(r.GetProperty("assets")) is { } a)
            {
                string name = a.GetProperty("name").GetString();
                list.Add(new KytyRelease(
                    repo,
                    r.GetProperty("tag_name").GetString(),
                    r.GetProperty("published_at").GetDateTime().ToLocalTime(),
                    r.GetProperty("prerelease").GetBoolean(),
                    r.GetProperty("html_url").GetString(),
                    name,
                    a.GetProperty("browser_download_url").GetString(),
                    a.GetProperty("size").GetInt64()));
            }
        }
        return list.OrderByDescending(r => r.Published).ToList();
    }

    /// <summary>kyty_emulator.exe of an installed release, or null.</summary>
    public static string Installed(KytyRelease r, string buildsDir = null)
    {
        return FindEmulator(Path.Combine(buildsDir ?? BuildsDir, r.FolderName));
    }

    /// <summary>
    /// Downloads the release's zip and unpacks it to its own folder (nothing existing is touched; the folder appears
    /// only once it is complete). Returns its kyty_emulator.exe. Progress is (bytes so far, total).
    /// </summary>
    public static async Task<string> InstallAsync(KytyRelease r, IProgress<(long Done, long Total)> progress = null,
                                                  string buildsDir = null, CancellationToken ct = default)
    {
        buildsDir ??= BuildsDir;
        string final = Path.Combine(buildsDir, r.FolderName);
        if (FindEmulator(final) is { } there)
            return there;
        if (Directory.Exists(final))
            throw new IOException($"{final} exists but has no kyty_emulator.exe; move it aside first");

        string zip = Path.Combine(buildsDir, $".{r.AssetName}.part");
        string staging = Path.Combine(buildsDir, $".{r.FolderName}.unpacking");
        try
        {
            using (var res = await Http.GetAsync(r.AssetUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                res.EnsureSuccessStatusCode();
                long total = res.Content.Headers.ContentLength ?? r.Size;
                await using var src = await res.Content.ReadAsStreamAsync(ct);
                await using var dst = new FileStream(zip, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true);
                var buf = new byte[1 << 16];
                long done = 0;
                int n;
                while ((n = await src.ReadAsync(buf, ct)) > 0)
                {
                    await dst.WriteAsync(buf.AsMemory(0, n), ct);
                    done += n;
                    progress?.Report((done, total));
                }
                if (done != total)
                    throw new IOException($"download ended at {done:N0} of {total:N0} bytes");
            }

            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            // ExtractToDirectory refuses entries that would land outside the folder
            await Task.Run(() => ZipFile.ExtractToDirectory(zip, staging), ct);
            string root = staging;
            // a zip with everything under one top folder unpacks that folder's contents
            if (FindEmulator(root) == null && Directory.GetFiles(root).Length == 0 && Directory.GetDirectories(root) is [var only])
                root = only;
            // the whole zip is kept: Faceoff's launcher expects its builds folder next to it
            if (FindEmulator(root) == null)
                throw new InvalidDataException($"{r.AssetName} has no kyty_emulator.exe (looked at its top and in builds)");
            Directory.Move(root, final);
            return FindEmulator(final);
        }
        finally
        {
            try { if (File.Exists(zip)) File.Delete(zip); } catch { /* left for the next try */ }
            try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { /* left for the next try */ }
        }
    }
}
