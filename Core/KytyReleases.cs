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
}

/// <summary>
/// Lists and installs the Windows builds of the KytyPS5 NHL forks in <see cref="Sources"/>. A build unpacks into
/// <see cref="BuildsDir"/> as KytyPS5-&lt;tag&gt;-Windows-x64, the folder the kit already looks in for emulators.
/// </summary>
public static class KytyReleases
{
    /// <summary>The forks whose releases are offered, as owner/name.</summary>
    public static readonly string[] Sources = { "pengui07/KytyPS5-nhl", "duy64/KytyPS5-nhl" };

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
            foreach (var a in r.GetProperty("assets").EnumerateArray())
            {
                string name = a.GetProperty("name").GetString();
                if (!name.EndsWith("-Windows-x64.zip", StringComparison.OrdinalIgnoreCase)) continue;
                list.Add(new KytyRelease(
                    repo,
                    r.GetProperty("tag_name").GetString(),
                    r.GetProperty("published_at").GetDateTime().ToLocalTime(),
                    r.GetProperty("prerelease").GetBoolean(),
                    r.GetProperty("html_url").GetString(),
                    name,
                    a.GetProperty("browser_download_url").GetString(),
                    a.GetProperty("size").GetInt64()));
                break;
            }
        }
        return list.OrderByDescending(r => r.Published).ToList();
    }

    /// <summary>kyty_emulator.exe of an installed release, or null.</summary>
    public static string Installed(KytyRelease r, string buildsDir = null)
    {
        string exe = EmulatorIn(Path.Combine(buildsDir ?? BuildsDir, r.FolderName));
        return File.Exists(exe) ? exe : null;
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
        if (File.Exists(EmulatorIn(final)))
            return EmulatorIn(final);
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
            if (!File.Exists(EmulatorIn(root)) && Directory.GetFiles(root).Length == 0 && Directory.GetDirectories(root) is [var only])
                root = only;
            if (!File.Exists(EmulatorIn(root)))
                throw new InvalidDataException($"{r.AssetName} has no kyty_emulator.exe");
            Directory.Move(root, final);
            return EmulatorIn(final);
        }
        finally
        {
            try { if (File.Exists(zip)) File.Delete(zip); } catch { /* left for the next try */ }
            try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { /* left for the next try */ }
        }
    }
}
