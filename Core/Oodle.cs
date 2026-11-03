using System.Runtime.InteropServices;

namespace NHL27Kit.Core;

/// <summary>
/// Oodle, the compression Frostbite's data blocks use (Kraken). oo2core_9_win64.dll is loaded from next to the kit
/// (or a "ThirdParty" folder below it).
/// </summary>
public static unsafe class Oodle
{
    public const int Kraken = 8;

    private delegate long DecompressFn(byte* src, long srcLen, byte* dst, long dstLen, int fuzz, int crc, int verbose,
                                       byte* dstBase, long dstBaseSize, nint callback, nint userdata, nint scratch, long scratchSize, int threadPhase);
    private delegate long CompressFn(int codec, byte* src, long srcLen, byte* dst, int level, nint options, nint dictBase, nint lrm, nint scratch, long scratchSize);

    private static DecompressFn _decompress;
    private static CompressFn _compress;
    private static readonly object Gate = new();

    public static string DllPath { get; set; }

    private static void Bind()
    {
        if (_decompress != null)
            return;
        lock (Gate)
        {
            if (_decompress != null)
                return;
            string path = DllPath;
            if (path == null)
            {
                foreach (string dir in new[] { AppContext.BaseDirectory, Path.Combine(AppContext.BaseDirectory, "ThirdParty") })
                {
                    if (!Directory.Exists(dir))
                        continue;
                    path = Directory.EnumerateFiles(dir, "oo2core_9*.dll", SearchOption.AllDirectories).FirstOrDefault()
                           ?? Directory.EnumerateFiles(dir, "oo2core_*.dll", SearchOption.AllDirectories).OrderBy(p => p).LastOrDefault();
                    if (path != null)
                        break;
                }
            }
            if (path == null)
                throw new FileNotFoundException("Oodle (oo2core_9_win64.dll) was not found next to NHL27Kit.");
            nint lib = NativeLibrary.Load(path);
            _decompress = Marshal.GetDelegateForFunctionPointer<DecompressFn>(NativeLibrary.GetExport(lib, "OodleLZ_Decompress"));
            _compress = Marshal.GetDelegateForFunctionPointer<CompressFn>(NativeLibrary.GetExport(lib, "OodleLZ_Compress"));
        }
    }

    /// <summary>Decompresses <paramref name="src"/> into exactly <paramref name="rawSize"/> bytes.</summary>
    public static byte[] Decompress(ReadOnlySpan<byte> src, int rawSize)
    {
        Bind();
        var dst = new byte[rawSize];
        fixed (byte* s = src)
        fixed (byte* d = dst)
        {
            long n = _decompress(s, src.Length, d, rawSize, 0, 0, 0, null, 0, 0, 0, 0, 0, 3);
            if (n != rawSize)
                throw new InvalidDataException($"Oodle decompressed {n} of {rawSize} bytes.");
        }
        return dst;
    }

    /// <summary>Compresses with the given codec and level; null if Oodle fails.</summary>
    public static byte[] Compress(ReadOnlySpan<byte> src, int codec, int level)
    {
        Bind();
        var dst = new byte[src.Length + 274 * ((src.Length + 0x3FFFF) / 0x40000) + 64];
        fixed (byte* s = src)
        fixed (byte* d = dst)
        {
            long n = _compress(codec, s, src.Length, d, level, 0, 0, 0, 0, 0);
            if (n <= 0)
                return null;
            Array.Resize(ref dst, (int)n);
        }
        return dst;
    }
}
