"""Builds the IHN (Ice Hockey Network) score clock for NHL 24.

usage: python build.py BASE.swf OUT.swf [FFDEC]

BASE.swf is an NHL24Kit score clock (e.g. the ESPN+ scoreclock.gfx, inflated to FWS): its fonts and settings
constant are kept, and its ScoreClock class is replaced by ScoreClock.tpl.as (with the SN art from sn.json, traced
from the reference icon by trace.py). Write the result with: nhl27kit scorebug24 --movie OUT.swf --name IHN
"""
import os, re, subprocess, sys, tempfile

here = os.path.dirname(os.path.abspath(__file__))
base, out = sys.argv[1], sys.argv[2]
ff = sys.argv[3] if len(sys.argv) > 3 else os.path.join(here, '..', '..', 'ThirdParty', 'ffdec', 'ffdec-cli.exe')
b = open(base, 'rb').read()
i = b.find(b'#NHL24KIT_CFG#')
if i < 0 or b[i - 2:i] != b'\x80\x20':
    sys.exit('BASE has no 4096-character settings block')
cfg = '      private static const CFG:String = "#NHL24KIT_CFG#' + ' ' * (4096 - 14) + '";'
src = open(os.path.join(here, 'ScoreClock.tpl.as'), encoding='utf-8').read()
src = src.replace('__CFG__', cfg).replace('__SN_ART__', open(os.path.join(here, 'sn.json')).read().strip())
tmp = tempfile.mkdtemp(prefix='ihn-')
asf = os.path.join(tmp, 'ScoreClock.as')
open(asf, 'w', encoding='utf-8', newline='\n').write(src)
r = subprocess.run([ff, '-replace', base, out, 'ScoreClock', asf], capture_output=True, text=True)
print(r.stdout, r.stderr)
# the movie must fit the score clock's slot (162,761 bytes compressed) or the game doesn't show it: empty EA's unused sprites
sys.path.insert(0, here)
import strip
o, n = strip.strip(open(out, 'rb').read())
open(out, 'wb').write(o)
import zlib
size = len(zlib.compress(o[8:], 9)) + 8
print(f'emptied {n} unused sprites; compressed size {size:,} bytes (slot 162,761)' + ('' if size <= 162761 else '  TOO BIG: the game will not show it'))
j = o.find(b'#NHL24KIT_CFG#')
print('ok' if j > 0 and o[j - 2:j] == b'\x80\x20' else 'settings block missing!')
