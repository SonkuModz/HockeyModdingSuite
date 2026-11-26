using System.Buffers.Binary;
using System.Text;

namespace NHL27Kit.Core;

/// <summary>
/// Converts an NHL 27 crowd placement EBX (type 0132086d) into the NHL 24 one a level loads (type 14568e26), for
/// porting an NHL 27 arena's seats (PLAN §35). Both hold a root with a seat position array (16 bytes per seat) and a
/// pointer array of channel objects (Orientation f32, Layer u8, Attendance u8, FanProfiles u32, SeatColour u32, Tier
/// u8; NHL 27 adds ActivatedPairIdAndIndex). The layouts are the same; NHL 24's arizonageneric file and NHL 27's differ
/// only in the root type guid and signature, the first two EBXX array hashes and the instance guids.
/// The result is the NHL 27 file with: NHL 24's file guid, instance guids, root type/signature and array hashes; the
/// channel list cut to NHL 24's count; the name string replaced; orientation wrapped to ±180; and Layer, Attendance,
/// FanProfiles, SeatColour and Tier taken from the nearest NHL 24 seat once both bowls are scaled to the same extent
/// (the two games use different value conventions for those, e.g. NHL 27 Utah has Tier 0 everywhere).
/// </summary>
public static class CrowdPort
{
    private sealed class Doc
    {
        public byte[] B; public EbxFile F; public int Efix, EbxxAt; public List<int> Arrays = new(); // EBXX entry positions
        public int Seats, PosAt; public List<int> Channels = new(); // channel instance payload offsets (absolute)
        public int ChannelArrayEntry;
    }

    private static Doc Open(byte[] b)
    {
        var d = new Doc { B = b, F = EbxFile.Parse(b) ?? throw new InvalidDataException("not an EBX") };
        d.Efix = d.F.DataEnd + (d.F.DataEnd & 1);
        int efixEnd = d.Efix + 8 + I(b, d.Efix + 4);
        if (Encoding.ASCII.GetString(b, efixEnd, 4) != "EBXX") throw new InvalidDataException("no EBXX");
        d.EbxxAt = efixEnd;
        int n = I(b, efixEnd + 8);
        for (int i = 0; i < n; i++) d.Arrays.Add(efixEnd + 16 + 16 * i);
        d.PosAt = EbxFile.Payload + I(b, d.Arrays[0]);
        d.Seats = I(b, d.Arrays[0] + 4);
        d.ChannelArrayEntry = d.Arrays[1];
        int ca = EbxFile.Payload + I(b, d.Arrays[1]);
        int count = I(b, d.Arrays[1] + 4);
        for (int i = 0; i < count; i++) d.Channels.Add(ca + 8 * i + I(b, ca + 8 * i));
        return d;
    }

    private static int DataAt(Doc d, int ch) => d.Channels[ch] + 0x28 + I(d.B, d.Channels[ch] + 0x28);
    private static int Cls(Doc d, int ch) => BinaryPrimitives.ReadUInt16LittleEndian(d.B.AsSpan(d.Channels[ch]));
    private static int Size(Doc d, int ch) => Cls(d, ch) == 2 ? 1 : 4;

