using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace NHL27Kit.Core;

/// <summary>
/// The player skeleton (content/rigging/skeleton/skeleton_player, a SkeletonAsset EBX): 434 bones with their parents
/// and three matrix arrays, local pose, model pose (the A-pose the garment meshes are bound in) and its inverse.
/// Matrices are row-vector (rows: right, up, forward, translation), which is System.Numerics' convention; the stored
/// fourth column is 0 and is set to (0, 0, 0, 1) here. Each array is a u32 count then the data (16-byte aligned).
/// Skinned meshes' blend indices are indices into this bone list.
/// </summary>
public sealed class Skeleton
{
    public const string Asset = "content/rigging/skeleton/skeleton_player";
    public string[] Names { get; }
    public int[] Parents { get; }
    public Matrix4x4[] Local { get; }
    public Matrix4x4[] Model { get; }
    public Matrix4x4[] Inverse { get; }
    public int Count => Names.Length;

    public Skeleton(byte[] d)
    {
        // the bone names: the longest run of NUL-terminated identifiers; the first is the asset's own name
        string text = Encoding.ASCII.GetString(d);
        var run = Regex.Matches(text, @"(?:[A-Za-z_][A-Za-z0-9_]*\0){30,}").MaxBy(m => m.Length)
            ?? throw new InvalidDataException("No bone names in the skeleton.");
        Names = run.Value.TrimEnd('\0').Split('\0').Skip(1).ToArray();
        int n = Names.Length;

        int at = 0;
        for (int o = 0; o + 4 + 4 * n <= d.Length && Parents == null; o += 4)
        {
            if (U32(d, o) != n) continue;
            var p = new int[n];
            bool ok = true;
            for (int i = 0; i < n && ok; i++)
            {
                p[i] = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(o + 4 + 4 * i));
                ok = i == 0 ? p[i] == -1 : p[i] >= 0 && p[i] < i;
            }
            if (ok) { Parents = p; at = o + 4 + 4 * n; }
        }
        if (Parents == null) throw new InvalidDataException("No parent list in the skeleton.");

        var mats = new List<Matrix4x4[]>();
        for (int o = at; o + 4 + 64 * n <= d.Length && mats.Count < 3; o += 4)
        {
            if (U32(d, o) != n || (o + 4) % 16 != 0) continue;
            var m = new Matrix4x4[n];
            for (int i = 0; i < n; i++) m[i] = Read(d, o + 4 + 64 * i);
            mats.Add(m);
            o += 64 * n;
        }
        if (mats.Count < 3) throw new InvalidDataException("The skeleton's pose matrices weren't found.");
        (Local, Model, Inverse) = (mats[0], mats[1], mats[2]);
    }

    public int IndexOf(string name) => Array.IndexOf(Names, name);

    public static Skeleton Load(AssetIndex index) =>
        new(index.Read(index.GetEbx(Asset) ?? throw new InvalidDataException($"No {Asset}.")));

    private static Matrix4x4 Read(byte[] d, int p)
    {
        var f = new float[16];
        for (int i = 0; i < 16; i++) f[i] = BinaryPrimitives.ReadSingleLittleEndian(d.AsSpan(p + 4 * i));
        return new Matrix4x4(f[0], f[1], f[2], 0, f[4], f[5], f[6], 0, f[8], f[9], f[10], 0, f[12], f[13], f[14], 1);
    }

    private static uint U32(byte[] b, int p) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p));
}

/// <summary>
/// An ANT animation clip's data (the clip's RES), DCT-compressed. After a 0x20-byte resource header:
/// - u16 frames, u16 quaternion channels, u16 vector channels, u16 float channels, u16 quantize divisor, u8 quantize
///   scale, u8 catch-all width; then a byte per channel whose high nibble is its coefficient count;
/// - per channel: 4 × s16 base, then a u16 bit spec per coefficient (x, y, z, w widths, a nibble each);
/// - a little-endian bit stream, read least significant bit first: for each block of 8 frames, for each channel,
///   each coefficient's 4 signed values (block 0 skips coefficient 0, which is the base alone; in every block the
///   base is added to coefficient 0).
/// A frame's value is an 8-point inverse DCT of its block's coefficients over the divisor. Quaternions are x, y, z, w
/// and come out near unit length; they're normalized.
/// </summary>
public sealed class AntClip
{
    public int Frames { get; }
    public int Quats { get; }
    public int Vectors { get; }
    public int Floats { get; }
    /// <summary>[frame][channel] = x, y, z, w (quaternion channels first, then vectors, then floats).</summary>
    public Vector4[][] Values { get; }

