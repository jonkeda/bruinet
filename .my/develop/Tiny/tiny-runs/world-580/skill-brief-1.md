# world/580 — water as shapes

## What you can play

On the Street bench, pick Hoorn: the Binnenhaven, the Koepoortsgracht and the Rottegat lie as
water in their real shapes, quays along them. Edam shows its canals. Hovering water names it.

## What gets built

- The import keeps OSM's water polygons (harbours, canals drawn as areas, ponds, basins) in each cell's map body as a new field, with their names.
- The map body's version goes up; the lines are imported again with `-Lines`, the test set's Hoorn too.
- Street's water comes from those shapes, not from the commonest cover of each 200 m tile, so a harbour narrower than a tile stays.
- The old core's quays follow the shaped water, as they follow today's.
- The counts line says how many squares are water and where they came from; the canvas names the water under the cursor.

## Scenarios

```gherkin
Feature: PLT.10 The Street bench

  Scenario: PLT.10.5 Hoorn's water comes from its shapes
    Given I am on the Home page
    And I am signed in as the admin
    When I tap Admin
    And I tap Streets
    And I pick the area "hoorn" and the town "Hoorn"
    And I lay it out from "1" % built with blocks of "4" squares
    Then the counts should show water squares
    And the water should come from shapes
    And the counts should show quays along the water

  Scenario: PLT.10.6 The water under the cursor is named
    Given I am on the Home page
    And I am signed in as the admin
    When I tap Admin
    And I tap Streets
    And I pick the area "hoorn" and the town "Hoorn"
    And I lay it out from "1" % built with blocks of "4" squares
    And I hover the first water square
    Then the bench should name the water under it
    When I switch to Iso
    Then the counts should show water squares
```

Unit tests prove the rest: a polygon kept, one narrower than a tile laid as water. Edam is played
on the dev data; the test set holds only Hoorn.

## Left for later

- Lakes cut open by the extract's box (the IJsselmeer at Enkhuizen and Medemblik): their shores stay missing.
- Water shapes in district and region bodies, and on the game's own Street.
- Water narrower than a triangle, such as Edam's smallest ditches: dropped as specks, as today.

## Open questions, with proposals

- **Keep the atlas's water cells?** Proposed: yes, for open water whose polygon reaches past the extract, so a coast town doesn't turn dry.
- **Do water lines still mark water?** Proposed: yes, only where no shape covers them, so a canal mapped as a line alone still shows.
- **Which tags count as water?** Proposed: `natural=water`, `waterway=riverbank`, `landuse=basin` and `landuse=reservoir`, and harbour areas; nothing else.

## Next action

Approve the brief, then design: the body field, the import and the read.
