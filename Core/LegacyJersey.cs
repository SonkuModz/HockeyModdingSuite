namespace NHL27Kit.Core;

/// <summary>
/// Converts a jersey texture made for the older (legacy) NHL template into NHL 27's jersey layout. Both lay the jersey
/// out alike (back on top, front below, collar in the middle, sleeves off the sides, the inside of the collar in the
/// bottom-left square) at different proportions. Every NHL 27 skater jersey (nineties, rbk, adidas) shares one UV
/// layout, so one mapping covers all of them.
///
/// Fitted on the NHL 27 jersey mesh against screenshots of the same jerseys in NHL Legacy:
/// - body: a vertical piecewise-linear map down the jersey (legacy collar is taller; the crest area shrinks 1.2-1.3×
///   evenly and sits mid-chest; the hem band keeps its proportions) and, per row, a horizontal map out from the centre
///   (1.28× across the middle, then to the legacy side seam, which is wider on the front than the back);
/// - sleeves: the legacy sleeve runs the other way, from the shoulder at the sheet's edge to the cuff near the body,
///   so stripes land at the elbow as they do in Legacy.
/// </summary>
public static class LegacyJersey
{
    // NHL 27 y → legacy y down the body, on a 1024 sheet
    private static readonly (double T, double S)[] Rows =
    {
        (0, 12), (5, 16), (37, 50), (330, 330),
        (445, 412), (490, 452), (530, 500), (571, 548), (600, 600), (625, 637),  // collar, lace, NHL shield
        (698, 660), (852, 845),                                                  // crest area
        (911, 880), (1008, 1000), (1024, 1020),                                  // hem band
    };

    // legacy y → legacy half-width at the side seam (back panel narrower than the front)
    private static readonly (double T, double S)[] Edge = { (0, 220), (230, 220), (330, 235), (714, 245), (760, 257), (1024, 257) };

    private const double Centre = 512, HalfWidth = 182;   // NHL 27 body column: x 330..694
    private const double Inner = 92, InnerScale = 1.28;   // the middle (crest, numbers) scales evenly

    // NHL 27 sleeve: shoulder at x 330 (y 330..688) tapering to the cuff at x 33 (y 397..628);
    // legacy sleeve: shoulder at x 0, cuff at x 340, y 330..712
    private const double SleeveIn = 330, SleeveOut = 33;
    private const double LegacyShoulder = 0, LegacyCuff = 340, LegacyTop = 330, LegacyBottom = 712;

    // the inside of the collar: a square in the bottom-left corner of both, copied straight across
    private const double PatchW = 250, PatchTop = 780, LegacyPatchW = 240, LegacyPatchTop = 785;

    /// <summary>Converts an RGBA legacy jersey (any size, square expected) into an RGBA NHL 27 jersey of size×size.</summary>
    public static byte[] Convert(byte[] rgba, int width, int height, int size = 2048)
    {
        var dst = new byte[size * size * 4];
        Parallel.For(0, size, y =>
        {
            for (int x = 0; x < size; x++)
            {
                var (su, sv) = Source((x + 0.5) / size, (y + 0.5) / size);
                Sample(rgba, width, height, su * width - 0.5, sv * height - 0.5, dst, (y * size + x) * 4);
            }
        });
        return dst;
    }

    /// <summary>Where in the legacy sheet (0..1) the NHL 27 point (u, v) (0..1) comes from.</summary>
    public static (double U, double V) Source(double u, double v)
    {
        double x = u * 1024, y = v * 1024;
        if (x < PatchW && y > PatchTop)
            return (x / PatchW * LegacyPatchW / 1024, (LegacyPatchTop + (y - PatchTop) / (1024 - PatchTop) * (1024 - LegacyPatchTop)) / 1024);
        foreach (bool right in new[] { false, true })
        {
            double sx = right ? 1024 - x : x;
            double s = (SleeveIn - sx) / (SleeveIn - SleeveOut);
            double top = 330 + 67 * s, bottom = 688 - 60 * s, t = (y - top) / (bottom - top);
            if (s > 0 && s <= 1.05 && t >= -0.03 && t <= 1.03)
            {
                double lx = LegacyShoulder + s * (LegacyCuff - LegacyShoulder);
                return ((right ? 1024 - lx : lx) / 1024, (LegacyTop + t * (LegacyBottom - LegacyTop)) / 1024);
            }
        }
        double ly = Interp(Rows, y);
        double edge = Interp(Edge, ly), dx = Math.Abs(x - Centre);
        double ldx = dx <= Inner ? dx * InnerScale : Inner * InnerScale + (dx - Inner) * (edge - Inner * InnerScale) / (HalfWidth - Inner);
        return (Math.Clamp(Centre + Math.Sign(x - Centre) * ldx, 0, 1024) / 1024, Math.Clamp(ly, 0, 1024) / 1024);
    }

    private static double Interp((double T, double S)[] k, double t)
    {
        if (t <= k[0].T) return k[0].S;
        for (int i = 1; i < k.Length; i++)
            if (t <= k[i].T) return k[i - 1].S + (t - k[i - 1].T) / (k[i].T - k[i - 1].T) * (k[i].S - k[i - 1].S);
        return k[^1].S;
    }

    private static void Sample(byte[] src, int w, int h, double x, double y, byte[] dst, int o)
    {
        x = Math.Clamp(x, 0, w - 1);
        y = Math.Clamp(y, 0, h - 1);
        int x0 = (int)x, y0 = (int)y, x1 = Math.Min(x0 + 1, w - 1), y1 = Math.Min(y0 + 1, h - 1);
        double fx = x - x0, fy = y - y0;
        int a = (y0 * w + x0) * 4, b = (y0 * w + x1) * 4, c = (y1 * w + x0) * 4, d = (y1 * w + x1) * 4;
        for (int k = 0; k < 4; k++)
        {
            double top = src[a + k] * (1 - fx) + src[b + k] * fx, bottom = src[c + k] * (1 - fx) + src[d + k] * fx;
            dst[o + k] = (byte)Math.Round(top * (1 - fy) + bottom * fy);
        }
    }
}
