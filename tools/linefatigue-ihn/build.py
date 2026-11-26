"""IHN "horizontal bar" line-on-the-ice panel for NHL 24 (faceoffstrategy.layout, used only by LineFatigueAway/Home).

usage: python build.py OUTDIR
writes OUTDIR/faceoffstrategy.layout, linefatigueaway.layout, linefatiguehome.layout

The game code (nhl.overlays.linefatigue.LineFatigueOverlay) still finds every node it needs, one level deep:
PanelBase{PanelBackground, HeaderBackground, HeaderTopLine, HeaderBottomLine}, PanelEdgeLeft/Right,
ProgressFooter{ProgressBar, LineName, FooterBackground, FooterTopLine, FooterBottomLine}, PosPlayersGroup{Position1-3,
PlayerName1-3}; the propertyMap still sets PanelBase.HeaderColor (team colour) and PanelBase.TeamName.
The nodes it resizes (PanelBackground, edges) are kept at alpha 0; the bar is drawn with background_white fills.

Vertical: with EA's players-count binding the clip top sits at 994 - (n*30 + 95) and only ProgressFooter is moved back
(clip y n*30 + 49), so the bar jumped 30 px between 3-man and 2-man lines and the line name sat off centre. The panel
layouts (linefatigueaway/home.layout) drop that binding: numOfPlayers is never set, the panel height stays 0, the clip
top sits at the panel bottom (994, or 914 when overlapping) and nothing moves. The bar is drawn above it (clip y -66..0,
screen 928..994) and the footer keeps its layout y. A 2-man line leaves the third cell empty (the game blanks its text).
"""
import json, os, sys

NAVY, NAVY2, GOLD, GOLD2, WHITE = '0x0B1A33', '0x13294B', '0xC9A54C', '0xE8CB7A', '0xFFFFFF'
W, BY, BH = 700, -66, 66
TEAM_W, CELL_W, LINE_X = 124, 130, 516
FONT = 'NHL_BODY_7_ALT_ITALIC'


def asset(name, x, y, w, h, color=None, alpha=1.0, type_='background_white', **kw):
    d = {'commonName': 'Asset', 'name': name, 'alpha': alpha, 'x': float(x), 'y': float(y), 'width': float(w), 'height': float(h), 'type': type_}
    if color: d['color'] = color
    d.update(kw)
    return d


def label(name, x, y, w, h, size, color, text=None, resize=None, styles=None):
    d = {'commonName': 'Label', 'name': name, 'styles': styles or [FONT], 'fontColor': color, 'x': float(x), 'y': float(y),
         'width': float(w), 'height': float(h), 'fontSize': float(size), 'forceCaps': True, 'textAlignH': 'LEFT'}
    if text is not None: d['text'] = text
    if resize: d['resizeType'] = resize
    return d


def composite(name, x, y, children, **kw):
    d = {'commonName': 'Composite', 'name': name, 'x': float(x), 'y': float(y), 'layout': {'type': 'CanvasLayout'}}
    d.update(kw)
    d['children'] = children
    return d


