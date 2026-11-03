using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;

namespace NHL27Kit.Core;

/// <summary>One of the emulator's Be A Pro save slots, with the summary the game writes beside it.</summary>
public sealed record BeAProSlot(string Dir, string Name, string FirstName, string LastName, string Position, string Team, int Season, bool IsBackup)
{
    public string Title => $"{FirstName} {LastName}".Trim();
    public string Detail => $"{Name}{(IsBackup ? " (the game's backup)" : "")}  ·  {Position}  ·  {Team}  ·  {SeasonText}";
    public string SeasonText => Season > 19000000 ? new DateTime(Season / 10000, Season / 100 % 100, Season % 100).ToString("MMMM d, yyyy") : "";
}

/// <summary>A rating as the pro's career stores it: where each copy lives (see docs/beapro/README.md).</summary>
public sealed record BeAProRating(string Name, int Current = -1, int Base = -1, int Other = -1, int Fallback = -1, int Pro = -1);

/// <summary>
/// A Be A Pro save from KytyPS5's save folder (unencrypted), read and written in place. DATA packs the files INDEX
/// lists (0x50-byte entries: name, flag, size, start, end); each packed file is u64 length, an 8-byte save id, then its
/// bytes. The career file is the byte 1, an LZ4 frame and the u32 length it unpacks to: a stream of blocks and, at its
/// end, a table of (u32 block id, u32 offset, u32 stored size, u32 unpacked size) entries and a u16 count. A block with
/// an unpacked size is the byte 0 and its bytes (under 512) or 1 and an LZ4 frame; one without is stored as is. The
/// game matches blocks by id and checks nothing else, so an edit repacks the blocks it changed and fixes the offsets.
/// </summary>
public sealed class BeAProSave
{
    public const uint CareerBlock = 0x1B9A22BB, PlayersBlock = 0xA086F262, LeagueBlock = 0x423A2DAC, ProBlock = 0xC0F3C086;

    /// <summary>The 26 skater ratings in the pro block's order: overall, then alphabetical without fighting frequency.</summary>
    public static readonly BeAProRating[] SkaterRatings =
    {
        new("Overall", Current: 451, Pro: 0),
        new("Acceleration", Current: 538, Base: 556, Other: 617, Pro: 1),
        new("Aggression", Current: 446, Fallback: 489, Pro: 2),
        new("Agility", Current: 447, Fallback: 490, Pro: 3),
        new("Balance", Current: 539, Base: 557, Other: 618, Pro: 4),
        new("Body checking", Current: 540, Base: 558, Other: 619, Pro: 5),
        new("Defensive awareness", Current: 541, Base: 559, Other: 620, Pro: 6),
        new("Deking", Current: 542, Base: 560, Other: 621, Pro: 7),
        new("Discipline", Current: 543, Base: 561, Other: 622, Pro: 8),
        new("Durability", Current: 448, Fallback: 491, Pro: 9),
        new("Endurance", Current: 449, Fallback: 492, Pro: 10),
        new("Faceoffs", Current: 544, Base: 562, Other: 623, Pro: 11),
        new("Fighting skill", Current: 545, Base: 563, Other: 624, Pro: 12),
        new("Hand-eye", Current: 546, Base: 564, Other: 625, Pro: 13),
        new("Offensive awareness", Current: 547, Base: 565, Other: 626, Pro: 14),
        new("Passing", Current: 452, Fallback: 494, Pro: 15),
        new("Poise", Current: 453, Fallback: 495, Pro: 16),
        new("Puck control", Current: 548, Base: 566, Other: 627, Pro: 17),
        new("Shot blocking", Current: 549, Base: 567, Other: 628, Pro: 18),
        new("Slap shot accuracy", Current: 550, Base: 568, Other: 629, Pro: 19),
        new("Slap shot power", Current: 551, Base: 569, Other: 630, Pro: 20),
        new("Speed", Current: 455, Fallback: 497, Pro: 21),
        new("Stick checking", Current: 552, Base: 570, Other: 631, Pro: 22),
        new("Strength", Current: 553, Base: 571, Other: 632, Pro: 23),
        new("Wrist shot accuracy", Current: 554, Base: 572, Other: 633, Pro: 24),
        new("Wrist shot power", Current: 555, Base: 573, Other: 634, Pro: 25),
    };

