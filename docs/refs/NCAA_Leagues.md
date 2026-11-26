# NCAA Division I Men's Hockey Leagues

Hand-off from the Blueline NHL 19 project: **7 conferences, 63 teams, 1720 players** (374 C, 311 LW, 293 RW, 547 D, 195 G). Everything here is already built and working in NHL 19, so it can be reused as-is rather than researched again.

## What is in this folder

| Path | What |
|---|---|
| `NCAA_Leagues.md` | This document: every league, team, colour, arena and roster |
| `ncaa_leagues.json` | The same data, machine-readable (use this for import scripts) |
| `logos/teams/<slug>.png` | Team logos, transparent PNG, as used for the in-game logo, helmet decal and centre ice |
| `logos/leagues/<key>.png` | Conference logos |
| `kits/<slug>_home.png`, `_away.png` | The finished NHL 19 jersey colour sheets (1024 px, opaque). NHL 19 UV layout, for reference |

## Notes on the data

- **Colours** are hex RGB. `primary/secondary/tertiary` are the team's in-game colours (menus, score clock). `officialPalette` is the school's published colour list (from trucolor.net) with each colour's name.
- **Kits**: every team has its own home and away kit made from one clean template (the adidas cut), recoloured with two colours, `main` and `second`. Home: body, pants and collar `main`; sleeve and hem stripe band `second` with its thin lines in `main`; numbers `second` outlined in white, or white outlined in `second` when the two colours are too close to read. Away: white (`#F0F0F0`) body; stripe band `main` with lines in `second`; pants `main`; numbers `main` outlined in `second`. The team logo is on the chest and is the helmet decal. Helmets are `main` (with light grey trim), gloves `main` with trim in `second`. `kits.recipe` in the JSON lists each part.
- **Kit sheets** are raw texture colour maps. The colour data has a light square behind the chest logo and over the patch spots (where the template's crest and patches were painted out); ignore those. Build NHL 24 kits from the logo PNG and the colours.
- **Players**: height in inches, weight in pounds, `shoots` L/R (catches, for goalies), `captain` C or A as the team lists them, `birthYear` real. **Not real data**: the source has no birth month or day and no ratings, so in NHL 19 each player got a random birth month and day and a random overall from 60 to 70 (kept under `nhl19` in the JSON). Rate the players properly in NHL 24 rather than copying those.
- **`nhl19` fields** are what NHL 19 uses (team number, art id, league number, birthplace codes, the made-up birth date and overall, generator seed). They mean nothing in NHL 24; they are there only so both projects can be matched up.
- Rosters are 2025-26 team lists taken from EliteProspects (name, number, position, captaincy, birth year and place, height, weight, handedness). Maryville has no roster of its own in this set.

## Leagues

| Conference | Teams | Logo |
|---|---|---|
| Big Ten | 7 | `logos/leagues/bigten.png` |
| NCHC | 10 | `logos/leagues/nchc.png` |
| Hockey East | 11 | `logos/leagues/he.png` |
| ECAC Hockey | 12 | `logos/leagues/ecac.png` |
| CCHA | 8 | `logos/leagues/ccha.png` |
| Atlantic Hockey America | 9 | `logos/leagues/aha.png` |
| NCAA Independents | 6 | `logos/leagues/ind.png` |

## Big Ten

Logo: `logos/leagues/bigten.png`

| Team | Abbr | City, State | Arena | Primary | Secondary | Tertiary | Kit main / second | Logo |
|---|---|---|---|---|---|---|---|---|
| Michigan Wolverines | MCH | Ann Arbor, Michigan | Yost Ice Arena | `#F1C400` | `#041E42` | `` | `#041E42` / `#F1C400` | `logos/teams/michiganwolverines.png` |
| Michigan State Spartans | MSU | East Lansing, Michigan | Munn Ice Arena | `#173F35` | `#FFFFFF` | `#7C878E` | `#173F35` / `#FFFFFF` | `logos/teams/michiganstatespartans.png` |
| Minnesota Golden Gophers | MIN | Minneapolis, Minnesota | 3M Arena at Mariucci | `#862633` | `#FFBF3F` | `#B9975B` | `#862633` / `#FFBF3F` | `logos/teams/minnesotagoldengophers.png` |
| Notre Dame Fighting Irish | ND | Notre Dame, Indiana | Compton Family Ice Arena | `#0C2340` | `#C99700` | `#00843D` | `#0C2340` / `#C99700` | `logos/teams/notredamefightingirish.png` |
| Ohio State Buckeyes | OSU | Columbus, Ohio | OSU Ice Rink | `#BA0C2F` | `#A2AAAD` | `#010101` | `#BA0C2F` / `#A2AAAD` | `logos/teams/ohiostatebuckeyes.png` |
| Penn State Nittany Lions | PSU | University Park, Pennsylvania | Pegula Ice Arena | `#041E42` | `#FFFFFF` | `#A2AAAD` | `#041E42` / `#FFFFFF` | `logos/teams/pennstatenittanylions.png` |
| Wisconsin Badgers | WIS | Madison, Wisconsin | Kohl Center | `#BA0C2F` | `#FFFFFF` | `#25282A` | `#BA0C2F` / `#FFFFFF` | `logos/teams/wisconsinbadgers.png` |

### Michigan Wolverines (MCH)

- School: University of Michigan, Ann Arbor, Michigan
- Arena: Yost Ice Arena
- School colours: Maize `#F1C400`, Blue `#041E42`
- In-game colours: primary `#F1C400`, secondary `#041E42`
- Kits: main `#041E42`, second `#F1C400`; sheets `kits/michiganwolverines_home.png`, `kits/michiganwolverines_away.png`
- Logo: `logos/teams/michiganwolverines.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 9 | Nicholas Moldenhauer | C | R | 5'11" | 181 | 2004 | Mississauga | A |
| 11 | Cole McKinney | C | R | 6'2" | 198 | 2007 | Chicago, IL |  |
| 16 | Aidan Park | C | R | 6'1" | 187 | 2006 | Hermosa Beach, CA |  |
| 17 | Garrett Schifsky | C | R | 5'9" | 185 | 2003 | Andover, MN | C |
| 19 | Michael Hage | C | R | 6'1" | 198 | 2006 | Mississauga | A |
| 44 | William Horcoff | C | L | 6'5" | 203 | 2007 | Birmingham, MI |  |
| 71 | Jack Nesbitt | C | L | 6'5" | 185 | 2007 | Sarnia |  |
| 95 | Kieren Dervin | C | L | 6'1" | 183 | 2007 | Gloucester |  |
| 2 | J.P. Hurlbert | LW | R | 6'0" | 190 | 2008 | Allen, TX |  |
| 22 | Kason Muscutt | LW | L | 5'11" | 176 | 2005 | Bossier City, LA |  |
| 27 | Malcolm Spence | LW | L | 6'2" | 201 | 2006 | Mississauga |  |
| 18 | Jayden Perron | RW | R | 5'9" | 179 | 2005 | Winnipeg |  |
| 92 | Adam Valentini | RW | L | 5'10" | 185 | 2008 | Toronto |  |
| 96 | Nicholas Sima | RW | R | 6'3" | 194 | 2005 | Mississauga |  |
| 3 | Brandt Harper | D | R | 6'1" | 196 | 2006 | Tampa, FL |  |
| 4 | Asher Barnett | D | L | 6'1" | 196 | 2007 | Wilmette, IL | A |
| 7 | Dakoda Rhéaume-Mullen | D | L | 6'1" | 194 | 2006 | Northville, MI |  |
| 8 | Landon DuPont | D | R | 6'0" | 190 | 2009 | Calgary |  |
| 10 | Drew Schock | D | L | 6'0" | 185 | 2007 | Green Bay, WI |  |
| 21 | Ben Robertson | D | L | 6'1" | 196 | 2004 | Potomac Falls, VA | A |
| 23 | Miles Gust | D | L | 5'10" | 181 | 2004 | Chicago, IL |  |
| 68 | Joshua Orrico | D | R | 5'9" | 176 | 2003 | Woodbridge |  |
| 81 | Cameron Reid | D | L | 6'0" | 194 | 2007 | Aylmer |  |
| 88 | Henry Mews | D | R | 6'1" | 187 | 2006 | Ottawa |  |
| 31 | Stephen Peck | G | L | 6'2" | 185 | 2005 | New York, NY |  |
| 34 | Julian Molinaro | G | L | 5'11" | 187 | 2003 | Mississauga |  |
| 72 | Jack Ivankovic | G | L | 5'11" | 179 | 2007 | Mississauga |  |

### Michigan State Spartans (MSU)

- School: Michigan State University, East Lansing, Michigan
- Arena: Munn Ice Arena
- School colours: Spartan Green `#173F35`, White `#FFFFFF`, Silver `#7C878E` (supporting), Bronze `#696158` (supporting), Action Green `#78BE21` (additional)
- In-game colours: primary `#173F35`, secondary `#FFFFFF`, tertiary `#7C878E`
- Kits: main `#173F35`, second `#FFFFFF`; sheets `kits/michiganstatespartans_home.png`, `kits/michiganstatespartans_away.png`
- Logo: `logos/teams/michiganstatespartans.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 9 | Mason West | C | R | 6'6" | 225 | 2007 | Edina, MN |  |
| 17 | Jack Hextall | C | R | 6'1" | 187 | 2008 | Rolling Meadows, IL |  |
| 21 | Cullen Potter | C | L | 5'10" | 172 | 2007 | Minneapolis, MN |  |
| 26 | Eric Nilson | C | R | 6'0" | 170 | 2007 | Calgary |  |
| 28 | Cayden Lindstrom | C | L | 6'3" | 220 | 2006 | Chetwynd |  |
| 7 | Jimmy Clark | LW | L | 6'0" | 179 | 2004 | Edina, MN |  |
| 12 | Griffin Jurecki | LW | L | 5'10" | 181 | 2003 | Grosse Ile, MI |  |
| 16 | Ethan Belchetz | LW | L | 6'5" | 229 | 2008 | Oakville |  |
| 19 | Diego Johnson | LW | R | 5'11" | 179 | 2006 | Fort St. James |  |
| 25 | Cole Burke | LW | R | 6'2" | 181 | 2004 | Trenton, MI |  |
| 10 | Tommi Männistö | RW | L | 6'0" | 205 | 2004 | Riihimäki | A |
| 15 | Anthony Romani | RW | R | 6'1" | 194 | 2005 | Pickering |  |
| 18 | Ryker Lee | RW | R | 6'1" | 185 | 2006 | Wilmette, IL |  |
| 23 | Shane Vansaghi | RW | R | 6'2" | 216 | 2006 | St. Louis, MO | A |
| 29 | Gavin O'Connell | RW | R | 6'0" | 194 | 2004 | Wayzata, MN |  |
| 2 | Patrick Geary | D | L | 6'1" | 192 | 2004 | Hamburg, NY | C |
| 3 | Sean Barnhill | D | R | 6'6" | 214 | 2007 | Scottsdale, AZ |  |
| 4 | Brady Peddle | D | L | 6'3" | 205 | 2007 | Antigonish |  |
| 6 | Cole Ward | D | R | 6'6" | 209 | 2005 | Rochester Hills, MI |  |
| 11 | Owen West | D | L | 5'11" | 194 | 2004 | Pittsboro, IN |  |
| 14 | Matthew Lahey | D | L | 6'6" | 209 | 2006 | Victoria |  |
| 22 | Tommy Bleyl | D | R | 6'0" | 181 | 2007 | Schenectady, NY |  |
| 27 | Chase Reid | D | R | 6'2" | 194 | 2007 | Chesterfield, MI |  |
| 1 | Quentin Sigurdson | G | L | 6'2" | 174 | 2003 | Sartell, MN |  |
| 31 | Joshua Ravensbergen | G | R | 6'5" | 190 | 2006 | North Vancouver |  |
| 32 | JJ Salajko | G | R | 6'2" | 183 | 2006 | Columbus, OH |  |

### Minnesota Golden Gophers (MIN)

- School: University of Minnesota, Minneapolis, Minnesota
- Arena: 3M Arena at Mariucci
- School colours: Maroon `#862633`, Gold `#FFBF3F`, Tan `#B9975B` (supporting), Brown `#8B5B29` (supporting), Black `#010101` (supporting)
- In-game colours: primary `#862633`, secondary `#FFBF3F`, tertiary `#B9975B`
- Kits: main `#862633`, second `#FFBF3F`; sheets `kits/minnesotagoldengophers_home.png`, `kits/minnesotagoldengophers_away.png`
- Logo: `logos/teams/minnesotagoldengophers.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 3 | Tate Pritchard | C | R | 6'2" | 201 | 2004 | Savage, MN |  |
| 11 | Tanner Ludtke | C | L | 6'0" | 185 | 2004 | Elko, MN |  |
| 17 | Brooks Cullen | C | L | 6'1" | 190 | 2006 | Moorhead, MN |  |
| 34 | Graham Harris | C | L | 6'0" | 185 | 2003 | Excelsior, MN |  |
| 79 | Mason Moe | C | L | 6'2" | 194 | 2007 | Eden Prairie, MN |  |
| 7 | Beckett Hendrickson | LW | L | 6'2" | 190 | 2005 | Minnetonka, MN |  |
| 20 | Jackson Nevers | LW | R | 6'0" | 205 | 2005 | Edina, MN |  |
| 22 | Javon Moore | LW | L | 6'5" | 216 | 2005 | Minnetonka, MN |  |
| 44 | Teddy Townsend | LW | L | 5'11" | 174 | 2005 | Eden Prairie, MN |  |
| 77 | Wyatt Cullen | LW | L | 6'1" | 185 | 2008 | Moorhead, MN |  |
| 18 | L.J. Mooney | RW | R | 5'8" | 174 | 2007 | West Mifflin, PA | A |
| 23 | Austin Burnevik | RW | L | 6'4" | 209 | 2005 | Ham Lake, MN |  |
| 48 | Christian Humphreys | RW | R | 5'11" | 170 | 2006 | Pittsburgh, PA |  |
| 74 | Brodie Ziemer | RW | R | 5'11" | 201 | 2006 | Chaska, MN | C |
| 83 | Jacob Kvasnicka | RW | R | 5'11" | 174 | 2007 | Burnsville, MN |  |
| 4 | John Whipple | D | L | 6'2" | 205 | 2006 | Morristown, NJ | A |
| 5 | Jacob Rombach | D | L | 6'6" | 216 | 2007 | Blaine, MN |  |
| 9 | Finn Loftus | D | L | 6'0" | 201 | 2004 | Blaine, MN |  |
| 10 | Evan Murr | D | R | 6'0" | 185 | 2003 | Stillwater, MN |  |
| 24 | Tarin Smith | D | L | 6'2" | 194 | 2006 | Porcupine Plain |  |
| 25 | Tanner Henricks | D | R | 6'4" | 205 | 2006 | Mission Viejo, CA |  |
| 26 | Axel Löfgren | D | L | 6'4" | 201 | 2005 | Luleå |  |
| 33 | Axel Begley | D | L | 6'2" | 190 | 2003 | Mahtomedi, MN |  |
| 30 | Alex Sabev | G | L | 6'0" | 174 | 2006 | Mounds View, MN |  |
| 31 | Luca Di Pasquo | G | R | 6'1" | 194 | 2003 | Livonia, MI |  |
| 60 | Melvin Strahl | G | L | 6'3" | 165 | 2005 | Sollefteå |  |

### Notre Dame Fighting Irish (ND)

- School: University of Notre Dame, Notre Dame, Indiana
- Arena: Compton Family Ice Arena
- School colours: Notre Dame Blue `#0C2340`, Standard Dome Gold `#C99700`, Irish Green `#00843D` (supporting), Metallic Dome Gold `#AC8A0C` (metallic)
- In-game colours: primary `#0C2340`, secondary `#C99700`, tertiary `#00843D`
- Kits: main `#0C2340`, second `#C99700`; sheets `kits/notredamefightingirish_home.png`, `kits/notredamefightingirish_away.png`
- Logo: `logos/teams/notredamefightingirish.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 11 | Danny Nelson | C | L | 6'3" | 216 | 2005 | Maple Grove, MN | C |
| 13 | Brennan Ali | C | L | 6'0" | 194 | 2004 | Glencoe, IL |  |
| 15 | Jayden Davis | C | L | 5'10" | 190 | 2003 | Calgary |  |
| 17 | Pano Fimis | C | R | 5'11" | 183 | 2004 | Richmond Hill |  |
| 22 | Hagen Burrows | C | R | 6'3" | 176 | 2005 | Orono, MN |  |
| 23 | Michael Schermerhorn | C | L | 6'0" | 216 | 2003 | Traverse City, MI |  |
| 81 | Beckham Edwards | C | L | 6'1" | 187 | 2008 | London |  |
| 9 | Cole Brown | LW | L | 6'3" | 190 | 2005 | Aurora |  |
| 20 | David Klee | LW | L | 6'2" | 181 | 2005 | Castle Rock, CO |  |
| 24 | Dashel Oliver | LW | R | 5'10" | 185 | 2005 | Bloomington, IN |  |
| 25 | Carter Slaggert | LW | L | 5'9" | 174 | 2005 | South Bend, IN |  |
| 8 | William Belle | RW | R | 6'4" | 236 | 2007 | Dongguan |  |
| 14 | Maddox Fleming | RW | R | 5'11" | 198 | 2004 | Rochester, MN |  |
| 19 | Evan Werner | RW | R | 5'9" | 181 | 2003 | Flower Mound, TX |  |
| 34 | Sammy Nelson | RW | R | 6'2" | 192 | 2008 | Maple Grove, MN |  |
| 2 | Jimmy Jurcev | D | R | 6'5" | 201 | 2003 | Palos Heights, IL |  |
| 4 | Michael Quinn | D | R | 5'11" | 176 | 2003 | St. Louis, MO |  |
| 5 | Leo Gruba | D | R | 6'2" | 194 | 2003 | Lake Elmo, MN |  |
| 6 | Drew Mackie | D | L | 6'0" | 187 | 2005 | Anchorage, AK |  |
| 12 | Henry Nelson | D | L | 6'1" | 196 | 2003 | Maple Grove, MN |  |
| 16 | Paul Fischer | D | L | 6'0" | 203 | 2005 | River Forest, IL | A |
| 21 | Jaedon Kerr | D | R | 6'4" | 218 | 2003 | Elk River, MN |  |
| 43 | Danny Klaers | D | R | 6'0" | 185 | 2007 | Minnetrista, MN |  |
| 31 | Nicholas Kempf | G | L | 6'2" | 192 | 2006 | Morton Grove, IL |  |
| 32 | Patrick Quinlan | G | L | 6'1" | 174 | 2007 | Kennett Square, PA |  |
| 37 | Toby Hopp | G | L | 6'1" | 185 | 2004 | Maple Grove, MN |  |

### Ohio State Buckeyes (OSU)

- School: Ohio State University, Columbus, Ohio
- Arena: OSU Ice Rink
- School colours: Scarlet `#BA0C2F`, Gray `#A2AAAD`, Black `#010101` (supporting), Green `#046A38`
- In-game colours: primary `#BA0C2F`, secondary `#A2AAAD`, tertiary `#010101`
- Kits: main `#BA0C2F`, second `#A2AAAD`; sheets `kits/ohiostatebuckeyes_home.png`, `kits/ohiostatebuckeyes_away.png`
- Logo: `logos/teams/ohiostatebuckeyes.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 10 | Thomas Weis | C | R | 5'10" | 181 | 2002 | Madison, WI |  |
| 11 | Jackson Crowder | C | R | 6'3" | 205 | 2007 | Allen, TX |  |
| 25 | Samuel Deckhut | C | L | 5'11" | 194 | 2002 | San Diego, CA |  |
| 29 | Ryan Gordon | C | R | 6'0" | 185 | 2002 | Duluth, GA | A |
| 33 | Erik Påhlsson | C | L | 6'1" | 194 | 2004 | Landskrona |  |
| 49 | Jake Karabela | C | L | 5'11" | 187 | 2004 | Guelph |  |
| 53 | Ben Wilmott | C | L | 6'2" | 190 | 2006 | Seattle, WA |  |
| 56 | Ryan Rucinski | C | R | 5'11" | 185 | 2006 | Broadview Heights, OH |  |
| 9 | Evan Jardine | LW | L | 6'0" | 192 | 2007 | Bloomfield Hills, MI |  |
| 18 | William Tomko | LW | R | 6'0" | 190 | 2006 | McMurray, PA |  |
| 24 | Niall Crocker | LW | R | 6'3" | 196 | 2004 | Delta |  |
| 63 | Nathan Lewis | LW | R | 6'6" | 220 | 2004 | Chicago, IL |  |
| 13 | Félix Caron | RW | R | 6'0" | 181 | 2004 | Terrebonne |  |
| 16 | Max Montes | RW | L | 5'9" | 168 | 2003 | Waukesha, WI |  |
| 19 | James Hong | RW | L | 5'10" | 176 | 2004 | Seoul |  |
| 2 | Chris Able | D | R | 6'1" | 198 | 2004 | Libertyville, IL |  |
| 4 | Frankie Marrelli | D | L | 5'11" | 190 | 2006 | Markham |  |
| 5 | Jakub Fibigr | D | L | 6'1" | 203 | 2006 | Unicov |  |
| 8 | Nathan McBrayer | D | L | 6'0" | 194 | 2004 | Columbus, OH | C |
| 21 | Bryce Ingles | D | R | 5'9" | 194 | 2005 | Clyde, MI |  |
| 27 | Samuel McGinley | D | L | 6'3" | 194 | 2002 | Calgary |  |
| 28 | William Smith | D | L | 6'1" | 201 | 2003 | Toronto |  |
| 78 | Sascha Boumedienne | D | L | 6'3" | 194 | 2007 | Oulu |  |
| 1 | Jack Erickson | G | L | 6'1" | 185 | 2004 | White Bear Lake, MN |  |
| 31 | Jakub Milota | G | L | 6'1" | 187 | 2006 | Ostrava |  |
| 60 | Kristoffer Eberly | G | L | 6'4" | 207 | 2002 | Pinckney, MI |  |

### Penn State Nittany Lions (PSU)

- School: Pennsylvania State University, University Park, Pennsylvania
- Arena: Pegula Ice Arena
- School colours: Navy `#041E42`, White `#FFFFFF`, Gray `#A2AAAD`
- In-game colours: primary `#041E42`, secondary `#FFFFFF`, tertiary `#A2AAAD`
- Kits: main `#041E42`, second `#FFFFFF`; sheets `kits/pennstatenittanylions_home.png`, `kits/pennstatenittanylions_away.png`
- Logo: `logos/teams/pennstatenittanylions.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 11 | Luke Misa | C | L | 5'10" | 181 | 2005 | Oakville |  |
| 17 | Kaden Pitre | C | L | 6'0" | 185 | 2006 | Stouffville |  |
| 19 | Dane Dowiak | C | L | 5'10" | 181 | 2002 | Wexford, PA | C |
| 22 | Kale Dach | C | L | 5'10" | 165 | 2007 | Fort Saskatchewan |  |
| 29 | Reese Laubach | C | R | 6'0" | 185 | 2003 | San Jose, CA | A |
| 33 | Lev Katzin | C | L | 5'8" | 176 | 2007 | Thornhill |  |
| 91 | Andrew Kuzma | C | L | 6'0" | 194 | 2005 | New York, NY |  |
| 21 | Braedon Ford | LW | L | 5'11" | 185 | 2004 | Pittsburgh, PA |  |
| 74 | Shea Busch | LW | L | 6'3" | 216 | 2007 | North Vancouver |  |
| 93 | Shea Van Olm | LW | L | 6'2" | 209 | 2004 | Calgary |  |
| 96 | Justin Larose | LW | L | 5'9" | 174 | 2005 | Saint-Lazare |  |
| 9 | Keaton Peters | RW | R | 5'10" | 187 | 2004 | Sussex, WI |  |
| 26 | Marco Mignosa | RW | R | 6'0" | 172 | 2005 | Toronto |  |
| 89 | Justin Poirier | RW | R | 5'8" | 185 | 2006 | Salaberry-de-Valleyfield |  |
| 2 | Caeden Herrington | D | R | 6'2" | 205 | 2006 | Manchester, VT |  |
| 3 | Casey Aman | D | R | 6'0" | 185 | 2003 | Johnstown, PA |  |
| 4 | Mac Gadowsky | D | L | 6'3" | 190 | 2002 | Fairbanks, AK |  |
| 6 | Ethan Weber | D | R | 6'0" | 196 | 2006 | Manhattan Beach, CA |  |
| 7 | Jackson Smith | D | L | 6'4" | 194 | 2007 | Calgary |  |
| 8 | Charlie Michaud | D | L | 6'0" | 196 | 2006 | Denver, CO |  |
| 10 | Nicholas Fascia | D | L | 6'2" | 185 | 2005 | Blackwood, NJ |  |
| 77 | Nolan Collins | D | R | 6'4" | 216 | 2004 | Whitby |  |
| 1 | Ryder Fetterolf | G | L | 6'0" | 185 | 2008 | Pittsburgh, PA |  |
| 34 | Josh Fleming | G | L | 6'2" | 183 | 2004 | Montréal |  |
| 35 | Crew Petty | G | L | 6'4" | 205 | 2007 | Kennewick, WA |  |

### Wisconsin Badgers (WIS)

- School: University of Wisconsin-Madison, Madison, Wisconsin
- Arena: Kohl Center
- School colours: Cardinal `#BA0C2F`, White `#FFFFFF`, Black `#25282A` (supporting)
- In-game colours: primary `#BA0C2F`, secondary `#FFFFFF`, tertiary `#25282A`
- Kits: main `#BA0C2F`, second `#FFFFFF`; sheets `kits/wisconsinbadgers_home.png`, `kits/wisconsinbadgers_away.png`
- Logo: `logos/teams/wisconsinbadgers.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 17 | Grady Deering | C | R | 6'2" | 205 | 2005 | Des Moines, IA |  |
| 18 | Adam Pietila | C | R | 5'11" | 190 | 2003 | Howell, MI |  |
| 19 | Zach Wooten | C | L | 6'2" | 207 | 2006 | Apple Valley, MN |  |
| 55 | Oliver Tulk | C | R | 5'8" | 174 | 2005 | Gibsons |  |
| 57 | Eetu Orpana | C | L | 6'1" | 196 | 2007 | Lempäälä |  |
| 9 | Chase Jette | LW | R | 5'10" | 179 | 2007 | Lake Forest, IL |  |
| 12 | Gavin Uhlenkamp | LW | R | 5'7" | 170 | 2006 | Chanhassen, MN |  |
| 24 | Talan Blanck | LW | L | 5'11" | 179 | 2005 | Fond du Lac, WI |  |
| 27 | Finn Brink | LW | L | 5'10" | 181 | 2005 | Maple Grove, MN |  |
| 91 | Bruno Idzan | LW | L | 6'0" | 194 | 2006 | Zagreb |  |
| 3 | Brent Solomon | RW | R | 6'0" | 181 | 2007 | Champlin, MN |  |
| 7 | Gavin Morrissey | RW | L | 5'11" | 190 | 2003 | Dayton, OH | A |
| 21 | Ryan Botterill | RW | L | 5'11" | 187 | 2003 | Portage la Prairie |  |
| 86 | J.J. Wiebusch | RW | R | 6'0" | 185 | 2004 | River Falls, WI |  |
| 94 | Vasily Zelenov | RW | L | 6'0" | 194 | 2006 | Moskva |  |
| 2 | Luke Osburn | D | L | 6'1" | 190 | 2006 | Plymouth, MI |  |
| 4 | Dylan Compton | D | R | 6'0" | 194 | 2005 | Victoria |  |
| 5 | Zach Schulz | D | L | 6'1" | 196 | 2005 | South Lyon, MI | A |
| 6 | Logan Hensler | D | R | 6'2" | 198 | 2006 | Woodbury, MN |  |
| 8 | Jack Phelan | D | R | 6'3" | 196 | 2004 | Hinsdale, IL |  |
| 14 | Joe Palodichuk | D | L | 6'0" | 183 | 2003 | Cottage Grove, MN | C |
| 23 | John Stout | D | L | 6'2" | 207 | 2006 | Minnetonka, MN |  |
| 26 | Weston Knox | D | L | 6'0" | 183 | 2004 | Andover, MN |  |
| 30 | Alexis Cournoyer | G | L | 6'4" | 205 | 2005 | Trois-Rivières |  |
| 31 | Daniel Hauser | G | L | 5'11" | 168 | 2004 | Chestermere |  |
| 35 | Xander Miceli | G | L | 6'4" | 194 | 2005 | Nashville, TN |  |

## NCHC

Logo: `logos/leagues/nchc.png`

| Team | Abbr | City, State | Arena | Primary | Secondary | Tertiary | Kit main / second | Logo |
|---|---|---|---|---|---|---|---|---|
| Arizona State Sun Devils | ASU | Tempe, Arizona | Mullett Arena | `#7D2248` | `#FFC72C` | `#010101` | `#7D2248` / `#FFC72C` | `logos/teams/arizonastatesundevils.png` |
| Colorado College Tigers | CC | Colorado Springs, Colorado | Ed Robson Arena | `#010101` | `#FFC72C` | `` | `#010101` / `#FFC72C` | `logos/teams/coloradocollegetigers.png` |
| Denver Pioneers | DEN | Denver, Colorado | Magness Arena | `#BA0C2F` | `#A89968` | `#010101` | `#BA0C2F` / `#A89968` | `logos/teams/denverpioneers.png` |
| Miami RedHawks | MIA | Oxford, Ohio | Goggin Ice Center | `#C8102E` | `#FFFFFF` | `#010101` | `#C8102E` / `#FFFFFF` | `logos/teams/miamiredhawks.png` |
| Minnesota Duluth Bulldogs | UMD | Duluth, Minnesota | AMSOIL Arena | `#862633` | `#FFBF3F` | `#010101` | `#862633` / `#FFBF3F` | `logos/teams/minnesotaduluthbulldogs.png` |
| North Dakota Fighting Hawks | UND | Grand Forks, North Dakota | Ralph Engelstad Arena | `#009A44` | `#FFFFFF` | `#010101` | `#009A44` / `#FFFFFF` | `logos/teams/northdakotafightinghawks.png` |
| Omaha Mavericks | UNO | Omaha, Nebraska | Baxter Arena | `#010101` | `#C8102E` | `#63666A` | `#010101` / `#C8102E` | `logos/teams/omahamavericks.png` |
| St. Cloud State Huskies | SCS | St. Cloud, Minnesota | Brooks Center | `#BA0C2F` | `#010101` | `#A2AAAD` | `#010101` / `#BA0C2F` | `logos/teams/stcloudstatehuskies.png` |
| St. Thomas Tommies | UST | Saint Paul, Minnesota | Lee and Penny Anderson Arena | `#512773` | `#98999B` | `` | `#512773` / `#98999B` | `logos/teams/stthomastommies.png` |
| Western Michigan Broncos | WMU | Kalamazoo, Michigan | Lawson Arena | `#4F2C1D` | `#F1C400` | `#7A6855` | `#4F2C1D` / `#F1C400` | `logos/teams/westernmichiganbroncos.png` |

### Arizona State Sun Devils (ASU)

- School: Arizona State University, Tempe, Arizona
- Arena: Mullett Arena
- School colours: Sun Devil Maroon `#7D2248`, Gold `#FFC72C`, Black `#010101` (supporting), Gray `#5B6770` (supporting), Copper Metallic `#8F654D` (metallic / accent)
- In-game colours: primary `#7D2248`, secondary `#FFC72C`, tertiary `#010101`
- Kits: main `#7D2248`, second `#FFC72C`; sheets `kits/arizonastatesundevils_home.png`, `kits/arizonastatesundevils_away.png`
- Logo: `logos/teams/arizonastatesundevils.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 7 | Nic Chin-Degraves | C | R | 6'1" | 192 | 2003 | Edmonton |  |
| 9 | Jack O'Brien | C | L | 6'1" | 190 | 2003 | Denver, CO |  |
| 16 | Sean McGurn | C | R | 5'11" | 190 | 2002 | Ottawa | A |
| 19 | Logan Morrell | C | R | 6'3" | 212 | 2003 | Mesa, AZ | A |
| 20 | Cade Littler | C | R | 6'4" | 205 | 2004 | East Wenatchee, WA |  |
| 22 | Olivier Houde | C | L | 6'1" | 181 | 2004 | Dosquet |  |
| 47 | Daniel Shlaine | C | L | 6'1" | 185 | 2005 | Moskva |  |
| 13 | Michael Barron | LW | R | 5'5" | 168 | 2006 | Canton, MI |  |
| 17 | Teddy Merrill | LW | R | 6'0" | 185 | 2005 | Scottsdale, AZ |  |
| 18 | Ben Muthersbaugh | LW | L | 6'1" | 205 | 2004 | Gilford, NH | A |
| 89 | Tony Achille | LW | L | 6'0" | 174 | 2002 | Hingham, MA |  |
| 92 | Nathan Behm | LW | R | 6'2" | 207 | 2007 | Calgary |  |
| 2 | Jack Beck | RW | L | 6'0" | 181 | 2003 | Richmond Hill | A |
| 10 | Landon Hafele | RW | L | 6'0" | 179 | 2007 | Fairbanks, AK |  |
| 25 | Kyle Smolen | RW | R | 5'11" | 176 | 2002 | Crystal Lake, IL | C |
| 48 | Benjamin Kevan | RW | R | 6'1" | 179 | 2007 | Fairfield, CA |  |
| 95 | Lukas Sawchyn | RW | R | 5'10" | 174 | 2007 | Grande Prairie |  |
| 3 | Hunter Hady | D | R | 6'4" | 205 | 2004 | Rochester, MN |  |
| 4 | Jack Willson | D | L | 5'8" | 176 | 2005 | Northville, MI |  |
| 5 | Richard Baran | D | L | 6'1" | 190 | 2005 | Bratislava |  |
| 8 | Rylan Brown | D | L | 6'0" | 170 | 2005 | Edmonton |  |
| 11 | Lincoln Kuehne | D | R | 6'2" | 205 | 2007 | Fargo, ND |  |
| 27 | Matthew Mayich | D | L | 6'2" | 196 | 2004 | Stoney Creek |  |
| 33 | Filip Nordberg | D | L | 6'5" | 225 | 2004 | Stockholm |  |
| 44 | Jonas Woo | D | R | 5'9" | 174 | 2006 | Winnipeg |  |
| 73 | Hunter Mullett | D | L | 6'0" | 179 | 2003 | Hartland, WI |  |
| 29 | Sam Hillebrandt | G | L | 6'1" | 179 | 2005 | Port Huron, MI |  |
| 30 | Chase Hamm | G | L | 5'9" | 168 | 2002 | Saskatoon |  |
| 31 | Samuel Urban | G | L | 6'0" | 205 | 2005 | Dolne Kockovce |  |

### Colorado College Tigers (CC)

- School: Colorado College, Colorado Springs, Colorado
- Arena: Ed Robson Arena
- School colours: Black `#010101`, Gold `#FFC72C`
- In-game colours: primary `#010101`, secondary `#FFC72C`
- Kits: main `#010101`, second `#FFC72C`; sheets `kits/coloradocollegetigers_home.png`, `kits/coloradocollegetigers_away.png`
- Logo: `logos/teams/coloradocollegetigers.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 9 | Owen Beckner | C | L | 6'2" | 190 | 2005 | Victoria |  |
| 12 | Zac Olsen | C | R | 6'1" | 203 | 2008 | Calgary |  |
| 17 | Tomas Mrsic | C | L | 6'0" | 174 | 2006 | Surrey |  |
| 24 | Klavs Veinbergs | C | L | 6'3" | 212 | 2003 | Riga |  |
| 27 | Ty Halaburda | C | R | 5'11" | 187 | 2005 | Victoria |  |
| 8 | Brandon Lisowsky | LW | L | 5'9" | 183 | 2004 | Port Coquitlam |  |
| 14 | Connor Hvidston | LW | L | 6'3" | 185 | 2004 | Tisdale |  |
| 25 | Riley Stuart | LW | R | 6'2" | 209 | 2002 | Phoenix, AZ |  |
| 26 | Wilson Björck | LW | L | 6'0" | 176 | 2006 | Stockholm |  |
| 29 | Drew Montgomery | LW | L | 5'11" | 174 | 2003 | Grand Forks, ND |  |
| 10 | Bret Link | RW | R | 6'3" | 196 | 2002 | Anchorage, AK |  |
| 19 | Mateo Mrsic | RW | L | 5'11" | 185 | 2004 | Cloverdale |  |
| 20 | Sebastian Vitali | RW | L | 6'0" | 181 | 2006 | Brooklyn, NY |  |
| 21 | Carmelo Crandell | RW | R | 5'11" | 176 | 2005 | St. Albert |  |
| 22 | Jordan Brisson | RW | L | 6'2" | 190 | 2003 | Manhattan Beach, CA |  |
| 3 | Seth Constance | D | R | 6'2" | 187 | 2004 | Northville, MI |  |
| 4 | Max Burkholder | D | R | 5'9" | 176 | 2003 | Chaska, MN |  |
| 5 | Ryan Koering | D | L | 6'4" | 194 | 2005 | Eden Prairie, MN |  |
| 6 | Colton Roberts | D | R | 6'4" | 209 | 2006 | Ridge Meadows |  |
| 7 | Philippe Blais-Savoie | D | L | 6'1" | 196 | 2005 | San Jose, CA |  |
| 13 | Blake Jones | D | L | 5'10" | 176 | 2005 | Winnipeg |  |
| 15 | Nick Baer | D | R | 6'0" | 185 | 2004 | Minnetonka, MN |  |
| 18 | Fisher Scott | D | L | 6'2" | 185 | 2004 | Carbondale, CO |  |
| 23 | Mats Lindgren | D | L | 6'0" | 183 | 2004 | North Vancouver |  |
| 1 | Beckham Dempsey | G | L | 6'1" | 183 | 2004 | Wayne, IL |  |
| 30 | Jackson Unger | G | R | 6'2" | 190 | 2005 | Calgary |  |
| 32 | Michal Pradel | G | L | 6'5" | 198 | 2007 | Dolny Kubin |  |

### Denver Pioneers (DEN)

- School: University of Denver, Denver, Colorado
- Arena: Magness Arena
- School colours: Crimson `#BA0C2F`, Gold `#A89968`, Black `#010101` (supporting)
- In-game colours: primary `#BA0C2F`, secondary `#A89968`, tertiary `#010101`
- Kits: main `#BA0C2F`, second `#A89968`; sheets `kits/denverpioneers_home.png`, `kits/denverpioneers_away.png`
- Logo: `logos/teams/denverpioneers.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 14 | Max Heise | C | R | 6'3" | 172 | 2006 | Calgary |  |
| 17 | Brady Milburn | C | L | 6'1" | 194 | 2005 | Kamloops |  |
| 19 | Jake Fisher | C | L | 6'2" | 192 | 2005 | Woodbury, MN | A |
| 23 | Ryan Miller | C | L | 6'0" | 181 | 2007 | Medicine Hat |  |
| 28 | Reid Varkonyi | C | L | 5'10" | 183 | 2006 | Sherwood Park |  |
| 8 | Kristian Epperson | LW | L | 6'0" | 185 | 2006 | Mequon, WI |  |
| 12 | Sam Harris | LW | L | 5'11" | 185 | 2003 | San Diego, CA | A |
| 16 | Kyle Chyzowski | LW | L | 5'10" | 170 | 2004 | Surrey | A |
| 22 | Brendan McMorrow | LW | L | 6'0" | 190 | 2006 | Lakeville, MN |  |
| 25 | Clarke Caswell | LW | L | 6'0" | 185 | 2006 | Brandon |  |
| 9 | Mikey Berchild | RW | L | 5'10" | 181 | 2008 | Excelsior, MN |  |
| 10 | Jack Pridham | RW | R | 6'2" | 185 | 2005 | Stouffville |  |
| 18 | Payton Nelson | RW | R | 5'11" | 174 | 2005 | Centennial, CO |  |
| 24 | Kieran Cebrian | RW | L | 6'2" | 194 | 2003 | Denver, CO | C |
| 27 | James Reeder | RW | R | 5'10" | 168 | 2005 | Glenview, IL |  |
| 2 | Tory Pitner | D | R | 6'1" | 190 | 2006 | Greenwich, CT |  |
| 3 | Cale Ashcroft | D | L | 5'10" | 185 | 2004 | St. Albert | A |
| 4 | Ryan Lin | D | R | 5'11" | 176 | 2008 | Richmond |  |
| 6 | Alec Whipple | D | R | 6'0" | 181 | 2004 | Far Hills, NJ |  |
| 11 | Daxon Rudolph | D | R | 6'3" | 205 | 2008 | Lacombe |  |
| 15 | Eric Jamieson | D | L | 6'3" | 205 | 2005 | Calgary |  |
| 20 | Ben Macbeath | D | L | 6'2" | 187 | 2008 | Calgary |  |
| 38 | Blake Fiddler | D | R | 6'5" | 212 | 2007 | Nashville, TN |  |
| 30 | Paxton Geisel | G | L | 6'1" | 190 | 2004 | Estevan |  |
| 31 | Johnny Hicks | G | L | 5'10" | 157 | 2005 | Kamloops |  |
| 35 | Tyler Krivtsov | G | L | 6'2" | 190 | 2003 | Santa Clarita, CA |  |

### Miami RedHawks (MIA)

- School: Miami University, Oxford, Ohio
- Arena: Goggin Ice Center
- School colours: Red `#C8102E`, White `#FFFFFF`, Black `#010101` (supporting)
- In-game colours: primary `#C8102E`, secondary `#FFFFFF`, tertiary `#010101`
- Kits: main `#C8102E`, second `#FFFFFF`; sheets `kits/miamiredhawks_home.png`, `kits/miamiredhawks_away.png`
- Logo: `logos/teams/miamiredhawks.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 8 | John Emmons | C | L | 6'0" | 185 | 2004 | Washington, MI |  |
| 12 | Charles Blanchard | C | R | 6'0" | 192 | 2005 | West Hartford, CT |  |
| 13 | Kocha Delic | C | L | 5'11" | 198 | 2004 | Ottawa |  |
| 18 | Ilia Morozov | C | L | 6'3" | 201 | 2008 | Moskva |  |
| 23 | Matous Jan Kucharcik | C | L | 6'4" | 181 | 2007 | Vipiteno |  |
| 68 | Ethan Hay | C | L | 6'1" | 194 | 2005 | Waterloo |  |
| 4 | Layne Loomer | LW | R | 5'11" | 187 | 2005 | Lethbridge |  |
| 20 | David Deputy | LW | R | 5'9" | 185 | 2004 | Gurnee, IL |  |
| 21 | Bradley Walker | LW | R | 6'1" | 194 | 2004 | Orono, MN |  |
| 27 | Austin Baker | LW | L | 6'0" | 190 | 2006 | White Lake Township, MI |  |
| 37 | Ryan Seelinger | LW | R | 5'11" | 187 | 2005 | Downers Grove, IL |  |
| 10 | Nicholas Mikan | RW | R | 6'4" | 229 | 2004 | Edina, MN |  |
| 14 | Ryan Smith | RW | R | 5'9" | 179 | 2005 | Pendleton, NY |  |
| 19 | Doug Grimes | RW | R | 6'3" | 201 | 2002 | Brookline, MA |  |
| 91 | Matteo Giampa | RW | R | 6'1" | 201 | 2003 | Virgil |  |
| 2 | Shaun McEwen | D | L | 6'0" | 192 | 2004 | Hadley, MA |  |
| 5 | Timofei Runtso | D | R | 6'3" | 196 | 2007 | Marina del Rey, CA |  |
| 9 | Jack Hamilton | D | L | 6'5" | 205 | 2006 | Powell, OH |  |
| 11 | David Helledy | D | L | 6'1" | 194 | 2003 | Milwaukee, WI |  |
| 17 | Cal Huston | D | R | 6'0" | 187 | 2005 | Youngstown, OH |  |
| 22 | Branko Vukas | D | R | 6'4" | 214 | 2005 | Dyer, IN |  |
| 43 | Ryder Thompson | D | L | 5'11" | 183 | 2004 | Russell |  |
| 71 | Vladislav Lukashevich | D | L | 6'1" | 187 | 2003 | Magadan |  |
| 73 | Michael Phelan | D | R | 6'0" | 176 | 2006 | Hinsdale, IL |  |
| 29 | Benjamin Motew | G | L | 6'0" | 185 | 2004 | Glencoe, IL |  |
| 45 | Matteo Drobac | G | L | 6'2" | 201 | 2003 | Oakville |  |
| 72 | Yegor Yegorov | G | L | 6'3" | 192 | 2005 | Moskva |  |

### Minnesota Duluth Bulldogs (UMD)

- School: University of Minnesota Duluth, Duluth, Minnesota
- Arena: AMSOIL Arena
- School colours: Maroon `#862633`, Gold `#FFBF3F`, Black `#010101` (supporting)
- In-game colours: primary `#862633`, secondary `#FFBF3F`, tertiary `#010101`
- Kits: main `#862633`, second `#FFBF3F`; sheets `kits/minnesotaduluthbulldogs_home.png`, `kits/minnesotaduluthbulldogs_away.png`
- Logo: `logos/teams/minnesotaduluthbulldogs.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 10 | Max Plante | C | L | 5'11" | 181 | 2006 | Hermantown, MN | A |
| 15 | Barrett Hall | C | L | 6'2" | 194 | 2003 | Apple Valley, MN |  |
| 17 | Jayson Shaugabay | C | R | 5'10" | 174 | 2005 | Warroad, MN |  |
| 24 | Peyton Platter | C | L | 6'2" | 185 | 2003 | Eau Claire, WI |  |
| 27 | Zam Plante | C | L | 5'9" | 183 | 2004 | Hermantown, MN | A |
| 11 | Hunter Anderson | LW | L | 5'10" | 185 | 2005 | Manhattan Beach, CA |  |
| 12 | Kade Kohanski | LW | R | 5'10" | 165 | 2005 | Hermantown, MN |  |
| 20 | Luke Bibby | LW | L | 6'0" | 205 | 2004 | Brantford |  |
| 21 | Braden Fischer | LW | L | 6'1" | 185 | 2002 | Winnipeg |  |
| 39 | Trevor Stachowiak | LW | R | 6'2" | 196 | 2003 | Dallas, TX |  |
| 16 | Blake Bechen | RW | R | 6'0" | 185 | 2005 | Dubuque, IA |  |
| 19 | Harper Bentz | RW | R | 6'0" | 181 | 2004 | Moorhead, MN |  |
| 22 | Ryan Zaremba | RW | L | 5'10" | 174 | 2006 | Balgonie |  |
| 25 | Callum Arnott | RW | R | 6'1" | 194 | 2004 | King City |  |
| 44 | Victor Plante | RW | L | 5'10" | 165 | 2008 | Hermantown, MN |  |
| 2 | Ty Hanson | D | R | 5'9" | 190 | 2005 | Hermantown, MN | C |
| 3 | Jacob Toll | D | L | 6'3" | 185 | 2004 | Rosemount, MN |  |
| 5 | Brady Cleveland | D | L | 6'5" | 234 | 2005 | Wausau, WI |  |
| 6 | Adam Kleber | D | R | 6'6" | 220 | 2006 | Chaska, MN |  |
| 8 | Aaron Pionk | D | L | 6'1" | 174 | 2003 | Hermantown, MN | A |
| 28 | Keith McInnis | D | L | 6'1" | 196 | 2006 | Red Deer |  |
| 34 | Carter Murphy | D | L | 5'9" | 170 | 2007 | The Woodlands, TX |  |
| 37 | Grayden Siepmann | D | R | 6'0" | 185 | 2004 | Windsor |  |
| 32 | Cole Sheffield | G | L | 5'10" | 181 | 2004 | London |  |
| 35 | Ethan Dahlmeir | G | L | 6'3" | 220 | 2003 | Lakeville, MN |  |
| 38 | Carl Axelsson | G | L | 6'4" | 183 | 2006 | Vaxholm |  |

### North Dakota Fighting Hawks (UND)

- School: University of North Dakota, Grand Forks, North Dakota
- Arena: Ralph Engelstad Arena
- School colours: Kelly Green `#009A44`, White `#FFFFFF`, Black `#010101` (supporting), Gray `#B2B4B2` (supporting)
- In-game colours: primary `#009A44`, secondary `#FFFFFF`, tertiary `#010101`
- Kits: main `#009A44`, second `#FFFFFF`; sheets `kits/northdakotafightinghawks_home.png`, `kits/northdakotafightinghawks_away.png`
- Logo: `logos/teams/northdakotafightinghawks.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 8 | Ollie Josephson | C | L | 6'1" | 190 | 2006 | Victoria |  |
| 11 | Ashton Schultz | C | R | 6'0" | 194 | 2006 | Excelsior, MN |  |
| 15 | Jack Kernan | C | R | 6'2" | 205 | 2005 | Maple Grove, MN |  |
| 17 | Cole Reschny | C | L | 5'11" | 190 | 2007 | Macklin | C |
| 28 | Brock Schultz | C | R | 5'8" | 176 | 2005 | East Grand Forks, MN |  |
| 29 | Hudson Malinoski | C | L | 6'2" | 194 | 2004 | Saskatoon |  |
| 7 | Mac Swanson | LW | L | 5'9" | 170 | 2006 | Nürnberg | A |
| 9 | Will Zellers | LW | L | 6'0" | 185 | 2006 | Maple Grove, MN |  |
| 10 | Kasper Magnussen | LW | L | 6'0" | 207 | 2004 | Düsseldorf |  |
| 19 | Cody Croal | LW | L | 5'11" | 201 | 2003 | North Branch, MN | A |
| 21 | Cooper Simpson | LW | L | 6'1" | 192 | 2007 | Shakopee, MN |  |
| 3 | Connor Davis | RW | R | 6'2" | 196 | 2006 | Vaudreuil-Dorion |  |
| 20 | Gavin Lindberg | RW | R | 6'1" | 201 | 2004 | Fergus Falls, MN |  |
| 24 | Joshua Zakreski | RW | L | 6'0" | 198 | 2005 | Saskatoon |  |
| 27 | Anthony Menghini | RW | L | 5'10" | 194 | 2002 | Brainerd, MN | A |
| 2 | Garrett Lindberg | D | R | 5'11" | 201 | 2007 | Moorhead, MN |  |
| 4 | Carson Carels | D | L | 6'2" | 192 | 2008 | Cypress River |  |
| 5 | Brasen Boser | D | L | 5'11" | 190 | 2004 | Minot, ND |  |
| 6 | EJ Emery | D | R | 6'3" | 194 | 2006 | Surrey |  |
| 13 | Sam Laurila | D | L | 6'1" | 194 | 2006 | Moorhead, MN |  |
| 14 | Bauer Dumanski | D | L | 6'2" | 209 | 2005 | Drake |  |
| 18 | Keaton Verhoeff | D | R | 6'4" | 214 | 2008 | Fort Saskatchewan |  |
| 25 | Ethan MacKenzie | D | L | 6'1" | 194 | 2006 | West Kelowna |  |
| 30 | Caleb Heil | G | L | 6'2" | 207 | 2006 | Victoria, MN |  |
| 32 | Alexander Scheiwiller | G | L | 6'1" | 187 | 2005 | Calgary |  |
| 35 | Jan Spunar | G | L | 6'3" | 203 | 2004 | Olomouc |  |

### Omaha Mavericks (UNO)

- School: University of Nebraska Omaha, Omaha, Nebraska
- Arena: Baxter Arena
- School colours: Black `#010101`, Crimson `#C8102E`, Dark Gray `#63666A` (supporting), Light Gray `#BBBCBC` (supporting)
- In-game colours: primary `#010101`, secondary `#C8102E`, tertiary `#63666A`
- Kits: main `#010101`, second `#C8102E`; sheets `kits/omahamavericks_home.png`, `kits/omahamavericks_away.png`
- Logo: `logos/teams/omahamavericks.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 9 | Cameron Briere | C | L | 5'10" | 163 | 2005 | Fife |  |
| 12 | Luke Woodworth | C | R | 5'9" | 163 | 2004 | Bridgewater |  |
| 23 | Maxime Pellerin | C | R | 6'0" | 194 | 2003 | Victoriaville |  |
| 38 | Trevor Wong | C | L | 5'9" | 159 | 2003 | Vancouver |  |
| 42 | Vincent Desjardins | C | R | 5'11" | 172 | 2006 | Mirabel |  |
| 17 | Gavin Hodnett | LW | L | 5'8" | 161 | 2006 | Winnipeg |  |
| 19 | Sean Tschigerl | LW | L | 6'0" | 190 | 2003 | Whitecourt |  |
| 27 | Joel Plante | LW | R | 5'11" | 161 | 2005 | Langley |  |
| 39 | Maël Lavigne | LW | L | 6'4" | 196 | 2005 | Neuville |  |
| 71 | Brett Hyland | LW | L | 5'11" | 194 | 2003 | Edmonton |  |
| 7 | Logan Wormald | RW | L | 5'10" | 163 | 2005 | Langley |  |
| 15 | Aiden Oiring | RW | L | 5'7" | 181 | 2005 | Calgary |  |
| 18 | Myles Hilman | RW | L | 5'9" | 194 | 2003 | Blackfalds |  |
| 22 | Lincoln Hjelm | RW | R | 6'0" | 185 | 2006 | Omaha, NE |  |
| 72 | Marcus Nguyen | RW | R | 5'10" | 181 | 2004 | Calgary |  |
| 3 | Jayden Jubenvill | D | L | 6'0" | 190 | 2003 | Gilbert Plains |  |
| 6 | Ryan McCleary | D | R | 6'3" | 194 | 2003 | Swift Current |  |
| 8 | Marcus Broberg | D | L | 6'0" | 183 | 2003 | Örebro |  |
| 24 | Marc Lajoie | D | L | 6'6" | 223 | 2003 | St. Albert |  |
| 43 | Arseni Marchenko | D | R | 6'5" | 212 | 2006 | Minsk |  |
| 44 | Spencer Sova | D | L | 6'1" | 190 | 2004 | Windsor |  |
| 59 | Carter Sotheran | D | R | 6'4" | 198 | 2005 | Sanford |  |
| 83 | Parker Berge | D | L | 6'0" | 181 | 2003 | Edmonton |  |
| 1 | Will Craig | G | R | 6'2" | 229 | 2002 | Ennismore |  |
| 35 | William Lacelle | G | L | 6'1" | 185 | 2007 | Gatineau |  |
| 73 | Dawson Cowan | G | L | 6'2" | 187 | 2005 | Warren |  |

### St. Cloud State Huskies (SCS)

- School: St. Cloud State University, St. Cloud, Minnesota
- Arena: Brooks Center
- School colours: Cardinal `#BA0C2F`, Black `#010101`, Blizzard Gray `#A2AAAD` (supporting)
- In-game colours: primary `#BA0C2F`, secondary `#010101`, tertiary `#A2AAAD`
- Kits: main `#010101`, second `#BA0C2F`; sheets `kits/stcloudstatehuskies_home.png`, `kits/stcloudstatehuskies_away.png`
- Logo: `logos/teams/stcloudstatehuskies.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 14 | Noah Urness | C | L | 5'10" | 181 | 2005 | Roseau, MN |  |
| 16 | Hayden Paupanekis | C | R | 6'5" | 212 | 2007 | Winnipeg |  |
| 21 | Jake Southgate | C | L | 5'11" | 185 | 2002 | North Battleford |  |
| 22 | Nolan Roed | C | L | 5'11" | 185 | 2005 | White Bear Lake, MN |  |
| 23 | William Ahlrik | C | L | 6'0" | 194 | 2003 | Tidaholm |  |
| 7 | Jack Reimann | LW | R | 6'0" | 201 | 2002 | Ham Lake, MN |  |
| 11 | John Hirschfeld | LW | L | 5'8" | 159 | 2006 | St. Cloud, MN |  |
| 15 | Adam Vigfusson | LW | L | 5'9" | 161 | 2005 | Gimli |  |
| 19 | Brock Souch | LW | L | 6'0" | 181 | 2006 | Edmonton |  |
| 27 | E.J. Paddington | LW | L | 6'4" | 212 | 2005 | Thunder Bay |  |
| 5 | Carson Pilgrim | RW | R | 5'11" | 176 | 2006 | Warroad, MN |  |
| 13 | Charlie Lurie | RW | L | 5'11" | 187 | 2003 | Minnetonka, MN |  |
| 18 | Aiden Welch | RW | R | 6'2" | 209 | 2004 | White Bear Lake, MN |  |
| 26 | Jamison Sluys | RW | L | 5'10" | 194 | 2005 | Point Roberts, WA |  |
| 28 | Gavyn Thoreson | RW | L | 5'9" | 185 | 2004 | Andover, MN |  |
| 2 | Finn McLaughlin | D | L | 6'2" | 214 | 2006 | Canmore |  |
| 3 | Joe Belisle | D | L | 6'0" | 176 | 2005 | Hugo, MN |  |
| 4 | Jacob Napier | D | L | 6'2" | 205 | 2003 | Lancaster, NY |  |
| 6 | Nolan Stevenson | D | R | 6'2" | 161 | 2005 | Shaunavon |  |
| 8 | Darels Uljanskis | D | L | 6'2" | 194 | 2006 | Riga |  |
| 10 | Garrett Frazer | D | R | 6'1" | 185 | 2006 | Haymarket, VA |  |
| 17 | Will Sharpe | D | L | 6'1" | 198 | 2007 | Delta |  |
| 25 | Thor Byfuglien | D | L | 5'11" | 174 | 2003 | Roseau, MN |  |
| 1 | Yan Shostak | G | L | 6'0" | 185 | 2004 | Minsk |  |
| 35 | Mason McElroy | G | L | 6'0" | 183 | 2006 | St. Michael, MN |  |
| 39 | Patriks Berzins | G | L | 6'1" | 174 | 2003 | Sabile |  |

### St. Thomas Tommies (UST)

- School: University of St. Thomas, Saint Paul, Minnesota
- Arena: Lee and Penny Anderson Arena
- School colours: Tommie Purple `#512773`, Gray `#98999B`
- In-game colours: primary `#512773`, secondary `#98999B`
- Kits: main `#512773`, second `#98999B`; sheets `kits/stthomastommies_home.png`, `kits/stthomastommies_away.png`
- Logo: `logos/teams/stthomastommies.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 14 | Jakub Altrichter | C | L | 5'10" | 154 | 2003 | Praha |  |
| 18 | Jackson Potulny | C | L | 6'3" | 185 | 2005 | Maple Grove, MN |  |
| 21 | Kevin Anderson | C | L | 6'0" | 181 | 2003 | Regina |  |
| 27 | Andrew O'Neill | C | R | 6'2" | 194 | 2007 | Fargo, ND |  |
| 29 | Nathan Pilling | C | L | 6'4" | 209 | 2004 | Calgary |  |
| 9 | Kaeden Hawkins | LW | L | 6'1" | 194 | 2005 | St. Louis, MO |  |
| 11 | Aiden Grossklaus | LW | R | 6'0" | 185 | 2007 | Woodbury, MN |  |
| 22 | Quinn Rudrud | LW | R | 6'3" | 198 | 2001 | Farmington, MN |  |
| 25 | Attila Lippai | LW | L | 5'9" | 181 | 2004 | St. Paul, MN |  |
| 28 | Dylan Godbout | LW | L | 5'11" | 185 | 2004 | Woodbury, MN |  |
| 7 | Winter Wallace | RW | R | 6'4" | 223 | 2002 | Boulder, CO |  |
| 13 | Lucas Van Vliet | RW | L | 6'2" | 181 | 2006 | Livonia, MI |  |
| 15 | Vincent Collard | RW | R | 6'3" | 198 | 2005 | Saguenay |  |
| 17 | David Rozsíval | RW | L | 6'1" | 187 | 2007 | Liberec |  |
| 19 | Kade Stengrim | RW | L | 6'4" | 207 | 2007 | Brainerd, MN |  |
| 4 | Cole Miller | D | R | 5'11" | 185 | 2002 | Littleton, CO |  |
| 6 | Justin Kipkie | D | L | 6'4" | 216 | 2005 | Calgary |  |
| 8 | Luken Huff | D | R | 6'3" | 198 | 2006 | Mazama, WA |  |
| 10 | Mason Poolman | D | L | 6'1" | 190 | 2002 | East Grand Forks, MN |  |
| 12 | Samuel Groebner | D | L | 6'3" | 201 | 2003 | Apple Valley, MN |  |
| 16 | Bauer Berry | D | L | 6'4" | 218 | 2005 | Grand Forks, ND |  |
| 20 | Nick Williams | D | L | 5'9" | 174 | 2002 | Edina, MN |  |
| 26 | Chase Cheslock | D | R | 6'3" | 205 | 2004 | Rogers, MN |  |
| 1 | Will Ingemann | G | L | 5'11" | 179 | 2005 | Plymouth, MN |  |
| 32 | Carsen Musser | G | L | 6'4" | 220 | 2005 | Fairmont, MN |  |
| 35 | Ajay White | G | L | 6'3" | 196 | 2006 | Bryn Mawr, PA |  |

### Western Michigan Broncos (WMU)

- School: Western Michigan University, Kalamazoo, Michigan
- Arena: Lawson Arena
- School colours: Brown `#4F2C1D`, Gold `#F1C400`, Dark Gray `#7A6855` (supporting), Gray `#AFA9A0` (supporting)
- In-game colours: primary `#4F2C1D`, secondary `#F1C400`, tertiary `#7A6855`
- Kits: main `#4F2C1D`, second `#F1C400`; sheets `kits/westernmichiganbroncos_home.png`, `kits/westernmichiganbroncos_away.png`
- Logo: `logos/teams/westernmichiganbroncos.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 9 | Adam Benák | C | L | 5'8" | 163 | 2007 | Plzen |  |
| 16 | Tommy Lafrenière | C | R | 6'0" | 183 | 2007 | St-Sauveur |  |
| 18 | William Whitelaw | C | R | 5'9" | 174 | 2005 | Rosemount, MN |  |
| 21 | Luke McNamara | C | L | 6'3" | 187 | 2005 | Mississauga |  |
| 25 | Cole Spicer | C | L | 5'11" | 185 | 2004 | Grand Forks, ND |  |
| 26 | Ty Mason | C | R | 6'0" | 179 | 2005 | Weyburn |  |
| 12 | Samuel Huck | LW | L | 5'8" | 170 | 2004 | Calgary |  |
| 17 | Ty Henricks | LW | L | 6'5" | 209 | 2005 | Mission Viejo, CA |  |
| 19 | Bobby Cowan | LW | R | 5'11" | 181 | 2006 | Edina, MN |  |
| 22 | Iiro Hakkarainen | LW | L | 5'11" | 172 | 2004 | Helsinki |  |
| 28 | Casy Laylin | LW | R | 5'9" | 174 | 2004 | Sierre |  |
| 32 | Nicholas Sykora | LW | L | 6'0" | 176 | 2007 | Roseland, NJ |  |
| 4 | Zaccharya Wisdom | RW | R | 6'0" | 185 | 2004 | Toronto |  |
| 8 | Zachary Nehring | RW | R | 6'5" | 201 | 2005 | Minot, ND |  |
| 11 | Tyler MacKenzie | RW | L | 5'11" | 190 | 2004 | Red Deer |  |
| 13 | Jordan Keller | RW | L | 5'11" | 176 | 2005 | Sapporo |  |
| 24 | Garrett Szydlowski | RW | R | 6'0" | 190 | 2002 | Detroit, MI |  |
| 2 | Michael Neumeier | D | L | 6'2" | 194 | 2003 | Kerrobert |  |
| 3 | Cole Crusberg-Roseen | D | R | 6'0" | 190 | 2002 | Stratham, NH |  |
| 6 | Theo Wallberg | D | L | 6'5" | 216 | 2003 | Stockholm |  |
| 10 | Zack Sharp | D | L | 6'2" | 194 | 2005 | Naperville, IL |  |
| 14 | Colby Woogk | D | L | 6'1" | 203 | 2004 | Fort Worth, TX |  |
| 15 | Alexander Calbeck | D | L | 6'1" | 190 | 2006 | St. Charles, IL |  |
| 23 | Edison Engle | D | L | 6'2" | 174 | 2007 | Arlington Heights, IL |  |
| 27 | Joona Väisänen | D | R | 6'0" | 176 | 2004 | Espoo |  |
| 29 | Quentin Miller | G | L | 6'3" | 198 | 2004 | Montréal |  |
| 35 | Gavin Moffatt | G | L | 6'0" | 183 | 2004 | Darien, CT |  |
| 37 | Michael Polston | G | L | 6'0" | 176 | 2004 | Cedarburg, WI |  |

## Hockey East

Logo: `logos/leagues/he.png`

| Team | Abbr | City, State | Arena | Primary | Secondary | Tertiary | Kit main / second | Logo |
|---|---|---|---|---|---|---|---|---|
| Boston College Eagles | BC | Chestnut Hill, Massachusetts | Conte Forum | `#862633` | `#DDCBA4` | `#010101` | `#862633` / `#DDCBA4` | `logos/teams/bostoncollegeeagles.png` |
| Boston University Terriers | BU | Boston, Massachusetts | Agganis Arena | `#C8102E` | `#FFFFFF` | `#862633` | `#C8102E` / `#FFFFFF` | `logos/teams/bostonuniversityterriers.png` |
| Maine Black Bears | ME | Orono, Maine | Alfond Arena | `#0C2340` | `#69B3E7` | `#010101` | `#0C2340` / `#69B3E7` | `logos/teams/maineblackbears.png` |
| Merrimack Warriors | MER | North Andover, Massachusetts | J. Thom Lawler Rink | `#003057` | `#F2A900` | `#8098AB` | `#003057` / `#F2A900` | `logos/teams/merrimackwarriors.png` |
| New Hampshire Wildcats | UNH | Durham, New Hampshire | Whittemore Center | `#041E42` | `#FFFFFF` | `#BBBCBC` | `#041E42` / `#FFFFFF` | `logos/teams/newhampshirewildcats.png` |
| Northeastern Huskies | NE | Boston, Massachusetts | Matthews Arena | `#C8102E` | `#010101` | `` | `#010101` / `#C8102E` | `logos/teams/northeasternhuskies.png` |
| Providence Friars | PRO | Providence, Rhode Island | Schneider Arena | `#010101` | `#FFFFFF` | `#A2AAAD` | `#010101` / `#FFFFFF` | `logos/teams/providencefriars.png` |
| UConn Huskies | UCO | Storrs, Connecticut | Toscano Family Ice Forum | `#0C2340` | `#FFFFFF` | `#A2AAAD` | `#0C2340` / `#FFFFFF` | `logos/teams/uconnhuskies.png` |
| UMass Minutemen | UMA | Amherst, Massachusetts | William D. Mullins Memorial Center | `#862633` | `#FFFFFF` | `#010101` | `#862633` / `#FFFFFF` | `logos/teams/umassminutemen.png` |
| UMass Lowell River Hawks | UML | Lowell, Massachusetts | Tsongas Center | `#003DA5` | `#FFFFFF` | `#C8102E` | `#003DA5` / `#FFFFFF` | `logos/teams/umasslowellriverhawks.png` |
| Vermont Catamounts | UVM | Burlington, Vermont | Gutterson Fieldhouse | `#154734` | `#FFC72C` | `#010101` | `#154734` / `#FFC72C` | `logos/teams/vermontcatamounts.png` |

### Boston College Eagles (BC)

- School: Boston College, Chestnut Hill, Massachusetts
- Arena: Conte Forum
- School colours: Cardinal `#862633`, Vegas Gold `#DDCBA4`, Black `#010101` (supporting), Gray `#B2B4B2` (supporting), Heritage Gold `#B3A369`
- In-game colours: primary `#862633`, secondary `#DDCBA4`, tertiary `#010101`
- Kits: main `#862633`, second `#DDCBA4`; sheets `kits/bostoncollegeeagles_home.png`, `kits/bostoncollegeeagles_away.png`
- Logo: `logos/teams/bostoncollegeeagles.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 12 | Jake Sondreal | C | L | 6'0" | 187 | 2004 | Woodbury, MN | A |
| 15 | Olivers Mūrnieks | C | L | 6'1" | 192 | 2008 | Riga |  |
| 18 | Teddy Mutryn | C | R | 6'1" | 209 | 2007 | Norwell, MA |  |
| 29 | Dean Letourneau | C | R | 6'7" | 229 | 2006 | Arnprior |  |
| 40 | Will Moore | C | L | 6'3" | 185 | 2007 | Mississauga |  |
| 4 | Teddy Stiga | LW | L | 5'10" | 179 | 2006 | Sudbury, MA | A |
| 8 | Alex Pelletier | LW | L | 5'10" | 190 | 2005 | Granby, CT |  |
| 9 | Oscar Hemming | LW | L | 6'4" | 205 | 2008 | Vaasa |  |
| 23 | Brady Kudrick | LW | L | 6'0" | 183 | 2005 | Norwell, MA |  |
| 37 | Matias Vanhanen | LW | L | 5'11" | 176 | 2007 | Nokia |  |
| 14 | Landan Resendes | RW | R | 5'10" | 183 | 2005 | Marlborough, MA |  |
| 19 | Casey Mutryn | RW | R | 6'3" | 203 | 2008 | Norwell, MA |  |
| 22 | Will Vote | RW | R | 5'8" | 161 | 2005 | Arlington, MA | C |
| 28 | Tim Delay | RW | L | 6'1" | 183 | 2003 | Hingham, MA |  |
| 36 | Ryan Conmy | RW | R | 5'10" | 185 | 2004 | Alexandria, VA |  |
| 2 | Kristian Kostadinski | D | L | 6'6" | 236 | 2005 | Göteborg |  |
| 5 | Teddy Mallgrave | D | L | 6'3" | 201 | 2005 | Summit, NJ |  |
| 6 | Matthew Grimes | D | L | 6'1" | 185 | 2007 | Locust Valley, NY |  |
| 10 | Luke Schairer | D | R | 6'3" | 194 | 2008 | Milwaukee, WI |  |
| 11 | Quinn Beauchesne | D | R | 6'0" | 185 | 2007 | Ottawa |  |
| 24 | Carson Woodall | D | L | 6'0" | 185 | 2006 | Belle River |  |
| 26 | Luka Radivojevic | D | R | 5'10" | 185 | 2007 | Edina, MN |  |
| 27 | Nolan Joyce | D | R | 6'1" | 194 | 2003 | Dedham, MA |  |
| 1 | Louka Cloutier | G | L | 6'2" | 172 | 2006 | Sherbrooke |  |
| 20 | Roberto Henriquez | G | L | 6'1" | 165 | 2007 | Bratislava |  |
| 30 | Jan Korec | G | L | 6'2" | 205 | 2004 | Bratislava |  |
| 31 | Alex Musielak | G | L | 6'4" | 205 | 2003 | Buffalo, NY |  |

### Boston University Terriers (BU)

- School: Boston University, Boston, Massachusetts
- Arena: Agganis Arena
- School colours: Scarlet `#C8102E`, White `#FFFFFF`, Dark Red `#862633` (supporting), Black `#010101` (supporting), Dark Gray `#53565A` (supporting), Light Gray `#B1B3B3` (supporting)
- In-game colours: primary `#C8102E`, secondary `#FFFFFF`, tertiary `#862633`
- Kits: main `#C8102E`, second `#FFFFFF`; sheets `kits/bostonuniversityterriers_home.png`, `kits/bostonuniversityterriers_away.png`
- Logo: `logos/teams/bostonuniversityterriers.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 8 | Brandon Svoboda | C | R | 6'3" | 209 | 2005 | Level Green, PA |  |
| 10 | Jonathan Morello | C | L | 6'2" | 187 | 2006 | Toronto |  |
| 11 | Tynan Lawrence | C | L | 6'1" | 185 | 2008 | Fredericton |  |
| 16 | Conrad Fondrk | C | L | 6'0" | 203 | 2007 | St. Paul, MN |  |
| 18 | Jay Feldberg | C | R | 6'0" | 185 | 2006 | Toronto |  |
| 19 | Jack Murtagh | C | L | 6'1" | 198 | 2007 | East Greenbush, NY |  |
| 25 | Ben Merrill | C | R | 6'4" | 205 | 2005 | Hingham, MA |  |
| 27 | Caleb Malhotra | C | L | 6'2" | 185 | 2008 | Toronto |  |
| 38 | Kamil Bednarik | C | L | 6'1" | 187 | 2006 | Elk Grove Village, IL | A |
| 7 | Nick Roukounakis | LW | L | 6'2" | 229 | 2003 | Pembroke, MA | A |
| 12 | Jack Harvey | LW | L | 5'10" | 176 | 2003 | Stacy, MN | C |
| 13 | Viktor Nörringer | RW | R | 6'4" | 205 | 2006 | Kungsbacka |  |
| 21 | Ryder Ritchie | RW | R | 6'2" | 190 | 2006 | Calgary |  |
| 81 | Niklas Aaram-Olsen | RW | L | 6'0" | 187 | 2008 | Oslo |  |
| 5 | Adam Barone | D | R | 6'1" | 209 | 2004 | Sault Ste. Marie |  |
| 9 | Jax Wismer | D | L | 6'0" | 176 | 2005 | Newmarket |  |
| 14 | Mick Frechette | D | L | 6'2" | 198 | 2003 | Weston, MA | A |
| 15 | Carter Amico | D | R | 6'5" | 220 | 2007 | Westbrook, ME |  |
| 22 | Aiden Celebrini | D | R | 6'1" | 194 | 2004 | Vancouver | A |
| 61 | Haoxi Wang | D | L | 6'5" | 223 | 2007 | Beijing |  |
| 72 | Xavier Villeneuve | D | L | 5'11" | 163 | 2007 | Laval |  |
| 91 | Charlie Trethewey | D | R | 6'2" | 201 | 2007 | Ellicott City, MD |  |
| 1 | Steven Luciano | G | L | 6'1" | 179 | 2007 | New Canaan, CT |  |
| 30 | Sartaj Dhami | G | L | 6'0" | 165 | 2007 | Calgary |  |
| 33 | Max Lacroix | G | L | 6'0" | 181 | 2003 | Castle Pines, CO |  |
| 40 | Mikhail Yegorov | G | L | 6'5" | 181 | 2006 | St. Petersburg |  |

### Maine Black Bears (ME)

- School: University of Maine, Orono, Maine
- Arena: Alfond Arena
- School colours: Navy `#0C2340`, Columbia Blue `#69B3E7`, Black `#010101` (supporting), Dark Gray `#545859` (supporting), Light Gray `#97999B` (supporting), Red `#9B2743` (supporting)
- In-game colours: primary `#0C2340`, secondary `#69B3E7`, tertiary `#010101`
- Kits: main `#0C2340`, second `#69B3E7`; sheets `kits/maineblackbears_home.png`, `kits/maineblackbears_away.png`
- Logo: `logos/teams/maineblackbears.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 11 | Charlie Russell | C | R | 5'11" | 181 | 2003 | Skaneateles, NY |  |
| 18 | Judah Makway | C | L | 6'3" | 190 | 2004 | Trail |  |
| 27 | Max Scott | C | L | 6'0" | 185 | 2002 | San Jose, CA | A |
| 28 | Masun Fleece | C | R | 5'10" | 174 | 2007 | Rolling Hills, CA |  |
| 29 | Thomas Pichette | C | R | 5'11" | 187 | 2003 | Maskinongé |  |
| 41 | Jaden Lipinski | C | R | 6'4" | 209 | 2004 | Scottsdale, AZ |  |
| 12 | Owen Fowler | LW | L | 5'10" | 190 | 2002 | Tewksbury, MA | C |
| 14 | Sully Scholle | LW | L | 5'10" | 185 | 2003 | Chaska, MN | A |
| 20 | Tanner Klimpke | LW | L | 6'2" | 183 | 2002 | Calgary |  |
| 22 | Will Gerrior | LW | L | 5'10" | 179 | 2004 | Halifax |  |
| 26 | Rowan Calvert | LW | L | 6'0" | 194 | 2005 | Moose Jaw |  |
| 15 | Ross Campbell | RW | R | 5'10" | 181 | 2006 | Souris |  |
| 23 | Lee Parks | RW | R | 6'2" | 209 | 2004 | Stittsville |  |
| 24 | Brock James | RW | R | 6'2" | 201 | 2004 | Falmouth, ME |  |
| 33 | Sam Alfano | RW | R | 6'4" | 209 | 2004 | Hamilton |  |
| 38 | Oskar Komarov | RW | R | 6'0" | 201 | 2003 | Uusikaarlepyy |  |
| 2 | Lukas Peterson | D | R | 5'10" | 168 | 2006 | Waldwick, NJ |  |
| 4 | Chris Romaine | D | R | 6'0" | 196 | 2004 | Boston, MA |  |
| 6 | Alexander Bales | D | L | 5'10" | 181 | 2006 | East Amherst, NY |  |
| 7 | Jérémy Langlois | D | L | 6'1" | 185 | 2003 | Ste-Brigitte-de-Laval |  |
| 13 | Dante Siciliano | D | L | 6'2" | 194 | 2005 | Whitby |  |
| 16 | Loic Usereau | D | L | 6'0" | 176 | 2004 | Saint-Michel |  |
| 17 | Ludvig Lafton | D | L | 6'2" | 194 | 2006 | Bærum |  |
| 21 | Isa Parekh | D | R | 6'0" | 163 | 2003 | Toronto |  |
| 25 | Dru Krebs | D | L | 6'1" | 185 | 2003 | Okotoks |  |
| 30 | Albin Boija | G | L | 6'1" | 194 | 2003 | Sundsvall |  |
| 31 | Petter Wickström Stumer | G | L | 6'6" | 218 | 2004 | Stockholm |  |
| 32 | Colin Ronan | G | L | 6'2" | 190 | 2003 | Saugus, MA |  |

### Merrimack Warriors (MER)

- School: Merrimack College, North Andover, Massachusetts
- Arena: J. Thom Lawler Rink
- School colours: Navy `#003057`, Gold `#F2A900`, 50% Pantone 540 C `#8098AB`
- In-game colours: primary `#003057`, secondary `#F2A900`, tertiary `#8098AB`
- Kits: main `#003057`, second `#F2A900`; sheets `kits/merrimackwarriors_home.png`, `kits/merrimackwarriors_away.png`
- Logo: `logos/teams/merrimackwarriors.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 19 | Jakub Hes | C | L | 6'2" | 181 | 2006 | Zlin |  |
| 23 | Justin Gill | C | L | 6'1" | 190 | 2003 | St-Joseph-du-Lac |  |
| 27 | Mavrick Lachance | C | L | 5'11" | 174 | 2006 | St-Gédéon |  |
| 28 | Ryan O'Connell | C | L | 5'10" | 187 | 2003 | Moorestown, NJ |  |
| 29 | Caelan Fitzpatrick | C | L | 5'11" | 185 | 2003 | Moose Jaw |  |
| 6 | Daniel Astapovich | LW | R | 6'3" | 198 | 2005 | Birmingham, MI |  |
| 12 | Parker Lalonde | LW | R | 6'1" | 176 | 2004 | Aberdeen |  |
| 20 | Nolan Flamand | LW | L | 5'9" | 181 | 2004 | Saskatoon |  |
| 21 | Caden Cranston | LW | R | 5'10" | 174 | 2004 | Rochester, NY |  |
| 22 | Connor Dale | LW | L | 6'2" | 183 | 2005 | St. Albert |  |
| 10 | Trevor Hoskin | RW | R | 6'1" | 196 | 2004 | Belleville |  |
| 11 | Nick Pierre | RW | L | 5'10" | 176 | 2004 | Cottage Grove, MN |  |
| 16 | Ty Daneault | RW | R | 5'11" | 192 | 2004 | Red Deer |  |
| 25 | Alex Mercier | RW | R | 5'10" | 183 | 2005 | L'Ancienne-Lorette |  |
| 26 | Michael Emerson | RW | R | 6'1" | 201 | 2003 | Yorktown Heights, NY |  |
| 2 | Nathan King | D | L | 6'2" | 192 | 2004 | Victoria |  |
| 3 | Simon-Pier Brunet | D | R | 6'2" | 205 | 2005 | Gatineau |  |
| 4 | Seamus Powell | D | L | 6'0" | 165 | 2004 | Marcellus, NY |  |
| 5 | Keaton Orrey | D | L | 6'1" | 201 | 2006 | Inver Grove Heights, MN |  |
| 7 | Ethan Beyer | D | R | 5'11" | 183 | 2004 | Philadelphia, PA |  |
| 8 | David Green | D | L | 6'2" | 203 | 2006 | Toronto |  |
| 9 | Tristen Doyle | D | R | 5'9" | 185 | 2006 | Winnipeg |  |
| 17 | Cameron Kungle | D | L | 6'2" | 205 | 2002 | Red Deer |  |
| 24 | Austin Oravetz | D | R | 5'9" | 192 | 2003 | Canonsburg, PA |  |
| 30 | Charlie Durkin | G | L | 6'1" | 183 | 2006 | Glencoe, IL |  |
| 31 | Ryan Keyes | G | L | 6'2" | 172 | 2003 | Fairbanks, AK |  |
| 88 | Linards Feldbergs | G | L | 6'1" | 201 | 2005 | Ogre |  |

### New Hampshire Wildcats (UNH)

- School: University of New Hampshire, Durham, New Hampshire
- Arena: Whittemore Center
- School colours: Blue `#041E42`, White `#FFFFFF`, Gray `#BBBCBC` (supporting)
- In-game colours: primary `#041E42`, secondary `#FFFFFF`, tertiary `#BBBCBC`
- Kits: main `#041E42`, second `#FFFFFF`; sheets `kits/newhampshirewildcats_home.png`, `kits/newhampshirewildcats_away.png`
- Logo: `logos/teams/newhampshirewildcats.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 6 | Martins Lavins | C | R | 6'1" | 194 | 2003 | Riga | C |
| 9 | Kole Hyles | C | R | 6'0" | 190 | 2006 | Newfields, NH |  |
| 16 | J.P. Turner | C | R | 6'0" | 194 | 2003 | East Falmouth, MA | A |
| 20 | Ryan Johnson | C | R | 5'10" | 181 | 2003 | Calgary |  |
| 21 | Nicholas Van Tassell | C | R | 6'4" | 201 | 2004 | Basking Ridge, NJ |  |
| 47 | Sam Oliver | C | L | 5'11" | 183 | 2004 | Quispamsis |  |
| 61 | Cameron MacDonald | C | L | 6'2" | 194 | 2003 | Toronto |  |
| 14 | Émile Guité | LW | L | 6'2" | 187 | 2007 | Chambly |  |
| 17 | Mason Zebeski | LW | L | 6'2" | 196 | 2005 | Oakville |  |
| 18 | Nicky Romeo | LW | L | 6'0" | 181 | 2005 | New City, NY |  |
| 91 | Jacob Newcombe | LW | L | 6'0" | 203 | 2004 | Halifax |  |
| 11 | Rasmus Svartström | RW | L | 6'5" | 220 | 2005 | Helsinki |  |
| 22 | Félix Gagnon | RW | R | 6'0" | 181 | 2004 | Saguenay |  |
| 23 | Jason Siedem | RW | R | 5'10" | 185 | 2002 | Madison, NJ |  |
| 37 | Nick Ring | RW | R | 5'10" | 185 | 2003 | Hingham, MA | A |
| 2 | Alex Carr | D | R | 6'0" | 194 | 2004 | Sackville |  |
| 4 | Ryan Philbrick | D | L | 5'11" | 185 | 2004 | Concord, NH |  |
| 5 | Wyatt Kennedy | D | R | 6'5" | 212 | 2005 | Ottawa |  |
| 8 | Parker Von Richter | D | R | 6'1" | 205 | 2006 | Mississauga |  |
| 15 | Oscar Plandowski | D | R | 6'0" | 194 | 2003 | Red Deer |  |
| 19 | Brendan Fitzgerald | D | L | 6'0" | 170 | 2003 | North Reading, MA | A |
| 57 | Malte Vass | D | L | 6'3" | 194 | 2007 | Karlstad |  |
| 72 | Conner de Haro | D | L | 5'11" | 194 | 2005 | Raleigh, NC |  |
| 1 | Stefan Carney | G | R | 6'0" | 185 | 2003 | Wheeling, IL |  |
| 31 | Michael Simpson | G | L | 6'1" | 198 | 2003 | London |  |
| 33 | Teagan Kendrick | G | L | 6'4" | 201 | 2004 | St. Albert |  |

### Northeastern Huskies (NE)

- School: Northeastern University, Boston, Massachusetts
- Arena: Matthews Arena
- School colours: Red `#C8102E`, Black `#010101`
- In-game colours: primary `#C8102E`, secondary `#010101`
- Kits: main `#010101`, second `#C8102E`; sheets `kits/northeasternhuskies_home.png`, `kits/northeasternhuskies_away.png`
- Logo: `logos/teams/northeasternhuskies.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 8 | Matthew Perkins | C | L | 5'11" | 174 | 2004 | Balgonie |  |
| 9 | Tyler Fukakusa | C | L | 5'11" | 181 | 2002 | Toronto |  |
| 11 | Eli Sebastian | C | L | 6'0" | 192 | 2004 | Burlington | A |
| 13 | Charles Pardue | C | R | 5'11" | 190 | 2006 | Winnetka, IL |  |
| 14 | Joe Connor | C | L | 5'10" | 181 | 2005 | Amherst, NH | A |
| 81 | Jacob Mathieu | C | L | 5'11" | 192 | 2004 | St-Odilon-de-Cranbourne | A |
| 92 | Matthew Maltais | C | L | 6'2" | 190 | 2004 | St-Basile-le-Grand |  |
| 3 | Amine Hajibi | LW | L | 6'1" | 185 | 2005 | Montréal |  |
| 10 | Trey Jefferis | LW | L | 5'10" | 192 | 2005 | Hummelstown, PA |  |
| 19 | Marc Lund | LW | R | 6'4" | 205 | 2003 | Edina, MN |  |
| 21 | Jack Pechar | LW | R | 6'1" | 198 | 2005 | Niskayuna, NY |  |
| 22 | Giacomo Martino | LW | L | 5'11" | 185 | 2005 | Toronto |  |
| 6 | Grayson Badger | RW | R | 6'2" | 187 | 2004 | Hyde Park, MA |  |
| 7 | Andy Moore | RW | R | 5'11" | 194 | 2002 | Cumberland, ME | A |
| 23 | Dawson Sharkey | RW | R | 6'1" | 190 | 2006 | Souris |  |
| 88 | Miguel Marques | RW | R | 5'11" | 190 | 2006 | Prince George |  |
| 2 | Joaquim Lemay | D | L | 6'1" | 185 | 2002 | St-Pierre-les-Becquets | C |
| 4 | Alex Pineau | D | R | 6'3" | 216 | 2003 | Thunder Bay |  |
| 5 | Joel Kjellberg | D | R | 6'0" | 190 | 2004 | Nacka |  |
| 15 | Marcus Kearsey | D | L | 5'11" | 183 | 2006 | Deer Lake |  |
| 18 | Liam O'Keeffe | D | L | 6'0" | 190 | 2006 | Boston, MA |  |
| 33 | Noah Jones | D | R | 6'5" | 225 | 2004 | Bridgewater, NJ |  |
| 44 | Owen Keefe | D | L | 6'0" | 185 | 2006 | Saugus, MA |  |
| 49 | Jordan Tourigny | D | R | 6'0" | 190 | 2005 | Québec |  |
| 91 | Noah Laberge | D | L | 6'1" | 190 | 2006 | St-Bruno-de-Montarville |  |
| 37 | Sam Caulfield | G | L | 6'3" | 185 | 2006 | Needham, MA |  |
| 47 | Lawton Zacher | G | L | 6'2" | 190 | 2003 | Buffalo, NY |  |
| 70 | Matthew DellaRusso | G | L | 6'3" | 187 | 2004 | Darien, CT |  |

### Providence Friars (PRO)

- School: Providence College, Providence, Rhode Island
- Arena: Schneider Arena
- School colours: Black `#010101`, White `#FFFFFF`, Silver `#A2AAAD`, Silver Metallic `#8D9093` (metallic)
- In-game colours: primary `#010101`, secondary `#FFFFFF`, tertiary `#A2AAAD`
- Kits: main `#010101`, second `#FFFFFF`; sheets `kits/providencefriars_home.png`, `kits/providencefriars_away.png`
- Logo: `logos/teams/providencefriars.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 8 | J.J. Monteiro | C | L | 6'0" | 194 | 2006 | Hudson, MA |  |
| 13 | Ryder Cali | C | L | 6'2" | 218 | 2008 | Lugano |  |
| 14 | Aleksi Kivioja | C | R | 6'5" | 220 | 2003 | Helsinki | A |
| 16 | Jonathan Fauchon | C | L | 5'11" | 176 | 2004 | Ste-Claire-de-Bellechasse |  |
| 18 | Carter Klippenstein | C | L | 6'3" | 185 | 2006 | Lethbridge |  |
| 22 | Clint Levens | C | R | 6'3" | 185 | 2003 | Windermere, FL |  |
| 26 | Julius Sumpf | C | L | 6'2" | 198 | 2005 | München |  |
| 37 | Thomas Vandenberg | C | L | 6'0" | 181 | 2008 | Nepean |  |
| 12 | Blake Zielinski | LW | R | 6'0" | 187 | 2008 | Berlin, NJ |  |
| 17 | Beau Jelsma | LW | L | 5'10" | 181 | 2004 | London |  |
| 21 | John Mustard | LW | L | 6'1" | 185 | 2006 | Newmarket |  |
| 34 | Logan Sawyer | LW | L | 6'1" | 174 | 2006 | Orangeville |  |
| 11 | Graham Gamache | RW | R | 5'11" | 183 | 2002 | Edmonton | C |
| 15 | Will Elger | RW | R | 6'2" | 174 | 2004 | Westerville, OH |  |
| 19 | Tanner Adams | RW | R | 5'11" | 185 | 2005 | Northport, NY |  |
| 2 | Tomas Machu | D | R | 6'4" | 194 | 2003 | Ostrava | A |
| 3 | Jack Laferriere | D | L | 6'3" | 201 | 2005 | Chatham, NJ |  |
| 4 | Everett Baldwin | D | R | 6'0" | 183 | 2007 | Providence, RI |  |
| 10 | Donovan McCoy | D | R | 6'1" | 205 | 2004 | Belleville |  |
| 20 | Cade Christenson | D | L | 6'3" | 183 | 2004 | Edmonton |  |
| 23 | Andrew Centrella | D | L | 5'10" | 190 | 2004 | Philadelphia, PA |  |
| 24 | Quinn Mantei | D | L | 6'0" | 187 | 2005 | Weyburn |  |
| 27 | Alexander Rybakov | D | L | 6'2" | 190 | 2005 | Chelyabinsk |  |
| 28 | Martin Masa | D | L | 6'2" | 194 | 2005 | Brno |  |
| 30 | John Parsons | G | L | 6'3" | 194 | 2006 | Ithaca, NY |  |
| 31 | Gibson Homer | G | L | 6'5" | 209 | 2003 | Grand Rapids, MI |  |
| 33 | Aaron Matthews | G | L | 5'11" | 170 | 2004 | Newton, MA |  |

### UConn Huskies (UCO)

- School: University of Connecticut, Storrs, Connecticut
- Arena: Toscano Family Ice Forum
- School colours: Navy `#0C2340`, White `#FFFFFF`, Gray `#A2AAAD` (supporting), Red `#E4002B` (supporting), Light Blue `#A4C8E1` (supporting)
- In-game colours: primary `#0C2340`, secondary `#FFFFFF`, tertiary `#A2AAAD`
- Kits: main `#0C2340`, second `#FFFFFF`; sheets `kits/uconnhuskies_home.png`, `kits/uconnhuskies_away.png`
- Logo: `logos/teams/uconnhuskies.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 10 | Brittan Alstead | C | L | 5'9" | 174 | 2006 | Plymouth, MN |  |
| 12 | Carter Sproule | C | L | 6'3" | 207 | 2005 | Grand Forks, ND |  |
| 15 | Colin Frank | C | R | 5'6" | 163 | 2006 | Ladera Ranch, CA |  |
| 16 | Jérémy Loranger | C | R | 5'9" | 161 | 2007 | Trois-Rivières |  |
| 26 | Mike Murtagh | C | R | 5'11" | 187 | 2004 | East Greenbush, NY | A |
| 9 | Cole Davis | LW | L | 5'11" | 187 | 2006 | Sutton |  |
| 13 | Ethan Gardula | LW | L | 5'11" | 174 | 2005 | Princeton, MA |  |
| 22 | Alexandre Blais | LW | L | 5'10" | 176 | 2005 | Longueuil |  |
| 24 | Carlin Dezainde | LW | L | 5'11" | 172 | 2004 | Calgary |  |
| 8 | Joey Muldowney | RW | R | 5'9" | 161 | 2004 | Lake View, NY | C |
| 20 | Kaden Shahan | RW | R | 6'0" | 170 | 2005 | Everett, WA |  |
| 23 | Jesse Heslop | RW | L | 5'11" | 179 | 2006 | Nanaimo |  |
| 27 | Joseph Odyniec | RW | R | 6'2" | 201 | 2004 | Washington, DC |  |
| 29 | Lirim Amidovski | RW | R | 6'1" | 174 | 2006 | Alliston |  |
| 71 | Ethan Whitcomb | RW | L | 6'4" | 209 | 2004 | St. Catharines |  |
| 2 | Brendan Dunphy | D | L | 6'5" | 207 | 2006 | San Diego, CA |  |
| 6 | Jacob Reese | D | R | 6'0" | 190 | 2006 | Boston, NY |  |
| 7 | Charlie Morrison | D | L | 6'3" | 198 | 2007 | Miramichi |  |
| 11 | Kai Janviriya | D | R | 5'8" | 170 | 2005 | Bloomfield, MI |  |
| 18 | William McIsaac | D | R | 6'3" | 205 | 2006 | Vancouver |  |
| 21 | Trey Scott | D | R | 5'10" | 181 | 2004 | Lunenburg, MA | A |
| 28 | Anthony Allain-Samaké | D | L | 6'2" | 183 | 2007 | Gatineau |  |
| 34 | Owen Simpson | D | L | 6'5" | 190 | 2003 | Toronto |  |
| 1 | Kam Hendrickson | G | R | 6'2" | 185 | 2006 | Chanhassen, MN |  |
| 30 | Veeti Louhivaara | G | L | 6'4" | 207 | 2006 | Jyväskylä |  |
| 31 | Ryan Sanborn | G | L | 6'2" | 190 | 2005 | Brookfield, CT |  |

### UMass Minutemen (UMA)

- School: University of Massachusetts Amherst, Amherst, Massachusetts
- Arena: William D. Mullins Memorial Center
- School colours: Maroon `#862633`, White `#FFFFFF`, Black `#010101` (supporting), Gray `#AEAEAE` (supporting)
- In-game colours: primary `#862633`, secondary `#FFFFFF`, tertiary `#010101`
- Kits: main `#862633`, second `#FFFFFF`; sheets `kits/umassminutemen_home.png`, `kits/umassminutemen_away.png`
- Logo: `logos/teams/umassminutemen.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 10 | Jack Galanek | C | L | 6'0" | 187 | 2006 | Hopkinton, MA |  |
| 17 | Daniel Jencko | C | L | 6'0" | 192 | 2005 | Humenne |  |
| 19 | Michael DeAngelo | C | L | 5'11" | 194 | 2004 | Itasca, IL |  |
| 22 | Max Curran | C | L | 6'3" | 194 | 2006 | Praha |  |
| 25 | Cameron Dunn | C | R | 6'3" | 201 | 2003 | Holland, MI |  |
| 9 | Jack Musa | LW | L | 5'11" | 187 | 2003 | Estero, FL | A |
| 14 | Egor Barabanov | LW | L | 6'0" | 181 | 2006 | St. Petersburg |  |
| 26 | Jasper Kuhta | LW | L | 6'2" | 205 | 2006 | Helsinki |  |
| 27 | Melvin Novotny | LW | L | 6'2" | 198 | 2007 | Täby |  |
| 28 | John McNelis | LW | L | 5'10" | 165 | 2006 | Potomac, MD |  |
| 7 | Maxim Massé | RW | R | 6'3" | 198 | 2006 | Rimouski |  |
| 8 | Cameron O'Neill | RW | R | 6'0" | 198 | 2004 | Odenton, MD | A |
| 11 | Václav Nestrašil | RW | R | 6'6" | 205 | 2007 | Praha |  |
| 12 | Lukas Klecka | RW | R | 5'11" | 183 | 2005 | Bratislava |  |
| 15 | Justin Kerr | RW | R | 6'5" | 225 | 2004 | Grand Rapids, MN |  |
| 34 | Gavin Cornforth | RW | R | 5'9" | 170 | 2006 | Boston, MA |  |
| 2 | Coleson Hanrahan | D | R | 5'10" | 187 | 2005 | Longmeadow, MA | C |
| 3 | Barrett Dexheimer | D | L | 5'11" | 176 | 2006 | Edina, MN |  |
| 4 | Samu Alalauri | D | R | 6'2" | 220 | 2008 | Lahti |  |
| 6 | Mathieu Taillefer | D | R | 6'2" | 201 | 2006 | Richelieu |  |
| 13 | Landon Nycz | D | L | 6'3" | 216 | 2007 | Detroit, MI |  |
| 18 | Larry Keenan | D | L | 6'4" | 205 | 2005 | Barrie | A |
| 21 | Charlie Lieberman | D | R | 6'5" | 231 | 2004 | Naperville, IL |  |
| 23 | Francesco Dell'Elce | D | L | 6'1" | 185 | 2005 | Toronto |  |
| 1 | Jackson Irving | G | L | 6'2" | 185 | 2004 | Newbury, MA |  |
| 29 | Tobias Trejbal | G | R | 6'4" | 196 | 2007 | Most |  |
| 32 | Ethan Eskit | G | L | 6'0" | 190 | 2006 | Calgary |  |

### UMass Lowell River Hawks (UML)

- School: University of Massachusetts Lowell, Lowell, Massachusetts
- Arena: Tsongas Center
- School colours: Blue `#003DA5`, White `#FFFFFF`, Red `#C8102E`, Black `#010101` (supporting), Graphite `#63666A` (supporting), Light Blue `#7D9CC0` (supporting)
- In-game colours: primary `#003DA5`, secondary `#FFFFFF`, tertiary `#C8102E`
- Kits: main `#003DA5`, second `#FFFFFF`; sheets `kits/umasslowellriverhawks_home.png`, `kits/umasslowellriverhawks_away.png`
- Logo: `logos/teams/umasslowellriverhawks.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 7 | Cole Lonsdale | C | L | 5'11" | 170 | 2004 | Mississauga |  |
| 10 | Chris Delaney | C | L | 5'7" | 154 | 2004 | Hopkinton, MA |  |
| 14 | Dalyn Wakely | C | R | 6'1" | 203 | 2004 | Port Hope |  |
| 15 | David Adaszynski | C | L | 5'11" | 196 | 2005 | Burnaby |  |
| 28 | Tobias Pitka | C | R | 6'4" | 225 | 2006 | Poprad |  |
| 29 | Jak Vaarwerk | C | L | 6'0" | 190 | 2003 | East Amherst, NY | C |
| 19 | Kolin Sisson | LW | R | 5'10" | 179 | 2006 | Lincoln, RI |  |
| 25 | Kuzma Voronin | LW | L | 5'11" | 172 | 2006 | Yaroslavl |  |
| 26 | Joseph McGraw | LW | R | 6'1" | 205 | 2005 | Sugar Land, TX |  |
| 72 | Nicholas Wellenreiter | LW | R | 5'11" | 168 | 2006 | Dundas |  |
| 88 | Jacob MacDonald | LW | L | 6'2" | 190 | 2004 | Fort Worth, TX |  |
| 3 | Dominick Rivelli | RW | R | 5'10" | 185 | 2003 | Highland Park, IL |  |
| 8 | Jaiden Moriello | RW | R | 6'2" | 194 | 2003 | Saugus, MA |  |
| 20 | Libor Nemec | RW | L | 6'3" | 196 | 2003 | Bardejov | A |
| 23 | Misha Volotovskii | RW | L | 6'2" | 190 | 2005 | Calgary |  |
| 2 | Daniel Buchbinder | D | L | 6'3" | 194 | 2004 | Buffalo, NY |  |
| 6 | Félix Plamondon | D | L | 6'2" | 181 | 2006 | St-Raymond |  |
| 11 | Tnias Mathurin | D | L | 6'3" | 201 | 2004 | Ajax |  |
| 12 | Thomas Stewart | D | R | 6'2" | 185 | 2003 | Toronto |  |
| 13 | Nate Misskey | D | R | 6'4" | 212 | 2005 | Melfort |  |
| 18 | Sean Kilcullen | D | L | 6'0" | 190 | 2002 | Kensington, MD | A |
| 22 | Josh Mori | D | L | 5'11" | 174 | 2004 | Richmond | A |
| 27 | Dominic Payne | D | R | 6'1" | 176 | 2004 | North Vancouver |  |
| 44 | Ryan Kroll | D | L | 6'1" | 194 | 2006 | Plainfield, IL |  |
| 30 | Nikola Goich | G | R | 6'4" | 174 | 2004 | Homer Glen, IL |  |
| 31 | Austin Elliott | G | L | 6'2" | 185 | 2004 | Strathmore |  |
| 34 | Cole Moore | G | L | 6'6" | 201 | 2003 | Toronto |  |

### Vermont Catamounts (UVM)

- School: University of Vermont, Burlington, Vermont
- Arena: Gutterson Fieldhouse
- School colours: Green `#154734`, Gold `#FFC72C`, Black `#010101` (supporting), Brown `#8B5B29` (supporting), 49% Pantone 466 C `#E3D5BC` (supporting), Orange `#FF6A14` (supporting)
- In-game colours: primary `#154734`, secondary `#FFC72C`, tertiary `#010101`
- Kits: main `#154734`, second `#FFC72C`; sheets `kits/vermontcatamounts_home.png`, `kits/vermontcatamounts_away.png`
- Logo: `logos/teams/vermontcatamounts.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 10 | Max Strand | C | R | 5'10" | 181 | 2003 | Roseau, MN |  |
| 11 | Zach Filak | C | L | 6'1" | 185 | 2004 | Northville, MI |  |
| 12 | Jonah Aegerter | C | R | 5'9" | 183 | 2004 | Janesville, WI |  |
| 18 | Mathias Loiselle | C | L | 6'3" | 205 | 2005 | St-Jean-sur-Richelieu |  |
| 21 | Daniel Sambuco | C | L | 6'0" | 207 | 2003 | Springfield, PA |  |
| 37 | Cameron Garvey | C | L | 6'0" | 181 | 2002 | Oakville |  |
| 9 | Michael La Starza | LW | L | 5'11" | 190 | 2004 | Montréal |  |
| 15 | Cam Springer | LW | R | 6'0" | 187 | 2006 | Dexter, MI |  |
| 16 | Tyler Dysart | LW | L | 5'10" | 170 | 2004 | Sunnyvale, CA |  |
| 17 | Colin Kessler | LW | L | 6'0" | 181 | 2004 | Anchorage, AK |  |
| 19 | Cédrick Guindon | LW | L | 5'11" | 185 | 2004 | Rockland |  |
| 13 | Blake Steenerson | RW | R | 6'4" | 209 | 2005 | Maple Grove, MN |  |
| 22 | Félix Lacerte | RW | L | 5'10" | 176 | 2006 | Trois-Rivières |  |
| 24 | Logan Renkowski | RW | L | 5'9" | 181 | 2005 | Nesconset, NY |  |
| 28 | Matteo Michels | RW | R | 5'11" | 170 | 2005 | Colleyville, TX |  |
| 29 | Ethan Burroughs | RW | R | 5'10" | 190 | 2003 | Oakville |  |
| 2 | Matej Teply | D | L | 6'1" | 185 | 2005 | Stamford, CT |  |
| 4 | Andoni Fimis | D | R | 6'1" | 185 | 2006 | Richmond Hill |  |
| 5 | Mathieu Plante | D | L | 6'0" | 179 | 2006 | Québec |  |
| 7 | P.J. Forgione | D | L | 6'2" | 214 | 2004 | South Burlington, VT |  |
| 20 | Michael Hagens | D | L | 6'0" | 198 | 2005 | Hauppauge, NY |  |
| 23 | David Jesus | D | R | 6'4" | 187 | 2002 | Toronto |  |
| 25 | Jack Malinski | D | R | 5'10" | 170 | 2002 | Lakeville, MN |  |
| 27 | Duncan Ramsay | D | L | 6'5" | 212 | 2004 | Halifax | C |
| 38 | Sebastian Törnqvist | D | R | 6'0" | 190 | 2003 | Everlöv | A |
| 32 | Dylan Silverstein | G | L | 6'0" | 179 | 2004 | Calabasas, CA |  |
| 34 | Aiden Wright | G | L | 6'1" | 185 | 2005 | Wake Forest, NC |  |
| 39 | Jacob Oster | G | L | 6'3" | 194 | 2004 | Manotick |  |

## ECAC Hockey

Logo: `logos/leagues/ecac.png`

| Team | Abbr | City, State | Arena | Primary | Secondary | Tertiary | Kit main / second | Logo |
|---|---|---|---|---|---|---|---|---|
| Brown Bears | BRN | Providence, Rhode Island | Meehan Auditorium | `#3F2021` | `#BA0C2F` | `` | `#3F2021` / `#BA0C2F` | `logos/teams/brownbears.png` |
| Clarkson Golden Knights | CLK | Potsdam, New York | Cheel Arena | `#004E42` | `#FFCD00` | `#010101` | `#004E42` / `#FFCD00` | `logos/teams/clarksongoldenknights.png` |
| Colgate Raiders | COL | Hamilton, New York | Class of 1965 Arena | `#862633` | `#FFFFFF` | `#010101` | `#862633` / `#FFFFFF` | `logos/teams/colgateraiders.png` |
| Cornell Big Red | COR | Ithaca, New York | Lynah Rink | `#A6192E` | `#FFFFFF` | `#010101` | `#A6192E` / `#FFFFFF` | `logos/teams/cornellbigred.png` |
| Dartmouth Big Green | DAR | Hanover, New Hampshire | Thompson Arena | `#046A38` | `#FFFFFF` | `#010101` | `#046A38` / `#FFFFFF` | `logos/teams/dartmouthbiggreen.png` |
| Harvard Crimson | HAR | Cambridge, Massachusetts | Bright-Landry Hockey Center | `#9D2235` | `#FFFFFF` | `#010101` | `#9D2235` / `#FFFFFF` | `logos/teams/harvardcrimson.png` |
| Princeton Tigers | PRI | Princeton, New Jersey | Hobey Baker Memorial Rink | `#E87722` | `#010101` | `` | `#010101` / `#E87722` | `logos/teams/princetontigers.png` |
| Quinnipiac Bobcats | QU | Hamden, Connecticut | M&T Bank Arena | `#0C2340` | `#FFB81C` | `#418FDE` | `#0C2340` / `#FFB81C` | `logos/teams/quinnipiacbobcats.png` |
| RPI Engineers | RPI | Troy, New York | Houston Field House | `#DA291C` | `#FFFFFF` | `#010101` | `#DA291C` / `#FFFFFF` | `logos/teams/rpiengineers.png` |
| St. Lawrence Saints | SLU | Canton, New York | Appleton Arena | `#A6192E` | `#623B2A` | `#D3BC8D` | `#623B2A` / `#A6192E` | `logos/teams/stlawrencesaints.png` |
| Union Garnet Chargers | UNI | Schenectady, New York | M&T Bank Center | `#862633` | `#FFFFFF` | `#010101` | `#862633` / `#FFFFFF` | `logos/teams/uniongarnetchargers.png` |
| Yale Bulldogs | YAL | New Haven, Connecticut | Ingalls Rink | `#0C2340` | `#FFFFFF` | `#00346A` | `#0C2340` / `#FFFFFF` | `logos/teams/yalebulldogs.png` |

### Brown Bears (BRN)

- School: Brown University, Providence, Rhode Island
- Arena: Meehan Auditorium
- School colours: Brown `#3F2021`, Cardinal Red `#BA0C2F`
- In-game colours: primary `#3F2021`, secondary `#BA0C2F`
- Kits: main `#3F2021`, second `#BA0C2F`; sheets `kits/brownbears_home.png`, `kits/brownbears_away.png`
- Logo: `logos/teams/brownbears.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 12 | Benjamin Poitras | C | R | 6'0" | 190 | 2005 | Montréal |  |
| 15 | Charlie Gollob | C | L | 6'2" | 201 | 2004 | Toronto |  |
| 17 | Michael Cataldo | C | R | 6'1" | 201 | 2003 | Norfolk, MA |  |
| 18 | George Poirier | C | R | 6'2" | 201 | 2005 | Arvada, CO |  |
| 29 | Andrew King | C | L | 6'0" | 170 | 2003 | Dubuque, IA |  |
| 9 | Matteo Danis | LW | L | 6'1" | 194 | 2005 | Calgary |  |
| 13 | Michael Salandra | LW | R | 5'10" | 181 | 2003 | Pleasantville, NY |  |
| 19 | Matthew Brille | LW | R | 5'10" | 179 | 2003 | Bethesda, MD |  |
| 21 | Matthew Cataldo | LW | L | 6'0" | 205 | 2005 | Norfolk, MA |  |
| 55 | Brian Nicholas | LW | L | 6'2" | 185 | 2005 | Scarsdale, NY |  |
| 7 | Archer Brown | RW | L | 6'1" | 196 | 2005 | New York, NY |  |
| 11 | Trip Pendy | RW | L | 6'1" | 201 | 2005 | Far Hills, NJ |  |
| 14 | Brendan Tighe | RW | R | 5'11" | 181 | 2006 | Birmingham, MI |  |
| 28 | Cullen Emery | RW | R | 5'10" | 190 | 2005 | Los Angeles, CA |  |
| 34 | Griffin Erdman | RW | L | 5'10" | 187 | 2005 | Wilmington, DE |  |
| 2 | Tristan Zarsky | D | R | 6'3" | 225 | 2003 | Edmonton |  |
| 5 | Kevin Fitzgerald | D | L | 6'0" | 185 | 2004 | Bath |  |
| 6 | Gerrit Lansing | D | R | 6'0" | 190 | 2005 | New York, NY |  |
| 10 | Ashton Bynum | D | L | 6'4" | 201 | 2004 | Tulsa, OK |  |
| 16 | Owen Dyer | D | R | 6'3" | 225 | 2004 | Clarkston, MI |  |
| 20 | Marco Senerchia | D | L | 5'9" | 161 | 2007 | Greenville, RI |  |
| 22 | Ethan Mistry | D | L | 5'10" | 174 | 2003 | Toronto |  |
| 24 | Brian Lonergan | D | R | 5'11" | 176 | 2005 | Montvale, NJ |  |
| 26 | Leo Schwartz | D | R | 6'0" | 174 | 2004 | New York, NY |  |
| 27 | Jack Hewitt | D | R | 6'0" | 185 | 2003 | Toronto |  |
| 1 | Freddie Halyk | G | L | 6'6" | 225 | 2003 | Cochrane |  |
| 30 | Devin Shakar | G | R | 6'1" | 165 | 2005 | Glenville, NY |  |
| 31 | Ryan Kazmouz | G | L | 6'0" | 170 | 2003 | Livonia, MI |  |
| 35 | Eli Pulver | G | L | 6'1" | 165 | 2003 | Vancouver |  |

### Clarkson Golden Knights (CLK)

- School: Clarkson University, Potsdam, New York
- Arena: Cheel Arena
- School colours: Dark Green `#004E42`, Gold `#FFCD00`, Black `#010101` (supporting)
- In-game colours: primary `#004E42`, secondary `#FFCD00`, tertiary `#010101`
- Kits: main `#004E42`, second `#FFCD00`; sheets `kits/clarksongoldenknights_home.png`, `kits/clarksongoldenknights_away.png`
- Logo: `logos/teams/clarksongoldenknights.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 10 | Rémi Gélinas | C | L | 6'0" | 187 | 2005 | Deux-Montagnes |  |
| 15 | Sebastien Gervais | C | L | 6'1" | 190 | 2005 | Windsor |  |
| 16 | Hayden Reid | C | L | 6'0" | 181 | 2006 | Orono |  |
| 20 | Jared Mangan | C | L | 5'10" | 172 | 2003 | Charleston, SC |  |
| 21 | Alex Pharand | C | R | 6'3" | 192 | 2005 | Sudbury |  |
| 26 | Tyler Wallace | C | L | 6'2" | 187 | 2002 | Calgary |  |
| 37 | Maël St-Denis | C | L | 6'0" | 196 | 2005 | Nominingue |  |
| 86 | Adrian Misaljevic | C | L | 5'11" | 174 | 2004 | Woodbridge |  |
| 91 | Mikael Huchette | C | R | 6'3" | 198 | 2003 | Longueuil |  |
| 92 | Justin Côté | C | L | 5'6" | 163 | 2004 | Salaberry-de-Valleyfield |  |
| 14 | Talon Sigurdson | LW | R | 6'2" | 181 | 2002 | Sartell, MN |  |
| 19 | Owen Van Steensel | LW | L | 5'10" | 185 | 2004 | Ilderton |  |
| 81 | Jace Letourneau | LW | L | 6'5" | 190 | 2004 | Arnprior |  |
| 11 | Luka Sukovic | RW | R | 6'3" | 216 | 2003 | Lincolnshire, IL |  |
| 13 | Shawn O'Donnell | RW | L | 5'11" | 181 | 2003 | Pittsburgh, PA |  |
| 28 | Conyr Hellyer | RW | L | 6'3" | 209 | 2004 | Okotoks |  |
| 2 | Michael Craig | D | R | 5'11" | 187 | 2002 | St. Catharines |  |
| 4 | Nikolas Young | D | L | 6'0" | 172 | 2006 | Lawton, NY |  |
| 8 | Axel Dufresne | D | R | 6'0" | 194 | 2005 | Boisbriand |  |
| 12 | Adam Cardona | D | R | 6'2" | 201 | 2003 | Beaconsfield |  |
| 17 | Zackary Sandhu | D | L | 6'2" | 205 | 2005 | Toronto |  |
| 27 | Tate Taylor | D | R | 5'11" | 174 | 2004 | Richmond |  |
| 44 | Ty Brassington | D | L | 6'1" | 185 | 2004 | White Rock | C |
| 55 | Jack Sparkes | D | R | 6'8" | 238 | 2003 | Ottawa |  |
| 72 | Bryce Sookro | D | R | 6'1" | 223 | 2004 | Nelson |  |
| 30 | Shane Soderwall | G | L | 6'2" | 185 | 2002 | Algonquin, IL |  |
| 35 | Dawson Labre | G | L | 6'3" | 185 | 2004 | Les Cèdres |  |
| 70 | Ben Gaudreau | G | L | 6'2" | 174 | 2003 | North Bay |  |

### Colgate Raiders (COL)

- School: Colgate University, Hamilton, New York
- Arena: Class of 1965 Arena
- School colours: Colgate Maroon `#862633`, Ice White `#FFFFFF`, Night Black `#010101` (supporting)
- In-game colours: primary `#862633`, secondary `#FFFFFF`, tertiary `#010101`
- Kits: main `#862633`, second `#FFFFFF`; sheets `kits/colgateraiders_home.png`, `kits/colgateraiders_away.png`
- Logo: `logos/teams/colgateraiders.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 17 | Braiden Clark | C | L | 6'0" | 194 | 2005 | Upper Arlington, OH |  |
| 18 | Elias Zimmerman | C | L | 6'7" | 220 | 2005 | Gävle |  |
| 20 | Easton Wainwright | C | L | 5'10" | 190 | 2005 | Mount Hope |  |
| 21 | Nicholas Rexine | C | R | 6'2" | 194 | 2002 | Syracuse, NY |  |
| 23 | Tobias Öhman | C | L | 5'7" | 174 | 2006 | Uppsala |  |
| 25 | Tyson Doucette | C | L | 6'0" | 181 | 2004 | Sault Ste. Marie |  |
| 29 | Davide Patella | C | L | 6'2" | 194 | 2004 | Laval |  |
| 12 | Samuel Lyne | LW | L | 6'2" | 223 | 2005 | Chelmsford |  |
| 14 | Jack Brandt | LW | L | 5'10" | 170 | 2004 | Columbus, OH |  |
| 15 | Luke Pakulak | LW | R | 6'4" | 205 | 2003 | Surrey |  |
| 19 | Ryan Spinale | LW | R | 6'6" | 216 | 2004 | Plymouth, MA |  |
| 22 | Porter Byrd-Leitner | LW | L | 6'0" | 183 | 2006 | Moorestown, NJ |  |
| 8 | Luka Shcherbyna | RW | R | 5'10" | 174 | 2006 | Vancouver |  |
| 10 | Simone Dadiè | RW | L | 6'2" | 209 | 2003 | Brunico |  |
| 13 | Max Nagel | RW | R | 5'8" | 161 | 2004 | West St. Paul, MN |  |
| 16 | Ryan Sullivan | RW | L | 6'2" | 190 | 2002 | Calgary |  |
| 24 | Jake Schneider | RW | R | 5'11" | 198 | 2002 | Plymouth, MN |  |
| 2 | Nathan Larioza | D | L | 6'1" | 214 | 2006 | Los Gatos, CA |  |
| 4 | Brandt Dubey | D | L | 6'2" | 176 | 2006 | Orchard Park, NY |  |
| 7 | Kyle Kim | D | R | 6'2" | 187 | 2005 | Seoul |  |
| 9 | Isaiah Norlin | D | R | 6'6" | 205 | 2003 | Minneapolis, MN |  |
| 11 | Antonio Fernandez | D | R | 5'8" | 179 | 2003 | San Jose, CA |  |
| 26 | Joshua Niedermayer | D | L | 6'0" | 207 | 2004 | Newport Beach, CA |  |
| 27 | Brett Merner | D | R | 6'1" | 181 | 2004 | Nanaimo |  |
| 28 | Luke Malboeuf | D | R | 6'0" | 190 | 2005 | Monroeville, PA |  |
| 1 | Andrew Krakora | G | L | 6'0" | 185 | 2005 | Pittsburgh, PA |  |
| 35 | Jack Olson | G | R | 6'1" | 174 | 2003 | St. Louis Park, MN |  |
| 37 | Reid Dyck | G | L | 6'4" | 196 | 2004 | Winkler |  |

### Cornell Big Red (COR)

- School: Cornell University, Ithaca, New York
- Arena: Lynah Rink
- School colours: Carnelian Red `#A6192E`, White `#FFFFFF`, Black `#010101` (supporting), Dark Brown `#693F23` (supporting), Brown `#946037` (supporting)
- In-game colours: primary `#A6192E`, secondary `#FFFFFF`, tertiary `#010101`
- Kits: main `#A6192E`, second `#FFFFFF`; sheets `kits/cornellbigred_home.png`, `kits/cornellbigred_away.png`
- Logo: `logos/teams/cornellbigred.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 8 | Luke Devlin | C | L | 6'3" | 209 | 2004 | Memphis, TN |  |
| 10 | Jacob Kraft | C | L | 5'8" | 181 | 2003 | Rochester, NY |  |
| 14 | Ryan Walsh | C | R | 6'1" | 194 | 2003 | Rochester, NY | C |
| 15 | Tyler Catalano | C | R | 6'4" | 216 | 2004 | St. Louis, MO |  |
| 20 | Brad Gardiner | C | R | 6'1" | 185 | 2005 | Aurora |  |
| 21 | Louie Wehmann | C | L | 6'3" | 209 | 2006 | Wayzata, MN |  |
| 12 | Caton Ryan | LW | L | 6'1" | 185 | 2006 | San Francisco, CA |  |
| 13 | Giovanni DiGiulian | LW | R | 6'1" | 181 | 2005 | South Burlington, VT |  |
| 17 | Aiden Long | LW | L | 6'2" | 209 | 2005 | Mono |  |
| 18 | Reegan Hiscock | LW | L | 6'5" | 209 | 2004 | St. John's |  |
| 19 | Connor Arseneault | LW | L | 6'2" | 212 | 2006 | Saint John |  |
| 27 | Callum Hughes | LW | L | 6'1" | 170 | 2006 | Basking Ridge, NJ |  |
| 7 | Ivan Zadvernyuk | RW | L | 6'3" | 201 | 2004 | Moskva |  |
| 9 | Charlie Major | RW | R | 5'10" | 179 | 2004 | Skaneateles, NY |  |
| 22 | Chase Pirtle | RW | R | 6'2" | 176 | 2005 | Far Hills, NJ |  |
| 23 | Charles Arend | RW | L | 6'0" | 185 | 2006 | Wilmette, IL |  |
| 29 | Parker Murray | RW | L | 6'5" | 229 | 2003 | Manhattan Beach, CA |  |
| 36 | Coco Armstrong | RW | R | 6'0" | 185 | 2006 | West Vancouver |  |
| 2 | Hudson Gorski | D | R | 6'3" | 181 | 2005 | Scotch Plains, NJ |  |
| 3 | Luke McCrady | D | L | 6'3" | 185 | 2005 | Calgary |  |
| 4 | Xavier Veilleux | D | L | 6'1" | 192 | 2006 | L'Ancienne-Lorette |  |
| 5 | Ryland Randle | D | L | 5'10" | 170 | 2006 | Winnipeg |  |
| 6 | George Fegaras | D | R | 6'2" | 212 | 2004 | Richmond Hill |  |
| 11 | Luke Ashton | D | L | 6'6" | 238 | 2005 | North Vancouver |  |
| 16 | Michael Fisher | D | R | 6'2" | 194 | 2004 | Westborough, MA |  |
| 24 | Nicholas Wolfenberg | D | R | 6'2" | 212 | 2004 | Calgary |  |
| 26 | Donovan Hamilton | D | L | 6'3" | 198 | 2005 | Buffalo, NY |  |
| 28 | Cole Tuminaro | D | R | 6'4" | 225 | 2007 | South Beloit, IL |  |
| 37 | Justin Katz | G | R | 6'2" | 183 | 2004 | Mont-Royal |  |
| 40 | Erick Roest | G | L | 6'0" | 181 | 2004 | Lethbridge |  |
| 60 | Mathis Rousseau | G | L | 5'11" | 174 | 2004 | Montréal |  |

### Dartmouth Big Green (DAR)

- School: Dartmouth College, Hanover, New Hampshire
- Arena: Thompson Arena
- School colours: Dartmouth Green `#046A38`, Snow White `#FFFFFF`, Midnight Black `#010101` (supporting), Winter Gray `#C6BCD0`, Granite Gray `#584446`
- In-game colours: primary `#046A38`, secondary `#FFFFFF`, tertiary `#010101`
- Kits: main `#046A38`, second `#FFFFFF`; sheets `kits/dartmouthbiggreen_home.png`, `kits/dartmouthbiggreen_away.png`
- Logo: `logos/teams/dartmouthbiggreen.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 7 | Nikita Nikora | C | L | 5'10" | 170 | 2004 | St. Petersburg |  |
| 19 | Drew Stewart | C | R | 5'9" | 176 | 2005 | Minnetonka, MN |  |
| 27 | Tyler Grahme | C | R | 5'11" | 185 | 2004 | Bloomington, MN |  |
| 34 | Ben Buckley | C | R | 5'11" | 179 | 2005 | Warwick, RI |  |
| 71 | Brock Cummings | C | L | 6'2" | 216 | 2005 | Mississauga |  |
| 77 | Grayden Robertson-Palmer | C | L | 5'11" | 192 | 2007 | Kanata |  |
| 8 | Austin Salani | LW | R | 6'2" | 194 | 2003 | Hancock, MI |  |
| 10 | Hayden Stavroff | LW | R | 5'11" | 190 | 2004 | Dublin, OH |  |
| 11 | Nathan Morin | LW | L | 5'10" | 161 | 2004 | St-Augustin-de-Desmaures |  |
| 13 | Joshua Schenk | LW | R | 6'0" | 163 | 2004 | Whitehorse |  |
| 21 | Andrew Clarke | LW | R | 5'11" | 170 | 2004 | St. Louis, MO |  |
| 23 | Grant Young | LW | R | 5'11" | 179 | 2006 | Boulder, CO |  |
| 18 | Hank Cleaves | RW | L | 6'4" | 201 | 2003 | Riverside, CT | A |
| 26 | Ryan Schelling | RW | R | 6'0" | 174 | 2004 | Airmont, NY |  |
| 86 | Jack Silverberg | RW | R | 5'9" | 165 | 2004 | Sherwood Park |  |
| 92 | Jason Stefanek | RW | R | 5'11" | 165 | 2004 | Yorba Linda, CA |  |
| 93 | Jesse Allecia | RW | R | 6'1" | 192 | 2005 | New York, NY |  |
| 2 | Eric Charpentier | D | L | 6'3" | 205 | 2002 | Silver Spring, MD | A |
| 4 | C.J. Foley | D | R | 5'11" | 174 | 2004 | Hanover, MA | C |
| 6 | Matthew Fusco | D | R | 5'11" | 194 | 2003 | Westwood, MA |  |
| 9 | Colin Grable | D | R | 6'2" | 205 | 2003 | Spring Lake, NJ | A |
| 12 | Tim Busconi | D | R | 6'1" | 170 | 2004 | Milton, MA |  |
| 20 | Brock Devlin | D | L | 5'10" | 165 | 2005 | Mission Viejo, CA |  |
| 22 | Cooper Cleaves | D | R | 6'3" | 205 | 2006 | Riverside, CT |  |
| 24 | Matthew Desiderio | D | L | 6'3" | 209 | 2005 | North Caldwell, NJ |  |
| 43 | Owen Desilets | D | L | 6'1" | 174 | 2004 | Airdrie |  |
| 44 | Ryan Harvey | D | L | 6'0" | 194 | 2005 | Stouffville |  |
| 32 | Roan Clarke | G | L | 6'3" | 190 | 2003 | Cloverdale |  |
| 70 | Emmett Croteau | G | L | 6'4" | 209 | 2003 | Bonnyville |  |
| 88 | Sebastian Gatto | G | L | 6'4" | 185 | 2006 | Troy, MI |  |

### Harvard Crimson (HAR)

- School: Harvard University, Cambridge, Massachusetts
- Arena: Bright-Landry Hockey Center
- School colours: Crimson `#9D2235`, White `#FFFFFF`, Black `#010101` (supporting), Anthracite `#373A36` (supporting), Light Tonal `#C8C9C7` (additional), Dark Tonal `#572932` (additional), Gold `#89734C` (metallic / additional)
- In-game colours: primary `#9D2235`, secondary `#FFFFFF`, tertiary `#010101`
- Kits: main `#9D2235`, second `#FFFFFF`; sheets `kits/harvardcrimson_home.png`, `kits/harvardcrimson_away.png`
- Logo: `logos/teams/harvardcrimson.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 9 | Heikki Ruohonen | C | L | 6'2" | 205 | 2006 | Helsinki |  |
| 16 | Will McDonough | C | R | 6'2" | 201 | 2005 | Duxbury, MA |  |
| 17 | William Hughes | C | R | 6'3" | 216 | 2004 | Andover, MA |  |
| 19 | Ben MacDonald | C | L | 6'1" | 194 | 2004 | Weston, MA |  |
| 21 | Cameron Johnson | C | L | 5'10" | 168 | 2004 | Vancouver |  |
| 22 | Justin Graf | C | L | 6'1" | 174 | 2007 | Boston, MA |  |
| 24 | Chase Stefanek | C | L | 6'2" | 192 | 2006 | Yorba Linda, CA |  |
| 6 | Richard Gallant | LW | L | 5'8" | 170 | 2007 | Concord, MA |  |
| 10 | Ryan Fine | LW | R | 5'9" | 183 | 2005 | Center Moriches, NY |  |
| 11 | Justin Solovey | LW | L | 6'2" | 209 | 2004 | Holmdel, NJ |  |
| 27 | Mick Thompson | LW | L | 5'9" | 192 | 2004 | Toronto |  |
| 57 | Eli Heinze | LW | R | 6'3" | 194 | 2005 | Santa Barbara, CA |  |
| 7 | Niles Benson | RW | R | 6'1" | 190 | 2007 | Safety Harbor, FL |  |
| 8 | Michael Callow | RW | R | 6'4" | 220 | 2004 | Boston, MA |  |
| 15 | Joseph Salandra | RW | R | 5'10" | 198 | 2008 | Pleasantville, NY |  |
| 23 | Salvatore Guzzo | RW | R | 6'1" | 185 | 2005 | Old Tappan, NJ |  |
| 88 | Aidan Lane | RW | R | 6'1" | 201 | 2007 | Mississauga |  |
| 2 | David Hejduk | D | R | 6'2" | 205 | 2004 | Parker, CO |  |
| 3 | Matthew Biotti | D | R | 6'4" | 181 | 2004 | Cambridge, MA |  |
| 5 | Sean Keohane | D | L | 6'4" | 205 | 2004 | Boston, MA |  |
| 12 | Alex Huang | D | R | 6'0" | 181 | 2007 | Rosemère |  |
| 14 | Matthew Morden | D | L | 6'4" | 203 | 2004 | Hamilton |  |
| 18 | Donato Bracco | D | L | 5'10" | 161 | 2007 | Locust Valley, NY |  |
| 20 | Drake Murray | D | R | 6'1" | 201 | 2006 | Manhattan Beach, CA |  |
| 26 | Lucas St-Louis | D | L | 5'11" | 181 | 2005 | Burlington, VT |  |
| 44 | James Mackey | D | L | 6'6" | 243 | 2005 | Villanova, PA |  |
| 1 | Evan Zhang | G | L | 6'2" | 165 | 2006 | Sherborn, MA |  |
| 25 | Rudy Guimond | G | L | 6'4" | 187 | 2005 | Pointe-Claire |  |
| 31 | Ben Charette | G | L | 6'3" | 183 | 2003 | Aurora |  |

### Princeton Tigers (PRI)

- School: Princeton University, Princeton, New Jersey
- Arena: Hobey Baker Memorial Rink
- School colours: Orange `#E87722`, Black `#010101`
- In-game colours: primary `#E87722`, secondary `#010101`
- Kits: main `#010101`, second `#E87722`; sheets `kits/princetontigers_home.png`, `kits/princetontigers_away.png`
- Logo: `logos/teams/princetontigers.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 10 | Malcolm Green | C | R | 6'6" | 225 | 2005 | Bedford |  |
| 12 | Jake Manfre | C | L | 6'0" | 205 | 2004 | Smithtown, NY | A |
| 13 | Drew Garzone | C | R | 6'1" | 194 | 2004 | Dover, MA |  |
| 14 | Carson Buydens | C | R | 6'3" | 201 | 2004 | Gladstone |  |
| 18 | Josh Karnish | C | R | 6'0" | 185 | 2003 | Marlton, NJ | A |
| 9 | Hans Martin Ulvebne | LW | L | 6'0" | 194 | 2004 | Oslo |  |
| 11 | Shaan Kingwell | LW | L | 6'0" | 174 | 2007 | Ottawa |  |
| 22 | Matthew Souliere | LW | L | 6'3" | 205 | 2006 | London |  |
| 24 | Jake Gudelj | LW | L | 6'2" | 205 | 2005 | North Vancouver |  |
| 26 | Luc Pelletier | LW | L | 6'4" | 205 | 2003 | Courtenay |  |
| 7 | Kai Daniells | RW | L | 6'0" | 185 | 2003 | Whistler | A |
| 15 | Julian Facchinelli | RW | R | 6'0" | 205 | 2004 | Woodbridge |  |
| 17 | Miles Gunty | RW | R | 5'11" | 185 | 2004 | Bethesda, MD |  |
| 19 | Seamus Latta | RW | L | 6'0" | 194 | 2005 | Bozeman, MT |  |
| 21 | Michael Young | RW | L | 5'11" | 190 | 2002 | Kings Park, NY |  |
| 2 | Dennison Monahan | D | L | 6'6" | 225 | 2006 | Norwell, MA |  |
| 3 | Patrick Reilly | D | R | 6'1" | 201 | 2004 | Avon, NJ |  |
| 4 | Christian Lyons | D | L | 6'1" | 185 | 2007 | St. Louis, MO |  |
| 5 | Max Lavoie | D | L | 6'4" | 216 | 2006 | Victoria |  |
| 6 | Ian Devlin | D | L | 6'2" | 209 | 2002 | Memphis, TN | C |
| 23 | Kai Greaves | D | R | 6'2" | 205 | 2003 | Cambridge |  |
| 27 | Ethan Mittelsteadt | D | L | 6'0" | 190 | 2005 | Kelowna |  |
| 33 | Chris Reiniger | D | R | 5'10" | 170 | 2006 | Annapolis, MD |  |
| 1 | Arthur Smith | G | L | 6'4" | 209 | 2004 | Farmington, CT |  |
| 30 | Conor Callaghan | G | L | 6'0" | 181 | 2003 | Upper Saddle River, NJ |  |
| 31 | Brady Morris | G | L | 5'10" | 174 | 2006 | Rye, NY |  |
| 35 | Daniel Moor | G | L | 6'2" | 190 | 2006 | Omaha, NE |  |

### Quinnipiac Bobcats (QU)

- School: Quinnipiac University, Hamden, Connecticut
- Arena: M&T Bank Arena
- School colours: Navy `#0C2340`, Bobcat Gold `#FFB81C`, York Hill Sky Blue `#418FDE` (supporting), Darkest Gray `#63666A` (additional), Dark Gray `#888B8D` (additional), Medium Gray `#A7A8A9` (additional), Light Gray `#C8C9C7` (additional), Silver `#8D9093` (metallic / additional / special uses)
- In-game colours: primary `#0C2340`, secondary `#FFB81C`, tertiary `#418FDE`
- Kits: main `#0C2340`, second `#FFB81C`; sheets `kits/quinnipiacbobcats_home.png`, `kits/quinnipiacbobcats_away.png`
- Logo: `logos/teams/quinnipiacbobcats.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 12 | Markus Vidicek | C | L | 5'10" | 174 | 2004 | Montréal |  |
| 14 | Gabriel Frasca | C | L | 6'1" | 190 | 2006 | Caledon |  |
| 18 | Jack Stockfish | C | R | 6'4" | 216 | 2002 | North Bay |  |
| 20 | Mason Marcellus | C | L | 5'10" | 181 | 2002 | Greely | C |
| 26 | Matthew Lansing | C | L | 6'1" | 196 | 2007 | Tonawanda, NY |  |
| 27 | Andon Cerbone | C | L | 5'11" | 181 | 2004 | Stamford, CT |  |
| 28 | Chris Pelosi | C | L | 6'2" | 203 | 2005 | Sewell, NJ | A |
| 11 | Aaron Schwartz | LW | R | 5'11" | 174 | 2004 | Parkland, FL |  |
| 19 | Ethan Wyttenbach | LW | R | 5'11" | 185 | 2007 | Roslyn, NY |  |
| 23 | Antonin Verreault | LW | L | 5'9" | 174 | 2004 | Mirabel |  |
| 24 | Dylan Edwards | LW | L | 5'8" | 174 | 2005 | Toronto |  |
| 8 | Matthew McGroarty | RW | R | 5'11" | 181 | 2003 | Westport, CT | A |
| 10 | Tyler Borgula | RW | R | 5'11" | 181 | 2005 | Livonia, MI |  |
| 17 | Alex Kostov | RW | R | 6'4" | 209 | 2006 | Mississauga |  |
| 2 | Jan Golicic | D | L | 6'5" | 205 | 2006 | Jesenice |  |
| 3 | Drew Anastasio | D | L | 6'0" | 185 | 2005 | West Simsbury, CT |  |
| 4 | Andrew Strathmann | D | L | 5'11" | 190 | 2005 | Beach Park, IL |  |
| 5 | Brady Schultz | D | L | 5'11" | 190 | 2004 | Monroe, CT |  |
| 6 | Parker Alcos | D | R | 6'4" | 190 | 2006 | Port Moody |  |
| 7 | Elliott Groenewold | D | L | 6'2" | 216 | 2006 | Springfield, VT | A |
| 15 | Graham Sward | D | L | 6'4" | 194 | 2003 | Langley |  |
| 22 | Braden Blace | D | R | 6'3" | 194 | 2003 | Duncan |  |
| 25 | Nate Tivey | D | L | 6'6" | 205 | 2004 | Burlington |  |
| 31 | Sam Scopa | G | L | 6'1" | 190 | 2005 | Lexington, MA |  |
| 33 | Rorke Applebee | G | L | 6'0" | 185 | 2003 | Châteauguay |  |
| 65 | Christian Kirsch | G | L | 6'4" | 209 | 2006 | Basel |  |

### RPI Engineers (RPI)

- School: Rensselaer Polytechnic Institute, Troy, New York
- Arena: Houston Field House
- School colours: Cherry `#DA291C`, White `#FFFFFF`, Black `#010101` (supporting), Dark Red `#9A3324` (supporting), Gray `#A2AAAD` (supporting)
- In-game colours: primary `#DA291C`, secondary `#FFFFFF`, tertiary `#010101`
- Kits: main `#DA291C`, second `#FFFFFF`; sheets `kits/rpiengineers_home.png`, `kits/rpiengineers_away.png`
- Logo: `logos/teams/rpiengineers.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 11 | Rainers Rullers | C | L | 6'4" | 194 | 2004 | Riga |  |
| 13 | Jordan Gavin | C | L | 5'11" | 187 | 2006 | Surrey |  |
| 19 | Ben Yurchuk | C | L | 5'8" | 170 | 2005 | Montréal |  |
| 21 | Matthew Buckley | C | R | 5'11" | 183 | 2005 | Oakville |  |
| 49 | Filip Sitar | C | R | 6'0" | 187 | 2005 | Ljubljana | A |
| 86 | Alfred Lindberg | C | L | 5'11" | 187 | 2002 | Hallsberg |  |
| 91 | Ian Scherzer | C | L | 5'11" | 196 | 2005 | Villach |  |
| 9 | Ethan Bono | LW | L | 6'3" | 201 | 2004 | Port McNeill | C |
| 14 | Luke Goukler | LW | L | 5'11" | 179 | 2006 | Cumming, GA |  |
| 43 | Brad McNeil | LW | L | 6'1" | 181 | 2002 | Royal Oak, MI |  |
| 72 | Jagger Tapper | LW | R | 6'0" | 185 | 2004 | Calgary |  |
| 82 | Mathieu Bourgault | LW | L | 6'2" | 176 | 2003 | St-Bruno-de-Montarville |  |
| 8 | Harrison Lodewyk | RW | L | 5'11" | 207 | 2005 | Red Deer |  |
| 10 | Caleb Hadland | RW | L | 5'10" | 190 | 2006 | Sylvan Lake |  |
| 16 | Brendan Ross | RW | R | 6'3" | 194 | 2003 | Calgary |  |
| 20 | Leo Serlin | RW | R | 6'1" | 192 | 2006 | Toronto |  |
| 3 | Logan Heroux | D | R | 6'2" | 205 | 2003 | Middletown, NJ |  |
| 4 | Guerin Slezak | D | L | 5'10" | 187 | 2006 | Arlington, TX |  |
| 5 | Kazimier Sobieski | D | R | 6'1" | 181 | 2004 | Deerfield, MA |  |
| 6 | Thomas Klassek | D | L | 6'0" | 187 | 2005 | Klagenfurt |  |
| 12 | Ryan Michael | D | L | 6'2" | 196 | 2005 | Langley |  |
| 17 | James Guo | D | R | 6'1" | 194 | 2005 | Woodbridge |  |
| 22 | Landen Hilditch | D | R | 6'4" | 201 | 2005 | Surrey |  |
| 24 | Grayson Burzynski | D | L | 6'4" | 212 | 2005 | Iowa City, IA |  |
| 46 | Simon Motew | D | R | 6'1" | 190 | 2003 | Highland Park, IL |  |
| 30 | Nathan Krawchuk | G | L | 6'3" | 190 | 2005 | Thunder Bay |  |
| 32 | Carson Dorfman | G | L | 6'0" | 179 | 2003 | Hermitage, TN |  |
| 94 | Ethan McCallum | G | L | 6'4" | 203 | 2006 | Brandon |  |

### St. Lawrence Saints (SLU)

- School: St. Lawrence University, Canton, New York
- Arena: Appleton Arena
- School colours: Scarlet `#A6192E`, Brown `#623B2A`, Beige `#D3BC8D` (supporting)
- In-game colours: primary `#A6192E`, secondary `#623B2A`, tertiary `#D3BC8D`
- Kits: main `#623B2A`, second `#A6192E`; sheets `kits/stlawrencesaints_home.png`, `kits/stlawrencesaints_away.png`
- Logo: `logos/teams/stlawrencesaints.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 13 | Cooper Pierson | C | R | 5'9" | 176 | 2005 | Zionsville, IN | A |
| 17 | Gabriel Westling | C | L | 6'4" | 205 | 2003 | Mora | A |
| 27 | Isaac Tremblay | C | L | 6'1" | 190 | 2004 | Granby |  |
| 28 | Nicholas Beneteau | C | R | 6'3" | 192 | 2003 | Windsor |  |
| 29 | Gunnar Thoreson | C | L | 5'10" | 185 | 2002 | Andover, MN | A |
| 12 | Jakub Kopecký | LW | L | 6'3" | 194 | 2004 | Grand Rapids, MI |  |
| 14 | Cayden Casey | LW | L | 6'1" | 181 | 2004 | Elk River, MN |  |
| 19 | Jake Bernadet | LW | R | 5'10" | 170 | 2003 | South Burlington, VT |  |
| 21 | Jake Rozzi | LW | L | 6'0" | 183 | 2004 | Kirkland |  |
| 24 | Luke Posthumus | LW | R | 6'0" | 190 | 2005 | Ottawa |  |
| 8 | Sam LeDrew | RW | R | 6'1" | 181 | 2005 | Stillwater Lake |  |
| 9 | Filip Juříček | RW | L | 5'10" | 170 | 2004 | Ostrava |  |
| 11 | Ihnat Pazii | RW | L | 5'10" | 174 | 2006 | Kharkiv |  |
| 18 | James Fisher | RW | R | 6'2" | 194 | 2004 | Burlington, MA |  |
| 20 | Owen Tylec | RW | L | 5'11" | 179 | 2006 | Wheatfield, NY |  |
| 2 | Will Bishop | D | R | 6'6" | 216 | 2005 | London |  |
| 3 | William-Charles Bishop | D | L | 6'1" | 205 | 2004 | Baie d'Urfée |  |
| 4 | Andrew Brown | D | L | 5'11" | 187 | 2005 | Oakville |  |
| 7 | Sam Hall | D | L | 6'2" | 185 | 2004 | Far Hills, NJ |  |
| 15 | Evan Mitchell | D | R | 5'11" | 185 | 2002 | Kanata |  |
| 16 | Sam Frandina | D | R | 5'9" | 170 | 2004 | Hanover, NH |  |
| 22 | Luke Santilli | D | R | 6'4" | 212 | 2004 | Reading, MA |  |
| 23 | Jan Olenginski | D | R | 5'9" | 170 | 2003 | Philadelphia, PA | C |
| 25 | Maxim Dirracolo | D | L | 6'4" | 220 | 2006 | Barrie |  |
| 26 | Jake Lammens | D | R | 6'1" | 185 | 2002 | Norwalk, CT |  |
| 1 | Cameron Smith | G | L | 6'3" | 181 | 2002 | Westchester, NY |  |
| 31 | Colin Winn | G | L | 6'1" | 194 | 2004 | Westford, MA |  |
| 33 | Jack Wieneke | G | L | 6'1" | 185 | 2003 | Plymouth, MN |  |

### Union Garnet Chargers (UNI)

- School: Union College, Schenectady, New York
- Arena: M&T Bank Center
- School colours: Garnet `#862633`, White `#FFFFFF`, Black `#010101` (supporting)
- In-game colours: primary `#862633`, secondary `#FFFFFF`, tertiary `#010101`
- Kits: main `#862633`, second `#FFFFFF`; sheets `kits/uniongarnetchargers_home.png`, `kits/uniongarnetchargers_away.png`
- Logo: `logos/teams/uniongarnetchargers.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 6 | Troy Pelton | C | L | 6'1" | 190 | 2004 | Clifton Park, NY |  |
| 14 | Gavin Garry | C | L | 6'2" | 201 | 2005 | Minnetonka, MN |  |
| 16 | Lucas Buzziol | C | R | 6'1" | 205 | 2004 | Mississauga | C |
| 17 | Luke Garry | C | L | 5'11" | 165 | 2005 | Minnetonka, MN |  |
| 20 | August Falloon | C | L | 6'2" | 207 | 2004 | St. Cloud, MN |  |
| 8 | Connor Smith | LW | R | 5'10" | 185 | 2003 | Hugo, MN |  |
| 11 | Tommy Cronin | LW | R | 5'11" | 181 | 2004 | Edina, MN |  |
| 12 | Elliot Gulley | LW | L | 5'11" | 185 | 2005 | Champaign, IL |  |
| 18 | Lefty Markonidis | LW | L | 6'0" | 190 | 2005 | Hudson, MA |  |
| 27 | Riley Brueck | LW | L | 6'2" | 201 | 2003 | Chesterfield, MO | A |
| 9 | Alex Laurenza | RW | L | 5'11" | 170 | 2005 | Woodbridge |  |
| 13 | Nathan Free | RW | R | 5'10" | 170 | 2006 | Calgary |  |
| 19 | Drew Sutton | RW | L | 5'9" | 165 | 2003 | Hortonville, WI |  |
| 22 | Jacob Jeannette | RW | L | 6'1" | 216 | 2002 | Duluth, MN |  |
| 33 | Luke Buss | RW | R | 5'11" | 185 | 2003 | Columbus, OH |  |
| 2 | D.J. Hart | D | R | 6'3" | 209 | 2002 | Stamford, CT |  |
| 4 | Will Felicio | D | L | 5'10" | 185 | 2006 | Holden, MA |  |
| 5 | Carter Rose | D | L | 6'2" | 194 | 2002 | Brasher Falls, NY | A |
| 7 | Jack Henry | D | R | 6'0" | 190 | 2004 | Skaneateles, NY |  |
| 15 | Olivier Chessler | D | L | 5'11" | 185 | 2004 | Watertown, MA |  |
| 21 | Tyler Dunbar | D | L | 6'1" | 203 | 2003 | Sault Ste. Marie, MI | A |
| 24 | Brady O'Malley | D | L | 6'2" | 205 | 2005 | Ann Arbor, MI |  |
| 26 | Cooper Sharp | D | L | 6'1" | 205 | 2005 | Camrose |  |
| 28 | Étienne Lessard | D | R | 5'9" | 174 | 2004 | Blainville |  |
| 29 | Brayden Gillespie | G | L | 5'11" | 174 | 2005 | Oakville |  |
| 30 | Brendan Holahan | G | L | 6'3" | 190 | 2004 | New Canaan, CT |  |
| 31 | Cameron Korpi | G | L | 6'4" | 161 | 2004 | South Lyon, MI |  |

### Yale Bulldogs (YAL)

- School: Yale University, New Haven, Connecticut
- Arena: Ingalls Rink
- School colours: Midnight Blue `#0C2340`, China White `#FFFFFF`, Yale Blue `#00346A` (supporting), Gray `#B1B3B3` (supporting)
- In-game colours: primary `#0C2340`, secondary `#FFFFFF`, tertiary `#00346A`
- Kits: main `#0C2340`, second `#FFFFFF`; sheets `kits/yalebulldogs_home.png`, `kits/yalebulldogs_away.png`
- Logo: `logos/teams/yalebulldogs.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 12 | Beanie Richter | C | L | 6'2" | 181 | 2004 | Greenwich, CT |  |
| 13 | Iisai Pesonen | C | L | 5'11" | 181 | 2004 | Kuopio |  |
| 16 | David Andreychuk | C | L | 6'1" | 205 | 2002 | Los Angeles, CA |  |
| 17 | Zachary Wagnon | C | R | 6'0" | 179 | 2004 | Red Bank, NJ |  |
| 28 | Julian Frias | C | R | 6'5" | 209 | 2004 | Hudson, MA |  |
| 29 | Braden Keeble | C | R | 5'10" | 170 | 2004 | Brandon |  |
| 7 | Drew Waterfield | LW | R | 6'2" | 205 | 2006 | Aurora, IL |  |
| 9 | Micah Berger | LW | L | 5'11" | 181 | 2004 | Bethesda, MD |  |
| 10 | James Shannon | LW | R | 5'10" | 179 | 2004 | New York, NY |  |
| 14 | Tom Molson | LW | L | 5'8" | 154 | 2005 | Montréal |  |
| 19 | Alexander Assadourian | LW | L | 5'10" | 172 | 2005 | Stouffville |  |
| 27 | Donovan Frias | LW | L | 6'4" | 201 | 2004 | Hudson, MA |  |
| 8 | Ronan O'Donnell | RW | R | 6'2" | 174 | 2004 | Fairfield, CT |  |
| 15 | Eli Friedman | RW | R | 5'10" | 163 | 2005 | Rye, NY |  |
| 18 | Seiya Tanaka-Campbell | RW | L | 5'9" | 174 | 2003 | Mill Bay |  |
| 21 | Kurt Gurkan | RW | L | 5'10" | 179 | 2004 | Darien, CT |  |
| 26 | Kyle O'Leary | RW | R | 6'0" | 165 | 2006 | Medfield, MA |  |
| 3 | Henryk Lasky | D | L | 6'4" | 205 | 2005 | London |  |
| 4 | Dylan Hunt | D | L | 6'1" | 190 | 2006 | Marlborough, MA |  |
| 5 | Joose Pesonen | D | L | 5'11" | 176 | 2005 | Kuopio |  |
| 6 | Hudson Miller | D | R | 6'4" | 205 | 2005 | New York, NY |  |
| 11 | Hughie Hooker | D | L | 5'7" | 159 | 2003 | Brandon |  |
| 20 | Owen Forester | D | R | 5'10" | 185 | 2003 | Mississauga |  |
| 22 | Rhys Bentham | D | L | 5'10" | 181 | 2002 | Calgary | C |
| 23 | Joseph Blackley | D | L | 5'11" | 185 | 2003 | Lockport, NY |  |
| 24 | Matthew Virgilio | D | R | 6'0" | 201 | 2006 | Vaughan |  |
| 1 | Jack Stark | G | L | 5'10" | 165 | 2003 | Chaska, MN |  |
| 31 | Noah Pak | G | L | 6'0" | 170 | 2003 | Oakville |  |
| 33 | Jeth Fogg | G | L | 6'2" | 179 | 2004 | Colorado Springs, CO |  |
| 35 | Derrick Huang | G | L | 5'10" | 174 | 2007 | Carlisle, MA |  |

## CCHA

Logo: `logos/leagues/ccha.png`

| Team | Abbr | City, State | Arena | Primary | Secondary | Tertiary | Kit main / second | Logo |
|---|---|---|---|---|---|---|---|---|
| Augustana Vikings | AUG | Sioux Falls, South Dakota | Midco Arena | `#041E42` | `#FFD100` | `#8E9FBC` | `#041E42` / `#FFD100` | `logos/teams/augustanavikings.png` |
| Bemidji State Beavers | BSU | Bemidji, Minnesota | Sanford Center | `#004E42` | `#FFFFFF` | `#B9975B` | `#004E42` / `#FFFFFF` | `logos/teams/bemidjistatebeavers.png` |
| Bowling Green Falcons | BGS | Bowling Green, Ohio | Slater Family Ice Arena | `#E35205` | `#4F2C1D` | `#CDA177` | `#4F2C1D` / `#E35205` | `logos/teams/bowlinggreenfalcons.png` |
| Ferris State Bulldogs | FSU | Big Rapids, Michigan | Ewigleben Arena | `#BA0C2F` | `#FFC72C` | `#FFC72C` | `#BA0C2F` / `#FFC72C` | `logos/teams/ferrisstatebulldogs.png` |
| Lake Superior State Lakers | LSS | Sault Ste. Marie, Michigan | Taffy Abel Arena | `#002F6C` | `#FFC72C` | `` | `#002F6C` / `#FFC72C` | `logos/teams/lakesuperiorstatelakers.png` |
| Michigan Tech Huskies | MTU | Houghton, Michigan | MacInnes Student Ice Arena | `#FFCD00` | `#010101` | `` | `#010101` / `#FFCD00` | `logos/teams/michigantechhuskies.png` |
| Minnesota State Mavericks | MNS | Mankato, Minnesota | Mayo Clinic Health System Event Center | `#512D6D` | `#FFD100` | `#010101` | `#512D6D` / `#FFD100` | `logos/teams/minnesotastatemavericks.png` |
| Northern Michigan Wildcats | NMU | Marquette, Michigan | Berry Events Center | `#115740` | `#FFC72C` | `#A6192E` | `#115740` / `#FFC72C` | `logos/teams/northernmichiganwildcats.png` |

### Augustana Vikings (AUG)

- School: Augustana University, Sioux Falls, South Dakota
- Arena: Midco Arena
- School colours: Blue `#041E42`, Yellow `#FFD100`, Light Blue `#8E9FBC` (supporting)
- In-game colours: primary `#041E42`, secondary `#FFD100`, tertiary `#8E9FBC`
- Kits: main `#041E42`, second `#FFD100`; sheets `kits/augustanavikings_home.png`, `kits/augustanavikings_away.png`
- Logo: `logos/teams/augustanavikings.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 13 | William Howard | C | L | 6'2" | 190 | 2002 | Grand Forks, ND |  |
| 14 | Jacob Fletcher | C | R | 5'9" | 170 | 2004 | Midland |  |
| 18 | Matvei Morshchyonok | C | R | 5'9" | 165 | 2005 | Minsk |  |
| 20 | Jacob Jastrzebski | C | L | 6'0" | 192 | 2005 | Chicago, IL |  |
| 86 | Nace Langus | C | L | 6'1" | 190 | 2004 | Jesenice |  |
| 22 | Heath Nelson | LW | L | 6'1" | 181 | 2006 | Lino Lakes, MN |  |
| 26 | Callum Gau | LW | L | 6'1" | 181 | 2002 | Camrose |  |
| 72 | Brett Meerman | LW | L | 5'9" | 170 | 2002 | Red Deer |  |
| 92 | Leonid Bulgakov | LW | L | 5'9" | 170 | 2004 | Minsk |  |
| 93 | Cole Burtch | LW | R | 5'10" | 170 | 2001 | Markham |  |
| 10 | Joey DelGreco | RW | R | 5'10" | 205 | 2003 | Grand Rapids, MN |  |
| 11 | Tyler Hennen | RW | R | 5'11" | 150 | 2004 | Hallock, MN |  |
| 12 | Hunter Bischoff | RW | R | 6'1" | 181 | 2003 | Cohasset, MN |  |
| 39 | Semyon Morshchyonok | RW | L | 5'9" | 165 | 2005 | Minsk |  |
| 81 | Owen Bohn | RW | L | 5'11" | 165 | 2002 | Morgan Hill, CA |  |
| 2 | Landon Fandel | D | L | 5'10" | 185 | 2005 | Sioux Falls, SD |  |
| 4 | Maxwell Chakrabarti | D | R | 6'3" | 181 | 2005 | Surrey |  |
| 5 | Easton Young | D | R | 5'11" | 185 | 2003 | Grand Rapids, MN |  |
| 6 | Samo Meritähti | D | L | 6'3" | 192 | 2003 | Seinäjoki |  |
| 7 | Erick Comstock | D | L | 5'6" | 165 | 2005 | Salol, MN |  |
| 9 | Hayden Hennen | D | R | 6'3" | 181 | 2002 | Hallock, MN |  |
| 23 | Carter Theissen | D | L | 5'11" | 181 | 2003 | Plymouth, MN |  |
| 48 | Ryan Whiterabbit | D | L | 6'1" | 181 | 2006 | Mahtomedi, MN |  |
| 84 | Jeff Hutchinson | D | L | 6'1" | 201 | 2002 | St. Louis, MO |  |
| 96 | Owen Baumgartner | D | L | 5'10" | 185 | 2002 | Owatonna, MN |  |
| 1 | Christian Manz | G | L | 6'1" | 181 | 2004 | Blaine, MN |  |
| 29 | Owen Nelson | G | L | 6'1" | 181 | 2007 | Hudson, WI |  |
| 31 | Mathis Langevin | G | L | 6'4" | 201 | 2006 | St-Zotique |  |
| 33 | Angelo Zol | G | L | 6'0" | 170 | 2005 | Kelowna |  |

### Bemidji State Beavers (BSU)

- School: Bemidji State University, Bemidji, Minnesota
- Arena: Sanford Center
- School colours: Green `#004E42`, White `#FFFFFF`, Brown `#B9975B` (supporting), Black `#010101` (supporting)
- In-game colours: primary `#004E42`, secondary `#FFFFFF`, tertiary `#B9975B`
- Kits: main `#004E42`, second `#FFFFFF`; sheets `kits/bemidjistatebeavers_home.png`, `kits/bemidjistatebeavers_away.png`
- Logo: `logos/teams/bemidjistatebeavers.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 9 | Jaksen Panzer | C | L | 5'10" | 170 | 2004 | Grand Forks, ND |  |
| 11 | Rylan Gould | C | L | 5'10" | 181 | 2005 | Headingley |  |
| 14 | Alexi Sylvestre | C | L | 6'2" | 198 | 2004 | Ste-Martine |  |
| 22 | Oliver Peer | C | L | 5'11" | 181 | 2003 | Burlington | A |
| 25 | Tory Lund | C | L | 5'11" | 187 | 2004 | Sartell, MN |  |
| 8 | Isaac Johnson | LW | L | 6'0" | 185 | 2005 | Fergus Falls, MN |  |
| 15 | Ethan Miedema | LW | L | 6'4" | 212 | 2005 | Fort St. John |  |
| 16 | Jiří Klíma | LW | L | 5'11" | 196 | 2006 | Suchý Dul |  |
| 18 | Max Namestnikov | LW | L | 5'8" | 183 | 2004 | Wolverine, MI |  |
| 26 | Carter Rapalje | LW | R | 5'10" | 181 | 2001 | Burlington, VT |  |
| 5 | Brady Birnie | RW | L | 5'8" | 159 | 2005 | Regina |  |
| 17 | Will Dawson | RW | L | 6'0" | 183 | 2003 | Traverse City, MI |  |
| 19 | Vann Yuhas | RW | R | 6'0" | 187 | 2004 | Medicine Hat |  |
| 27 | Jake McLean | RW | L | 6'0" | 192 | 2002 | North Vancouver |  |
| 29 | Brayden Dube | RW | R | 5'10" | 183 | 2005 | Roblin |  |
| 2 | Maxon Vig | D | L | 6'3" | 207 | 2005 | Bismarck, ND |  |
| 3 | Joe Gramer | D | L | 6'0" | 196 | 2005 | Moorhead, MN |  |
| 4 | Luca D'Amato | D | R | 5'9" | 161 | 2004 | Maple |  |
| 6 | Samuel Harris | D | L | 6'0" | 176 | 2006 | Minneapolis, MN |  |
| 7 | Eddie Revenig | D | L | 5'11" | 183 | 2005 | Edina, MN |  |
| 12 | Hudson Thornton | D | L | 5'11" | 192 | 2003 | Winnipeg | A |
| 13 | Benjamin Vigneault | D | R | 6'0" | 181 | 2004 | Boischâtel |  |
| 21 | Aiden Ziprick | D | L | 6'1" | 190 | 2005 | Russell |  |
| 23 | Luke Roelofs | D | L | 6'1" | 201 | 2003 | Minnetrista, MN | C |
| 1 | Max Hildebrand | G | L | 6'2" | 192 | 2004 | Martensville |  |
| 30 | Trent Wiemken | G | L | 5'11" | 183 | 2004 | Fargo, ND |  |
| 35 | Raythan Robbins | G | L | 5'11" | 183 | 2002 | Anchorage, AK |  |

### Bowling Green Falcons (BGS)

- School: Bowling Green State University, Bowling Green, Ohio
- Arena: Slater Family Ice Arena
- School colours: Orange `#E35205`, Brown `#4F2C1D`, Tan `#CDA177` (supporting), Black `#010101` (supporting)
- In-game colours: primary `#E35205`, secondary `#4F2C1D`, tertiary `#CDA177`
- Kits: main `#4F2C1D`, second `#E35205`; sheets `kits/bowlinggreenfalcons_home.png`, `kits/bowlinggreenfalcons_away.png`
- Logo: `logos/teams/bowlinggreenfalcons.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 8 | Jérémie Minville | C | L | 6'0" | 190 | 2004 | Drummondville |  |
| 15 | Max Martin | C | L | 6'0" | 194 | 2003 | Tawas City, MI |  |
| 16 | Jake Sloan | C | R | 6'4" | 223 | 2004 | Irvine |  |
| 20 | Connor Levis | C | R | 6'1" | 192 | 2004 | Vancouver |  |
| 26 | Teague Patton | C | R | 5'10" | 172 | 2003 | Kelowna |  |
| 11 | Noah Morneau | LW | L | 6'0" | 183 | 2004 | Riverside |  |
| 14 | Ty Cheveldayoff | LW | L | 6'3" | 225 | 2003 | Stony Plain |  |
| 18 | Thomas Verdon | LW | L | 5'11" | 183 | 2005 | Cleveland, OH |  |
| 29 | Brody Waters | LW | R | 6'3" | 207 | 2002 | Heidelberg |  |
| 77 | Dominik Rymon | LW | L | 5'10" | 161 | 2004 | Karlovy Vary |  |
| 10 | Adam Zlnka | RW | R | 6'0" | 179 | 2004 | Detva |  |
| 22 | Matthew Soto | RW | R | 6'0" | 185 | 2005 | St. Paul, MN |  |
| 23 | Ben Doran | RW | R | 5'11" | 181 | 2003 | Chesterfield, MO |  |
| 24 | Tyler Hotson | RW | L | 6'0" | 185 | 2003 | Stratford |  |
| 27 | Brandon Whynott | RW | L | 6'2" | 198 | 2004 | Langley |  |
| 2 | Carson Campbell | D | R | 6'1" | 181 | 2006 | Campbellford |  |
| 3 | Ty Higgins | D | R | 6'1" | 183 | 2004 | Stratford |  |
| 4 | Mazden Leslie | D | R | 6'1" | 190 | 2005 | Lloydminster |  |
| 5 | Peteris Bulans | D | L | 6'0" | 185 | 2005 | Riga |  |
| 6 | Cooper Wilson | D | R | 6'4" | 212 | 2005 | North Vancouver |  |
| 7 | Evan Bushy | D | L | 6'1" | 194 | 2002 | Thief River Falls, MN |  |
| 17 | Breck McKinley | D | R | 5'9" | 174 | 2003 | St. Albert |  |
| 21 | Ivan Korodiuk | D | L | 6'4" | 201 | 2004 | Kyiv |  |
| 25 | Brayden Crampton | D | R | 6'1" | 176 | 2004 | Chilliwack |  |
| 1 | Jacob Steinman | G | L | 6'1" | 183 | 2004 | Cambridge |  |
| 31 | Tyler Palmer | G | L | 6'0" | 194 | 2003 | Fernie |  |
| 33 | Cooper Michaluk | G | L | 6'2" | 179 | 2005 | Sherwood Park |  |

### Ferris State Bulldogs (FSU)

- School: Ferris State University, Big Rapids, Michigan
- Arena: Ewigleben Arena
- School colours: Crimson `#BA0C2F`, Gold `#FFC72C`, Gold `#FFC72C`, Black `#010101` (supporting)
- In-game colours: primary `#BA0C2F`, secondary `#FFC72C`, tertiary `#FFC72C`
- Kits: main `#BA0C2F`, second `#FFC72C`; sheets `kits/ferrisstatebulldogs_home.png`, `kits/ferrisstatebulldogs_away.png`
- Logo: `logos/teams/ferrisstatebulldogs.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 6 | Adyn Merrick | C | R | 6'1" | 205 | 2005 | Boston, MA |  |
| 15 | Akseli Pulkkanen | C | L | 5'11" | 181 | 2006 | Tampere |  |
| 16 | Holden Doell | C | R | 5'11" | 185 | 2003 | Martensville |  |
| 18 | Nicholas Johnson | C | L | 6'2" | 207 | 2005 | Calgary |  |
| 37 | Owen Mehlenbacher | C | L | 6'2" | 190 | 2004 | Fort Erie |  |
| 9 | Zach Bade | LW | R | 6'2" | 183 | 2003 | Rosemount, MN |  |
| 19 | Nathan Leek | LW | L | 6'2" | 216 | 2005 | Alliston |  |
| 21 | Aidan Dyer | LW | R | 5'11" | 172 | 2005 | Grand Rapids, MI |  |
| 22 | Ryan Kusler | LW | R | 6'0" | 187 | 2003 | Battle Creek, MI |  |
| 39 | Brecken Smith | LW | R | 5'9" | 183 | 2006 | Jackson, MI |  |
| 13 | Gavin Garland | RW | R | 5'10" | 190 | 2006 | Clearwater, FL |  |
| 20 | Justin Stupka | RW | R | 6'2" | 214 | 2005 | Pittsburgh, PA |  |
| 23 | Hudson Darby | RW | R | 6'2" | 183 | 2006 | Swift Current |  |
| 24 | Nathan Mackie | RW | R | 5'11" | 194 | 2004 | Prince George |  |
| 27 | Reggie Newman | RW | R | 6'2" | 207 | 2005 | Kamloops |  |
| 4 | Christopher Lie | D | L | 5'10" | 181 | 2005 | Hamar |  |
| 5 | Luke Coughlin | D | L | 5'10" | 183 | 2005 | Charlottetown |  |
| 7 | Austin Zemlak | D | L | 6'1" | 185 | 2005 | Fort McMurray |  |
| 8 | Briggs Orr | D | R | 6'2" | 201 | 2005 | Denver, CO |  |
| 10 | Jack Mesic | D | R | 5'11" | 185 | 2002 | Plymouth, MI |  |
| 11 | Brian Morse | D | R | 6'1" | 185 | 2003 | Fresno, CA |  |
| 26 | Colton Jamieson | D | L | 6'0" | 198 | 2004 | Hudson, WI |  |
| 28 | Trevor Taulien | D | R | 6'5" | 229 | 2003 | Crystal Lake, IL |  |
| 43 | Urban Podrekar | D | L | 6'0" | 205 | 2005 | Kranj |  |
| 30 | Alan Lendak | G | L | 6'1" | 174 | 2006 | Nitra |  |
| 31 | Noah Rupprecht | G | L | 5'11" | 185 | 2002 | Thief River Falls, MN |  |
| 55 | Martin Holtet Lundberg | G | L | 6'1" | 181 | 2005 | Romedal |  |

### Lake Superior State Lakers (LSS)

- School: Lake Superior State University, Sault Ste. Marie, Michigan
- Arena: Taffy Abel Arena
- School colours: Royal Blue `#002F6C`, Yellow Gold `#FFC72C`
- In-game colours: primary `#002F6C`, secondary `#FFC72C`
- Kits: main `#002F6C`, second `#FFC72C`; sheets `kits/lakesuperiorstatelakers_home.png`, `kits/lakesuperiorstatelakers_away.png`
- Logo: `logos/teams/lakesuperiorstatelakers.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 9 | Cooper Foster | C | L | 6'0" | 190 | 2005 | Sault Ste. Marie |  |
| 11 | Carter Batchelder | C | L | 5'10" | 183 | 2003 | Savage, MN |  |
| 14 | Måns Toresson | C | L | 6'1" | 187 | 2006 | Göteborg |  |
| 17 | Reese Shaw | C | R | 6'2" | 194 | 2005 | Coon Rapids, MN |  |
| 24 | William Samuelsson | C | L | 6'0" | 183 | 2005 | Södertälje |  |
| 26 | John Herrington | C | L | 5'11" | 192 | 2002 | Hudson's Hope |  |
| 63 | Markus Loponen | C | L | 6'1" | 185 | 2006 | Oulu |  |
| 71 | Raoul Boilard | C | L | 6'2" | 192 | 2006 | Sherbrooke |  |
| 97 | Matheson Mason | C | L | 6'0" | 179 | 2004 | Toronto |  |
| 4 | Charles Savoie | LW | L | 6'2" | 168 | 2003 | St-Eustache |  |
| 13 | Wilson Dahlheimer | LW | R | 5'10" | 181 | 2003 | Monticello, MN |  |
| 18 | Ethan Moore | LW | R | 6'1" | 176 | 2005 | Okotoks |  |
| 19 | Kazumo Sasaki | LW | L | 5'10" | 172 | 2005 | Nishitokyo |  |
| 91 | Pierson Sobush | LW | L | 5'9" | 170 | 2004 | Sudbury |  |
| 7 | Calem Mangone | RW | R | 5'9" | 165 | 2004 | Sault Ste. Marie |  |
| 28 | Hunter Ramos | RW | L | 5'9" | 161 | 2005 | Fort Wayne, IN |  |
| 33 | Casper Nässén | RW | R | 6'4" | 220 | 2004 | Norrtälje |  |
| 43 | Hadley Hudak | RW | R | 6'4" | 201 | 2003 | Canton, MI |  |
| 3 | Joey Potter | D | R | 5'11" | 185 | 2003 | Los Angeles, CA |  |
| 12 | Doogan Pederson | D | L | 6'3" | 176 | 2006 | Langley |  |
| 15 | Samuel Bélanger | D | L | 6'4" | 229 | 2004 | Montréal |  |
| 21 | Tyson Galloway | D | L | 6'4" | 205 | 2003 | Kamloops |  |
| 27 | Adam Belusko | D | L | 5'11" | 198 | 2006 | Kosice |  |
| 44 | Nolan Seed | D | L | 6'0" | 190 | 2002 | Newboro |  |
| 76 | Matteo Mann | D | R | 6'6" | 229 | 2004 | Sackville |  |
| 92 | Teo Besnier | D | R | 5'9" | 168 | 2005 | Briançon |  |
| 1 | Charlie Tritt | G | L | 6'0" | 170 | 2005 | Vancouver |  |
| 29 | Andrew Oke | G | L | 6'2" | 194 | 2004 | Shelby Township, MI |  |
| 31 | Anders Miller | G | L | 6'2" | 190 | 2006 | Anchorage, AK |  |

### Michigan Tech Huskies (MTU)

- School: Michigan Technological University, Houghton, Michigan
- Arena: MacInnes Student Ice Arena
- School colours: Gold `#FFCD00`, Black `#010101`
- In-game colours: primary `#FFCD00`, secondary `#010101`
- Kits: main `#010101`, second `#FFCD00`; sheets `kits/michigantechhuskies_home.png`, `kits/michigantechhuskies_away.png`
- Logo: `logos/teams/michigantechhuskies.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 2 | Tom Leppä | C | L | 6'0" | 183 | 2005 | Kauniainen | A |
| 6 | Giovanni Morneau | C | R | 5'9" | 168 | 2003 | Ottawa |  |
| 14 | Max Koskipirtti | C | L | 6'1" | 190 | 2004 | Espoo | A |
| 15 | Carson Birnie | C | L | 6'1" | 187 | 2005 | Arcola |  |
| 16 | Isaac Gordon | C | R | 6'0" | 187 | 2004 | Landmark | C |
| 20 | Tristan Delisle | C | L | 5'11" | 183 | 2007 | Cornwall |  |
| 27 | Lauri Raiman | C | L | 5'11" | 179 | 2004 | Vantaa |  |
| 10 | Elias Jansson | LW | L | 6'0" | 205 | 2005 | Oulu |  |
| 17 | Reid Daavettila | LW | R | 6'2" | 198 | 2005 | Howell, MI |  |
| 19 | Michael Cicek | LW | R | 6'1" | 190 | 2004 | Winnipeg |  |
| 22 | Matthew Van Blaricom | LW | R | 5'11" | 174 | 2006 | Southey |  |
| 29 | Conner Roulette | LW | L | 6'0" | 181 | 2003 | Winnipeg |  |
| 9 | Carson Latimer | RW | R | 6'1" | 192 | 2003 | White Rock |  |
| 11 | Teydon Trembecky | RW | L | 5'11" | 190 | 2005 | Sherwood Park |  |
| 18 | Noah Reinhart | RW | R | 6'1" | 201 | 2004 | Dundas |  |
| 28 | Owen Baker | RW | R | 5'10" | 185 | 2004 | Howell, MI |  |
| 3 | Austin Moline | D | R | 6'5" | 209 | 2005 | Las Vegas, NV |  |
| 4 | Luke Shipley | D | R | 6'0" | 187 | 2004 | Powell River |  |
| 5 | Max Matthews | D | R | 6'1" | 190 | 2004 | Arlington Heights, IL |  |
| 7 | Lucas Brenton | D | L | 6'4" | 212 | 2003 | Winnipeg | A |
| 8 | Kalem Parker | D | R | 6'0" | 187 | 2004 | Clavet |  |
| 12 | Morgan Brady | D | R | 6'2" | 207 | 2005 | Woking |  |
| 13 | Tyler Miller | D | L | 6'0" | 176 | 2004 | Medicine Hat |  |
| 21 | Reid Andresen | D | L | 5'9" | 165 | 2005 | Saskatoon |  |
| 26 | Luca Fasciano | D | R | 6'4" | 209 | 2003 | Toronto |  |
| 30 | Joseph Costanzo | G | L | 6'1" | 185 | 2005 | Toronto |  |
| 31 | Bryant Lee | G | L | 6'0" | 181 | 2006 | Madison, WI |  |
| 35 | Owen Bartoszkiewicz | G | L | 6'2" | 198 | 2003 | Northville, MI | A |
| 37 | Ryan Manzella | G | L | 6'0" | 174 | 2004 | Eagan, MN |  |

### Minnesota State Mavericks (MNS)

- School: Minnesota State University, Mankato, Mankato, Minnesota
- Arena: Mayo Clinic Health System Event Center
- School colours: Purple `#512D6D`, Gold `#FFD100`, Black `#010101` (supporting)
- In-game colours: primary `#512D6D`, secondary `#FFD100`, tertiary `#010101`
- Kits: main `#512D6D`, second `#FFD100`; sheets `kits/minnesotastatemavericks_home.png`, `kits/minnesotastatemavericks_away.png`
- Logo: `logos/teams/minnesotastatemavericks.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 12 | Alexander Zetterberg | C | R | 5'8" | 170 | 2006 | Sundsvall |  |
| 13 | Jack Christ | C | L | 5'9" | 170 | 2005 | Chaska, MN |  |
| 27 | Liam Watkins | C | L | 6'1" | 201 | 2004 | Okotoks |  |
| 44 | Reid Morich | C | R | 6'2" | 214 | 2005 | Scottsdale, AZ |  |
| 81 | Daimon Gardner | C | L | 6'4" | 205 | 2004 | Eagle Lake |  |
| 9 | Luigi Benincasa | LW | R | 5'9" | 157 | 2002 | Edmonton |  |
| 11 | Jordan Ronn | LW | L | 5'10" | 176 | 2005 | Champlin Park, MN |  |
| 15 | Sam Kappell | LW | R | 6'1" | 190 | 2007 | Appleton, WI |  |
| 19 | Mason Kraft | LW | L | 5'11" | 190 | 2007 | Moorhead, MN |  |
| 21 | Finlay Williams | LW | R | 6'1" | 185 | 2003 | North Vancouver |  |
| 14 | Mason LeBel | RW | L | 6'3" | 205 | 2004 | South Bend, IN |  |
| 17 | Ean Somoza | RW | R | 6'3" | 205 | 2003 | Thousand Oaks, CA |  |
| 20 | Cash Carter | RW | R | 5'11" | 183 | 2006 | St. Albert |  |
| 23 | Sawyer Scholl | RW | L | 6'2" | 190 | 2002 | Green Bay, WI |  |
| 4 | Jacob Conrad | D | L | 5'10" | 170 | 2002 | Green Bay, WI |  |
| 5 | Gus Thorp | D | L | 6'2" | 205 | 2007 | Chippewa Falls, WI |  |
| 7 | Bryce Strand | D | L | 5'9" | 165 | 2004 | Green Bay, WI |  |
| 8 | Will Skahan | D | L | 6'5" | 223 | 2006 | Orange, CA |  |
| 18 | Jakob Stender | D | L | 6'1" | 185 | 2002 | Alexandria, MN |  |
| 22 | Liam Hupka | D | R | 6'3" | 190 | 2005 | Minneapolis, MN |  |
| 26 | Rylan Bonkowski | D | R | 5'11" | 170 | 2006 | Richmond |  |
| 28 | Easton Hewson | D | L | 6'4" | 220 | 2005 | Edmonton |  |
| 37 | Sam Court | D | R | 5'10" | 190 | 2004 | Winnipeg |  |
| 31 | Matthew Syverson | G | L | 6'4" | 185 | 2003 | Apple Valley, MN |  |
| 34 | Raiden LeGall | G | L | 6'0" | 181 | 2006 | Morden |  |
| 35 | Lassi Lehti | G | L | 6'4" | 209 | 2001 | Espoo |  |

### Northern Michigan Wildcats (NMU)

- School: Northern Michigan University, Marquette, Michigan
- Arena: Berry Events Center
- School colours: Green `#115740`, Gold `#FFC72C`, Red `#A6192E`, Alt Gold `#F1BE48`
- In-game colours: primary `#115740`, secondary `#FFC72C`, tertiary `#A6192E`
- Kits: main `#115740`, second `#FFC72C`; sheets `kits/northernmichiganwildcats_home.png`, `kits/northernmichiganwildcats_away.png`
- Logo: `logos/teams/northernmichiganwildcats.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 7 | Branden Piku | C | R | 5'8" | 172 | 2002 | Harrison Township, MI | A |
| 9 | Coster Dunn | C | R | 6'1" | 190 | 2005 | Edmonton |  |
| 13 | Médrick Bolduc | C | R | 5'9" | 183 | 2003 | Val-d'Or | C |
| 15 | Grayden Slipec | C | L | 5'10" | 190 | 2005 | Surrey |  |
| 20 | Niklas Gudmundson | C | L | 5'10" | 187 | 2005 | Carnation, WA |  |
| 14 | Reilley Kotai | LW | R | 6'3" | 196 | 2004 | Abbotsford |  |
| 18 | Michael Burchill | LW | R | 5'10" | 163 | 2005 | Mundelein, IL |  |
| 26 | Merril Steenari | LW | R | 5'10" | 181 | 2004 | Aliso Viejo, CA |  |
| 28 | Owen Pitters | LW | R | 6'1" | 198 | 2005 | Chesterfield, MI |  |
| 29 | Adam Csabi | LW | L | 5'11" | 163 | 2005 | Karvina |  |
| 10 | Mathew Ward | RW | L | 5'9" | 168 | 2004 | Kamloops |  |
| 11 | Brendan Lamb | RW | R | 5'11" | 176 | 2004 | Battle Creek, MI |  |
| 12 | Peter Cisar | RW | L | 6'0" | 176 | 2005 | Bratislava |  |
| 17 | Girts Silkalns | RW | R | 6'2" | 198 | 2003 | Talsi |  |
| 23 | Aapo Katavisto | RW | R | 6'0" | 183 | 2007 | Espoo |  |
| 2 | Joe Schiller | D | L | 6'0" | 194 | 2004 | Moorhead, MN | A |
| 3 | Noah Houle | D | R | 6'1" | 192 | 2003 | Montréal |  |
| 4 | Warren Clark | D | R | 6'3" | 194 | 2004 | Riverside |  |
| 5 | Grayden Daul | D | R | 6'0" | 181 | 2003 | Chicago, IL |  |
| 6 | Jason Davenport | D | R | 6'2" | 190 | 2006 | Shelby Township, MI |  |
| 8 | Olivier Beaulieu | D | L | 5'11" | 176 | 2005 | Terrebonne |  |
| 19 | Ian Engel | D | L | 6'0" | 185 | 2004 | Hortonville, WI |  |
| 22 | Darik Olson | D | L | 6'0" | 174 | 2006 | Duluth, MN |  |
| 24 | Evan Dépatie | D | R | 5'9" | 176 | 2006 | Granby |  |
| 30 | Oliver Auyeung-Ashton | G | L | 5'10" | 172 | 2005 | Coquitlam |  |
| 31 | Jackson Fuller | G | L | 6'0" | 170 | 2004 | Centerville, MN |  |
| 32 | Max Weilandt | G | L | 6'2" | 196 | 2005 | Northbrook, IL |  |

## Atlantic Hockey America

Logo: `logos/leagues/aha.png`

| Team | Abbr | City, State | Arena | Primary | Secondary | Tertiary | Kit main / second | Logo |
|---|---|---|---|---|---|---|---|---|
| Air Force Falcons | AFA | USAF Academy, Colorado | Cadet Ice Arena | `#003594` | `#FFFFFF` | `#B2B4B2` | `#003594` / `#FFFFFF` | `logos/teams/airforcefalcons.png` |
| Army Black Knights | ARM | West Point, New York | Tate Rink | `#010101` | `#D3BC8D` | `#B1B3B3` | `#010101` / `#D3BC8D` | `logos/teams/armyblackknights.png` |
| Bentley Falcons | BEN | Waltham, Massachusetts | Bentley Arena | `#010101` | `#0057B7` | `#707372` | `#010101` / `#0057B7` | `logos/teams/bentleyfalcons.png` |
| Canisius Golden Griffins | CAN | Buffalo, New York | LECOM Harborcenter | `#0C2340` | `#EAAA00` | `` | `#0C2340` / `#EAAA00` | `logos/teams/canisiusgoldengriffins.png` |
| Holy Cross Crusaders | HC | Worcester, Massachusetts | Hart Center | `#582C83` | `#FFFFFF` | `#010101` | `#582C83` / `#FFFFFF` | `logos/teams/holycrosscrusaders.png` |
| Niagara Purple Eagles | NIA | Lewiston, New York | Dwyer Arena | `#582C83` | `#FFFFFF` | `#010101` | `#582C83` / `#FFFFFF` | `logos/teams/niagarapurpleeagles.png` |
| RIT Tigers | RIT | Rochester, New York | Gene Polisseni Center | `#4E3629` | `#FF6720` | `#010101` | `#4E3629` / `#FF6720` | `logos/teams/rittigers.png` |
| Robert Morris Colonials | RMU | Moon Township, Pennsylvania | RMU Island Sports Center | `#041E42` | `#C8102E` | `#B2B4B2` | `#041E42` / `#C8102E` | `logos/teams/robertmorriscolonials.png` |
| Sacred Heart Pioneers | SHU | Fairfield, Connecticut | Martire Family Arena | `#BA0C2F` | `#FFFFFF` | `#010101` | `#BA0C2F` / `#FFFFFF` | `logos/teams/sacredheartpioneers.png` |

### Air Force Falcons (AFA)

- School: United States Air Force Academy, USAF Academy, Colorado
- Arena: Cadet Ice Arena
- School colours: Academy Blue `#003594`, White `#FFFFFF`, Academy Silver `#B2B4B2`, Academy Dark Blue `#002554` (supporting)
- In-game colours: primary `#003594`, secondary `#FFFFFF`, tertiary `#B2B4B2`
- Kits: main `#003594`, second `#FFFFFF`; sheets `kits/airforcefalcons_home.png`, `kits/airforcefalcons_away.png`
- Logo: `logos/teams/airforcefalcons.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 9 | Anthony Yu | C | R | 5'9" | 181 | 2004 | Baldwin Park, CA |  |
| 10 | Oli Genest | C | R | 6'1" | 194 | 2004 | Sanford, ME |  |
| 16 | Zander Lipsett | C | L | 6'4" | 187 | 2005 | Lexington, KY |  |
| 17 | Harper Searles | C | L | 5'9" | 176 | 2005 | Blaine, MN |  |
| 23 | Nick Sajevic | C | R | 5'11" | 174 | 2003 | Shoreview, MN |  |
| 64 | Brendan Gibbons | C | R | 6'2" | 205 | 2002 | South Kingstown, RI |  |
| 11 | Brady Arneson | LW | L | 5'10" | 183 | 2005 | Red Wing, MN |  |
| 13 | Duke Gentzler | LW | L | 6'1" | 185 | 2005 | Manhattan Beach, CA |  |
| 21 | Hayden Nichol | LW | R | 6'1" | 190 | 2003 | Rochester, NY |  |
| 27 | Samuel Stitz | LW | R | 6'0" | 185 | 2003 | Perry Hall, MD |  |
| 92 | Grant Denuccio | LW | L | 5'9" | 165 | 2005 | Leesburg, VA |  |
| 8 | Cole Christian | RW | L | 5'8" | 154 | 2004 | Duluth, MN |  |
| 15 | Owen DuBois | RW | L | 6'4" | 201 | 2002 | Madison, WI |  |
| 18 | Christian Kim | RW | R | 5'7" | 154 | 2005 | Torrance, CA |  |
| 72 | Aidan Willis | RW | L | 5'11" | 185 | 2005 | Farmington, MN |  |
| 81 | Charles Panchisin | RW | R | 5'10" | 174 | 2004 | IL |  |
| 2 | Beau Janzig | D | R | 6'2" | 201 | 2003 | Hermantown, MN |  |
| 3 | Ren Morque | D | L | 6'1" | 190 | 2003 | Grand Rapids, MN |  |
| 4 | Francois Devilliers | D | L | 6'0" | 181 | 2005 | Irvine, CA |  |
| 6 | Simon Houge | D | L | 5'7" | 172 | 2004 | Woodbury, MN |  |
| 7 | Jake Peterson | D | R | 6'2" | 190 | 2003 | Rosemount, MN |  |
| 14 | Gavin Reed | D | L | 6'4" | 218 | 2005 | Dayton, MN |  |
| 19 | Calvin Beard | D | R | 6'2" | 179 | 2005 | Southborough, MA |  |
| 20 | Nolan Cunningham | D | R | 6'2" | 185 | 2001 | Great Falls, MT |  |
| 22 | Garin Ludwig | D | L | 5'9" | 172 | 2006 | Eagle River, AK |  |
| 25 | Carson Pavlowich | D | R | 6'3" | 196 | 2006 | Proctor, MN |  |
| 44 | Will Staring | D | L | 6'2" | 194 | 2003 | Springfield, VA |  |
| 30 | Dominik Wasik | G | L | 6'1" | 165 | 2002 | Superior, WI |  |
| 31 | Jack Solomon | G | L | 6'2" | 185 | 2005 | St. Louis, MO |  |
| 35 | Carter Clafton | G | L | 6'2" | 174 | 2002 | Grand Rapids, MN |  |
| 39 | Dylan Krick | G | L | 6'2" | 168 | 2005 | West Chester, PA |  |

### Army Black Knights (ARM)

- School: United States Military Academy, West Point, New York
- Arena: Tate Rink
- School colours: Black `#010101`, Gold `#D3BC8D`, Gray `#B1B3B3`
- In-game colours: primary `#010101`, secondary `#D3BC8D`, tertiary `#B1B3B3`
- Kits: main `#010101`, second `#D3BC8D`; sheets `kits/armyblackknights_home.png`, `kits/armyblackknights_away.png`
- Logo: `logos/teams/armyblackknights.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 4 | Benjamin Ivey | C | L | 6'4" | 220 | 2004 | San Diego, CA |  |
| 8 | Vincent Salice | C | L | 5'7" | 174 | 2003 | Commerce Township, MI |  |
| 11 | Brock Jones | C | R | 5'11" | 183 | 2004 | Lake View, NY |  |
| 14 | Thomas Neu | C | R | 5'10" | 181 | 2005 | Hartland, MI |  |
| 25 | Barron Woodring | C | R | 6'2" | 201 | 2002 | Chicago, IL | C |
| 28 | Brent Keefer | C | R | 6'0" | 170 | 2001 | Colorado Springs, CO |  |
| 5 | Jacob Ivey | LW | R | 6'3" | 192 | 2004 | San Diego, CA |  |
| 12 | Rhys Wallin | LW | L | 6'3" | 179 | 2005 | Wayzata, MN |  |
| 13 | Jacob Ligi | LW | R | 5'10" | 176 | 2004 | South Lyon, MI |  |
| 15 | Sean Gibbons | LW | R | 5'8" | 165 | 2005 | Southport, CT |  |
| 24 | Billy Batten | LW | L | 5'9" | 174 | 2004 | Phoenixville, PA |  |
| 29 | Adam Marshall | LW | L | 5'10" | 181 | 2003 | Wayzata, MN |  |
| 9 | Nils Forselius | RW | L | 5'9" | 181 | 2003 | Guilford, CT |  |
| 10 | Dylan Wegner | RW | R | 6'2" | 185 | 2004 | Nashville, TN |  |
| 19 | Nik Hong | RW | R | 6'0" | 185 | 2002 | Minneapolis, MN | A |
| 22 | Kellen Murphy | RW | L | 5'8" | 165 | 2004 | Roseau, MN |  |
| 26 | Connor Jalbert | RW | L | 5'11" | 176 | 2005 | Marblehead, MA |  |
| 27 | Cade De St. Hubert | RW | R | 6'0" | 181 | 2006 | Hopkins, MN |  |
| 2 | Lukas McCloskey | D | R | 6'3" | 220 | 2005 | Newport Beach, CA |  |
| 3 | Easton Zueger | D | R | 5'8" | 190 | 2003 | Sioux Falls, SD | A |
| 6 | Pierce Patterson | D | R | 5'9" | 165 | 2002 | Valrico, FL |  |
| 7 | Liam Chapman | D | L | 6'1" | 187 | 2004 | Weare, NH |  |
| 16 | Cam Charron | D | L | 5'10" | 185 | 2005 | Falmouth, ME |  |
| 17 | Thomas Quast | D | L | 6'3" | 185 | 2005 | Forest Lake, MN |  |
| 18 | Jon Bell | D | L | 5'10" | 201 | 2002 | St. Cloud, MN |  |
| 20 | Max Holland | D | R | 5'11" | 172 | 2006 | San Francisco, CA |  |
| 23 | Alex Bosland | D | R | 6'0" | 185 | 2005 | Basking Ridge, NJ |  |
| 34 | Noah Mertz | D | R | 5'11" | 161 | 2005 | Coventry, RI |  |
| 1 | Jacob Biron | G | L | 6'4" | 190 | 2004 | Buffalo, NY |  |
| 30 | J.J. Cataldo | G | L | 6'0" | 183 | 2003 | Stuart, FL |  |
| 31 | Owen Crudale | G | L | 6'1" | 187 | 2005 | Poway, CA |  |
| 33 | Ansel Holt | G | L | 6'3" | 190 | 2003 | Bowling Green, OH |  |

### Bentley Falcons (BEN)

- School: Bentley University, Waltham, Massachusetts
- Arena: Bentley Arena
- School colours: Black `#010101`, Blue `#0057B7`, Gray `#707372` (supporting)
- In-game colours: primary `#010101`, secondary `#0057B7`, tertiary `#707372`
- Kits: main `#010101`, second `#0057B7`; sheets `kits/bentleyfalcons_home.png`, `kits/bentleyfalcons_away.png`
- Logo: `logos/teams/bentleyfalcons.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 8 | Oliver Salo | C | R | 6'1" | 190 | 2003 | Kaarina |  |
| 16 | Kellan Hjartarson | C | L | 5'11" | 185 | 2003 | Calgary |  |
| 17 | Jacob Sagadin | C | L | 6'0" | 192 | 2005 | Ljungby |  |
| 19 | Michael Mesic | C | L | 5'11" | 179 | 2004 | Fort Myers, FL |  |
| 20 | Jan Gaspar | C | L | 5'10" | 174 | 2004 |  |  |
| 26 | Kolby Amici | C | R | 5'9" | 170 | 2002 | Orchard Park, NY |  |
| 5 | Dylan Cook | LW | L | 6'0" | 198 | 2003 | Princeton, MN |  |
| 9 | Saxton Tess | LW | L | 6'1" | 190 | 2005 | Chicago, IL |  |
| 11 | Ryan Mansfield | LW | L | 6'0" | 194 | 2002 | Burlington |  |
| 13 | Peter Kramer | LW | L | 5'11" | 183 | 2002 | Bridgewater, MA |  |
| 18 | Joshua Giuliani | LW | R | 6'1" | 176 | 2004 | Plymouth, MN |  |
| 22 | Chase Davis | LW | R | 6'5" | 220 | 2002 | Alpharetta, GA |  |
| 10 | Jimmy Doyle | RW | R | 6'2" | 190 | 2002 | Plainfield, IL |  |
| 12 | Owen Goodbrand | RW | L | 6'2" | 181 | 2004 | Waterdown |  |
| 14 | James Duerr | RW | R | 6'3" | 216 | 2004 | Chicago, IL |  |
| 15 | Oskar Kind Bakkevig | RW | L | 5'11" | 183 | 2004 | Oslo |  |
| 23 | Márton Nemes | RW | L | 6'2" | 203 | 2005 | Székesfehérvár |  |
| 2 | Charlie Emmons | D | L | 6'0" | 174 | 2005 | Washington Township, MI |  |
| 3 | Josh Player | D | R | 6'0" | 190 | 2005 | Thorofare, NJ |  |
| 4 | Tobias Bjercke Larsen | D | L | 6'2" | 207 | 2004 | Oslo |  |
| 7 | Alec Leonard | D | R | 5'11" | 185 | 2004 | Toronto |  |
| 21 | John Dalton | D | R | 6'0" | 185 | 2003 | River Forest, IL |  |
| 24 | Colton Cameron | D | L | 5'11" | 190 | 2002 | Surrey |  |
| 25 | Garrett Horsager | D | L | 6'0" | 194 | 2002 | Rosemount, MN |  |
| 28 | Jack Farrell | D | L | 6'0" | 192 | 2004 | Wilmington, MA |  |
| 55 | Ephram McNutt | D | R | 6'0" | 181 | 2005 | Yellowknife |  |
| 29 | Nick Bevilacqua | G | L | 6'0" | 190 | 2003 | Abington, MA |  |
| 30 | Easton Rye | G | L | 6'0" | 196 | 2006 | Peterborough |  |
| 60 | Lukas Swedin | G | L | 5'10" | 161 | 2004 | Stockholm |  |

### Canisius Golden Griffins (CAN)

- School: Canisius University, Buffalo, New York
- Arena: LECOM Harborcenter
- School colours: Blue `#0C2340`, Gold `#EAAA00`
- In-game colours: primary `#0C2340`, secondary `#EAAA00`
- Kits: main `#0C2340`, second `#EAAA00`; sheets `kits/canisiusgoldengriffins_home.png`, `kits/canisiusgoldengriffins_away.png`
- Logo: `logos/teams/canisiusgoldengriffins.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 9 | Walter Zacher | C | R | 5'11" | 190 | 2002 | Buffalo, NY |  |
| 55 | Louis-Philippe Fontaine | C | L | 5'9" | 203 | 2004 | Chambly |  |
| 61 | Hunter Price | C | R | 6'1" | 209 | 2003 | Calgary |  |
| 72 | Kyan Haldenby | C | R | 6'2" | 194 | 2004 | Toronto |  |
| 77 | Joseph Henneberry | C | L | 6'2" | 192 | 2004 | Halifax |  |
| 94 | David Elmy | C | R | 6'0" | 185 | 2005 | Mississauga |  |
| 4 | Brice Cooke | LW | L | 6'3" | 190 | 2004 | Orangeville |  |
| 8 | Rhys Chiddenton | LW | L | 6'0" | 185 | 2003 | Campbellville |  |
| 20 | Zach Homer | LW | R | 6'2" | 192 | 2005 | Detroit, MI |  |
| 22 | Nico Grabas | LW | L | 5'10" | 161 | 2005 | Burnaby |  |
| 88 | Noah Quinn | LW | R | 6'0" | 181 | 2003 | Nelson |  |
| 92 | Benjamin Brunelle | LW | L | 6'4" | 194 | 2005 | Trois-Rivières |  |
| 5 | Ryan Franks | RW | L | 6'4" | 205 | 2005 | Roslyn, NY |  |
| 14 | Trey Funk | RW | R | 6'2" | 181 | 2002 | Redwood Meadows |  |
| 15 | Cole Miller | RW | R | 6'4" | 190 | 2005 | Edmonton |  |
| 25 | Cole Gordon | RW | L | 6'2" | 198 | 2002 | Sheffield |  |
| 27 | Carter Anderson | RW | L | 5'11" | 185 | 2004 | Thompson |  |
| 91 | Ethan Wolthers | RW | R | 5'9" | 181 | 2001 | Valencia, CA |  |
| 12 | Matthew Campbell | D | L | 6'1" | 170 | 2003 | North Vancouver |  |
| 13 | Jason Gallucci | D | L | 5'11" | 174 | 2004 | Robbinsville, NJ |  |
| 23 | Joseph Messina | D | L | 5'9" | 185 | 2002 | Woodbridge |  |
| 26 | Trent Ballentyne | D | R | 6'3" | 205 | 2004 | London |  |
| 44 | Luke Marshall | D | L | 6'1" | 181 | 2004 | Saskatoon |  |
| 47 | Reid Conn | D | R | 6'4" | 209 | 2004 | Centennial, CO |  |
| 53 | James Goffredo | D | R | 5'11" | 185 | 2003 | Mount Laurel, NJ |  |
| 57 | Carter Patterson | D | R | 6'4" | 216 | 2003 | Burlington |  |
| 73 | François-James Buteau | D | L | 6'4" | 209 | 2004 | Montréal |  |
| 1 | Kolby Hay | G | L | 6'5" | 185 | 2004 | Kamloops |  |
| 30 | Ethan Robertson | G | L | 6'1" | 181 | 2002 | Courtice |  |
| 33 | Jack Lisson | G | R | 6'2" | 190 | 2006 | Oakville |  |

### Holy Cross Crusaders (HC)

- School: College of the Holy Cross, Worcester, Massachusetts
- Arena: Hart Center
- School colours: Royal Purple `#582C83`, White `#FFFFFF`, Black `#010101` (supporting), Cool Gray `#B1B3B3` (supporting)
- In-game colours: primary `#582C83`, secondary `#FFFFFF`, tertiary `#010101`
- Kits: main `#582C83`, second `#FFFFFF`; sheets `kits/holycrosscrusaders_home.png`, `kits/holycrosscrusaders_away.png`
- Logo: `logos/teams/holycrosscrusaders.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 2 | A.J. Lacroix | C | L | 6'0" | 194 | 2005 | Livingston, NJ |  |
| 10 | David Hymovitch | C | R | 5'11" | 176 | 2003 | Phoenix, AZ |  |
| 12 | Matt Kursonis | C | L | 6'1" | 187 | 2004 | Worcester, MA | A |
| 17 | Bode Sampair | C | R | 5'10" | 181 | 2006 | Mahtomedi, MN |  |
| 19 | Ben Riche | C | L | 5'11" | 196 | 2005 | Bethune |  |
| 27 | Ryan Flaherty | C | R | 6'2" | 174 | 2005 | Milton, MA |  |
| 7 | Anthony Carone | LW | R | 6'1" | 183 | 2003 | Pittsburgh, PA |  |
| 15 | Jarrod Smith | LW | R | 5'10" | 165 | 2004 | West Vancouver |  |
| 20 | Preston Lounsbury | LW | L | 6'1" | 181 | 2005 | Salisbury |  |
| 21 | Owen Kim | LW | R | 5'8" | 161 | 2004 | North Vancouver |  |
| 34 | Cooper Conway | LW | L | 6'0" | 163 | 2005 | Andover, MN |  |
| 38 | Noah Eyre | LW | R | 5'11" | 176 | 2004 | Vienna, VA |  |
| 6 | Christian Maro | RW | L | 6'0" | 187 | 2005 | Stamford, CT |  |
| 11 | Drew DellaSalla | RW | L | 6'0" | 183 | 2006 | New York, NY |  |
| 18 | Ty Gagno | RW | R | 6'2" | 185 | 2003 | Langley | A |
| 26 | Will Reardon | RW | R | 6'7" | 218 | 2004 | Philadelphia, PA |  |
| 28 | Jack Larrigan | RW | R | 5'10" | 179 | 2004 | St. Louis, MO |  |
| 29 | Connor Welsh | RW | R | 5'11" | 174 | 2004 | Greenwich, CT |  |
| 3 | Brody Gagno | D | R | 6'1" | 190 | 2002 | South Surrey | C |
| 4 | Anthony Biakabutuka | D | L | 6'3" | 209 | 2006 | Longueuil |  |
| 5 | Will Troutwine | D | L | 6'3" | 216 | 2002 | Eveleth, MN |  |
| 8 | Ryan Buckley | D | R | 6'1" | 179 | 2004 | Warwick, RI |  |
| 13 | William Elias | D | R | 6'0" | 179 | 2003 | Weston, MA | A |
| 14 | Owain Johnston | D | R | 6'1" | 194 | 2003 | Toronto |  |
| 22 | Jack Martin | D | L | 6'1" | 190 | 2005 | Upper Coverdale |  |
| 23 | Daniel D'Alessandro | D | R | 5'10" | 181 | 2006 | Essex Fells, NJ |  |
| 37 | Quinn McCall | D | R | 5'11" | 181 | 2005 | St. John, IN |  |
| 30 | Danick Leroux | G | L | 6'2" | 201 | 2005 | Candiac |  |
| 32 | Connor Towle | G | L | 6'2" | 176 | 2006 | Fairfield, CT |  |
| 33 | Connor Mackenzie | G | L | 6'0" | 179 | 2002 | Toronto |  |

### Niagara Purple Eagles (NIA)

- School: Niagara University, Lewiston, New York
- Arena: Dwyer Arena
- School colours: Purple `#582C83`, White `#FFFFFF`, Black `#010101` (supporting), Gray `#B3B3B3` (supporting)
- In-game colours: primary `#582C83`, secondary `#FFFFFF`, tertiary `#010101`
- Kits: main `#582C83`, second `#FFFFFF`; sheets `kits/niagarapurpleeagles_home.png`, `kits/niagarapurpleeagles_away.png`
- Logo: `logos/teams/niagarapurpleeagles.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 2 | Alix Durocher | C | L | 5'10" | 172 | 2006 | Drummondville |  |
| 5 | Camden Prier | C | R | 6'0" | 183 | 2006 | North Palm Beach, FL |  |
| 18 | Spencer Young | C | R | 6'0" | 174 | 2003 | Elmira | A |
| 20 | Jack Rosensteel | C | L | 6'0" | 179 | 2006 | Greensburg, PA |  |
| 23 | Brayden Boehm | C | L | 6'0" | 187 | 2004 | Nanaimo |  |
| 26 | Jacob Maillet | C | R | 6'1" | 205 | 2003 | Dundas |  |
| 10 | Hunter Wallace | LW | L | 5'8" | 165 | 2003 | Oak Lake |  |
| 16 | Andy Reist | LW | L | 6'0" | 174 | 2004 | Waterloo |  |
| 17 | Dante Josefsson Westling | LW | L | 6'2" | 187 | 2006 | Danderyd |  |
| 21 | Dallon Melin | LW | L | 6'4" | 220 | 2002 | Camrose |  |
| 24 | Ryan Humphrey | LW | L | 5'11" | 165 | 2003 | Northville, MI |  |
| 34 | Noah Hackett | LW | R | 6'1" | 174 | 2001 | Ponoka | C |
| 11 | Adam Gionta | RW | L | 5'8" | 170 | 2005 | Clarence Center, NY |  |
| 13 | Jack Richard | RW | L | 6'4" | 181 | 2002 | Stoney Creek |  |
| 14 | Marshall Finnie | RW | R | 6'2" | 194 | 2004 | Lethbridge |  |
| 15 | Maxim Muranov | RW | L | 6'2" | 185 | 2004 | Moskva |  |
| 28 | Davis Codd | RW | R | 5'10" | 174 | 2003 | Brighton, MI |  |
| 4 | Théo Lemieux | D | L | 5'10" | 176 | 2005 | Ste-Claire |  |
| 7 | James Richards | D | L | 6'2" | 172 | 2005 | Port Williams |  |
| 8 | Cameron Eke | D | L | 6'2" | 198 | 2005 | St. Catharines |  |
| 9 | Nathan Oickle | D | R | 6'0" | 185 | 2003 | Peterborough |  |
| 12 | Ross Roloson | D | R | 5'11" | 174 | 2003 | Woodbury, MN | A |
| 19 | Roberto Mancini | D | L | 6'4" | 214 | 2003 | Vaughan |  |
| 22 | Josh Van Mulligen | D | R | 6'3" | 209 | 2005 | Medicine Hat |  |
| 25 | Mitch Young | D | L | 6'2" | 190 | 2005 | Elmira |  |
| 27 | Ray Murakami | D | L | 6'0" | 176 | 2003 | Tomakomai |  |
| 1 | Deivs Rolovs | G | L | 6'2" | 183 | 2004 | Riga |  |
| 29 | Charles-Edward Gravel | G | R | 6'2" | 194 | 2004 | Lévis |  |
| 31 | Tomas Anderson | G | L | 6'3" | 201 | 2004 | Stillwater, MN |  |

### RIT Tigers (RIT)

- School: Rochester Institute of Technology, Rochester, New York
- Arena: Gene Polisseni Center
- School colours: Burnt Umber `#4E3629`, Orange `#FF6720`, Black `#010101` (supporting), Gray `#BFB8AF` (supporting)
- In-game colours: primary `#4E3629`, secondary `#FF6720`, tertiary `#010101`
- Kits: main `#4E3629`, second `#FF6720`; sheets `kits/rittigers_home.png`, `kits/rittigers_away.png`
- Logo: `logos/teams/rittigers.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 8 | Zach Wigle | C | L | 6'0" | 194 | 2004 | Oakville |  |
| 12 | Nathan Delladonna | C | R | 5'10" | 176 | 2006 | Brewerton, NY |  |
| 15 | Jack McAra | C | L | 6'1" | 176 | 2005 | Burlington |  |
| 22 | Will Moore | C | L | 5'11" | 185 | 2004 | Westfield, IN |  |
| 28 | Julian Demiglio | C | L | 5'11" | 181 | 2006 | Concord |  |
| 6 | Matthew Wilde | LW | L | 6'3" | 185 | 2002 | Mississauga | A |
| 9 | Nick Peluso | LW | R | 5'8" | 174 | 2004 | Rochester, NY |  |
| 10 | Matthew Wang | LW | L | 6'2" | 201 | 2005 | London |  |
| 14 | Ryan Shaw | LW | L | 5'10" | 170 | 2005 | East Longmeadow, MA |  |
| 19 | Camden Bajzer | LW | R | 5'11" | 183 | 2004 | Cleveland, OH |  |
| 11 | Caleb Elfering | RW | R | 5'9" | 170 | 2004 | Richland, WA |  |
| 16 | Lucas Romeo | RW | R | 6'3" | 205 | 2005 | Oakville |  |
| 23 | Christian Catalano | RW | R | 6'1" | 190 | 2003 | Mississauga | A |
| 25 | Alex Christopoulos | RW | R | 6'0" | 196 | 2003 | Richmond Hill |  |
| 26 | Evan Konyen | RW | R | 6'0" | 181 | 2004 | Newmarket |  |
| 2 | Brock Reinhart | D | R | 6'3" | 201 | 2004 | Elora |  |
| 3 | Ryan Williams | D | L | 6'4" | 205 | 2003 | Pleasantville, OH |  |
| 4 | Braedyn Rogers | D | R | 6'1" | 185 | 2006 | Belleville |  |
| 7 | Nick De Angelis | D | L | 6'2" | 194 | 2004 | King City |  |
| 17 | Tristan Allen | D | R | 6'0" | 183 | 2003 | Kamloops | A |
| 18 | Kevin Scott | D | R | 6'3" | 216 | 2003 | Ashburn, VA | C |
| 20 | Ben Roger | D | R | 6'5" | 205 | 2002 | Markham | A |
| 21 | Matthew Mania | D | R | 6'1" | 190 | 2005 | Brandon, FL |  |
| 24 | Mason Croucher | D | L | 6'2" | 185 | 2004 | Victoria |  |
| 1 | Jakub Krbecek | G | L | 6'2" | 198 | 2003 | Praha |  |
| 31 | Zachary Bowen | G | L | 6'2" | 190 | 2005 | Ottawa |  |
| 33 | Collin MacKenzie | G | L | 6'0" | 181 | 2004 | Elora |  |

### Robert Morris Colonials (RMU)

- School: Robert Morris University, Moon Township, Pennsylvania
- Arena: RMU Island Sports Center
- School colours: Blue `#041E42`, Red `#C8102E`, Gray `#B2B4B2` (supporting), Light Gray `#D9D9D6`
- In-game colours: primary `#041E42`, secondary `#C8102E`, tertiary `#B2B4B2`
- Kits: main `#041E42`, second `#C8102E`; sheets `kits/robertmorriscolonials_home.png`, `kits/robertmorriscolonials_away.png`
- Logo: `logos/teams/robertmorriscolonials.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 11 | Dario Beljo | C | L | 6'3" | 190 | 2002 | Sudbury |  |
| 18 | Bruce McDonald | C | L | 6'0" | 176 | 2004 | Ottawa |  |
| 19 | Ryan MacPherson | C | L | 6'1" | 185 | 2005 | Windsor |  |
| 20 | McKay Hayes | C | L | 6'3" | 212 | 2002 | Ancaster |  |
| 28 | Brennan Faulkner | C | L | 6'0" | 176 | 2005 | London |  |
| 44 | Luca Primerano | C | L | 6'2" | 190 | 2005 | North Vancouver |  |
| 14 | Julian Beaumont | LW | L | 5'8" | 161 | 2004 | Allen, TX |  |
| 16 | George Krotiris | LW | L | 5'11" | 176 | 2002 | Markham |  |
| 21 | Ben Pickell | LW | L | 5'9" | 187 | 2005 | Courtice |  |
| 25 | Stanley Hubbard | LW | L | 6'7" | 225 | 2005 | Albuquerque, NM |  |
| 27 | Trent Wilson | LW | L | 6'1" | 187 | 2004 | Tsawwassen |  |
| 29 | Michael Felsing | LW | R | 5'11" | 181 | 2003 | Pittsburgh, PA |  |
| 12 | Jackson Reineke | RW | R | 5'10" | 181 | 2002 | Faribault, MN | A |
| 15 | Connor Gourley | RW | L | 6'2" | 170 | 2004 | Calgary |  |
| 17 | Patrick Johnson | RW | L | 5'8" | 187 | 2003 | Chesterfield, MO | C |
| 24 | Calvin Crombie | RW | R | 6'0" | 187 | 2005 | Hamilton |  |
| 26 | Braden Rourke | RW | R | 6'0" | 172 | 2004 | Des Moines, IA |  |
| 2 | Gavin Ewles | D | R | 6'1" | 190 | 2005 | Barrie |  |
| 3 | Dominic Elliott | D | R | 5'10" | 176 | 2004 | Allendale, MI | A |
| 5 | Luke van Why | D | R | 5'10" | 172 | 2003 | Hatfield, PA |  |
| 6 | Charlie Kinsman | D | R | 5'11" | 196 | 2005 | Orchard Park, NY |  |
| 7 | John Babcock | D | L | 6'2" | 205 | 2004 | North Vancouver | A |
| 8 | Gabriel Lunn | D | L | 6'0" | 190 | 2003 | Lockport, NY |  |
| 9 | Max Wattvil | D | R | 6'2" | 192 | 2002 | Stockholm |  |
| 10 | J.R. Ashmead | D | L | 6'2" | 192 | 2003 | Calgary |  |
| 22 | Gunnar VanDamme | D | L | 6'0" | 190 | 2002 | Pittsford, NY |  |
| 1 | Croix Kochendorfer | G | L | 6'4" | 209 | 2003 | St. Paul, MN |  |
| 30 | Lucas Massie | G | L | 6'1" | 190 | 2003 | Claremont, CA |  |
| 31 | Charlie Schenkel | G | R | 6'5" | 205 | 2004 | Toronto |  |

### Sacred Heart Pioneers (SHU)

- School: Sacred Heart University, Fairfield, Connecticut
- Arena: Martire Family Arena
- School colours: Scarlet `#BA0C2F`, White `#FFFFFF`, Black `#010101` (supporting), Dark Gray `#545859` (supporting), Gray `#97999B` (supporting), Tan `#BF9474` (supporting)
- In-game colours: primary `#BA0C2F`, secondary `#FFFFFF`, tertiary `#010101`
- Kits: main `#BA0C2F`, second `#FFFFFF`; sheets `kits/sacredheartpioneers_home.png`, `kits/sacredheartpioneers_away.png`
- Logo: `logos/teams/sacredheartpioneers.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 11 | Jacob Hewitt | C | R | 5'11" | 190 | 2002 | Ashburnham, MA |  |
| 13 | Gavin Bryant | C | L | 5'11" | 187 | 2004 | Ingersoll |  |
| 21 | Devin Phillips | C | L | 5'10" | 181 | 2001 | Raleigh, NC | A |
| 27 | Cole Galata | C | R | 6'0" | 185 | 2002 | Barrie | A |
| 38 | Mirko Buttazzoni | C | L | 5'11" | 190 | 2004 | Langley |  |
| 90 | Diego Buttazzoni | C | L | 5'9" | 183 | 2006 | Langley |  |
| 9 | Christian Chouha | LW | R | 5'11" | 181 | 2006 | Freehold, NJ |  |
| 14 | Jack Hillier | LW | R | 6'2" | 181 | 2004 | Petawawa |  |
| 16 | Nolan Flynn | LW | L | 6'1" | 185 | 2005 | Natick, MA |  |
| 28 | Emmanuel Vermette | LW | L | 6'1" | 205 | 2005 | Québec |  |
| 41 | Ethan Semeniuk | LW | R | 6'1" | 192 | 2005 | Fort St. John |  |
| 91 | Landen Gunderson | LW | R | 6'1" | 190 | 2004 | Plymouth, MN |  |
| 17 | Vitaly Levyy | RW | R | 5'11" | 174 | 2003 | Old Brookville, NY |  |
| 19 | Jake Bongo | RW | R | 5'10" | 170 | 2003 | Ridgefield, CT | C |
| 22 | Ashton Dahms | RW | L | 6'1" | 179 | 2005 | Lakeville, MN |  |
| 26 | Talon Brigley | RW | R | 5'11" | 176 | 2005 | Red Deer |  |
| 86 | Landon Hanson | RW | R | 6'0" | 181 | 2006 | Grande Prairie |  |
| 2 | Aiden VanRooyan | D | L | 6'2" | 201 | 2003 | Dakota Dunes, SD | A |
| 4 | Michael Rubin | D | R | 6'1" | 194 | 2003 | Blauvelt, NY |  |
| 5 | Luke Amell | D | R | 6'4" | 205 | 2003 | Mars, PA |  |
| 6 | Jack O'Brien | D | L | 6'2" | 205 | 2005 | Kennebunk, ME |  |
| 7 | Noah Roberts | D | L | 6'1" | 198 | 2005 | Boca Raton, FL |  |
| 8 | Chase Ramsay | D | R | 6'0" | 181 | 2003 | Granite Springs, NY |  |
| 10 | Liam Lesakowski | D | L | 6'5" | 225 | 2003 | Buffalo, NY |  |
| 23 | Lenny Greenberg | D | R | 6'0" | 181 | 2005 | El Segundo, CA |  |
| 48 | Nicholas Bernardo | D | L | 5'9" | 174 | 2003 | Hauppauge, NY |  |
| 1 | Ajeetpal Gundarah | G | L | 6'0" | 185 | 2003 | Richmond |  |
| 30 | Jack Spicer | G | L | 6'1" | 185 | 2004 | Rockville, MD |  |
| 31 | Nils Wallström | G | L | 6'3" | 185 | 2003 | Skellefteå |  |

## NCAA Independents

Logo: `logos/leagues/ind.png`

| Team | Abbr | City, State | Arena | Primary | Secondary | Tertiary | Kit main / second | Logo |
|---|---|---|---|---|---|---|---|---|
| Alaska Nanooks | UAF | Fairbanks, Alaska | Carlson Center | `#236192` | `#FFCD00` | `#010101` | `#236192` / `#FFCD00` | `logos/teams/alaskananooks.png` |
| Alaska Anchorage Seawolves | UAA | Anchorage, Alaska | Seawolf Sports Complex | `#115740` | `#FFC72C` | `` | `#115740` / `#FFC72C` | `logos/teams/alaskaanchorageseawolves.png` |
| LIU Sharks | LIU | Brooklyn, New York | Northwell Health Ice Center | `#69B3E7` | `#FFC72C` | `` | `#69B3E7` / `#FFC72C` | `logos/teams/liusharks.png` |
| Lindenwood Lions | LIN | St. Charles, Missouri | Centene Community Ice Center | `#010101` | `#B3A369` | `` | `#010101` / `#B3A369` | `logos/teams/lindenwoodlions.png` |
| Maryville Saints | MRY | Town and Country, Missouri | Maryville University Hockey Center | `#BF0D3E` | `#FFFFFF` | `#010101` | `#BF0D3E` / `#FFFFFF` | `logos/teams/maryvillesaints.png` |
| Stonehill Skyhawks | STO | Easton, Massachusetts | Bridgewater Ice Arena | `#582C83` | `#FFFFFF` | `#010101` | `#582C83` / `#FFFFFF` | `logos/teams/stonehillskyhawks.png` |

### Alaska Nanooks (UAF)

- School: University of Alaska Fairbanks, Fairbanks, Alaska
- Arena: Carlson Center
- School colours: Blue `#236192`, Gold `#FFCD00`, Black `#010101` (supporting)
- In-game colours: primary `#236192`, secondary `#FFCD00`, tertiary `#010101`
- Kits: main `#236192`, second `#FFCD00`; sheets `kits/alaskananooks_home.png`, `kits/alaskananooks_away.png`
- Logo: `logos/teams/alaskananooks.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 11 | Kyle Powers | C | L | 6'1" | 192 | 2005 | Ottawa |  |
| 15 | Dylan Sydor | C | R | 5'10" | 170 | 2003 | Kamloops |  |
| 18 | Rylen Roersma | C | L | 5'11" | 190 | 2004 | Lethbridge |  |
| 23 | Sam Elliott | C | L | 6'1" | 161 | 2005 | White Rock |  |
| 28 | Joseph Serpa | C | R | 6'0" | 165 | 2002 | Cambridge |  |
| 36 | Joel Lehtinen | C | L | 6'4" | 209 | 2003 | Dallas, TX |  |
| 8 | Davis Borozinskis | LW | R | 6'0" | 190 | 2004 | Liepaja |  |
| 10 | Jack Rogers | LW | R | 6'3" | 201 | 2002 | East Northport, NY |  |
| 13 | Tai Ushio | LW | R | 5'8" | 161 | 2005 | Hokkaido |  |
| 27 | Zachary Howard | LW | R | 6'1" | 174 | 2005 | New Richmond, WI |  |
| 34 | Jack Plandowski | LW | L | 5'10" | 174 | 2005 | Halifax |  |
| 91 | Simon Binkley | LW | L | 6'0" | 192 | 2006 | Fairbanks, AK |  |
| 6 | Jhett Larson | RW | L | 5'9" | 176 | 2004 | Delisle |  |
| 9 | Dean Spak | RW | L | 5'10" | 181 | 2003 | Calgary |  |
| 12 | Dominik Bartecko | RW | L | 5'9" | 174 | 2003 | Praha |  |
| 17 | Mykhailo Danylov | RW | L | 5'8" | 154 | 2004 | Kyiv |  |
| 29 | Beau Courtney | RW | R | 5'10" | 179 | 2004 | Parker, CO |  |
| 2 | Carson Reed | D | L | 6'0" | 194 | 2004 | Warroad, MN |  |
| 5 | William Fleet | D | R | 6'2" | 205 | 2004 | Denver, CO |  |
| 7 | Jackson Anderson | D | R | 6'2" | 196 | 2004 | Prior Lake, MN |  |
| 14 | Seth Fryer | D | R | 6'7" | 209 | 2006 | Victoria |  |
| 16 | Linden Burrett | D | R | 6'2" | 183 | 2006 | Surrey |  |
| 20 | Grady Gallatin | D | L | 6'4" | 205 | 2004 | White Bear Lake, MN |  |
| 24 | Lukas Klemm | D | R | 6'2" | 194 | 2004 | Ridgefield, CT |  |
| 25 | Max Rud | D | L | 6'3" | 190 | 2003 | St. Cloud, MN |  |
| 26 | Nathan Rickey | D | R | 6'4" | 170 | 2003 | New Lenox, IL |  |
| 31 | Zakari Brice | G | L | 5'11" | 181 | 2003 | Fairbanks, AK |  |
| 32 | Calvin Vachon | G | L | 5'11" | 168 | 2005 | Redondo Beach, CA |  |
| 62 | Tyler Shea | G | L | 6'0" | 179 | 2001 | Stevenson Ranch, CA |  |

### Alaska Anchorage Seawolves (UAA)

- School: University of Alaska Anchorage, Anchorage, Alaska
- Arena: Seawolf Sports Complex
- School colours: Green `#115740`, Gold `#FFC72C`
- In-game colours: primary `#115740`, secondary `#FFC72C`
- Kits: main `#115740`, second `#FFC72C`; sheets `kits/alaskaanchorageseawolves_home.png`, `kits/alaskaanchorageseawolves_away.png`
- Logo: `logos/teams/alaskaanchorageseawolves.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 13 | Adam Parsons | C | L | 6'0" | 170 | 2003 | Port Moody |  |
| 15 | Jacob Terpstra | C | R | 6'0" | 190 | 2005 | Shelby Township, MI |  |
| 18 | Jalen Bianchet | C | L | 6'0" | 181 | 2005 | Pickering |  |
| 21 | Ben Anderson | C | R | 6'5" | 209 | 2002 | Crystal, MN |  |
| 43 | Karter McNarland | C | L | 5'11" | 190 | 2003 | Saskatoon |  |
| 86 | Charles-Edward Tardif | C | L | 6'0" | 201 | 2003 | Québec |  |
| 5 | Connor Brown | LW | R | 5'8" | 185 | 2005 | Estero, FL |  |
| 9 | Oren Shtrom | LW | L | 5'10" | 185 | 2004 | Dollard-des-Ormeaux |  |
| 11 | Bryce Monrean | LW | L | 6'1" | 181 | 2003 | Anchorage, AK |  |
| 22 | Aiden Westin | LW | R | 6'1" | 185 | 2002 | Anchorage, AK |  |
| 27 | Samuel Boisvert | LW | L | 6'5" | 183 | 2005 | Mirabel |  |
| 96 | Matthew Martin-Gaudreault | LW | L | 6'6" | 216 | 2005 | Montréal |  |
| 17 | Paul Bloomer | RW | R | 6'0" | 179 | 2005 | Chicago, IL |  |
| 19 | Tye Spencer | RW | R | 5'8" | 150 | 2004 | Saskatoon |  |
| 23 | Aaron Reierson | RW | R | 6'1" | 183 | 2005 | Moorhead, MN |  |
| 29 | Ethan Neutens | RW | R | 6'4" | 207 | 2005 | Cochrane |  |
| 32 | Dylan Contreras | RW | R | 5'7" | 170 | 2003 | Yorba Linda, CA |  |
| 2 | Ethan Warrener | D | L | 6'1" | 187 | 2003 | Calgary |  |
| 4 | Niko Tsakumis | D | L | 6'0" | 190 | 2005 | Vancouver |  |
| 7 | Cooper Anderson | D | L | 6'4" | 207 | 2006 | Elk River, MN |  |
| 8 | Cosmo Wilson | D | R | 6'0" | 163 | 2005 | Vernon |  |
| 10 | Drew Nelson | D | R | 5'10" | 172 | 2006 | Duluth, MN |  |
| 12 | John-Paul Steele | D | R | 6'2" | 201 | 2003 | Franklin, MI |  |
| 26 | Drew Hockley | D | R | 6'3" | 196 | 2005 | Kentville |  |
| 55 | Luke Helgeson | D | L | 6'0" | 205 | 2004 | Anchorage, AK |  |
| 77 | Rhett Ravndahl | D | R | 6'2" | 185 | 2005 | Birch Hills |  |
| 31 | Bryant Marks | G | L | 6'0" | 174 | 2004 | Wasilla, AK |  |
| 33 | William Gramme | G | L | 6'4" | 209 | 2002 | Stockholm |  |
| 72 | Joey Rocha | G | L | 6'2" | 181 | 2005 | Nanaimo |  |

### LIU Sharks (LIU)

- School: Long Island University, Brooklyn, New York
- Arena: Northwell Health Ice Center
- School colours: Blue `#69B3E7`, Gold `#FFC72C`
- In-game colours: primary `#69B3E7`, secondary `#FFC72C`
- Kits: main `#69B3E7`, second `#FFC72C`; sheets `kits/liusharks_home.png`, `kits/liusharks_away.png`
- Logo: `logos/teams/liusharks.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 10 | Heath Armstrong | C | L | 5'8" | 172 | 2002 | Calgary |  |
| 12 | Brennan Nelson | C | L | 5'11" | 205 | 2003 | Edmonton |  |
| 18 | Anthony Lucarelli | C | L | 6'2" | 190 | 2003 | Chatham |  |
| 21 | Bo Cosman | C | L | 6'3" | 192 | 2002 | Lexington, KY |  |
| 26 | Isaac Lambert | C | L | 6'1" | 196 | 2003 | Victoriaville |  |
| 29 | Jaden Dyke | C | L | 6'4" | 205 | 2004 | St. John's |  |
| 13 | Luca Leighton | LW | L | 5'8" | 181 | 2003 | Old Brookville, NY |  |
| 14 | Luc Bydal | LW | L | 6'1" | 185 | 2005 | Grand Forks, ND |  |
| 17 | Sixten Jennersjö | LW | L | 5'11" | 190 | 2003 | Ekerö |  |
| 34 | Samuel Rousseau | LW | L | 6'0" | 194 | 2005 | St-Eustache |  |
| 38 | J.R. Perdion | LW | R | 5'11" | 181 | 2003 | Strongsville, OH |  |
| 71 | Zachary Benayon | LW | L | 6'2" | 194 | 2005 | Vaughan |  |
| 9 | Anthony Calafiore | RW | R | 5'9" | 170 | 2002 | Staten Island, NY |  |
| 11 | Johnny McMahon | RW | R | 5'11" | 190 | 2004 | Oceanside, NY |  |
| 15 | Alexis Bourque | RW | R | 6'1" | 198 | 2005 | Gatineau |  |
| 19 | Sam Ranallo | RW | L | 5'10" | 168 | 2004 | Rogers, MN |  |
| 23 | Dalton Andrew | RW | R | 6'5" | 205 | 2003 | Brandon |  |
| 27 | Dylan Kinch | RW | R | 5'11" | 185 | 2004 | Salzburg |  |
| 2 | Trenton Powell | D | R | 5'10" | 185 | 2004 | Soldotna, AK |  |
| 6 | Blake Matheson | D | R | 6'1" | 192 | 2005 | Everett, WA |  |
| 7 | Conner Brown | D | L | 6'0" | 194 | 2003 | Maplewood, MN |  |
| 8 | Jayden Veney | D | L | 6'7" | 223 | 2005 | Rochester, MN |  |
| 16 | Lucas Lemieux | D | L | 6'4" | 216 | 2005 | Edmonton |  |
| 22 | Nate Benoit | D | L | 6'1" | 187 | 2002 | Bow, NH |  |
| 24 | Luke Hause | D | L | 6'4" | 194 | 2004 | St. Paul, MN |  |
| 25 | August Classon | D | L | 6'0" | 185 | 2004 | Stockholm |  |
| 28 | Garrett Valk | D | R | 6'3" | 205 | 2002 | North Vancouver |  |
| 30 | Daniel Duris | G | R | 6'1" | 181 | 2003 | Martin |  |
| 37 | Lukas Renaud | G | L | 6'2" | 194 | 2004 | Montréal |  |
| 39 | Dane Callaway | G | L | 6'3" | 216 | 2005 | Duluth, MN |  |

### Lindenwood Lions (LIN)

- School: Lindenwood University, St. Charles, Missouri
- Arena: Centene Community Ice Center
- School colours: Black `#010101`, Gold `#B3A369`
- In-game colours: primary `#010101`, secondary `#B3A369`
- Kits: main `#010101`, second `#B3A369`; sheets `kits/lindenwoodlions_home.png`, `kits/lindenwoodlions_away.png`
- Logo: `logos/teams/lindenwoodlions.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 2 | Ryan Abraham | C | L | 5'8" | 170 | 2004 | Troy, MI |  |
| 7 | Andrew LeBlanc | C | L | 6'0" | 183 | 2004 | George Town |  |
| 10 | Chase LaPinta | C | R | 5'9" | 170 | 2004 | Frisco, TX |  |
| 13 | Adam Raesler | C | L | 6'1" | 185 | 2003 | Edmonton |  |
| 15 | Will Schumacher | C | L | 6'0" | 181 | 2004 | Prior Lake, MN |  |
| 19 | Kian Bell | C | L | 5'10" | 170 | 2003 | Ottawa |  |
| 12 | Ethan Zielke | LW | L | 5'10" | 190 | 2003 | Calgary | A |
| 16 | Anselmo Rego | LW | R | 5'9" | 176 | 2005 | Brampton |  |
| 22 | Ty Nash | LW | L | 5'11" | 179 | 2003 | Scottsdale, AZ |  |
| 24 | Noah Dziver | LW | R | 6'0" | 185 | 2005 | Winnipeg |  |
| 25 | Caden Brown | LW | R | 5'11" | 190 | 2005 | Fort St. John |  |
| 26 | Tyler Loughman | LW | R | 5'11" | 174 | 2003 | St. Louis, MO |  |
| 9 | Christopher Thibodeau | RW | R | 5'10" | 170 | 2005 | Charleston, SC |  |
| 17 | Andrew Earl | RW | R | 6'0" | 190 | 2005 | Eden Prairie, MN |  |
| 21 | Kyle Rohrer | RW | L | 6'0" | 174 | 2005 | Oregon, WI |  |
| 23 | Will Winemaster | RW | R | 6'3" | 205 | 2003 | Chicago, IL |  |
| 27 | Brady McIsaac | RW | L | 5'9" | 165 | 2004 | Prince George |  |
| 3 | Kim Elfving | D | L | 6'4" | 209 | 2005 | Södertälje |  |
| 4 | Connor Gengle | D | L | 5'11" | 174 | 2005 | Grand Rapids, MI |  |
| 5 | Jonathan Wong | D | R | 5'9" | 161 | 2004 | North Vancouver |  |
| 6 | Michael Bevilacqua | D | L | 6'2" | 205 | 2003 | Hamburg, NY |  |
| 8 | Jacob Delaney | D | L | 6'2" | 218 | 2004 | Glen Ellyn, IL |  |
| 11 | Jacob LeBlanc | D | R | 6'1" | 187 | 2004 | George Town |  |
| 18 | Ethan Elias | D | R | 5'8" | 187 | 2003 | Maple Grove, MN |  |
| 28 | Brody Mortensen | D | L | 6'1" | 185 | 2004 | Humboldt |  |
| 29 | Cody Butikis | G | L | 6'3" | 214 | 2005 | Sewell, NJ |  |
| 31 | Klayton Knapp | G | L | 6'0" | 190 | 2003 | Sylvania, OH |  |
| 35 | Liam Beerman | G | L | 6'0" | 165 | 2004 | Marquette, MI |  |

### Maryville Saints (MRY)

- School: Maryville University, Town and Country, Missouri
- Arena: Maryville University Hockey Center
- School colours: Red `#BF0D3E`, White `#FFFFFF`, Black `#010101`
- In-game colours: primary `#BF0D3E`, secondary `#FFFFFF`, tertiary `#010101`
- Kits: main `#BF0D3E`, second `#FFFFFF`; sheets `kits/maryvillesaints_home.png`, `kits/maryvillesaints_away.png`
- Logo: `logos/teams/maryvillesaints.png`

No roster of its own yet: in NHL 19 it plays with the template club's players.

### Stonehill Skyhawks (STO)

- School: Stonehill College, Easton, Massachusetts
- Arena: Bridgewater Ice Arena
- School colours: Purple `#582C83`, White `#FFFFFF`, Black `#010101` (supporting), Medium Gray `#676767` (supporting), Light Gray `#AEAEAE` (supporting), 69% Pantone 268 C `#8C6DA9` (supporting), 50% Pantone 268 C `#AC96C1` (supporting)
- In-game colours: primary `#582C83`, secondary `#FFFFFF`, tertiary `#010101`
- Kits: main `#582C83`, second `#FFFFFF`; sheets `kits/stonehillskyhawks_home.png`, `kits/stonehillskyhawks_away.png`
- Logo: `logos/teams/stonehillskyhawks.png`

| # | Name | Pos | Shoots | Ht | Wt | Born | Birthplace | Capt |
|---|---|---|---|---|---|---|---|---|
| 12 | Zachary Aben | C | L | 5'9" | 181 | 2003 | Harvey's Lake, PA | A |
| 13 | Leo Chambers | C | L | 6'1" | 190 | 2003 | Toronto | A |
| 15 | Billy Renfrew | C | L | 6'0" | 183 | 2003 | Fairbanks, AK |  |
| 20 | Tanner Duncan | C | L | 5'10" | 181 | 2005 | Ridgefield, CT |  |
| 27 | Dimitry Kebreau | C | L | 5'11" | 190 | 2003 | Silver Spring, MD |  |
| 38 | Liam Fitzpatrick | C | R | 6'1" | 190 | 2005 | Moose Jaw |  |
| 7 | Hayden Fechner | LW | L | 6'2" | 181 | 2005 | Edmonton |  |
| 10 | Joseph Grainda | LW | L | 6'3" | 190 | 2002 | Carmel, IN | A |
| 18 | Owen Drury | LW | R | 6'4" | 194 | 2005 | Newport Beach, CA |  |
| 24 | Anthony Galante | LW | L | 5'11" | 174 | 2002 | Morganville, NJ |  |
| 29 | Landon Mackie | LW | R | 6'0" | 196 | 2006 | Victoria |  |
| 14 | Cole Melady | RW | L | 5'11" | 190 | 2003 | Seaforth |  |
| 17 | Nikita Ivashchenko | RW | R | 6'1" | 172 | 2005 | Kharkiv |  |
| 19 | Patrick Murphy | RW | R | 5'11" | 190 | 2005 | North Kingstown, RI |  |
| 37 | Jack Gorton | RW | R | 6'3" | 201 | 2002 | Granite Springs, NY |  |
| 71 | Matthew Romer | RW | R | 5'11" | 185 | 2002 | Carpentersville, IL | A |
| 2 | Adam Mahler | D | R | 6'2" | 218 | 2004 | Ancaster |  |
| 4 | Charlie Banquier | D | R | 6'2" | 201 | 2002 | Oakville | C |
| 5 | Caden Tremblay | D | L | 5'10" | 170 | 2005 | Port Alberni |  |
| 6 | Justin Gibson | D | L | 6'1" | 190 | 2003 | Pittsburgh, PA |  |
| 11 | Wyatt Davis | D | R | 6'4" | 185 | 2005 | Cobden |  |
| 16 | Luke Lepper | D | L | 6'0" | 185 | 2005 | Brandon |  |
| 21 | Devon Carlstrom | D | L | 6'3" | 212 | 2002 | Palm Beach Gardens, FL |  |
| 22 | Jordin Palmer | D | L | 6'2" | 194 | 2004 | Auburn, NH |  |
| 28 | Matthew Rafalski | D | R | 6'1" | 190 | 2004 | Waupaca, WI |  |
| 48 | Ozbej Rep | D | R | 6'1" | 192 | 2005 | Jesenice |  |
| 55 | Dominick Campione | D | R | 5'11" | 154 | 2002 | Kohler, WI |  |
| 1 | Mason Vaccari | G | L | 5'11" | 183 | 2005 | Mississauga |  |
| 32 | Linards Lipskis | G | L | 6'2" | 201 | 2003 | Sigulda |  |
| 33 | Pierson Haislet | G | L | 6'0" | 185 | 2005 | Chicago, IL |  |
