"""IHN jumbotron pause menu for NHL 24 (concept D in previews/ihn/menu/pause-D.png), Play Now Home page.

usage: python build.py SRC_DIR OUT_DIR
SRC_DIR holds EA's files at their legacy paths (nhl/ui/features/...), read fresh with nhl27kit legacy ... --out.
Install each OUT file with legacy-set (game closed, --game for NHL 24).

Home is the first hub section, so the hub's scroll clamps it to x 0: section coordinates are screen coordinates
(y + the hub's y, moved from 256 to 130).
  hubmanager.layout                tabs centred at y 50, hub sections up to y 130, a navy wash behind everything
  hubhomesectionprimaryversion     gold-framed jumbotron 1200x300 at x 360 (frame + "IHN . PAUSED"), the matchup
                                   art (EA's replay highlights widget) hidden, the score header placed in the frame,
                                   the options as a row of four buttons under it
  gamestatspanel.layout            score header scaled 1.35 with bigger logos and a gold score box; the stat list
                                   turned into a row of nine tiles across the bottom (x 120..1800, y 510)
  gamestatsitem.layout             one stat as a 180x200 navy tile: away value top, name middle (gold), home bottom
  manageteamrow.layout             option rows: navy button, gold when selected (EA's lime bar switched off);
                                   also used by the Manage Teams list
  singlefilter.layout              tab buttons: navy, gold when selected (shared by other filter bars)
gamestatspanel/gamestatsitem are also used by the other Home pages (Be A Pro, HUT ...): there the tiles sit where
this page puts them, which may not fit those pages.
"""
import json, os, sys

NAVY, NAVY2, NAVY3, GOLD, GOLD2 = '0x0B1A33', '0x13294B', '0x1B3560', '0xC9A54C', '0xE8CB7A'
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
        kids = list(n.get('children', []))
        if isinstance(n.get('layoutData'), dict): kids.append(n['layoutData'])
        for c in kids:
            if 'name' in c: found.setdefault(c['name'], c)
            walk(c)
    walk(d)
    return found


def box(name, x, y, w, h, color, alpha=1.0):
    return {'commonName': 'Asset', 'name': name, 'type': 'background_white', 'color': color, 'alpha': alpha,
            'x': float(x), 'y': float(y), 'width': float(w), 'height': float(h)}


def fill(name, color, alpha=1.0, **kw):
    d = {'commonName': 'Asset', 'name': name, 'type': 'background_white', 'color': color, 'alpha': alpha,
         'alignH': 'FILL', 'alignV': 'FILL'}
    d.update(kw)
    return d


# hub: tabs centred on top, sections up, navy wash
P = 'gameplay/vvms/pausemenu/hubmanager/hubmanager.layout'
hm = load(P)
n = nodes(hm)
n['TabGroup']['x'] = 492.0                  # 4 x 230 + 3 x 5 = 935 wide
n['TabGroup']['y'] = 50.0
n['HubSections']['y'] = 130.0
for step in n['HubSections']['layoutSteps']:      # keep the next tab's page off screen (EA: 70, it peeked in)
    if step['type'] == 'PositionItemsInLine':      # the gap next to the selected page is paddingAroundSelected
        step['data']['paddingBetweenItems'] = 400
        step['data']['paddingAroundSelected'] = 400
mg = hm['children'][0]['layoutData']        # MenuGroup: hidden with the hub, so the wash goes with it
mg['children'] = [box('IhnWash', 0, 0, 1920, 1080, NAVY, 0.55)] + [c for c in mg['children'] if c.get('name') != 'IhnWash']
save(P, hm)

# Home pages: Play Now in game, end of period / game, shootout end of game (same three pieces)
ROW_STYLES = load('gameplay/vvms/pausehub/hubsections/hubhomesectionprimaryversion.layout')['style-definitions']['CustomRowStyles']