    private const int RecordIndex = 140, XFactorIds = 744, XFactorTiers = 756, ProRatings = 6188, ProActivities = 6272, ProLevels = 6328;
    public static readonly string[] Activities = { "Management", "Teammates", "Brand", "Health", "Skill training", "X-Factor training" };

    private sealed class Packed { public string Name; public byte[] Bytes; }
    private sealed class Block { public uint Id; public int Raw; public byte[] Data; public bool Plain, Dirty; public byte[] Stored; }

    private readonly List<Packed> _files = new();
    private readonly List<Block> _blocks = new();
    private readonly byte[] _careerHeader;
    private int _record = -1;

    public string Dir { get; }
    public BeAProSlot Slot { get; }
    public int PlayerId { get; }
    public bool IsGoalie => Slot.Position == "G";
    /// <summary>Whether the pro's player record was found (ratings and X-Factors can be edited).</summary>
    public bool HasRecord => _record >= 0;

    // ================================================================ slots

    /// <summary>The emulator's save folder for a title: RUN\_SaveData\TITLE.</summary>
    public static string SavesDir(string runDir, string titleId) => Path.Combine(runDir, "_SaveData", titleId);

    /// <summary>The Be A Pro slots in a save folder, newest first.</summary>
    public static List<BeAProSlot> Slots(string savesDir)
    {
        var list = new List<BeAProSlot>();
        if (!Directory.Exists(savesDir)) return list;
        foreach (var dir in Directory.GetDirectories(savesDir, "BeAPro*").OrderByDescending(d => File.GetLastWriteTimeUtc(Path.Combine(d, "DATA"))))
        {
            try { list.Add(ReadSlot(dir, ReadPacked(dir))); }
            catch { /* not a save we can read */ }
        }
        return list;
    }

    private static BeAProSlot ReadSlot(string dir, List<Packed> files)
    {
        var meta = files.FirstOrDefault(f => f.Name == "__SaveLoadMetaData")?.Bytes;
        string S(JsonElement r, string k) => r.TryGetProperty(k, out var v) ? v.ToString() : "";
        if (meta == null) return new BeAProSlot(dir, Path.GetFileName(dir), "", "", "", "", 0, false);
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(meta, 16, meta.Length - 16).TrimEnd('\0'));
        var r = doc.RootElement;
        return new BeAProSlot(dir, Path.GetFileName(dir), S(r, "PlayerFirstName"), S(r, "PlayerLastName"), S(r, "PositionString"), S(r, "TeamFullName"),
                              int.TryParse(S(r, "SeasonYear"), out int y) ? y : 0, S(r, "IsBackup") == "True");
    }

    private static List<Packed> ReadPacked(string dir)
    {
        byte[] data = File.ReadAllBytes(Path.Combine(dir, "DATA")), index = File.ReadAllBytes(Path.Combine(dir, "INDEX"));
        var list = new List<Packed>();
        for (int e = 0; e + 0x50 <= index.Length; e += 0x50)
        {
            string name = Encoding.ASCII.GetString(index, e, 0x20).TrimEnd('\0');
            if (name.Length == 0) continue;
            long start = BinaryPrimitives.ReadInt64LittleEndian(index.AsSpan(e + 0x30)), end = BinaryPrimitives.ReadInt64LittleEndian(index.AsSpan(e + 0x38));
            if (start < 0 || end > data.Length || end < start) throw new InvalidDataException($"INDEX entry {name} is out of range");
            list.Add(new Packed { Name = name, Bytes = data.AsSpan((int)start, (int)(end - start)).ToArray() });
        }
        return list;
    }

    // ================================================================ open

    public static BeAProSave Open(string dir) => new(dir);

