using System.Buffers.Binary;

namespace NHL27Kit.Core;

/// <summary>
/// RIFF EBX ("EBX\0" / "EBXD" payload, then "EFIX" fixups): file guid, class (type) guids, instance offsets,
/// pointer and import tables. Field layouts need the game's type info, which the kit doesn't load; what it needs
/// is read from these tables and known offsets.
/// </summary>
public sealed class EbxFile
{
    public const int Payload = 32;

    public byte[] Data { get; }
    public int DataEnd { get; }
    public Guid FileGuid { get; }
    public Guid[] ClassGuids { get; }
    public uint[] Instances { get; }
    public uint[] Pointers { get; }
    public (Guid File, Guid Instance)[] Imports { get; }
    public uint[] ImportOffsets { get; }

    private EbxFile(byte[] b)
    {
        Data = b;
        DataEnd = 20 + BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(16));
        int p = DataEnd + (DataEnd & 1);
        if (BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p)) != 0x58494645)   // "EFIX"
            throw new InvalidDataException("No EFIX chunk.");
        p += 8;
        FileGuid = new Guid(b.AsSpan(p, 16)); p += 16;
        int n = I(b, p); p += 4;
        ClassGuids = new Guid[n];
        for (int i = 0; i < n; i++) ClassGuids[i] = new Guid(b.AsSpan(p + 16 * i, 16));
        p += 16 * n;
        n = I(b, p); p += 4 + 4 * n;          // type signatures
        p += 4;                               // exported instance count
        Instances = U32s(b, ref p);
        Pointers = U32s(b, ref p);
        U32s(b, ref p);                       // resource references
        n = I(b, p); p += 4;
        Imports = new (Guid, Guid)[n];
        for (int i = 0; i < n; i++) Imports[i] = (new Guid(b.AsSpan(p + 32 * i, 16)), new Guid(b.AsSpan(p + 32 * i + 16, 16)));
        p += 32 * n;
        ImportOffsets = U32s(b, ref p);
    }

    public static bool IsEbx(byte[] b) => b.Length >= 32 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[12] == 'E' && b[15] == 'D';

    public static EbxFile Parse(byte[] b) => IsEbx(b) ? new EbxFile(b) : null;

    /// <summary>The file guid and the type guid of the first object, read without parsing the whole file.</summary>
    public static (Guid Id, Guid? Type) ReadHeader(byte[] b)
    {
        var e = Parse(b);
        if (e == null)
            return (Guid.Empty, null);
        if (e.Instances.Length == 0)
            return (e.FileGuid, null);
        uint classRef = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(Payload + (int)e.Instances[0])) & 0xFFFF;
        return (e.FileGuid, classRef < e.ClassGuids.Length ? e.ClassGuids[classRef] : null);
    }

    private static int I(byte[] b, int p) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(p));

    private static uint[] U32s(byte[] b, ref int p)
    {
        int n = I(b, p);
        p += 4;
        var r = new uint[n];
        for (int i = 0; i < n; i++) r[i] = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p + 4 * i));
        p += 4 * n;
        return r;
    }
}
