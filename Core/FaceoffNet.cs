using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace NHL27Kit.Core;

/// <summary>A rink in TheFaceoff.net's archive: its title, its page, and the picture its list shows (the centre-ice close-up).</summary>
public sealed record FaceoffRink(string Name, string Url, string ImageId)
{
    public string Thumb(int size) => FaceoffNet.Thumb(ImageId, size);
}

/// <summary>One league's list of rinks on TheFaceoff.net, read a page (8 rinks) at a time, newest first.</summary>
public sealed class FaceoffLeague
{
    public string Key { get; init; }
    public string Label { get; init; }
    /// <summary>The list's pager (comp-…), read from its first page.</summary>
    public string Pager { get; set; }
    public int Pages { get; set; }
    public bool Done { get; set; }
    public List<FaceoffRink> Rinks { get; } = new();
}

/// <summary>
/// Reads TheFaceoff.net (a Wix site; every page is rendered on the server, so no browser is needed):
/// - a league's list (/ice-nhl, paged with ?&lt;pager&gt;_page=N) gives each rink's title, page and centre-ice picture;
/// - a rink's page lists its pictures with their sizes; the first 5000×2236 one is that season's top-down rink layout.
/// Pictures come from static.wixstatic.com: the bare media URL is the full-size original, /v1/fill/… a resized copy.
/// The site allows its graphics for personal use with credit; downloads are kept in export/faceoff, nothing is shipped.
/// </summary>
public static class FaceoffNet
{
    public const string Site = "https://www.thefaceoff.net";
    private const string Media = "https://static.wixstatic.com/media/84911f_";

    public static readonly (string Key, string Label)[] Leagues =
    {
        ("nhl", "NHL"), ("ahl", "AHL"), ("echl", "ECHL"), ("sphl", "SPHL"), ("fphl", "FPHL"), ("ushl", "USHL"), ("chl", "CHL"),
        ("bchl", "BCHL"), ("ncaa", "NCAA"), ("pwhl", "PWHL"), ("iihf", "IIHF"), ("olympics", "Olympics"), ("3ice", "3ICE"),
        ("phf", "PHF"), ("cehl", "CeHL"), ("ihl", "IHL"), ("wha", "WHA"), ("plus", "Faceoff Plus"),
    };

    /// <summary>The site's league for the game's league name (the NHL when nothing fits).</summary>
    public static string LeagueKey(string league)
    {
        string l = (league ?? "").ToLowerInvariant();
        return Leagues.FirstOrDefault(x => l.Contains(x.Key)).Key ?? "nhl";
    }

    /// <summary>A failed request in plain words: a refusal says what to do instead.</summary>
    public static string Explain(Exception ex) => ex is HttpRequestException { StatusCode: HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests }
        ? "TheFaceoff.net is refusing the kit's requests right now. Try again later, or download the rink's picture yourself and use the team's Centre ice tab."
        : "Can't read TheFaceoff.net: " + ex.Message;

    private static readonly HttpClient Http = MakeClient();

    private static HttpClient MakeClient()
    {
        var http = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) NHL27Kit");
        return http;
    }

    private static readonly Regex Entry = new(
        @"title=""([^""]+)""><a data-testid=""linkElement"" href=""(https://www\.thefaceoff\.net/center-ice/[0-9a-f-]+)""(.{0,1500}?)84911f_([0-9a-f]{32}~mv2\.\w+)",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex Picture = new(@"""width"":(\d+),""height"":(\d+),""uri"":""84911f_([0-9a-f]{32}~mv2\.\w+)""", RegexOptions.Compiled);

    public static string Thumb(string id, int size) => $"{Media}{id}/v1/fill/w_{size},h_{size},al_c,q_85/thumb.jpg";
    public static string Full(string id) => Media + id;

    /// <summary>Reads the next page of a league's list; returns the rinks it added.</summary>
    public static async Task<List<FaceoffRink>> MoreAsync(FaceoffLeague league, CancellationToken ct = default)
    {
        if (league.Done) return new List<FaceoffRink>();
        int page = league.Pages + 1;
        string url = page == 1 || league.Pager == null ? $"{Site}/ice-{league.Key}" : $"{Site}/ice-{league.Key}?{league.Pager}_page={page}";
        string html = await GetStringAsync(url, ct);
        league.Pager ??= Regex.Match(html, @"(comp-[a-z0-9]+)_page=2").Groups[1].Value is { Length: > 0 } p ? p : null;
        var added = new List<FaceoffRink>();
        foreach (Match m in Entry.Matches(html))
        {
            string name = WebUtility.HtmlDecode(m.Groups[1].Value), link = m.Groups[2].Value;
            if (league.Rinks.Any(r => r.Url == link)) continue;
            var rink = new FaceoffRink(name, link, m.Groups[4].Value);
            league.Rinks.Add(rink);
            added.Add(rink);
        }
        league.Pages = page;
        league.Done = league.Pager == null || !html.Contains($"{league.Pager}_page={page + 1}");
        return added;
    }

    /// <summary>A rink page's title and its top-down layout picture (null when it has none).</summary>
    public static async Task<(string Title, string LayoutId)> RinkAsync(string url, CancellationToken ct = default)
    {
        string html = WebUtility.HtmlDecode(await GetStringAsync(url, ct));
        string title = Regex.Match(html, @"<title>([^<]*)</title>").Groups[1].Value;
        title = Regex.Replace(title, @"\s*(Center Ice and Rink Layout)?\s*\|\s*TheFaceoff\.net\s*$", "").Trim();
        foreach (Match m in Picture.Matches(html))
        {
            double w = double.Parse(m.Groups[1].Value), h = double.Parse(m.Groups[2].Value);
            if (w >= 2000 && Math.Abs(w / h - 5000.0 / 2236) < 0.02) return (title, m.Groups[3].Value);
        }
        return (title, null);
    }

    /// <summary>A full-size picture, kept in <paramref name="folder"/> after the first download.</summary>
    public static async Task<string> DownloadAsync(string id, string folder, CancellationToken ct = default)
    {
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, id.Replace('~', '_'));
        if (File.Exists(file) && new FileInfo(file).Length > 0) return file;
        var bytes = await GetBytesAsync(Full(id), ct);
        string tmp = file + ".part";
        await File.WriteAllBytesAsync(tmp, bytes, ct);
        File.Move(tmp, file, true);
        return file;
    }

    public static Task<byte[]> GetBytesAsync(string url, CancellationToken ct = default) => Retry(() => Http.GetByteArrayAsync(url, ct));

    private static Task<string> GetStringAsync(string url, CancellationToken ct) => Retry(() => Http.GetStringAsync(url, ct));

    private static readonly SemaphoreSlim Pace = new(1, 1);
    private static DateTime last = DateTime.MinValue;
    private static readonly TimeSpan Gap = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// One request at a time, at most one every 1.5 s, so the site isn't flooded. The CDN resets a connection now and
    /// then: a few tries. A refusal (403/429) isn't tried again.
    /// </summary>
    private static async Task<T> Retry<T>(Func<Task<T>> get)
    {
        for (int i = 0; ; i++)
        {
            await Pace.WaitAsync();
            try
            {
                var wait = last + Gap - DateTime.UtcNow;
                if (wait > TimeSpan.Zero) await Task.Delay(wait);
                try { return await get(); }
                finally { last = DateTime.UtcNow; }
            }
            catch (HttpRequestException ex) when (i < 3 && ex.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests))
            {
                await Task.Delay(400 * (i + 1));
            }
            finally { Pace.Release(); }
        }
    }
}
