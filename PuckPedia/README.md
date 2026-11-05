# PuckPedia team pages (saved HTML)

These are the 32 PuckPedia team pages (`https://puckpedia.com/team/<slug>`), saved as
`<slug>.html` — one file per team, e.g. `minnesota-wild.html`. Each page embeds the team's
draft picks as JSON, which is the draft-pick ownership data HMS syncs against (see
`Core/PuckPediaPicks.cs`, `Parse()`).

## Why they are here

HMS checks the game's draft picks (2027 and later) against PuckPedia's current owners
(`picks-sync` / NHL Sync). The live fetch is tried first, but PuckPedia sits behind
Cloudflare, which refuses the app's requests on some machines (HTTP 403) while the same
pages load fine in a browser. When the live fetch fails for a team, the app falls back to
that team's saved page in this folder instead — live wins where it succeeded, files fill
the gaps. Users can also point the app at their own folder of saved pages
(`picks-sync --files`, or "Load saved pages…" in NHL Sync); that mode reads only the
files, with no live fetch.

## How they get into a release

The App and Cli projects copy `PuckPedia/*.html` next to the exe on build (see the
conditional `Content` items in `App/NHL27Kit.App.csproj` and `Cli/NHL27Kit.Cli.csproj`) —
`HMS.exe` looks for a `PuckPedia/` folder beside itself, and `nhl27kit.exe` does the same.
The folder is NOT in the repo by default: the build is conditional
(`Condition="Exists(...)"`) and never breaks without it. Before a release, drop the 32
pages in here.

## How to refresh them

The pages go stale whenever a trade moves a pick, so refresh them before a release.
From a machine whose curl is not blocked by Cloudflare (the maintainer's, usually):

```bash
mkdir -p PuckPedia
cd PuckPedia
for slug in anaheim-ducks boston-bruins buffalo-sabres calgary-flames carolina-hurricanes chicago-blackhawks colorado-avalanche columbus-blue-jackets dallas-stars detroit-red-wings edmonton-oilers florida-panthers los-angeles-kings minnesota-wild montreal-canadiens nashville-predators new-jersey-devils new-york-islanders new-york-rangers ottawa-senators philadelphia-flyers pittsburgh-penguins san-jose-sharks seattle-kraken st-louis-blues tampa-bay-lightning toronto-maple-leafs utah-mammoth vancouver-canucks vegas-golden-knights washington-capitals winnipeg-jets; do
  curl -s -o "$slug.html" "https://puckpedia.com/team/$slug"
  sleep 1
done
ls *.html | wc -l   # expect 32
```

From a machine where curl is blocked (HTTP 403), save each team page from the browser
instead (Save As → "Webpage, HTML Only") and name the file `<slug>.html`.

## Freshness

The pages are a snapshot: they are current as of the day they were pulled, and go stale
whenever a trade moves a pick. When refreshing them, note the pull date here (e.g.
"Pulled 2026-11-04") so users can tell how fresh the bundled snapshot is. The sync
status line says how many teams' picks came from these pages, so a stale snapshot is
visible, not silent.
