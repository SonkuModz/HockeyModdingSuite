namespace NHL27Kit.Core;

/// <summary>
/// A guide to a kit's jersey texture, to paint a design over in an image editor and bring back with "Use an image": each
/// part of the jersey in its own colour (front, back, sleeves, and the fabric regions of the base's alpha), lines every
/// 10 cm up from the hem and the cuffs, and the stamp's logo and lettering spots, labelled in the game's own lettering.
/// </summary>
public static class JerseyTemplate
{
    public enum Part { None, Front, Back, RightSleeve, LeftSleeve, Yoke, Gussets, Collar, CollarSides, Neck }

    private static readonly Dictionary<Part, (int Color, string Label)> Parts = new()
    {
        [Part.Front] = (0x8FB8E8, "FRONT"),
        [Part.Back] = (0xE8C08F, "BACK"),
        [Part.RightSleeve] = (0x9FD8A8, "RIGHT SLEEVE"),
        [Part.LeftSleeve] = (0xD8A8D8, "LEFT SLEEVE"),
        [Part.Yoke] = (0xE8E08F, "YOKE"),
        [Part.Gussets] = (0xB0B0B8, "GUSSET"),
        [Part.Collar] = (0xE89090, "SIDE"),
        [Part.CollarSides] = (0x90D8D8, "COLLAR"),
        [Part.Neck] = (0xF0A0B8, "NECK"),
    };

    private const int Outside = 0x24272D, Hatch = 0x30343B, Seam = 0x1A1C20, Guide = 0x5A6270, SpotBox = 0xC8102E, LettersBox = 0x6A3FC8;

    /// <summary>The template at <paramref name="size"/>², opaque RGBA. The kit must have a stamped jersey and a readable jersey model.</summary>
    public static byte[] Build(AssetIndex index, Kit kit, int size)
    {
        var src = JerseyStudio.LoadSource(index, kit, size);
        var shape = src.Shape ?? throw new InvalidDataException($"{kit}'s jersey model can't be read, so its parts can't be found.");
        var map = shape.Map;
        var pos = map.Position;
        int n = size * size;

        // which way the model faces and which side is its right, from where the stamp puts the crest and the right shoulder logo
        var brands = KitStamp.Brands(src.Spots);
        float At(StampHotspot s, int axis)
        {
            int cx = (int)(s.Tx * size), cy = (int)((1 - s.Ty) * size);
            for (int r = 0; r < size / 16; r++)
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int x = cx + dx, y = cy + dy;
                if (x < 0 || y < 0 || x >= size || y >= size) continue;
                float v = pos[(y * size + x) * 3];
                if (!float.IsNaN(v)) return pos[(y * size + x) * 3 + axis];
            }
            return float.NaN;
        }
        var crest = brands.FirstOrDefault(b => OwnKit.Role(b.Spot, b.Layer) == OwnKit.LogoRole.Main).Spot;
        float frontZ = crest != null ? At(crest, 2) : float.NaN;
        int front = float.IsNaN(frontZ) || frontZ >= 0 ? 1 : -1;
        var rightLogo = brands.FirstOrDefault(b => b.Spot.Name.Contains("right", StringComparison.OrdinalIgnoreCase)).Spot;
        float rightX = rightLogo != null ? At(rightLogo, 0) : float.NaN;
        // facing +z, a body's right is at -x
        int right = !float.IsNaN(rightX) ? Math.Sign(rightX) : -front;

        var parts = new Part[n];
        var cm = new double[n];
        for (int i = 0; i < n; i++)
        {
            float x = pos[i * 3];
            if (float.IsNaN(x)) continue;
            double y = pos[i * 3 + 1], z = pos[i * 3 + 2];
            bool sleeve = shape.OnSleeve(x, y);
            parts[i] = JerseyStudio.Region(src.Base[i * 4 + 3]) switch
            {
                2 => Part.Gussets,
                3 => Part.Yoke,
                4 => Part.Collar,
                5 => Part.CollarSides,
                _ when sleeve => Math.Sign(x) == right ? Part.RightSleeve : Part.LeftSleeve,
                _ => z * front >= 0 ? Part.Front : Part.Back,
            };
            cm[i] = sleeve ? (shape.Cuff - shape.Along(x, y)) * 100 : (y - shape.Hem) * 100;
        }

