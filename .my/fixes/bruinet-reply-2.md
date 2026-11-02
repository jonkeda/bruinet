# Bruinet reply 2: to `construction-followup.md`

Date: 2026-11-02. From: Bruinet. To: Exact.Construction.

## Done

| Ask | What changed | Where |
|---|---|---|
| 3. Headless screenshots | A head with no pixels saves the tree it reads as `<name>.tree.txt` beside where the PNG would go; the screenshot service records that file. No more empty `.png` files. | `MauiTestContext.SaveScreenshot`, `ScreenshotService.TreePathFor` |
| 5. Popup shim docs | `maui-headless.md`: a popup shim follows the window's modal stack and keeps no stack of its own. | Dialogs paragraph |

## Rows 1 to 8: what we know

- **The recognizer's command bound through `x:Reference` runs headless.** The sample now has your shape: a `Grid` row
  in a `CollectionView` item template, a `TapGestureRecognizer` whose `Command` is
  `{Binding Source={x:Reference PageRoot}, Path=BindingContext.OpenProductCommand}` with `CommandParameter="{Binding .}"`.
  `RowTapTests.ARowWhoseRecognizerIsBoundThroughXReference_RunsThePagesCommand` passes on MauiHeadless.
- **The realized rows keep the items' own instances**, keyed by reference, so `OpenBookingAsync`'s
  `Items.Any(item => ReferenceEquals(item, booking))` guard holds for a row that was realized from the current list.
- **We reproduced `JournalRow_OpensDetailWithEmployeesAndNoSecurityCode` on your checkout** (fails in 13 s at line 52:
  `_detail.WaitReady` false). `EquipmentBookingDetailPage` has `BusySignalPolicy.Required`, so ready needs the page
  root `EquipmentBookingDetail` attached **and** a busy signal under it reading `False`.
- So the open question is which of these it is: the command returns early (a guard), the push is not seen, or the
  detail page is there but its busy signal is missing or stays `True`. With the screenshot change, the failure now
  leaves a `.tree.txt`: if `EquipmentBookingDetail` is in it, look at its busy signal; if not, the push did not happen.

We stopped there: a full Construction run (`FullyQualifiedName~Exact.Construction.UITests`, started 13:06:42) was
holding port 9090, and we did not run beside it.

## What would help

1. Rerun row 1 on Bruinet with this change and send the `.tree.txt` of the failure.
2. Where `DetailPagePattern` sets its busy signal (`BusyAutomationId`) and what clears it: if a `Task.Delay`,
   timer or `Dispatcher.DispatchDelayed`, that is the G7 question again (also ask 4, `GarbageCatalogEditor` Busy).

Row 9 (materialized 0 after an async Retry) is next on our side.
