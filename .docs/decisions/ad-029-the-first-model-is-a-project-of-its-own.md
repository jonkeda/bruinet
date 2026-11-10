# AD-029: The First Model Is A Project Of Its Own

**Superseded 2026-11-10.** WPF, WinForms and Stride moved onto the call model (`.my/upgrade/plan.md`,
U3 to U6), and `Bruinet.Core.Legacy` was deleted with the last of them (U7). Every stack now stands on
Core alone, as AD-010 says; `SolutionTests.NoProject_UsesTheFirstModel` keeps the old project from
coming back. What follows is the record of why it was split off.

`Bruinet.Core` describes the call model alone. The interfaces and waits of Bruinet's first model,
where every lookup waits on a budget of its own, are `Bruinet.Core.Legacy`, which only the WPF,
WinForms and Stride stacks reference: `IDriver`, `IElement`, `IElementScope`, `IPageObject`,
`IContainerControl`, `IContainerObject`, `IPagedScope`, `IRangePatternElement`, the generic
`ITestContext<T>`, `PageReadinessSnapshot`, the old `ControlObjectBase`, the geometry over
`IElement`, and `LegacyWaitHelper` (once `WaitHelper.WaitFor`). The namespaces did not change,
so those stacks only gained a reference.

What stays in Core because the call-model stacks use it: the non-generic `ITestContext` (every
stack's context implements it), `BusySignalPolicy` (MAUI's pages), `IDiagnosticDriver`,
`ElementGeometryExtensions.CenterOf`, `WaitHelper.Pause`, and the control-object interfaces.
A wait below a call is `Bruinet.Core.Calls.Settle.Until`: one confirmation, bounded by the
running call (`CallBudget.Within`). Composition and UAT still find a first-model page, by the
interface's name (`TestPages.IsFirstModelPage`), so neither references the old project.

**Why:** Html, MAUI and native Android derive from none of the first model (AD-010), yet Core's
public surface was mostly that model, and a reader of `IDriver` learned the opposite of the
call-model contract (the [review of 2026-10-21](https://github.com/exact-mobile-apps/test-tooling/blob/37f180a7/.my/reviews/2026-10-21-review.md)). The
project earns its place by AD-026's R4: a layer the old stacks build on and the call-model stacks
build without.

**Ruled out:**
- marking the interfaces `[Obsolete]` in Core: the old stacks would build with warnings
  everywhere, and Core would still describe two models;
- moving WPF, WinForms and Stride onto the call model first: a topic of its own, and this split
  did not wait on it. The owner asked for it on 2026-11-09 (`.my/upgrade/plan.md`); when the last
  of the three moves, this project is deleted and this record superseded.

**Broken when:** a call-model project (Html, Blazor, MAUI and its heads, native Android, Appium,
UAT, Bdd) references `Bruinet.Core.Legacy` (`SolutionTests.NoCallModelStack_ReferencesTheFirstModel`);
a first-model type is added to Core again; or a call-model stack waits with `LegacyWaitHelper`.