def jumbotron(P, tag):
    hs = load(P)
    grp = hs['children'][0]
    kids = {c['name']: c for c in grp['children']}
    frame = [box('IhnFrameGold', 360, 0, 1200, 300, GOLD), box('IhnFrameNavy', 363, 3, 1194, 294, NAVY),
             {'commonName': 'Label', 'name': 'IhnFrameTag', 'styles': ['NHL_BODY_7_ALT_ITALIC'], 'fontColor': GOLD2,
              'text': tag, 'x': 360.0, 'y': 18.0, 'width': 1200.0, 'height': 30.0, 'fontSize': 22.0,
              'textAlignH': 'CENTER'}]
    rh = kids['ReplayHighlights']
    rh['visible'] = False
    rh['alpha'] = 0.0
    rh['layoutData']['x'] = -3000
    st = kids['Stats']
    st['layoutData'].update({'x': 520.0, 'y': 116.0})
    if 'preloadData' in st: st['preloadData'].update({'x': 520.0, 'y': 116.0})
    ol = kids['OptionsListContainer']
    ol['layoutData'].update({'x': 360.0, 'y': 340.0})
    if 'preloadData' in ol: ol['preloadData'].update({'x': 360.0, 'y': 340.0, 'width': 1200.0, 'height': 90.0})
    on = nodes(ol)
    sb = on['ScrollbarOptionsList']
    sb['visible'] = False
    sb['alpha'] = 0.0
    lst = on['OptionsList']
    lst.update({'orientation': 'HORIZONTAL', 'width': 1200.0, 'height': 90.0, 'x': 0.0})
    lst['layoutSteps'] = [
        {'type': 'SetPropertyOnMount', 'data': {'property': 'height', 'value': 90}},
        {'type': 'SetPropertyOnMount', 'data': {'property': 'width', 'value': 1200}},
        {'type': 'SetPropertyOnItems', 'data': {'property': 'width', 'value': 293}},
        {'type': 'SetPropertyOnItems', 'data': {'property': 'height', 'value': 84}},
        {'type': 'PositionItemsInLine', 'data': {'paddingBetweenItems': 9}},
        {'type': 'CalculateLayoutSize'},
        {'type': 'CalculateWindowPercent'},
        {'type': 'ScrollSelectionToCenter'},
        {'type': 'ClampScrollPercent'},
        {'type': 'PositionMountByScroll'},
        {'type': 'AnimateValuesOnMount', 'data': {'properties': ['x'], 'speed': 0.4}},
    ]
    # smaller option text on this page only (30 px wrapped "VIEW X-FACTOR PLAYERS" in a 293 px button)
    sd = hs.setdefault('style-definitions', {})
    if 'CustomRowStyles' not in sd:                 # the end-of-game page has none: use the Play Now page's
        sd['CustomRowStyles'] = json.loads(json.dumps(ROW_STYLES))
        lst['itemStyles'] = ['CustomRowStyles']
    sd['IHN_BTN'] = {'fontFace': '$Body', 'fontSize': 23}
    sd['IHN_BTN_ALT'] = {'fontFace': '$BodyAlternate', 'fontSize': 23}
    for k, v in sd['CustomRowStyles'].items():
        sd['CustomRowStyles'][k] = ['IHN_BTN' if x == 'NHL_BODY_6' else 'IHN_BTN_ALT' if x == 'NHL_BODY_6_ALT' else x for x in v]
    grp['children'] = frame + [rh, st, ol]
    save(P, hs)


for name, tag in (('hubhomesectionprimaryversion', 'IHN  ·  PAUSED'), ('hubhomesection3stars', 'IHN'),
                  ('hubhomesectionshootoutendofgame', 'IHN')):
    jumbotron(f'gameplay/vvms/pausehub/hubsections/{name}.layout', tag)

# score header + stat tiles
P = 'widgets/pausemenu/gamestatspanel.layout'
gp = load(P)
n = nodes(gp)
ig = n['InformationGroup']
ig['scaleX'] = ig['scaleY'] = 1.35
# first test: logos ~218 px and 45 px low at 1.4 (native ~115); 1.1 -> ~171 px, centred on the score row
for name, x in (('AwayTeamLogo', -89.0), ('HomeTeamLogo', 614.0)):
    n[name].update({'x': x, 'y': -34.0, 'scaleX': 1.1, 'scaleY': 1.1})
sbg = n['ScoresBackground']
sbg.update({'type': 'background_white', 'color': GOLD, 'alpha': 1.0})
sbg.pop('styles', None)
gp.setdefault('style-definitions', {})['IHN_SCORE_TEXT'] = {'fontColor': NAVY}
for s in ('AwayScore', 'SeparatorLabel', 'HomeScore'):
    n[s]['styles'] = ['IHN_SCORE_TEXT' if x == 'SCOREBOX_LABEL_FONT_COLOR' else x for x in n[s]['styles']]
