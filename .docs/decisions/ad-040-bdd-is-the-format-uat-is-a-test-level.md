# AD-040: BDD Is The Format And Engine, UAT Is A Test Level

**Decided:** the two words mean one thing each.

| Word | Means | Where it is used |
| --- | --- | --- |
| BDD | the format and the engine: features, runnable specs, steps, binding, running | every name a suite writer reads: types, attributes, diagnostic codes (`BDD…`), the config file (`bdd.config.md`), the artifact folder (`bdd/`), docs |
| UAT | a test level: a user's acceptance of what was built | the category `Category=UAT` (`[Uat]`), run sets and prose |

The layer split of AD-021 stays: `Bruinet.Bdd` has no UI, `Bruinet.Uat` binds steps to pages and controls. Its
project name stays; the names a suite writer meets from it are `Bdd` names (`BddScenarioTestBase`,
`[BddScenarioTheory]`, `[BddPage]`, `[BddControl]`, `[BddNavigatesTo]`). Types only Bruinet reads may keep `Uat` in
`Bruinet.Uat`.

**Why:** a feature can be a UAT or a service test below the UI, and a UAT can be a feature, a runnable spec or a C# UI
test. Naming code after the level made a service suite get "UAT" errors from an engine with no UI, kept a control
library from naming its controls (`[UatName]` sat in a package it doesn't reference), and left "spec" meaning two
things once specs became runnable. Evidence:
[proposal.md](https://github.com/exact-mobile-apps/test-tooling/blob/e3310f54/.my/bdd-uat/proposal.md).

**Ruled out:**
- a new `Uat*` name in a suite writer's surface, or a `UAT…` diagnostic code;
- two config file names for one format;
- "spec" in a type or command id that is not about runnable specs;
- renaming the `Bruinet.Uat` project or package (option B of the proposal), until its consumers are moved.

**Broken when:** `grep '"UAT[A-D]\?[0-9]\{3\}"' src` finds a code; a public `Uat*` type without `[Obsolete]` is meant
for suites; a doc calls the engine "UAT".
