# ui/275 — Region you can zoom and pan

## Outcome

Region gets three zoom steps: big towns always, middle towns from the middle step, villages from the closest. You drag the paper or use the arrow keys; the paper fills its box, clipped, with no scrollbars. The bench and the game share `RegionCanvas`, so both change together (0070).

## Play

On Here, open Region, zoom out to the big towns, drag to Den Helder, zoom in until the villages return. On the bench, pick an area as big as Noord-Holland and do the same.

## Scenarios

```gherkin
Scenario: Region hides villages and middle towns as you zoom out
  Given I am signed in as a new player
  And I am on the Here page
  When I press the level "Region"
  Then the board should show zoom "3 of 3"
  And the Region map should show villages
  When I zoom out
  Then the Region map should show middle towns but no villages
  When I zoom out
  Then the Region map should show only big towns
  And the board should have no scrollbars

Scenario: Region pans by dragging and by the arrow keys
  Given I am signed in as a new player
  And I am on the Here page
  When I press the level "Region"
  And I drag the Region map to the left
  Then the Region view should have moved east
  When I press the arrow key "Right"
  Then the Region view should have moved east

Scenario: The bench zooms and pans the same way
  Given I am signed in as the admin
  And I am on the Region map bench
  When I pick the area "hoorn"
  And I zoom the map out
  Then the map should show only big towns
  When I drag the map to the left
  Then the map's view should have moved east
  And the bench should have no scrollbars
```

Steps read zoom, view position and towns per kind from the module's counts, never pixels.

## Scope

- A camera in `RegionCanvas`: only the view is drawn.
- Paper, washes and hatched water drawn once per zoom step into tiles, reused while panning.
- The layout kept per area, recipe and theme until one changes.
- − and + walk Region's three steps before crossing a level.
- Names show and hide with their symbol; panning stops at the region's edge.

## Later

- Free wheel or pinch zoom between steps.
- Clicking map features one by one (would reopen 0070).
- District bodies for more roads (Maps changes 03).
- The map's names as a page list for screen readers.

## Unknown

- No Noord-Holland-sized area in the test set. Proposal: tests use `hoorn`; the big area is checked by hand in play, since importing a province per run costs minutes.
- Which step Region opens at. Proposal: the closest, so the villages around you show first.

## Next

Approve; the design picks tile size and camera state.