stl = n['Stats']
# x is ignored in a PanelLayout, so the strip moves by marginLeft
stl.update({'orientation': 'HORIZONTAL', 'alignH': 'LEFT', 'x': 0.0, 'marginLeft': -400.0, 'marginTop': 394.0, 'width': 1680.0, 'height': 200.0})
for b in gp['behaviors']:
    if b.get('name') == 'NormalCompressList':
        for k in ('onTrue', 'onFalse'):
            for step in b[k]:
                if step['type'] == 'PositionItemsInLine': step['data']['paddingBetweenItems'] = 188
save(P, gp)

P = 'widgets/pausemenu/gamestatsitem.layout'
gi = load(P)
gi['width'], gi['height'] = 180.0, 200.0
gi.setdefault('style-definitions', {})['IHN_GOLD_TEXT'] = {'fontColor': GOLD2}
n = nodes(gi)
n['BaseLine']['alpha'] = 0.0
an = n['AttrName']
an.update({'alignV': 'CENTER', 'marginTop': 0.0, 'width': 172.0, 'textAlignH': 'CENTER', 'resizeType': 'SCALEDOWN'})
an['styles'] = ['IHN_GOLD_TEXT' if s == 'FONT_SWATCH_DEFAULT' else s for s in an['styles']]
for side, v in (('Away', 'TOP'), ('Home', 'BOTTOM')):
    c = next(x for x in gi['children'] if x.get('name') == side)
    c.update({'width': 180.0, 'height': 70.0, 'alignH': 'LEFT', 'alignV': v, 'marginTop': 10.0 if v == 'TOP' else 0.0,
              'marginBottom': 10.0 if v == 'BOTTOM' else 0.0})
    for k in c['children']:
        if k.get('name') == 'Label':
            k.update({'width': 180.0, 'marginLeft': 0.0, 'marginRight': 0.0, 'marginTop': 0.0, 'alignH': 'CENTER',
                      'alignV': 'CENTER', 'textAlignH': 'CENTER'})
        else:
            k['alpha'] = 0.0
            k['visible'] = False
gi['children'] = [fill('IhnTile', NAVY, 0.92), fill('IhnTileTop', GOLD, 1.0, alignV='TOP', height=3.0)] + \
                 [c for c in gi['children'] if not c.get('name', '').startswith('Ihn')]
save(P, gi)

# option buttons: navy, gold when selected, EA's lime bar off
P = 'widgets/manageteam/manageteamrow.layout'
mr = load(P)
mr['style-definitions']['PANEL_ON_SELECTION'] = {'showPanel': False}
mr['showPanel'] = False
mr['useBacklight'] = False
cs = lambda d, s: [{'commonName': 'ControlStateStyles', 'name': 'ControlStateStyles', 'defaultStyles': d,
                    'disabledStyles': d, 'selectedStyles': s}]
mr['children'] = [fill('IhnButton', NAVY2, 0.95, marginLeft=0.0, marginRight=0.0),
                  fill('IhnButtonOn', GOLD, 1.0, behaviors=cs(['INVISIBLE'], ['VISIBLE'])),
                  fill('IhnButtonLine', GOLD, 1.0, alignV='BOTTOM', height=3.0)] + \
                 [c for c in mr['children'] if not c.get('name', '').startswith('Ihn')]
for c in mr['children']:
    if c.get('name') == 'DividerBottom': c['alpha'] = 0.0
    if c.get('name') == 'Title':                    # "VIEW X-FACTOR PLAYERS" wrapped in a 293 px button
        c.update({'multiline': False, 'wordWrap': False, 'resizeType': 'SCALEDOWN', 'marginLeft': 10.0, 'marginRight': 10.0})
save(P, mr)

# tab buttons
P = 'widgets/dropdownfilter/singlefilter.layout'
sf = load(P)
sf.setdefault('style-definitions', {})['IHN_TAB_OFF'] = {'color': NAVY3}
txt = json.dumps(sf).replace('"$HIGHLIGHT_PRIMARY"', f'"{GOLD}"').replace('"FILTER_BACKGROUND_UNSELECTED_COLOR"', '"IHN_TAB_OFF"')
save(P, json.loads(txt))
