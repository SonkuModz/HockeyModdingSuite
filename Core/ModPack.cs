using System.IO.Compression;
using System.Text.Json;

namespace NHL27Kit.Core;

/// <summary>One replacement in a pack: what it replaces and the file (in the pack) it is rebuilt from.</summary>
public sealed class PackItem
{
    /// <summary>texture, sound (Name is "wave#variation") or db (File is the whole edited database).</summary>
    public string Kind { get; set; }
    public string Name { get; set; }
    public string File { get; set; }
    public string Summary { get; set; }
}

public sealed class PackManifest
{
    public int Format { get; set; } = 1;
    public string Title { get; set; }
    public DateTime Created { get; set; } = DateTime.Now;
    public List<PackItem> Items { get; set; } = new();
}

/// <summary>
/// A .nhl27mod pack: a zip of manifest.json and the sources of each replacement (images, audio, edited databases).
/// Nothing in it is a byte patch: importing rebuilds every replacement against the game it is imported into, so a pack
/// keeps working when the game's data moves.
/// </summary>
public static class ModPack
{
    public const string Extension = ".nhl27mod";
    public const string Filter = "HMS mod pack|*.nhl27mod";

    /// <summary>
    /// Writes a pack of these mods. <paramref name="dbBytes"/> gives a database's current bytes in the game (its mod must
    /// be applied); a texture or sound needs its source file. Returns notes on what was left out.
    /// </summary>
    public static List<string> Export(string file, string title, IEnumerable<ModEntry> entries, Func<ModEntry, byte[]> dbBytes)
    {
        var notes = new List<string>();
        var manifest = new PackManifest { Title = title };
        string tmp = file + ".tmp";
        using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
        {
            int n = 0;
            foreach (var e in entries)
            {
                n++;
                if (e.Kind == DbEditor.Kind)
                {
                    byte[] db;
                    try { db = dbBytes(e); }
                    catch (Exception ex) { notes.Add($"{e.Name}: left out ({ex.Message})"); continue; }
                    string path = $"items/{n:000}.db";
                    using (var s = zip.CreateEntry(path, CompressionLevel.Optimal).Open()) s.Write(db);
                    manifest.Items.Add(new PackItem { Kind = e.Kind, Name = e.Name, File = path, Summary = e.Summary });
                    continue;
                }
                if (string.IsNullOrEmpty(e.Source) || !System.IO.File.Exists(e.Source))
                {
                    notes.Add($"{e.Name}: left out (its source file {e.Source} is gone)");
                    continue;
                }
                string entryPath = $"items/{n:000}{Path.GetExtension(e.Source).ToLowerInvariant()}";
                zip.CreateEntryFromFile(e.Source, entryPath, CompressionLevel.Optimal);
                manifest.Items.Add(new PackItem { Kind = e.Kind, Name = e.Name, File = entryPath, Summary = e.Summary });
            }
            using var m = zip.CreateEntry("manifest.json").Open();
            JsonSerializer.Serialize(m, manifest, new JsonSerializerOptions { WriteIndented = true });
        }
        System.IO.File.Move(tmp, file, overwrite: true);
        return notes;
    }

    /// <summary>Reads a pack and copies its sources into <paramref name="sourcesDir"/>; each item's File becomes that path.</summary>
    public static PackManifest Open(string file, string sourcesDir)
    {
        using var zip = ZipFile.OpenRead(file);
        var me = zip.GetEntry("manifest.json") ?? throw new InvalidDataException("Not a mod pack (no manifest.json).");
        PackManifest manifest;
        using (var s = me.Open()) manifest = JsonSerializer.Deserialize<PackManifest>(s) ?? throw new InvalidDataException("The pack's manifest is empty.");
        if (manifest.Format != 1) throw new NotSupportedException($"Pack format {manifest.Format} is newer than this kit.");
        Directory.CreateDirectory(sourcesDir);
        string id = Guid.NewGuid().ToString("N")[..8];
        foreach (var item in manifest.Items)
        {
            var e = zip.GetEntry(item.File) ?? throw new InvalidDataException($"The pack lacks {item.File}.");
            string dst = Path.Combine(sourcesDir, $"{id}_{Path.GetFileName(item.File)}");
            e.ExtractToFile(dst, overwrite: true);
            item.File = dst;
        }
        return manifest;
    }
}
