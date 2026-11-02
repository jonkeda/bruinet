# Bruinet reply 2: to `construction-followup.md`

Date: 2026-11-02. From: Bruinet. To: Exact.Construction.

## Outcome

Take **Bruinet `747ca002`** (branch `move`). Your full headless run on it: **413 passed, 4 failed, 7 skipped** out of
424, up from 388. Every head gap you reported, and the three we found on the way, is fixed. The 4 left are in your
tests; each has a proposal below.

| Ask | What it was | Fix | Commit |
|---|---|---|---|
| 1. Rows 1 to 8 | `PageableCollectionView`'s inner list is `SelectionMode=Single`, so `Click` on a row selected the item; the row's own recognizer never ran, and the journal sets no `ItemTappedCommand` | a row with its own single-tap recognizer is tapped, not selected | `f9c88a1f` |
| 4. `GarbageCatalogEditor` Busy | without a handler MAUI's `NavigationPage` changes its stack and raises nothing after: no `Popped`, `Pushed` or `Appearing`. Your `NavigationService` ends a page's view model on `Popped`, so the nested editor's completion never came and the editor under it stayed Busy | each `NavigationPage` gets a headless handler that finishes every navigation at once, and keeps its alerts for the dialogs | `5d612ec1`, `e8437046` |
| 2. Row 9 | rows found by `ControlType ListItem` matched only `ListView` cells headless; a `CollectionView` row is a list item on Windows, so the contact list read 0 rows | a realized `CollectionView` row is a `ListItem` (`VisualTree.IsItemRow`) | `fcbaad7a` |
| 3. Screenshots | a head with no pixels saves the tree as `<name>.tree.txt`; no more empty `.png` files | | `32d1c2ae` |
| 5. Popup shim docs | `maui-headless.md`: a popup shim follows the window's modal stack and keeps no stack of its own | | `32d1c2ae` |
| HOURS.V03, PROG.V04, `ProgressDialog_RejectThenSave` | a popup page is a modal headless, and MAUI's modal push sent `Disappearing` to the page under it. Your Hours overview cancels on `Disappearing` the token the amount sheet's answer is awaited with, so Save never closed the sheet. With Mopups on Windows the page under a popup stays as it is | the page under a `DialogPages` page gets neither `Disappearing` nor `Appearing` | `747ca002` |
| FILE.V07 | headless `MediaPicker.IsCaptureSupported` was false; Windows answers true, so your sheet offers Photo there | true by default (`HeadlessEssentialsOptions.CaptureSupported`); a capture answers as cancelled | `1692a001` |

Each fix has a test in Bruinet that fails without it (the shortcut rows in the MAUI sample,
`NavigationTests.Back_raises_the_navigation_pages_Popped_as_a_device_does`,
`DialogTests.An_alert_the_navigation_page_raises_is_shown_as_well`, `PopupPageTests`,
`HeadlessAppTests.The_camera_can_capture_as_on_Windows_and_a_capture_is_cancelled`).

## The 4 left: in your tests

| Test | Cause | Proposal |
|---|---|---|
| EQU.V04.1 | a back now raises `Popped`, so `OnViewDestroyed` runs headless, as on a device. `AssertMachines` passes on the group input's display while `SelectGroupAsync` is still saving; `ReopenSelector`'s back destroys the selector and `CancelGroupChange` cancels the save. On Windows the steps are slow enough for it to finish | in `ReopenSelector`, wait for the save before going back (the fixture's `IsIdle(50)` poll, or the request count, as your change 3) |
| `NestedCatalogCreation_ReturnsSelectionsAndPreservesBookingAmount` | the nested save works (back to the combination editor, Ready, within a second). `CreateNestedValue`'s `NameField.AssertVisible(false)` then reads `null`: the combination editor's Name field is hidden, and a hidden MAUI element leaves the tree, on Windows as headless | `AssertExists(false)`, as `uat-step-definitions.md` says for an element hidden with `IsVisible="False"` |
| `ReviewRows_ConveyCommentAndInactiveRelationVisiblyAndAccessibly` | it captures the window and reads UI Automation bounds in physical pixels; headless has no pixels or layout | mark it device-only (`[DeviceFact]` or your Windows-only trait) |
| MIL.V02.1 | passes alone, fails in the full run: `AssertNextWeek` spins on the mock's request count outside the driver, so the app does not run while it waits | the same fix as your change 3: wait on the screen, or `IsIdle(50)` on each poll |

## ClearWindow logging

Not checked yet.
