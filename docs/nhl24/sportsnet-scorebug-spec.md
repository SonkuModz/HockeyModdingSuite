# Sportsnet scorebug for NHL 24: what the references show

Sources: `refs/NHL on Sportsnet/` (19 screenshots, `goal.mp4` 1920×1080, 30 fps, 170 frames, every frame checked).

## Main bar (in play)

One rounded bar, top-left, left to right:

1. **SN box.** Light grey (near white), the Sportsnet "SN" wordmark with its red and blue swoosh. Full height of the bar,
   square left side.
2. **Away team.** Solid team colour (Anaheim orange, Montreal red, Vancouver navy, Edmonton orange…). Abbreviation in
   white condensed caps on the left, score large and bold on the right.
3. **Home team.** The same, in the home team's colour, directly next to the away team. No gap.
4. **Clock box.** Light grey. Game clock on top (dark navy, bold); under 1:00 it shows tenths (`14.4`). A thin divider,
   then the period below (`1ST`, `2ND`, `3RD`, `OT`, `FINAL`). The right end of the bar is rounded at the bottom-right.
5. **Shots row** under both team boxes, light grey: away shots under the away team, `SHOTS` in the middle (dark navy),
   home shots under the home team, with thin vertical dividers.

Colours: the light grey is about #E8EAEC with a faint vertical gradient. Text on grey is dark navy (#1C2B3A). Team text
is white.

## States

- **Final:** the clock box shows only `FINAL`.
- **Power play:** a tab hangs under the power play team's box, in that team's colour: `POWER PLAY 1:36`. A team logo
  tile (white on team colour) can sit between the home box and the clock.
- **Empty net:** the same tab: `EMPTY NET`. When the logo tile shows, the tab spans the logo and the team.
- **4-on-4:** an extra grey box after the clock: `4-ON-4` over the time left (`1:10`).
- **Stat panel:** a team-colour panel to the right of the clock, with a logo, `POWER PLAY`, `LAST 10 GAMES` and a large
  stat (`9/24`).
- **Shootout:** the bar becomes away (grey, abbreviation plus three round markers: ✖ miss, ✔ goal, empty ring for not
  yet taken), then home in team colour. Below it: `SHOOTOUT - ROUND 3`.
- **Replays:** only the SN box shows (the rest is hidden).
- **Line-up / scorer panels:** headshots (`GOAL 4TH THIS SEASON / BOESER 11:40`; three-player line with numbers and
  positions). NHL 24's score clock gets no headshots or season stats, so these can't be fed.

## Goal animation (goal.mp4, 30 fps)

| Frames | Time | What happens |
|---|---|---|
| 1–9 | 0.00–0.30 s | Normal bar: ANA 0, CGY 0, 17:37, 1ST, shots 0 and 1. |
| 10–16 | 0.30–0.53 s | A **blue** (Sportsnet blue, about #1E63C8) panel wipes in from the left edge of the team area and covers the team, clock and shots boxes. The SN box stays. The bar becomes one tall block (the shots row is covered too). |
| 17–22 | 0.53–0.73 s | **White** bands sweep left to right through the blue (two passes), like a light streak. |
| 22–28 | 0.70–0.93 s | The scoring team's colour (Calgary red) wipes in left to right behind the blue. It carries the team name in big embossed 3D letters, tiled and cut off ("…S FLAMES FL…"), with the team logo in the middle. |
| 29–39 | 0.93–1.30 s | Hold, then the logo grows quickly. |
| 40–42 | 1.30–1.40 s | The logo zooms past the camera and fills the bar, then is gone. |
| 41–55 | 1.37–1.83 s | `FLAMES` in widely spaced white condensed letters, centred, over a faint logo watermark on the team colour. A thin underline sits under the right end. |
| 56–58 | 1.87–1.93 s | The name breaks up and disappears (horizontal slices). |
| 59–64 | 1.97–2.13 s | Team colour bar with only the watermark. |
| 65–73 | 2.17–2.43 s | `GOAL` builds letter by letter in a set order: G, then L, then A, then O. Each fades in, white italic condensed caps, large. |
| 74–125 | 2.47–4.17 s | Hold `GOAL` (about 1.7 s). |
| 126–132 | 4.20–4.40 s | Letters drop out (O, then L and A) while the red bar shrinks from the right edge towards the left. |
| 133–136 | 4.43–4.53 s | Only a sliver of red next to the SN box; the grey frame appears under it. |
| 137–145 | 4.57–4.83 s | The bar rebuilds left to right: the away box with its abbreviation, then the home box, then the clock box, with the shots row as an empty frame. |
| 146–152 | 4.87–5.07 s | Scores fade in (the new score, CGY 1), then the clock and period, then the shots numbers. |
| 153–170 | 5.10–5.67 s | Back to normal. |

Power play goal: the same, but the hold reads `POWER PLAY` (small) over `GOAL`.

The goal banner covers the full bar width from the SN box's right edge to the clock box's right edge. It is the full bar
height (the team row plus the shots row).

## Built (2026-11-23)

- Source: `tools/scorebug-sportsnet/` (`ScoreClock.tpl.as`, a fork of the NHL24Kit ESPN+ ScoreClock with the same public
  API, settings block and tween/`runAction` engine; `sn.json`, the SN mark traced by `trace.py`).
- Build: `python tools/scorebug-sportsnet/build.py BASE.swf OUT.swf`. BASE is the ESPN+ clock inflated to FWS. It
  reproduces `App/Assets/scoreclock-sportsnet.swf` byte for byte.
- Install: the app's Scorebug page (NHL 24) has **Install Sportsnet clock**. The CLI equivalent is
  `nhl27kit scorebug24 --movie OUT.swf --name Sportsnet`. Both write scoreclock.gfx and score_clock.gfx as one
  restorable entry.
- Preview: Ruffle web plus headless Edge over CDP. Ruffle needs FileAttributes first and the GFx tags (1000-1009)
  removed; do that only in the preview copy.
- The user confirmed these in-game: the bar, the power play tab, the logo tile (for either team) and the goal
  animation.
- Not done: the replay-only SN box (hideClock can't tell replays apart) and the headshot/stat panels (no data).
