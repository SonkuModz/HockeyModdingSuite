"""Writes the UI files of the IHN live shots feed (see ihnshots.lua).

usage: python build.py LINEFATIGUE_DIR OUTDIR [SCORECLOCK_DIR]
LINEFATIGUE_DIR holds the game's linefatigueaway.vvm and linefatigueaway.layout (features/overlays/vvms/linefatigue).
The script runs as a second lua VM of the away line-on-the-ice overlay, which the game loads for the whole time the
HUD is up (adding a VM to scoreclock.vvm itself made the clock disappear: it changes how that VVM loads). Install
(game closed, always --game for NHL 24) with legacy-set:
  OUTDIR/widgettester.lua        -> nhl/ui/features/debug/vvms/widgettester/widgettester.lua (unused debug file)
  OUTDIR/linefatigueaway.vvm     -> nhl/ui/features/overlays/vvms/linefatigue/linefatigueaway.vvm (adds the VM)
  OUTDIR/linefatigueaway.layout  -> nhl/ui/features/overlays/vvms/linefatigue/linefatigueaway.layout (hidden label)
"""
import json, os, shutil, sys

src, out = sys.argv[1], sys.argv[2]
here = os.path.dirname(os.path.abspath(__file__))
os.makedirs(out, exist_ok=True)
shutil.copyfile(os.path.join(here, 'ihnshots.lua'), os.path.join(out, 'widgettester.lua'))

vvm = json.load(open(os.path.join(src, 'linefatigueaway.vvm'), encoding='utf-8'))
vvm['vms'] = [v for v in vvm['vms'] if 'widgettester' not in v[1]] + [['lua', '/features/debug/vvms/widgettester/widgettester.lua']]
open(os.path.join(out, 'linefatigueaway.vvm'), 'w', newline='\n').write(json.dumps(vvm))

lay = json.load(open(os.path.join(src, 'linefatigueaway.layout'), encoding='utf-8'))
# IhnMark: fixed text the movie looks for (a binding can't blank it); IhnShots right after it gets the script's text
lay['children'] = [c for c in lay['children'] if c.get('name') not in ('IhnShots', 'IhnMark')] + [{
    'commonName': 'Label', 'name': 'IhnMark', 'styles': ['NHL_BODY_7_ALT_ITALIC'], 'text': 'IHNMARK',
    'x': -3000.0, 'y': 40.0, 'width': 2000.0, 'height': 30.0, 'alpha': 0.0, 'resizeType': 'NONE', 'fontSize': 12.0}, {
    'commonName': 'Label', 'name': 'IhnShots', 'styles': ['NHL_BODY_7_ALT_ITALIC'], 'text': 'IHNSOG',
    'x': -3000.0, 'y': 0.0, 'width': 2000.0, 'height': 30.0, 'alpha': 0.0, 'resizeType': 'NONE', 'fontSize': 12.0,
    'bindings': [{'binding': 'ihn_shots', 'property': 'text'}]}]
open(os.path.join(out, 'linefatigueaway.layout'), 'w', newline='\n').write(json.dumps(lay, indent=1))
for f in sorted(os.listdir(out)):
    print(f, os.path.getsize(os.path.join(out, f)), 'bytes')

# also a marker + feed label in the score clock's own layout (that layout loads fine with extra labels; whether a
# binding published by another VVM's script reaches it is what this tests)
if len(sys.argv) > 3:
    sc = json.load(open(os.path.join(sys.argv[3], 'scoreclockoverlay.layout'), encoding='utf-8'))
    sc.pop('authoringData', None)
    sc['children'] = [c for c in sc['children'] if c.get('name') not in ('IhnShots', 'IhnMark')] + [
        c for c in lay['children'] if c.get('name') in ('IhnMark', 'IhnShots')]
    open(os.path.join(out, 'scoreclockoverlay.layout'), 'w', newline='\n').write(json.dumps(sc, indent=1))
    print('scoreclockoverlay.layout', os.path.getsize(os.path.join(out, 'scoreclockoverlay.layout')), 'bytes')
