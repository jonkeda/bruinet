# world/580 — water as shapes: brief

> Plan item WORLD-RW-80, after platform/322. Design source: [Hoorn on curves](../../../../.designs/Studio/street/06_hoorn_curves.md) and the water rule in [Maps 05](../../../../.designs/Maps/05_street.md) (decided 2026-10-15: water laid out as its own shapes, never sampled onto squares).

## What you will play

On Studio › Street, pick Hoorn and see its harbour as it really runs. The Binnenhaven joins the
Markermeer, and the Koepoortsgracht and the Rottegat show as water a square or more wide. The old
core's quays line all three. Pick a town with small canals and see them as water, not lost.

Today Street's water is each 200 m tile's commonest cover, so anything narrower than a tile falls
apart or vanishes (the survey: the Binnenhaven 4 loose squares, the Koepoortsgracht and the Rottegat
gone). After this step the water comes from OSM's own water polygons, kept in the map bodies.

## Scenarios

- **Hoorn's harbour.** On the Street bench pick the area hoorn and the town Hoorn. The Binnenhaven is one body of water joined to the Markermeer, and the Koepoortsgracht and the Rottegat are water at least one square wide. Quay squares run along all three in the old core.
- **Small canals stay.** A canal whose polygon is narrower than a square still lays as water one square wide, joined to the water it really joins.
- **Edam's canals.** Pick Edam and see its canals as water (see the open question: Edam lies outside the dev area).
- **Hover names the water.** Hovering a water square names its water ("Binnenhaven"), as a street square names its street since platform/322.
- **The re-import.** `./data/deploy-database.ps1 -ImportFrom ../tomorrow.data -Lines` rebuilds every map body with its water shapes. A database still holding the old body version stops the start and says to run `-Lines`, as today.
- **Everything else as before.** Kersenboogerd, Enkhuizen and Stede Broec lay their streets as in platform/322; the game's own Street, Area and Region draw as before.

## Scope

**In.**

- The lines import reads OSM's water areas: closed ways and multipolygon relations tagged `natural=water`, `waterway=dock` or `riverbank`, `landuse=basin` or `reservoir` (the survey's `survey.py` set). Each is clipped to every covered resolution-7 cell it touches, simplified (about 5 m), outer rings minus inner, with its name and OSM id.
- `MapBody` gains a water field (`MapWater`: name, OSM id, rings as metres from the body's centre), only on cell bodies; `MapBody.Version` goes to 4 and `Check` refuses water on district or region bodies. No EF migration: the body is one JSON object (0041). No reader of version 3 (no backward compat).
- `StreetTownReads` builds `WaterAreas` from the bodies' shapes, named, instead of the cover tiles.
- `StreetTownLayout`'s water: a polygon narrower than a square keeps a one-square-wide course along it, so a harbour or canal narrower than a tile stays; two shapes that touch in reality stay joined; a named water is never dropped as a speck. The shore smoothing and the old core's quays stay as they are.
- The bench: the counts line's water squares, and hover naming a square's water.

**Out.**

- The game's Street (`Board/StreetLayout`), Area, Region and walking: studio first, game later. They keep reading cover tiles and the atlas's water cells (0024).
- `man_made=quay` lines as quays, piers and breakwaters; boats; water art (07's ripple and reflection).
- Bridges: they keep coming from the water lines (`Bridges.Find`), unchanged.
- The study's dropped lake shores at Enkhuizen and Medemblik beyond what the clipped shapes give.

## Tests, by tier

- **Game:** `MapBody` round-trips water and refuses it off a cell body; on hand-made reads, a 0.4-square-wide canal lays one square wide and joined, two touching polygons stay one body, a named pond under the speck size stays, a polygon with a hole leaves its island dry. `TheLargestTownLaysInTime` keeps its 3 s budget.
- **Data:** a small extract with a harbour way, a multipolygon with an inner ring and a lake crossing two cells: each cell's body holds its clipped part with name and id; a lines import replaces them.
- **UI:** the Street bench smoke and platform/322's touched scenario, plus one scenario: Hoorn shows water and quay squares in the old core (counts line), hover names the Binnenhaven.

## What is unknown

- **Body size and import time.** The Markermeer and the IJsselmeer are huge multipolygons; clipping them per cell bounds each body but costs import time. Measured on the Netherlands extract in build; a budget is set then.
- **Whether OSM maps every Hoorn water as a polygon.** The survey found the Binnenhaven, the Koepoortsgracht and the Rottegat in the same tags, so this is expected to hold.
- **The cell's edge.** A water clipped at a cell's box meets its neighbour's piece; the read must join them without a seam of dry squares.

## Open questions, with proposals

- **Edam lies outside the dev area** (hoorn.poly reaches 52.58° N; Edam is at 52.51°). Proposed: play Enkhuizen's Oude Haven and Buitenhaven instead, and add Edam when an area covers it, because widening the dev area changes every dev world and test fixture for one scenario.
- **Keep the cover tiles at all for Street?** Proposed: shapes only, plus the atlas's water cells where a cell has no shapes (the open lake beyond an extract's box), because a tile's cover is what drowned the quays and broke the harbour.
- **Large lakes in every cell body?** Proposed: yes, clipped, because the harbour must meet the Markermeer on Street and the clip keeps each body small.
- **A decision record?** Proposed: yes, short, amending 0024 for Street only: water areas from OSM shapes in the map bodies; 0024 still rules walking.

## Next

Approve the brief, or change the Edam question; then the design.