        // the neck band: a piece of its own (not joined to the body in the texture) at the top of the model
        var seen = new bool[n];
        var queue = new Queue<int>();
        var piece = new List<int>();
        for (int start = 0; start < n; start++)
        {
            if (seen[start] || parts[start] == Part.None) continue;
            piece.Clear();
            float top = float.MinValue;
            seen[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                piece.Add(i);
                top = Math.Max(top, pos[i * 3 + 1]);
                int x = i % size;
                foreach (int j in new[] { x > 0 ? i - 1 : -1, x < size - 1 ? i + 1 : -1, i - size, i + size })
                    if (j >= 0 && j < n && !seen[j] && parts[j] != Part.None) { seen[j] = true; queue.Enqueue(j); }
            }
            if (piece.Count < n / 20 && top > map.Bounds[4] - 0.05)
                foreach (int i in piece) parts[i] = Part.Neck;
        }

        var img = new byte[n * 4];
        void Set(int i, int c) { img[i * 4] = (byte)(c >> 16); img[i * 4 + 1] = (byte)(c >> 8); img[i * 4 + 2] = (byte)c; img[i * 4 + 3] = 255; }
        int line = Math.Max(1, size / 1024);
        Parallel.For(0, size, y =>
        {
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                var p = parts[i];
                if (p == Part.None) { Set(i, (x + y) / Math.Max(4, size / 128) % 2 == 0 ? Outside : Hatch); continue; }
                bool seam = false, guide = false;
                for (int d = 1; d <= line && !seam; d++)
                    foreach (int j in new[] { x + d < size ? i + d : -1, y + d < size ? i + d * size : -1, x - d >= 0 ? i - d : -1, y - d >= 0 ? i - d * size : -1 })
                    {
                        if (j < 0 || parts[j] != p) { seam = true; break; }
                        if (p is Part.Front or Part.Back or Part.RightSleeve or Part.LeftSleeve && Math.Floor(cm[j] / 10) != Math.Floor(cm[i] / 10) && j > i) guide = true;
                    }
                Set(i, seam ? Seam : guide ? Guide : Parts[p].Color);
            }
        });

        var font = LabelFont(index);
        // the stamp's spots: logos in red, lettering in violet, each named
        foreach (var s in src.Spots.Where(s => s.Sx > 0 && s.Sy > 0 && (s.Type == "brand" || s.Type == "font")))
        {
            int layer = brands.FirstOrDefault(b => b.Spot == s).Layer;
            string label = s.Type == "font" ? FontLabel(s.Name) : OwnKit.Role(s, layer) switch
            {
                OwnKit.LogoRole.Main => "MAIN LOGO",
                OwnKit.LogoRole.Shoulder => "SHOULDER",
                OwnKit.LogoRole.Sponsor => "SPONSOR",
                _ => "LEAGUE",
            };
            if (label == null) continue;
            int color = s.Type == "font" ? LettersBox : SpotBox;
            Box(img, size, s, color, line * 3);
            if (font != null) Label(img, size, font.Value, label, s with { Name = "name", Sx = s.Sx * 0.86, Sy = Math.Min(s.Sy * 0.3, s.Sx * 0.86 / Math.Max(4, label.Length) * 1.6) }, color);
        }

        // each part's name at its middle, turned as the stamp turns that part's spots so it reads upright on the model
        if (font != null)
            foreach (var p in new[] { Part.Front, Part.Back, Part.RightSleeve, Part.LeftSleeve, Part.Neck })
            {
                double sx = 0, sy = 0; long k = 0;
                for (int y = 0; y < size; y += 4)
                for (int x = 0; x < size; x += 4)
                    if (parts[y * size + x] == p) { sx += x; sy += y; k++; }
                if (k == 0) continue;
                double cx = sx / k, cy = sy / k;
                double r = p switch
                {
                    Part.Back => src.Spots.FirstOrDefault(s => s.Name == "number_back")?.R ?? 0,
                    Part.RightSleeve => src.Spots.FirstOrDefault(s => s.Name == "number_arm_right")?.R ?? 90,
                    Part.LeftSleeve => src.Spots.FirstOrDefault(s => s.Name == "number_arm_left")?.R ?? 270,
                    _ => crest?.R ?? 180,
                };
                string text = Parts[p].Label;
                double h = p is Part.Front or Part.Back ? 0.03 : 0.022;
                var spot = new StampHotspot(0, "font", "name", true, r, h * text.Length * 0.55, h, cx / size, 1 - cy / size);
                // off the spots: down the text (towards the hem or cuff) while it covers one and stays on its part
                double a = (r + 180) * Math.PI / 180, ddx = Math.Sin(a), ddy = Math.Cos(a);
                // a ring's middle is off it (the neck): down to the cloth first
                bool On(StampHotspot q) { int qx = (int)(q.Tx * size), qy = (int)((1 - q.Ty) * size); return qx >= 0 && qy >= 0 && qx < size && qy < size && parts[qy * size + qx] == p; }
                bool moved = !On(spot);
                for (int step = 0; step < 80 && !On(spot); step++) spot = spot with { Tx = spot.Tx + ddx * h * 0.25, Ty = spot.Ty - ddy * h * 0.25 };
                if (moved && On(spot)) spot = spot with { Tx = spot.Tx + ddx * h * 0.6, Ty = spot.Ty - ddy * h * 0.6 };
                for (int step = 0; step < 80 && src.Spots.Any(s => s.Enabled && s.Sx > 0 && (s.Type == "brand" || s.Type == "font") && Overlap(s, spot)); step++)
                {
                    var next = spot with { Tx = spot.Tx + ddx * h * 0.5, Ty = spot.Ty - ddy * h * 0.5 };
                    int nx = (int)(next.Tx * size), ny = (int)((1 - next.Ty) * size);
                    if (nx < 0 || ny < 0 || nx >= size || ny >= size || parts[ny * size + nx] != p) break;
                    spot = next;
                }
                Label(img, size, font.Value, text, spot, Seam);
            }
        if (font != null) Key(img, size, parts, font.Value);
        return img;
    }

    /// <summary>The colour key, in an empty corner (when one is clear of the jersey).</summary>
    private static void Key(byte[] img, int size, Part[] parts, (byte[] Rgba, int Size) font)
    {
        var rows = new List<(int Color, bool Box, string Text)>
        {
            (Parts[Part.Front].Color, false, "FRONT"), (Parts[Part.Back].Color, false, "BACK"),
            (Parts[Part.RightSleeve].Color, false, "RIGHT SLEEVE"), (Parts[Part.LeftSleeve].Color, false, "LEFT SLEEVE"),
            (Parts[Part.Yoke].Color, false, "SHOULDER YOKE"), (Parts[Part.Gussets].Color, false, "UNDERARM GUSSET"),
            (Parts[Part.Collar].Color, false, "SIDE PANEL"), (Parts[Part.CollarSides].Color, false, "COLLAR ENDS"),
            (Parts[Part.Neck].Color, false, "NECK BAND"), (SpotBox, true, "LOGO SPOT"), (LettersBox, true, "LETTERING SPOT"),
            (Guide, false, "LINE EVERY TEN CM"),
        };
        double row = 0.022, w = 0.27;
        int x0 = (int)(size * 0.015), y0 = (int)(size * 0.015), x1 = x0 + (int)(w * size), y1 = y0 + (int)(rows.Count * row * size);
        for (int y = y0; y < y1; y += 4)
        for (int x = x0; x < x1; x += 4)
            if (parts[y * size + x] != Part.None) return;
        int sw = (int)(row * size * 0.7);
        for (int k = 0; k < rows.Count; k++)
        {
            var (color, box, text) = rows[k];
            int top = y0 + (int)(k * row * size);
            for (int y = top; y < top + sw; y++)
            for (int x = x0; x < x0 + sw; x++)
            {
                bool edge = x - x0 < 3 || y - top < 3 || x0 + sw - x <= 3 || top + sw - y <= 3;
                int c = box && !edge ? Outside : text.StartsWith("LINE") ? (y - top == sw / 2 || y - top == sw / 2 + 1 ? Guide : Outside) : color;
                int o = (y * size + x) * 4;
                img[o] = (byte)(c >> 16); img[o + 1] = (byte)(c >> 8); img[o + 2] = (byte)c;
            }
            // drawn into a wide box, then its letters copied in from the left edge
            var spot = new StampHotspot(0, "font", "name", true, 180, w, row * 0.5, 0.5, 0.5);
            if (KitStamp.Text(spot, text, font.Rgba, font.Size, size, size) is not { } t) continue;
            int left = t.Width;
            for (int y = 0; y < t.Height; y++)
            for (int x = 0; x < left; x++)
                if (t.Rgba[(y * t.Width + x) * 4 + 3] > 0) { left = x; break; }
            int tx0 = x0 + (int)(sw * 1.5), ty0 = top + sw / 2 - t.Height / 2;
            for (int y = 0; y < t.Height; y++)
            for (int x = left; x < t.Width; x++)
            {
                int dx = tx0 + x - left, dy = ty0 + y, si = (y * t.Width + x) * 4;
                if (dx >= size || dy < 0 || dy >= size || t.Rgba[si + 3] == 0) continue;
                double al = t.Rgba[si + 3] / 255.0;
                int o = (dy * size + dx) * 4;
                img[o] = (byte)(0xD8 * al + img[o] * (1 - al)); img[o + 1] = (byte)(0xDC * al + img[o + 1] * (1 - al)); img[o + 2] = (byte)(0xE4 * al + img[o + 2] * (1 - al));
            }
        }
    }

    /// <summary>Whether two spots' boxes (as turned, bounded square on the texture) overlap.</summary>
    private static bool Overlap(StampHotspot a, StampHotspot b)
    {
        static (double W, double H) Half(StampHotspot s)
        {
            double t = s.R * Math.PI / 180, c = Math.Abs(Math.Cos(t)), n = Math.Abs(Math.Sin(t));
            return ((c * s.Sx + n * s.Sy) / 2, (n * s.Sx + c * s.Sy) / 2);
        }
        var (aw, ah) = Half(a);
        var (bw, bh) = Half(b);
        return Math.Abs(a.Tx - b.Tx) < aw + bw && Math.Abs(a.Ty - b.Ty) < ah + bh;
    }

    private static string FontLabel(string name) => name switch
    {
        "name" => "NAME",
        "number_back" => "BACK NUMBER",
        "number_front" => "FRONT NUMBER",
        "number_arm_left" => "LEFT ARM",
        "number_arm_right" => "RIGHT ARM",
        "captain" => "CAPTAIN",
        "patch" => "PATCH",
        _ => null,
    };

    /// <summary>The create-a-team lettering font that has its letters' shapes (custom 0), for the labels.</summary>
    private static (byte[] Rgba, int Size)? LabelFont(AssetIndex index)
    {
        string name = index.Ebx.Keys.FirstOrDefault(k => k.Contains("/custom/font/", StringComparison.OrdinalIgnoreCase) && k.EndsWith("_custom_0_color", StringComparison.OrdinalIgnoreCase));
        if (name == null) return null;
        var t = Kits.Load(index, name, 2048, 0);
        return t.Rgba != null && t.Width == t.Height ? (t.Rgba, t.Width) : null;
    }

    private static void Label(byte[] img, int size, (byte[] Rgba, int Size) font, string text, StampHotspot spot, int color)
    {
        if (KitStamp.Text(spot, text, font.Rgba, font.Size, size, size) is not { } t) return;
        for (int i = 0; i < t.Rgba.Length; i += 4)
            if (t.Rgba[i + 3] > 0) { t.Rgba[i] = (byte)(color >> 16); t.Rgba[i + 1] = (byte)(color >> 8); t.Rgba[i + 2] = (byte)color; }
        var drawn = KitStamp.Compose(img, size, size, new[] { (spot, t.Rgba, t.Width, t.Height) });
        Buffer.BlockCopy(drawn, 0, img, 0, img.Length);
    }

    /// <summary>A spot's box outline, turned as the stamp turns it.</summary>
    private static void Box(byte[] img, int size, StampHotspot s, int color, int width)
    {
        int bw = Math.Max(8, (int)(s.Sx * size)), bh = Math.Max(8, (int)(s.Sy * size));
        var frame = new byte[bw * bh * 4];
        for (int y = 0; y < bh; y++)
        for (int x = 0; x < bw; x++)
        {
            bool edge = x < width || y < width || x >= bw - width || y >= bh - width;
            int o = (y * bw + x) * 4;
            frame[o] = (byte)(color >> 16); frame[o + 1] = (byte)(color >> 8); frame[o + 2] = (byte)color;
            frame[o + 3] = (byte)(edge ? 255 : 40);
        }
        var drawn = KitStamp.Compose(img, size, size, new[] { (s with { Enabled = true }, frame, bw, bh) });
        Buffer.BlockCopy(drawn, 0, img, 0, img.Length);
    }

    /// <summary>An uncompressed 32-bit RGBA DDS (no mips), which every image editor opens.</summary>
    public static byte[] Dds(byte[] rgba, int width, int height)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(0x20534444u); // "DDS "
        w.Write(124u);
        w.Write(0x1u | 0x2u | 0x4u | 0x8u | 0x1000u); // caps|height|width|pitch|pixelformat
        w.Write((uint)height);
        w.Write((uint)width);
        w.Write((uint)width * 4);
        w.Write(0u); // depth
        w.Write(0u); // mips
        for (int i = 0; i < 11; i++) w.Write(0u);
        w.Write(32u);
        w.Write(0x41u); // RGB | alpha pixels
        w.Write(0u);
        w.Write(32u);
        w.Write(0x000000FFu); w.Write(0x0000FF00u); w.Write(0x00FF0000u); w.Write(0xFF000000u);
        w.Write(0x1000u); // texture
        w.Write(0u); w.Write(0u); w.Write(0u); w.Write(0u);
        w.Write(rgba);
        return ms.ToArray();
    }
}
