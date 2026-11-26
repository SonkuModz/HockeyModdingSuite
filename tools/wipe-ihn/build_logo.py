"""Builds the IHN logo geometry for NHL 24's Play Now flat replay wipe (the "EA SPORTS" wipe) from the user's SVG.

usage: python build_logo.py LOGO.svg ORIG_DIR OUT_DIR

ORIG_DIR holds OBJ exports of the game's meshes (alt.obj, medallion.obj, easports.obj from `nhl27kit mesh NAME --obj`),
used for each section's UV mapping (a planar map of position), triangle winding and depth. Writes one geometry file
per mesh (MeshWriter24.Load format) for `nhl27kit mesh24 NAME --geo FILE`, plus preview.png (front view).

The wipe draws its logo as flat shapes whose colours come from the wipe's materials, so the logo is a one-colour
cut-out: part 0 (white) = the shield (fill on the medallion, rim on the alt ring), part 1 (dark) = I H N,
part 2 (dark) = stick, speed lines, puck, and the banner with HOCKEY NETWORK cut out. Shells (the extruded sides)
are blanked. Each section's twin (the position + UV copy after it) gets the same shapes.
"""
import json, math, os, sys
import numpy as np
from svgelements import SVG, Shape, Path, Polygon, Ellipse, Rect, Text, Group
from fontTools.ttLib import TTFont
from fontTools.pens.recordingPen import RecordingPen

SVG_FILE, ORIG, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
os.makedirs(OUT, exist_ok=True)

# SVG -> mesh space: centred on the shield, y up, 470 SVG units across -> 1.88 (the medallion is 1.89 across)
CX, CY, S = 300.0, 132.5, 0.004

def to_mesh(pts):
    return [((x - CX) * S, -(y - CY) * S) for x, y in pts]

# ---------------------------------------------------------------- shapes from the SVG
def flatten(shape, seg=10):
    """Closed point lists of a transformed svgelements shape."""
    p = Path(shape)
    p.reify()
    out, cur = [], []
    for s in p:
        name = type(s).__name__
        if name == 'Move':
            if len(cur) > 2: out.append(cur)
            cur = [(s.end.x, s.end.y)]
        elif name in ('Line', 'Close'):
            if s.end is not None: cur.append((s.end.x, s.end.y))
        else:
            for k in range(1, seg + 1):
                q = s.point(k / seg); cur.append((q.x, q.y))
    if len(cur) > 2: out.append(cur)
    clean = []
    for c in out:
        d = [c[0]]
        for q in c[1:]:
            if math.dist(q, d[-1]) > 1e-6: d.append(q)
        if math.dist(d[0], d[-1]) < 1e-6: d.pop()
        clean.append(d)
    return clean

svg = SVG.parse(SVG_FILE)
shapes = [e for e in svg.elements() if isinstance(e, Shape) and not isinstance(e, Group)]
texts = [e for e in svg.elements() if isinstance(e, Text)]
print(len(shapes), 'shapes,', len(texts), 'text')
P = [flatten(s, 16 if isinstance(s, Ellipse) else 10) for s in shapes]
# order in the SVG: 0 shield rim, 1 shield inner, 2 top band, 3-4 speed lines, 5 stick, 6 blade accent,
# 7 puck bottom, 8 puck side, 9 puck top, 10 I, 11 gold cap, 12 H, 13 N, 14 banner
shield_outer, shield_inner = P[0][0], P[1][0]
dark2 = [P[3][0], P[4][0], P[5][0], P[6][0], P[7][0], P[8][0], P[9][0]]
dark1 = [P[10][0], P[12][0], P[13][0]]
banner = P[14][0]

