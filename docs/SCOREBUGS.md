# Scorebugs on NHL 27

How NHL 27 draws its score clock, and how a changed one gets into the game. Compare NHL 24Kit's
`C:\dev\NHL24Kit\docs\SCOREBUGS.md`: there the bug was a standalone `scoreclock.gfx` movie that was swapped out whole.

## 1. Where it lives

NHL 27's UI is still Scaleform (AS3), driven by EA's "UX" layer. It lives in **legacy files** in UI chunk file
collectors, not in EBX:

| Collector | Files | What |
|---|---|---|
| `globals/chunkfilecollector` | 12,985 | databases, CSVs, service JSON, Lua (what the kit read before) |
| `globals/chunkfilecollectoruicommon_nhluidata` | 6,862 | **the UI**: `.layout` (JSON), `.vvm`, `.lua`, `.nav`, `.dd`, SWF code libraries |
| `globals/chunkfilecollectoruips5_swfassets` | 452 | `.gfx` movies and their `.gnf` images (goal scored, lower thirds, team intro…) |
| `globals/chunkfilecollectoruips5_launch_swfassets` | 297 | more `.gfx` movies |
| `globals/chunkfilecollectoruicommon_fonts` | 21 | fonts |

`nhl27kit legacy --collector list` shows them all. `nhl27kit legacy --collector C --out DIR` dumps one.

## 2. The score clock

- `nhl/ui/features/overlays/vvms/scoreclock/scoreclock.vvm`: `{"views":[["gfx", {"layout":"scoreclockoverlay.layout"}]]}`.
- `scoreclockoverlay.layout` (JSON): a 771×69 canvas with one child, `ScoreclockWdgtWrapper` named `NewScoreClock`, at
  (40, 36) in a 1920×1080 design. The 3on3, proam and gauntlet modes have their own layouts in sibling folders.
- `nhl/ui/generated_widget.manifest` maps the name to the AS3 class `nhl.widgets.tin.widgets.scoreclock::ScoreclockWdgtWrapper`,
  which extends `nhl.overlays::OverlayBase`.
- **`nhl/ui/lib/nhluidata.swf`** has `ScoreclockWdgtWrapper` (data glue: `noaData`, `receiveEvent`, power play text,
  shootout, faceoff, and the `SetNewGameClock` direct-access hook) and the older `nhl.overlays.scoreclock.ScoreClockOverlay`.
- **`nhl/ui/lib/external/tin_nhl.swf`** has the drawing: `nhl.tin.widgets.scoreclock.ScoreclockWdgt` (about 1,000 lines)
  and 38 subwidget, data and style classes (team, logo, time, goal, message, stats, shootout, faceoff, post-goal).
- Everything is drawn in code. The **style classes** (`…subwidgets.styles.*Style`) hold the positions, sizes, colours
  and fonts as plain numbers, for example `ScoreclockTimeSubwdgtStyle.bgProps = {width 100, height 80, color 0x222222}`.
- Textures go by alias through `nhl/ui/definition.ps5.assetpaths`, for example `"scoreclock_time_base":
  "img://fb/ui/textures/common/widgets/scoreclock/timeBgBase&[256X64]"`. They're ordinary `TextureAsset`s, so the
  Textures page can replace them. Nothing in EBX imports them, which is why searching EBX references found nothing.
- The executable has the settings `SCORE_CLOCK_POSITION_X/Y` and `ui_use_new_score_clock`.

Decompile with FFDec (`C:\dev\tools\ffdec\ffdec-cli.exe`):

```
ffdec-cli -selectclass "nhl.tin.widgets.scoreclock.**" -export script OUT tin_nhl.swf
ffdec-cli -replace tin_nhl.swf out.swf nhl.tin.widgets.scoreclock.subwidgets.styles.ScoreclockTimeSubwdgtStyle Style.as
```

The FFDec compiler quirks from NHL 24 still apply: no closures, and no `switch` on strings.

## 3. Writing a legacy file (`LegacyEditor`)

- Collector index entries are 28 bytes: [FNV-1 64 of the path][stored start][stored end][offset][size][chunk index].
  The chunk is stored as blocks of 256 KB plus an 8-byte header, and stored start/end is the block range that holds the
  file: `offset / 0x40000 * 0x40008` to `((offset + size - 1) / 0x40000 + 1) * 0x40008`. That holds for every entry checked.
