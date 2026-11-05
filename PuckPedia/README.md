# Draft picks snapshot

`draft-picks.json` is a static snapshot of who owns every future NHL draft pick, taken from
PuckPedia's team pages (puckpedia.com/team/{slug}) on 2026-11-04. It covers the 2027–2030
drafts: 896 picks, one per (year, round, original team).

## Why a file, not a live fetch

PuckPedia sits behind Cloudflare, which refuses the app's requests (HTTP 403) while the same
pages load fine in a browser — so the app does not try to fetch picks at all. The checkbox in
NHL Sync reads this file instead. The pulling code (live fetch, retries, saved HTML pages) was
removed; it can come back if a way around the site's limits is found.

## Format

```json
{
  "source": "PuckPedia",
  "asOf": "2026-11-04",
  "snapshotYear": 2026,
  "picks": [{ "y": 2027, "r": 2, "orig": "MIN", "owner": "COL" }]
}
```

- `y` is the REAL calendar draft year, `r` the round, `orig` the team the pick originally
  belonged to, `owner` the team that holds it now (3-letter abbreviations).
- `snapshotYear` is the draft year that was current when the file was pulled (2026).
- Conditions are treated as non-existent: a conditional pick is listed with its owner and round
  exactly as PuckPedia shows them, and the sync uses those as-is. There is no condition field.

## Years and other game versions

The file only knows real years. The app maps them onto the game it is editing: it reads the
game's first draft year from the database (2026 for NHL 27, 2023 for NHL 24) and shifts every
pick by (game first year − snapshot year) — so real 2027 picks land in NHL 24's 2024 slots.
The same mapping drives the Draft Picks tab, so the tab and the sync cannot drift.

## Refreshing

- For a release: replace `PuckPedia/draft-picks.json` in the repo (and update `asOf` /
  `snapshotYear` in the file) before building — the build copies it next to the exe.
- For yourself: replace the `draft-picks.json` sitting next to `HMS.exe` (or `nhl27kit.exe`
  for the CLI). No rebuild needed; the app reads it every time it fetches picks.
- If the file is missing next to the exe, the sync says so instead of showing zero pick rows.
