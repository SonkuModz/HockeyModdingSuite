using NHL27Kit.Core;

namespace NHL27Kit.Cli;

/// <summary>
/// arena-port: NHL 27 arena geometry into NHL 24 meshes (PLAN §35).
///   arena-port elems MESH [--section N] [--n 3]      raw vertex elements of a mesh's sections (either game)
/// </summary>
public static partial class Program
{
    private static int ArenaPortCmd(string game, Options opts)
    {
        string sub = opts.Positional.FirstOrDefault() ?? throw new ArgumentException("arena-port elems|...");
        var rest = opts;
        return sub switch
        {
            "elems" => ArenaPortElems(game, opts),
            "placements" => ArenaPortPlacements(game, opts),
            "build" => ArenaPortBuild(game, opts),
            "materials" => ArenaPortMaterials(game, opts),
            "idxfmt" => ArenaPortIdxFmt(game),
            "boxes" => ArenaPortBoxes(game, opts),
            "crowd" => ArenaPortCrowd(game, opts),
            "uvs" => ArenaPortUvs(game, opts),
            "region" => ArenaPortRegion(game, opts),
            "mattex" => ArenaPortMatTex(game, opts),
            "tancheck" => ArenaPortTanCheck(game, opts),
            "mvdb-tex" => ArenaPortMvdbTex(game, opts),
            "lift" => ArenaPortLift(game, opts),
            _ => throw new ArgumentException($"arena-port {sub}?"),
        };
    }