# ---------------------------------------------------------------- HOCKEY NETWORK outlines (Arial Black, as the SVG asks)
def text_outlines(s, size, spacing, cx, baseline, font='C:/Windows/Fonts/ariblk.ttf'):
    f = TTFont(font)
    gs, cmap, hmtx = f.getGlyphSet(), f.getBestCmap(), f['hmtx']
    k = size / f['head'].unitsPerEm
    # SVG letter-spacing adds after every glyph (also the last one); text-anchor middle centres that whole advance
    adv = [hmtx[cmap[ord(ch)]][0] * k + spacing for ch in s]
    x = cx - sum(adv) / 2
    contours = []
    for ch, a in zip(s, adv):
        pen = RecordingPen(); gs[cmap[ord(ch)]].draw(pen)
        cur = []
        for op, args in pen.value:
            if op == 'moveTo': cur = [args[0]]
            elif op == 'lineTo': cur.append(args[0])
            elif op == 'qCurveTo':
                pts = [cur[-1]] + list(args)
                # implied on-curve points between consecutive off-curve points
                on = pts[0]
                offs = pts[1:-1]; last = pts[-1]
                segs = []
                for i, o in enumerate(offs):
                    nxt = last if i == len(offs) - 1 else ((o[0] + offs[i + 1][0]) / 2, (o[1] + offs[i + 1][1]) / 2)
                    segs.append((on, o, nxt)); on = nxt
                if not offs: cur.append(last)
                for p0, p1, p2 in segs:
                    for t in (0.25, 0.5, 0.75, 1.0):
                        cur.append(((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0],
                                    (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]))
            elif op == 'curveTo':
                p0 = cur[-1]; p1, p2, p3 = args
                for t in (0.25, 0.5, 0.75, 1.0):
                    cur.append(tuple((1 - t) ** 3 * p0[i] + 3 * (1 - t) ** 2 * t * p1[i] + 3 * (1 - t) * t * t * p2[i] + t ** 3 * p3[i] for i in (0, 1)))
            elif op in ('closePath', 'endPath'):
                if math.dist(cur[0], cur[-1]) < 1e-6: cur.pop()
                if len(cur) > 2: contours.append([(x + px * k, baseline - py * k) for px, py in cur])
                cur = []
        x += a
    return contours

glyphs = text_outlines('HOCKEY NETWORK', 12.5, 3.5, 307, 195 + 4)

# ---------------------------------------------------------------- triangulation (ear clipping, holes bridged in)
def area(c):
    return 0.5 * sum(c[i][0] * c[(i + 1) % len(c)][1] - c[(i + 1) % len(c)][0] * c[i][1] for i in range(len(c)))

def orient(c, ccw):
    return c if (area(c) > 0) == ccw else c[::-1]

def cross(o, a, b):
    return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])

def seg_cross(p1, p2, p3, p4):
    d1, d2, d3, d4 = cross(p3, p4, p1), cross(p3, p4, p2), cross(p1, p2, p3), cross(p1, p2, p4)
    return ((d1 > 1e-12 and d2 < -1e-12) or (d1 < -1e-12 and d2 > 1e-12)) and ((d3 > 1e-12 and d4 < -1e-12) or (d3 < -1e-12 and d4 > 1e-12))

def inside(pt, c):
    x, y = pt; r = False
    for i in range(len(c)):
        a, b = c[i], c[i - 1]
        if (a[1] > y) != (b[1] > y) and x < (b[0] - a[0]) * (y - a[1]) / (b[1] - a[1]) + a[0]: r = not r
    return r

def bridge(outer, holes):
    """One simple polygon (CCW) from an outer ring with holes (CW), joined by zero-width bridges."""
    poly = orient(outer, True)
    for h in sorted([orient(h, False) for h in holes], key=lambda h: -max(p[0] for p in h)):
        mi = max(range(len(h)), key=lambda i: h[i][0]); m = h[mi]
        best = None
        for j in sorted(range(len(poly)), key=lambda j: math.dist(poly[j], m)):
            v = poly[j]
            edges = [(poly[i], poly[(i + 1) % len(poly)]) for i in range(len(poly))] + [(h[i], h[(i + 1) % len(h)]) for i in range(len(h))]
            if not any(seg_cross(m, v, a, b) for a, b in edges):
                mid = ((m[0] + v[0]) / 2, (m[1] + v[1]) / 2)
                if inside(mid, poly): best = j; break
        if best is None: raise SystemExit('no bridge for a hole')
        ring = h[mi:] + h[:mi]
        poly = poly[:best + 1] + ring + [ring[0], poly[best]] + poly[best + 1:]
    return poly

