# .docs

The durable layer to read **before changing Bruinet**: decisions, invariants and
contracts - what must stay true. Not a description of the code; that can be regenerated
from the source and goes stale. The reasoning: [the documentation plan](https://github.com/exact-mobile-apps/test-tooling/blob/fe5d2b66/.my/documentation/plan.md).

How this differs from the other trees:

| Tree | Holds | Reader |
| --- | --- | --- |
| **`.docs/`** | decisions, invariants, contracts: what must stay true | someone **changing** Bruinet |
| `docs/` | getting started, guides, control catalogue, how to run | someone **using** Bruinet |
| `.my/` | plans, analyses, reviews for work in flight | whoever is doing that work |
| `docs2/` | archive | nobody, until needed |

## Invariants

- [Invariants](invariants.md) - the short list, each linking its decision.

## Decisions

- [AD-001: Core stays platform-neutral](decisions/ad-001-core-stays-platform-neutral.md)
- [AD-002: Page objects own structure](decisions/ad-002-page-objects-own-structure.md)
- [AD-003: Controls own repeated interaction behavior](decisions/ad-003-controls-own-repeated-interaction-behavior.md)
- [AD-004: Wait for state](decisions/ad-004-wait-for-state.md)
- [AD-005: Physical input is opt-in](decisions/ad-005-physical-input-is-opt-in.md)
- [AD-006: xUnit Assert only](decisions/ad-006-xunit-assert-only.md)
- [AD-007: Shared artifact layout](decisions/ad-007-shared-artifact-layout.md)
- [AD-008: Gestures and semantic actions go through UI Automation](decisions/ad-008-gestures-and-semantic-actions-go-through-ui-automation.md)
- [AD-009: App bugs stay failures](decisions/ad-009-app-bugs-stay-failures.md)
- [AD-010: MAUI, Html and native Android ahead of Core, on purpose](decisions/ad-010-maui-ahead-of-core.md)
- [AD-011: UAT step vocabulary is declared by attributes on Core interfaces](decisions/ad-011-uat-vocabulary-is-attribute-declared.md)
- [AD-012: Test data is typed, validated, and chosen by the scenario file](decisions/ad-012-test-data-is-typed-validated-and-chosen-by-the-scenario.md)
- [AD-013: Html controls have MAUI's shape](decisions/ad-013-html-controls-have-mauis-shape.md)
- [AD-014: One scenario file serves every head](decisions/ad-014-one-scenario-file-serves-every-head.md)
- [AD-015: A step's subject picks its app](decisions/ad-015-a-steps-subject-picks-its-app.md)
- [AD-016: Phrase arguments are typed by the method](decisions/ad-016-phrase-arguments-are-typed-by-the-method.md)
- [AD-017: Callable phrases are sequences, not programs](decisions/ad-017-callable-phrases-are-sequences-not-programs.md)
- [AD-018: The Presenter workspace is the UAT project](decisions/ad-018-the-presenter-workspace-is-the-uat-project.md)
- [AD-019: Phrases have scopes and namespaces](decisions/ad-019-phrases-have-scopes-and-namespaces.md)
- [AD-020: A tier is a head](decisions/ad-020-a-tier-is-a-head.md)
- [AD-021: Bruinet.Bdd under Bruinet.Uat](decisions/ad-021-bdd-under-uat.md)
- [AD-022: The IDEs run the gate's engine](decisions/ad-022-the-ides-run-the-gates-engine.md)
- [AD-023: A feature file is Markdown; Gherkin is fenced](decisions/ad-023-a-feature-file-is-markdown.md)
- [AD-024: A recording is a paced gate run](decisions/ad-024-a-recording-is-a-paced-gate-run.md)
- [AD-025: A suite runs in a process of its own](decisions/ad-025-a-suite-runs-in-a-process-of-its-own.md)
- [AD-026: A project earns its place](decisions/ad-026-a-project-earns-its-place.md)
- [AD-027: The Scraper records into the project and calls no model](decisions/ad-027-the-scraper-records-into-the-project-and-calls-no-model.md)
- [AD-028: One command line](decisions/ad-028-one-command-line.md)
- [AD-029: The first model is a project of its own](decisions/ad-029-the-first-model-is-a-project-of-its-own.md) (superseded 2026-11-10: every stack is on the call model)
- [AD-030: A Blazor suite runs without a browser, and the browser judges the gate](decisions/ad-030-a-blazor-suite-runs-without-a-browser.md)
- [AD-031: A replayed test answers from the database it recorded, and the database judges the gate](decisions/ad-031-a-replayed-test-answers-from-the-database-it-recorded.md)
- [AD-032: A MAUI suite runs without a device, and the device judges the gate](decisions/ad-032-a-maui-suite-runs-without-a-device.md)
- [AD-033: The build is the first check](decisions/ad-033-the-build-is-the-first-check.md)
- [AD-034: A dependency is replayed, stubbed or copied by its kind](decisions/ad-034-a-dependency-is-replayed-stubbed-or-copied-by-its-kind.md)
- [AD-035: An API contract is the DTO the app reads, and the real service judges it](decisions/ad-035-an-api-contract-is-the-dto-the-app-reads.md)
- [AD-036: An outline has named Examples sections, and a row keeps its number](decisions/ad-036-an-outline-has-named-examples-sections.md)
- [AD-037: A MAUI runner is chosen apart from the platform, and DevFlow judges no gate](decisions/ad-037-a-maui-runner-is-chosen-apart-from-the-platform.md)
- [AD-038: A test names its account and never holds a secret](decisions/ad-038-a-test-names-its-account-and-never-holds-a-secret.md)
- [AD-039: A tab's content is a page object of its own](decisions/ad-039-a-tabs-content-is-a-page-object-of-its-own.md)

## Contracts

- [Call model](contracts/call-model.md) - one call, one budget, one log pair (R0-R9); MAUI, Html and native Android.
- [Control object](contracts/control-object.md) - the `.tpl.cs` / `.gen.cs` contract,
  act-once and `Confirm`.
- [Bridge](contracts/bridge.md) - `GestureAutomation.Verbs`: what an app must publish.
- [Apps in a test](contracts/uat-apps-host.md) - `TestAppSet` and `IUatAppsHost`: one table of apps, started
  lazily, evidence from every started app.
