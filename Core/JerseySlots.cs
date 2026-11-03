namespace NHL27Kit.Core;

/// <summary>
/// One of a team's uniform slots: a row of nhl_game.db's stockteamjerseys. The kit it shows is the art team's
/// (<see cref="ArtId"/>, exhibitionteams' artid) folder of the brand (<see cref="Style"/>: nineties 0, rbk 1, adidas 2)
/// and number (<see cref="Variant"/>: home 0, away 1, two…). Row is -1 for a slot that isn't in the table yet.
/// </summary>
public sealed record JerseySlot
{
    public int Row { get; init; } = -1;
    /// <summary>The slot's number within the team (stockteamjerseys' jerseyindex; the table is sorted by team, then this).</summary>
    public int JerseyIndex { get; init; } = -1;
    public int ArtId { get; init; }
    public int Style { get; init; }
    public int Variant { get; init; }
    /// <summary>The name the menu gives the slot (locstring: 0 Home, 1 Away, 2 Third…).</summary>
    public int Label { get; init; }
    /// <summary>The team's default home or away uniform (two per team).</summary>
    public bool IsDefault { get; init; }
    public bool IsLight { get; init; }
    /// <summary>For a new slot: the row whose other values it starts from (the kit's own slot on its team, when it has one).</summary>
    public int Template { get; init; } = -1;
}

/// <summary>
/// Reads and writes a team's uniform slots. The menu lists them by orderid; adding a slot takes one of the table's
/// unused records (it has room for 3,135, 2,573 used), removing one gives it back, and the table stays sorted by team
/// and jerseyindex as the game ships it.
/// </summary>
public static class JerseySlots
{
    public const string Database = "gamemodes/database/t3db/nhl_game.db";
    public const string TableName = "stockteamjerseys";
    public static readonly string[] Brands = { "nineties", "rbk", "adidas" };

    /// <summary>The menu names a slot can have. 0-2 are the game's Home, Away and Third; the others haven't been checked in game.</summary>
    public static string LabelName(int code) => code switch
    {
        0 => "Home", 1 => "Away", 2 => "Third", _ => $"Name {code}",
    };

    /// <summary>A kit folder's team in the database: its exhibitionteams row, the id its slots use (index) and its artid.</summary>
    public sealed record KitTeam(int Row, int TeamId, int ArtId, string Name);

    /// <summary>The database team of each kit folder (league, team); folders with no team are left out.</summary>
    public static Dictionary<(string League, string Team), KitTeam> Teams(T3Db db, IEnumerable<Kit> kits)
    {
        var t = db.Table("exhibitionteams");
        var f = t.Fields.ToDictionary(x => x.Name);
        var map = new Dictionary<(string, string), KitTeam>();
        foreach (var kit in kits)
        {
            if (map.ContainsKey((kit.League, kit.Team))) continue;
            int row = KitEquipment.TeamIndex(db, kit);
            if (row < 0) continue;
            map[(kit.League, kit.Team)] = new KitTeam(row, (int)db.Get(t, row, f["index"]), (int)db.Get(t, row, f["artid"]), (string)db.Get(t, row, f["teamname"]));
        }
        return map;
    }

    /// <summary>A kit's brand and number as a slot names them, or null when a slot can't show it (another brand, a folder that isn't a number).</summary>
    public static (int Style, int Variant)? Address(Kit kit)
    {
        int style = Array.IndexOf(Brands, kit.Brand), variant = KitEquipment.Variant(kit.Name);
        return style < 0 || variant < 0 ? null : (style, variant);
    }

    /// <summary>Every kit some team's menu shows (art id, brand, number).</summary>
    public static HashSet<(int ArtId, int Style, int Variant)> Used(T3Db db)
    {
        var t = db.Table(TableName);
        var f = t.Fields.ToDictionary(x => x.Name);
        var used = new HashSet<(int, int, int)>();
        for (int r = 0; r < t.WrittenRecords; r++)
            if (!db.IsInvalid(t, r)) used.Add(((int)db.Get(t, r, f["teamartid"]), (int)db.Get(t, r, f["style"]), (int)db.Get(t, r, f["variant"])));
        return used;
    }

    /// <summary>Why no menu shows a kit folder.</summary>
    public enum UnusedReason { Free, NoTeam, NoAddress }