def earclip(poly):
    """Triangles (index triples into poly) of a simple CCW polygon (bridges allowed)."""
    idx = list(range(len(poly))); tris = []
    def is_ear(i0, i1, i2):
        a, b, c = poly[i0], poly[i1], poly[i2]
        if cross(a, b, c) <= 1e-14: return False
        for k in idx:
            if k in (i0, i1, i2): continue
            p = poly[k]
            if p == a or p == b or p == c: continue
            if cross(a, b, p) >= 0 and cross(b, c, p) >= 0 and cross(c, a, p) >= 0: return False
        return True
    guard = 0
    while len(idx) > 3 and guard < 100000:
        guard += 1
        n = len(idx)
        for k in range(n):
            i0, i1, i2 = idx[k - 1], idx[k], idx[(k + 1) % n]
            if is_ear(i0, i1, i2):
                tris.append((i0, i1, i2)); idx.pop(k); break
        else:
            # degenerate leftovers (collinear bridge points): drop the flattest vertex
            k = min(range(n), key=lambda k: abs(cross(poly[idx[k - 1]], poly[idx[k]], poly[idx[(k + 1) % n]])))
            if abs(cross(poly[idx[k - 1]], poly[idx[k]], poly[idx[(k + 1) % n]])) > 1e-9:
                raise SystemExit(f'ear clipping stuck with {n} vertices left')
            idx.pop(k)
    if len(idx) == 3 and cross(poly[idx[0]], poly[idx[1]], poly[idx[2]]) > 1e-14: tris.append(tuple(idx))
    return tris

def nest(contours):
    """Groups contours by even-odd depth: [(outer, [holes])]."""
    depth = []
    for i, c in enumerate(contours):
        d = sum(1 for j, o in enumerate(contours) if j != i and inside(c[0], o) and abs(area(o)) > abs(area(c)))
        depth.append(d)
    groups = []
    for i, c in enumerate(contours):
        if depth[i] % 2: continue
        holes = [h for j, h in enumerate(contours) if depth[j] == depth[i] + 1 and inside(h[0], c)]
        groups.append((c, holes))
    return groups

class Mesh:
    def __init__(self): self.pos, self.tri = [], []
    def add(self, contours):
        """Fills even-odd: contours in SVG space."""
        for outer, holes in nest(contours):
            poly = bridge(outer, holes)
            base = len(self.pos)
            # bridges repeat points; keep them as separate vertices (simple and safe)
            self.pos += poly
            self.tri += [(base + a, base + b, base + c) for a, b, c in earclip(poly)]
        return self

# ---------------------------------------------------------------- the pieces (SVG space)
fill = Mesh().add([shield_outer])
rim = Mesh().add([shield_outer, shield_inner])
letters = Mesh()
for c in dark1: letters.add([c])
rest = Mesh()
for c in dark2: rest.add([c])
rest.add([banner] + glyphs)
for nm, m in [('shield fill', fill), ('shield rim', rim), ('IHN', letters), ('stick/banner', rest)]:
    print(f'{nm}: {len(m.pos)} verts, {len(m.tri)} tris')

# ---------------------------------------------------------------- the game's sections: UV map, winding, depth
def read_obj(f):
    V, T, groups = [], [], []
    for line in open(f):
        p = line.split()
        if not p: continue
        if p[0] == 'v': V.append(tuple(map(float, p[1:4])))
        elif p[0] == 'vt': T.append(tuple(map(float, p[1:3])))
        elif p[0] == 'o': groups.append([' '.join(p[1:]), []])
        elif p[0] == 'f': groups[-1][1].append([int(x.split('/')[0]) - 1 for x in p[1:4]])
    return V, T, groups

