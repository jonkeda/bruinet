# world/580 — review

## Outcome

The build does what the design says: OSM's water areas are read in three passes, cut to each covered cell's hexagon,
simplified, capped and kept as rings in the cell body (version 4), and Street's town read paints them. The code is small
and follows the existing patterns (barriers in world/385, the 0058 truncate migration, the version kept in step with the
deploy script). Two points should be fixed before the owner plays it: one bad water area can abort a whole `-Lines`
import, and a tile whose commonest cover is water still paints its whole 200 m square, which the plan wanted to end.

## What was checked

The diff was read against the brief, the design and the code it touches in the parent commit. Tests were not run and
nothing was built, by this review's rules. The re-import of the Netherlands was not run in the build either, so the play
outcome (Hoorn's Binnenhaven, Koepoortsgracht and Rottegat; Edam's canals) is unverified, and import time and memory on a
country extract are unmeasured.

## Findings

- **Should fix: an exception while cutting aborts the import.** `OsmWater.Read` guards only the building of an area
  (`Keep`); `WorldGrid.CellsIn(area)` and `InCell` (projection, `Intersection`, simplify) run outside the try. One area
  that the overlay or the H3 fill can't handle stops the whole extract's line import after the lines and buildings passes.
- **Should fix, or decide: tile water still overrides the shapes.** The plan item says Street's water comes "from shapes
  and not from each 200 m tile's commonest cover"; the brief turned that into "beside". A harbour tile whose commonest cover
  is water stays a full 4x4-square water block, so the quays the shapes would give may not appear.
- **Concern: memory and time on a country extract.** Every water area of the extract is held in memory before cutting, and
  each cell an area touches copies and projects the whole area before intersecting; a long riverbank relation is projected
  once per cell it crosses.
- **Small: old-style multipolygons count twice.** A closed way tagged as water that is also a water relation's outer gives
  two identical rings in the cell, spending the 64-ring and 2,000-point budget.
- **Small: `Skipped` is never reported.** The design says skipped areas are counted; the count stops at `WaterRead` and is
  neither in `LineImportResult` nor printed. `WaterRead.Rings` and `Points` are unused.
- **Nits.** The layout's summary still says "the import keeps no water shapes"; the cell-mismatch error in `MapBody.Check`
  doesn't name water rings; the houses' odd-count message now reads "Houses of kind 3 has"; `OsmImporter` keeps an unused
  `using NetTopologySuite.Operation.Polygonize`; `MinArea`'s comment ("a fifth of a Close square's patch") doesn't say
  what it means.

## Decision for the owner

Proposal: when a cell's body holds water rings, its tiles' cover water is not painted (the atlas's wet cells and cover
stay for cells without rings, such as sea the extract's box cut open). That meets the plan's wording and keeps today's
water where OSM gives no shapes. The alternative is to keep both, as built, and see Hoorn after the re-import first.

## Details for the fixer

| Item | Where | Change |
| --- | --- | --- |
| Cut guarded | `src/Tomorrow.Data/World/Import/OsmWater.cs`, the `foreach (var area in areas)` loop | Wrap `CellsIn`, the outline walk and `InCell` per area in the same `try`/`catch (Exception e) when (e is not OperationCanceledException)` as `Keep`, counting `skipped`. Test: an area that throws in the cut (e.g. a fake via an internal seam) leaves the others kept. |
| Tile water vs rings | `src/Tomorrow.Game/Board/StreetMaps/StreetTownReads.cs`, the tiles loop | If decided: skip a cell's cover-water tiles when `set.Waters.Any(w => w.Cell == cell)`. Test: `StreetTownReadsTests` with a water-cover tile and a ring on the same cell gives only the ring's area. |
| Clip before projecting | `OsmWater.InCell` | Intersect the WGS84 area with the hexagon's envelope (or a prepared hexagon) before `Copy` and `Transform`. |
| Duplicates | `OsmWater.Read` | Skip a closed water way that is an outer member of a kept water relation (`members` already holds the ids). |
| Report skipped | `MapBodyImport.LineImportResult`, `GeoImport.WriteLinesAsync`, `ImporterCommand` | Carry `water.Skipped` through and print it; drop or use `WaterRead.Rings`/`Points`. |
| Nits | `StreetTownLayout.cs` summary, `MapBodies.cs` `Check` and `Pairs`' houses caller, `OsmImporter.cs` usings, `OsmWater.MinArea` doc | Text only. |
| Verify | — | `dotnet test tests/Tomorrow.Game.Tests`, `tests/Tomorrow.Data.Tests` (`OsmWaterTests`, `LineBodiesTests`, `DatabaseSetupTests`), `tests/Tomorrow.Architecture.Tests` (`MapBodyVersionTests`); then `./data/deploy-database.ps1 -ImportFrom ../tomorrow.data -Lines`, note the printed water rings, time and peak memory, and look at Hoorn and Edam on Studio › Street. |

Expected next action: guard the cut loop in `OsmWater.Read`, settle the tile-water proposal, then re-import with `-Lines` and look at Hoorn.