    /// <summary>
    /// A kit folder with jersey textures that no team's menu shows. <see cref="Team"/> is its database team (null for
    /// <see cref="UnusedReason.NoTeam"/>); <see cref="CopyOf"/> is a menu's kit whose jersey is the same stored texture.
    /// </summary>
    public sealed record UnusedKit(Kit Kit, KitTeam Team, UnusedReason Reason, Kit CopyOf)
    {
        public string Why => (Reason switch
        {
            UnusedReason.Free => "free",
            UnusedReason.NoTeam => "no team in the database",
            _ => "a slot can't name it",
        }) + (CopyOf != null ? $", copy of {CopyOf}" : "");
    }

    /// <summary>
    /// Every kit folder with a jersey texture that no menu (stockteamjerseys) shows, by league, team, brand and name:
    /// free folders a slot could point at, folders of no database team, and folders a slot can't name.
    /// </summary>
    public static List<UnusedKit> Unused(AssetIndex index, T3Db db, IEnumerable<Kit> allKits)
    {
        var kits = allKits.Where(k => k.Diffuse("jersey") != null).ToList();
        var teams = Teams(db, kits);
        var used = Used(db);
        Guid Chunk(Kit k)
        {
            try { return TextureHeader.Parse(index.Read(index.GetRes(k.Diffuse("jersey").Name))).ChunkId; }
            catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException) { return Guid.Empty; }
        }
        bool InMenu(Kit k) => teams.TryGetValue((k.League, k.Team), out var t) && Address(k) is { } a && used.Contains((t.ArtId, a.Style, a.Variant));
        var shownBy = new Dictionary<Guid, Kit>();
        foreach (var k in kits.Where(InMenu)) shownBy.TryAdd(Chunk(k), k);
        shownBy.Remove(Guid.Empty);
        return kits.Where(k => !InMenu(k))
            .Select(k =>
            {
                var team = teams.GetValueOrDefault((k.League, k.Team));
                var reason = team == null ? UnusedReason.NoTeam : Address(k) == null ? UnusedReason.NoAddress : UnusedReason.Free;
                return new UnusedKit(k, team, reason, shownBy.GetValueOrDefault(Chunk(k)));
            })
            .OrderBy(u => u.Kit.League).ThenBy(u => u.Kit.Team).ThenBy(u => u.Kit.Brand).ThenBy(u => u.Kit.Name).ToList();
    }

    /// <summary>The team's slots in menu order (orderid, then jerseyindex).</summary>
    public static List<JerseySlot> Read(T3Db db, int teamId)
    {
        var t = db.Table(TableName);
        var f = t.Fields.ToDictionary(x => x.Name);
        var list = new List<(int Order, JerseySlot Slot)>();
        for (int r = 0; r < t.WrittenRecords; r++)
        {
            if (db.IsInvalid(t, r)) continue;
            int I(string n) => (int)db.Get(t, r, f[n]);
            if (I("teamid") != teamId) continue;
            list.Add((I("orderid"), new JerseySlot
            {
                Row = r, JerseyIndex = I("jerseyindex"), ArtId = I("teamartid"), Style = I("style"), Variant = I("variant"),
                Label = I("locstring"), IsDefault = I("isdefault") == 1, IsLight = I("islightcolor") == 1,
            }));
        }
        return list.OrderBy(x => x.Order).ThenBy(x => x.Slot.JerseyIndex).Select(x => x.Slot).ToList();
    }

    /// <summary>A slot for a kit (art team, brand, number), started from the kit's own slot on its team when it has one.</summary>
    public static JerseySlot NewSlot(T3Db db, int artId, int style, int variant)
    {
        var t = db.Table(TableName);
        var f = t.Fields.ToDictionary(x => x.Name);
        for (int r = 0; r < t.WrittenRecords; r++)
        {
            if (db.IsInvalid(t, r)) continue;
            int I(string n) => (int)db.Get(t, r, f[n]);
            if (I("teamartid") == artId && I("style") == style && I("variant") == variant)
                return new JerseySlot { ArtId = artId, Style = style, Variant = variant, Label = I("locstring") is 0 or 1 ? 2 : I("locstring"), IsLight = I("islightcolor") == 1, Template = r };
        }
        return new JerseySlot { ArtId = artId, Style = style, Variant = variant, Label = 2 };
    }

    /// <summary>
    /// Makes the team's slots <paramref name="slots"/>, in that menu order: kept slots (Row set) get the new values, slots
    /// left out are removed, new ones (Row -1) are added with the next free jerseyindex. Returns a summary of what changed.
    /// </summary>
    public static string Write(T3Db db, int teamId, IReadOnlyList<JerseySlot> slots)
    {
        var t = db.Table(TableName);
        var f = t.Fields.ToDictionary(x => x.Name);
        var before = Read(db, teamId);
        if (before.Count == 0) throw new InvalidOperationException($"Team {teamId} has no uniform slots in {TableName}.");
        var keep = slots.Where(s => s.Row >= 0).Select(s => s.Row).ToHashSet();
        if (slots.Any(s => s.Row >= 0 && before.All(b => b.Row != s.Row))) throw new ArgumentException("A slot belongs to another team.");
        var removed = before.Where(b => !keep.Contains(b.Row)).Select(b => b.Row).ToList();
        var added = slots.Where(s => s.Row < 0).ToList();
        if (slots.Count == 0) throw new ArgumentException("A team needs at least one uniform slot.");
        int written = t.WrittenRecords, need = written - removed.Count + added.Count;
        if (need > t.Records) throw new InvalidOperationException($"{TableName} has room for {t.Records} slots; this needs {need}.");

        // values every new slot takes from the team (its score clock colour and logo)
        int teamRow = before[0].Row;
        int Team(string n) => (int)db.Get(t, teamRow, f[n]);
        int clockColor = Team("scoreclockcolor"), clockLogo = Team("scoreclocklogo");
        int nextIndex = slots.Where(s => s.Row >= 0).Select(s => s.JerseyIndex).DefaultIfEmpty(-1).Max() + 1;
        nextIndex = Math.Max(nextIndex, before.Max(b => b.JerseyIndex) + 1);
        int maxIndex = (1 << f["jerseyindex"].Depth) - 1 + f["jerseyindex"].RangeLow;
        if (nextIndex + added.Count - 1 > maxIndex) throw new InvalidOperationException($"A team's slot numbers go up to {maxIndex}.");

        // a new slot's starting values, read before any record is overwritten
        var templates = added.Select(s => s.Template >= 0 ? s.Template : teamRow).Distinct().ToDictionary(r => r, r => db.RecordBytes(t, r));
        using (db.Batch())
        {
            // new slots go into removed slots' records first, then into unused records after the written ones
            var free = new Queue<int>(removed);
            if (need > written) db.SetWritten(t, need);
            int tail = written;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                int row = s.Row;
                if (row < 0)
                {
                    row = free.Count > 0 ? free.Dequeue() : tail++;
                    db.WriteRecord(t, row, templates[s.Template >= 0 ? s.Template : teamRow]);
                    db.Set(t, row, f["teamid"], teamId);
                    db.Set(t, row, f["jerseyindex"], nextIndex++);
                    db.Set(t, row, f["active"], 1);
                    db.Set(t, row, f["scoreclockcolor"], clockColor);
                    db.Set(t, row, f["scoreclocklogo"], clockLogo);
                }
                db.Set(t, row, f["orderid"], i);
                db.Set(t, row, f["teamartid"], s.ArtId);
                db.Set(t, row, f["style"], s.Style);
                db.Set(t, row, f["variant"], s.Variant);
                db.Set(t, row, f["fevariant"], s.Variant);
                db.Set(t, row, f["variantleadingscorer"], s.Variant);
                db.Set(t, row, f["locstring"], s.Label);
                db.Set(t, row, f["isdefault"], s.IsDefault ? 1 : 0);
                db.Set(t, row, f["islightcolor"], s.IsLight ? 1 : 0);
            }
            // removed slots that no new one took: marked, then cut off the end below
            var drop = free.ToHashSet();
            // the table sorted by team and slot number, the dropped records last
            var order = Enumerable.Range(0, t.WrittenRecords)
                .OrderBy(r => drop.Contains(r) ? 1 : 0)
                .ThenBy(r => (int)db.Get(t, r, f["teamid"])).ThenBy(r => (int)db.Get(t, r, f["jerseyindex"])).ThenBy(r => r).ToList();
            if (!order.Select((r, i) => r == i).All(x => x)) db.Reorder(t, order);
            if (drop.Count > 0) db.SetWritten(t, t.WrittenRecords - drop.Count);
        }
        var parts = new List<string>();
        if (added.Count > 0) parts.Add($"{added.Count} added");
        if (removed.Count > 0) parts.Add($"{removed.Count} removed");
        // kept slots whose place among the other kept ones changed, and kept slots with other values
        var keptBefore = before.Where(b => keep.Contains(b.Row)).Select(b => b.Row).ToList();
        var keptAfter = slots.Where(s => s.Row >= 0).Select(s => s.Row).ToList();
        if (!keptAfter.SequenceEqual(keptBefore)) parts.Add("new order");
        int changed = slots.Count(s => s.Row >= 0 && before.First(b => b.Row == s.Row) != s with { Template = -1 });
        if (changed > 0) parts.Add($"{changed} changed");
        return parts.Count == 0 ? "no changes" : string.Join(", ", parts);
    }
}
