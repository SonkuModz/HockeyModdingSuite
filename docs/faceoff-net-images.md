# Getting rink pictures from TheFaceoff.net

How HMS finds a team's rink on [TheFaceoff.net](https://www.thefaceoff.net) and downloads its full-size top-down layout,
without a browser, scraping framework or login. The code is `Core/FaceoffNet.cs` (site access) and `Core/IceAds.cs`
(finding a team's current rink). `Core/CenterIce.cs` then turns the picture into the game's centre-ice textures.

## Why it's easy

1. **The pages are rendered on the server.** TheFaceoff.net is a Wix site, and Wix sends each page as finished
   HTML, so a plain HTTP GET returns everything. No JavaScript has to run, so a headless browser isn't needed.
2. **Each picture's size is written into the page.** A rink page embeds a JSON list of its images with their width,
   height and media id. You can pick the picture you want by its size without downloading anything.
3. **The CDN gives you the original when asked.** Every image is served from `static.wixstatic.com`. The bare media
   URL returns the original upload at full size; adding `/v1/fill/...` returns a resized copy. So the 5000×2236
   original is one request away once you know its id.

## Step 1: the league list

Each league has a list page, newest rink first, 8 rinks per page:

```
https://www.thefaceoff.net/ice-nhl
https://www.thefaceoff.net/ice-ahl
https://www.thefaceoff.net/ice-echl     (also sphl, fphl, ushl, chl, bchl, ncaa, pwhl, iihf, olympics, 3ice, …)
```

Page 2 onwards adds a pager parameter: `?<pager>_page=N`. The pager id (`comp-…`) differs per list. Read it from
page 1 by looking for the link to page 2:

```
(comp-[a-z0-9]+)_page=2            → NHL: comp-mb1jb5iy
https://www.thefaceoff.net/ice-nhl?comp-mb1jb5iy_page=2
```

The list ends when the page no longer links to `_page=N+1`.

Each rink in the list is one match of this pattern (title, rink page, and the id of its centre-ice close-up):

```regex
title="([^"]+)"><a data-testid="linkElement" href="(https://www\.thefaceoff\.net/center-ice/[0-9a-f-]+)"(.{0,1500}?)84911f_([0-9a-f]{32}~mv2\.\w+)
```

Titles look like `Toronto Maple Leafs 2027`. To get a team's current game-night rink, HMS takes the newest entry
named "TEAM YEAR", skipping practice rinks, outdoor games and the like (`IceAds.FindRinkAsync`). It reads more pages
only until it finds the team.

## Step 2: the rink page

Open the rink's `center-ice/<guid>` page. Its HTML embeds the page's images with their sizes:

```
"width":5000,"height":2236,"uri":"84911f_<32 hex>~mv2.jpg"
```

```regex
"width":(\d+),"height":(\d+),"uri":"84911f_([0-9a-f]{32}~mv2\.\w+)"
```

The **top-down rink layout** is the first image with the 5000×2236 shape: HMS accepts width ≥ 2000 with an aspect
within 0.02 of 5000/2236. The other images are the centre-ice close-up, logos and photos. The page `<title>` gives the
rink's name once `Center Ice and Rink Layout | TheFaceoff.net` is cut off.

Exceptions found so far:
- Florida's 2027 page has no layout.
- Pittsburgh's 5000×2236 image is an arena photo, not a layout, so check what you get.

## Step 3: download the original

```
https://static.wixstatic.com/media/84911f_<id>                                  full-size original (the layout)
https://static.wixstatic.com/media/84911f_<id>/v1/fill/w_256,h_256,al_c,q_85/thumb.jpg   resized copy (thumbnails)
```

HMS uses the resized form only for the thumbnails in its picker window, and the bare form for the actual download.
Downloads are cached in `export/faceoff/downloads`, named after the id (`~` replaced with `_`). A file already there
isn't fetched again. Each file is written as `.part` and renamed when complete, so an interrupted download never
leaves a broken file.

## Being polite to the site (and not getting blocked)

All requests go through one gate (`FaceoffNet.Retry`):

- **One request at a time, at least 1.5 s apart.** The first rollout paged the NHL list and fetched rink pages
  back to back, and the site then answered **403 to everything** for a while. After adding the 1.5 s gap, fetching
  30 NHL layouts in one go worked.
- **A 403 or 429 is never retried.** It means "stop", so HMS reports it ("TheFaceoff.net is refusing the kit's
  requests right now…") instead of hammering. Only dropped connections from the CDN are retried, 3 times, with a
  short growing wait.
- **Cache everything.** A league's list is kept for the session, and layouts stay on disk, so a team's rink is
  downloaded once.
- The client sends a normal browser-style User-Agent ending in `NHL27Kit` and accepts compressed responses.

## When the site refuses anyway

The site sometimes answers the kit 403 while a browser still gets through. The team editor's **Centre ice** tab
therefore also takes a picture you downloaded yourself:

- On a rink's page, the whole-rink picture links to a Google Drive file (e.g. "2027 Rink.jpg"). Its Download button
  gives the full-size picture. This is the one to use.
- Right-clicking the picture on the site only saves a resized AVIF about 1035 px wide. HMS accepts it but warns
  below 4000 px wide, and rejects anything under 1000 wide or not the 5000×2236 shape.

## Using it in HMS

- **App:** Teams → a team → Logos card → *Centre ice from TheFaceoff.net…*. Pick a league, filter, *Load older*
  (5 pages at a time), or paste any rink link; then preview, adjust and *Write into TEAM*. The Centre ice tab takes a
  picture file instead, and contacts nothing (*Open TheFaceoff.net* just opens your browser).
- **CLI:** `nhl27kit faceoff [NAME] [--league ahl] [--pages N]` lists rinks; with a name it finds the first match's
  layout and downloads it to `export/faceoff/downloads`. Nothing is written into the game.
- **All NHL teams' ads:** `logo-ads-all --teams DIR` downloads every team's current layout (with the 1.5 s gap), and
  `--layouts DIR --nhl DIR/teams.tsv --write` writes them.

## What happens to the picture next (short)

`Core/CenterIce.cs` cuts the game textures from the layout:

- **Scale:** measured from the blue lines, which are 25 ft either side of centre. The template position (1882/3118 of
  5000) is the fallback.
- **`ice_centerlogo_N_color`:** a 40.3 ft square around centre at 1024² (2048² for the team-ads rollout). The white
  ice is made transparent, and the site's frosted-ice tint is undone with levels.
- **`ice_centerline_N_color`:** the 85 ft red line as a 64×4096 strip, keeping team patterns such as Toronto's
  maple leaves.
- **Orientation:** TheFaceoff.net layouts lie on the ice with right = +z and down = −x.
- **Ads:** the four centre ads are sampled from the same layout into the logo's ad boxes.

## Terms

TheFaceoff.net allows its graphics for personal use with credit and a link back, not for sale. HMS downloads on
demand, keeps the files local, never ships or packs them, and credits the site in the README.
