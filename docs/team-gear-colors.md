# NHL 27 team helmet and glove colours

The helmet and gloves each team wears with the jerseys in its **Home** and **Away** slots, for all 537 teams. St. Louis, for example, is `adidas fourteen` at home and `adidas fifteen` away.

Read on 2026-11-07 from `nhl_game.db` in the game files, so any mods applied to that database are included.

- **Slot:** the jersey marked as default for the team with the Home or Away label in `stockteamjerseys`.
- **Colours:** hex `RRGGBB` from `stockteamequipmenttable`, using the most specific row for that jersey (era and jersey match first, then the team's general row).
- **Helmet:** Shell (zone 1), Trim (zone 2) and Logo (zone 3, the maker's logo). Other helmet zones aren't used by the model.
- **Gloves:** the model uses zones 1 to 12. *Gloves main* is the colour covering the most glove area, and *Other glove zones* lists every zone that differs. For example, `FFB60F` z7 means zone 7 is `FFB60F` and the rest are the main colour.
- **†** The team has no row of its own, so the colour comes from another team using the same jersey art, such as an alumni team using its club's row. This fallback is an assumption, not something confirmed in game.
- **—** No matching colour in the table.
- **\*** These teams label their two default jerseys 8 and 9, not Home and Away. The dark jersey is listed as Home and the light one as Away.
- A team name in brackets after a jersey is the team whose art that jersey uses.

## NHL (36)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Anaheim Ducks | Home | adidas ten | `E63917` | `202020` | `E7E7E7` | `202020` | `FC5F1A` z6,11; `E7E7E7` z7 |
| Anaheim Ducks | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `E63917` | `FC5F1A` | `202020` z6; `E7E7E7` z7 |
| Boston Bruins | Home | adidas thirteen | `202020` | `202020` | `E7E7E7` | `202020` | `FEB101` z7 |
| Boston Bruins | Away | adidas fourteen | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `FEB101` z7 |
| Buffalo Sabres | Home | adidas ten | `172F7E` | `E8E8E8` | `E7E7E7` | `172F7E` | `FEB101` z6 |
| Buffalo Sabres | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `1B1B1B` | `172F7E` | `FEB101` z6 |
| Calgary Flames | Home | adidas ten | `BD1C27` | `E7E7E7` | `E7E7E7` | `8C181C` | `C9962E` z3,8,9; `E7E7E7` z6; `202020` z7 |
| Calgary Flames | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `202020` | `8C181C` | `C9962E` z3,8,9; `E7E7E7` z6; `202020` z7 |
| Carolina Hurricanes | Home | adidas ten | `202020` | `202020` | `E7E7E7` | `1B1B1B` | `9A1B1F` z6; `E7E7E7` z7 |
| Carolina Hurricanes | Away | adidas thirteen | `BF221A` | `202020` | `E7E7E7` | `9A1B1F` | `202020` z6; `E7E7E7` z7 |
| Chicago Blackhawks | Home | adidas ten | `202020` | `202020` | `E7E7E7` | `202020` | `A00106` z3,4; `E7E7E7` z7 |
| Chicago Blackhawks | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `A00106` z3,4; `E7E7E7` z7 |
| Colorado Avalanche | Home | adidas ten | `202020` | `202020` | `E7E7E7` | `1B3971` | `E7E7E7` z6 |
| Colorado Avalanche | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `202020` | `1B3971` | `E7E7E7` z6 |
| Columbus Blue Jackets | Home | adidas ten | `132035` | `202020` | `E7E7E7` | `162335` | `9C1B1F` z4,6; `E7E7E7` z7 |
| Columbus Blue Jackets | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `132035` | `162335` | `9C1B1F` z4,6; `E7E7E7` z7 |
| Dallas Stars | Home | adidas ten | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Dallas Stars | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `E7E7E7` z7 |
| Detroit Red Wings | Home | adidas ten | `9F0D1A` | `E7E7E7` | `E7E7E7` | `9F0D1A` | `E7E7E7` z7 |
| Detroit Red Wings | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `9F0D1A` | `9F0D1A` | `E7E7E7` z7 |
| Edmonton Oilers | Home | adidas ten | `1A255E` | `E7E7E7` | `E7E7E7` | `1A255E` | `DA391C` z6,10; `E7E7E7` z7 |
| Edmonton Oilers | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `132032` | `1A255E` | `DA391C` z6,10; `E7E7E7` z7 |
| Florida Panthers | Home | adidas ten | `212B44` | `E7E7E7` | `E7E7E7` | `1A1E2C` | `E7E7E7` z7 |
| Florida Panthers | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `212B44` | `1A1E2C` | `E7E7E7` z7 |
| Los Angeles Kings | Home | adidas ten | `202020` | `202020` | `E7E7E7` | `202020` | `999D9D` z6 |
| Los Angeles Kings | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `999D9D` z6 |
| Minnesota Wild | Home | adidas ten | `183739` | `202020` | `CCB99C` | `18372F` | `7C1A15` z4,6; `CCB99C` z7 |
| Minnesota Wild | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `18372F` | `18372F` | `7C1A15` z4,6; `CCB99C` z7 |
| Montreal Canadiens | Home | adidas ten | `253B73` | `202020` | `E7E7E7` | `253B73` | `E7E7E7` z4,7; `90181C` z6,8,11 |
| Montreal Canadiens | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `202020` | `253B73` | `E7E7E7` z4,7; `90181C` z6,8,11 |
| Nashville Predators | Home | adidas ten | `EDB118` | `202020` | `18223A` | `1C1F2F` | `EDB118` z6,9 |
| Nashville Predators | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `18223A` | `1C1F2F` | `EDB118` z6,9 |
| New Jersey Devils | Home | adidas ten | `202020` | `202020` | `E7E7E7` | `922328` | `202020` z6; `E7E7E7` z7 |
| New Jersey Devils | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `202020` | `922328` | `202020` z6; `E7E7E7` z7 |
| New York Islanders | Home | adidas ten | `11205A` | `E7E7E7` | `E7E7E7` | `0F1C4F` | `C5431D` z6,10,11; `E7E7E7` z7 |
| New York Islanders | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `11205A` | `0F1C4F` | `C5431D` z6,10,11; `E7E7E7` z7 |
| New York Rangers | Home | adidas ten | `254C94` | `202020` | `E7E7E7` | `0C3361` | `CF1531` z1,8,11,12; `E7E7E7` z3,4,6 |
| New York Rangers | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `202020` | `0C3361` | `CF1531` z1,8,11,12; `E7E7E7` z3,4,6 |
| Ottawa Senators | Home | adidas ten | `202020` | `202020` | `E7E7E7` | `202020` | `9A2323` z6; `E7E7E7` z7 |
| Ottawa Senators | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `9A2323` z6; `E7E7E7` z7 |
| Philadelphia Flyers | Home | adidas ten | `202020` | `E7E7E7` | `E7E7E7` | `202020` | `E7E7E7` z6; `DA391C` z7 |
| Philadelphia Flyers | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `E7E7E7` z6; `DA391C` z7 |
| Pittsburgh Penguins | Home | adidas ten | `202020` | `E7E7E7` | `E7E7E7` | `202020` | `E7E7E7` z2,6 |
| Pittsburgh Penguins | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `E7E7E7` z2,6 |
| San Jose Sharks | Home | adidas ten | `0E555E` | `202020` | `E7E7E7` | `0E555E` | `E7E7E7` z6 |
| San Jose Sharks | Away | adidas eleven | `0E555E` | `E7E7E7` | `E7E7E7` | `0E555E` | `E7E7E7` z6 |
| Seattle Kraken | Home | adidas ten | `0B182E` | `202020` | `E7E7E7` | `0B182E` | `9BD8D8` z7 |
| Seattle Kraken | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `0B182E` | `0B182E` | `9BD8D8` z7 |
| St. Louis Blues | Home | adidas fourteen | `0072CE` | `202020` | `FFB60F` | `0072CE` | `FFB60F` z7 |
| St. Louis Blues | Away | adidas fifteen | `E7E7E7` | `E7E7E7` | `0072CE` | `0072CE` | `FFB60F` z7 |
| Tampa Bay Lightning | Home | adidas ten | `122E4D` | `202020` | `E7E7E7` | `153561` | `E7E7E7` z6; `202020` z7 |
| Tampa Bay Lightning | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `202020` | `153561` | `E7E7E7` z6; `202020` z7 |
| Toronto Maple Leafs | Home | adidas ten | `102962` | `E7E7E7` | `E7E7E7` | `007938` | `E7E7E7` z7 |
| Toronto Maple Leafs | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `202020` | `052E6C` | `E7E7E7` z6 |
| Utah Mammoth | Home | adidas twelve | `202020` | `E7E7E7` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Utah Mammoth | Away | adidas thirteen | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `E7E7E7` z6 |
| Vancouver Canucks | Home | adidas ten | `06386F` | `202020` | `E7E7E7` | `122854` | `E7E7E7` z7 |
| Vancouver Canucks | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `06386F` | `122854` | `E7E7E7` z7 |
| Vegas Golden Knights | Home | adidas ten | `3B3B42` | `202020` | `E7E7E7` | `E7E7E7` | `DEB253` z6,11; `202020` z7 |
| Vegas Golden Knights | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `202020` | `E7E7E7` | `DEB253` z6,11; `202020` z7 |
| Washington Capitals | Home | adidas ten | `051A2E` | `051A2E` | `BB1C20` | `BA1C20` | `071845` z2,4,5,7,11; `E7E7E7` z3,6 |
| Washington Capitals | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `BB1C20` | `BA1C20` | `071845` z2,4,5,7,11; `E7E7E7` z3,6 |
| Winnipeg Jets | Home | adidas ten | `14192C` | `1B1B1B` | `E7E7E7` | `14192C` | `2655B7` z6; `E7E7E7` z7 |
| Winnipeg Jets | Away | adidas eleven | `E7E7E7` | `E7E7E7` | `14192C` | `14192C` | `2655B7` z6; `E7E7E7` z7 |
| Atlantic Division All-Stars | Home | adidas eight | `202020` | `202020` | `E7E7E7` | `202020` | `F7B512` z6 |
| Atlantic Division All-Stars | Away | adidas nine | `202020` | `202020` | `E7E7E7` | `202020` | `F7B512` z6 |
| Central Division All-Stars | Home | adidas eight | `202020` | `202020` | `E7E7E7` | `202020` | `F7B512` z6 |
| Central Division All-Stars | Away | adidas nine | `202020` | `202020` | `E7E7E7` | `202020` | `F7B512` z6 |
| Metropolitan Division All-Stars | Home | adidas eight | `202020` | `202020` | `E7E7E7` | `202020` | `F7B512` z6 |
| Metropolitan Division All-Stars | Away | adidas nine | `202020` | `202020` | `E7E7E7` | `202020` | `F7B512` z6 |
| Pacific Division All-Stars | Home | adidas eight | `202020` | `202020` | `E7E7E7` | `202020` | `F7B512` z6 |
| Pacific Division All-Stars | Away | adidas nine | `202020` | `202020` | `E7E7E7` | `202020` | `F7B512` z6 |

## AHL (32)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Abbotsford Canucks | Home | rbk away | `06386F` | `202020` | `E7E7E7` | `122854` | `E7E7E7` z7 |
| Abbotsford Canucks | Away | rbk home | `E7E7E7` | `202020` | `06386F` | `122854` | `E7E7E7` z7 |
| Bakersfield Condors | Home | rbk away | `1A255E` | `E7E7E7` | `E7E7E7` | `1A255E` | `DA391C` z6,10,11; `E7E7E7` z7 |
| Bakersfield Condors | Away | rbk home | `E7E7E7` | `E7E7E7` | `1A255E` | `1A255E` | `DA391C` z6,10,11; `E7E7E7` z7 |
| Belleville Senators | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `9A2323` z6; `E7E7E7` z7,11 |
| Belleville Senators | Away | rbk home | `E7E7E7` | `EAE8E0` | `202020` | `202020` | `9A2323` z6; `E7E7E7` z7,11 |
| Calgary Wranglers | Home | rbk away | `BD1C27` | `E7E7E7` | `E7E7E7` | `8C181C` | `C9962E` z3,8,9; `E7E7E7` z6; `202020` z7 |
| Calgary Wranglers | Away | rbk home | `E7E7E7` | `E7E7E7` | `BD1C27` | `8C181C` | `C9962E` z3,8,9; `E7E7E7` z6; `202020` z7 |
| Charlotte Checkers | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `E9E9E9` z7 |
| Charlotte Checkers | Away | rbk home | `E7E7E7` | `EAE8E0` | `202020` | `202020` | `E9E9E9` z7 |
| Chicago Wolves | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Chicago Wolves | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `E7E7E7` z7 |
| Cleveland Monsters | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Cleveland Monsters | Away | rbk home | `E7E7E7` | `EAE8E0` | `202020` | `202020` | `E7E7E7` z7 |
| Coachella Valley Firebirds | Home | rbk away | `191D2E` | `202020` | `E7E7E7` | `191D2E` | `E7E7E7` z7 |
| Coachella Valley Firebirds | Away | rbk home | `E7E7E7` | `E7E7E7` | `191D2E` | `191D2E` | `9BD8D8` z7 |
| Colorado Eagles | Home | rbk away | `1B3971` | `E7E7E7` | `E7E7E7` | `1B3971` | `E7E7E7` z6 |
| Colorado Eagles | Away | rbk home | `E7E7E7` | `E7E7E7` | `0B182E` | `1B3971` | `E7E7E7` z6 |
| Grand Rapids Griffins | Home | rbk away | `9F0D1A` | `E7E7E7` | `E7E7E7` | `A31620` | `E7E7E7` z7 |
| Grand Rapids Griffins | Away | rbk home | `E7E7E7` | `EAE8E0` | `202020` | `A31620` | `E7E7E7` z7 |
| Hamilton Hammers | Home | rbk home | `001D42` | `E7E7E7` | `EB6411` | — |  |
| Hamilton Hammers | Away | rbk away | `E7E7E7` | `E7E7E7` | `001D42` | `001D42` | `EB6411` z6 |
| Hartford Wolf Pack | Home | rbk away | `21222E` | `202020` | `E7E7E7` | `1A1B25` | `CF1531` z1,12; `E7E7E7` z6,8 |
| Hartford Wolf Pack | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `1A1B25` | `CF1531` z1,12; `E7E7E7` z6,8 |
| Henderson Silver Knights | Home | rbk away | `C8C8C8` | `202020` | `E7E7E7` | `202020` | `DEB253` z3,7 |
| Henderson Silver Knights | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `E7E7E7` | `DEB253` z3,6; `202020` z7,11 |
| Hershey Bears | Home | rbk away | `180C0C` | `E7E7E7` | `E7E7E7` | `180C0C` | `D6C39A` z3,7 |
| Hershey Bears | Away | rbk home | `E7E7E7` | `EAE8E0` | `202020` | `180C0C` | `D6C39A` z3,7 |
| Iowa Wild | Home | rbk away | `18372F` | `202020` | `E7E7E7` | `18372F` | `6A0008` z4,6,10,11; `CCB99C` z7 |
| Iowa Wild | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `18372F` | `6A0008` z4,6,10,11; `CCB99C` z7 |
| Laval Rocket | Home | rbk away | `253B73` | `202020` | `E7E7E7` | `253B73` | `E7E7E7` z3,4,7,9,10,11; `90181C` z6,8 |
| Laval Rocket | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `253B73` | `E7E7E7` z3,4,7,9,10,11; `90181C` z6,8 |
| Lehigh Valley Phantoms | Home | rbk away | `0F0F0F` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Lehigh Valley Phantoms | Away | rbk home | `FFFFF7` | `EAE8E0` | `202020` | `202020` | `E7E7E7` z7 |
| Manitoba Moose | Home | rbk away | `14192C` | `14192C` | `E7E7E7` | `162338` | `E7E7E7` z7 |
| Manitoba Moose | Away | rbk home | `E7E7E7` | `EAE8E0` | `202020` | `162338` | `E7E7E7` z7 |
| Milwaukee Admirals | Home | rbk away | `14192C` | `202020` | `E7E7E7` | `14192C` | `E7E7E7` z7 |
| Milwaukee Admirals | Away | rbk home | `E7E7E7` | `EAE8E0` | `202020` | `14192C` | `E7E7E7` z7 |
| Ontario Reign | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `999D9D` z6 |
| Ontario Reign | Away | rbk home | `E7E7E7` | `EAE8E0` | `202020` | `202020` | `999D9D` z6 |
| Providence Bruins | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Providence Bruins | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `E7E7E7` z7 |
| Rochester Americans | Home | rbk away | `122663` | `202020` | `E7E7E7` | `002663` | `CC092F` z1,8; `E7E7E7` z6 |
| Rochester Americans | Away | rbk home | `E7E7E7` | `EAE8E0` | `202020` | `002663` | `CC092F` z1,8; `E7E7E7` z6 |
| Rockford IceHogs | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `A00106` z3,4; `E7E7E7` z7 |
| Rockford IceHogs | Away | rbk home | `E7E7E7` | `EAE8E0` | `202020` | `202020` | `A00106` z3,4; `E7E7E7` z7 |
| San Diego Gulls | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| San Diego Gulls | Away | rbk home | `E7E7E7` | `EAE8E0` | `202020` | `202020` | `E7E7E7` z7 |
| San Jose Barracuda | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z6; `0E555E` z7 |
| San Jose Barracuda | Away | rbk home | `0E555E` | `EAE8E0` | `E7E7E7` | `0E555E` | `E7E7E7` z6 |
| Springfield Thunderbirds | Home | rbk away | `194487` | `202020` | `CFAE65` | `191D2E` | `CFAE65` z7 |
| Springfield Thunderbirds | Away | rbk home | `E7E7E7` | `EAE8E0` | `DFB56A` | `191D2E` | `CFAE65` z7 |
| Syracuse Crunch | Home | rbk away | `142C5D` | `202020` | `E7E7E7` | `142C5D` | `E7E7E7` z6 |
| Syracuse Crunch | Away | rbk home | `E7E7E7` | `E8E8E8` | `202020` | `142C5D` | `E7E7E7` z6 |
| Texas Stars | Home | rbk away | `1B1B1B` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Texas Stars | Away | rbk home | `E7E7E7` | `E7E7E7` | `1B1B1B` | `202020` | `E7E7E7` z7 |
| Toronto Marlies | Home | rbk away | `213363` | `202020` | `E7E7E7` | `162B64` | `E7E7E7` z6 |
| Toronto Marlies | Away | rbk home | `E7E7E7` | `EAE8E0` | `202020` | `162B64` | `E7E7E7` z6 |
| Tucson Roadrunners | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `023008` z5; `E7E7E7` z7; `631219` z8,9 |
| Tucson Roadrunners | Away | rbk home | `E7E7E7` | `EAE8E0` | `202020` | `202020` | `023008` z5; `E7E7E7` z7; `631219` z8,9 |
| Utica Comets | Home | rbk away | `202020` | `202020` | `E7E7E7` | `922328` | `202020` z6; `E7E7E7` z7 |
| Utica Comets | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `922328` | `202020` z6; `E7E7E7` z7 |
| Wilkes-Barre/Scranton Penguins | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z3,6 |
| Wilkes-Barre/Scranton Penguins | Away | rbk home | `E7E7E7` | `EAE8E0` | `202020` | `202020` | `E7E7E7` z3,6 |

## ECHL (30)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Adirondack Thunder | Home | rbk away | `202020` | `202020` | `E7E7E7` | `1B1B1B` | `E7E7E7` z7 |
| Adirondack Thunder | Away | rbk home | `202020` | `202020` | `E7E7E7` | `1B1B1B` | `E7E7E7` z7 |
| Allen Americans | Home | rbk away | `7B0E0A` | `1B1B1B` | `E7E7E7` | `7B0E0A` | `E7E7E7` z6; `0B182E` z7 |
| Allen Americans | Away | rbk home | `7B0E0A` | `E7E7E7` | `E7E7E7` | `7B0E0A` | `E7E7E7` z6; `0B182E` z7 |
| Atlanta Gladiators | Home | rbk away | `0B182E` | `202020` | `E7E7E7` | `0B182E` | `FCBB17` z6 |
| Atlanta Gladiators | Away | rbk home | `E7E7E7` | `E7E7E7` | `0E1D38` | `0B182E` | `FCBB17` z6 |
| Bloomington Bison | Home | rbk away | `202020` | `E7E7E7` | `E7E7E7` | `202020` | `9F0D1A` z2,4; `E7E7E7` z7 |
| Bloomington Bison | Away | rbk home | `202020` | `E7E7E7` | `E7E7E7` | `202020` | `9F0D1A` z2,4; `E7E7E7` z7 |
| Cincinnati Cyclones | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `9A1B1F` z6 |
| Cincinnati Cyclones | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `9A1B1F` z6 |
| Florida Everblades | Home | rbk away | `0B182E` | `202020` | `E7E7E7` | `0B182E` | `0B703B` z6 |
| Florida Everblades | Away | rbk home | `0B703B` | `E7E7E7` | `E7E7E7` | `14243F` | `0B703B` z6 |
| Fort Wayne Komets | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `FF8701` z4,6,10; `E7E7E7` z7 |
| Fort Wayne Komets | Away | rbk home | `202020` | `202020` | `E7E7E7` | `202020` | `FF8701` z4,6,10; `E7E7E7` z7 |
| Greensboro Gargoyles | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `46046D` z6; `E7E7E7` z7 |
| Greensboro Gargoyles | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `46046D` z6; `E7E7E7` z7 |
| Greenville Swamp Rabbits | Home | rbk away | `0B182E` | `202020` | `E7E7E7` | `0B182E` | `FF8701` z6 |
| Greenville Swamp Rabbits | Away | rbk home | `0B182E` | `202020` | `E7E7E7` | `0B182E` | `FF8701` z6 |
| Idaho Steelheads | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `999D9D` z6; `E7E7E7` z7 |
| Idaho Steelheads | Away | rbk home | `202020` | `202020` | `E7E7E7` | `202020` | `999D9D` z6; `E7E7E7` z7 |
| Indy Fuel | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `7B0E0A` z3,4; `E7E7E7` z7 |
| Indy Fuel | Away | rbk home | `202020` | `202020` | `E7E7E7` | `202020` | `7B0E0A` z3,4; `E7E7E7` z7 |
| Jacksonville IceMen | Home | rbk away | `202020` | `202020` | `E7E7E7` | `0B182E` | `2655B7` z6 |
| Jacksonville IceMen | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `0B182E` | `2655B7` z6 |
| Kalamazoo Wings | Home | rbk away | `0B182E` | `202020` | `E7E7E7` | `0B182E` | `7B0E0A` z6; `E7E7E7` z7 |
| Kalamazoo Wings | Away | rbk home | `E7E7E7` | `E7E7E7` | `0B182E` | `0B182E` | `7B0E0A` z6; `E7E7E7` z7 |
| Kansas City Mavericks | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `CF3D1C` z7 |
| Kansas City Mavericks | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `CF3D1C` z7 |
| Maine Mariners | Home | rbk away | `0B182E` | `E8E8E8` | `E7E7E7` | `0B182E` | `2D8A4E` z7 |
| Maine Mariners | Away | rbk home | `E7E7E7` | `E7E7E7` | `0B182E` | `0B182E` | `2D8A4E` z7 |
| New Mexico Goatheads | Home | rbk home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| New Mexico Goatheads | Away | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Norfolk Admirals | Home | rbk away | `0B182E` | `202020` | `E7E7E7` | `1C1F2F` | `EDB118` z6 |
| Norfolk Admirals | Away | rbk home | `0B182E` | `202020` | `E7E7E7` | `1C1F2F` | `EDB118` z6 |
| Orlando Solar Bears | Home | rbk away | `392078` | `E7E7E7` | `E7E7E7` | `392078` | `E7E7E7` z7 |
| Orlando Solar Bears | Away | rbk home | `392078` | `E7E7E7` | `E7E7E7` | `392078` | `E7E7E7` z7 |
| Rapid City Rush | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z2,7; `90181C` z6 |
| Rapid City Rush | Away | rbk home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z2,7; `90181C` z6 |
| Reading Royals | Home | rbk away | `202020` | `202020` | `E7E7E7` | `1B1B1B` | `392078` z6; `DA391C` z7 |
| Reading Royals | Away | rbk home | `202020` | `202020` | `E7E7E7` | `1B1B1B` | `392078` z6; `DA391C` z7 |
| Savannah Ghost Pirates | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `5AB42C` z7 |
| Savannah Ghost Pirates | Away | rbk home | `202020` | `202020` | `E7E7E7` | `202020` | `5AB42C` z7 |
| South Carolina Stingrays | Home | rbk away | `0B182E` | `202020` | `E7E7E7` | `90181C` | `0B182E` z4,5,7; `E7E7E7` z6 |
| South Carolina Stingrays | Away | rbk home | `E7E7E7` | `E7E7E7` | `0B182E` | `90181C` | `0B182E` z4,5,7; `E7E7E7` z6 |
| Tahoe Knight Monsters | Home | rbk away | `202020` | `202020` | `B79858` | `202020` | `0C768C` z6; `B79858` z7 |
| Tahoe Knight Monsters | Away | rbk home | `E7E7E7` | `E7E7E7` | `B79858` | `202020` | `0C768C` z6; `B79858` z7 |
| Toledo Walleye | Home | rbk away | `0B182E` | `202020` | `E7E7E7` | `0B182E` | `FFC423` z6 |
| Toledo Walleye | Away | rbk home | `E7E7E7` | `E7E7E7` | `0B182E` | `0B182E` | `FFC423` z6 |
| Trenton Ironhawks | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `2B623F` z2,5; `E7E7E7` z7 |
| Trenton Ironhawks | Away | rbk home | `202020` | `202020` | `E7E7E7` | `202020` | `2B623F` z2,5; `E7E7E7` z7 |
| Trois-Rivieres Lions | Home | rbk away | `003E7E` | `202020` | `E7E7E7` | `003E7E` | `E7E7E7` z7 |
| Trois-Rivieres Lions | Away | rbk home | `003E7E` | `202020` | `E7E7E7` | `003E7E` | `E7E7E7` z7 |
| Tulsa Oilers | Home | rbk away | `0B182E` | `202020` | `E7E7E7` | `0B182E` | `E7E7E7` z6 |
| Tulsa Oilers | Away | rbk home | `0B182E` | `202020` | `E7E7E7` | `0B182E` | `E7E7E7` z6 |
| Wheeling Nailers | Home | rbk away | `202020` | `E7E7E7` | `E7E7E7` | `202020` | `E7E7E7` z6 |
| Wheeling Nailers | Away | rbk home | `EDBA13` | `E7E7E7` | `202020` | `202020` | `E7E7E7` z6 |
| Wichita Thunder | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z4,6 |
| Wichita Thunder | Away | rbk home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z4,6 |
| Worcester Railers | Home | rbk away | `0B182E` | `202020` | `E7E7E7` | `0B182E` | `E7E7E7` z6; `999D9D` z7 |
| Worcester Railers | Away | rbk home | `0B182E` | `202020` | `E7E7E7` | `0B182E` | `E7E7E7` z6; `999D9D` z7 |

## PWHL (12)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Boston Fleet | Home | nineties home | `E7E7E7` | `EAE8E0` | `202020` | `253B73` | `E7E7E7` z7; `90181C` z8,9 |
| Boston Fleet | Away | nineties away | `E7E7E7` | `EAE8E0` | `202020` | `253B73` | `E7E7E7` z7; `90181C` z8,9 |
| Minnesota Frost | Home | nineties home | `202020` | `202020` | `E7E7E7` | `9F0D1A` | `E7E7E7` z7 |
| Minnesota Frost | Away | nineties away | `202020` | `202020` | `E7E7E7` | `9F0D1A` | `E7E7E7` z7 |
| Montreal Victoire | Home | nineties home | `0F0F0F` | `202020` | `E7E7E7` | `202020` | `9F0D1A` z3; `E7E7E7` z7 |
| Montreal Victoire | Away | nineties away | `0F0F0F` | `202020` | `E7E7E7` | `202020` | `9F0D1A` z3; `E7E7E7` z7 |
| New York Sirens | Home | nineties home | `21356C` | `202020` | `E7E7E7` | `21356C` | `F0BD18` z7 |
| New York Sirens | Away | nineties away | `21356C` | `202020` | `E7E7E7` | `21356C` | `F0BD18` z7 |
| Ottawa Charge | Home | nineties home | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `851228` z6; `E7E7E7` z7 |
| Ottawa Charge | Away | nineties away | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `851228` z6; `E7E7E7` z7 |
| PWHL Detroit | Home | nineties home | `E7E7E7` | `EAE8E0` | `21356A` | `0B182E` | `90181C` z2,8; `E7E7E7` z3,7,9 |
| PWHL Detroit | Away | nineties away | `E7E7E7` | `EAE8E0` | `21356A` | `0B182E` | `90181C` z2,8; `E7E7E7` z3,7,9 |
| PWHL Hamilton | Home | nineties home | `041E42` | `202020` | `E7E7E7` | `041E42` | `862633` z6; `E7E7E7` z7 |
| PWHL Hamilton | Away | nineties away | `041E42` | `202020` | `E7E7E7` | `041E42` | `862633` z6; `E7E7E7` z7 |
| PWHL Las Vegas | Home | nineties home | `250E62` | `E7E7E7` | `E7E7E7` | `250E62` | `A07DC5` z7 |
| PWHL Las Vegas | Away | nineties away | `250E62` | `E7E7E7` | `E7E7E7` | `250E62` | `A07DC5` z7 |
| PWHL San Jose | Home | nineties home | `041E42` | `202020` | `E7E7E7` | `041E42` | `ED8B00` z7 |
| PWHL San Jose | Away | nineties away | `041E42` | `202020` | `E7E7E7` | `041E42` | `ED8B00` z7 |
| Seattle Torrent | Home | nineties home | `861519` | `E7E7E7` | `E7E7E7` | `861519` | `E7E7E7` z7 |
| Seattle Torrent | Away | nineties away | `861519` | `E7E7E7` | `E7E7E7` | `861519` | `E7E7E7` z7 |
| Toronto Sceptres | Home | nineties home | `E7E7E7` | `202020` | `202020` | `162338` | `9C1B1F` z4,8,9; `E7E7E7` z7 |
| Toronto Sceptres | Away | nineties away | `E7E7E7` | `202020` | `202020` | `162338` | `9C1B1F` z4,8,9; `E7E7E7` z7 |
| Vancouver Goldeneyes | Home | nineties home | `9F0D1A` | `E7E7E7` | `E7E7E7` | `9F0D1A` | `E7E7E7` z7 |
| Vancouver Goldeneyes | Away | nineties away | `9F0D1A` | `E7E7E7` | `E7E7E7` | `9F0D1A` | `E7E7E7` z7 |

## OHL (20)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Barrie Colts | Home | rbk away | `1C1E33` | `202020` | `E7E7E7` | `1C1E33` | `FFC423` z7 |
| Barrie Colts | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `1C1E33` | `FFC423` z7 |
| Brampton Steelheads | Home | rbk away | `102962` | `202020` | `E7E7E7` | `002D62` | `E7E7E7` z6 |
| Brampton Steelheads | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `002D62` | `E7E7E7` z6 |
| Brantford Bulldogs | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `EEBA23` z7 |
| Brantford Bulldogs | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `EEBA23` z7 |
| Erie Otters | Home | rbk away | `1C1E33` | `202020` | `E7E7E7` | `1C1E33` | `DDAA1F` z6 |
| Erie Otters | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `1C1E33` | `DDAA1F` z1,2,7,8,9; `E7E7E7` z6 |
| Flint Firebirds | Home | rbk away | `051A2E` | `202020` | `E7E7E7` | `001D42` | `EB6411` z4,6,10; `E7E7E7` z7 |
| Flint Firebirds | Away | rbk home | `E7E7E7` | `E7E7E7` | `051A2E` | `001D42` | `EB6411` z4,6,10; `E7E7E7` z7 |
| Guelph Storm | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `981B1E` z7 |
| Guelph Storm | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `981B1E` z7 |
| Kingston Frontenacs | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `FFC423` z7 |
| Kingston Frontenacs | Away | rbk home | `E7E7E7` | `202020` | `202020` | `202020` | `FFC423` z7 |
| Kitchener Rangers | Home | rbk away | `0C3361` | `202020` | `E7E7E7` | `13378F` | `E7E7E7` z7,8,9; `CF1531` z10,11 |
| Kitchener Rangers | Away | rbk home | `C41730` | `202020` | `E7E7E7` | `13378F` | `E7E7E7` z7,8,9; `CF1531` z10,11 |
| London Knights | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `119957` z6; `E7E7E7` z7 |
| London Knights | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `119957` z6; `E7E7E7` z7 |
| Niagara IceDogs | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `A62626` z2; `E7E7E7` z7 |
| Niagara IceDogs | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `A62626` z2; `E7E7E7` z7 |
| North Bay Battalion | Home | rbk away | `89811A` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z6 |
| North Bay Battalion | Away | rbk home | `89811A` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z6 |
| Oshawa Generals | Home | rbk away | `051A2E` | `202020` | `E7E7E7` | `051A2E` | `90181C` z6; `E7E7E7` z7 |
| Oshawa Generals | Away | rbk home | `E7E7E7` | `202020` | `202020` | `051A2E` | `90181C` z6; `E7E7E7` z7 |
| Ottawa 67's | Home | rbk away | `0F0F0F` | `E7E7E7` | `E7E7E7` | `202020` | `9A2323` z7 |
| Ottawa 67's | Away | rbk home | `E7E7E7` | `202020` | `202020` | `202020` | `9A2323` z7 |
| Owen Sound Attack | Home | rbk away | `0F0F0F` | `202020` | `E7E7E7` | `202020` | `9A2323` z6; `E7E7E7` z7 |
| Owen Sound Attack | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `9A2323` z6; `E7E7E7` z7 |
| Peterborough Petes | Home | rbk away | `4E1015` | `E7E7E7` | `E7E7E7` | `4E1015` | `E7E7E7` z7 |
| Peterborough Petes | Away | rbk home | `E7E7E7` | `E7E7E7` | `4E1015` | `4E1015` | `E7E7E7` z7 |
| Saginaw Spirit | Home | rbk away | `111621` | `202020` | `E7E7E7` | `111621` | `8F1A0F` z6,8; `E7E7E7` z7 |
| Saginaw Spirit | Away | rbk home | `111621` | `202020` | `E7E7E7` | `111621` | `8F1A0F` z6,8; `E7E7E7` z7 |
| Sarnia Sting | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `FFC423` z6 |
| Sarnia Sting | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `FFC423` z6 |
| Sault Ste. Marie Greyhounds | Home | rbk away | `9F0D1A` | `E7E7E7` | `E7E7E7` | `9F0D1A` | `E7E7E7` z7 |
| Sault Ste. Marie Greyhounds | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `9F0D1A` | `E7E7E7` z7 |
| Sudbury Wolves | Home | rbk away | `1E346E` | `202020` | `E7E7E7` | `1E346E` | `A5ACB0` z6; `E7E7E7` z7 |
| Sudbury Wolves | Away | rbk home | `E7E7E7` | `202020` | `202020` | `1E346E` | `A5ACB0` z6; `E7E7E7` z7 |
| Windsor Spitfires | Home | rbk away | `0B182E` | `202020` | `E7E7E7` | `001528` | `E7E7E7` z7; `7B0E0A` z9,10,11 |
| Windsor Spitfires | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `001528` | `E7E7E7` z7; `7B0E0A` z9,10,11 |

## QMJHL (18)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Baie-Comeau Drakkar | Home | rbk away | `90181C` | `202020` | `E7E7E7` | `90181C` | `FFC423` z4,7; `E7E7E7` z11 |
| Baie-Comeau Drakkar | Away | rbk home | `90181C` | `202020` | `E7E7E7` | `90181C` | `FFC423` z4,7; `E7E7E7` z11 |
| Blainville-Boisbriand Armada | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Blainville-Boisbriand Armada | Away | rbk home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Cape Breton Eagles | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `FFC423` z3,7 |
| Cape Breton Eagles | Away | rbk home | `E6E6E6` | `E6E6E6` | `202020` | `202020` | `FFC423` z3,7 |
| Charlottetown Islanders | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `A8996E` z4,11; `E7E7E7` z7 |
| Charlottetown Islanders | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `A8996E` z4,11; `E7E7E7` z7 |
| Chicoutimi Saguenéens | Home | rbk away | `132035` | `202020` | `E7E7E7` | `162335` | `E7E7E7` z7 |
| Chicoutimi Saguenéens | Away | rbk home | `132035` | `202020` | `E7E7E7` | `162335` | `E7E7E7` z7 |
| Drummondville Voltigeurs | Home | rbk away | `202020` | `202020` | `E7E7E7` | `D92131` | `202020` z6; `E7E7E7` z7 |
| Drummondville Voltigeurs | Away | rbk home | `202020` | `202020` | `E7E7E7` | `D92131` | `202020` z6; `E7E7E7` z7 |
| Gatineau Olympiques | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `999D9D` z6; `E7E7E7` z7 |
| Gatineau Olympiques | Away | rbk home | `E7E7E7` | `E7E7E7` | `0F0F0F` | `202020` | `999D9D` z6; `E7E7E7` z7 |
| Halifax Mooseheads | Home | rbk away | `142929` | `202020` | `E7E7E7` | `142929` | `E51937` z6; `E7E7E7` z7 |
| Halifax Mooseheads | Away | rbk home | `142929` | `202020` | `E7E7E7` | `142929` | `E51937` z6; `E7E7E7` z7 |
| Moncton Wildcats | Home | rbk away | `003896` | `E7E7E7` | `E7E7E7` | `003896` | `90181C` z6,9; `E7E7E7` z7 |
| Moncton Wildcats | Away | rbk home | `003896` | `E7E7E7` | `E7E7E7` | `003896` | `90181C` z6,9; `E7E7E7` z7 |
| Newfoundland Regiment | Home | rbk away | `0D2340` | `E7E7E7` | `E7E7E7` | `0D2340` | `8E2939` z6; `E7E7E7` z7 |
| Newfoundland Regiment | Away | rbk home | `0D2340` | `E7E7E7` | `E7E7E7` | `0D2340` | `8E2939` z6; `E7E7E7` z7 |
| Québec Remparts | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z3,7; `9C1B1F` z9,10,11 |
| Québec Remparts | Away | rbk home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z3,7; `9C1B1F` z9,10,11 |
| Rimouski Océanic | Home | rbk away | `181F33` | `202020` | `E7E7E7` | `181F33` | `E7E7E7` z3,7 |
| Rimouski Océanic | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `181F33` | `E7E7E7` z3,7 |
| Rouyn-Noranda Huskies | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `CC092F` z6; `E7E7E7` z7 |
| Rouyn-Noranda Huskies | Away | rbk home | `202020` | `202020` | `E7E7E7` | `202020` | `CC092F` z6; `E7E7E7` z7 |
| Saint John Sea Dogs | Home | rbk away | `171C8F` | `202020` | `E7E7E7` | `171C8F` | `202020` z5,6; `999D9D` z7 |
| Saint John Sea Dogs | Away | rbk home | `171C8F` | `202020` | `E7E7E7` | `171C8F` | `202020` z5,6; `999D9D` z7 |
| Shawinigan Cataractes | Home | rbk away | `1B1E2F` | `202020` | `E7E7E7` | `1B1E2F` | `FFC423` z7 |
| Shawinigan Cataractes | Away | rbk home | `1B1E2F` | `202020` | `E7E7E7` | `1B1E2F` | `FFC423` z7 |
| Sherbrooke Phoenix | Home | rbk away | `132035` | `202020` | `E7E7E7` | `162335` | `E7E7E7` z6 |
| Sherbrooke Phoenix | Away | rbk home | `132035` | `202020` | `E7E7E7` | `162335` | `E7E7E7` z6 |
| Val-D'Or Foreurs | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `DEB253` z6; `E7E7E7` z7 |
| Val-D'Or Foreurs | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `DEB253` z6; `E7E7E7` z7 |
| Victoriaville Tigres | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `FFC423` z6 |
| Victoriaville Tigres | Away | rbk home | `202020` | `202020` | `E7E7E7` | `202020` | `FFC423` z6 |

## WHL (23)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Brandon Wheat Kings | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Brandon Wheat Kings | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `E7E7E7` z7 |
| Calgary Hitmen | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Calgary Hitmen | Away | rbk home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Edmonton Oil Kings | Home | rbk away | `253B73` | `E7E7E7` | `E7E7E7` | `253B73` | `E7E7E7` z6; `90181C` z11 |
| Edmonton Oil Kings | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `253B73` | `E7E7E7` z6; `90181C` z11 |
| Everett Silvertips | Home | rbk away | `0D3324` | `202020` | `E7E7E7` | `0D3324` | `999D9D` z6 |
| Everett Silvertips | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `0D3324` | `999D9D` z6 |
| Kamloops Blazers | Home | rbk away | `051936` | `202020` | `E7E7E7` | `051936` | `E7E7E7` z7; `F37736` z11 |
| Kamloops Blazers | Away | rbk home | `E7E7E7` | `E7E7E7` | `051936` | `051936` | `E7E7E7` z7; `F37736` z11 |
| Kelowna Rockets | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `006785` z6; `E7E7E7` z7 |
| Kelowna Rockets | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `006785` z6; `E7E7E7` z7 |
| Lethbridge Hurricanes | Home | rbk away | `051A2E` | `051A2E` | `E7E7E7` | `051A2E` | `E7E7E7` z3,7; `BB1C20` z9,10,11 |
| Lethbridge Hurricanes | Away | rbk home | `E7E7E7` | `E7E7E7` | `051A2E` | `051A2E` | `E7E7E7` z3,7; `BB1C20` z9,10,11 |
| Medicine Hat Tigers | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Medicine Hat Tigers | Away | rbk home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Moose Jaw Warriors | Home | rbk away | `202020` | `E7E7E7` | `E7E7E7` | `202020` | `C6231B` z6; `E7E7E7` z7 |
| Moose Jaw Warriors | Away | rbk home | `202020` | `E7E7E7` | `E7E7E7` | `202020` | `C6231B` z6; `E7E7E7` z7 |
| Penticton Vees | Home | rbk away | `162338` | `E7E7E7` | `E7E7E7` | `162338` | `E7E7E7` z7 |
| Penticton Vees | Away | rbk home | `162338` | `E7E7E7` | `E7E7E7` | `162338` | `E7E7E7` z7 |
| Portland Winter Hawks | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Portland Winter Hawks | Away | rbk home | `E7E7E7` | `E7E7E7` | `80A3BF` | `202020` | `E7E7E7` z7 |
| Prince Albert Raiders | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `00853E` z7; `BFB181` z11 |
| Prince Albert Raiders | Away | rbk home | `202020` | `202020` | `E7E7E7` | `202020` | `00853E` z7; `BFB181` z11 |
| Prince George Cougars | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `9C1B1F` z6; `E7E7E7` z7 |
| Prince George Cougars | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `9C1B1F` z6; `E7E7E7` z7 |
| Red Deer Rebels | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Red Deer Rebels | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `E7E7E7` z7 |
| Regina Pats | Home | rbk away | `0B182E` | `E7E7E7` | `E7E7E7` | `0B182E` | `90181C` z6; `E9E9E9` z7 |
| Regina Pats | Away | rbk home | `E7E7E7` | `E7E7E7` | `01254F` | `0B182E` | `90181C` z6; `E9E9E9` z7 |
| Saskatoon Blades | Home | rbk away | `132080` | `E7E7E7` | `E7E7E7` | `132080` | `E7E7E7` z7 |
| Saskatoon Blades | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `132080` | `E7E7E7` z7 |
| Seattle Thunderbirds | Home | rbk away | `191D2E` | `202020` | `E7E7E7` | `191D2E` | `00703C` z6; `E7E7E7` z7 |
| Seattle Thunderbirds | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `191D2E` | `00703C` z6; `E7E7E7` z7 |
| Spokane Chiefs | Home | rbk away | `90181C` | `202020` | `E7E7E7` | `253B73` | `E7E7E7` z3,7; `90181C` z6,9,10,11 |
| Spokane Chiefs | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `253B73` | `E7E7E7` z3,7; `90181C` z6,9,10,11 |
| Swift Current Broncos | Home | rbk away | `061F5C` | `202020` | `E7E7E7` | `061F5C` | `166936` z6; `E7E7E7` z7 |
| Swift Current Broncos | Away | rbk home | `061F5C` | `202020` | `E7E7E7` | `061F5C` | `166936` z6; `E7E7E7` z7 |
| Tri-City Americans | Home | rbk away | `00172D` | `202020` | `E7E7E7` | `00172D` | `8F1A0F` z4,6; `999D9D` z7 |
| Tri-City Americans | Away | rbk home | `E7E7E7` | `E7E7E7` | `00172D` | `00172D` | `8F1A0F` z4,6; `999D9D` z7 |
| Vancouver Giants | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `631219` z6; `E7E7E7` z7 |
| Vancouver Giants | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `631219` z6; `E7E7E7` z7 |
| Victoria Royals | Home | rbk away | `1F1F1F` | `1F1F1F` | `E7E7E7` | `202020` | `114178` z6; `E7E7E7` z7 |
| Victoria Royals | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | — |  |
| Wenatchee Wild | Home | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `213E92` z6; `E7E7E7` z7 |
| Wenatchee Wild | Away | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `213E92` z6; `E7E7E7` z7 |

## SHL (14)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Brynäs IF | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `D22734` z7 |
| Brynäs IF | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `D22734` z7 |
| Djurgården Hockey | Home | rbk home | `202020` | `202020` | `E7E7E7` | `202020` | `FEB101` z4,7 |
| Djurgården Hockey | Away | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `FEB101` z4,7 |
| Frölunda HC | Home | rbk home | `1D2A3C` | `202020` | `E7E7E7` | `1D2A3C` | `FFD519` z7 |
| Frölunda HC | Away | rbk away | `1D2A3C` | `202020` | `E7E7E7` | `1D2A3C` | `FFD519` z7 |
| Färjestad BK | Home | rbk home | `0B182E` | `202020` | `E7E7E7` | `0B182E` | `C41130` z7 |
| Färjestad BK | Away | rbk away | `0B182E` | `202020` | `E7E7E7` | `0B182E` | `C41130` z7 |
| HV71 | Home | nineties home | `0B182E` | `0B182E` | `E7E7E7` | `0B182E` | `E7E7E7` z7 |
| HV71 | Away | nineties away | `0B182E` | `202020` | `E7E7E7` | `0B182E` | `E7E7E7` z7 |
| IF Bjorkloven | Home | nineties home | `18372F` | `E7E7E7` | `E7E7E7` | `18372F` | `E7E7E7` z7 |
| IF Bjorkloven | Away | nineties away | `18372F` | `E7E7E7` | `E7E7E7` | `18372F` | `E7E7E7` z7 |
| Linköping HC | Home | rbk home | `18372F` | `E7E7E7` | `E7E7E7` | `18372F` | `A72929` z6 |
| Linköping HC | Away | rbk away | `18372F` | `E7E7E7` | `E7E7E7` | `18372F` | `A72929` z6 |
| Luleå Hockey | Home | nineties home | `18372F` | `E7E7E7` | `E7E7E7` | `18372F` | `FFD519` z7 |
| Luleå Hockey | Away | nineties away | `18372F` | `E7E7E7` | `E7E7E7` | `18372F` | `FFD519` z7 |
| Malmӧ Redhawks | Home | rbk home | `191D2E` | `202020` | `E7E7E7` | `191D2E` | `920C1D` z7 |
| Malmӧ Redhawks | Away | rbk away | `191D2E` | `202020` | `E7E7E7` | `191D2E` | `920C1D` z7 |
| Orebro Hockey | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `7B0E0A` z6 |
| Orebro Hockey | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `7B0E0A` z6 |
| Rogle BK | Home | nineties home | `0F0F0F` | `202020` | `E7E7E7` | `202020` | `9A1B1F` z4,6; `E7E7E7` z7 |
| Rogle BK | Away | nineties away | `0F0F0F` | `202020` | `E7E7E7` | `202020` | `9A1B1F` z4,6; `E7E7E7` z7 |
| Skelleftea AIK | Home | rbk home | `202020` | `202020` | `E7E7E7` | `202020` | `AF2C27` z7 |
| Skelleftea AIK | Away | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `AF2C27` z7 |
| Timrå IK | Home | nineties home | `044D28` | `E7E7E7` | `E7E7E7` | `044D28` | `E7E7E7` z6 |
| Timrå IK | Away | nineties away | `044D28` | `E7E7E7` | `E7E7E7` | `044D28` | `E7E7E7` z6 |
| Växjö Lakers | Home | rbk home | `202020` | `202020` | `E7E7E7` | `1B1B1B` | `FFD519` z7 |
| Växjö Lakers | Away | rbk away | `E7E7E7` | `E7E7E7` | `202020` | `1B1B1B` | `FFD519` z7 |

## HockeyAllsvenskan (14)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Almtuna IS | Home | rbk home | `202020` | `202020` | `E7E7E7` | `202020` | `9A1B1F` z6; `E7E7E7` z7 |
| Almtuna IS | Away | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `9A1B1F` z6; `E7E7E7` z7 |
| BIK Karlskoga | Home | nineties home | `142C5D` | `E7E7E7` | `E7E7E7` | — |  |
| BIK Karlskoga | Away | nineties away | `142C5D` | `E7E7E7` | `E7E7E7` | `142C5D` | `E7E7E7` z6; `1F58C5` z7 |
| IK Oskarshamn | Home | nineties home | `0B182E` | `1B1B1B` | `E7E7E7` | `191D2E` | `E7E7E7` z6 |
| IK Oskarshamn | Away | nineties away | `0B182E` | `1B1B1B` | `E7E7E7` | `191D2E` | `E7E7E7` z6 |
| Kalmar HC | Home | rbk home | `7B0E0A` | `E7E7E7` | `E7E7E7` | `7B0E0A` | `E7E7E7` z6; `18372F` z7 |
| Kalmar HC | Away | rbk away | `7B0E0A` | `E7E7E7` | `E7E7E7` | `7B0E0A` | `E7E7E7` z6; `18372F` z7 |
| Leksands IF | Home | rbk home | — | — | — | `202020` | `E6DB17` z7 |
| Leksands IF | Away | rbk away | — | — | — | `202020` | `E6DB17` z7 |
| MODO Hockey | Home | nineties home | `8C1220` | `E7E7E7` | `E7E7E7` | `8C1220` | `E7E7E7` z6 |
| MODO Hockey | Away | nineties away | `8C1220` | `E7E7E7` | `E7E7E7` | `8C1220` | `E7E7E7` z6 |
| Mora IK | Home | rbk home | `8C181C` | `E9E9E9` | `E9E9E9` | `8C181C` | `E7E7E7` z7 |
| Mora IK | Away | rbk away | `8C181C` | `E9E9E9` | `E9E9E9` | `8C181C` | `E7E7E7` z7 |
| Ostersunds IK | Home | rbk home | `0B182E` | `202020` | `E7E7E7` | `0B182E` | `FFD519` z7 |
| Ostersunds IK | Away | rbk away | `0B182E` | `202020` | `E7E7E7` | `0B182E` | `FFD519` z7 |
| Södertälje SK | Home | rbk home | `142C5D` | `E7E7E7` | `E7E7E7` | `142C5D` | `E7E7E7` z7; `CF1531` z11 |
| Södertälje SK | Away | rbk away | `142C5D` | `E7E7E7` | `E7E7E7` | `142C5D` | `E7E7E7` z7; `CF1531` z11 |
| Vik Vasteras HK | Home | rbk home | `1C1E33` | `202020` | `E7E7E7` | `1C1E33` | `DDAA1F` z7 |
| Vik Vasteras HK | Away | rbk away | `1C1E33` | `202020` | `E7E7E7` | `1C1E33` | `DDAA1F` z7 |
| Vimmerby Hockey | Home | nineties home | `18372F` | `202020` | `E7E7E7` | `18372F` | `EDBA13` z7 |
| Vimmerby Hockey | Away | nineties away | `18372F` | `202020` | `E7E7E7` | `18372F` | `EDBA13` z7 |
| Visby/Roma HK | Home | nineties home | `9A1B1F` | `E7E7E7` | `E7E7E7` | `9A1B1F` | `E7E7E7` z7 |
| Visby/Roma HK | Away | nineties away | `9A1B1F` | `E7E7E7` | `E7E7E7` | `9A1B1F` | `E7E7E7` z7 |

No helmet or glove colours in the table for: AIK, Nybro Vikings IF.

## Liiga (17)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Helsingin IFK | Home | nineties home | `E7E7E7` | `E7E7E7` | `202020` | `9C1B1F` | `E7E7E7` z7 |
| Helsingin IFK | Away | nineties away | `E7E7E7` | `E7E7E7` | `202020` | `9C1B1F` | `E7E7E7` z7 |
| HPK Hämeenlinna | Home | rbk home | `E7E7E7` | `E7E7E7` | `202020` | `9F0D1A` | `E7E7E7` z7 |
| HPK Hämeenlinna | Away | rbk away | `E7E7E7` | `E7E7E7` | `202020` | `9F0D1A` | `E7E7E7` z7 |
| Jokerit Helsinki | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `FEB101` z4,6 |
| Jokerit Helsinki | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `FEB101` z4,6 |
| JYP Jyväskylä | Home | nineties home | `202020` | `202020` | `E7E7E7` | `1F3D33` | `202020` z2,3; `E8B017` z7 |
| JYP Jyväskylä | Away | nineties away | `202020` | `202020` | `E7E7E7` | `1F3D33` | `202020` z2,3; `E8B017` z7 |
| KalPa Kuopio | Home | nineties home | `001B71` | `E7E7E7` | `E7E7E7` | `001B71` | `7C0E0A` z4; `E5CA02` z6,11; `E7E7E7` z7 |
| KalPa Kuopio | Away | nineties away | `001B71` | `E7E7E7` | `E7E7E7` | `001B71` | `7C0E0A` z4; `E5CA02` z6,11; `E7E7E7` z7 |
| Kiekko-Espoo | Home | nineties home | `253B73` | `E7E7E7` | `E7E7E7` | `0F1C4F` | `C5431D` z6,10; `E7E7E7` z7 |
| Kiekko-Espoo | Away | nineties away | `253B73` | `E7E7E7` | `E7E7E7` | `0F1C4F` | `C5431D` z6,10; `E7E7E7` z7 |
| KooKoo Kouvala | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `FCC726` z6 |
| KooKoo Kouvala | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `FCC726` z6 |
| Mikkelin Jukurit | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `E8E8E8` z7 |
| Mikkelin Jukurit | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `E8E8E8` z7 |
| Oulun Kärpät | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `D22734` z6; `E7E7E7` z7 |
| Oulun Kärpät | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `D22734` z6; `E7E7E7` z7 |
| Pelicans Lahti | Home | rbk home | `202020` | `202020` | `E7E7E7` | — |  |
| Pelicans Lahti | Away | rbk away | `202020` | `202020` | `E7E7E7` | — |  |
| Porin Ässät | Home | nineties home | `9A1A1E` | `202020` | `E7E7E7` | `9A1A1E` | `E7E7E7` z6 |
| Porin Ässät | Away | nineties away | `9A1A1E` | `202020` | `EAE8E0` | `9A1A1E` | `E7E7E7` z6 |
| Rauman Lukko | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `E8B017` z7; `E7E7E7` z10 |
| Rauman Lukko | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `E8B017` z7; `E7E7E7` z10 |
| SaiPa Lappeenranta | Home | nineties home | `142C63` | `202020` | `E7E7E7` | `142C63` | `E8B017` z7 |
| SaiPa Lappeenranta | Away | nineties away | `142C63` | `202020` | `E7E7E7` | `142C63` | `E8B017` z7 |
| Tampereen Ilves | Home | rbk home | `B23337` | `202020` | `E8E8E8` | `0B182E` | `DE353A` z6; `E7E7E7` z7 |
| Tampereen Ilves | Away | rbk away | `B23337` | `202020` | `E8E8E8` | `0B182E` | `DE353A` z6; `E7E7E7` z7 |
| Tampereen Tappara | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Tampereen Tappara | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| TPS Turku | Home | nineties home | `7B0E0A` | `E7E7E7` | `E7E7E7` | `9F0D1A` | `E7E7E7` z6 |
| TPS Turku | Away | nineties away | `7B0E0A` | `E7E7E7` | `E7E7E7` | `9F0D1A` | `E7E7E7` z6 |
| Vaasan Sport | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Vaasan Sport | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |

## DEL (14)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Adler Mannheim | Home | nineties home | `202020` | `202020` | `E7E7E7` | `0B182E`† | `F0BD18` z7,11 |
| Adler Mannheim | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Augsburger Panther | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Augsburger Panther | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| EHC München | Home | nineties home | `E69D22` | `202020` | `E7E7E7` | `202020` | `DA391C` z6 |
| EHC München | Away | nineties away | `E69D22` | `202020` | `E7E7E7` | `202020` | `DA391C` z6 |
| Eisbären Berlin | Home | nineties home | `122452` | `202020` | `E7E7E7` | `18372F` | `E7E7E7` z4,7; `CF1531` z6,10 |
| Eisbären Berlin | Away | nineties away | `122452` | `202020` | `E7E7E7` | `18372F` | `E7E7E7` z4,7; `CF1531` z6,10 |
| ERC Ingolstadt | Home | nineties home | `18372F` | `202020` | `E7E7E7` | `051633` | `E7E7E7` z7 |
| ERC Ingolstadt | Away | nineties away | `18372F` | `202020` | `E7E7E7` | `051633` | `E7E7E7` z7 |
| Fischtown Pinguins | Home | nineties home | `0F0F0F` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Fischtown Pinguins | Away | nineties away | `0F0F0F` | `202020` | `EBDE2F` | `202020` | `E7E7E7` z7 |
| Grizzly Adams Wolfsburg | Home | nineties home | `7B0E0A` | `E7E7E7` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Grizzly Adams Wolfsburg | Away | nineties away | `7B0E0A` | `E7E7E7` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Iserlohn Roosters | Home | nineties home | `E69D22` | `E7E7E7` | `656969` | `202020` | `E7E7E7` z7 |
| Iserlohn Roosters | Away | nineties away | `E69D22` | `E7E7E7` | `656969` | `202020` | `E7E7E7` z7 |
| Krefeld Pinguine | Home | nineties home | `171C3A` | `E7E7E7` | `E7E7E7` | `171C3A` | `D9A416` z4; `E7E7E7` z7 |
| Krefeld Pinguine | Away | nineties away | `171C3A` | `E7E7E7` | `E7E7E7` | `171C3A` | `D9A416` z4; `E7E7E7` z7 |
| Kölner Haie | Home | nineties home | `0D2042` | `021B1B` | `E7E7E7` | `0B182E` | `E7E7E7` z3,7 |
| Kölner Haie | Away | nineties away | `0D2042` | `021B1B` | `E7E7E7` | `0B182E` | `E7E7E7` z3,7 |
| Lowen Frankfurt | Home | nineties home | `222B3A` | `202020` | `E7E7E7` | `222B3A` | `9F0D1A` z7; `E7E7E7` z11 |
| Lowen Frankfurt | Away | nineties away | `222B3A` | `202020` | `E7E7E7` | `222B3A` | `9F0D1A` z7; `E7E7E7` z11 |
| Nurnberg Ice Tigers | Home | nineties home | `E7E7E7` | `E7E7E7` | `202020` | `0B182E` | `CF1531` z6; `E7E7E7` z7 |
| Nurnberg Ice Tigers | Away | nineties away | `E7E7E7` | `E7E7E7` | `202020` | `0B182E` | `CF1531` z6; `E7E7E7` z7 |
| Schwenninger Wild Wings | Home | nineties home | — | — | — | `0B182E` | `E7E7E7` z7 |
| Schwenninger Wild Wings | Away | nineties away | — | — | — | `0B182E` | `E7E7E7` z7 |
| Straubing Tigers | Home | nineties home | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `E7E7E7` z7; `9F0D1A` z11 |
| Straubing Tigers | Away | nineties away | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `E7E7E7` z7; `9F0D1A` z11 |

## Czech Extraliga (14)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Bilí Tygři Liberec | Home | nineties home | `0F0F0F` | `0F0F0F` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Bilí Tygři Liberec | Away | nineties away | `0F0F0F` | `0F0F0F` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| BK Mladá Boleslav | Home | nineties home | `1C2238` | `202020` | `E7E7E7` | `1C2238` | `E7E7E7` z4,7 |
| BK Mladá Boleslav | Away | nineties away | `1C2238` | `202020` | `E7E7E7` | `1C2238` | `E7E7E7` z4,7 |
| CEZ Motor Ceske Budejovice | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| CEZ Motor Ceske Budejovice | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| HC Dynamo Pardubice | Home | nineties home | `9E121B` | `202020` | `E7E7E7` | `9E121B` | `E7E7E7` z7 |
| HC Dynamo Pardubice | Away | nineties away | `9E121B` | `202020` | `E7E7E7` | `9E121B` | `E7E7E7` z7 |
| HC Energie Karlovy Vary | Home | nineties home | `E7E7E7` | `E7E7E7` | `202020` | `192A62` | `E7E7E7` z7 |
| HC Energie Karlovy Vary | Away | nineties away | `E7E7E7` | `E7E7E7` | `202020` | `192A62` | `E7E7E7` z7 |
| HC Kometa Brno | Home | nineties home | `122452` | `1B1B1B` | `E7E7E7` | `122452` | `E7E7E7` z7 |
| HC Kometa Brno | Away | nineties away | `122452` | `1B1B1B` | `E8E8E8` | `122452` | `E7E7E7` z7 |
| HC Oceláři Třinec | Home | nineties home | `0B182E` | `202020` | `E7E7E7` | `1C2238` | `E7E7E7` z7 |
| HC Oceláři Třinec | Away | nineties away | `0B182E` | `E7E7E7` | `E7E7E7` | `1C2238` | `E7E7E7` z7 |
| HC Olomouc | Home | nineties home | `0F0F0F` | `202020` | `E7E7E7` | `155E47` | `202020` z6; `E7E7E7` z7 |
| HC Olomouc | Away | nineties away | `0F0F0F` | `202020` | `E7E7E7` | `155E47` | `202020` z6; `E7E7E7` z7 |
| HC SKODA Plzeň | Home | nineties home | `0F0F0F` | `202020` | `EAE8E0` | `9F0D1A` | `EAE8E0` z7 |
| HC SKODA Plzeň | Away | nineties away | `0F0F0F` | `202020` | `EAE8E0` | `9F0D1A` | `EAE8E0` z7 |
| HC Sparta Praha | Home | nineties home | `E7E7E7` | `EAE8E0` | `202020` | `9F0D1A` | `E7E7E7` z7 |
| HC Sparta Praha | Away | nineties away | `E7E7E7` | `EAE8E0` | `202020` | `9F0D1A` | `E7E7E7` z7 |
| HC VERVA Litvinov | Home | nineties home | `152A64` | `202020` | `E7E7E7` | `152A64` | `E7E7E7` z7 |
| HC VERVA Litvinov | Away | nineties away | `152A64` | `202020` | `E7E7E7` | `152A64` | `E7E7E7` z7 |
| HC Vítkovice Ridera | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| HC Vítkovice Ridera | Away | nineties away | `202020` | `202020` | `E6E6E6` | `202020` | `E7E7E7` z7 |
| Mountfield HK | Home | nineties home | `0F0F0F` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z3,7 |
| Mountfield HK | Away | nineties away | `0F0F0F` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z3,7 |
| Rytíři Kladno | Home | nineties home | `23396E` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Rytíři Kladno | Away | nineties away | `23396E` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |

## Swiss National League (14)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| EHC Biel | Home | nineties home | `061033` | `061033` | `E7E7E7` | `061033` | `E7E7E7` z6,11 |
| EHC Biel | Away | nineties away | `E7E7E7` | `E7E7E7` | `202020` | `061033` | `E7E7E7` z6,11 |
| EV Zug | Home | nineties home | `8C1220` | `E7E7E7` | `E7E7E7` | `961E1E` | `D9B236` z7 |
| EV Zug | Away | nineties away | `8C1220` | `E7E7E7` | `E7E7E7` | `961E1E` | `D9B236` z7 |
| Genève-Servette HC | Home | nineties home | `003087` | `E7E7E7` | `E7E7E7` | `16458D` | `E7E7E7` z7 |
| Genève-Servette HC | Away | nineties away | `E7E7E7` | `E7E7E7` | `202020` | `16458D` | `E7E7E7` z7 |
| HC Ajoie | Home | nineties home | `0B182E` | `202020` | `E7E7E7` | `0B182E` | `E7E7E7` z7 |
| HC Ajoie | Away | nineties away | `0B182E` | `202020` | `E8E8E8` | `0B182E` | `E7E7E7` z7 |
| HC Ambri-Piotta | Home | nineties home | `132080` | `E7E7E7` | `E7E7E7` | `132080` | `E7E7E7` z6 |
| HC Ambri-Piotta | Away | nineties away | `E7E7E7` | `E7E7E7` | `202020` | `132080` | `E7E7E7` z6 |
| HC Davos | Home | nineties home | `0F0F0F` | `202020` | `E7E7E7` | `202020` | `9A1B1F` z7 |
| HC Davos | Away | nineties away | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `9A1B1F` z7 |
| HC Fribourg-Gottéron | Home | nineties home | `8C1220` | `E7E7E7` | `E7E7E7` | `8C1220` | `E7E7E7` z6; `FAE132` z11 |
| HC Fribourg-Gottéron | Away | nineties away | `E7E7E7` | `E7E7E7` | `202020` | `8C1220` | `E7E7E7` z6; `FAE132` z11 |
| HC Lausanne | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| HC Lausanne | Away | nineties away | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `E7E7E7` z7 |
| HC Lugano | Home | nineties home | `401015` | `E8E8E8` | `E8E8E8` | `3E171C` | `D9B236` z7; `E7E7E7` z11 |
| HC Lugano | Away | nineties away | `E7E7E7` | `E7E7E7` | `202020` | `3E171C` | `D9B236` z7; `E7E7E7` z11 |
| Kloten Flyers | Home | nineties home | `9F0D1A` | `E7E7E7` | `E7E7E7` | `9F0D1A` | `E7E7E7` z7 |
| Kloten Flyers | Away | nineties away | `9F0D1A` | `EAE8E0` | `EAE8E0` | `9F0D1A` | `E7E7E7` z7 |
| Rapperswil-Jona Lakers | Home | nineties home | `7B0E0A` | `E7E7E7` | `E7E7E7` | `202020` | `7B0E0A` z7 |
| Rapperswil-Jona Lakers | Away | nineties away | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `7B0E0A` z7 |
| SC Bern | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `F0BD18` z6 |
| SC Bern | Away | nineties away | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `F0BD18` z6 |
| SCL Tigers | Home | nineties home | `202020` | `202020` | `E8E8E8` | `202020` | `E7E7E7` z7 |
| SCL Tigers | Away | nineties away | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `E7E7E7` z7 |
| ZSC Lions | Home | nineties home | `B31323` | `E7E7E7` | `E7E7E7` | `B31323` | `E7E7E7` z7 |
| ZSC Lions | Away | nineties away | `E7E7E7` | `E7E7E7` | `202020` | `B31323` | `E7E7E7` z7 |

## ICE Hockey League (14)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Black Wings Linz | Home | nineties home | `253B73` | `202020` | `E7E7E7` | `90181C` | `E7E7E7` z7 |
| Black Wings Linz | Away | nineties away | `253B73` | `202020` | `E7E7E7` | `90181C` | `E7E7E7` z7 |
| EC iDM Warmepumpen VSV | Home | nineties home | `091445` | `1B1B1B` | `E7E7E7` | `9F0D1A` | `E7E7E7` z7 |
| EC iDM Warmepumpen VSV | Away | nineties away | `091445` | `1B1B1B` | `E7E7E7` | `9F0D1A` | `E7E7E7` z7 |
| EC KAC | Home | nineties home | `E7E7E7` | `202020` | `202020` | `202020` | `DA391C` z7 |
| EC KAC | Away | nineties away | `E7E7E7` | `202020` | `202020` | `202020` | `DA391C` z7 |
| FTC Telecom Budapest | Home | nineties home | `1B1B1B` | `1B1B1B` | `E7E7E7` | `1B1B1B` | `EDBA29` z7 |
| FTC Telecom Budapest | Away | nineties away | `1B1B1B` | `1B1B1B` | `E7E7E7` | `1B1B1B` | `EDBA29` z7 |
| Graz 99ers | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `9F0D1A` z7 |
| Graz 99ers | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `9F0D1A` z7 |
| HC Bolzano Sudtirol Alpheria | Home | nineties home | `0B182E` | `202020` | `E8E8E8` | `0B182E` | `E7E7E7` z5 |
| HC Bolzano Sudtirol Alpheria | Away | nineties away | `E7E7E7` | `202020` | `202020` | `0B182E` | `E7E7E7` z5 |
| HC Falkensteiner Pustertal Wolfe | Home | nineties home | `E7E7E7` | `E7E7E7` | `1B1B1B` | `9F0D1A` | `E7E7E7` z7 |
| HC Falkensteiner Pustertal Wolfe | Away | nineties away | `E7E7E7` | `E7E7E7` | `1B1B1B` | `9F0D1A` | `E7E7E7` z7 |
| HC TWK Innsbruck | Home | nineties home | `0B182E` | `1B1B1B` | `E7E7E7` | `0B182E` | `E7E7E7` z7 |
| HC TWK Innsbruck | Away | nineties away | `E7E7E7` | `1B1B1B` | `0B182E` | `0B182E` | `E7E7E7` z7 |
| Hockey Club Milano | Home | nineties home | `1D2E73` | `E7E7E7` | `E7E7E7` | `1D2E73` | `90181C` z1,3; `E7E7E7` z4,6,11 |
| Hockey Club Milano | Away | nineties away | `E7E7E7` | `E7E7E7` | `202020` | `1D2E73` | `90181C` z1,3; `E7E7E7` z4,6,11 |
| HYDRO Fehervar AV 19 | Home | nineties home | `F28207` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7,11 |
| HYDRO Fehervar AV 19 | Away | nineties away | `F28207` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7,11 |
| Olimpija Ljubljana | Home | nineties home | `1D2E73` | `E7E7E7` | `E7E7E7` | `0B182E` | `7B0E0A` z3,10; `E7E7E7` z7 |
| Olimpija Ljubljana | Away | nineties away | `1D2E73` | `E7E7E7` | `E7E7E7` | `0B182E` | `7B0E0A` z3,10; `E7E7E7` z7 |
| Pioneers Vorarlberg | Home | nineties home | `364A03` | `E7E7E7` | `E7E7E7` | `8C1220` | `E7E7E7` z6 |
| Pioneers Vorarlberg | Away | nineties away | `364A03` | `E7E7E7` | `E7E7E7` | `8C1220` | `E7E7E7` z6 |
| Red Bull Salzburg | Home | nineties home | `202020` | `E7E7E7` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Red Bull Salzburg | Away | nineties away | `202020` | `E7E7E7` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Vienna Capitals | Home | nineties home | `454546` | `202020` | `E7E7E7` | `202020` | `DDAA1F` z7 |
| Vienna Capitals | Away | nineties away | `454546` | `202020` | `E7E7E7` | `202020` | `DDAA1F` z7 |

## Other European clubs (5)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Bordeaux Boxers | Home\* | nineties ten | `0B182E` | `202020` | `E7E7E7` | `0B182E` | `E7E7E7` z7 |
| Bordeaux Boxers | Away\* | nineties eleven | `0B182E` | `202020` | `E7E7E7` | `0B182E` | `E7E7E7` z7 |
| GKS Tychy | Home\* | nineties ten | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| GKS Tychy | Away\* | nineties eleven | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Herning Blue Fox | Home\* | nineties ten | `185D43` | `E7E7E7` | `E7E7E7` | `185D43` | `E7E7E7` z6 |
| Herning Blue Fox | Away\* | nineties eleven | `185D43` | `E7E7E7` | `E7E7E7` | `185D43` | `E7E7E7` z6 |
| HK Nitra | Home\* | nineties ten | `FFD519` | `202020` | `202020` | `142C5D` | `202020` z7 |
| HK Nitra | Away\* | nineties eleven | `FFD519` | `202020` | `202020` | `142C5D` | `202020` z7 |
| Storhamar Hamar | Home\* | nineties ten | `1C1E33` | `202020` | `E7E7E7` | `1C1E33` | `E7E7E7` z7 |
| Storhamar Hamar | Away\* | nineties eleven | `1C1E33` | `202020` | `E7E7E7` | `1C1E33` | `E7E7E7` z7 |

## Spengler Cup (6)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Adler Mannheim - Spengler | Home | nineties home (None) | `0B182E` | `202020` | `E7E7E7` | `0B182E` | `F0BD18` z7,11 |
| Adler Mannheim - Spengler | Away | nineties away (None) | `0B182E` | `202020` | `E7E7E7` | `0B182E` | `F0BD18` z7,11 |
| Frölunda HC - Spengler | Home | nineties home (None) | `202020` | `202020` | `E7E7E7` | `208D49` | `E7E7E7` z7 |
| Frölunda HC - Spengler | Away | nineties away (None) | `202020` | `202020` | `E7E7E7` | `208D49` | `E7E7E7` z7 |
| HC Davos - Spengler | Home | nineties home | `003087` | `E7E7E7` | `FFD100` | `0B182E` | `FFDD00` z6; `E7E7E7` z7 |
| HC Davos - Spengler | Away | nineties away | `003087` | `E7E7E7` | `FFD100` | `0B182E` | `FFDD00` z6; `E7E7E7` z7 |
| SCL Tigers - Spengler | Home | nineties home (None) | `002469` | `E7E7E7` | `E7E7E7` | `002469` | `B12726` z6; `E7E7E7` z7 |
| SCL Tigers - Spengler | Away | nineties away (None) | `002469` | `E7E7E7` | `E7E7E7` | `002469` | `B12726` z6; `E7E7E7` z7 |
| Tampereen Ilves - Spengler | Home | nineties home (None) | `202020` | `E7E7E7` | `E7E7E7` | `202002` | `90181C` z6; `E7E7E7` z7 |
| Tampereen Ilves - Spengler | Away | nineties away (None) | `202020` | `E7E7E7` | `E7E7E7` | `202002` | `90181C` z6; `E7E7E7` z7 |
| US College Selects - Spengler | Home | nineties home | `B23337` | `E7E7E7` | `E7E7E7` | `0B182E` | `B12726` z6; `E7E7E7` z7 |
| US College Selects - Spengler | Away | nineties away | `B23337` | `E7E7E7` | `E7E7E7` | `0B182E` | `B12726` z6; `E7E7E7` z7 |

## International (24)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Austria | Home | nineties home | `BF221A` | `202020` | `E7E7E7` | `9A1B1F` | `E7E7E7` z7 |
| Austria | Away | nineties away | `BF221A` | `202020` | `E7E7E7` | `9A1B1F` | `E7E7E7` z7 |
| Belarus | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Belarus | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Canada | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Canada | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Czech Republic | Home | nineties home | `661414` | `E7E7E7` | `E7E7E7` | `661414` | `E4E2B9` z6 |
| Czech Republic | Away | nineties away | `661414` | `E7E7E7` | `E7E7E7` | `661414` | `E4E2B9` z6 |
| Denmark | Home | nineties home | `082C49` | `E7E7E7` | `E7E7E7` | `082C49` | `E7E7E7` z6 |
| Denmark | Away | nineties away | `082C49` | `E7E7E7` | `E7E7E7` | `082C49` | `E7E7E7` z6 |
| Finland | Home | nineties home | `202020` | `E7E7E7` | `E7E7E7` | `202020` | `E7E7E7` z6 |
| Finland | Away | nineties away | `202020` | `E7E7E7` | `E7E7E7` | `202020` | `E7E7E7` z6 |
| France | Home | nineties home | `1B1B1B` | `E7E7E7` | `E7E7E7` | `1B1B1B` | `E7E7E7` z7 |
| France | Away | nineties away | `404040` | `E7E7E7` | `E7E7E7` | `404040` | `E7E7E7` z7 |
| Germany | Home | nineties home | `29528B`† | `202020`† | `E7E7E7`† | `234C86`† | `E7E7E7` z7 |
| Germany | Away | nineties away | `29528B`† | `202020`† | `E7E7E7`† | `234C86`† | `E7E7E7` z7 |
| Great Britain | Home | nineties home | `4B1419`† | `202020`† | `E7E7E7`† | `4B1419`† | `E7E7E7` z3,7 |
| Great Britain | Away | nineties away | `4B1419`† | `202020`† | `E7E7E7`† | `4B1419`† | `E7E7E7` z3,7 |
| Hungary | Home | nineties home | `4B1419` | `202020` | `E7E7E7` | `4B1419` | `E7E7E7` z3,7 |
| Hungary | Away | nineties away | `4B1419` | `202020` | `E7E7E7` | `4B1419` | `E7E7E7` z3,7 |
| Italy | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Italy | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Japan | Home | nineties home | `7A0E1F` | `202020` | `E7E7E7` | `7A0E1F` | `E7E7E7` z7 |
| Japan | Away | nineties away | `7A0E1F` | `202020` | `E7E7E7` | `7A0E1F` | `E7E7E7` z7 |
| Kazakstan | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `871619` z6; `E7E7E7` z7 |
| Kazakstan | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `871619` z6; `E7E7E7` z7 |
| Latvia | Home | nineties home | `23396E` | `202020` | `E7E7E7` | `253B73` | `E7E7E7` z7 |
| Latvia | Away | nineties away | `23396E` | `202020` | `E7E7E7` | `253B73` | `E7E7E7` z7 |
| Norway | Home | nineties home | `861519` | `E7E7E7` | `E7E7E7` | `861519` | `E7E7E7` z7 |
| Norway | Away | nineties away | `861519` | `E7E7E7` | `E7E7E7` | `861519` | `E7E7E7` z7 |
| Poland | Home | nineties home | `0B182E` | `E7E7E7` | `E7E7E7` | `0B182E` | `E7E7E7` z6 |
| Poland | Away | nineties away | `0B182E` | `E7E7E7` | `E7E7E7` | `0B182E` | `E7E7E7` z6 |
| Russia | Home | nineties home | `E7E7E7` | `EAE8E0` | `21356A` | `0B182E` | `90181C` z2,8; `E7E7E7` z3,7,9 |
| Russia | Away | nineties away | `E7E7E7` | `E7E7E7` | `21356A` | `0B182E` | `90181C` z2,8; `E7E7E7` z3,7,9 |
| Slovakia | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Slovakia | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Slovenia | Home | nineties home | `E7E7E7` | `EAE8E0` | `202020` | `253B73` | `E7E7E7` z7; `90181C` z8,9 |
| Slovenia | Away | nineties away | `E7E7E7` | `EAE8E0` | `202020` | `253B73` | `E7E7E7` z7; `90181C` z8,9 |
| South Korea | Home | nineties home | `89151A` | `EAE8E0` | `202020` | `89151A` | `E7E7E7` z3,7 |
| South Korea | Away | nineties away | `89151A` | `202020` | `E7E7E7` | `89151A` | `E7E7E7` z3,7 |
| Sweden | Home | nineties home | `24386C` | `EAE8E0` | `E7E7E7` | `0B182E` | `E7E7E7` z3,7 |
| Sweden | Away | nineties away | `24386C` | `E7E7E7` | `E7E7E7` | `0B182E` | `E7E7E7` z3,7 |
| Switzerland | Home | nineties home | `2B458B` | `202020` | `E7E7E7` | `122854` | `E7E7E7` z7 |
| Switzerland | Away | nineties away | `2B458B` | `202020` | `E7E7E7` | `122854` | `E7E7E7` z7 |
| Ukraine | Home | nineties home | `0F0F0F` | `202020` | `E7E7E7` | `202020` | `9F0D1A` z3; `E7E7E7` z7 |
| Ukraine | Away | nineties away | `0F0F0F` | `202020` | `E7E7E7` | `202020` | `9F0D1A` z3; `E7E7E7` z7 |
| USA | Home | nineties home | `182269` | `E7E7E7` | `E7E7E7` | `1C234D` | `E7E7E7` z6 |
| USA | Away | nineties away | `182269` | `E7E7E7` | `E7E7E7` | `1C234D` | `E7E7E7` z6 |

## Men's Juniors (10)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Canada - Mens Juniors | Home | nineties twenty | `15419A` | `E7E7E7` | `E7E7E7` | `2A3A6A` | `E7E7E7` z7 |
| Canada - Mens Juniors | Away | nineties twentyone | `15419A` | `E7E7E7` | `E7E7E7` | `2A3A6A` | `E7E7E7` z7 |
| Czech Republic - Mens Juniors | Home | nineties twenty | `21356C` | `202020` | `E7E7E7` | `21356C` | `F0BD18` z7 |
| Czech Republic - Mens Juniors | Away | nineties twentyone | `21356C` | `202020` | `E7E7E7` | `21356C` | `F0BD18` z7 |
| Denmark - Mens Juniors | Home | nineties home | `8E172A` | `202020` | `E7E7E7` | `CF1531` | `E7E7E7` z7 |
| Denmark - Mens Juniors | Away | nineties away | `8E172A` | `202020` | `E7E7E7` | `CF1531` | `E7E7E7` z7 |
| Finland - Mens Juniors | Home | nineties twenty | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `851228` z6; `E7E7E7` z7 |
| Finland - Mens Juniors | Away | nineties twentyone | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `851228` z6; `E7E7E7` z7 |
| Germany - Mens Juniors | Home | nineties twenty | `29528B` | `202020` | `E7E7E7` | `234C86` | `E7E7E7` z7 |
| Germany - Mens Juniors | Away | nineties twentyone | `29528B` | `202020` | `E7E7E7` | `234C86` | `E7E7E7` z7 |
| Latvia - Mens Juniors | Home | nineties twenty | `E7E7E7` | `202020` | `202020` | `162338` | `9C1B1F` z4,8,9; `E7E7E7` z7 |
| Latvia - Mens Juniors | Away | nineties twentyone | `E7E7E7` | `202020` | `202020` | `162338` | `9C1B1F` z4,8,9; `E7E7E7` z7 |
| Slovakia - Mens Juniors | Home | nineties twenty | `202020` | `202020` | `E7E7E7` | `9F0D1A` | `E7E7E7` z7 |
| Slovakia - Mens Juniors | Away | nineties twentyone | `202020` | `202020` | `E7E7E7` | `9F0D1A` | `E7E7E7` z7 |
| Sweden - Mens Juniors | Home | nineties twenty | `E7E7E7` | `E7E7E7` | `0B182E` | `052E6C` | `45AC01` z7 |
| Sweden - Mens Juniors | Away | nineties twentyone | `E7E7E7` | `E7E7E7` | `0B182E` | `052E6C` | `45AC01` z7 |
| Switzerland - Mens Juniors | Home | nineties twenty | `253B73` | `202020` | `E7E7E7` | `253B73` | `9F0D1A` z6,8,9; `E7E7E7` z7 |
| Switzerland - Mens Juniors | Away | nineties twentyone | `253B73` | `202020` | `E7E7E7` | `253B73` | `9F0D1A` z6,8,9; `E7E7E7` z7 |
| USA - Mens Juniors | Home | nineties twenty | `861519` | `E7E7E7` | `E7E7E7` | `861519` | `E7E7E7` z7 |
| USA - Mens Juniors | Away | nineties twentyone | `861519` | `E7E7E7` | `E7E7E7` | `861519` | `E7E7E7` z7 |

## Men's Worlds (16)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Austria - Mens Worlds | Home | nineties home | `23396E` | `202020` | `E7E7E7` | `253B73` | `E7E7E7` z7 |
| Austria - Mens Worlds | Away | nineties away | `23396E` | `202020` | `E7E7E7` | `253B73` | `E7E7E7` z7 |
| Canada - Mens Worlds | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `871619` z6; `E7E7E7` z7 |
| Canada - Mens Worlds | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `871619` z6; `E7E7E7` z7 |
| Czech Republic - Mens Worlds | Home | nineties home | `0B182E` | `E7E7E7` | `E7E7E7` | `0B182E` | `E7E7E7` z6 |
| Czech Republic - Mens Worlds | Away | nineties away | `0B182E` | `E7E7E7` | `E7E7E7` | `0B182E` | `E7E7E7` z6 |
| Denmark - Mens Worlds | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Denmark - Mens Worlds | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Finland - Mens Worlds | Home | nineties home | `E7E7E7` | `202020` | `202020` | `162338` | `9C1B1F` z4,8,9; `E7E7E7` z7 |
| Finland - Mens Worlds | Away | nineties away | `E7E7E7` | `202020` | `202020` | `162338` | `9C1B1F` z4,8,9; `E7E7E7` z7 |
| Germany - Mens Worlds | Home | nineties home | `15419A` | `E7E7E7` | `E7E7E7` | `2A3A6A` | `E7E7E7` z7 |
| Germany - Mens Worlds | Away | nineties away | `15419A` | `E7E7E7` | `E7E7E7` | `2A3A6A` | `E7E7E7` z7 |
| Great Britain - Mens Worlds | Home | nineties home | `4B1419` | `202020` | `E7E7E7` | `4B1419` | `E7E7E7` z3,7 |
| Great Britain - Mens Worlds | Away | nineties away | `4B1419` | `202020` | `E7E7E7` | `4B1419` | `E7E7E7` z3,7 |
| Hungary - Mens Worlds | Home | nineties home | `21356C` | `202020` | `E7E7E7` | `21356C` | `F0BD18` z7 |
| Hungary - Mens Worlds | Away | nineties away | `21356C` | `202020` | `E7E7E7` | `21356C` | `F0BD18` z7 |
| Italy - Mens Worlds | Home | nineties home | `23396E` | `202020` | `E7E7E7` | `253B73` | `E7E7E7` z7 |
| Italy - Mens Worlds | Away | nineties away | `23396E` | `202020` | `E7E7E7` | `253B73` | `E7E7E7` z7 |
| Latvia - Mens Worlds | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `871619` z6; `E7E7E7` z7 |
| Latvia - Mens Worlds | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `871619` z6; `E7E7E7` z7 |
| Norway - Mens Worlds | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Norway - Mens Worlds | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Slovakia - Mens Worlds | Home | nineties home | `24386C` | `EAE8E0` | `E7E7E7` | `0B182E` | `E7E7E7` z3,7 |
| Slovakia - Mens Worlds | Away | nineties away | `24386C` | `E7E7E7` | `E7E7E7` | `0B182E` | `E7E7E7` z3,7 |
| Slovenia - Mens Worlds | Home | nineties home | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `851228` z6; `E7E7E7` z7 |
| Slovenia - Mens Worlds | Away | nineties away | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `851228` z6; `E7E7E7` z7 |
| Sweden - Mens Worlds | Home | nineties home | `861519` | `E7E7E7` | `E7E7E7` | `861519` | `E7E7E7` z7 |
| Sweden - Mens Worlds | Away | nineties away | `861519` | `E7E7E7` | `E7E7E7` | `861519` | `E7E7E7` z7 |
| Switzerland - Mens Worlds | Home | nineties home | `202020` | `202020` | `E7E7E7` | `9F0D1A` | `E7E7E7` z7 |
| Switzerland - Mens Worlds | Away | nineties away | `202020` | `202020` | `E7E7E7` | `9F0D1A` | `E7E7E7` z7 |
| USA - Mens Worlds | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| USA - Mens Worlds | Away | nineties away | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |

## Women's Worlds (14)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Austria - Womens Worlds | Home | nineties ten | `4B1419` | `202020` | `E7E7E7` | `4B1419` | `E7E7E7` z3,7 |
| Austria - Womens Worlds | Away | nineties eleven | `4B1419` | `202020` | `E7E7E7` | `4B1419` | `E7E7E7` z3,7 |
| Canada - Womens Worlds | Home | nineties ten | `E7E7E7` | `E7E7E7` | `0B182E` | `052E6C` | `45AC01` z7 |
| Canada - Womens Worlds | Away | nineties eleven | `E7E7E7` | `E7E7E7` | `0B182E` | `052E6C` | `45AC01` z7 |
| China - Womens Worlds | Home | nineties ten | `23396E` | `202020` | `E7E7E7` | `253B73` | `E7E7E7` z7 |
| China - Womens Worlds | Away | nineties eleven | `23396E` | `202020` | `E7E7E7` | `253B73` | `E7E7E7` z7 |
| Czech Republic - Womens Worlds | Home | nineties ten | `0B182E` | `E7E7E7` | `E7E7E7` | `0B182E` | `E7E7E7` z6 |
| Czech Republic - Womens Worlds | Away | nineties eleven | `0B182E` | `E7E7E7` | `E7E7E7` | `0B182E` | `E7E7E7` z6 |
| Denmark - Womens Worlds | Home | nineties ten | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Denmark - Womens Worlds | Away | nineties eleven | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Finland - Womens Worlds | Home | nineties ten | `E7E7E7` | `EAE8E0` | `202020` | `253B73` | `E7E7E7` z7; `90181C` z8,9 |
| Finland - Womens Worlds | Away | nineties eleven | `E7E7E7` | `EAE8E0` | `202020` | `253B73` | `E7E7E7` z7; `90181C` z8,9 |
| France - Womens Worlds | Home | nineties ten | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| France - Womens Worlds | Away | nineties eleven | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Germany - Womens Worlds | Home | nineties ten | `2B458B` | `202020` | `E7E7E7` | `122854` | `E7E7E7` z7 |
| Germany - Womens Worlds | Away | nineties eleven | `2B458B` | `202020` | `E7E7E7` | `122854` | `E7E7E7` z7 |
| Hungary - Womens Worlds | Home | nineties ten | `21356C` | `202020` | `E7E7E7` | `21356C` | `F0BD18` z7 |
| Hungary - Womens Worlds | Away | nineties eleven | `21356C` | `202020` | `E7E7E7` | `21356C` | `F0BD18` z7 |
| Japan - Womens Worlds | Home | nineties ten | `15419A` | `E7E7E7` | `E7E7E7` | `2A3A6A` | `E7E7E7` z7 |
| Japan - Womens Worlds | Away | nineties eleven | `15419A` | `E7E7E7` | `E7E7E7` | `2A3A6A` | `E7E7E7` z7 |
| Norway - Womens Worlds | Home | nineties home | `0B182E` | `E7E7E7` | `E7E7E7` | `0B182E` | `E7E7E7` z6 |
| Norway - Womens Worlds | Away | nineties away | `0B182E` | `E7E7E7` | `E7E7E7` | `0B182E` | `E7E7E7` z6 |
| Sweden - Womens Worlds | Home | nineties ten | `E7E7E7` | `202020` | `202020` | `162338` | `9C1B1F` z4,8,9; `E7E7E7` z7 |
| Sweden - Womens Worlds | Away | nineties eleven | `E7E7E7` | `202020` | `202020` | `162338` | `9C1B1F` z4,8,9; `E7E7E7` z7 |
| Switzerland - Womens Worlds | Home | nineties ten | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `851228` z6; `E7E7E7` z7 |
| Switzerland - Womens Worlds | Away | nineties eleven | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `851228` z6; `E7E7E7` z7 |
| USA - Womens Worlds | Home | nineties ten | `202020` | `202020` | `E7E7E7` | `202020` | `871619` z6; `E7E7E7` z7 |
| USA - Womens Worlds | Away | nineties eleven | `202020` | `202020` | `E7E7E7` | `202020` | `871619` z6; `E7E7E7` z7 |

## NHL Alumni (33)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Anaheim Ducks Alumni | Home | nineties home | `202020` | `202020` | `E7E7E7` | `1F3D33` | `202020` z2,3; `E8B017` z7 |
| Anaheim Ducks Alumni | Away | nineties away | `202020` | `202020` | `E8E8E8` | `1F3D33` | `202020` z2,3; `E8B017` z7 |
| Boston Bruins Alumni | Home | nineties home | `202020`† | `202020`† | `E7E7E7`† | `202020`† | `E7E7E7` z7 |
| Boston Bruins Alumni | Away | nineties away | `E7E7E7`† | `EAE8E0`† | `202020`† | `202020`† | `E7E7E7` z7 |
| Buffalo Sabres Alumni | Home | nineties home | `091524`† | `202020`† | `E7E7E7`† | `151726`† | `FDBD41` z6 |
| Buffalo Sabres Alumni | Away | nineties away | `E7E7E7`† | `EAE8E0`† | `091524`† | `151726`† | `FDBD41` z6 |
| Calgary Flames Alumni | Home | nineties home | `202020`† | `202020`† | `E7E7E7`† | `202020`† | `E7E7E7` z7 |
| Calgary Flames Alumni | Away | nineties away | `E7E7E7`† | `EAE8E0`† | `202020`† | `202020`† | `E7E7E7` z7 |
| Carolina Hurricanes Alumni | Home | nineties home | `BF221A`† | `202020`† | `E7E7E7`† | `9A1B1F`† | `E7E7E7` z7 |
| Carolina Hurricanes Alumni | Away | nineties away | `E7E7E7`† | `EAE8E0`† | `202020`† | `9A1B1F`† | `E7E7E7` z7 |
| Chicago Blackhawks Alumni | Home | nineties home | `202020`† | `202020`† | `E7E7E7`† | `202020`† | `A00106` z3,4; `E7E7E7` z7 |
| Chicago Blackhawks Alumni | Away | nineties away | `E7E7E7`† | `E7E7E7`† | `202020`† | `202020`† | `A00106` z3,4; `E7E7E7` z7 |
| Colorado Avalanche Alumni | Home | nineties home | `1B1B1B` | `1B1B1B` | `E7E7E7` | `1B1B1B` | `FC5F1A` z7 |
| Colorado Avalanche Alumni | Away | nineties away | `E9E9E9` | `1B1B1B` | `1B1B1B` | `1B1B1B` | `FC5F1A` z7 |
| Columbus Blue Jackets Alumni | Home | nineties home | `3B3B42` | `1B1B1B` | `DEB253` | `202020` | `DEB253` z3,6 |
| Columbus Blue Jackets Alumni | Away | nineties away | `E7E7E7` | `E7E7E7` | `3B3B42` | `E7E7E7` | `DEB253` z3,6; `202020` z7,11 |
| Dallas Stars Alumni | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Dallas Stars Alumni | Away | nineties away | `E7E7E7` | `EAE8E0` | `202020` | `202020` | `E7E7E7` z7 |
| Detroit Red Wings Alumni | Home | nineties home | `091524` | `202020` | `E7E7E7` | `151726` | `FDBD41` z6 |
| Detroit Red Wings Alumni | Away | nineties away | `E7E7E7` | `EAE8E0` | `091524` | `151726` | `FDBD41` z6 |
| Edmonton Oilers Alumni | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Edmonton Oilers Alumni | Away | nineties away | `E7E7E7` | `EAE8E0` | `202020` | `202020` | `E7E7E7` z7 |
| Florida Panthers Alumni | Home | nineties home | `BF221A` | `202020` | `E7E7E7` | `9A1B1F` | `E7E7E7` z7 |
| Florida Panthers Alumni | Away | nineties away | `E7E7E7` | `EAE8E0` | `202020` | `9A1B1F` | `E7E7E7` z7 |
| Hartford Whalers Alumni | Home | nineties four (Carolina Hurricanes) | `153D83` | `202020` | `E7E7E7` | `153D83` | `E7E7E7` z4,7,8,9; `D8A924` z6 |
| Hartford Whalers Alumni | Away | nineties three (Carolina Hurricanes) | `E7E7E7` | `EAE8E0` | `202020` | `153D83` | `E7E7E7` z4,7,8,9; `D8A924` z6 |
| Los Angeles Kings Alumni | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `A00106` z3,4; `E7E7E7` z7 |
| Los Angeles Kings Alumni | Away | nineties away | `E7E7E7` | `E7E7E7` | `202020` | `202020` | `A00106` z3,4; `E7E7E7` z7 |
| Minnesota North Stars Alumni | Home | nineties four (Dallas Stars) | `E7E7E7` | `E7E7E7` | `000000` | `202020` | `0E555E` z7; `E7E7E7` z11 |
| Minnesota North Stars Alumni | Away | nineties three (Dallas Stars) | `202020` | `202020` | `E7E7E7` | `202020` | `0E555E` z7; `E7E7E7` z11 |
| Minnesota Wild Alumni | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Minnesota Wild Alumni | Away | nineties away | `E7E7E7` | `EAE8E0` | `202020` | `202020` | `E7E7E7` z7 |
| Montreal Canadiens Alumni | Home | nineties home | `132035` | `202020` | `E7E7E7` | `253B73`† | `90181C` z2,6,8; `E7E7E7` z3,4,7 |
| Montreal Canadiens Alumni | Away | nineties away | `E7E7E7` | `EAE8E0` | `162338` | `253B73`† | `90181C` z2,6,8; `E7E7E7` z3,4,7 |
| Nashville Predators Alumni | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Nashville Predators Alumni | Away | nineties away | `E7E7E7` | `EAE8E0` | `202020` | `202020` | `E7E7E7` z7 |
| New Jersey Devils Alumni | Home | nineties home | `9F0D1A` | `E7E7E7` | `E7E7E7` | `9F0D1A` | `E7E7E7` z7 |
| New Jersey Devils Alumni | Away | nineties away | `E7E7E7` | `EAE8E0` | `202020` | `9F0D1A` | `E7E7E7` z7 |
| New York Islanders Alumni | Home | nineties home | `132032` | `132032` | `E7E7E7` | `132032` | `E7E7E7` z7 |
| New York Islanders Alumni | Away | nineties away | `E7E7E7` | `EAE8E0` | `132032` | `132032` | `E7E7E7` z7 |
| New York Rangers Alumni | Home | nineties home | `212B44` | `202020` | `E7E7E7` | `1A1E2C` | `E7E7E7` z7 |
| New York Rangers Alumni | Away | nineties away | `E7E7E7` | `EAE8E0` | `212B44` | `1A1E2C` | `E7E7E7` z7 |
| Ottawa Senators Alumni | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Ottawa Senators Alumni | Away | nineties away | `E7E7E7` | `EAE8E0` | `202020` | `202020` | `E7E7E7` z7 |
| Philadelphia Flyers Alumni | Home | nineties home | `18372F` | `202020` | `E7E7E7` | `18372F` | `7C1A15` z4,6; `CCB99C` z7 |
| Philadelphia Flyers Alumni | Away | nineties away | `E7E7E7` | `EAE8E0` | `202020` | `18372F` | `7C1A15` z4,6; `CCB99C` z7 |
| Pittsburgh Penguins Alumni | Home | nineties home | `253B73` | `202020` | `E7E7E7` | `253B73` | `90181C` z2,6,8; `E7E7E7` z3,4,7 |
| Pittsburgh Penguins Alumni | Away | nineties away | `E7E7E7` | `EAE8E0` | `202020` | `253B73` | `90181C` z2,6,8; `E7E7E7` z3,4,7 |
| Quebec Nordiques Alumni | Home | nineties four (Colorado Avalanche) | `E7E7E7` | `E7E7E7` | `003E7E` | `153561` | `E7E7E7` z6; `202020` z7 |
| Quebec Nordiques Alumni | Away | nineties three (Colorado Avalanche) | `122E4D` | `1B1B1B` | `E7E7E7` | `122E4D` | `E7E7E7` z6; `202020` z7 |
| San Jose Sharks Alumni | Home | nineties home | `202020` | `202020` | `E7E7E7` | `922328` | `202020` z2,4,6,9; `E7E7E7` z3,7,8 |
| San Jose Sharks Alumni | Away | nineties away | `E7E7E7` | `E7E7E7` | `202020` | `922328` | `202020` z2,4,6,9; `E7E7E7` z3,7,8 |
| St. Louis Blues Alumni | Home | nineties home | `18223A` | `202020` | `E7E7E7` | `1C1F2F` | `EDB118` z6,9 |
| St. Louis Blues Alumni | Away | nineties away | `E7E7E7` | `EAE8E0` | `202020` | `1C1F2F` | `EDB118` z6,9 |
| Tampa Bay Lightning Alumni | Home | nineties home | `1D2A3C` | `202020` | `E7E7E7` | `0F1C4F` | `C5431D` z3,6; `E7E7E7` z7 |
| Tampa Bay Lightning Alumni | Away | nineties away | `E7E7E7` | `EAE8E0` | `1D2A3C` | `0F1C4F` | `C5431D` z3,6; `E7E7E7` z7 |
| Toronto Maple Leafs Alumni | Home | nineties home | `254C94` | `202020` | `E7E7E7` | `CF1531` | `0C3361` z2,4,5,7; `E7E7E7` z3,6,9 |
| Toronto Maple Leafs Alumni | Away | nineties away | `E7E7E7` | `EAE8E0` | `202020` | `CF1531` | `0C3361` z2,4,5,7; `E7E7E7` z3,6,9 |
| Vancouver Canucks Alumni | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `9A2323` z6; `E7E7E7` z7 |
| Vancouver Canucks Alumni | Away | nineties away | `E7E7E7` | `EAE8E0` | `202020` | `202020` | `9A2323` z6; `E7E7E7` z7 |
| Vegas Golden Knights Alumni | Home | adidas home | `202020` | `202020` | `E7E7E7` | `961E1E` | `D9B236` z7 |
| Vegas Golden Knights Alumni | Away | adidas away | `202020` | `202020` | `E7E7E7` | `961E1E` | `D9B236` z7 |
| Washington Capitals Alumni | Home | nineties home | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Washington Capitals Alumni | Away | nineties away | `E7E7E7` | `EAE8E0` | `202020` | `202020` | `E7E7E7` z7 |
| Winnipeg Jets Alumni | Home | nineties four | `202020` | `202020` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Winnipeg Jets Alumni | Away | nineties three | `E7E7E7` | `EAE8E0` | `202020` | `202020` | `E7E7E7` z7 |

## All-Time teams (4)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| All-Time All-Stars | Home | adidas home | `051A2E` | `051A2E` | `BB1C20` | `BA1C20` | `071845` z2,3,4,5,7,11; `E7E7E7` z6 |
| All-Time All-Stars | Away | adidas away | `E7E7E7` | `EAE8E0` | `BB1C20` | `BA1C20` | `071845` z2,3,4,5,7,11; `E7E7E7` z6 |
| All-Time Eastern Conference | Home | adidas home | `102962` | `E7E7E7` | `E7E7E7` | `052E6C` | `E7E7E7` z6 |
| All-Time Eastern Conference | Away | adidas away | `E7E7E7` | `EAE8E0` | `202020` | `052E6C` | `E7E7E7` z6 |
| All-Time Grit | Home | adidas home | `14192C` | `1B1B1B` | `E7E7E7` | `14192C` | `2655B7` z6; `E7E7E7` z7 |
| All-Time Grit | Away | adidas away | `14192C` | `1B1B1B` | `E7E7E7` | `14192C` | `2655B7` z6; `E7E7E7` z7 |
| All-Time Western Conference | Home | adidas home | `06386F` | `202020` | `E7E7E7` | `122854` | `E7E7E7` z7 |
| All-Time Western Conference | Away | adidas away | `E7E7E7` | `E7E7E7` | `06386F` | `122854` | `E7E7E7` z7 |

## Mascots (2)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Mascots East | Home | rbk home | `202020` | `202020` | `E7E7E7` | `202020` | `A6192E` z6; `FFB81C` z7 |
| Mascots East | Away | rbk away | `202020` | `202020` | `E7E7E7` | `202020` | `A6192E` z6; `FFB81C` z7 |
| Mascots West | Home | rbk home | `0E1D38` | `202020` | `E7E7E7` | `0E1D38` | `0067B9` z6; `FFBF1C` z7 |
| Mascots West | Away | rbk away | `0E1D38` | `202020` | `E7E7E7` | `0E1D38` | `0067B9` z6; `FFBF1C` z7 |

## World of CHEL (26)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Hockey Canada Practice | Home | nineties home | `202020`† | `202020`† | `E7E7E7`† | `202020`† | `E7E7E7` z7 |
| Hockey Canada Practice | Away | nineties away | `202020`† | `202020`† | `E7E7E7`† | `202020`† | `E7E7E7` z7 |
| NHL HUT GWC | Home | rbk away | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7` z7 |
| NHL HUT GWC | Away | rbk home | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `1B1B1B`† | `E7E7E7` z7 |
| World of CHEL Tier 1 | Home | adidas home (EASHL) | `0A5258` | `E7E7E7` | `EEE9D8` | `0E5258` | `EEE9D8` z7 |
| World of CHEL Tier 1 | Away | adidas away (EASHL) | `0A5258` | `E7E7E7` | `EEE9D8` | `0E5258` | `EEE9D8` z7 |
| World of CHEL Tier 2 | Home | adidas ten (Canada) | `173F35` | `E7E7E7` | `E7E7E7` | `173F35` | `B5E3D8` z7 |
| World of CHEL Tier 2 | Away | adidas ten (Czech Republic) | `173F35` | `E7E7E7` | `E7E7E7` | `173F35` | `B5E3D8` z7 |
| World of CHEL Tier 3 | Home | nineties home (Anaheim Ducks) | `202020` | `E7E7E7` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| World of CHEL Tier 3 | Away | nineties away (Anaheim Ducks) | `202020` | `E7E7E7` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| World of CHEL Tier 3 | Home | nineties home (New Jersey Devils) | `202020` | `E7E7E7` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| World of CHEL Tier 3 | Away | nineties away (New Jersey Devils) | `202020` | `E7E7E7` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| World of CHEL Tier 4 | Home | nineties home (Anaheim Ducks) | `535816` | `E7E7E7` | `E7E7E7` | `535816` | `E7E7E7` z7 |
| World of CHEL Tier 4 | Away | nineties away (Anaheim Ducks) | `535816` | `E7E7E7` | `E7E7E7` | `535816` | `E7E7E7` z7 |

No helmet or glove colours in the table for: Aces (×17), EAS, EASHL, NHL 33.

## EASHL (2)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Fridge Raiders | Home | nineties home | `10457F` | `E7E7E7` | `EEE9D8` | `10457F` | `EEE9D8` z7 |
| Fridge Raiders | Away | nineties away | `10457F` | `E7E7E7` | `EEE9D8` | `10457F` | `EEE9D8` z7 |

No helmet or glove colours in the table for: EASHL Drop In.

## Palm Springs (1)


No helmet or glove colours in the table for: Palm Springs HC.

## Circuit teams (121)

| Team | Slot | Jersey | Shell | Trim | Logo | Gloves main | Other glove zones |
|---|---|---|---|---|---|---|---|
| Circuit 1 Team 1 | Home | adidas home (Central Division All-Stars) | `5C0A17` | `E7E7E7` | `E7E7E7` | `5C0A17` | `E7E7E7` z7 |
| Circuit 1 Team 1 | Away | adidas away (Central Division All-Stars) | `5C0A17` | `E7E7E7` | `E7E7E7` | `5C0A17` | `E7E7E7` z7 |
| Circuit 1 Team 10 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 1 Team 10 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 1 Team 11 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 1 Team 11 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 1 Team 12 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 1 Team 12 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 1 Team 13 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 1 Team 13 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 1 Team 14 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 1 Team 14 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 1 Team 15 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 1 Team 15 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 1 Team 16 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 1 Team 16 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 1 Team 17 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 1 Team 17 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 1 Team 18 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 1 Team 18 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 1 Team 19 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 1 Team 19 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 1 Team 2 | Home | adidas home (Central Division All-Stars) | `0775CE` | `E7E7E7` | `E7E7E7` | `0775CE` | `E7E7E7` z7 |
| Circuit 1 Team 2 | Away | adidas away (Central Division All-Stars) | `0775CE` | `E7E7E7` | `E7E7E7` | `0775CE` | `E7E7E7` z7 |
| Circuit 1 Team 20 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 1 Team 20 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 1 Team 3 | Home | adidas home (Central Division All-Stars) | `1B1B1B` | `1B1B1B` | `E7E7E7` | `1B1B1B` | `E3E30C` z6 |
| Circuit 1 Team 3 | Away | adidas away (Central Division All-Stars) | `E7E7E7` | `E7E7E7` | `1B1B1B` | `1B1B1B` | `E3E30C` z6 |
| Circuit 1 Team 4 | Home | adidas home (Central Division All-Stars) | `1B1B1B` | `1B1B1B` | `E7E7E7` | `1B1B1B` | `E3E30C` z6 |
| Circuit 1 Team 4 | Away | adidas away (Central Division All-Stars) | `E7E7E7` | `E7E7E7` | `1B1B1B` | `1B1B1B` | `E3E30C` z6 |
| Circuit 1 Team 5 | Home | adidas home (Central Division All-Stars) | `1B1B1B` | `1B1B1B` | `E7E7E7` | `1B1B1B` | `E7E7E7` z7 |
| Circuit 1 Team 5 | Away | adidas away (Central Division All-Stars) | `1B1B1B` | `1B1B1B` | `E7E7E7` | `1B1B1B` | `E7E7E7` z7 |
| Circuit 1 Team 6 | Home | adidas home (Central Division All-Stars) | `202020` | `E7E7E7` | `E7E7E7` | `202020` | `E7E7E7` z7 |
| Circuit 1 Team 6 | Away | adidas away (Central Division All-Stars) | `E7E7E7` | `E7E7E7` | `202020` | `E8A92C` | `961E1E` z6 |
| Circuit 1 Team 7 | Home | adidas home (Central Division All-Stars) | `21356C` | `202020` | `E7E7E7` | `253B73` | `E7E7E7` z7; `90181C` z8,9 |
| Circuit 1 Team 7 | Away | adidas away (Central Division All-Stars) | `21356C` | `202020` | `E7E7E7` | `253B73` | `E7E7E7` z7; `90181C` z8,9 |
| Circuit 1 Team 8 | Home | adidas home (Central Division All-Stars) | `12192E` | `1B1B1B` | `E7E7E7` | `132032` | `DA391C` z7 |
| Circuit 1 Team 8 | Away | adidas away (Central Division All-Stars) | `E7E7E7` | `E7E7E7` | `202020` | `BA1C20` | `071845` z2,3,4,5,7,11; `E7E7E7` z6 |
| Circuit 1 Team 9 | Home | adidas home (Central Division All-Stars) | `E7E7E7` | `E7E7E7` | `0B182E` | `202020` | `E7E7E7` z7 |
| Circuit 1 Team 9 | Away | adidas away (Central Division All-Stars) | `E7E7E7` | `E7E7E7` | `202020` | `1B1B1B` | `E7E7E7` z7 |
| Circuit 2 Team 1 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 2 Team 1 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 2 Team 10 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 2 Team 10 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 2 Team 11 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 2 Team 11 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 2 Team 12 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 2 Team 12 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 2 Team 13 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 2 Team 13 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 2 Team 14 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 2 Team 14 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 2 Team 15 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 2 Team 15 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 2 Team 16 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 2 Team 16 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 2 Team 17 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 2 Team 17 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 2 Team 18 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 2 Team 18 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 2 Team 19 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 2 Team 19 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 2 Team 2 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 2 Team 2 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 2 Team 20 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 2 Team 20 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 2 Team 3 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 2 Team 3 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 2 Team 4 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 2 Team 4 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 2 Team 5 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 2 Team 5 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 2 Team 6 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 2 Team 6 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 2 Team 7 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 2 Team 7 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 2 Team 8 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 2 Team 8 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 2 Team 9 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 2 Team 9 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 3 Team 1 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 3 Team 1 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 3 Team 10 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 3 Team 10 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 3 Team 11 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 3 Team 11 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 3 Team 12 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 3 Team 12 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 3 Team 13 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 3 Team 13 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 3 Team 14 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 3 Team 14 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 3 Team 15 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 3 Team 15 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 3 Team 16 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 3 Team 16 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 3 Team 17 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 3 Team 17 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 3 Team 18 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 3 Team 18 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 3 Team 19 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 3 Team 19 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 3 Team 2 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 3 Team 2 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 3 Team 20 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 3 Team 20 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 3 Team 3 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 3 Team 3 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 3 Team 4 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 3 Team 4 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 3 Team 5 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 3 Team 5 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 3 Team 6 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 3 Team 6 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 3 Team 7 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 3 Team 7 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 3 Team 8 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 3 Team 8 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 3 Team 9 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 3 Team 9 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 4 Team 1 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 4 Team 1 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 4 Team 10 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 4 Team 10 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 4 Team 11 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 4 Team 11 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 4 Team 12 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 4 Team 12 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 4 Team 13 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 4 Team 13 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 4 Team 14 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 4 Team 14 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 4 Team 15 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 4 Team 15 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 4 Team 16 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 4 Team 16 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 4 Team 17 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 4 Team 17 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 4 Team 18 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 4 Team 18 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 4 Team 19 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 4 Team 19 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 4 Team 2 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 4 Team 2 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 4 Team 20 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 4 Team 20 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 4 Team 3 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 4 Team 3 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 4 Team 4 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 4 Team 4 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 4 Team 5 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 4 Team 5 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 4 Team 6 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 4 Team 6 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 4 Team 7 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 4 Team 7 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 4 Team 8 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 4 Team 8 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 4 Team 9 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 4 Team 9 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 5 Team 1 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 5 Team 1 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 5 Team 10 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 5 Team 10 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 5 Team 11 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 5 Team 11 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 5 Team 12 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 5 Team 12 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 5 Team 13 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 5 Team 13 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 5 Team 14 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 5 Team 14 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 5 Team 15 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 5 Team 15 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 5 Team 16 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 5 Team 16 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 5 Team 17 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 5 Team 17 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 5 Team 18 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 5 Team 18 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 5 Team 19 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 5 Team 19 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 5 Team 2 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 5 Team 2 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 5 Team 20 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 5 Team 20 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 5 Team 3 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 5 Team 3 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 5 Team 4 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 5 Team 4 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 5 Team 5 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 5 Team 5 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 5 Team 6 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 5 Team 6 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 5 Team 7 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 5 Team 7 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 5 Team 8 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 5 Team 8 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 5 Team 9 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 5 Team 9 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 6  - Evil Cyborgs CPU | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 6  - Evil Cyborgs CPU | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 6 - Superheroes | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 6 - Superheroes | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 6 T1 - Blackhawks | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 6 T1 - Blackhawks | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 6 T2 - Blues | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 6 T2 - Blues | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 7 - Chel Xmas Snowmen | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 7 - Chel Xmas Snowmen | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 7 - Xmas Elves | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 7 - Xmas Elves | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 7 - Xmas Gnomes | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 7 - Xmas Gnomes | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 7 - Xmas Trees | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 7 - Xmas Trees | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 8 - Blackhawks | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 8 - Blackhawks | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 8 - Bruins | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 8 - Bruins | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 8 - Canadiens CPU | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 8 - Canadiens CPU | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 8 - Canucks | Home | adidas home (Circuit 1 Team 1) | `5C0A17`† | `E7E7E7`† | `E7E7E7`† | `5C0A17`† | `E7E7E7` z7 |
| Circuit 8 - Canucks | Away | adidas away (Circuit 1 Team 1) | `5C0A17`† | `E7E7E7`† | `E7E7E7`† | `5C0A17`† | `E7E7E7` z7 |
| Circuit 8 - Flames | Home | adidas home (Circuit 1 Team 1) | `5C0A17`† | `E7E7E7`† | `E7E7E7`† | `5C0A17`† | `E7E7E7` z7 |
| Circuit 8 - Flames | Away | adidas away (Circuit 1 Team 1) | `5C0A17`† | `E7E7E7`† | `E7E7E7`† | `5C0A17`† | `E7E7E7` z7 |
| Circuit 8 - Islanders | Home | adidas home (Circuit 1 Team 1) | `5C0A17`† | `E7E7E7`† | `E7E7E7`† | `5C0A17`† | `E7E7E7` z7 |
| Circuit 8 - Islanders | Away | adidas away (Circuit 1 Team 1) | `5C0A17`† | `E7E7E7`† | `E7E7E7`† | `5C0A17`† | `E7E7E7` z7 |
| Circuit 8 - Penguins | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 8 - Penguins | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 8 - Rangers | Home | adidas home (Circuit 1 Team 1) | `5C0A17`† | `E7E7E7`† | `E7E7E7`† | `5C0A17`† | `E7E7E7` z7 |
| Circuit 8 - Rangers | Away | adidas away (Circuit 1 Team 1) | `5C0A17`† | `E7E7E7`† | `E7E7E7`† | `5C0A17`† | `E7E7E7` z7 |
| Circuit 8 - Red Wings | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 8 - Red Wings | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 8 - Toronto Pro | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 8 - Toronto Pro | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 8 Cinco De Mayo | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 8 Cinco De Mayo | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 9 Team 7- Alum Pro G4 | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 9 Team 7- Alum Pro G4 | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |
| Circuit 9 Team 8- LNY Rabbits CP | Home | adidas home (Central Division All-Stars) | `1B1B1B`† | `1B1B1B`† | `E7E7E7`† | `1B1B1B`† | `FF6A4C` z7 |
| Circuit 9 Team 8- LNY Rabbits CP | Away | adidas away (Central Division All-Stars) | `E7E7E7`† | `E7E7E7`† | `1B1B1B`† | `E7E7E7`† | `FF6A4C` z7 |

## Hockey Ultimate Team (1)


No jersey marked as Home or Away: Hockey Ultimate Team.