base = [
    # nodes the code looks up / resizes: invisible
    asset('PanelBackground', 0, 0, 251, 187, alpha=0.0, type_='gameplay_grey_panel_bg'),
    asset('HeaderBackground', 0, 0, 251, 35, alpha=0.0, type_='gameplay_grey_panel_bg'),
    asset('HeaderTopLine', 1, 0, 249, 1, alpha=0.0, type_='divider_white'),
    asset('HeaderBottomLine', 1, 34, 249, 1, alpha=0.0, type_='divider_white'),
    # navy bar, lighter top half
    asset('IhnFill', 0, BY, W, BH, NAVY, 0.93),
    asset('IhnFillTop', 0, BY, W, BH // 2, NAVY2, 0.85),
    # team block: the game tints this glow with the team colour
    asset('HeaderColor', 0, BY, TEAM_W, BH, alpha=1.0, type_='faceoff_panel_header_glow', styles=['WHITE_COLOR']),
    # line block
    asset('IhnLineFill', LINE_X, BY, W - LINE_X, BH, GOLD, 0.18),
    # dividers
    asset('IhnTeamDiv', TEAM_W, BY, 2, BH, GOLD),
    asset('IhnCellDiv1', TEAM_W + 2 + CELL_W, BY + 9, 1, BH - 18, GOLD, 0.45),
    asset('IhnCellDiv2', TEAM_W + 2 + 2 * CELL_W, BY + 9, 1, BH - 18, GOLD, 0.45),
    asset('IhnLineDiv', LINE_X, BY, 2, BH, GOLD),
    # gold rim
    asset('IhnRimTop', 0, BY, W, 2, GOLD),
    asset('IhnRimBottom', 0, BY + BH - 2, W, 2, GOLD),
    asset('IhnRimLeft', 0, BY, 2, BH, GOLD),
    asset('IhnRimRight', W - 2, BY, 2, BH, GOLD),
    label('TeamName', 13, BY + 5, TEAM_W - 22, 36, 32, WHITE, resize='SCALEDOWN'),
    label('IhnTag', 15, BY + 40, 60, 20, 15, GOLD2, text='IHN'),
]

players = []
for i in range(3):
    cx = TEAM_W + 2 + i * CELL_W + 12
    players.append(label(f'Position{i + 1}', cx, BY + 6, 60, 22, 16, GOLD2))
for i in range(3):
    cx = TEAM_W + 2 + i * CELL_W + 12
    players.append(label(f'PlayerName{i + 1}', cx, BY + 27, CELL_W - 20, 32, 24, WHITE, text='', resize='COMPRESS'))

# energy bar: its width is 250 - x(layout) - 28 = 88; the game moves it right of the line name
footer = [
    asset('FooterBackground', 0, 0, W - LINE_X, 46, alpha=0.0, type_='gameplay_grey_panel_bg'),
    asset('FooterTopLine', 0, 0, 1, 1, alpha=0.0, type_='divider_white'),
    asset('FooterBottomLine', 0, 45, 1, 1, alpha=0.0, type_='divider_white'),
    label('LineName', 14, 13, 80, 30, 22, GOLD2),
    {'commonName': 'OverlayEnergyBar', 'name': 'ProgressBar', 'x': 134.0, 'y': 20.0, 'width': 88.0, 'height': 16.0, 'progressBarWidth': 88.0},
]

layout = {
    'layout': {'type': 'CanvasLayout'},
    'propertyMap': {
        'teamInfo.teamColor': 'PanelBase.HeaderColor.color',
        'teamInfo.teamName': 'PanelBase.TeamName.text',
        'showFaceoffs': 'PosPlayersGroup.visible',
    },
    'children': [
        composite('PanelBase', 0, 0, base),
        composite('PosPlayersGroup', 0, 0, players),
        composite('ProgressFooter', LINE_X, BY + BH // 2 - 28, footer, width=float(W - LINE_X), height=46.0),
        asset('PanelEdgeLeft', 0, 0, 1, 186, alpha=0.0, type_='divider_white'),
        asset('PanelEdgeRight', 250, 0, 1, 186, alpha=0.0, type_='divider_white'),
    ],
}

out = sys.argv[1]
os.makedirs(out, exist_ok=True)
txt = json.dumps(layout, indent=1)
open(os.path.join(out, 'faceoffstrategy.layout'), 'w', newline='\n').write(txt)
print('faceoffstrategy.layout', len(txt.encode()), 'bytes')

# the panels: away flush left (EA x -1), home flush right
src = sys.argv[2] if len(sys.argv) > 2 else None
if src:
    for side, x_old, x_new in (('away', '"x" : "-1"', '"x" : "-1"'), ('home', '"x" : "1670"', f'"x" : "{1920 - W + 1}"')):
        t = open(os.path.join(src, f'linefatigue{side}.layout'), encoding='utf-8').read()
        assert x_old in t, side
        t = t.replace(x_old, x_new)
        # drop the players-count binding (it moves the panel 30 px per player; see the top)
        d = json.loads(t)
        d['bindings'] = [b for b in d['bindings'] if b.get('binding') != f'{side}_players_count']
        assert len(d['bindings']) == len(json.loads(t)['bindings']) - 1, side
        t = json.dumps(d, indent=4)
        open(os.path.join(out, f'linefatigue{side}.layout'), 'w', newline='\n', encoding='utf-8').write(t)
        print(f'linefatigue{side}.layout', len(t.encode()), 'bytes')
