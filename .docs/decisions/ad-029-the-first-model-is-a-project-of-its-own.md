# AD-029: The First Model Is A Project Of Its Own

**Superseded 2026-11-10; nothing here holds.** WPF, WinForms and Stride moved onto the call model
(`.my/upgrade/plan.md`, U3 to U6), and `Bruinet.Core.Legacy` was deleted with the last of them (U7).
Every stack stands on Core alone (AD-010), and `SolutionTests.NoProject_UsesTheFirstModel` keeps the
old project from coming back.

**What it decided** (2026-10): `Bruinet.Core` describes the call model alone. The first model's
interfaces and waits (`IDriver`, `IElement`, `IPageObject`, `LegacyWaitHelper`…), where every lookup
waited on a budget of its own, lived in `Bruinet.Core.Legacy`, which only the three stacks still on
them referenced.

**Why:** Core's public surface was mostly the first model, and a reader of `IDriver` learned the
opposite of the call-model contract (the [review of 2026-10-21](https://github.com/exact-mobile-apps/test-tooling/blob/37f180a7/.my/reviews/2026-10-21-review.md)).
Marking the interfaces `[Obsolete]` in Core was ruled out: Core would still have described two models.

The full record: this file at `98a84eff`.