    /// <summary>arena-port idxfmt: counts the LOD index formats (+0x54) over every *_mesh RES, with examples.</summary>
    private static int ArenaPortIdxFmt(string game)
    {
        var index = OpenIndex(game, false);
        int b0 = index.Layout.IsPs5 ? 16 : 0;
        var seen = new Dictionary<uint, (int N, List<string> Ex)>();
        foreach (var (name, e) in index.Res)
        {
            if (!name.EndsWith("_mesh", StringComparison.OrdinalIgnoreCase)) continue;
            byte[] b;
            try { b = index.Read(e); } catch { continue; }
            if (b.Length < b0 + 0x60) continue;
            for (int i = 0; i < 6; i++)
            {
                long o = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(b0 + 0x20 + 8 * i));
                if (o <= 0 || b0 + o + 0x58 > b.Length) continue;
                uint f = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(b0 + (int)o + 0x54));
                var t = seen.GetValueOrDefault(f, (0, new List<string>()));
                if (t.Item2.Count < 4 && !t.Item2.Contains(name)) t.Item2.Add(name);
                seen[f] = (t.Item1 + 1, t.Item2);
            }
        }
        foreach (var (f, t) in seen.OrderByDescending(x => x.Value.N)) Console.WriteLine($"{f,6}  {t.N,7} LODs  e.g. {string.Join(", ", t.Ex)}");
        return 0;
    }

    /// <summary>arena-port boxes LEVEL [--prefix P]: each placement's world box (LOD 0 positions through its transform) and transform.</summary>
    private static int ArenaPortBoxes(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string level = opts.Positional.ElementAtOrDefault(1) ?? throw new ArgumentException("arena-port boxes LEVEL");
        string prefix = opts.Get("prefix") ?? "";
        var arena = ArenaScene.Levels(index).First(l => l.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
        foreach (var p in ArenaScene.Placements(index, arena, ArenaScene.Names(index)).Where(p => p.Mesh.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            MeshRaw m;
            try { m = LoadRaw(index, p.Mesh); } catch { continue; }
            var t = p.Transform;
            float[] bx = { 1e9f, 1e9f, 1e9f, -1e9f, -1e9f, -1e9f };
            foreach (var s in m.Sections.Where(s => s.Lod == 0 && s.Get(1) != null))
                for (int v = 0; v < s.VertexCount; v++)
                {
                    var q = MeshRaw.Floats(s.Get(1), v, 3);
                    float[] w = { q[0] * t[0] + q[1] * t[3] + q[2] * t[6] + t[9], q[0] * t[1] + q[1] * t[4] + q[2] * t[7] + t[10], q[0] * t[2] + q[1] * t[5] + q[2] * t[8] + t[11] };
                    for (int k = 0; k < 3; k++) { bx[k] = Math.Min(bx[k], w[k]); bx[k + 3] = Math.Max(bx[k + 3], w[k]); }
                }
            Console.WriteLine($"x {bx[0],7:0.0}..{bx[3],7:0.0}  y {bx[1],6:0.0}..{bx[4],6:0.0}  z {bx[2],7:0.0}..{bx[5],7:0.0}  T[{string.Join(" ", t.Select(x => x.ToString("0.##")))}]  {p.Mesh[(p.Mesh.LastIndexOf('/') + 1)..]}");
        }
        return 0;
    }

    /// <summary>
    /// arena-port lift EBX FROM TO --game NHL24 [--apply --name NAME]: every LinearTransform in the EBX (unit rotation rows,
    /// zero pads) whose translation y is FROM (±0.05) gets y = TO, e.g. a level's light trusses raised to a taller roof.
    /// Same size; one ModStore entry (kind "arena").
    /// </summary>
    private static int ArenaPortLift(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        if (index.Layout.IsPs5) throw new ArgumentException("arena-port lift writes NHL 24: pass --game <NHL 24>");
        var a = opts.Positional.Skip(1).ToList();
        if (a.Count != 3) throw new ArgumentException("arena-port lift EBX FROM TO");
        float from = float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture), to = float.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture);
        var bytes = (byte[])index.Read(index.GetEbx(a[0]) ?? throw new ArgumentException($"no EBX {a[0]}")).Clone();
        var f = EbxFile.Parse(bytes);
        float F(int q) => BitConverter.ToSingle(bytes, q);
        int hits = 0;
        for (int o = EbxFile.Payload; o + 64 <= f.DataEnd; o += 16)
        {
            bool unit = true;
            for (int r = 0; r < 3 && unit; r++)
            {
                float x = F(o + 16 * r), y = F(o + 16 * r + 4), z = F(o + 16 * r + 8);
                unit = Math.Abs(MathF.Sqrt(x * x + y * y + z * z) - 1) < 1e-3f && BitConverter.ToUInt32(bytes, o + 16 * r + 12) == 0;
            }
            if (!unit || BitConverter.ToUInt32(bytes, o + 60) != 0 || Math.Abs(F(o + 52) - from) > 0.05f) continue;
            Console.WriteLine($"  0x{o:X}: ({F(o + 48):0.##}, {F(o + 52):0.##}, {F(o + 56):0.##}) -> y {to}");
            BitConverter.TryWriteBytes(bytes.AsSpan(o + 52), to);
            hits++; o += 48;
        }
        Console.WriteLine($"  {hits} transform(s)");
        if (hits == 0 || !opts.Has("apply")) return 0;
        string name = opts.Get("name") ?? throw new ArgumentException("--name NAME for the mod entry");
        RefuseWhileRunning();
        var store = Mods();
        if (store.Find("arena", name) is { } old) { store.Restore(old, game, Console.WriteLine); store.Remove(old); index = OpenIndex(game, false); }
        var plan = ResGrower.PlanEbx(index, a[0], _ => bytes, new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase));
        var entry = store.Add("arena", name, a[0], $"{hits} transform(s) in {Path.GetFileName(a[0])} moved from y {from} to {to}", plan.Patches, plan.Notes);
        store.Apply(entry, game, Console.WriteLine);
        Console.WriteLine($"state: {store.State(entry, game)}");
        return 0;
    }

    /// <summary>
    /// arena-port mvdb-tex MVDB OLDTEX NEWTEX [OLDTEX NEWTEX...] --game NHL24 [--apply --name NAME]: in a level's
    /// MeshVariationDatabase, every import of texture OLDTEX becomes NEWTEX (file and instance guid), so the materials that
    /// bound OLDTEX there draw NEWTEX in that level only. Same size; written through one ModStore entry (kind "arena").
    /// </summary>
    private static int ArenaPortMvdbTex(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        if (index.Layout.IsPs5) throw new ArgumentException("arena-port mvdb-tex writes NHL 24: pass --game <NHL 24>");
        var a = opts.Positional.Skip(1).ToList();
        if (a.Count < 3 || a.Count % 2 == 0) throw new ArgumentException("arena-port mvdb-tex MVDB OLDTEX NEWTEX [OLDTEX NEWTEX...]");
        string mvdb = a[0];
        var bytes = (byte[])index.Read(index.GetEbx(mvdb) ?? throw new ArgumentException($"no EBX {mvdb}")).Clone();
        (Guid File, Guid Inst) Id(string tex)
        {
            var e = EbxFile.Parse(index.Read(index.GetEbx(tex) ?? throw new ArgumentException($"no EBX {tex}")));
            int at = EbxFile.Payload + (int)e.Instances[0] - 16;
            return (e.FileGuid, new Guid(e.Data.AsSpan(at, 16)));
        }
        var f = EbxFile.Parse(bytes);
        // the import table's place: right before the import offsets, 32 bytes an entry
        int p = f.DataEnd + (f.DataEnd & 1) + 8 + 16;
        int I(int q) => BitConverter.ToInt32(bytes, q);
        p += 4 + 16 * I(p); p += 4 + 4 * I(p); p += 4;
        for (int k = 0; k < 3; k++) p += 4 + 4 * I(p);
        int n = I(p); p += 4;
        if (n != f.Imports.Length) throw new InvalidDataException("import table not where expected");
        for (int i = 2; i < a.Count; i += 2)
        {
            var (of, oi) = Id(a[i - 1]); var (nf, ni) = Id(a[i]);
            int hits = 0;
            for (int k = 0; k < n; k++)
            {
                if (new Guid(bytes.AsSpan(p + 32 * k, 16)) != of || new Guid(bytes.AsSpan(p + 32 * k + 16, 16)) != oi) continue;
                nf.TryWriteBytes(bytes.AsSpan(p + 32 * k, 16)); ni.TryWriteBytes(bytes.AsSpan(p + 32 * k + 16, 16)); hits++;
            }
            Console.WriteLine($"  {Path.GetFileName(a[i - 1])} -> {Path.GetFileName(a[i])}: {hits} import(s)");
            if (hits == 0) throw new ArgumentException($"{mvdb} doesn't import {a[i - 1]}");
        }
        if (!opts.Has("apply")) return 0;
        string name = opts.Get("name") ?? throw new ArgumentException("--name NAME for the mod entry");
        RefuseWhileRunning();
        var store = Mods();
        if (store.Find("arena", name) is { } old) { store.Restore(old, game, Console.WriteLine); store.Remove(old); index = OpenIndex(game, false); }
        var plan = ResGrower.PlanEbx(index, mvdb, _ => bytes, new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase));
        var entry = store.Add("arena", name, mvdb, $"Level texture binding: {string.Join(", ", a.Skip(1).Where((_, k) => k % 2 == 1).Select(Path.GetFileName))} in {mvdb}", plan.Patches, plan.Notes);
        store.Apply(entry, game, Console.WriteLine);
        Console.WriteLine($"state: {store.State(entry, game)}");
        return 0;
    }

    /// <summary>
    /// arena-port crowd HOST24 SRC27 --game NHL24 --game27 NHL27 [--dry FILE] [--apply --name NAME]: an NHL 27 crowd
    /// placement EBX converted into the NHL 24 one (CrowdPort) and written through one ModStore entry (kind "arena").
    /// </summary>
    private static int ArenaPortCrowd(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        if (index.Layout.IsPs5) throw new ArgumentException("arena-port crowd writes NHL 24: pass --game <NHL 24>");
        string host = opts.Positional.ElementAtOrDefault(1) ?? throw new ArgumentException("arena-port crowd HOST24 SRC27");
        string src = opts.Positional.ElementAtOrDefault(2) ?? throw new ArgumentException("arena-port crowd HOST24 SRC27");
        string g27 = KitPaths.Canonical(opts.Get("game27") ?? throw new ArgumentException("--game27 DIR")) ?? opts.Get("game27");
        var i27 = AssetIndex.Open(g27, File.ReadAllBytes, KitPaths.IndexFileFor(GameProfile.Detect(g27)), _ => { });
        var h = index.GetEbx(host) ?? throw new ArgumentException($"no NHL 24 EBX {host}");
        var s = i27.GetEbx(src) ?? throw new ArgumentException($"no NHL 27 EBX {src}");
        var (bytes, notes) = CrowdPort.Convert(index.Read(h), i27.Read(s));
        foreach (var n in notes) Console.WriteLine("  " + n);
        Console.WriteLine($"  {bytes.Length:N0} bytes (was {index.Read(h).Length:N0})");
        if (opts.Get("dry") is { } dry) { File.WriteAllBytes(dry, bytes); Console.WriteLine($"  -> {dry}"); }
        if (!opts.Has("apply")) return 0;
        string name = opts.Get("name") ?? throw new ArgumentException("--name NAME for the mod entry");
        RefuseWhileRunning();
        var store = Mods();
        if (store.Find("arena", name) is { } old) { store.Restore(old, game, Console.WriteLine); store.Remove(old); index = OpenIndex(game, false); }
        var tocs = new TocCache(index);
        var loc = LocationIndex.Build(index, tocs);
        var plan = ResGrower.PlanEbx(index, host, _ => bytes, new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase));
        var entry = store.Add("arena", name, src, $"Crowd placement from NHL 27 {Path.GetFileName(src)} into {Path.GetFileName(host)}", plan.Patches, plan.Notes);
        store.Apply(entry, game, Console.WriteLine);
        Console.WriteLine($"state: {store.State(entry, game)}");
        return 0;
    }

    /// <summary>arena-port uvs MESH...: per LOD 0 section, the UV (0x21) range and UV units per metre (median edge ratio).</summary>
    private static int ArenaPortUvs(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        foreach (string name in opts.Positional.Skip(1))
        {
            var m = LoadRaw(index, name);
            Console.WriteLine(name);
            foreach (var s in m.Sections.Where(s => s.Lod == 0 && s.VertexCount > 0))
            {
                var uv = s.Get(0x21); var p = s.Get(1);
                if (uv == null || p == null) continue;
                float u0 = 1e9f, u1 = -1e9f, v0 = 1e9f, v1 = -1e9f;
                for (int v = 0; v < s.VertexCount; v++) { var q = MeshRaw.Floats(uv, v, 2); u0 = Math.Min(u0, q[0]); u1 = Math.Max(u1, q[0]); v0 = Math.Min(v0, q[1]); v1 = Math.Max(v1, q[1]); }
                var ratios = new List<float>();
                for (int i = 0; i + 2 < s.Indices.Length && ratios.Count < 4000; i += 3)
                {
                    int a = s.Indices[i], b = s.Indices[i + 1];
                    var pa = MeshRaw.Floats(p, a, 3); var pb = MeshRaw.Floats(p, b, 3); var ua = MeshRaw.Floats(uv, a, 2); var ub = MeshRaw.Floats(uv, b, 2);
                    float dp = MathF.Sqrt((pa[0] - pb[0]) * (pa[0] - pb[0]) + (pa[1] - pb[1]) * (pa[1] - pb[1]) + (pa[2] - pb[2]) * (pa[2] - pb[2]));
                    float du = MathF.Sqrt((ua[0] - ub[0]) * (ua[0] - ub[0]) + (ua[1] - ub[1]) * (ua[1] - ub[1]));
                    if (dp > 0.05f) ratios.Add(du / dp);
                }
                ratios.Sort();
                Console.WriteLine($"  s{s.Index} u {u0:0.##}..{u1:0.##} v {v0:0.##}..{v1:0.##}  uv/m {(ratios.Count > 0 ? ratios[ratios.Count / 2] : 0):0.###}  {s.Material}");
            }
        }
        return 0;
    }

    /// <summary>arena-port region LEVEL x0 y0 z0 x1 y1 z1 [--prefix P]: triangles (centroid inside the box, LOD 0, each mesh once at
    /// identity) per mesh and material, with their summed area and how much of it faces the rink (normal toward the origin).</summary>
    private static int ArenaPortRegion(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string level = opts.Positional.ElementAtOrDefault(1) ?? throw new ArgumentException("arena-port region LEVEL box");
        var bx = opts.Positional.Skip(2).Take(6).Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        string prefix = opts.Get("prefix") ?? "";
        var arena = ArenaScene.Levels(index).First(l => l.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
        var rows = new List<(string, string, int, float, float)>();
        foreach (var mesh in ArenaScene.Placements(index, arena, ArenaScene.Names(index)).Select(p => p.Mesh).Distinct().Where(m => m.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            MeshRaw m;
            try { m = LoadRaw(index, mesh); } catch { continue; }
            foreach (var s in m.Sections.Where(s => s.Lod == 0 && s.Get(1) != null))
            {
                int n = 0; float area = 0, facing = 0;
                for (int i = 0; i + 2 < s.Indices.Length; i += 3)
                {
                    var a = MeshRaw.Floats(s.Get(1), s.Indices[i], 3); var b = MeshRaw.Floats(s.Get(1), s.Indices[i + 1], 3); var c = MeshRaw.Floats(s.Get(1), s.Indices[i + 2], 3);
                    float cx = (a[0] + b[0] + c[0]) / 3, cy = (a[1] + b[1] + c[1]) / 3, cz = (a[2] + b[2] + c[2]) / 3;
                    if (cx < bx[0] || cx > bx[3] || cy < bx[1] || cy > bx[4] || cz < bx[2] || cz > bx[5]) continue;
                    float ux = b[0] - a[0], uy = b[1] - a[1], uz = b[2] - a[2], vx = c[0] - a[0], vy = c[1] - a[1], vz = c[2] - a[2];
                    float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                    float ar = MathF.Sqrt(nx * nx + ny * ny + nz * nz) / 2;
                    n++; area += ar;
                    if (nx * -cx + nz * -cz > 0) facing += ar;
                }
                if (n > 0) rows.Add((mesh[(mesh.LastIndexOf('/') + 1)..], s.Material, n, area, facing));
            }
        }
        foreach (var r in rows.OrderByDescending(r => r.Item4)) Console.WriteLine($"{r.Item4,9:0.0} m2 ({r.Item5,8:0.0} facing the rink) {r.Item3,6} t  {r.Item1}  {r.Item2}");
        return 0;
    }

    /// <summary>arena-port mattex LEVEL [--prefix P]: each placed mesh section's material and the colour texture it draws.</summary>
    private static int ArenaPortMatTex(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string level = opts.Positional.ElementAtOrDefault(1) ?? throw new ArgumentException("arena-port mattex LEVEL");
        string prefix = opts.Get("prefix") ?? "";
        var arena = ArenaScene.Levels(index).First(l => l.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
        var names = ArenaScene.Names(index); var presets = ArenaScene.Presets(index); var tex = new Dictionary<string, string>();
        var seen = new HashSet<string>();
        foreach (var mesh in ArenaScene.Placements(index, arena, names).Select(p => p.Mesh).Distinct().Where(m => m.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            foreach (var pc in ArenaScene.Load(index, arena, mesh, names, presets, tex))
                if (seen.Add(pc.Part.Material + "|" + pc.Texture)) Console.WriteLine($"{pc.Part.Material}	{pc.Texture}");
        return 0;
    }

    /// <summary>arena-port tancheck MESH...: per LOD 0 section, how often the tangent frame's major-axis bits (24-26 of the
    /// 0x34 u32) agree with the geometric vertex normal's major axis (+x 0, -x 1, +y 2, -y 3, +z 4, -z 5).</summary>
    private static int ArenaPortTanCheck(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        foreach (string name in opts.Positional.Skip(1))
        {
            MeshRaw m;
            try { m = LoadRaw(index, name); } catch { continue; }
            foreach (var s in m.Sections.Where(s => s.Lod == 0 && s.VertexCount > 0))
            {
                var p = s.Get(1); var t = s.Get(0x34);
                if (p == null || t == null || t.Size != 4) continue;
                var n = new float[s.VertexCount * 3];
                for (int i = 0; i + 2 < s.Indices.Length; i += 3)
                {
                    var a = MeshRaw.Floats(p, s.Indices[i], 3); var b = MeshRaw.Floats(p, s.Indices[i + 1], 3); var c = MeshRaw.Floats(p, s.Indices[i + 2], 3);
                    float ux = b[0] - a[0], uy = b[1] - a[1], uz = b[2] - a[2], vx = c[0] - a[0], vy = c[1] - a[1], vz = c[2] - a[2];
                    float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                    foreach (int k in new[] { s.Indices[i], s.Indices[i + 1], s.Indices[i + 2] }) { n[k * 3] += nx; n[k * 3 + 1] += ny; n[k * 3 + 2] += nz; }
                }
                int ok = 0, flip = 0, tot = 0;
                for (int v = 0; v < s.VertexCount; v++)
                {
                    float x = n[v * 3], y = n[v * 3 + 1], z = n[v * 3 + 2];
                    if (x * x + y * y + z * z < 1e-12f) continue;
                    int ax = Math.Abs(x) >= Math.Abs(y) && Math.Abs(x) >= Math.Abs(z) ? (x > 0 ? 0 : 1) : Math.Abs(y) >= Math.Abs(z) ? (y > 0 ? 2 : 3) : (z > 0 ? 4 : 5);
                    int bits = (int)((BitConverter.ToUInt32(t.Data, v * 4) >> 24) & 7);
                    tot++; if (bits == ax) ok++; else if (bits == (ax ^ 1)) flip++;
                }
                Console.WriteLine($"{100.0 * ok / Math.Max(1, tot),5:0}% match {100.0 * flip / Math.Max(1, tot),5:0}% opposite  {tot,6} v  {name[(name.LastIndexOf('/') + 1)..]} s{s.Index} {s.Material}");
            }
        }
        return 0;
    }

    private static MeshRaw LoadRaw(AssetIndex index, string name)
    {
        var res = index.GetRes(name) ?? throw new ArgumentException($"no RES {name}");
        return new MeshRaw(index.Read(res), id => index.GetChunk(id) is { } c ? index.Read(c) : null, !index.Layout.IsPs5, res.ResMeta);
    }

    /// <summary>
    /// arena-port build PLAN.json --game NHL24 --game27 NHL27 [--dry DIR] [--apply --name NAME]: NHL 27 pieces into NHL 24
    /// host meshes (MeshRebuild24). The plan: {"level27": "utah_30", "jobs": [{"host": MESH24, "blank": [sections],
    /// "rules": [{"mesh": MESH27 (or a prefix ending in '*'), "material": "glob", "to": section index or host material,
    /// "minY": -1000, "skip": "glob"}]}]}. Each rule takes the LOD 0 sections of every placement of the matching meshes in
    /// level27 (world space; identity when the level doesn't place it) whose material matches and that no earlier rule
    /// took. --dry writes each rebuilt host as OBJ (read back through MeshSet); --apply writes one ModStore entry (kind
    /// "arena", NAME) with every host's grown chunk and patched RES, restoring an earlier entry of that name first.
    /// </summary>
    private static int ArenaPortBuild(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        if (index.Layout.IsPs5) throw new ArgumentException("arena-port build writes NHL 24: pass --game <NHL 24>");
        string planFile = opts.Positional.ElementAtOrDefault(1) ?? throw new ArgumentException("arena-port build PLAN.json");
        string g27 = KitPaths.Canonical(opts.Get("game27") ?? throw new ArgumentException("--game27 DIR")) ?? opts.Get("game27");
        var i27 = AssetIndex.Open(g27, File.ReadAllBytes, KitPaths.IndexFileFor(GameProfile.Detect(g27)), _ => { });
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(planFile));
        var root = doc.RootElement;
        // the NHL 27 level's placements: mesh -> transforms
        var placed = new Dictionary<string, List<float[]>>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("level27", out var lv))
        {
            var arena = ArenaScene.Levels(i27).First(l => l.Level.Equals(lv.GetString(), StringComparison.OrdinalIgnoreCase));
            foreach (var p in ArenaScene.Placements(i27, arena, ArenaScene.Names(i27)))
                (placed.TryGetValue(p.Mesh, out var l) ? l : placed[p.Mesh] = new()).Add(p.Transform);
        }
        // "placement": "identity" puts each placed mesh in once where it is modelled (the level's transform arrays are
        // found heuristically and some belong to other records)
        bool identity = root.TryGetProperty("placement", out var pm) && pm.GetString() == "identity";
        var raw27 = new Dictionary<string, MeshRaw>(StringComparer.OrdinalIgnoreCase);
        MeshRaw Src(string n) => raw27.TryGetValue(n, out var m) ? m : raw27[n] = LoadRaw(i27, n);
        // "clearBox": [x0,y0,z0,x1,y1,z1]: with identity placement, a mesh with any vertex inside is left out whole (pieces
        // modelled around the origin that need a parent transform, e.g. aisle railings)
        float[] clear = root.TryGetProperty("clearBox", out var cb) ? cb.EnumerateArray().Select(x => x.GetSingle()).ToArray() : null;
        var cleared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // "extra": [{"mesh": glob, "transform": [12 floats]}]: with identity placement, a matching mesh also goes in again
        // under that transform (e.g. one side's suites mirrored across x onto the side NHL 27 leaves open)
        var extras = new List<(string Mesh, float[] T)>();
        if (root.TryGetProperty("extra", out var ex))
            foreach (var e in ex.EnumerateArray())
                extras.Add((e.GetProperty("mesh").GetString(), e.GetProperty("transform").EnumerateArray().Select(x => x.GetSingle()).ToArray()));
        bool Inside(string m27)
        {
            if (clear == null || !identity) return false;
            foreach (var s in Src(m27).Sections.Where(s => s.Lod == 0 && s.Get(1) != null))
                for (int v = 0; v < s.VertexCount; v++)
                {
                    var q = MeshRaw.Floats(s.Get(1), v, 3);
                    if (q[0] > clear[0] && q[0] < clear[3] && q[1] > clear[1] && q[1] < clear[4] && q[2] > clear[2] && q[2] < clear[5]) return true;
                }
            return false;
        }
        static bool Glob(string pat, string s) => pat == null || pat == "*" ||
            System.Text.RegularExpressions.Regex.IsMatch(s ?? "", "^" + System.Text.RegularExpressions.Regex.Escape(pat).Replace("\\*", ".*").Replace("\\?", ".") + "$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // "lightmapMirror": "+z" etc.: see MeshRebuild24.LightmapMirror
        MeshRebuild24.LightmapMirror = root.TryGetProperty("lightmapMirror", out var lmm) ? lmm.GetString() : null;
        var results = new List<(string Host, MeshRebuild24.Result R)>();
        var taken = new HashSet<(string, int, int)>(); // (mesh27, placement, section) used
        foreach (var job in root.GetProperty("jobs").EnumerateArray())
        {
            string host = job.GetProperty("host").GetString();
            var hres = index.GetRes(host) ?? throw new ArgumentException($"no NHL 24 mesh {host}");
            var hostBytes = index.Read(hres);
            var hostRaw = new MeshRaw(hostBytes, id => index.GetChunk(id) is { } c ? index.Read(c) : null, true, hres.ResMeta);
            var lod0 = hostRaw.Sections.Where(s => s.Lod == 0).ToList();
            var pieces = new Dictionary<int, List<MeshRebuild24.Piece>>();
            if (job.TryGetProperty("blank", out var bl))
                foreach (var x in bl.EnumerateArray()) pieces[x.GetInt32()] = new();
            foreach (var rule in job.GetProperty("rules").EnumerateArray())
            {
                string mpat = rule.GetProperty("mesh").GetString(), mat = rule.TryGetProperty("material", out var mt) ? mt.GetString() : "*";
                string skip = rule.TryGetProperty("skip", out var sk) ? sk.GetString() : null;
                // "uv": [scale u, scale v, offset u, offset v] moves the pieces' first UV (e.g. into half of a shared texture)
                float[] uvx = rule.TryGetProperty("uv", out var ux) ? ux.EnumerateArray().Select(x => x.GetSingle()).ToArray() : null;
                var to = rule.GetProperty("to");
                int target = to.ValueKind == System.Text.Json.JsonValueKind.Number ? to.GetInt32()
                    : to.GetString() == "drop" ? -1
                    : lod0.FirstOrDefault(s => s.Material.Equals(to.GetString(), StringComparison.OrdinalIgnoreCase))?.Index ?? throw new ArgumentException($"{host}: no section {to}");
                var meshes = mpat.EndsWith("*") ? placed.Keys.Where(k => k.StartsWith(mpat[..^1], StringComparison.OrdinalIgnoreCase)).ToList() : new List<string> { mpat };
                int count = 0, tris = 0;
                foreach (string m27 in meshes)
                {
                    var transforms = !identity && placed.TryGetValue(m27, out var tl) ? tl
                        : new List<float[]> { ArenaPlacement.Identity }.Concat(identity ? extras.Where(e => Glob(e.Mesh, m27)).Select(e => e.T) : Enumerable.Empty<float[]>()).ToList();
                    var src = Src(m27);
                    if (cleared.Contains(m27)) continue;
                    if (Inside(m27)) { cleared.Add(m27); Console.WriteLine($"  left out (inside clearBox): {m27}"); continue; }
                    for (int pi = 0; pi < transforms.Count; pi++)
                        foreach (var s in src.Sections.Where(s => s.Lod == 0 && s.VertexCount > 0 && Glob(mat, s.Material) && (skip == null || !skip.Split('|').Any(k => Glob(k, s.Material)))))
                        {
                            if (!taken.Add((m27, pi, s.Index))) continue;
                            count++; tris += s.TriangleCount;
                            if (target < 0) continue; // "drop": taken, not placed
                            count--; tris -= s.TriangleCount;
                            (pieces.TryGetValue(target, out var pl) ? pl : pieces[target] = new()).Add(new MeshRebuild24.Piece(s, transforms[pi], uvx));
                            count++; tris += s.TriangleCount;
                        }
                }
                Console.WriteLine($"  {Path.GetFileName(host)} s{target} <- {mpat} [{mat}]: {count} pieces, {tris:N0} triangles");
            }
            // "flatUv": [material...] keeps the host's first UV; {material: [u, v]} puts every vertex on that texel instead
            ISet<string> flat = null; Dictionary<string, float[]> flatAt = null;
            if (job.TryGetProperty("flatUv", out var fu))
                if (fu.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    flatAt = fu.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.ValueKind == System.Text.Json.JsonValueKind.Array ? x.Value.EnumerateArray().Select(y => y.GetSingle()).ToArray() : null, StringComparer.OrdinalIgnoreCase);
                    flat = flatAt.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
                else flat = fu.EnumerateArray().Select(x => x.GetString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            ISet<string> two = job.TryGetProperty("doubleSided", out var ds) ? ds.EnumerateArray().Select(x => x.GetString()).ToHashSet(StringComparer.OrdinalIgnoreCase) : null;
            var ring = job.TryGetProperty("ringUv", out var ru) ? ru.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetSingle(), StringComparer.OrdinalIgnoreCase) : null;
            var r = MeshRebuild24.Build(hostBytes, id => index.GetChunk(id) is { } c ? index.Read(c) : null, pieces, flat, two, ring, flatAt);
            foreach (var n in r.Notes) Console.WriteLine($"    {n}");
            results.Add((host, r));
        }
        if (root.TryGetProperty("report", out var rep))
        {
            var left = new Dictionary<string, (int N, long T)>();
            foreach (var (m27, tl) in placed.Where(x => x.Key.StartsWith(rep.GetString(), StringComparison.OrdinalIgnoreCase)))
                for (int pi = 0; pi < (identity ? 1 : tl.Count); pi++)
                    foreach (var s in Src(m27).Sections.Where(s => s.Lod == 0 && s.VertexCount > 0 && !taken.Contains((m27, pi, s.Index))))
                    {
                        var v = left.GetValueOrDefault(s.Material);
                        left[s.Material] = (v.N + 1, v.T + s.TriangleCount);
                    }
            foreach (var (m, v) in left.OrderByDescending(x => x.Value.T)) Console.WriteLine($"  not placed: {m} ({v.N} pieces, {v.T:N0} triangles)");
        }
        if (opts.Get("dry") is { } dry)
        {
            Directory.CreateDirectory(dry);
            foreach (var (host, r) in results)
            {
                var hres = index.GetRes(host);
                var back = new MeshSet(r.Res, id => r.Chunks.TryGetValue(id, out var c) ? c : index.GetChunk(id) is { } e ? index.Read(e) : null, ps4: true, meta: hres.ResMeta);
                string f = Path.Combine(dry, Path.GetFileName(host) + ".obj");
                File.WriteAllText(f, back.ToObj(0));
                Console.WriteLine($"read back {Path.GetFileName(host)}: {back.Lods[0].Sum(p => p.TriangleCount):N0} triangles -> {f}");
            }
        }
        if (!opts.Has("apply")) return 0;
        string name = opts.Get("name") ?? throw new ArgumentException("--name NAME for the mod entry");
        RefuseWhileRunning();
        var store = Mods();
        if (store.Find("arena", name) is { } old) { store.Restore(old, game, Console.WriteLine); store.Remove(old); index = OpenIndex(game, false); }
        var tocs = new TocCache(index);
        var loc = LocationIndex.Build(index, tocs);
        var ends = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var patches = new List<PatchData>();
        var notes = new List<string>();
        foreach (var (host, r) in results)
        {
            foreach (var (id, bytes) in r.Chunks)
            {
                var plan = ChunkGrower.Plan(index, id, FrostbiteBlocks.Store(bytes), loc, tocs, ends);
                patches.AddRange(plan.Patches); notes.AddRange(plan.Notes);
            }
            var rp = ResPatcher.Plan(index, host, r.Res, loc, tocs, ends);
            patches.AddRange(rp.Patches); notes.AddRange(rp.Notes);
        }
        var entry = store.Add("arena", name, planFile, $"Arena port: {results.Count} NHL 24 mesh(es) rebuilt from NHL 27 ({string.Join(", ", results.Select(x => Path.GetFileName(x.Host)))})", patches, notes);
        store.Apply(entry, game, Console.WriteLine);
        Console.WriteLine($"state: {store.State(entry, game)}");
        return 0;
    }

    /// <summary>arena-port materials LEVEL [--prefix P]: LOD 0 vertex/triangle totals per material over every placement.</summary>
    private static int ArenaPortMaterials(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string level = opts.Positional.ElementAtOrDefault(1) ?? throw new ArgumentException("arena-port materials LEVEL");
        string prefix = opts.Get("prefix") ?? "";
        var arena = ArenaScene.Levels(index).First(l => l.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
        var pl = ArenaScene.Placements(index, arena, ArenaScene.Names(index)).Where(p => p.Mesh.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        var tot = new Dictionary<string, (long V, long T, int Max, HashSet<string> M)>();
        bool byMesh = opts.Has("by-mesh");
        foreach (var g in pl.GroupBy(p => p.Mesh))
        {
            MeshRaw m;
            try { m = LoadRaw(index, g.Key); } catch { continue; }
            if (byMesh)
            {
                var l0 = m.Sections.Where(s => s.Lod == 0).ToList();
                Console.WriteLine($"{g.Count(),3}x {l0.Sum(s => s.TriangleCount),8:N0} t  {g.Key}");
                foreach (var s in l0) Console.WriteLine($"        s{s.Index} {s.VertexCount,6} v {s.TriangleCount,6} t  {s.Material}");
            }
            foreach (var s in m.Sections.Where(s => s.Lod == 0))
            {
                var t = tot.GetValueOrDefault(s.Material, (0, 0, 0, new HashSet<string>()));
                t.M.Add(Path.GetFileName(g.Key));
                tot[s.Material] = (t.V + (long)s.VertexCount * g.Count(), t.T + (long)s.TriangleCount * g.Count(), Math.Max(t.Max, s.VertexCount), t.M);
            }
        }
        foreach (var (k, v) in tot.OrderByDescending(x => x.Value.T))
            Console.WriteLine($"{v.V,9:N0} v {v.T,9:N0} t  max {v.Max,6}  {k}  ({v.M.Count} meshes)");
        Console.WriteLine($"total {tot.Values.Sum(x => x.V):N0} v {tot.Values.Sum(x => x.T):N0} t");
        return 0;
    }

    /// <summary>arena-port placements LEVEL: each placement's mesh and rotation part (rounded), grouped.</summary>
    private static int ArenaPortPlacements(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string level = opts.Positional.ElementAtOrDefault(1) ?? throw new ArgumentException("arena-port placements LEVEL");
        var arena = ArenaScene.Levels(index).First(l => l.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
        var pl = ArenaScene.Placements(index, arena, ArenaScene.Names(index));
        foreach (var g in pl.GroupBy(p => string.Join(",", p.Transform.Take(9).Select(x => MathF.Round(x, 2).ToString("0.##")))).OrderByDescending(g => g.Count()))
            Console.WriteLine($"{g.Count(),4}  rot [{g.Key}]  e.g. {string.Join(" ", g.Take(3).Select(p => Path.GetFileName(p.Mesh)))}");
        return 0;
    }

    private static int ArenaPortElems(string game, Options opts)
    {
        var index = OpenIndex(game, false);
        string name = opts.Positional.ElementAtOrDefault(1) ?? throw new ArgumentException("arena-port elems MESH");
        var m = LoadRaw(index, name);
        int n = int.TryParse(opts.Get("n"), out int k) ? k : 3;
        int? only = int.TryParse(opts.Get("section"), out int si) ? si : null;
        if (opts.Get("json") is { } jf)
        {
            // positions, triangles and the raw 0x34 tangent frame words of every section, for fitting the encoding
            var outSecs = m.Sections.Where(s => s.Lod == 0 && s.Get(1) != null && s.Get(0x34) != null).Select(s => new
            {
                s.Material,
                pos = Enumerable.Range(0, s.VertexCount).SelectMany(v => MeshRaw.Floats(s.Get(1), v, 3)).ToArray(),
                tri = s.Indices,
                tan = Enumerable.Range(0, s.VertexCount).Select(v => BitConverter.ToUInt32(s.Get(0x34).Data, v * 4)).ToArray(),
            }).ToList();
            File.WriteAllText(jf, System.Text.Json.JsonSerializer.Serialize(outSecs));
            Console.WriteLine($"wrote {outSecs.Count} sections to {jf}");
            return 0;
        }
        foreach (var s in m.Sections)
        {
            if (only != null && s.Index != only) continue;
            Console.WriteLine($"lod{s.Lod} s{s.Index} {s.Material} (mat {s.MaterialIndex}) {s.VertexCount} v {s.TriangleCount} t  idx[0..5]={string.Join(",", s.Indices.Take(6))}");
            foreach (var e in s.Elements)
            {
                var rows = Enumerable.Range(0, Math.Min(n, s.VertexCount)).Select(v =>
                {
                    string hex = Convert.ToHexString(e.Data, v * e.Size, e.Size);
                    string f = e.Format is 2 or 3 or 4 or 6 or 8 ? " (" + string.Join(" ", MeshRaw.Floats(e, v, e.Format is 6 ? 2 : e.Format is 8 or 4 ? 4 : e.Format is 2 ? 2 : 3).Select(x => x.ToString("0.###"))) + ")" : "";
                    return hex + f;
                });
                Console.WriteLine($"   {e.Usage:x2}:{e.Format:x2}/{e.Size}  {string.Join(" | ", rows)}");
            }
        }
        return 0;
    }
}
