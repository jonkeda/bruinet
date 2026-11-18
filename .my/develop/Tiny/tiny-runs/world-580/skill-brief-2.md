# world/580 — water as shapes

## What you will play

On Studio › Street, pick Hoorn: the Binnenhaven, the Koepoortsgracht and the Rottegat lie as water in their real shape, with quays along them. Pick Edam: its canals show. Today Street's water is each 200 m tile's commonest cover, so a harbour narrower than a tile vanishes; now it comes from OSM's water polygons.

## Scope

- The import keeps OSM's water polygons in each cell's map body, a new field; the body's version goes up.
- You import the lines again with `./data/deploy-database.ps1 -ImportFrom ../tomorrow.data -Lines`.
- Street fills a triangle with water when its centre lies in a kept polygon, not by cover; water lines still add the squares they cross.
- The test set's Hoorn extract gains a harbour polygon narrower than a tile.

## Scenarios

```gherkin
Feature: PLT.10 The Street bench, water as shapes

  Scenario: PLT.10.5 Hoorn's harbour drawn from its shape, with quays
    Given I am on the Home page
    And I am signed in as the admin
    When I tap Admin
    And I tap Streets
    And I pick the area "hoorn" and the town "Hoorn"
    And I lay it out from "1" % built with blocks of "4" squares
    Then the town's water should come from shapes
    And the harbour narrower than a tile should be water
    And quays should run along the water

  Scenario: PLT.10.6 The water stays when the town is laid out again
    Given I am on the Home page
    And I am signed in as the admin
    When I tap Admin
    And I tap Streets
    And I pick the area "hoorn" and the town "Hoorn"
    And I lay it out from "1" % built with blocks of "4" squares
    And I set the grain 45° away from the measured one
    Then the harbour narrower than a tile should be water
    When I switch to Iso
    Then the harbour narrower than a tile should be water
```

The Then steps read the counts line and the canvas's attributes, never the pixels. Edam is checked on the dev data.

## What grows later

- Canals narrower than a square: they stay water lines, as today.
- Lakes cut open by the extract's box, such as the IJsselmeer at Enkhuizen and Medemblik: their shores stay missing.
- Water shapes on the game's board and the Area and Region maps.
- Bridges from the shapes, and rendered water art.

## Open questions, with proposals

- **Keep cover tiles as a fallback where a body has no water shapes?** Proposed: no; the dev world is imported again, and two sources would hide a missed import.
- **Which OSM tags count as water?** Proposed: natural=water, water=harbour, landuse=basin and waterway=riverbank or dock; leave out wetland and swimming pools.
- **How small a polygon is kept?** Proposed: at least half a Street square, so specks go.

Expected next action: approve the brief, then design world/580.
