using NHL27Kit.Core;

namespace NHL27Kit.Cli;

public static partial class Program
{
    /// <summary>
    /// kit-alts-find --game NHL24 --game27 NHL27 [--all]: for each NHL team, NHL 27's adidas-style kit slots and their menu
    /// names next to the names NHL 24 already has for that team, marking the ones NHL 24 lacks (candidates for kit-alts).
    /// Reads only.
    /// </summary>
    private static int KitAltsFindCmd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        var i27 = AssetIndex.Open(KitPaths.Canonical(opts.Get("game27")) ?? opts.Get("game27"), File.ReadAllBytes,
            KitPaths.IndexFileFor(GameProfile.Detect(opts.Get("game27"))), _ => { });
        var leg24 = new Legacy(index); var leg27 = new Legacy(i27);
        var db24 = leg24.OpenDb(JerseySlots.Database); var db27 = leg27.OpenDb(JerseySlots.Database);
        var teams24 = JerseySlots.Teams(db24, Kits.Find(index));
        var teams27 = JerseySlots.Teams(db27, Kits.Find(i27));
        var n24 = KitNames.Read(leg24.OpenDb(KitNames.English)); var n27 = KitNames.Read(leg27.OpenDb(KitNames.English));
        var kits24 = Kits.Find(index);
        var specs = new List<string>(); var names = new List<string>();
        static string Norm(string s) => new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        foreach (var (key, t27) in teams27.Where(x => x.Key.League == "nhl").OrderBy(x => x.Key.Team))
        {
            if (!teams24.TryGetValue(key, out var t24)) { Console.WriteLine($"{key.Team}: not in NHL 24"); continue; }
            var s24 = JerseySlots.Read(db24, t24.TeamId);
            var names24 = s24.Select(s => n24.GetValueOrDefault(KitNames.Key(s.Style, s.ArtId, s.Variant))?.Text).Where(x => x != null).ToList();
            var used24 = kits24.Where(k => (k.League, k.Team) == key).Select(k => JerseySlots.Address(k)).Where(a => a != null).Select(a => a.Value.Variant).ToHashSet();
            Console.WriteLine($"{key.Team} (24 team {t24.TeamId} art {t24.ArtId}; 27 art {t27.ArtId}): NHL 24 has {string.Join(" | ", names24)}; folders {string.Join(",", used24.OrderBy(v => v))}");
            foreach (var s in JerseySlots.Read(db27, t27.TeamId))
            {
                string name = n27.GetValueOrDefault(KitNames.Key(s.Style, s.ArtId, s.Variant))?.Text ?? "(no name)";
                bool have = names24.Any(x => Norm(x) == Norm(name));
                if (have && !opts.Has("all")) continue;
                string word = s.Style == 2 && s.Variant < KitAlternates.Words.Length ? KitAlternates.Words[s.Variant] : $"style {s.Style} v{s.Variant}";
                Console.WriteLine($"   {(have ? "  " : "+ ")}{word,-12} {(s.IsLight ? "light" : "dark"),-5} label {s.Label,-3} \"{name}\"");
                // the batch: NHL 27's alternates (label 2) and its 2024-25 / 2025-26 kits, onto the matching Center Ice Fanatics kit
                bool want = !have && s.Style == 2 && t27.ArtId < 300 && (s.Label == 2 || (s.Label == 6 && (name.StartsWith("2024-25") || name.StartsWith("2025-26"))));
                if (!want) continue;
                // the Center Ice Fanatics kit first, then the team's other kits of the same shade, whose arrays hold NHL 27's layers
                var donor = s24.Where(x => x.Style == 2 && x.ArtId == t24.ArtId && x.IsLight == s.IsLight && used24.Contains(x.Variant) && x.Variant < KitAlternates.Words.Length)
                    .OrderBy(x => x.Variant is 24 or 25 ? 0 : x.Variant is 22 or 23 ? 1 : 2).ThenByDescending(x => x.Variant)
                    .FirstOrDefault(x => Fits(key.Team, KitAlternates.Words[x.Variant], word, out _) && KitAlternates.SameLength(KitAlternates.Words[x.Variant]).Any(v => !used24.Contains(v)));
                if (donor == null) { Fits(key.Team, s.IsLight ? "twentyfour" : "twentyfive", word, out var why); Console.WriteLine($"     no donor fits ({why})"); continue; }
                if (donor == null) { Console.WriteLine($"     no {(s.IsLight ? "light" : "dark")} Fanatics donor"); continue; }
                int to = KitAlternates.SameLength(KitAlternates.Words[donor.Variant]).FirstOrDefault(v => !used24.Contains(v) && !s24.Any(x => x.Style == 2 && x.Variant == v), -1);
                if (to < 0) { Console.WriteLine("     no free variant"); continue; }
                used24.Add(to);
                specs.Add($"--kit {key.Team}:{donor.Variant}:{to}:{(s.IsLight ? "light" : "dark")}:{word}");
                if (name == "(no name)") names.Add($"--name {key.Team}:{to}=2026-27 Alternate");
            }
        }
        bool Fits(string team, string word24, string word27, out string why)
        {
            why = null;
            string f24 = $"content/character/skater/league/nhl/{team}/adidas/{word24}/", f27 = $"content/character/skater/league/nhl/{team}/adidas/{word27}/";
            // only kits laid out like NHL 27's (base colour and branding arrays); older ones take none of NHL 27's textures
            if (index.GetRes($"{f24}jersey_adidas_{team}_{word24}_base_color") == null) { why = $"{word24} isn't laid out like NHL 27's kits"; return false; }
            foreach (var e in index.Res.Values.Where(e => e.Name.StartsWith(f24) && e.Name.EndsWith("_array")).ToList())
            {
                string n27 = f27 + e.Name[f24.Length..].Replace($"_{team}_{word24}", $"_{team}_{word27}");
                if (i27.GetRes(n27) == null) continue;
                int l24, l27;
                try { l24 = Math.Max(1, Texture.Load(index, e.Name, decode: false).Header.Layers); l27 = Math.Max(1, Texture.Load(i27, n27, decode: false).Header.Layers); }
                catch { continue; }
                if (l27 > l24 && KitConvert27.BrandHotspots(i27, n27) is var used && used >= 0 && used <= l24) l27 = l24;
                if (l27 > l24) { why = $"{Path.GetFileName(n27)} {l27} layers, {word24} {l24}"; return false; }
                if (l27 < l24 && e.Name.EndsWith("_color_array"))
                {
                    var t = Texture.Load(index, e.Name, decode: false);
                    bool blank = false;
                    for (int l = 0; l < l24 && !blank; l++)
                    {
                        t.Layer = l; t.DecodeMip(Math.Max(0, t.FirstResidentMip));
                        bool empty = true;
                        for (int i = 3; i < t.Rgba.Length && empty; i += 4) empty = t.Rgba[i] == 0;
                        blank = empty;
                    }
                    if (!blank) { why = $"{Path.GetFileName(e.Name)} has no blank layer"; return false; }
                }
            }
            return true;
        }
        if (opts.Get("specs") is { } file) File.WriteAllLines(file, specs.Concat(names));
        Console.WriteLine($"{specs.Count} kit(s), {names.Count} named here");
        return 0;
    }
}
