# world/580 — water as shapes: design

Brief: `100-brief.md`. OSM water areas are read in the lines import, cut to each covered resolution-7 cell's hexagon,
kept in the cell's `MapBody` (version 4), handed on by `MapBodyStore`, and added to Street's water areas by
`StreetTownReads`. `StreetTownLayout` already paints any water polygon it is given; only its doc changes.

## Shape of the data

| Thing | Where | What |
| --- | --- | --- |
| `MapWater(long Cell, long OsmId, IReadOnlyList<(short East, short North)> Ring)` | `src/Tomorrow.Game/World/MapBodies.cs` | One water ring on a cell, open (last point not repeated), at least 3 points, whole metres east/north of the cell's centre (`LineGeometry`). `OsmId`: the way's id, or the relation's id negated (so a way and a relation never share one). `Cell` filled on read, as `MapBuilding.Cell`. |
| `MapBody.Water` | same | `IReadOnlyList<MapWater> Water { get; init; } = []`; `IsEmpty` counts it. |
| `MapBody.Version` | same | `3` → `4`; summary: "4 (world/580) water rings". |
| JSON | `MapBody.Body` | top-level `"w"` (null when empty): `[{ "o": <osm id>, "p": [e0, n0, e1, n1, ...] }]`, record `WaterBody(o, p)`. Odd count of numbers throws `JsonException`, as `LineBody` does. |
| `MapBody.Check` | same | New errors: "Only a cell body holds water." (resolution ≠ 7 with water); "A water ring has fewer than three points."; water on another cell than its body's joins the existing "belongs to another cell" check. |
| `CellLineSet.Water` | `src/Tomorrow.Game/World/CellLines.cs` | `IReadOnlyList<MapWater> Water { get; init; } = []`; `MapOf(cell)` copies the cell's water into `CellMap.Water`. |
| `CellMap.Water` | same | `IReadOnlyList<MapWater> Water { get; init; } = []`. `Mapped` unchanged (buildings only). |

No EF model change: the body is a string. The migration only empties the table.

## Game (`src/Tomorrow.Game`)

| Change | Detail |
| --- | --- |
| `MapBodies.cs` | As above: `MapWater`, `MapBody.Water`, `Version = 4`, `ToJson`/`FromJson`/`Check`/`IsEmpty`, private `WaterBody`. |
| `CellLines.cs` | `CellLineSet.Water`, `CellMap.Water`, `MapOf` carries water. |
| `Board/StreetMaps/StreetTownReads.cs` | After the tile areas: for each `w` in `set.Water` with `cells.Contains(w.Cell)` and `w.Ring.Count >= 3`, `areas.Add([.. w.Ring.Select(p => Square(w.Cell, p.East, p.North, band))])`. No built-tile check for shapes (the shape is OSM's own, not a tile's commonest cover). Class summary: "…their bodies' water rings (world/580) beside their land's water tiles…". |
| `Board/StreetMaps/StreetTownLayout.cs` | Doc only: replace "(the import keeps no water shapes)" with "(the bodies' water rings, world/580, and each water tile's square)". `Water()` unchanged: a triangle whose centre lies in a ring is water; specks under `MinWater` dropped; two smoothing passes. |
| `Board/StreetMaps/StreetRecipe.cs` | `StreetTownRead.WaterAreas` doc: "each water tile as a square polygon and each body water ring". |

Not touched: the game board's Street (`GetStreet`, `StreetSquares`, `StreetLayout`), Area, Region, routes, links,
bridge sites. Water links stay from water lines only; a harbour mapped only as an area makes no link (out of scope).

## Data (`src/Tomorrow.Data`)

### `World/Import/OsmWater.cs` (new, `public static class OsmWater`)

`public static IReadOnlyDictionary<long, IReadOnlyList<MapWater>> Read(OsmExtract extract, IReadOnlySet<long> covered, out int dropped)`

Constants: `Tags = ["natural=water", "waterway=riverbank", "waterway=dock", "landuse=reservoir", "landuse=basin"]`,
`SimplifyMetres = 5`, `MinSquareMetres = 100`, `MaxRings = 64`, `MaxPoints = 2000`, `StepMetres = 100`.

| Pass | Reads |
| --- | --- |
| 1 | Relations with `type=multipolygon` and a water tag: kept; their way members with role `outer` or `""` noted (inner members ignored: an island reads as water). |
| 2 | Ways: closed (`Nodes.Count >= 4`, first == last) with a water tag, unless an outer member of a kept relation (no ring twice); and every noted member way. Needed node ids collected. |
| 3 | Node coordinates into a sorted id array and two `float` arrays, as `OsmBuildings` keeps them. |

