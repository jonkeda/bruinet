# Bruinet reply 2: to `construction-followup.md`

Date: 2026-11-02. From: Bruinet. To: Exact.Construction.

## Outcome

Take **Bruinet `fcbaad7a`** (branch `move`). Your full headless run on it went from 388 to 408 passed out of 424.
Of the 9 left, CTS.05 passes on `fcbaad7a` (the run was on the commit before) and MIL.V02 passed on a rerun.
Rows 1 to 9 and ask 4 were head gaps and are fixed; one test (EQU.V04) now shows a race in its steps.

| Ask | What it was | Fix | Commit |
|---|---|---|---|
| 1. Rows 1 to 8 | `PageableCollectionView`'s inner list is `SelectionMode=Single`, so `Click` on a row selected the item; the row's own recognizer never ran, and the journal sets no `ItemTappedCommand` | a row with its own single-tap recognizer is tapped, not selected | `f9c88a1f` |
| 4. `GarbageCatalogEditor` Busy | without a handler MAUI's `NavigationPage` changes its stack and raises nothing after: no `Popped`, `Pushed` or `Appearing`. Your `NavigationService` ends a page's view model on `Popped`, so the nested editor's completion never came and the editor under it stayed Busy | each `NavigationPage` gets a headless handler that finishes every navigation at once, and keeps its alerts for the dialogs | `5d612ec1`, `e8437046` |
| 2. Row 9 | rows found by `ControlType ListItem` matched only `ListView` cells headless; a `CollectionView` row is a list item on Windows, so the contact list read 0 rows | a realized `CollectionView` row is a `ListItem` (`VisualTree.IsItemRow`) | `fcbaad7a` |
| 3. Screenshots | a head with no pixels saves the tree as `<name>.tree.txt`; no more empty `.png` files | | `32d1c2ae` |
| 5. Popup shim docs | `maui-headless.md`: a popup shim follows the window's modal stack and keeps no stack of its own | | `32d1c2ae` |

Each fix has a test in Bruinet that failed before it with your message (the shortcut rows in the MAUI sample,
`NavigationTests.Back_raises_the_navigation_pages_Popped_as_a_device_does`,
`DialogTests.An_alert_the_navigation_page_raises_is_shown_as_well`).

## Now visible: views are destroyed on back

A back now raises `Popped`, so your `OnViewDestroyed` runs headless, as on a device.

- **EQU.V04.1** fails: `AssertRemembered` reads "All (no group selected)". `AssertMachines` passes on the group input's
  display while `SelectGroupAsync` (network reads, then the save) is still running; `ReopenSelector`'s back destroys
  the selector, `CancelGroupChange` cancels the save, and the group is not remembered. On Windows the steps are slow
  enough for the save to finish. **Proposal:** in `ReopenSelector`, wait for the save before going back (the
  fixture's `IsIdle(50)` poll, or the group's request count, as change 3 did).

## Still failing on `e8437046`, not looked at yet

| Test | First failure |
|---|---|
| `ProgressionTabTests.ProgressDialog_RejectThenSave`, PROG.V04.1 | `Expected Text to be 'Progress saved'` |
| `GarbageOverviewReviewTests.ReviewRows_ConveyCommentAndInactiveRelationVisiblyAndAccessibly` | `Assert.Single() Failure: The collection was empty` |
| `GarbageCatalogJourneyTests.NestedCatalogCreation_ReturnsSelectionsAndPreservesBookingAmount` | `Expected container visibility 'False'. Locator: GarbageCatalogEditor_Name` |
| HOURS.V03.1 | the amount sheet did not close after saving |
| FILE.V07.1 | `Expected Exists to be 'True'. Locator: Text:Photo` |

These were on your "not rerun yet" list too. Next on our side: the nested catalog creation (it shares ask 4's flow)
and the Hours sheet; send a `.tree.txt` for any of them if you get there first.

## ClearWindow logging

Not checked yet.
