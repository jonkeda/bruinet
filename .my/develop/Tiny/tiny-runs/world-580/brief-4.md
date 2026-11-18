# world/580 — water as shapes

> Brief. Plan item: WORLD-RW-80, after platform/322. Study: [Hoorn on curves](../../../.designs/Studio/street/06_hoorn_curves.md),
> water decided in [the Hoorn survey](../../../.designs/Studio/street/03_hoorn_survey.md) (2026-10-15).

## What you will play

On Studio › Street you pick Hoorn and see its old harbour as water: the Binnenhaven, the
Koepoortsgracht and the Rottegat each one joined piece, open to the Markermeer where they really
are, with quays along their banks. Today each 200 m tile's commonest cover decides, so the
Koepoortsgracht and the Rottegat vanish and the Binnenhaven falls into pieces. You then pick Edam
and see its canals run through the town instead of nothing. Hovering a water square names it, as a
street square already names its street.

## Scenarios

- **Hoorn's harbour.** Pick Hoorn; the Binnenhaven, Koepoortsgracht and Rottegat each show as one piece of water, and hovering each gives its name.
- **Narrow water stays.** A canal narrower than a square (Edam's, the Koepoortsgracht) is drawn at least one square wide along its whole length, never dropped as a speck.
- **Joined as it really is.** Water that joins in OSM joins on Street; the Binnenhaven reaches the Markermeer, a pond that stands alone stays alone.
- **Quays follow the shapes.** In the old core every dry square along the water is a quay, so the harbour streets run along the banks.
- **Buildings stay dry.** No building or house stands on water; where a shape and a building overlap, the building wins its squares.
- **The counts line.** The water count changes with the shapes; the real-street measure of 322 (Hoorn about 67%) does not drop.

## Screens

No new screen. The Street bench keeps its layout; the plan view shows the new water, the hover
names water as well as streets, and the counts line's Water count changes. Rendered draws the
water and quay squares as today.

## Scope

- **Import.** OSM's water areas (`natural=water` with any `water=*`, `waterway=riverbank` and `dock`, `landuse=basin` and `reservoir`; closed ways and multipolygon relations with their holes) kept in each resolution-7 cell's map body as a new field: the shape cut to the cell, its name and OSM id.
- **Detail.** Water shapes are simplified much finer than the lines' 15 m (proposed 3 m), because a canal is 15 to 25 m wide.
- **Body version.** `MapBody.Version` goes from 3 to 4; the start refuses the old bodies (0041, 0058), the table is emptied and the lines imported again with `deploy-database.ps1 -ImportFrom ../tomorrow.data -Lines`; no upgrade code.
- **Street's read.** `StreetTownReads` takes its water areas from the bodies' shapes instead of the WorldCover tiles; Natural Earth's water cells (0024) still fill a cell with no shape and nothing built on it, so the open sea stays.
- **Street's layout.** `StreetTownLayout.Water` keeps its triangle rule but never drops a named shape as a speck, and gives a shape thinner than a square a one-square channel along it, joined where the real shapes touch.
- **The bench.** A water square's hover names its water.
- **Out:** man-made quay lines (`man_made=quay`), quays outside the old core, the game's own Street and board (they still read cover and Natural Earth), the region and area maps, bridges over the new water (the bridge sites stay on the lines), and new water art.

## What's unknown

- **Size and time.** The Netherlands is wet; water rings in every cell body may grow `geo.MapBody` and the `-Lines` import noticeably. The build measures both on the Netherlands extract and reports them.
- **The big lakes.** The Markermeer and IJsselmeer are huge multipolygons; whether the extract holds them whole, and how long building and clipping them takes, is found at build. The study dropped lakes cut open by its box, which lost the shores at Enkhuizen and Medemblik.
- **The one-square channel.** How to draw it (a shape's centre line, or its triangles at a lower share) is the design's choice; it must stay joined and keep the shore at 45° steps.

## Open questions, with proposals

- **Edam lies outside the Hoorn area** (its poly runs from 52.58° N; Edam is at 52.51°), so the bench can't pick it today. Proposed: add a small Edam area through the New region queue and import it, as runs may; if that fails, play Enkhuizen's harbours instead and note it.
- **Keep WorldCover water anywhere on Street?** Proposed: no; a tile's commonest cover is what loses the harbours, and Natural Earth already covers open water where OSM has no shape.
- **Quays from OSM's quay lines?** Proposed: not now; the old core's rule (every dry square beside the water) already lines the harbour, and the quay lines can follow as their own step if the banks look wrong.
- **Water outside the old core with quays?** Proposed: no; elsewhere the bank is green or garden, as a ditch in a 1970s quarter is.

## Next action

Approve the brief, then design: the body field and its JSON, the importer's water pass beside `OsmBuildings`, the read, the layout's channel rule, and the tests per tier (Data: a canal and a holed lake cut across two cells; Game: a 20 m canal on a hand-made read keeps a joined channel; UI: Hoorn's harbour named on hover).
