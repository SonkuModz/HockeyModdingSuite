"""IHN top-bar main menu for NHL 24 (concept D in previews/ihn/menu).

usage: python build.py SRC_DIR OUT_DIR
SRC_DIR holds EA's files at their legacy paths (nhl/ui/features/...), read fresh with
  nhl27kit legacy --collector globals/chunkfilecollectoruicommon_nhluidata --filter X --out SRC_DIR --game G
OUT_DIR gets the same paths; install each with legacy-set (game closed, --game for NHL 24).

  mainmenubackground.layout  navy top bar (two strips), gold lines, a navy fade under the button hints
  backgroundwidget.layout    EA's video scrim turned down so the 1123 video shows
  mainmenu.layout            NHL 24 logo moved up into the top strip, IHN tag on the right
  tabsviewmanager.layout     tabs in the top strip (y 18), the sections moved up so the mode list sits in the bar
  mainmenusection.layout     the mode list runs left to right and scrolls (steps copied from EA's trade list),
                             d-pad left/right moves through it
  listmenurow.layout         smaller titles for a bar, section headers hidden (no stagger), gold highlight,
                             selected tile no wider than 520
  tabbutton.layout           selected tab gold with navy text (this button is shared by every tab bar)
"""
import json, os, sys

NAVY, NAVY2, GOLD, GOLD2 = '0x0B1A33', '0x13294B', '0xC9A54C', '0xE8CB7A'
src, out = sys.argv[1], sys.argv[2]
UI = 'nhl/ui/features/'


def load(p):
    return json.load(open(os.path.join(src, UI + p), encoding='utf-8'))


def save(p, d):
    d.pop('authoringData', None)
    dst = os.path.join(out, UI + p)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    txt = json.dumps(d, indent=1)
    open(dst, 'w', newline='\n', encoding='utf-8').write(txt)
    print(UI + p, len(txt.encode()), 'bytes')


def nodes(d):
    found = {}

    def walk(n):
        for c in n.get('children', []):
            if 'name' in c: found.setdefault(c['name'], c)
            walk(c)
    walk(d)
    return found


def box(name, x, y, w, h, color, alpha=1.0):
    return {'commonName': 'Asset', 'name': name, 'type': 'background_white', 'color': color, 'alpha': alpha,
            'x': float(x), 'y': float(y), 'width': float(w), 'height': float(h)}


# background: bars under everything (this view is drawn first)
bg = load('mainmenu/vvms/mainmenubackground.layout')
bg['children'] = [c for c in bg['children'] if not c.get('name', '').startswith('Ihn')] + [
    box('IhnBarLow', 0, 0, 1920, 178, NAVY, 0.92),
    box('IhnBarTop', 0, 0, 1920, 92, NAVY2, 1.0),
    box('IhnBarLine1', 0, 92, 1920, 2, GOLD, 0.6),
    box('IhnBarLine2', 0, 176, 1920, 3, GOLD, 1.0),
] + [box(f'IhnFoot{i}', 0, 1080 - 96 + i * 24, 1920, 24, NAVY, a) for i, a in enumerate((0.2, 0.4, 0.6, 0.8))]
save('mainmenu/vvms/mainmenubackground.layout', bg)

bw = load('homepage/vvms/backgroundwidget.layout')
nodes(bw)['MainMenuVideoScrim']['alpha'] = 0.25
save('homepage/vvms/backgroundwidget.layout', bw)

mm = load('mainmenu/vvms/mainmenu.layout')
n = nodes(mm)
n['NHLLogo']['marginTop'] = 17.0
n['EAAccessLogoTop']['marginTop'] = 17.0
mm['children'] = [c for c in mm['children'] if c.get('name') != 'IhnTag'] + [{
    'commonName': 'Label', 'name': 'IhnTag', 'styles': ['NHL_BODY_7_ALT_ITALIC'], 'fontColor': GOLD2, 'text': 'IHN',
    'x': 1700.0, 'y': 30.0, 'width': 127.0, 'height': 32.0, 'fontSize': 24.0, 'textAlignH': 'RIGHT'}]
save('mainmenu/vvms/mainmenu.layout', mm)