    public AntClip(byte[] res)
    {
        var b = res.AsSpan(0x20);
        Frames = BinaryPrimitives.ReadUInt16LittleEndian(b);
        Quats = BinaryPrimitives.ReadUInt16LittleEndian(b[2..]);
        Vectors = BinaryPrimitives.ReadUInt16LittleEndian(b[4..]);
        Floats = BinaryPrimitives.ReadUInt16LittleEndian(b[6..]);
        double div = BinaryPrimitives.ReadUInt16LittleEndian(b[8..]);
        int scale = b[10];
        int channels = Quats + Vectors + Floats, p = 12 + channels;
        var bases = new short[channels][];
        var specs = new ushort[channels][];
        for (int c = 0; c < channels; c++)
        {
            bases[c] = new short[4];
            for (int k = 0; k < 4; k++) bases[c][k] = BinaryPrimitives.ReadInt16LittleEndian(b[(p + 2 * k)..]);
            p += 8;
            specs[c] = new ushort[b[12 + c] >> 4];
            for (int j = 0; j < specs[c].Length; j++) specs[c][j] = BinaryPrimitives.ReadUInt16LittleEndian(b[(p + 2 * j)..]);
            p += 2 * specs[c].Length;
        }

        var dct = new double[8, 8];
        for (int f = 0; f < 8; f++)
            for (int i = 0; i < 8; i++)
                dct[f, i] = (i == 0 ? 0.25 : 0.5 * Math.Cos((2 * f + 1) * i * Math.PI / 16)) * (scale * 0.1 * i + 1) / div;

        var bits = new BitReader(res, 0x20 + p);
        Values = new Vector4[Frames][];
        for (int f = 0; f < Frames; f++) Values[f] = new Vector4[channels];
        var co = new int[8, 4];
        for (int blk = 0; blk * 8 < Frames; blk++)
            for (int c = 0; c < channels; c++)
            {
                Array.Clear(co);
                for (int j = 0; j < specs[c].Length; j++)
                {
                    if (blk == 0 && j == 0) continue;
                    int s = specs[c][j];
                    co[j, 0] = bits.Signed(s >> 12); co[j, 1] = bits.Signed(s >> 8 & 15);
                    co[j, 2] = bits.Signed(s >> 4 & 15); co[j, 3] = bits.Signed(s & 15);
                }
                for (int k = 0; k < 4; k++) co[0, k] += bases[c][k];
                for (int f = 0; f < 8 && blk * 8 + f < Frames; f++)
                {
                    var v = new double[4];
                    for (int i = 0; i < 8; i++)
                        for (int k = 0; k < 4; k++) v[k] += co[i, k] * dct[f, i];
                    var r = new Vector4((float)v[0], (float)v[1], (float)v[2], (float)v[3]);
                    if (c < Quats && r.LengthSquared() > 0) r = Vector4.Normalize(r);
                    Values[blk * 8 + f][c] = r;
                }
            }
    }

    private sealed class BitReader(byte[] b, int start)
    {
        private long _pos = (long)start * 8;
        public int Signed(int n)
        {
            if (n == 0) return 0;
            int v = 0;
            for (int i = 0; i < n; i++, _pos++)
            {
                int byteAt = (int)(_pos >> 3);
                if (byteAt < b.Length && (b[byteAt] >> (int)(_pos & 7) & 1) != 0) v |= 1 << i;
            }
            return v >> (n - 1) != 0 ? v - (1 << n) : v;
        }
    }
}

/// <summary>
/// The front-end idle (af/assets/nhl/clips/fe_home_selectidle, the stance players hold in the menus) on the player
/// skeleton, giving each bone's skinning matrix at a time. The clip's channels follow its rig layouts (nhl_rig), so
/// they're named here in that order; bones the clip doesn't drive keep their local bind pose (helper and twist bones
/// follow their parents). The clip stands side-on to its trajectory, so its constant turn and drift are taken out to
/// face the camera like the bind pose.
/// </summary>
public sealed class PlayerIdle
{
    public const string Clip = "af/assets/nhl/clips/fe_home_selectidle";
    public const float Fps = 30;

