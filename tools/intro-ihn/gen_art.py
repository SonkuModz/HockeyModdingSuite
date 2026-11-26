"""Turns the IHN logo SVG into drawing data for TeamIntro.tpl.as (an AS3 array literal, written to art.txt).

usage: python gen_art.py ../wipe-ihn/ihn-logo.svg art.txt

Each item is [fill, alpha, stroke colour (-1 none), stroke width, [contour, ...]] in SVG units (600 x 260); fill is a
colour or the name of one of the SVG's gradients ("silver", "bg", "ice", "gold"), drawn over the item's box like the
SVG's objectBoundingBox gradients. HOCKEY NETWORK is outlined from Arial Black, as the SVG asks (even-odd).
"""
import math, sys
from svgelements import SVG, Shape, Path, Ellipse, Group
from fontTools.ttLib import TTFont
from fontTools.pens.recordingPen import RecordingPen

def flatten(shape, seg=10):
    p = Path(shape); p.reify()
    out, cur = [], []
    for s in p:
        n = type(s).__name__
        if n == 'Move':
            if len(cur) > 2: out.append(cur)
            cur = [(s.end.x, s.end.y)]
        elif n in ('Line', 'Close'):
            if s.end is not None: cur.append((s.end.x, s.end.y))
        else:
            for k in range(1, seg + 1):
                q = s.point(k / seg); cur.append((q.x, q.y))
    if len(cur) > 2: out.append(cur)
    res = []
    for c in out:
        d = [c[0]]
        for q in c[1:]:
            if math.dist(q, d[-1]) > 1e-6: d.append(q)
        if math.dist(d[0], d[-1]) < 1e-6: d.pop()
        res.append(d)
    return res

def text_outlines(s, size, spacing, cx, baseline, font='C:/Windows/Fonts/ariblk.ttf'):
    f = TTFont(font)
    gs, cmap, hmtx = f.getGlyphSet(), f.getBestCmap(), f['hmtx']
    k = size / f['head'].unitsPerEm
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
                pts = [cur[-1]] + list(args); on = pts[0]; offs = pts[1:-1]; last = pts[-1]
                if not offs: cur.append(last)
                for i, o in enumerate(offs):
                    nxt = last if i == len(offs) - 1 else ((o[0] + offs[i + 1][0]) / 2, (o[1] + offs[i + 1][1]) / 2)
                    for t in (0.5, 1.0):
                        cur.append(((1 - t) ** 2 * on[0] + 2 * (1 - t) * t * o[0] + t * t * nxt[0],
                                    (1 - t) ** 2 * on[1] + 2 * (1 - t) * t * o[1] + t * t * nxt[1]))
                    on = nxt
            elif op == 'curveTo':
                p0 = cur[-1]; p1, p2, p3 = args
                for t in (0.5, 1.0):
                    cur.append(tuple((1 - t) ** 3 * p0[i] + 3 * (1 - t) ** 2 * t * p1[i] + 3 * (1 - t) * t * t * p2[i] + t ** 3 * p3[i] for i in (0, 1)))
            elif op in ('closePath', 'endPath'):
                if cur and math.dist(cur[0], cur[-1]) < 1e-6: cur.pop()
                if len(cur) > 2: contours.append([(x + px * k, baseline - py * k) for px, py in cur])
                cur = []
        x += a
    return contours

svg = SVG.parse(sys.argv[1])
shapes = [e for e in svg.elements() if isinstance(e, Shape) and not isinstance(e, Group)]
P = [flatten(s, 12 if isinstance(s, Ellipse) else 8) for s in shapes]
# order in the SVG: 0 shield rim, 1 shield inner, 2 top band, 3-4 speed lines, 5 stick, 6 blade accent,
# 7 puck bottom, 8 puck side, 9 puck top, 10 I, 11 gold cap, 12 H, 13 N, 14 banner
style = ['silver', 'bg', 'ice', (0x00D2FF, .85), (0x00D2FF, .55), 'silver', 0x00D2FF,
         (0x020617, 1, 0x64748B, 1.5), 0x0F172A, (0x334155, 1, 0x94A3B8, 1.5), 'silver', 'gold', 'silver', 'silver', 'ice']
assert len(P) == len(style), len(P)
def num(v):
    s = f'{v:.1f}'.rstrip('0').rstrip('.')
    return s if s not in ('-0', '') else '0'
def item(fill, alpha, stroke, sw, contours):
    f = f'"{fill}"' if isinstance(fill, str) else str(fill)
    cs = ','.join('[' + ','.join(num(c) for p in con for c in p) + ']' for con in contours)
    return f"[{f},{alpha:g},{stroke},{num(sw)},[{cs}]]"
items = []
for st, con in zip(style, P):
    if not isinstance(st, tuple): st = (st,)
    fill = st[0]; alpha = st[1] if len(st) > 1 else 1; stroke = st[2] if len(st) > 2 else -1; sw = st[3] if len(st) > 3 else 0
    items.append(item(fill, alpha, stroke, sw, con))
items.append(item(0xFFFFFF, 1, -1, 0, text_outlines('HOCKEY NETWORK', 12.5, 3.5, 307, 199)))
out = '[' + ',\n'.join(items) + ']'
open(sys.argv[2], 'w').write(out)
print(len(out), 'chars,', sum(len(c) for c in P), 'contours')
