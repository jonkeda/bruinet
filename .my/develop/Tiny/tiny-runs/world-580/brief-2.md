# world/580 — water as shapes

## What you will play

On the Street bench (`/admin/bench/street`) pick Hoorn and the Binnenhaven, the Koepoortsgracht and the Rottegat
lie as water, joined as they really are, with quays along them in the old core. Street's water comes from OSM's own
water outlines, kept per cell in the map bodies, instead of each 200 m tile's commonest land cover. A harbour or canal
narrower than a tile stays, and a tile half quay, half water no longer drowns its houses.

## Scenarios

```gherkin
Scenario: A harbour narrower than a tile stays
  Given an extract with a water outline 120 m wide across one cell
  When its lines are imported
  Then the cell's body keeps the outline in metres from the cell's centre
  And Street lays the squares inside it as water

Scenario: A harbour drawn as a multipolygon
  Given a water relation whose outer ring is two ways and which has an island
  When its lines are imported
  Then the body keeps one outline with the island left dry

Scenario: A lake larger than the cell
  Given a lake outline that covers many cells
  When its lines are imported
  Then each covered cell keeps only its own piece of it

Scenario: Hoorn's harbour on the bench
  Given the dev world imported again with -Lines
  When I pick Hoorn on the Street bench
  Then the counts line shows more water squares than before
  And the Binnenhaven, Koepoortsgracht and Rottegat are water with quays beside them
```

The first three are proven at the unit and data tiers; the last is the owner's play on the dev data, with the bench's
existing counts line as the check in the UI suite.

## Scope

- In: OSM's closed water areas (`natural=water`, `waterway=riverbank`, `landuse=reservoir` and `basin`) read by the lines import, ways and multipolygons alike.
- In: each area cut to its cell, thinned, and kept as a new field on the cell body, which raises the body's version.
- In: Street's read takes these outlines as water areas beside today's water lines; the layout's shores, specks and quays work as they do now.
- In: the dev world's map bodies imported again with `./data/deploy-database.ps1 -ImportFrom ../tomorrow.data -Lines`, since the start refuses the old version.
- Out: Area, Region and the board's tiles keep their water as today; district and region bodies get no outlines.
- Out: the game's own Street, which waits for the studio's layout to come back to the game.

## What is not known yet

- How large the bodies grow: the Markermeer and IJsselmeer are huge relations, so the cut to the cell and the thinning tolerance decide the size.
- Whether the layout's time budget on the largest town still holds with many more outlines to walk.
- The test extract `tests/TestData/osm/hoorn.osm` holds no water outlines, so a small one must be added, and PLT.10's counts may move with it.

## Open questions, with proposals

- **Edam is outside the dev play area** (`hoorn.poly` stops at 52.58° N, Edam lies at 52.51°). Proposed: play Enkhuizen as the second town now, whose harbours lie inside it, and check Edam when a wider West Friesland area exists; widening the area belongs to another step.
- **What happens to the cover tiles' water?** Proposed: a cover water tile still counts only where no outline crosses it, so the open sea (OSM draws it as a coastline, not an area) stays water while outlines win wherever they exist.
- **Which tolerance for the outlines?** Proposed: thin at 5 m, against 15 m for a cell's lines, since a 20 m canal must keep both banks.
- **Swimming pools, fountains and wastewater basins?** Proposed: leave them out by tag (`leisure=swimming_pool`, `water=wastewater`), as they read as specks the layout drops anyway.

## Next

Approve the brief, with the four proposals or changes to them; the design then follows.

Expected next action: the owner approves the brief or answers the Edam question.