    public static (byte[] Ebx, List<string> Notes) Convert(byte[] host24, byte[] src27)
    {
        var h = Open(host24);
        var s = Open(src27);
        var b = (byte[])src27.Clone();
        var notes = new List<string>();
        int keep = h.Channels.Count;
        if (s.Channels.Count < keep) throw new InvalidDataException($"source has {s.Channels.Count} channels, host {keep}");
        for (int c = 0; c < keep; c++)
            if (Cls(h, c) != Cls(s, c)) throw new InvalidDataException($"channel {c}: class {Cls(s, c)} in NHL 27, {Cls(h, c)} in NHL 24");
        // file guid, root type guid and signature
        int p = s.Efix + 8;
        h.B.AsSpan(h.Efix + 8, 16).CopyTo(b.AsSpan(p));
        p += 16;
        int nClass = I(b, p);
        h.B.AsSpan(h.Efix + 8 + 16 + 4, 16).CopyTo(b.AsSpan(p + 4));            // class 0
        int hSig = h.Efix + 8 + 16 + 4 + 16 * I(h.B, h.Efix + 8 + 16) + 4;
        int sSig = p + 4 + 16 * nClass + 4;
        h.B.AsSpan(hSig, 4).CopyTo(b.AsSpan(sSig));                                // signature 0
        // root and channel instance guids (the 16 bytes before each instance)
        h.B.AsSpan(EbxFile.Payload, 16).CopyTo(b.AsSpan(EbxFile.Payload));
        for (int c = 0; c < keep; c++) h.B.AsSpan(h.Channels[c] - 16, 16).CopyTo(b.AsSpan(s.Channels[c] - 16));
        // EBXX: the position and channel arrays' hash/flags/type from NHL 24; the channel list cut to NHL 24's count
        for (int a = 0; a < 2; a++) h.B.AsSpan(h.Arrays[a] + 8, 8).CopyTo(b.AsSpan(s.Arrays[a] + 8));
        if (s.Channels.Count != keep)
        {
            W(b, s.ChannelArrayEntry + 4, keep);
            W(b, EbxFile.Payload + I(b, s.ChannelArrayEntry) - 4, keep);
            notes.Add($"channel list cut from {s.Channels.Count} to {keep}");
        }
        // name: the root's first string, overwritten in place (zero padded)
        string hostName = Str(h.B, EbxFile.Payload + (int)h.F.Instances[0] + 0x18), srcName = Str(b, EbxFile.Payload + (int)s.F.Instances[0] + 0x18, out int nameAt);
        if (hostName.Length > srcName.Length) throw new InvalidDataException("host name longer than the source's");
        b.AsSpan(nameAt, srcName.Length).Clear();
        Encoding.ASCII.GetBytes(hostName).CopyTo(b, nameAt);
        // seats: positions and orientation from NHL 27; other channels from the nearest NHL 24 seat in a normalised bowl
        float[] Ext(Doc d) { var e = new float[6] { 1e9f, 1e9f, 1e9f, -1e9f, -1e9f, -1e9f }; for (int i = 0; i < d.Seats; i++) for (int k = 0; k < 3; k++) { float v = F(d.B, d.PosAt + 16 * i + 4 * k); e[k] = Math.Min(e[k], v); e[k + 3] = Math.Max(e[k + 3], v); } return e; }
        var eh = Ext(h); var es = Ext(s);
        float[] Norm(float[] e, float x, float y, float z) => new[] { (x - e[0]) / (e[3] - e[0]), (y - e[1]) / (e[4] - e[1]), (z - e[2]) / (e[5] - e[2]) };
        var hp = new float[h.Seats][];
        for (int i = 0; i < h.Seats; i++) hp[i] = Norm(eh, F(h.B, h.PosAt + 16 * i), F(h.B, h.PosAt + 16 * i + 4), F(h.B, h.PosAt + 16 * i + 8));
        int oriAt = DataAt(s, 0);
        for (int i = 0; i < s.Seats; i++)
        {
            float o = F(b, oriAt + 4 * i);
            o = ((o + 180) % 360 + 360) % 360 - 180;
            BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(oriAt + 4 * i), o);
            var q = Norm(es, F(b, s.PosAt + 16 * i), F(b, s.PosAt + 16 * i + 4), F(b, s.PosAt + 16 * i + 8));
            int best = 0; float bd = float.MaxValue;
            for (int j = 0; j < h.Seats; j++)
            {
                float dx = hp[j][0] - q[0], dy = hp[j][1] - q[1], dz = hp[j][2] - q[2], dd = dx * dx + dy * dy + dz * dz;
                if (dd < bd) { bd = dd; best = j; }
            }
            for (int c = 1; c < keep; c++)
            {
                int sz = Size(h, c);
                h.B.AsSpan(DataAt(h, c) + sz * best, sz).CopyTo(b.AsSpan(DataAt(s, c) + sz * i));
            }
        }
        notes.Add($"{s.Seats:N0} seats (NHL 24 had {h.Seats:N0}); NHL 24 values from the nearest seat in a normalised bowl");
        return (b, notes);
    }

    private static string Str(byte[] b, int ptrAt) => Str(b, ptrAt, out _);
    private static string Str(byte[] b, int ptrAt, out int at)
    {
        at = ptrAt + I(b, ptrAt);
        int e = Array.IndexOf(b, (byte)0, at);
        return Encoding.ASCII.GetString(b, at, e - at);
    }

    private static int I(byte[] b, int p) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(p));
    private static void W(byte[] b, int p, int v) => BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(p), v);
    private static float F(byte[] b, int p) => BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(p));
}