    private BeAProSave(string dir)
    {
        Dir = dir;
        _files = ReadPacked(dir);
        Slot = ReadSlot(dir, _files);
        var career = _files.FirstOrDefault(f => !f.Name.StartsWith("__")) ?? throw new InvalidDataException("No career file in this save");
        byte[] c = career.Bytes;
        _careerHeader = c[..16];
        int rawLength = BinaryPrimitives.ReadInt32LittleEndian(c.AsSpan(c.Length - 4));
        byte[] stream = Unpack(c.AsSpan(16, c.Length - 20), rawLength);

        int count = BinaryPrimitives.ReadUInt16LittleEndian(stream.AsSpan(stream.Length - 2));
        int table = stream.Length - 2 - count * 16;
        for (int i = 0; i < count; i++)
        {
            var e = stream.AsSpan(table + i * 16);
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(e);
            int off = BinaryPrimitives.ReadInt32LittleEndian(e[4..]), stored = BinaryPrimitives.ReadInt32LittleEndian(e[8..]), raw = BinaryPrimitives.ReadInt32LittleEndian(e[12..]);
            var bytes = stream.AsSpan(off, stored).ToArray();
            var b = new Block { Id = id, Raw = raw, Stored = bytes, Plain = raw == 0 };
            b.Data = raw == 0 ? bytes : Unpack(bytes, raw);
            _blocks.Add(b);
        }

        var pro = Find(ProBlock);
        PlayerId = pro != null ? BinaryPrimitives.ReadInt32LittleEndian(pro.Data) : -1;
        _record = FindRecord();
    }

    /// <summary>Unpacks a tagged block (0 = bytes as they are, 1 = an LZ4 frame).</summary>
    private static byte[] Unpack(ReadOnlySpan<byte> tagged, int raw)
    {
        if (tagged[0] == 0) return tagged[1..].ToArray();
        if (tagged[0] != 1) throw new InvalidDataException($"Unknown block packing {tagged[0]}");
        using var src = LZ4Stream.Decode(new MemoryStream(tagged[1..].ToArray()));
        var dst = new byte[raw];
        int n = 0, r;
        while (n < raw && (r = src.Read(dst, n, raw - n)) > 0) n += r;
        if (n != raw) throw new InvalidDataException($"A block unpacked to {n:N0} bytes, not {raw:N0}");
        return dst;
    }

    /// <summary>Packs like the game: under 512 bytes stored after a 0, else 1 and an LZ4 frame (64 KB independent blocks, content checksum).</summary>
    private static byte[] Pack(byte[] data)
    {
        if (data.Length < 0x200) return [0, .. data];
        var ms = new MemoryStream();
        ms.WriteByte(1);
        var settings = new LZ4EncoderSettings { BlockSize = 64 * 1024, ChainBlocks = false, ContentChecksum = true, CompressionLevel = LZ4Level.L00_FAST };
        using (var enc = LZ4Stream.Encode(ms, settings, leaveOpen: true)) enc.Write(data);
        return ms.ToArray();
    }

    private Block Find(uint id) => _blocks.FirstOrDefault(b => b.Id == id);

