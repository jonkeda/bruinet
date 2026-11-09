# AD-010: MAUI, Html And Native Android Ahead Of Core, On Purpose

`Bruinet.Maui` owns the interfaces its call model needs (`IMauiElement`,
`IMauiDriver`, `IMauiElementScope`, `IMauiPage`, `IMauiTestContext`) and no longer
implements Core's element, driver, scope and page interfaces. Since 2026-09-26
`Bruinet.Html` does the same, with its own set (`IHtmlElement`, `IHtmlDriver`,
`IHtmlElementScope`, `IHtmlPage`, `IHtmlTestContext`), and since 2026-10-07
`Bruinet.NativeAndroid` (`INativeAndroidElement`, `INativeAndroidDriver`,
`INativeAndroidElementScope`, `INativeAndroidPage`, `INativeAndroidTestContext`). The stacks where the model was
designed and proven keep their own shapes; the other stacks keep Core's until the proven
shape moves down to Core, one layer at a time, as its own step
([move-down plan](https://github.com/exact-mobile-apps/test-tooling/blob/7162ac7/.my/stale-readiness/move-down.md)).

**The leaf layer has moved down** (2026-09-26, [review at 7162ac7](https://github.com/exact-mobile-apps/test-tooling/blob/7162ac7/.my/core/review-what-can-move-now.md)).
It names no element, driver or scope type, so it needed none of the interfaces:

- `Bruinet.Core.Calls`: `Deadline`, `AttemptContext`, `Observation`, `ObservationLog`,
  `Poller`, `Confirmer`, `ControlCall`, `ScopeGate`, `FirstOutcome` (the body of every
  scope's `WaitFirst`) (internal, visible to `Bruinet.Maui`,
  `Bruinet.Html`, `Bruinet.NativeAndroid` and their test projects), and `Confirmation<T>`, `CallBudget`,
  `NearMissSettings` (public);
- `Bruinet.Core.Exceptions`: `StaleElementException`, `ElementNotReadyException` with
  `NotReadyReason`, `ScopeNotReadyException`, `AppUnavailableException`,
  `RouteUnavailableException`;
- `Bruinet.Core.Scopes`: `ScopeReadiness`, `ScopeReadinessState`.

MAUI, Html and native Android have no copies of these.

**The control layer's calls have moved down** (2026-10-22, the
[review of 2026-10-21](https://github.com/exact-mobile-apps/test-tooling/blob/37f180a7/.my/reviews/2026-10-21-review.md), C1). The three control bases
(Html's `ControlBase`, MAUI's and native Android's `ViewBase`) had the same `Run*` family three
times, and the copies had started to differ. Now `Bruinet.Core.Calls.ControlCalls<TElement>` runs
every call once for all of them: the attempt, the visibility check, the action once, the waits,
reads and asserts. Each base is its `IControlHost<TElement>` (internal, like the leaf layer): its
element lookup, scope probe, settings, and the few places a platform differs (MAUI's sweep and
native Android's scroll to find, Playwright's own actionability, the scroll throttle's key). The
bases keep the `Run*` names and signatures (R8) as one-line delegations, so the generator's
output is unchanged. It still names no element interface: `TElement` is the stack's own.

MAUI's scopes followed the same day: `RootedScopeBase`, under its containers, collections and
rooted pages, had the same family over the scope's root (`ActOnceOnRoot`), and is now a host
too, one that names itself a container, never checks its root's visibility and forgets a stale
root. Html's and native Android's containers had no such family (a scope there only probes its
readiness), so nothing of theirs moved.

What is still per stack: the interfaces, the collections' own polls (an item found by scrolling,
the end of a list), pages and their `RunProbe`, item keys, and Html's `BusySignal`.
Expect those to differ from their Core counterparts, and from each other where the
platform differs. Do not add adapters between them and Core: a bridge now would have to
be undone in the move down.

**Broken when:** an adapter is added between the MAUI, Html or native Android interfaces and Core's;
Core is changed to match any stack's interfaces or bases ahead of their deliberate
move-down; or a stack grows its own copy of a leaf-layer type, or of a control call that
`ControlCalls` runs, again.