Polygons (WGS84): a closed way → `CreatePolygon` (skip on `ArgumentException` or fewer than 4 coordinates); a relation →
`OsmRings.Polygons(outer member lines)` (below), skipped when it yields none (a relation cut open by the extract's box,
WORLD-RW-80's note: out of scope). Invalid polygons go through `GeometryFixer.Fix`.

Per polygon, the candidate cells: `WorldGrid.CellsIn(polygon)` ∪ `WorldGrid.CellsWithin(c, 1)` for the cell `c` of every
point along its shell densified to `StepMetres`; kept when `covered` holds them. Per candidate cell:

1. Project the shell's coordinates with `LineGeometry.ToMetres(cell, lat, lng)` into a planar `Polygon` (`GeometryFactory` without SRID); the shell only, so holes never come in.
2. The hexagon: `WorldGrid.CornersOf(cell)` projected the same way, closed.
3. `hexagon.Intersection(water)`; take each `Polygon` part (from `Polygon`, `MultiPolygon` or `GeometryCollection`).
4. `TopologyPreservingSimplifier.Simplify(part, SimplifyMetres)`; its exterior ring; area below `MinSquareMetres` dropped.
5. Coordinates rounded to `short` (clamped, as `OsmLines.Short`), the closing point dropped, consecutive duplicates dropped; fewer than 3 left: dropped.

Per cell, rings sorted by area descending (then `OsmId`), taken while the cell holds fewer than `MaxRings` rings and the
next ring keeps its points at or under `MaxPoints`; the rest counted into `dropped`. Same input gives the same rings in
the same order.

### `World/Import/OsmRings.cs` (new, `internal static class OsmRings`)

`Polygons(IEnumerable<Coordinate[]> lines, GeometryFactory factory)`: `Polygonizer` over the lines, each polygon fixed
when invalid; the code moved out of `OsmImporter.Multipolygon.Rings`, which then calls it. Behaviour of `OsmImporter`
unchanged (its tests stay green).

### Other Data changes

| File | Change |
| --- | --- |
| `World/Import/LineBodies.cs` | `Build(LineImport import, LineBook book, IReadOnlyDictionary<long, CellMap>? maps = null, IReadOnlyDictionary<long, IReadOnlyList<MapWater>>? water = null)`: a body for every covered cell with lines, a map or water; `Water = water?.GetValueOrDefault(cell) ?? []`. `BuiltBodies` gets `DroppedWater` (init). |
| `World/Import/GeoImport.cs` `WriteLinesAsync` | `var water = OsmWater.Read(osm, import.Covered, out var droppedWater);` then `LineBodies.Build(import, book, maps, water) with { DroppedWater = droppedWater }`. |
| `World/Import/MapBodyImport.cs` | `LineImportResult` gains `int Water = 0, int DroppedWater = 0` (rings written on cells, rings past the caps). Coarse bodies unchanged (district/region hold no water). |
| `World/MapBodyStore.cs` | `ReadAsync`: `Water = [.. own.Values.SelectMany(b => b.Water)]`. |
| `Migrations/<stamp>_WaterShapes.cs` | `dotnet ef migrations add WaterShapes --project src/Tomorrow.Data`; `Up` and `Down` both `migrationBuilder.Sql("TRUNCATE TABLE geo.MapBody");` with the comment "world/580: map bodies go to version 4 (water rings); the tooling imports them again (0058), as MapBodyVersion2 and GatesAndFences did." Snapshot unchanged. |

## Tooling and scripts

| File | Change |
| --- | --- |
| `data/deploy-database.ps1` | `$MapBodyVersion = 4` (`MapBodyVersionTests` holds it to `MapBody.Version`). |
| `tools/Tomorrow.Importer/ImporterCommand.cs` | The lines report adds `; {result.Water} water rings` and, when non-zero, `; {result.DroppedWater} past a cell's caps`. |

After merge the owner (or a local run) runs `./data/deploy-database.ps1 -ImportFrom ../tomorrow.data -Lines`. Until
then `geo.MapBody` is empty: Street, the board's lines and routes have no bodies (the brief's assumption).

## AI

None.

## App / UI

No page changes. `Street.razor` (Admin › Benches › Street) shows the new water through the layout. UI suites: smoke only
(`PLT.10-street-bench.feature.md` is in the smoke set); no new scenario, no page object change.

## Tests

