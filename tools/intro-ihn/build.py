"""Builds the IHN pre-game team intro (team_intro.gfx) for NHL 24, design "B Broadcast Band".

usage: python build.py BASE.swf OUT.swf [FFDEC]

BASE.swf is the game's team_intro.gfx inflated to FWS. Its document class overlays.teamintro.TeamIntro is replaced by
TeamIntro.tpl.as (with the IHN logo from art.txt, made by gen_art.py), and every EA timeline symbol, shape and image is
dropped: the new class draws everything in code. The movie must compress to at most 16,815 bytes (the file's room in
the game data; a moved file is not shown). Install with: nhl27kit ui24 nhl/ui/swf_assets/kettle/overlays/teamintro/team_intro.gfx --movie OUT.swf --name "Team intro: IHN"
"""
import os, struct, subprocess, sys, tempfile, zlib

here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(here, '..', 'scorebug-ihn'))
import strip as sw

ROOM = 16815
base, out = sys.argv[1], sys.argv[2]
ff = sys.argv[3] if len(sys.argv) > 3 else os.path.join(here, '..', '..', 'ThirdParty', 'ffdec', 'ffdec-cli.exe')
src = open(os.path.join(here, 'TeamIntro.tpl.as'), encoding='utf-8').read()
src = src.replace('__ART__', open(os.path.join(here, 'art.txt')).read().strip())
tmp = tempfile.mkdtemp(prefix='ihn-intro-')
asf = os.path.join(tmp, 'TeamIntro.as')
open(asf, 'w', encoding='utf-8', newline='\n').write(src)
mid = os.path.join(tmp, 'replaced.swf')
r = subprocess.run([ff, '-replace', base, mid, 'overlays.teamintro.TeamIntro', asf], capture_output=True, text=True)
print(r.stdout.strip(), r.stderr.strip())
if not os.path.exists(mid): sys.exit('ffdec did not write the movie')

b = open(mid, 'rb').read()
hdr, ts = sw.tags(b)
KEEP = {69, 9, 1000, 86, 82, 72, 1, 0}
body = []
for t, tb, raw in ts:
    if t == 76:  # SymbolClass: only the document class (id 0)
        n = struct.unpack_from('<H', tb)[0]; p = 2; keep = []
        for _ in range(n):
            cid = struct.unpack_from('<H', tb, p)[0]; e = tb.index(b'\0', p + 2)
            if cid == 0: keep.append(tb[p:e + 1])
            p = e + 1
        body.append(sw.tag(76, struct.pack('<H', len(keep)) + b''.join(keep)))
        print('document class:', [k[2:-1].decode() for k in keep])
    elif t in KEEP:
        body.append(raw)
o = bytearray(b[:hdr] + b''.join(body))
struct.pack_into('<I', o, 4, len(o))
open(out, 'wb').write(o)
size = len(zlib.compress(bytes(o[8:]), 9)) + 8
print(f'{len(o):,} bytes; compressed {size:,} (room {ROOM:,})' + ('' if size <= ROOM else '  TOO BIG: the game will not show it'))
