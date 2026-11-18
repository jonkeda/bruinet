# ui/275 — Region you can zoom and pan

## Outcome

Region gets three zoom steps: every town at the closest, no villages at the middle, only big towns at the farthest. You move it by dragging or with the arrow keys, and it is clipped to its box with no scrollbar. The bench and the game share `RegionCanvas`, so both change (0070).

## Scenarios

```gherkin
Scenario: The bench's map zooms in steps and pans without scrollbars
  Given I am on the Home page
  And I am signed in as the admin
  When I tap Admin
  And I tap Region map bench
  And I pick the area "hoorn"
  Then the map should show zoom "3 of 3"
  And the map should name villages
  When I zoom the map out
  Then the map should name no villages
  When I zoom the map out
  Then the map should name only big towns
  When I drag the map to the left
  Then the map's view should have moved east
  When I press the arrow key "Up" on the map
  Then the map's view should have moved north
  And the map should not be laid out again
  And the map should have no scrollbars

Scenario: The game's Region zooms and pans the same way
  Given I am on the Home page
  And I am signed in as a new player
  And my survivor "Mara" is in "Hoorn"
  And I open Here
  When I press the level "Region"
  And I zoom out on the board
  Then the board should name the level "Region"
  And the board should show zoom "2 of 3"
  And the Region map should name no villages
  When I drag the Region map to the left
  Then the Region map's view should have moved east
  And the Region map should have no scrollbars
```

Steps read `RegionMap_Canvas`'s attributes (zoom step, view centre, names by size, layout count), never pixels.

## Scope

- The JS module keeps the layout and redraws only the view.
- The paper, washes and water go into tiles once per zoom step, and panning reuses them.
- The layout is kept per area, recipe and theme version, so zooming and panning never lay it out again.
- The wheel, − and + step inside Region first, then move to the next level up or down.
- Panning works with mouse, finger and arrow keys, and stops at the region's edge.

## Later

- More roads from the district bodies, in [03](../.designs/Maps/changes/03_region_roads.md).
- Towns you can click one by one, the case 0070 names for revisiting.

## Open questions

- **Which step does Region open on?** The closest, as today's map.
- **Do the arrow keys pan on the game?** They walk there (WLD.12); I propose panning only while the map has focus.
- **How do we test a Noord-Holland-sized area?** Test data holds only Hoorn; I propose playing it on the dev database, with a unit test that only tiles in view are drawn.
