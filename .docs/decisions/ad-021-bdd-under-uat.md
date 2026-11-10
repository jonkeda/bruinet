# AD-021: Bruinet.Bdd Under Bruinet.Uat

The feature-file engine is `Bruinet.Bdd`; `Bruinet.Uat` is UAT on top of it and depends on it.

| `Bruinet.Bdd` (no UI) | `Bruinet.Uat` (UI on top) |
| --- | --- |
| parser and models, values, argument types, tables and table binding, `BddSets` | page and control discovery (`UatReflectionRuntime`), name inference, `[UatName]`, `[UatNavigatesTo]` |
| the catalog and the binder, callable steps, namespaces and `Using` | page scope: `UatPageSet`, `UatPageModel`, `UatPageTracker`, control verbs (`UatCatalogBuilder`) |
| config, feature files and source, execution and runner, hooks | apps from a `TestAppSet` (`IUatAppsHost`), sessions' steps |
| the step-definition runtime: fixture steps, `[BddStepDefinitions]`, argument binding | `UatRuntime`, `UatScenarioTestBase` (screenshots), `UatScenarioCheck` |
| `IBddClock`, `IBddSessionHost` (no UI in them) | target conventions, the spec catalog |

**Page names, not page types.** A catalog pattern keeps the page it belongs to and the page it
ends on as plain strings (`Page`, `PageAfter`, `NamesPage`), and a control verb as plain data
(`BddControlVerb`): the binder compares them, never resolves them. What gives a name meaning (the
page's type, its controls, where a control goes, which page a step is on) is `Bruinet.Uat`'s,
reached through the catalog's `Scope` (`IStepScopeModel`, one `IStepScope` per walk).

**Bdd is the main; Uat is a specialization of it.** `Uat` names are for `Bruinet.Uat` and its tests
only (and `UatPlatformsAttribute` in Bruinet.Maui, for now). Everything else takes `Bdd` names, types
and projects alike:
- the engine (`BddBinder`, `BddCatalogSet`, `IBddClock`) and `BddEffectiveStepKeyword` in Bruinet.Bdd.Abstractions;
- the Presenter's types;
- the tooling library `Bruinet.Bdd.Workspace` (it holds the preview, namespace `Bruinet.Bdd.Preview`) and the tool `bruinet` (`tools/Bruinet.Cli`, AD-028), whose `lsp` command is the language server;
- the IDE extensions, "Bruinet BDD".

A Bdd tool may use `Bruinet.Uat`: the workspace builds a UI suite's catalogs through it, as a
general tool handles the specialization too. The run address trait is `BddScenario`. The
first move kept the old names; they followed on 2026-09-28 ([the rename plan](https://github.com/exact-mobile-apps/test-tooling/blob/fb07253b/.my/uat/rename/plan.md)).

A step-definition project with no UI (the Todo sample's TestSupport) references `Bruinet.Bdd`
only. A service suite still runs on `UatScenarioTestBase`, because refusing a UI step (AD-020,
UATB013) needs the UI step patterns.

**Why:** three quarters of the old `Bruinet.Uat` had no UI in it, and an integration project had to
take the page machinery to run a feature. The split makes the line visible and keeps UI knowledge
from spreading into the engine. Evidence (at e44f897):
[proposal.md](https://github.com/exact-mobile-apps/test-tooling/blob/e44f897/.my/unit/proposal.md) section 3,
[plan.md](https://github.com/exact-mobile-apps/test-tooling/blob/e44f897/.my/unit/plan.md) phase 6 (the 6.3 options A-C; C chosen).

**Ruled out:** an opaque scope object on every pattern in place of page names (option A: more
seams for no gain yet); keeping the catalog and binder in `Bruinet.Uat` (option B: a service
project would need the UI package); keeping `Uat` names below `Bruinet.Uat` (the first move did,
and the names then hid the layers).

**Broken when:** `Bruinet.Bdd` references a page object, control or UI-automation type, or
`Bruinet.Uat`; page scope is decided in `Bruinet.Bdd` instead of through `IStepScope`; a project
with no UI takes `Bruinet.Uat` only for step definitions, tables or hooks; a new `Uat*` type, or a
`Bruinet.Uat.*` project, outside Bruinet.Uat.