    private static readonly string[] QuatChannels =
    {
        "AITrajectory", "MoveToFace", "Spine", "Spine1", "Spine2", "Spine3",
        "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand",
        "Neck", "Neck1", "Head", "Hips",
        "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase", "LeftFootEnd", "LeftSkate", "LeftSkate_End",
        "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase", "RightFootEnd", "RightSkate", "RightSkate_End",
        "StickTransform", "StickHeel", "StickHeelEnd", "StickFlex1", "StickFlex2", "StickBottomHand", "StickTopHand",
    };
    /// <summary>The vector channels that are bone translations (after AITrajectory.t, MoveToFace.s, MoveToFace.t).</summary>
    private static readonly (string Bone, int Channel)[] Translations =
    {
        ("Hips", 3), ("StickTransform", 4), ("StickHeelEnd", 5), ("StickFlex1", 6), ("StickFlex2", 7),
    };
    private const int HipsTranslation = 3;

    /// <summary>
    /// The grip: the clip doesn't move the fingers (the game layers its hands from separate hand rigs), so they're
    /// curled to hold the stick, in degrees per joint. The bind pose's slight curl turns each finger joint about its
    /// local Z towards local +Y on both hands, so the grip turns the same way.
    /// </summary>
    private static readonly (string Finger, float J1, float J2, float J3)[] Grip =
    {
        ("Index", 50, 65, 45), ("Middle", 55, 70, 45), ("Ring", 60, 70, 45), ("Pinky", 65, 70, 45), ("Thumb", 10, 25, 20),
    };

    public Skeleton Skeleton { get; }
    public AntClip Animation { get; }
    public float Duration => Animation.Frames / Fps;

    private readonly int[] _channel;   // per bone: its quaternion channel, or -1
    private readonly int[] _move;      // per bone: its translation channel, or -1
    private readonly float[] _curl;    // per bone: its grip turn (radians, before GripAmount)
    /// <summary>How far the fingers close: 0 open (the bind pose), 1 the grip.</summary>
    public float GripAmount { get; set; } = 1;
    private readonly int _hips, _hipsChannel, _root;
    private readonly Matrix4x4 _turn;
    private readonly Vector3 _drift;

    public PlayerIdle(Skeleton skeleton, AntClip clip)
    {
        if (clip.Quats < QuatChannels.Length || clip.Vectors <= HipsTranslation)
            throw new InvalidDataException($"The idle clip has {clip.Quats} rotation channels, not the expected {QuatChannels.Length}+.");
        Skeleton = skeleton;
        Animation = clip;
        _channel = Enumerable.Repeat(-1, skeleton.Count).ToArray();
        for (int c = 0; c < QuatChannels.Length; c++)
            if (skeleton.IndexOf(QuatChannels[c]) is int i and >= 0) _channel[i] = c;
        _move = Enumerable.Repeat(-1, skeleton.Count).ToArray();
        foreach (var (bone, c) in Translations)
            if (skeleton.IndexOf(bone) is int i and >= 0) _move[i] = clip.Quats + c;
        _curl = new float[skeleton.Count];
        foreach (var side in new[] { "Left", "Right" })
            foreach (var (finger, j1, j2, j3) in Grip)
            {
                float[] deg = { j1, j2, j3 };
                for (int j = 0; j < 3; j++)
                    if (skeleton.IndexOf($"{side}Hand{finger}{j + 1}") is int i and >= 0) _curl[i] = deg[j] * MathF.PI / 180;
            }
        _hips = skeleton.IndexOf("Hips");
        _root = skeleton.IndexOf("AITrajectory");
        _hipsChannel = Array.IndexOf(QuatChannels, "Hips");

        // the turn about Y that brings the first frame's hips back onto the bind pose's
        var a = Matrix4x4.CreateFromQuaternion(Q(clip.Values[0][_hipsChannel]));
        var d = Matrix4x4.Transpose(a) * skeleton.Local[_hips];
        float yaw = MathF.Atan2(d.M31 - d.M13, d.M11 + d.M33);
        var t0 = clip.Values[0][clip.Quats + HipsTranslation];
        _drift = new Vector3(t0.X, 0, t0.Z);
        // on the trajectory bone, so the hips and the stick (its children) turn together
        _turn = Matrix4x4.CreateTranslation(-_drift) * Matrix4x4.CreateRotationY(yaw);
    }

