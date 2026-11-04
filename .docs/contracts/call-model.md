# Contract: The call model

The MAUI, Html and native Android stacks run every public control/page/container/collection/row
member as one **call**. This is the shape all their code must keep. Evidence:
the [MAUI design](https://github.com/exact-mobile-apps/test-tooling/blob/7162ac7/.my/stale-readiness/design.md)
and the [Html design](https://github.com/exact-mobile-apps/test-tooling/blob/7162ac7/.my/html/design.md), at 7162ac7,
and the native Android slice (`.my/androidnative/`, 2026-10). Enforces
[AD-004](../decisions/ad-004-wait-for-state.md) and
[AD-009](../decisions/ad-009-app-bugs-stay-failures.md).

## The rules (R0-R9)

| Rule | What must stay true |
| --- | --- |
| **R0** | App bugs stay failures. The framework waits for *state*; it never waits out, retries through, or swallows a *defect*. Whatever a call absorbs on the way to success is still reported (near-miss). |
| **R1** | One unit per public call: one log entry/exit pair, one failure, around the whole call - the action included. |
| **R2** | Only the call's poll waits. Readiness, lookup, visibility, scrolling and settling are single attempts inside it. Nothing below the poll loops or sleeps. |
| **R3** | Each phase has the caller's budget: resolve with `timeoutMs`, then confirm with `timeoutMs`. A confirmation is a second phase with its own budget, not a second poll. |
| **R4** | A call waits for its scope chain: each scope reports itself and asks its parent. |
| **R5** | A stale element is a signal (`StaleElementException`) from both drivers. A poll re-resolves on it; a confirmation reports it and does not act again. No catch-alls. |
| **R6** | One check that a cached root is still alive, used by every scope that caches a root (`InstanceKey` read through the driver's `Live` helper). |
| **R7** | Non-`Try` members wait; `Try*` members answer about now. |
| **R8** | The generator does not change: same `Run*` names and signatures on every hierarchy. |
| **R9** | MAUI, Html and native Android own the interfaces and bases they change. The calls layer, its exceptions and `ScopeReadiness` are one copy, in Core (`Bruinet.Core.Calls`, `.Exceptions`, `.Scopes`), and so are a control's calls (`ControlCalls<TElement>`, each control base its `IControlHost`). See [AD-010](../decisions/ad-010-maui-ahead-of-core.md). |

## What a call may absorb, and what it must report (R0)

- A **stale element** (R5) is re-resolved by the poll and never acted on twice.
- A **not-ready scope** (R4) is waited for, within the budget.
- A **not-ready element** before an action (`ElementNotReadyException`) may be asked
  again, within the budget.
- Below the poll there are **no catch-alls** (R5): a failure is either an answer (an
  absent element, an unknown candidate) or a signal.
- These **end the call at once**: `AppUnavailableException` (process/session lost);
  configuration errors (`ScopeNotReadyException` - missing/invalid busy signal);
  `RouteUnavailableException` (no route on this element/driver/platform).
- A **budget** the caller set is never extended; a scroll below the call gets what is
  left of it. A driver call below a call that takes a timeout is given what is left of the
  call's budget (`CallBudget`), never the driver's own default. A read or action cut off by the
  budget ends the poll; the call is reported by the last attempt that completed.
- A **confirmation is a phase with its own budget**, down to the driver: the reads that confirm
  an action get the confirmation's time, not what the resolve phase left (R3). `Confirmer` runs
  under its own attempt context.
- A **read is about now**. A driver whose reads wait for their element (Playwright's locators
  do) caps that wait short and reports a node that is gone as stale (R5), for the poll to find
  again: one read never waits out the call (R2).

## Scopes

- **Who asks its parent (R4).** A page asks nobody: its root is present and shown, then
  `IsLoaded()`, then its busy signal. A container, collection and row ask their parent first.
  A scope shown over its parent (a popup, a dialog) does not (`AsksParent => false`).
- **A busy signal missing beneath a hidden root means not loaded, not misconfigured.** Headless,
  each read first runs the app's queued work, so a loading view can hide the page during one
  attempt. When the signal is not found, the root's shown state is read again rather than taken
  from the attempt's memo; only a root still shown makes a missing signal a configuration error.
- **A root is cached for the object's life and checked on every use (R6).** A property declared
  as `=> new` or `{ get; }` differs only in cost. A stale root is re-found once, in one place.
- **A page root is checked as attached, not only as shown.** An element that can outlive its place
  in the tree (`IDetachable`) is invalid once it is out of it. On Windows a page navigated away
  from answers as present, on screen and full size for about a second after it left the tree.
- **A row stands for its item, as far as its key can tell.** A row records the item's key when
  it is made and reports `ItemChanged` when the element at its place holds another item.
  A `Logical` key (`PositionInSet`) or `AutomationId` key can tell; a `Position` key cannot. A
  collection whose platform publishes no `PositionInSet` and whose rows carry no distinguishing
  id cannot detect recycling: give such an item template an id per row (`Task_0`, `Task_1`).

## Broken when

- A wait loop or sleep appears below a call's poll (violates R2).
- A catch-all swallows an exception below the poll (violates R5, R0).
- An action is repeated instead of confirmed (violates R0; see
  [control-object](control-object.md)).
- A budget is extended past the caller's `timeoutMs`.

## Html specifics

The rules above hold unchanged. Where Playwright differs from UI Automation and Appium:

- **Playwright calls take a timeout**, so the Playwright driver is the one that reads
  `CallBudget` today. A read waits for its element at most `PlaywrightHtmlElement.ReadAttachMs`.
- **A page's busy signal is `data-busy` on `<body>`.** A Blazor page requires it: a page
  without it is not loaded yet (a prerendered page is not acted on), not misconfigured. A
  value other than `true`/`false` is a configuration error and ends the call at once. On
  Playwright the page's load state and `data-busy` are read in one round trip (`IHtmlPageProbe`).
- **An action's actionability is Playwright's.** Playwright waits, within the call's budget, for
  the element to be visible, stable and receiving events, so a control does not check visibility
  first (`IHtmlElement.WaitsForActionability`). A timeout before the action ran is
  `ElementNotReadyException` with Playwright's reason ("element is not visible", "… intercepts
  pointer events"). A timeout after it ran ("click action done", then waiting for the navigation
  it started) is the action done (R0): it is not repeated, and the next call waits for the page.
- **A row stands for its item.** It finds its element again by `data-item-key` on every
  lookup, with a selector on the key, never by position: a Playwright element found by position
  is found by position again on every read. When the item is gone it reports `ItemChanged` and
  never acts on the row now in its place.
- **A scope shown over a busy page** (a dialog) does not ask its page (`AsksParent => false`).
- **A frame is a scope** (`FrameScope`), not a new test context: its children resolve inside
  the frame, and it can report not loaded while the frame loads.

## Native Android specifics

The rules above hold unchanged. Where Appium and UiAutomator2 differ:

- **The session's implicit wait is zero**, so a lookup is one attempt (R2), and every read goes
  through the element's `Live` helper: a recreated view is stale (R5), an ended session is
  `AppUnavailableException`, a `mobile:` command the server lacks is `RouteUnavailableException`.
- **The app root is the app in front.** Another app or a system dialog in front is a not-ready
  reason that names itself; a scope drawn by the system over the app (the permission prompt)
  does not ask the app (`AsksParent => false`).
- **A page's busy signal is a view with resource id `bruinet_busy`**, shown while the app works.
  Absent or gone is idle, so it is never a configuration error.
- **A view below the fold is absent from the tree.** A control on a page scrolls it in
  (`UiScrollable`) as part of an attempt, throttled like a scroll into view; a control in a row
  or a dialog does not, since that would move its own scope.
- **A row stands for its item.** Its key is read from the row (a declared view's text, its
  description, its first text); a row finds its view again by that key, never by position, so it
  follows its item when a `RecyclerView` rebinds its views. A keyed item is found by scrolling the
  list (`UiScrollable` from its start, then by hand), within the budget.
- **An accessibility action may not reach the app** (a `RatingBar`'s set-progress): such a
  control acts as a user does, by touch, and confirms what the app shows.
- **A window closing re-issues the nodes below it.** After a popup closes, the control that opened
  it reads as stale though its view is the same; the confirmation reads the control found again by
  its locator. The action is still not repeated (R0).
- **UiAutomator2's own idle wait is short** (`waitForIdleTimeout` 500 ms, not 10 s): an animating
  screen is never idle, and the call's poll is what waits (R2).
