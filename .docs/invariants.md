# Invariants

The short list that must not be broken. Each links the decision that explains it.

- **Core stays platform-neutral.** No platform element type in `Bruinet.Core`.
  [AD-001](decisions/ad-001-core-stays-platform-neutral.md)
- **Tests go through page objects and controls.** No `Locator`, `FindElement` or driver
  access in a test method. [AD-002](decisions/ad-002-page-objects-own-structure.md),
  [AD-003](decisions/ad-003-controls-own-repeated-interaction-behavior.md)
- **No arbitrary waits.** Wait for observable UI state, never a sleep.
  [AD-004](decisions/ad-004-wait-for-state.md),
  [call-model](contracts/call-model.md)
- **MAUI on Windows uses no physical input.** No pointer/keyboard/clipboard/foreground in
  `Bruinet.Maui.FlaUI`; every action is a UI Automation pattern or a bridge verb.
  [AD-005](decisions/ad-005-physical-input-is-opt-in.md)
- **xUnit `Assert` only.** No FluentAssertions.
  [AD-006](decisions/ad-006-xunit-assert-only.md)
- **Shared artifact layout.** `TestResults/<run-id>/suites/<suite>/`.
  [AD-007](decisions/ad-007-shared-artifact-layout.md)
- **Gestures go through the bridge, never coordinates.** Declared per element, classified,
  absent from shipping builds. [AD-008](decisions/ad-008-gestures-and-semantic-actions-go-through-ui-automation.md),
  [bridge](contracts/bridge.md)
- **App bugs stay failures.** An action runs once and is confirmed, never repeated;
  defects are reported, not absorbed. [AD-009](decisions/ad-009-app-bugs-stay-failures.md),
  [control-object](contracts/control-object.md)
- **Each stack owns its element and driver; what they share is Core's, once.** No adapters between a
  stack's interfaces and Core's: they derive. The calls layer, its exceptions, `IScope`, `ITestPage`,
  `ObjectBase<TContext>` and `ItemKey` have moved down; a layer moves only as a deliberate step.
  [AD-010](decisions/ad-010-maui-ahead-of-core.md)
- **UAT step vocabulary is attribute-declared.** Every step is a `[BddGiven]`, `[BddWhen]` or
  `[BddThen]` (base `BddStepAttribute`): a control verb on a control method, a step definition on a
  fixture or step-definition class method. No hand-written phrase table; every catalog comes from one
  discovery pass.
  [AD-011](decisions/ad-011-uat-vocabulary-is-attribute-declared.md)
- **Test data is typed and validated.** Test sets are read strictly into the app's real types
  and checked by one engine (gate and `bruinet data` alike); mock bodies are contract types,
  never anonymous (`BRUINET001`); a UAT file names its own `TestData`.
  [AD-012](decisions/ad-012-test-data-is-typed-validated-and-chosen-by-the-scenario.md)
- **Every stack's controls have MAUI's shape.** Generated from `.tpl.cs`, MAUI's `Run*` signatures and
  Core interfaces, no async members. [AD-013](decisions/ad-013-html-controls-have-mauis-shape.md)
- **One scenario file serves every head.** Head tags decide where it runs; no per-head copies.
  Apps of several heads meet only in a `mixed` suite, each binding against its own head's catalog.
  [AD-014](decisions/ad-014-one-scenario-file-serves-every-head.md)
- **A step's subject picks its app.** `Daan taps Save` binds to a declared `{user} taps {control}`
  and runs on Daan's app; no verb forms are inferred, names come from the `Apps` table only, and
  the subject sticks.
  [AD-015](decisions/ad-015-a-steps-subject-picks-its-app.md)
- **Phrase arguments are typed by the method.** One invariant reader for step values, Examples
  and table cells (`12-May-2026`, `1,250.75`), checked at bind time; a broken phrase fails only the
  steps that use it; the gate and `bruinet data` check alike.
  [AD-016](decisions/ad-016-phrase-arguments-are-typed-by-the-method.md)
- **Callable phrases are sequences, not programs.** A `## Step:` is steps with placeholders; no
  branch, loop or variable; bound where it is called, problems reported at the call; no shadowing.
  [AD-017](decisions/ad-017-callable-phrases-are-sequences-not-programs.md)
- **A tier is a head.** A feature below the UI runs on the opt-in `service` head from the same
  files; a UI step there is a bind error (UATB013); a scenario on several heads counts for each tier.
  [AD-020](decisions/ad-020-a-tier-is-a-head.md)
- **Bruinet.Bdd knows no UI.** It references no page object, control or UI-automation type, and not
  `Bruinet.Uat`; page names are plain strings, and page scope comes through `IStepScope`.
  [AD-021](decisions/ad-021-bdd-under-uat.md)
- **Bdd is the main; Uat is a specialization.** Only `Bruinet.Uat` and its tests have `Uat` names
  (and `UatPlatformsAttribute`, for now). The engine, the Presenter, the tools and the IDE
  extensions have `Bdd` names. [AD-021](decisions/ad-021-bdd-under-uat.md)
- **The IDEs run the gate's engine.** An IDE never parses or binds a scenario itself. It asks
  `bruinet lsp`, which checks with the gate's own code and words, and loads a suite
  only through `Bruinet.Bdd.Workspace`. A scenario runs by its `BddScenario` address.
  [AD-022](decisions/ad-022-the-ides-run-the-gates-engine.md)
- **Gherkin is fenced; the rest is prose.** A step is read only inside a ` ```gherkin ` block; any
  other Markdown, step-like text included, is prose. The engine reads the file through Markdig.
  [AD-023](decisions/ad-023-a-feature-file-is-markdown.md)
- **Replay answers only what it recorded, and the database judges the gate.** A command not recorded
  is a miss, never a made-up result; a recording is made against the real database, stamped, and
  reviewed before it goes in. [AD-031](decisions/ad-031-a-replayed-test-answers-from-the-database-it-recorded.md)
- **The generator output is not hand-edited.** `.gen.cs` mirrors `.tpl.cs` through
  `Bruinet.Generator`. [control-object](contracts/control-object.md)
