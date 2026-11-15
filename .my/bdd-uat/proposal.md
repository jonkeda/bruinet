# BDD and UAT: one meaning each

Status: **option A accepted 2026-11-15**; BU1–BU6 built 2026-11-15 (Bouw7 moved in BU5); BU7, BU8 open. Written before runnable specs (`.my/runnablespecs/design.md`),
which starts on the new names.

## Why now

AD-021 split the code in 2026-09: `Bruinet.Bdd` is the engine, with no UI; `Bruinet.Uat` is the UI layer on top. The
types follow it. What a suite writer meets still mixes the two words: error codes, docs, config files, attributes. And
runnable specs is about to add a third word ("spec") and a new attribute (`[BddControl]`) next to `[UatName]`, which
does nearly the same thing. Built as things are, it deepens the mix. Sorted first, it lands with the right names.

## What the words mean

| Word | Means | Answers | Where it belongs |
| --- | --- | --- | --- |
| **BDD** | the format and the engine: features, specs, steps, binding, running | *how* a test is written and run | type, attribute, code, doc and file names |
| **UAT** | a test level: a user's acceptance of what was built, on the real UI | *why* a test exists | a category (`Category=UAT`), a run set, prose |
| **UI** (or a head) | where steps meet pages and controls | *what* a step drives | the UI layer's names, the `Head` row |

So "UAT" stops being a name of code. A UAT can be a feature, a runnable spec, or a C# UI test; a feature can be a UAT
or a service test below the UI. That is already true today; the names say otherwise.

AD-021 said "Uat names are for `Bruinet.Uat`": the layer *is* UAT. This proposal says the layer is the UI binding of
BDD, and UAT is a purpose. That is the one real decision here; the rest follows from it.

## Inventory

Counted at the working tree of 2026-11-15.

### What already holds

- **Types.** `Bruinet.Bdd` has 142 public `Bdd*` types and no `Uat*` type. `Bruinet.Uat` has 30 `Uat*` types and
  2 `Bdd*` (the runner host). AD-021 holds in the type names.
