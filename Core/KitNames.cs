namespace NHL27Kit.Core;

/// <summary>Where a kit's name is in a language database: its LanguageStrings table and row, and the text there now.</summary>
public sealed record KitNameRef(T3Table Table, int Row, string Text);

/// <summary>
/// The names the game gives kits ("2016-17 Away", "2020-21 Reverse Retro"): strings of the language databases
/// (loc/eng_us.db, fre_fr…) whose id is VINTAGE_&lt;style&gt;_&lt;art id&gt;_&lt;variant&gt; (any case), the same brand, art team
/// and kit folder a jersey slot points at. 471 kits have one; the rest have none, and none can be added (both string
/// tables are full). A name is renamed in place, so the new text must compress to no more bytes than the old.
/// </summary>
public static class KitNames
{
    public const string English = "loc/eng_us.db";

    public static string Key(int style, int artId, int variant) => $"vintage_{style}_{artId}_{variant}";

    /// <summary>Every kit name in a language database, by lower-case id.</summary>
    public static Dictionary<string, KitNameRef> Read(T3Db db)
    {
        var map = new Dictionary<string, KitNameRef>();
        foreach (var t in db.Tables)
        {
            var id = t.Fields.FirstOrDefault(f => f.Name == "stringid");
            var text = t.Fields.FirstOrDefault(f => f.Name == "sourcetext");
            if (id == null || text == null) continue;
            for (int r = 0; r < t.WrittenRecords; r++)
            {
                if (db.IsInvalid(t, r)) continue;
                string s = (string)db.Get(t, r, id);
                if (s.StartsWith("vintage_", StringComparison.OrdinalIgnoreCase))
                    map.TryAdd(s.ToLowerInvariant(), new KitNameRef(t, r, (string)db.Get(t, r, text)));
            }
        }
        return map;
    }

    /// <summary>Bytes the text needs and the bytes the name has (need -1: a character this language's strings never use).</summary>
    public static (int Need, int Have) Budget(T3Db db, KitNameRef at, string text) =>
        db.Budget(at.Table, at.Row, at.Table.Fields.First(f => f.Name == "sourcetext"), text);

    public static void Set(T3Db db, KitNameRef at, string text) =>
        db.Set(at.Table, at.Row, at.Table.Fields.First(f => f.Name == "sourcetext"), text);
}
