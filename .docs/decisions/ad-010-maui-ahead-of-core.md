# AD-010: MAUI, Html And Native Android Ahead Of Core, On Purpose

Each stack owns its element and its driver. What the stacks share is in Core, once, and a stack's
interfaces derive from Core's. The title is the decision's first form: MAUI left Core's old
interfaces first (2026-09), Html (2026-09-26) and native Android (2026-10-07) followed, and since
2026-11-10 WPF, WinForms and Stride have too. Every stack is on the call model and the first model
is gone (AD-029).

| Layer | Where | What |
| --- | --- | --- |
| Calls | `Bruinet.Core.Calls`, public | `ControlCalls<TElement>` runs every control call once for all stacks, and each control base is its `IControlHost<TElement>`. With it: `ControlCall`, `AttemptContext`, `Deadline`, `Poller`, `Confirmer`, `ScopeGate`, `FirstOutcome`, `Observation`, `ObservationLog`, `Confirmation<T>`, `CallBudget`, `NearMissSettings`. |
| Exceptions | `Bruinet.Core.Exceptions` | `StaleElementException`, `ElementNotReadyException` with `NotReadyReason`, `ScopeNotReadyException`, `AppUnavailableException`, `RouteUnavailableException`. |
| Scopes and pages | `Bruinet.Core.Scopes` | `IScope`, `ITestPage : IScope` (its eleven members), `IElementScope<TElement>` (the three lookups), `ScopeReadiness`, `ObjectBase<TContext>`; on `ITestContext`, the context's `Name` and `NearMiss`. |
| Item keys | `Bruinet.Core.Collections` | `ItemKey` and `ItemKeyKind`; each stack reads its own keys. |
| Element and driver | each stack | `IMauiElement`, `IHtmlDriver`, `IWpf*`, `IStride*`…, for good. |

What holds:

- **A stack's scope, page and context interface derives from Core's** and adds what is typed:
  `Context`, `Page`, its element type for the lookups, and its own extras (Html's `Url`, Android's `Activity`, MAUI's
  `DescribeMiss`). It is never bridged to Core's by an adapter.
- **The element and the driver stay per stack.** MAUI's element carries some forty platform verbs,
  Android's its view's fields, Html's the DOM: a Core element would be the union of them all, or
  too poor to use.
- **One derived interface per stack, with no type parameters**; Core's `IElementScope<TElement>` names the element only, and there is no Core
  `IElementScope<TElement, TContext, TPage>`, since three type parameters on every page object read worse.
- **The page is `ITestPage`, not `IPage`**: Playwright's page is `IPage` in every Html suite, and the
  name matches Composition's `[TestPage]` and `TestPages`. Composition and UAT find pages by it
  (`TestPages.IsPage`); `[TestPage]` stays for a suite that names a page or its lifetime.
- **The calls layer is public** (2026-11-09). A stack is written against Core's published surface,
  as a third party's would be, and Core has no `InternalsVisibleTo` for a stack (the test projects
  keep theirs). The cost is accepted: a change to `IControlHost` is a breaking change, announced in
  the CHANGELOG and the change list (`.github/skills/api-upgrade/references/changes.md`).
- **The control bases keep the `Run*` names and signatures** (R8) as one-line delegations, so the
  generator's output does not change.
- **A layer moves down as a step of its own, once the stacks have proven it.** Still per stack: the
  containers, collections and rows with their own polls (an item found by scrolling, the end of a
  list), the page bases, the key readers, Html's `BusySignal` and MAUI's `RootedScopeBase`. Expect
  them to differ where the platform differs.

**Why:** the stacks had the same `Run*` family three times and the copies had started to differ, and
a scope's and a page's members differed only in the types they return, while elements and drivers
differ for real. Evidence: the [move-down plan](https://github.com/exact-mobile-apps/test-tooling/blob/7162ac7/.my/stale-readiness/move-down.md),
the [review at 7162ac7](https://github.com/exact-mobile-apps/test-tooling/blob/7162ac7/.my/core/review-what-can-move-now.md),
the [review of 2026-10-21](https://github.com/exact-mobile-apps/test-tooling/blob/37f180a7/.my/reviews/2026-10-21-review.md) (C1)
and `.my/upgrade/plan.md`. Each move as it was made: this record at `98a84eff`.

**Broken when:** an adapter is added between a stack's interfaces and Core's; Core is changed to
match a stack's interfaces or bases outside a deliberate move-down step; a stack declares a scope or
page member that Core's `IScope` or `ITestPage` already has; an element or driver
interface is added to Core; or a stack grows its own copy of a leaf-layer type, or of a control call
that `ControlCalls` runs, again.