    public static PlayerIdle Load(AssetIndex index)
    {
        var res = index.GetRes(Clip) ?? throw new InvalidDataException($"No {Clip}.");
        return new PlayerIdle(Skeleton.Load(index), new AntClip(index.Read(res)));
    }

    /// <summary>Each bone's skinning matrix (inverse bind × posed model) at <paramref name="seconds"/> (looping).</summary>
    public Matrix4x4[] Skin(double seconds)
    {
        double f = seconds * Fps % Animation.Frames;
        if (f < 0) f += Animation.Frames;
        int f0 = (int)f, f1 = (f0 + 1) % Animation.Frames;
        float u = (float)(f - f0);
        var v0 = Animation.Values[f0];
        var v1 = Animation.Values[f1];

        var s = Skeleton;
        var model = new Matrix4x4[s.Count];
        var skin = new Matrix4x4[s.Count];
        for (int i = 0; i < s.Count; i++)
        {
            var local = s.Local[i];
            if (_channel[i] >= 0)
            {
                var q = Quaternion.Slerp(Q(v0[_channel[i]]), Q(v1[_channel[i]]), u);
                var t = local.Translation;
                local = Matrix4x4.CreateFromQuaternion(q);
                local.Translation = t;
            }
            if (_curl[i] != 0 && GripAmount != 0) local = Matrix4x4.CreateRotationZ(_curl[i] * GripAmount) * local;
            if (_move[i] >= 0) local.Translation = Vector3.Lerp(V(v0[_move[i]]), V(v1[_move[i]]), u);
            if (i == _root) local *= _turn;
            model[i] = s.Parents[i] < 0 ? local : local * model[s.Parents[i]];
            skin[i] = s.Inverse[i] * model[i];
        }
        return skin;
    }

    // the clip's quaternions turn row vectors the way System.Numerics' do (checked against the bind pose's spine)
    private static Quaternion Q(Vector4 v) => new(v.X, v.Y, v.Z, v.W);
    private static Vector3 V(Vector4 v) => new(v.X, v.Y, v.Z);
}

/// <summary>
/// Linear blend skinning of a mesh part with a pose's skinning matrices (a rigid part comes back as it is). Indices with
/// the high bit set (the helmet's) name the part's own bones rather than the skeleton's; they follow
/// <paramref name="attach"/> (a skeleton bone, e.g. Head), or are left out when it's -1.
/// </summary>
public static class PoseSkin
{
    public static float[] Positions(MeshPart part, Matrix4x4[] skin, int attach = -1) => Apply(part, part.Positions, skin, attach, true);
    public static float[] Normals(MeshPart part, Matrix4x4[] skin, int attach = -1) => Apply(part, part.Normals, skin, attach, false);

    private static float[] Apply(MeshPart part, float[] src, Matrix4x4[] skin, int attach, bool point)
    {
        if (part.Bones == null || src == null) return src;
        int k = part.Influences, n = part.VertexCount;
        var dst = new float[n * 3];
        for (int v = 0; v < n; v++)
        {
            var p = new Vector3(src[v * 3], src[v * 3 + 1], src[v * 3 + 2]);
            var acc = Vector3.Zero;
            for (int j = 0; j < k; j++)
            {
                float w = part.Weights[v * k + j];
                int b = part.Bones[v * k + j];
                if ((b & 0x8000) != 0) b = attach;
                if (w <= 0 || b < 0 || b >= skin.Length) continue;
                acc += w * (point ? Vector3.Transform(p, skin[b]) : Vector3.TransformNormal(p, skin[b]));
            }
            if (!point && acc.LengthSquared() > 0) acc = Vector3.Normalize(acc);
            dst[v * 3] = acc.X; dst[v * 3 + 1] = acc.Y; dst[v * 3 + 2] = acc.Z;
        }
        return dst;
    }
}
