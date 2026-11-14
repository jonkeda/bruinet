---
name: scenario-from-spec
description: Turn an accepted Frainet spec into tests - one per example, tagged with its rule id, as the account the example names, using the suite's page objects - until frainet trace reaches every rule and the tests pass on acc. Use when asked to write tests or scenarios for a spec, to cover a spec's rules, or for loop stage 5 (Scenarios).
---

# Tests from a spec

Stage 5 of the site loop. Input: a spec with `Status: accepted …`. Output: tests that `frainet trace`
finds for every rule, passing on the app.

## C# test or `.feature.md`

- **C# test (the default).** One `[Fact]` per example, named after the example's title
  (`### R4 Part of a name is enough` → `PartOfANameIsEnough`), with `[Trait("Rule", "<id>.R4")]`. The
  class is `<Journey name>Tests` in `Modules/<Module>/Tests/`, with `[Module("<Module>")]` and `[Live]`.
- **`.feature.md`** only when every phrase of the example already binds to a step (the UAT runner's
  phrases and the suite's). Tag each scenario `@<id>.R<n>`. A phrase that would need a new step
  binding is a reason to write C# instead, not to invent steps.

## Rules for the test

- Open a session as the example's account (`Bouw7Session.Open("Creator")`); Reader never writes.
- Arrange, act, assert as the example's Given, When, Then. Use the page objects and their controls
  only: no Playwright, no `Find*`, no sleeps. Waits are the controls' `Assert*`/`Wait*`.
- A member the page object lacks is added to the page's or component's partial class (a control
  in a shadow root reads its text from inside it). Unit-test a new control behaviour where you can.
- **Data.** A test that creates data puts the run marker in it (`BRN-MMdd-HHmm`), removes what it
  made in a `finally`, and first removes what an earlier failed run left.
- Exact counts only where only the tests change the data (the Creator's own organization).

## Gate

```bash
frainet trace specs/<id>.spec.md <suite>/Modules
dotnet test <suite> --filter Module=<Module> --logger trx
frainet trace specs/<id>.spec.md <suite>/Modules --results <suite>/TestResults
```

Every rule `Passing`. A failure that is the app's (not the test's) stays a failure: hand it to
`run-triage`. What the spec got wrong goes back to the spec (and its owner), not into the test.
