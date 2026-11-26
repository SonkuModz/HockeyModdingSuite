namespace NHL27Kit.Core;

/// <summary>
/// A kit added to an NHL 24 team: a copy of one of its kit folders (<see cref="FromVariant"/>, the Center Ice Mod's
/// 24 or 25) as variant <see cref="ToVariant"/>, with a jersey slot and equipment rows copied from the source kit's.
/// </summary>
public sealed record AltKit
{
    public int TeamId { get; init; }
    public int ArtId { get; init; }
    /// <summary>The kit folder's team (boston).</summary>
    public string Team { get; init; }
    public int FromVariant { get; init; }
    public int ToVariant { get; init; }
    public bool Light { get; init; }
    /// <summary>What the kit is (for the log: "nhl/boston/adidas/fifteen 2025-26 Stadium").</summary>
    public string What { get; init; }
    /// <summary>NHL 27's equipment colours for the kit (equipment type -> zone1..zone15), when it is a converted NHL 27 kit.
    /// The copied rows take these over the source kit's; null keeps the source kit's.</summary>
    public Dictionary<int, int[]> Zones27 { get; set; }
}

public static class KitAlternates
{
    public const string Kind = "kits";
    public const int Adidas = 2;

    /// <summary>The kit folder words by variant number, as content/common/configs/mappings/team_variant_mapping has them.</summary>
    public static readonly string[] Words =
    {
        "home", "away", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen",
        "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty", "twentyone", "twentytwo", "twentythree",
        "twentyfour", "twentyfive", "twentysix", "twentyseven", "twentyeight", "twentynine", "thirty", "thirtyone", "thirtytwo",
        "thirtythree", "thirtyfour", "thirtyfive", "thirtysix", "thirtyseven", "thirtyeight", "thirtynine",
    };

    /// <summary>Variants whose word is as long as <paramref name="word"/> (a copy keeps every name's length), from 26 up.</summary>
    public static IEnumerable<int> SameLength(string word) =>
        Enumerable.Range(26, Words.Length - 26).Where(v => Words[v].Length == word.Length);

    /// <summary>
    /// Adds each kit's jersey slot (a copy of the team's slot for the source kit, right after the team's last Center Ice
    /// Mod slot in the menu) and its equipment rows (copies of the source kit's, the table kept sorted by team and its
    /// index the row number, as it ships).
    /// </summary>
    public static void AddRows(T3Db db, IReadOnlyList<AltKit> kits, List<string> log)
    {
        var jt = db.Table(JerseySlots.TableName);
        var jf = jt.Fields.ToDictionary(f => f.Name);
        foreach (var team in kits.GroupBy(k => k.TeamId))
        {
            var slots = JerseySlots.Read(db, team.Key);
            if (slots.Count == 0) throw new InvalidDataException($"Team {team.Key} has no jersey slots.");
            var list = slots.ToList();
            int after = list.FindLastIndex(s => s.Style == Adidas && s.Variant is 24 or 25);
            if (after < 0) after = list.Count - 1;
            int at = after + 1;
            foreach (var k in team)
            {
                if (slots.Any(s => s.Style == Adidas && s.Variant == k.ToVariant && s.ArtId == k.ArtId)) throw new InvalidDataException($"Team {k.TeamId} already has a slot for variant {k.ToVariant}.");
                var src = slots.FirstOrDefault(s => s.Style == Adidas && s.Variant == k.FromVariant && s.ArtId == k.ArtId)
                          ?? throw new InvalidDataException($"Team {k.TeamId} has no slot for its kit {k.FromVariant} to copy.");
                list.Insert(at++, new JerseySlot
                {
                    ArtId = k.ArtId, Style = Adidas, Variant = k.ToVariant, Label = src.Label, IsLight = k.Light, IsDefault = false, Template = src.Row,
                });
            }
            string what = JerseySlots.Write(db, team.Key, list);
            log.Add($"team {team.Key}: jersey slots {what}");
        }

        var et = db.Table("stockteamequipmenttable");
        if (et == null) return;
        var ef = et.Fields.ToDictionary(f => f.Name);
        int I(int r, string f) => (int)db.Get(et, r, ef[f]);
        bool sorted = true, numbered = true;
        for (int r = 0; r < et.WrittenRecords; r++)
        {
            if (r > 0 && I(r, "team") < I(r - 1, "team")) sorted = false;
            if (I(r, "index") != r) numbered = false;
        }
        int addedRows = 0, zoned = 0;
        using (db.Batch())
        {
            foreach (var k in kits)
            {
                var rows = Enumerable.Range(0, et.WrittenRecords).Where(r => !db.IsInvalid(et, r) && I(r, "jerseyartid") == k.ArtId && I(r, "era") == Adidas && I(r, "variant") == k.FromVariant).ToList();
                if (Enumerable.Range(0, et.WrittenRecords).Any(r => I(r, "jerseyartid") == k.ArtId && I(r, "era") == Adidas && I(r, "variant") == k.ToVariant))
                    throw new InvalidDataException($"Art team {k.ArtId} already has equipment rows for variant {k.ToVariant}.");
                foreach (int r in rows)
                {
                    if (et.WrittenRecords >= et.Records) throw new InvalidOperationException($"{et.Name} is full ({et.Records:N0} rows).");
                    var bytes = db.RecordBytes(et, r);
                    int row = et.WrittenRecords;
                    db.SetWritten(et, row + 1);
                    db.WriteRecord(et, row, bytes);
                    db.Set(et, row, ef["variant"], k.ToVariant);
                    if (k.Zones27 != null && k.Zones27.TryGetValue(I(row, "equipment"), out var z))
                    {
                        for (int i = 0; i < 15; i++) db.Set(et, row, ef[$"zone{i + 1}"], z[i]);
                        zoned++;
                    }
                    addedRows++;
                }
            }
            if (sorted)
            {
                var order = Enumerable.Range(0, et.WrittenRecords).OrderBy(r => I(r, "team")).ThenBy(r => r).ToList();
                if (!order.Select((r, i) => r == i).All(x => x)) db.Reorder(et, order);
            }
            if (numbered)
                for (int r = 0; r < et.WrittenRecords; r++)
                    if (I(r, "index") != r) db.Set(et, r, ef["index"], r);
        }
        log.Add($"{et.Name}: {addedRows} rows added ({et.WrittenRecords:N0} of {et.Records:N0}), {zoned} with NHL 27's colours");
    }
}

public static class KitEquipment27
{
    /// <summary>NHL 27's equipment colours for its kit (art id, adidas era, variant): equipment type -> zone1..zone15.</summary>
    public static Dictionary<int, int[]> Read(T3Db db27, int artId, int variant)
    {
        var et = db27.Table("stockteamequipmenttable");
        var ef = et.Fields.ToDictionary(f => f.Name);
        int I(int r, string f) => Convert.ToInt32(db27.Get(et, r, ef[f]));
        var map = new Dictionary<int, int[]>();
        for (int r = 0; r < et.WrittenRecords; r++)
            if (!db27.IsInvalid(et, r) && I(r, "jerseyartid") == artId && I(r, "era") == KitAlternates.Adidas && I(r, "variant") == variant)
                map.TryAdd(I(r, "equipment"), Enumerable.Range(1, 15).Select(i => I(r, $"zone{i}")).ToArray());
        return map;
    }
}