tv = load('tabsviewmanager/tabsviewmanager.layout')
n = nodes(tv)
n['TabGroup']['x'] = 330.0
n['TabGroup']['y'] = 18.0
n['TabsViewSections']['y'] = 58.0          # + MenuContainer y 50 = the mode bar at 108
n['TabsViewSections']['width'] = 1920      # EA's 1200 clips (to bounds) the bar after ~4 modes
save('tabsviewmanager/tabsviewmanager.layout', tv)

sec = load('widgets/mainmenu/mainmenusection.layout')
n = nodes(sec)
lv = n['ListCollectionView']
lv['orientation'] = 'HORIZONTAL'
lv['width'] = 1740.0
lv['height'] = 150.0
lv['clipToBounds'] = True
lv['checkItemWidths'] = True               # without it every row sits at x 0 (first in-game test)
lv['layoutSteps'] = [
    {'type': 'PositionItemsInLine', 'data': {'paddingBetweenItems': 34.0}},
    {'type': 'CalculateLayoutSize'},
    {'type': 'CalculateWindowPercent'},
    {'type': 'ScrollSelectionToCenter'},
    {'type': 'ClampScrollPercent'},
    {'type': 'PositionMountByScroll'},
    {'type': 'AnimateValuesOnMount', 'data': {'speed': 0.4, 'properties': ['x']}},
]
for b in lv['behaviors']:
    if b.get('name') == 'Next': b['inputCode'] = 'META_RIGHT'
    if b.get('name') == 'Previous': b['inputCode'] = 'META_LEFT'
n['DynamicMessageGroup']['marginTop'] = 160   # the For You card under the bar
save('widgets/mainmenu/mainmenusection.layout', sec)

row = load('widgets/mainmenu/listmenurow.layout')
sd = row['style-definitions']
sd['DynamicWidth'] = {'width': 'AUTO', 'maxWidth': 520}
sd['FixedWidth'] = {'width': 'AUTO', 'maxWidth': 520}
sd['IHN_TITLE_ON'] = {'fontFace': '$NHLHeaderAlternate', 'fontSize': 46}
sd['IHN_TITLE_OFF'] = {'fontFace': '$NHLHeaderAlternate', 'fontSize': 40}
sd['IHN_GOLD_FILL'] = {'color': GOLD}
for k in [b for b in row['behaviors'] if b.get('name') == 'ChangeHeaderTopMarginOnCustomState']:
    for st in k['states'].values():
        st['RowGroupHolder.RowGroup']['marginTop'] = 0
n = nodes(row)
h = n['HeaderTitle']
h['visible'] = False
h['alpha'] = 0.0
h['height'] = 0.0
n['Highlight']['styles'] = ['IHN_GOLD_FILL']
n['Description']['maxWidth'] = 520.0
mt = n['ModeTitle']
mt['styles'] = ['IHN_TITLE_ON' if s == 'NHL_HEADER_1_ALT' else s for s in mt['styles']]
for b in mt.get('behaviors', []):
    for lang in (b.get('styleStatesOnLanguage') or {}).values():
        for state, styles in lang.items():
            lang[state] = [('IHN_TITLE_ON' if state.endswith('selected') and not state.endswith('deselected') else 'IHN_TITLE_OFF')
                           if s.startswith('NHL_HEADER') else s for s in styles]
save('widgets/mainmenu/listmenurow.layout', row)

tb = load('widgets/button/tabbutton.layout')
tb.setdefault('style-definitions', {})
tb['style-definitions']['IHN_TAB_ON'] = {'color': GOLD}
tb['style-definitions']['IHN_TAB_TEXT_ON'] = {'fontColor': NAVY}
txt = json.dumps(tb)
for b in nodes(tb)['Background']['behaviors']:
    for st, styles in (b.get('styleStates') or {}).items():
        if st.endswith('selected') and not st.endswith('deselected'):
            b['styleStates'][st] = ['IHN_TAB_ON' if s in ('TAB_BUTTON_BACKGROUND_COLOR', 'HIGHLIGHT_PRIMARY_COLOR') else s for s in styles]
for b in nodes(tb)['TabName']['behaviors']:
    for st, styles in (b.get('styleStates') or {}).items():
        b['styleStates'][st] = ['IHN_TAB_TEXT_ON' if s == 'TAB_BUTTON_LABEL_SELECTED_COLOR' else s for s in styles]
save('widgets/button/tabbutton.layout', tb)
