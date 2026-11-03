using System.IO;
using System.Windows;
using Microsoft.Win32;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>
/// Legacy jersey converter: a jersey texture made for the older NHL (legacy) template is converted to NHL 27's jersey
/// layout (<see cref="LegacyJersey"/>), previewed on the shown kit, and written over the kit's jersey color and base
/// color on request. The kit's own stamped jersey logos (crest, patches, maker's logo) can be blanked at the same time,
/// since the legacy art carries its own.
/// </summary>
public partial class UniformsPage
{
    /// <summary>The converted PNG being previewed (kept in the export folder: the mod entry points at it).</summary>
    private string _legacyFile;

    private async void OnLegacy(object sender, RoutedEventArgs e)
    {
        if (_kit == null) return;
        var dlg = new OpenFileDialog { Title = "Legacy jersey texture", Filter = ImageLoader.Filter };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        await ConvertLegacy(dlg.FileName);
    }

    /// <summary>Converts a legacy jersey and shows it on the kit, without writing anything.</summary>
    public async Task ConvertLegacy(string file)
    {
        if (_kit == null) return;
        if (_kit.Slot("jersey_color") == null && _kit.Slot("jersey_base_color") == null)
        {
            StatusChanged?.Invoke($"{_kit} has no jersey texture to convert onto.");
            return;
        }
        var kit = _kit;
        StatusChanged?.Invoke($"Converting {Path.GetFileName(file)} to the NHL 27 jersey layout…");
        try
        {
            string folder = Path.Combine(KitPaths.Export, "uniforms", kit.League, kit.Team);
            Directory.CreateDirectory(folder);
            string name = Path.GetFileName(file);
            while (Path.HasExtension(name)) name = Path.GetFileNameWithoutExtension(name);
            string outFile = Path.Combine(folder, $"{kit.Name}_legacy_{name}.png");
            var rgba = await Task.Run(() =>
            {
                var (src, w, h) = ImageLoader.Load(file);
                var result = LegacyJersey.Convert(src, w, h, TextureSize);
                File.WriteAllBytes(outFile, ImageFiles.Png(result, TextureSize, TextureSize));
                return result;
            });
            if (kit != _kit || _shown == null) return;
            _legacyFile = outFile;
            var tex = TextureService.ToBitmap(rgba, TextureSize, TextureSize, Channel.Rgb);
            Viewer.Show(_shown.Select(p => (p.Part, p.Garment == "jersey" ? tex : p.Tex)));
            LegacyTitle.Text = $"Legacy preview: {name}";
            LegacyTitle.ToolTip = $"{Path.GetFileName(file)}, converted to the NHL 27 layout, shown on {UniformNames.Pretty(kit.Team)} {UniformNames.Pretty(kit.Name)}";
            LegacyBar.Visibility = Visibility.Visible;
            StatusChanged?.Invoke($"Converted {Path.GetFileName(file)} to the NHL 27 jersey layout (preview; saved as {outFile})");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"Can't convert {Path.GetFileName(file)}: {ex.Message}");
        }
    }

    private void OnLegacySave(object sender, RoutedEventArgs e)
    {
        if (_legacyFile == null) return;
        var dlg = new SaveFileDialog
        {
            Title = "Save the converted jersey",
            Filter = "PNG image|*.png",
            InitialDirectory = Path.GetDirectoryName(_legacyFile),
            FileName = Path.GetFileName(_legacyFile),
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        if (!string.Equals(Path.GetFullPath(dlg.FileName), Path.GetFullPath(_legacyFile), StringComparison.OrdinalIgnoreCase))
            File.Copy(_legacyFile, dlg.FileName, true);
        StatusChanged?.Invoke($"Saved the converted jersey to {dlg.FileName}");
    }

    private void OnLegacyDiscard(object sender, RoutedEventArgs e)
    {
        EndLegacy();
        if (_shown != null) Viewer.Show(_shown.Select(p => (p.Part, p.Tex)));
    }

    private async void OnLegacyWrite(object sender, RoutedEventArgs e) => await WriteLegacy(LegacyHideLogos.IsChecked == true);

    /// <summary>Writes the previewed jersey over the kit's jersey color and base color, and optionally blanks its jersey logos.</summary>
    public async Task WriteLegacy(bool hideLogos)
    {
        if (_legacyFile == null || _kit == null) return;
        var items = new[] { "jersey_color", "jersey_base_color" }
            .Select(s => _kit.Slot(s)).Where(t => t != null).Select(t => (t, _legacyFile)).ToList();
        int logos = 0;
        if (hideLogos)
        {
            string blank = Path.Combine(KitPaths.Export, "uniforms", "blank_logo.png");
            Directory.CreateDirectory(Path.GetDirectoryName(blank));
            if (!File.Exists(blank)) File.WriteAllBytes(blank, ImageFiles.Png(new byte[64 * 64 * 4], 64, 64));
            var brands = _kit.Textures.Where(t => System.Text.RegularExpressions.Regex.IsMatch(t.Slot, @"^branding_jersey_\d+_color$")).ToList();
            items.AddRange(brands.Select(t => (t, blank)));
            logos = brands.Count;
        }
        string label = $"{UniformNames.Pretty(_kit.Team)} {UniformNames.Pretty(_kit.Name)}";
        string summary = $"Legacy jersey written into {label}: jersey color and base color" + (logos > 0 ? $", {logos} jersey logos blanked" : "") + ". Restore on the Mods page undoes it.";
        if (await ReplaceAll(items, summary)) EndLegacy();
    }

    private void EndLegacy()
    {
        _legacyFile = null;
        LegacyBar.Visibility = Visibility.Collapsed;
    }
}