| Tier | Project, class | Test | Proves |
| --- | --- | --- | --- |
| Game | `Tomorrow.Game.Tests/World/MapBodiesTests` | `CellBody_RoundTrip_Water` | rings and OSM ids survive `ToJson`/`FromJson(json, h3)`, `Cell` filled. |
| Game | same | `Json_WaterUnderW_NullWhenNone` | short key `"w"`; absent when empty. |
| Game | same | `Check_WaterOnlyInACell_OnItsOwnCell_ThreePoints` | the three new errors. |
| Game | same | `FromJson_WaterOddCount_Throws` | `JsonException`. |
| Game | same | `Empty_IsEmpty` (extend) | a body with only water is not empty. |
| Game | `Board/StreetMaps/StreetTownReadsTests` | `AWaterRingIsAWaterArea` | a ring on the town cell comes out as one area in squares (`Square(cell, e, n, band)` per point). |
| Game | same | `AWaterRingOutsideTheAreaIsLeftOut` | a ring on a cell `within` drops. |
| Game | `Board/StreetMaps/StreetTownLayoutTests` | `AHarbourNarrowerThanATile_IsWater` | scenario 1's end: a read with a 120 m × 400 m ring inside the town paints its squares `StreetUse.Water`, where a read of tiles alone (no water tile) paints none. |
| Game | `World/FakeCellLines` | (support) | `List<MapWater> Water`, given with every line read. |
| Data unit | `Tomorrow.Data.Tests/World/Import/OsmWaterTests` (new, `Category=Unit`, elements in memory as `OsmBuildingsTests`) | `AHarbourNarrowerThanATile_KeptAsARing` | scenario 1: a 120 m wide `natural=water` closed way across the cell → one ring, metres from the centre, area within 5 % of the real one. |
| Data unit | same | `AMultipolygonOfTwoOuterWays_TheJoinedRing` | scenario 2: a relation whose outer is two open ways → one ring, `OsmId` the relation's negated. |
| Data unit | same | `AnInnerRing_LeftOut_ReadsAsWater` | an island's inner way changes nothing: the ring has the outer's area. |
| Data unit | same | `EveryWaterTag` (Theory over the five tags) and `OtherTags_NoRing` (`landuse=grass`, `waterway=canal` as a line) | the tag set. |
| Data unit | same | `AcrossTwoCells_ARingInEach_WithinItsHexagon` | the cut: every point within its cell's hexagon (1 m slack), areas sum to the whole. |
| Data unit | same | `UnderAHundredSquareMetres_Dropped` | the minimum. |
| Data unit | same | `CapsLargestFirst` | 70 rings on a cell → 64, the smallest dropped and counted; a ring past 2,000 points skipped. |
| Data unit | same | `UnclosedWay_RelationMissingAMember_Skipped`, `UncoveredCell_NoRing`, `AWayAlsoAnOuterMember_OneRing` | edges. |
| Data unit | same | `SameInput_SameRings` | determinism. |
| Data unit | `LineBodiesTests` | `Build_WaterAloneMakesABody` | a covered cell with only water gets a body holding it. |
| Data unit | `OsmImporterTests` | (existing, unchanged) | `OsmRings` move keeps multipolygon facts. |
| Data DB | `Tomorrow.Data.Tests/World/MapBodyStoreTests` | `WithStreets_GivesTheCellsWater` | a version-4 body with water written and read back with `Cell` set. |
| Data DB | `DatabaseSetupTests` | (update) | migration list ends `…,BuildingCategoryNoDefault,WaterShapes`. |
| Architecture | `MapBodyVersionTests` | (existing) | script and Game both at 4. |
| UI | `Tomorrow.App.UITests` | `Gate=Smoke` | the Street bench still opens. |

The Data DB tier and the re-import need the local SQL Server; in the cloud only Game, Data unit and Architecture run.

## Play and video

Play (after `-Lines`): Admin › Benches › Street, pick Hoorn: the Binnenhaven, the Koepoortsgracht and the Rottegat are
water with quays along them in the old core; pick Edam: its canals are water. Video script, three shots: Hoorn before
(tiles only, from the play copy) and after; Hoorn's harbour zoomed; Edam.

Risk: a ring narrower than about a triangle's centre spacing (a canal under ~25 m) may be eroded by the layout's specks
and smoothing passes; such canals usually carry a `waterway=canal` line, which is still rasterised. If Edam's narrow
canals are lost in play, the follow-up is in `StreetTownLayout.Water` (a ring also rasterised as its outline), not in the import.

## Decisions

None new: the body follows 0058 and 0041 (a shape change empties the table and imports again). Close notes "cell body
version 4: water rings (world/580)" in 0058's status line, as 0059 did for version 2.

## Tasks

1. `OsmRings` out of `OsmImporter`; `OsmImporterTests` green.
2. Game: `MapWater`, `MapBody.Water`, version 4, JSON, `Check`; `MapBodiesTests` first.
3. Game: `CellLineSet.Water`, `CellMap.Water`, `FakeCellLines.Water`.
4. Data: `OsmWater.Read` with `OsmWaterTests` first (both brief scenarios).
5. Data: `LineBodies.Build` water param, `BuiltBodies.DroppedWater`, `LineImportResult.Water`/`DroppedWater`, `GeoImport.WriteLinesAsync`; `LineBodiesTests`.
6. Data: `MapBodyStore` hands water on; migration `WaterShapes`; `DatabaseSetupTests`, `MapBodyStoreTests`.
7. `deploy-database.ps1` `$MapBodyVersion = 4`; importer report line; `MapBodyVersionTests` green.
8. Game: `StreetTownReads` adds rings; layout and read docs; `StreetTownReadsTests`, `StreetTownLayoutTests`.
9. Local: `-Lines` re-import, Street on Hoorn and Edam checked, UI smoke.