- Files are packed with no gaps, and chunks must keep their size (no TOC changes). `tin_nhl.swf` is compressed tighter
  than zlib 9 (EA's encoder), so even a one-number change grows it by about 1.4 KB.
- So a file that grows is written over a **debug-only file** of the same chunk (`nhl/ui/features/debug/…`, 8.6 MB of
  them in the UI chunk; the smallest that fits and isn't shared), and its index entry is repointed there. Same size or
  smaller: written in place, with the new size in its entry.
- One mod entry ("legacy", the path) holds the data chunk and index chunk patches. Restore puts both back. The written
  file is kept in `mods/sources`, so mod packs carry it.

```
nhl27kit legacy-set nhl/ui/lib/external/tin_nhl.swf --file tin_nhl_red.swf --note "red clock face"
```

## 4. How it draws (checked against an in-game screenshot)

Each part, bottom to top:
1. **Background shape** (`bgProps`). The code makes it the **mask** of the hex texture (`_bgTextureMount.mask = _bg`),
   and a mask isn't drawn, so the clock, SOG and team rows never show `bgProps.color`. The logo box is the exception: its
   mask is a second rectangle, so its colour (`GRAY_COLOR`, #222222) shows.
2. **Hex texture** `scoreclock_hive_texture` (16×80: charcoal, lighter at the top), tiled at its own size by a bitmap fill
   that's added outside the image loader, so `bgTexture.visible: false` doesn't hide it. This is the charcoal look of
   every box. A 40-pixel team row shows the top half of it.
3. Team rows: the **glow** `scoreclock_teamcolour_glow` (white, strong at the left and along the bottom), tinted with
   the team colour, then the **highlight strip** `scorebug_light_teamcolour` (a white line at the row's bottom: the
   bright line between the away and home rows), then the shading image `scoreclockgradient` (a dark fade at the top).
4. Clock, SOG, logo: the shared `ui/textures/common/bgs/gradient` (black, clear at the top), tinted by
   `bgGradientProps.color`/`alpha` (white at 10% on the bottom half). Other menus use that image too.
5. Text. The scores use `FONT_HEADER_5_WIDE_ALT`, an extra-wide face: a "0" looks like a white oval.

So the way to recolour the clock, SOG or team rows is the hex texture (or its alpha), not `bgProps.color`. The red
clock-face test only changed a colour that isn't drawn.

## 5. Sharp EA SPORTS badge

The logo box's badge is `scoreclock_ea_logo` (64×64, alias `scoreclock_nhl27_logo`). The same badge is in the match
intro's sheet `ui/textures/common/widgets/lowerthird/matchintro/eas_end` (1080×1080, 3×3 frames of 360×360; alias
`lower_third_match_ea_logo_anim_end`). The last frame is the front-facing badge (pixels 34..330 × 45..346).

The logo box draws `logoProps.path` scaled to `logoProps` and, unlike team logos, isn't clipped. So the sharp badge
(`Scorebug.SharpBadgeStyle`/`SharpBadgeWidget`, the page's "Sharp EA SPORTS badge", `nhl27kit scorebug --sharp`):
- the style points `logoProps` at the sheet, 190×190 at (−118.7, −121.1), which puts the last frame's badge on the old
  badge's 52 pixels around (40, 40), and gets a new `logoClipProps` (8, 5.6, 63.3×63.3);
- `ScoreclockLogoSubwdgt` gets a `TinMask` on the logo mount from `logoClipProps` (a `TinMask` masks its parent mount),
  so only that frame shows.

The UI image names come from `nhl/ui/definition.ps5.assetpaths` in `globals/chunkfilecollectoruips5_nhluidata`
(3,294 names); the page's preview uses them to draw whatever image `logoProps.path` names.

## 6. Status

- Scorebug page: parts, values and a preview drawn the way above, compared with an in-game screenshot
  (`previews/43-scorebug-vs-reference.png`). The preview fonts are stand-ins.
- Writing a changed SWF works and restores byte for byte (red test). A navy "classic gloss" style was saved by mistake
  (a misread request) and restored; the game has its own score clock again.
- **The sharp badge crashed the game (2026-10-27):** with it saved, Kyty crashed loading into a match (menus were fine).
  Restoring `tin_nhl.swf` fixed it, and the game has its own score clock again. Don't save score clock changes until the
  cause is known. It is one of these:
  - the rebuilt SWF itself: FFDec's recompile of the two changed classes, or the new `TinMask` on the logo;
  - its move into `debug/vvms/hubmanagerdemo/assets/tile12.png`, if the game reads that file after all, or the
    repointed index entry.

## 7. Next

1. Find what crashed. Test the move apart from the SWF: save the game's own SWF padded to a larger size, so it gets
   moved but its code is unchanged. Then test a same-size SWF change, which is written in place with no move.
2. Other logos for the logo box: `logoProps.path` takes any UI image name from `nhl/ui/definition.ps5.assetpaths`.
3. Full custom designs (ESPN+, Prime) by changing the drawing code (`ScoreclockWdgt` and the subwidgets). The interface
   to keep is `ScoreclockWdgtWrapper`'s calls: `setType`, `animIn`/`animOut`, `updateData`, `showMessage`/`hideMessage`,
   `showTimerGoal`/`hideTimerGoal`, `hideFaceoffProbabilities`, `isOverEntity`, `dispose`.