    /// <summary>The pro's record in the players block: first name at +40, last name at +65, the player index at +140.</summary>
    private int FindRecord()
    {
        var players = Find(PlayersBlock);
        if (players == null || PlayerId < 0 || Slot.FirstName.Length == 0) return -1;
        byte[] d = players.Data, first = Encoding.Latin1.GetBytes(Slot.FirstName + "\0"), last = Encoding.Latin1.GetBytes(Slot.LastName + "\0");
        for (int i = d.AsSpan().IndexOf(first); i >= 40; )
        {
            int s = i - 40;
            if (s + 964 <= d.Length && d[i - 1] == 0 && d.AsSpan(s + 65).StartsWith(last) && BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(s + RecordIndex)) == PlayerId) return s;
            int next = d.AsSpan(i + 1).IndexOf(first);
            i = next < 0 ? -1 : i + 1 + next;
        }
        return -1;
    }

    // ================================================================ values

    private byte[] Record => Find(PlayersBlock).Data;
    private void Touch(uint id) => Find(id).Dirty = true;

    public int Rating(BeAProRating r) => HasRecord ? Record[_record + r.Current] : -1;

    /// <summary>Sets a rating everywhere the career keeps it; the base copies move by the same amount.</summary>
    public void SetRating(BeAProRating r, int value)
    {
        if (!HasRecord) return;
        value = Math.Clamp(value, 0, 99);
        byte[] d = Record;
        int delta = value - d[_record + r.Current];
        if (delta == 0) return;
        d[_record + r.Current] = (byte)value;
        foreach (int off in new[] { r.Base, r.Other, r.Fallback })
            if (off >= 0) d[_record + off] = (byte)Math.Clamp(d[_record + off] + delta, 0, 99);
        Touch(PlayersBlock);
        var pro = Find(ProBlock);
        if (r.Pro >= 0 && pro != null && pro.Data.Length > ProRatings + 26)
        {
            pro.Data[ProRatings + r.Pro] = (byte)value;
            pro.Dirty = true;
        }
    }

    /// <summary>X-Factor slot 0-2: ability id (-1 none) and tier (1 Specialist, 2 All-Star, 3 Elite).</summary>
    public (int Ability, int Tier) XFactor(int slot) => HasRecord
        ? (BinaryPrimitives.ReadInt32LittleEndian(Record.AsSpan(_record + XFactorIds + slot * 4)), BinaryPrimitives.ReadInt32LittleEndian(Record.AsSpan(_record + XFactorTiers + slot * 4)))
        : (-1, 0);

    public void SetXFactor(int slot, int ability, int tier)
    {
        if (!HasRecord) return;
        if (ability < 0) tier = 0;
        BinaryPrimitives.WriteInt32LittleEndian(Record.AsSpan(_record + XFactorIds + slot * 4), ability);
        BinaryPrimitives.WriteInt32LittleEndian(Record.AsSpan(_record + XFactorTiers + slot * 4), tier);
        Touch(PlayersBlock);
    }

    /// <summary>The career's numbered values (u32 id, i32 value) at the start of the career block; what each id means isn't known.</summary>
    public List<(int Id, int Value)> CareerValues()
    {
        var b = Find(CareerBlock);
        var list = new List<(int, int)>();
        if (b == null) return list;
        int len = BinaryPrimitives.ReadInt32LittleEndian(b.Data.AsSpan(4));
        for (int p = 8; p + 8 <= Math.Min(8 + len, b.Data.Length); p += 8)
            list.Add((BinaryPrimitives.ReadInt32LittleEndian(b.Data.AsSpan(p)), BinaryPrimitives.ReadInt32LittleEndian(b.Data.AsSpan(p + 4))));
        return list;
    }

    public void SetCareerValue(int index, int value)
    {
        var b = Find(CareerBlock);
        BinaryPrimitives.WriteInt32LittleEndian(b.Data.AsSpan(8 + index * 8 + 4), value);
        b.Dirty = true;
    }

    /// <summary>Six values and six levels in the pro block that fit the energy activities (not confirmed).</summary>
    public (int Value, int Level) Activity(int i)
    {
        var p = Find(ProBlock);
        return p == null || p.Data.Length < ProLevels + 24 ? (0, 0)
            : (BinaryPrimitives.ReadInt32LittleEndian(p.Data.AsSpan(ProActivities + i * 4)), BinaryPrimitives.ReadInt32LittleEndian(p.Data.AsSpan(ProLevels + i * 4)));
    }

    public void SetActivity(int i, int value, int level)
    {
        var p = Find(ProBlock);
        if (p == null || p.Data.Length < ProLevels + 24) return;
        BinaryPrimitives.WriteInt32LittleEndian(p.Data.AsSpan(ProActivities + i * 4), value);
        BinaryPrimitives.WriteInt32LittleEndian(p.Data.AsSpan(ProLevels + i * 4), level);
        p.Dirty = true;
    }

    /// <summary>The save's league database (the 32 beapro tables of nhl_game), unpacked.</summary>
    public byte[] LeagueDatabase() => Find(LeagueBlock)?.Data;

    public bool IsDirty => _blocks.Any(b => b.Dirty);

    // ================================================================ write

    /// <summary>The DATA and INDEX files with the edited blocks repacked.</summary>
    public (byte[] Data, byte[] Index) Build()
    {
        var stream = new MemoryStream();
        var table = new MemoryStream();
        foreach (var b in _blocks)
        {
            byte[] stored = !b.Dirty ? b.Stored : b.Plain ? b.Data : Pack(b.Data);
            int off = (int)stream.Position;
            stream.Write(stored);
            Span<byte> e = stackalloc byte[16];
            BinaryPrimitives.WriteUInt32LittleEndian(e, b.Id);
            BinaryPrimitives.WriteInt32LittleEndian(e[4..], off);
            BinaryPrimitives.WriteInt32LittleEndian(e[8..], stored.Length);
            BinaryPrimitives.WriteInt32LittleEndian(e[12..], b.Plain ? 0 : b.Data.Length);
            table.Write(e);
        }
        stream.Write(table.ToArray());
        stream.Write(BitConverter.GetBytes((ushort)_blocks.Count));
        byte[] raw = stream.ToArray();

        // the career file: u64 length, save id, packed stream, unpacked length
        byte[] packed = Pack(raw);
        var career = new byte[16 + packed.Length + 4];
        BinaryPrimitives.WriteInt64LittleEndian(career, career.Length - 16);
        _careerHeader.AsSpan(8, 8).CopyTo(career.AsSpan(8));
        packed.CopyTo(career, 16);
        BinaryPrimitives.WriteInt32LittleEndian(career.AsSpan(career.Length - 4), raw.Length);

        var data = new MemoryStream();
        var index = new byte[_files.Count * 0x50];
        for (int i = 0; i < _files.Count; i++)
        {
            byte[] bytes = _files[i].Name.StartsWith("__") ? _files[i].Bytes : career;
            long start = data.Position;
            data.Write(bytes);
            var e = index.AsSpan(i * 0x50);
            Encoding.ASCII.GetBytes(_files[i].Name).CopyTo(e);
            BinaryPrimitives.WriteInt64LittleEndian(e[0x20..], 1);
            BinaryPrimitives.WriteInt64LittleEndian(e[0x28..], bytes.Length);
            BinaryPrimitives.WriteInt64LittleEndian(e[0x30..], start);
            BinaryPrimitives.WriteInt64LittleEndian(e[0x38..], start + bytes.Length);
        }
        return (data.ToArray(), index);
    }

    /// <summary>
    /// Writes the edits into the slot after copying it to <paramref name="backupRoot"/>\SLOT-yyyyMMdd-HHmmss. Returns the
    /// backup folder. The written save is read back before anything is replaced.
    /// </summary>
    public string Write(string backupRoot)
    {
        var (data, index) = Build();
        var check = Path.Combine(Path.GetTempPath(), "nhl27kit-bap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(check);
        try
        {
            File.WriteAllBytes(Path.Combine(check, "DATA"), data);
            File.WriteAllBytes(Path.Combine(check, "INDEX"), index);
            var back = Open(check);
            if (back._blocks.Count != _blocks.Count || back._blocks.Zip(_blocks).Any(p => !p.First.Data.AsSpan().SequenceEqual(p.Second.Data)))
                throw new InvalidDataException("The rebuilt save didn't read back the same; nothing was written.");
        }
        finally { Directory.Delete(check, true); }

        string backup = Path.Combine(backupRoot, $"{Path.GetFileName(Dir)}-{DateTime.Now:yyyyMMdd-HHmmss}");
        CopyDir(Dir, backup);
        File.WriteAllBytes(Path.Combine(Dir, "DATA"), data);
        File.WriteAllBytes(Path.Combine(Dir, "INDEX"), index);
        foreach (var b in _blocks.Where(b => b.Dirty)) { b.Stored = b.Plain ? b.Data : null; b.Dirty = false; }
        // keep the packed bytes for the next build
        var reread = Open(Dir);
        for (int i = 0; i < _blocks.Count; i++) _blocks[i].Stored = reread._blocks[i].Stored;
        return backup;
    }

    private static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
        foreach (var d in Directory.GetDirectories(from)) CopyDir(d, Path.Combine(to, Path.GetFileName(d)));
    }
}