- **References.** `Bruinet.Bdd` references nothing of `Bruinet.Uat` (one `InternalsVisibleTo`, two comments).
- **Glossary.** `docs/guides/bdd-glossary.md` uses BDD words, matched to Cucumber's.
- **Tools.** The CLI (`bruinet data phrases`, `bruinet scenario …`), the language server, the IDE extensions ("Bruinet
  BDD") and `Bruinet.Bdd.Workspace` have Bdd names.

### What mixes

| # | Where | Today | The mix |
| --- | --- | --- | --- |
| M1 | Error codes | 62 distinct `UAT…` codes raised in `Bruinet.Bdd`: parser `UAT0xx` (32), arguments `UATA` (11), binding `UATB` (11), callables `UATC` (8). `Bruinet.Uat` raises 9, of which 5 only there (`UATB008`, `UATB009` for page scope, `UATD001`–`003` for discovery). No `BDD` code. | A service suite, with no UI, gets "UAT" errors from the engine. |
| M2 | Attributes | `[BddGiven]`, `[BddWhen]`, `[BddThen]` in `Bruinet.Bdd.Abstractions`; `[UatName]`, `[UatNavigatesTo]`, `[UatAction]` in `Bruinet.Uat` | A page object carries both families. `UatName` can't be used by a control library (`Bruinet.Html`, `Bruinet.Web.Vuetify2`): they reference Core and Abstractions, not `Bruinet.Uat`. `UatAction` is read by discovery and used by nothing else. |
| M3 | Config file | `uat.config.md` for a UI suite, `bdd.config.md` for a service suite (`BddFileFormat`) | Same format, two names; the `Head` row already says which kind. Config keys too: `RequireExplicitUatAttributes`. |
| M4 | Docs | `uat-grammar.md` ("UAT Scenario Grammar"), `uat-step-definitions.md`, `uat-template-guide.md`, `quick-start-uat.md`, `.docs/contracts/uat-apps-host.md`, AD-011's title | The grammar and the step definitions are the engine's, not the UI's. Beside them: "BDD Glossary", "Bruinet BDD in VS Code". |
| M5 | "Spec" | `UatSpecCommandCatalog` (the generic catalog of built-in phrases, command ids `Spec.*`), `UatSpecFormatTestBase` (checks a feature file's metadata) | "Spec" there means "the description, not the runtime". Runnable specs makes "spec" a Frainet spec file. Two meanings in one engine. |
| M6 | Test base | `UatScenarioTestBase`, `UatScenarioTheory` (also in `Bruinet.Bdd.Workspace`), `UatRuntimeValidationOptions` | A service suite runs on `UatScenarioTestBase` too (AD-021: refusing a UI step needs the UI patterns). The base every suite uses has the UI layer's name. |
| M7 | Elsewhere | `UatDirectory` (artifact folder `uat/`) in Core and Bdd's config; `UatPlatformsAttribute` in Bruinet.Maui; `UatConfigFileName` in the Scraper; `[Uat]` trait writing `Category=UAT` | The trait is right (a test level). The artifact folder and the platforms attribute are engine or head facts with the level's name. |

### Who meets them

| Consumer | Uses |
| --- | --- |
| Bruinet's own tests | 53 files name a `Uat*` type; 14 use `Category=UAT` |
| Samples | 13 files |
| Tools | 13 files (Presenter, CLI, workspace) |
| Docs and extensions | 13 files quote a `UAT…` code |
| Bouw7 | `UatScenarioTestBase`, `UatScenarioTheory`, `UatRuntimeValidationOptions`, `uat.config.md`; its own `Bouw7UatFixture`, `Support/Uat/` |

The suites outside this repository that use Bruinet (named by the owner, surveyed 2026-11-15, their `external/`,
`bin/` and `obj/` left out):

| Suite | Features | `[UatName]` / `[UatNavigatesTo]` | Test base and others | `Category=UAT` | `UAT…` codes quoted |
| --- | --- | --- | --- | --- | --- |
| `Private/tomorrow` (Studio and App UI tests) | 69 | 102 / 50, in 44 files | `UatScenarioTheory` 12, `UatScenarioTestBase`, `UatRuntimeValidationOptions`; 2 `uat.config.md` | 3 files | none |
| `Private/Kofflard` | 8 | 20 / 6 | `UatScenarioTestBase` 2, `UatScenarioTheory` 2, `UatRuntimeValidationOptions` 2, `UatPlatforms` 1 | 1 file | none |
| `MainConstruction/.../Exact.Construction.UITests` | 306 | none | `UatScenarioTestBase`, `UatScenarioTheory`, `UatRuntimeValidationOptions` 49 each; `UatSpecFormatTestBase` 26 | 24 files | none |
| `MainConstruction/.../Exact.Core.Construction.UITest` | 0 | none | none (pages and controls only) | none | none |

`UatCollections` and `UatSplitTests` in tomorrow are its own names, not Bruinet's. No suite quotes a code, filters on
the `uat/` folder, or uses `UatSpecCommandCatalog`.

What it means: the attributes (A2) touch about 180 lines in two suites, the test base (A6) about 150 in three, the
metadata base (A3) 26 in one. So the old names stay one release as `[Obsolete]` aliases, and BU7 moves the four
suites.

## Options

### A. Finish AD-021 (no project rename)

Everything a suite writer reads gets its BDD name; the `Bruinet.Uat` project and its internal types keep theirs.

| # | Change | Breaks a consumer? |
| --- | --- | --- |
| A1 | **Codes.** Every code becomes `BDD…`, same letter and number: `UAT004` → `BDD004`, `UATB001` → `BDDB001`, `UATD001` → `BDDD001`. One series, whatever layer raises it. The docs' code tables say "was `UATB001`" for a release. | Only a suite that filters or suppresses a code by text (none known). |
| A2 | **Attributes** move to `Bruinet.Bdd.Abstractions`: `[UatName]` on a page → `[BddPage("Login")]`; on a control property → `[BddControl("Role")]` (a control name with no placeholder, the same attribute runnable specs adds); `[UatNavigatesTo]` → `[BddNavigatesTo]`. `[UatAction]` is removed. The old ones stay one release, `[Obsolete]`, still read. | Compiles with warnings; `api-upgrade` rows. |
| A3 | **"Spec" freed.** `UatSpecCommandCatalog` → `UatGenericCatalog` (built under that name, after its `IsGeneric`; command ids `Generic.*`, as the projections already call them; no suite uses it, so it keeps the layer's name); `UatSpecFormatTestBase` → `BddFeatureFormatTestBase` (built under that name) (a suite writer's base, so a Bdd name; Exact.Construction uses it 26 times). | The metadata base: warnings, an `[Obsolete]` subclass, `api-upgrade` rows. Ids in saved Presenter state, if any (check). |
| A4 | **Docs.** `uat-grammar.md` → `bdd-grammar.md` ("Feature and spec grammar"); `uat-step-definitions.md` → `step-definitions.md`; `uat-template-guide.md` → `ui-suite-guide.md`; `quick-start-uat.md` → `quick-start-bdd.md`; AD-011's title. "UAT" stays where the text means the test level. | No; links repointed. |
| A5 | **Config.** One name, `bdd.config.md`; the `Head` row says UI or service. `uat.config.md` is still read, with a warning naming the new name. Keys: `RequireExplicitBddAttributes`, the old key still read. | No, until the old name is dropped. |
| A6 | **Test base.** `BddScenarioTestBase<TFixture>`, `[BddScenarioTheory]` and `BddRuntimeValidationOptions` in `Bruinet.Uat`; the `Uat` names stay as subclasses, `[Obsolete]`, one release. The refusal of UI steps for a service suite stays where it is. | Warnings; `api-upgrade` rows. |
| A7 | **The rest.** `UatDirectory` → `BddDirectory` (folder `bdd/`, the old folder read by the report tools for one release); `UatPlatformsAttribute` → `BddHeadsAttribute` (it lists heads; Kofflard uses it once). `[Uat]` and `Category=UAT` stay: they are the test level. | Small; `api-upgrade` rows. |
| A8 | **Decision.** AD-040 "BDD is the format and engine; UAT is a test level", superseding AD-021's naming rule (its layer split stays). AD-021 and AD-011 point to it. | No |

After A, the words a suite writer meets are BDD everywhere, UAT only as a category, and `Bruinet.Uat` only in a
project reference.

### B. A and the project rename

`Bruinet.Uat` → `Bruinet.Bdd.Ui`, namespace and package too, and its types lose `Uat` (`UatPageTracker` →
`BddPageTracker` or `PageTracker`). The layer split of AD-021 stays exactly as it is, under a name that says what it
is.

- **For:** no `Uat` name left in code; the package list reads `Bruinet.Bdd`, `Bruinet.Bdd.Abstractions`,
  `Bruinet.Bdd.Ui`, `Bruinet.Bdd.Workspace`.
- **Against:** a new package id is a break for every consumer (a reference to change, not only a warning), and 30 types
  and 79 files move for a name. AD-021 ruled out nothing about the project name, but its rename plan kept it on
  purpose.

### C. Do nothing, write it down

Keep the names; add the meanings to the glossary and AD-021. Cheapest; runnable specs then adds `spec` and
`[BddControl]` beside `UatSpecCommandCatalog` and `[UatName]`, and the next agent asks the same question.

## Recommendation

**A now, B later if ever.** Accepted: A. A fixes everything a suite writer reads, mostly without breaking a build (obsolete
aliases for one release), and it is the base runnable specs needs: A2 *is* the `[BddControl]` decision applied to
fixed names, and A3 frees "spec". B is a rename for the code's readers only; decide it when the consumers are known.

## Plan

| Step | What | Done when |
| --- | --- | --- |
| BU1 | AD-040 (A8); this proposal accepted | The meanings are written once, in `.docs/` |
| BU2 | Codes (A1): one table of codes in `Bruinet.Bdd`, renamed; docs and the extensions' code lists | No `"UAT` code string in `src/`; the docs' code tables list the old code beside the new |
| BU3 | "Spec" freed (A3) | No `Spec` in a type or command id of the engine or UI layer |
| BU4 | Attributes (A2), with `[BddControl]` without placeholders; obsolete `UatName`, `UatNavigatesTo`; `UatAction` removed | Bruinet's tests and samples use only `Bdd*` attributes; a Vuetify2 control can declare a name |
| BU5 | Test base (A6), config (A5), the rest (A7) | Bouw7 builds and its scenarios pass with no `Uat*` type and `bdd.config.md`; an old suite still builds with warnings |
| BU6 | Docs (A4); `api-upgrade` rows for every rename; CHANGELOG | `docs/README.md` links the new names; no broken link |
| BU7 | The four suites moved to the new names (Bouw7 in BU5): tomorrow, Kofflard, Exact.Construction.UITests; Exact.Core.Construction.UITest has nothing to move. One commit per suite, in its own repository, when its owner says so | Each builds with no `Obsolete` warning from Bruinet, its scenarios bind (`bruinet scenario check` or a run), `uat.config.md` renamed |
| BU8 | The `[Obsolete]` aliases removed, the release after BU7 | No `Uat*` name in a suite writer's surface |

Then runnable specs' RS1 starts on the new names. Each step is one commit; BU2 to BU5 each run the Bdd, Uat and UI
test tiers that their files reach, and BU5 Bouw7's scenarios on acc.

## Decided

- **Consumers** (2026-11-15, the owner): the four suites in the table above, and Bouw7.
- **One code series.** Every code becomes `BDD…`, the UI layer's too. A writer meets them in one run output,
  `UATB008` and `UATB009` are binding codes like `UATB001`, and no consumer quotes a code, so nothing holds the old
  ones. The docs list the old code beside the new for one release.
- **`Category=UAT` stays.** It is the test level, the one place the word is right, and 28 files in the consumers
  filter on it.
- **The artifact folder becomes `bdd/`** (A7). No consumer reads `uat/` by name, and keeping it would leave
  `UatDirectory` in Core, below the UI layer. Bruinet's own report tools and scripts read both for one release.
