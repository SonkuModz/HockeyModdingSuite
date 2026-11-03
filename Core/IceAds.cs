using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NHL27Kit.Core;

/// <summary>
/// The ads on the ice around centre ice (the game's CenterAd1..4 hotspots, <see cref="CenterIce.CenterAds"/>).
/// - The game's: each arena's ids are in gamemodes/debug/scratch/arena/NAME_n1_ID.arena (ArenaHotSpot), and id N is the
///   texture ice_hotspot_1_N_color (512×256; ice_hotspot_0_N is the square kind). Every NHL arena has the same four:
///   191 Discover, 126 NHL App, 195 NHL EDGE, 2 EA Sports.
/// - A team's own: cut from its current rink on TheFaceoff.net ("Toronto Maple Leafs 2027"), NHL only.
/// </summary>
public static class IceAds
{
    private const string Hotspots = "content/worlds/rink/textures/hotspot/ice_hotspot_";
    private static readonly int[] NhlDefault = { 191, 126, 195, 2 };

    /// <summary>The game's CenterAd1..4 ids for an arena (the NHL's four when its file isn't found).</summary>
    public static int[] GameCenterAds(AssetIndex index, int arenaId)
    {
        try
        {
            var legacy = new Legacy(index);
            string name = legacy.Names.FirstOrDefault(n => n.StartsWith("gamemodes/debug/scratch/arena/", StringComparison.OrdinalIgnoreCase)
                                                           && n.EndsWith($"_n1_{arenaId}.arena", StringComparison.OrdinalIgnoreCase));
            if (name == null) return NhlDefault;
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(legacy.Read(name)).TrimStart('﻿'));
            var spot = doc.RootElement.GetProperty("ArenaHotSpot");
            return Enumerable.Range(1, 4).Select(i => spot.TryGetProperty($"CenterAd{i}", out var v) ? v.GetInt32() : 0).ToArray();
        }
        catch { return NhlDefault; }
    }

    /// <summary>Hotspot <paramref name="id"/>'s texture, the 2:1 kind first, or null.</summary>
    public static string Texture(AssetIndex index, int id) =>
        new[] { $"{Hotspots}1_{id}_color", $"{Hotspots}0_{id}_color" }.FirstOrDefault(n => index.GetEbx(n) != null);

    /// <summary>The NHL rinks read from TheFaceoff.net this session (the list is paged, newest first).</summary>
    private static readonly FaceoffLeague Nhl = new() { Key = "nhl", Label = "NHL" };
    private static readonly SemaphoreSlim Paging = new(1, 1);

    /// <summary>
    /// A team's current game-night rink on TheFaceoff.net: the newest named "TEAM YEAR" (not a practice rink, outdoor game
    /// or event). Reads more pages of the list until it's found.
    /// </summary>
    public static async Task<FaceoffRink> FindRinkAsync(string team, Action<string> progress = null, CancellationToken ct = default)
    {
        var name = new Regex($@"(^|\s){Regex.Escape(team.Trim())}\s+\d{{4}}$", RegexOptions.IgnoreCase);
        await Paging.WaitAsync(ct);
        try
        {
            for (int i = 0; i < 60; i++)
            {
                var rink = Nhl.Rinks.FirstOrDefault(r => name.IsMatch(r.Name.Trim()));
                if (rink != null || Nhl.Done) return rink;
                progress?.Invoke($"Finding {team} on TheFaceoff.net… page {Nhl.Pages + 1}");
                await FaceoffNet.MoreAsync(Nhl, ct);
            }
            return null;
        }
        finally { Paging.Release(); }
    }
}
