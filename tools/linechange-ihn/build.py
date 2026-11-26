"""IHN navy/gold line change menu for NHL 24 (linechangedisplay.layout; used by the four Offense/Defense LineChange overlays).

usage: python build.py SRC.layout OUT.layout

Recolours EA's layout in place: nhl.overlays.linechange.LineChangeElement still finds every node and keeps resizing
PanelBackground (206 + 38 * lines), the edges and the bottom line, moving the selected-line highlight and StrategyGroup.
Fills become background_white with a colour; the header keeps the team-colour glow; gold rim, gold header lines, gold
selected-line highlight, gold strategy dividers (added inside StrategyGroup so they move with it) and a small IHN tag.
linechangestrategies.layout is shared with other strategy panels, so it is left alone.
"""
import json, sys

NAVY, NAVY2, GOLD, GOLD2 = '0x0B1A33', '0x13294B', '0xC9A54C', '0xE8CB7A'
src, out = sys.argv[1], sys.argv[2]
d = json.load(open(src, encoding='utf-8'))
d.pop('authoringData', None)
nodes = {}


def walk(n):
    for c in n.get('children', []):
        if 'name' in c: nodes[c['name']] = c
        walk(c)


walk(d)


def fill(name, color, alpha, **kw):
    n = nodes[name]
    n['type'] = 'background_white'
    n['color'] = color
    n['alpha'] = alpha
    n.pop('styles', None)
    n.update(kw)


fill('PanelBackground', NAVY, 0.93)
fill('HeaderBackground', NAVY2, 0.95)
nodes['HeaderGridGloss']['alpha'] = 0.12
fill('TeamTopLine', GOLD, 1.0, height=2.0, x=0.0, width=251.0)
fill('HeaderTopLine', GOLD, 1.0, height=2.0, y=34.0, x=0.0, width=251.0)
fill('PanelEdgeLeft', GOLD, 1.0, width=2.0, x=0.0)
fill('PanelEdgeRight', GOLD, 1.0, width=2.0, x=249.0)
fill('PanelBottomLine', GOLD, 1.0, height=2.0, x=0.0, width=251.0)
fill('SelectedLineBackground', GOLD, 0.55)
nodes['TeamName']['width'] = 180.0

base = next(c for c in d['children'] if c.get('name') == 'PanelBase')
base['children'].append({'commonName': 'Label', 'name': 'IhnTag', 'styles': ['NHL_BODY_7_ALT_ITALIC'], 'fontColor': GOLD2,
                         'text': 'IHN', 'x': 200.0, 'y': 7.0, 'width': 44.0, 'height': 24.0, 'fontSize': 18.0,
                         'textAlignH': 'RIGHT'})
lines = next(c for c in d['children'] if c.get('name') == 'LinesGroup')
# a gold bar on the left edge of the highlight cannot follow it (only SelectedLineBackground moves), so none
strat = next(c for c in d['children'] if c.get('name') == 'StrategyGroup')
for i, y in enumerate((0.0, 85.0)):
    strat['children'].append({'commonName': 'Asset', 'name': f'IhnStratDiv{i + 1}', 'type': 'background_white', 'color': GOLD,
                              'alpha': 0.7 if i else 1.0, 'x': 0.0, 'y': y, 'width': 251.0, 'height': 2.0})

txt = json.dumps(d, indent=1)
open(out, 'w', newline='\n', encoding='utf-8').write(txt)
print(out, len(txt.encode()), 'bytes')