def section_info(objfile, gi):
    V, T, groups = read_obj(objfile)
    fs = groups[gi][1]
    vs = sorted({i for f in fs for i in f})
    A = np.array([[V[i][0], V[i][1], 1] for i in vs])
    has_uv = len(T) == len(V)
    if has_uv:
        U = np.array([T[i] for i in vs])
        cu = np.linalg.lstsq(A, U[:, 0], rcond=None)[0]; cv = np.linalg.lstsq(A, U[:, 1], rcond=None)[0]
        err = max(np.abs(A @ cu - U[:, 0]).max(), np.abs(A @ cv - U[:, 1]).max())
    else:
        cu = cv = None; err = 0
    sgn = [cross(V[a][:2], V[b][:2], V[c][:2]) for a, b, c in fs]
    ccw = sum(1 for s in sgn if s > 0) >= len(sgn) / 2
    z = float(np.median([V[i][2] for i in vs]))
    return dict(cu=cu, cv=cv, err=err, ccw=ccw, z=z)

def emit(m, info):
    pts = to_mesh(m.pos)
    pos, uv = [], []
    for x, y in pts:
        pos += [round(x, 5), round(y, 5), info['z']]
        if info['cu'] is not None:
            uv += [float(info['cu'] @ [x, y, 1]), float(info['cv'] @ [x, y, 1])]
    tri = []
    for a, b, c in m.tri:
        # SVG space is y-down: after the flip a CCW triangle in SVG space is CW in mesh space
        ccw_mesh = cross(pts[a], pts[b], pts[c]) > 0
        tri += [a, b, c] if ccw_mesh == info['ccw'] else [a, c, b]
    d = {'pos': pos, 'tri': tri}
    if uv: d['uv'] = [round(u, 5) for u in uv]
    return d

def build(objname, plan, out):
    """plan: list of (section index, piece or None, twin index)."""
    sections = {}
    objfile = os.path.join(ORIG, objname)
    _, _, groups = read_obj(objfile)
    for gi, piece in plan:
        info = section_info(objfile, gi)
        print(f'  {objname} {gi} {groups[gi][0] or "(twin)"}: uv fit error {info["err"]:.4f}, ccw {info["ccw"]}, z {info["z"]}')
        sections[f'0:{gi}'] = None if piece is None else emit(piece, info)
    json.dump({'sections': sections}, open(os.path.join(OUT, out), 'w'))

# OBJ groups follow the section order (empty sections are skipped by the reader; none are empty here)
build('medallion.obj', [(0, fill), (1, None), (2, fill), (3, None)], 'medallion.json')
build('easports.obj', [(0, letters), (1, None), (2, rest), (3, None), (4, letters), (5, None), (6, rest), (7, None)], 'easports.json')
build('alt.obj', [(0, rim), (1, None), (2, letters), (3, None), (4, rest), (5, None), (6, rim), (7, None), (8, letters), (9, None), (10, rest), (11, None)], 'alt.json')

# ---------------------------------------------------------------- preview (front view of what was emitted)
from PIL import Image, ImageDraw
W = 1200; Sc = 600
img = Image.new('RGB', (W, W // 2), (16, 16, 16)); d = ImageDraw.Draw(img)
for m, col in [(fill, (255, 255, 255)), (letters, (16, 16, 16)), (rest, (16, 16, 16))]:
    pts = to_mesh(m.pos)
    for a, b, c in m.tri:
        d.polygon([(W / 2 + pts[i][0] * Sc, W / 4 - pts[i][1] * Sc) for i in (a, b, c)], fill=col)
img.save(os.path.join(OUT, 'preview.png'))
print('ok')
