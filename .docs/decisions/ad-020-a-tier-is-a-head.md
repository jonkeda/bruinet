# AD-020: A Tier Is A Head

A feature below the UI runs on its own head, `service`, from the same `*.feature.md` files as the
UI heads (AD-014). A suite whose config (`bdd.config.md`) says `Head | service` drives the app's
services, database and fake backend without a screen: the Todo sample's `TodoServiceFixture` starts a
new headless `TodoHost` per scenario.

- **Opt-in heads.** `mixed`, `service` and `api` (AD-046) run only the scenarios tagged for them. A scenario tagged
  `@service` alone is skipped on `maui` and `web`; `@service @maui` runs on both; an untagged
  scenario never runs on `service`. A suite lists only the scenarios its head runs, so a shared
  folder does not show another head's cases as skipped.
- **No screen on `service`.** A step that binds to a page step, a control verb or a page phrase is a
  bind error there, **UATB013**, before anything runs. The service suite still binds against the UI
  step patterns to recognise them, which is why it runs on `Bruinet.Uat`'s test base (AD-021).
- **Service step definitions use the UI heads' words** where the same thing happens
  (`I restart the app`, `I create a todo {title}`). Steps that reach past the screen (backend,
  device, stored data) are one set for every head, behind the fixture's arrangement interface.
- **One feature, several tiers.** A scenario's head tags are its tiers in the coverage report:
  `service` counts as Integration, a UI head (or none) as UI hermetic. A scenario tagged
  `@service @maui` counts for both, and binding it on both suites is what keeps the two
  vocabularies from drifting.
- **What goes to `service`.** Gherkin when someone other than the developer reads it; C# when it is
  plumbing: contract shapes, tombstones, retry timing.

**Why:** the same journey was checked twice, in a UI scenario and a C# integration test with its
own names, and the coverage report counted only the C#. A tag moves a check to the cheapest tier
that can see it, without rewriting it. Evidence (at e44f897):
[proposal.md](https://github.com/exact-mobile-apps/test-tooling/blob/e44f897/.my/unit/proposal.md) sections 1-2 and D1-D4,
[plan.md](https://github.com/exact-mobile-apps/test-tooling/blob/e44f897/.my/unit/plan.md) phases 1-3 and 8.

**Ruled out:** a Gherkin runner for unit tests; a service scenario file beside the UI one; a
service step with different words for the same action; leaving page steps out of the service
catalog (a UI step would then be "no binding", not "a UI step"); counting a scenario for one tier
only when it runs on two.

**Broken when:** an untagged scenario runs on `service`; a `@service` scenario binds a page step,
control verb or page phrase without UATB013; a service step and a UI step say the same thing in
different words; the coverage report counts a `@service @maui` scenario for one tier; a service
suite shares one host, database or backend between scenarios.
