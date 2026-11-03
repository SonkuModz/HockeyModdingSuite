namespace NHL27Kit.Core;

/// <summary>
/// Writes database edits into the game files through the mod list. A database has one mod entry ("db", its path) that
/// holds every edit made to it so far: saving reads the database as the game has it now (earlier edits included), applies
/// the new changes, puts the original bytes back, and records and applies original → edited as that entry. Restoring the
/// entry undoes every edit; the original stays in the mod list's backups.
/// </summary>
public static class DbEditor
{
    public const string Kind = "db";

    /// <summary>
    /// Applies <paramref name="edit"/> to the database <paramref name="name"/> in the game. <paramref name="edit"/> gets the
    /// database as the game has it now and returns a summary of what it changed (or null for nothing).
    /// </summary>
    public static ModEntry Save(AssetIndex index, ModStore store, string game, string name, Func<T3Db, string> edit, Action<string> log = null, string source = "Database page")
    {
        var legacy = new Legacy(index);
        var db = legacy.OpenDb(name);
        string what = edit(db);
        if (what == null) return store.Find(Kind, name);
        var f = db.Source;

        // the original under any earlier edits
        var existing = store.Find(Kind, name);
        string history = "";
        if (existing != null)
        {
            var state = store.State(existing, game);
            if (state is ModState.Applied or ModState.Partial)
            {
                store.Restore(existing, game, log);
                history = existing.Summary;
            }
        }
        ChunkPlan plan;
        try
        {
            var raw = legacy.ReadChunk(f.ChunkId);
            db.Data.CopyTo(raw, f.Offset);
            var tocs = new TocCache(index);
            plan = ChunkPatcher.Plan(index, f.ChunkId, raw, LocationIndex.Build(index, tocs), tocs, log);
        }
        catch
        {
            // keep the earlier edits in the game
            if (existing != null) store.Apply(existing, game, log);
            throw;
        }
        string summary = string.IsNullOrEmpty(history) ? what : history + "; " + what;
        if (summary.Length > 600) summary = "…" + summary[^599..];
        var entry = store.Add(Kind, name, source, summary, plan.Patches, plan.Notes.Where(n => n.StartsWith("left") || n.Contains("shared")));
        store.Apply(entry, game, log);
        return entry;
    }
}
