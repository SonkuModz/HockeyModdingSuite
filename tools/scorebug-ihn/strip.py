"""Empties the leftover timeline symbols of EA's own score clock in a built IHN movie (FWS), so it fits its slot.

usage: python strip.py IN.swf OUT.swf

The IHN ScoreClock draws everything in code, but the movie has to fit the score clock's slot in the game data
(162,761 bytes compressed): a bigger one is moved, and the game then didn't show the score clock at all (2026-11-25).
Sprites that only the symbols in DROP use are emptied (one empty frame; the id and SymbolClass entry stay). The game
itself uses some linked symbols: with every sprite emptied, shootout mode crashed (2026-11-25), so every other linked
symbol and everything placed inside it stays as it was. Fonts, text, shapes, images, the ABC and every GFx tag are kept.
"""
import struct, sys

# EA's purely decorative animations are emptied. Every other linked symbol (shootout, clock, period, practice, team
# pills, scores, panels...), and everything placed inside one, stays whole: the game uses some (shootout mode crashed
# with all of them emptied)
DROP = {'mBgGlitchAnimation', 'overlays_common_elements_fla_fla.mAnimatedMaskAway_6',
        'overlays_common_elements_fla_fla.mAnimatedBoxesAway_12', 'overlays_common_elements_fla_fla.mAnimatedTimelineArrows_17',
        'overlays_common_elements_fla_fla.mCrossesAnimated_18', 'score_clock_fla.mAnimatedLine_01_16',
        'score_clock_fla.mAnimatedLine_02_13', 'score_clock_fla.mAnimatedLine_03_17', 'score_clock_fla.mAnimatedLine_04_14',
        'score_clock_fla.mPillAnimatedLines_12', 'score_clock_fla.mPillAnimatedLinesMask_44', 'score_clock_fla.mTickerMask_29'}

def tags(b, p=None, end=None):
    if p is None:
        nbits = b[8] >> 3; rect = (5 + 4 * nbits + 7) // 8; p = 8 + rect + 4
    hdr = p
    end = len(b) if end is None else end
    out = []
    while p < end:
        s = p; h = struct.unpack_from('<H', b, p)[0]; p += 2; t = h >> 6; L = h & 63
        if L == 63: L = struct.unpack_from('<I', b, p)[0]; p += 4
        out.append((t, b[p:p + L], b[s:p + L])); p += L
        if t == 0: break
    return hdr, out

def tag(t, body):
    if len(body) < 63 and t not in (6, 21, 35, 20, 36, 90):
        return struct.pack('<H', t << 6 | len(body)) + body
    return struct.pack('<HI', t << 6 | 63, len(body)) + body

def children(body):
    """Character ids a sprite places (PlaceObject2/3)."""
    _, ts = tags(body, 4, len(body))
    ids = set()
    for t, b, _ in ts:
        if t == 26 and b[0] & 0x02:
            ids.add(struct.unpack_from('<H', b, 3)[0])
        elif t == 70 and b[0] & 0x02:
            p = 4
            if b[1] & 0x08: p = b.index(b'\0', p) + 1
            ids.add(struct.unpack_from('<H', b, p)[0])
    return ids

def linked(ts):
    names = {}
    for t, body, _ in ts:
        if t == 76:
            n = struct.unpack_from('<H', body)[0]; p = 2
            for _ in range(n):
                cid = struct.unpack_from('<H', body, p)[0]; e = body.index(b'\0', p + 2)
                names[body[p + 2:e].decode('latin-1')] = cid; p = e + 1
    return names

def strip(b, drop=DROP):
    if b[:3] != b'FWS': raise SystemExit('need an uncompressed (FWS) movie')
    hdr, ts = tags(b)
    sprites = {struct.unpack_from('<H', body)[0]: body for t, body, _ in ts if t == 39}
    names = linked(ts)
    kept = set(); todo = [c for k, c in names.items() if k not in drop and c != 0]
    while todo:
        c = todo.pop()
        if c in kept or c not in sprites: continue
        kept.add(c); todo.extend(children(sprites[c]))
    out = []; n = 0
    for t, body, raw in ts:
        if t == 39 and struct.unpack_from('<H', body)[0] not in kept:
            sid = struct.unpack_from('<H', body)[0]
            out.append(tag(39, struct.pack('<HH', sid, 1) + struct.pack('<H', 1 << 6) + struct.pack('<H', 0)))
            n += 1
            continue
        out.append(raw)
    body = b[8:hdr] + b''.join(out)
    return b'FWS' + b[3:4] + struct.pack('<I', 8 + len(body)) + body, n

if __name__ == '__main__':
    data, n = strip(open(sys.argv[1], 'rb').read())
    open(sys.argv[2], 'wb').write(data)
    print(f'emptied {n} sprites')
