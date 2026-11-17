---
name: scenario-from-spec
description: Turn an accepted Lirenet spec into tests - one per example, tagged with its rule id, as the account the example names, using the suite's page objects - until lirenet trace reaches every rule and the tests pass on acc. Use when asked to write tests or scenarios for a spec, to cover a spec's rules, or for loop stage 5 (Scenarios).
---

# Tests from a spec

Stage 5 of the site loop. Input: a spec with `Status: accepted …`. Output: tests that `lirenet trace`
finds for every rule, passing on the app.

## Make the spec run (the default), C# where it says something badly

- **The spec runs itself** (`docs/guides/runnable-specs.md`). The suite's config names the specs' folder
  (`Specs | ../specs, Specs`) and the project copies the spec; each example is then a scenario named and
  tagged with its rule. Your work is to make every step bind: `bruinet data validate Specs/<id>.spec.md`
  from the output folder lists the steps that don't.
- **Make a step bind, in this order:** a word the vocabulary already has (`bruinet data phrases`: say
  the step that way, the rule's meaning unchanged); a control name on the component
  (`[BddControl("the filter above {column}")]` on its method); an extension verb written once for the
  control's type (`[BddVerbs]`, `this TableControl<TScope>`); a page phrase for a page's own workflow.
  Never a phrase with "should": Lirenet's check refuses it in a spec.
- **C# test** only for what an example says badly (a race, a layout, a polled value): one `[Fact]` with
  `[Trait("Rule", "<id>.R4")]` in `Modules/<Module>/Tests/`. A rule is checked once: its example or a
  C# test, never both; delete the C# test when the example runs.

## Rules for the test

- Open a session as the example's account (`Bouw7Session.Open("Creator")`); Reader never writes.
- Arrange, act, assert as the example's Given, When, Then. Use the page objects and their controls
  only: no Playwright, no `Find*`, no sleeps. Waits are the controls' `Assert*`/`Wait*`.
- A member the page object lacks is added to the page's or component's partial class (a control
  in a shadow root reads its text from inside it). Unit-test a new control behaviour where you can.
- **Data.** A test that creates data puts the run marker in it (`BRN-MMdd-HHmm`), removes what it
  made in a `finally`, and first removes what an earlier failed run left.
- Exact counts only where only the tests change the data (the Creator's own organization).

## While writing: one rule

Run the rule you are on, not the spec: a build and that rule's tests, in well under a minute. From Bruinet's root:

```bash
pwsh tools/Scripts/site-loop.ps1 -Suite <suite's folder> -Rule <id>.R4
```

It fails when no test names the rule. It leaves `100-Loops.md` alone. Go to the gate only when every rule has
passed this way once.

## Gate

```bash
pwsh tools/Scripts/site-loop.ps1 -Suite <suite's folder> -Spec <id> -Runs 3
```

which is, per run: the spec's own tests (`dotnet test <suite> --filter "Spec=<id>"`, or `Module=<Module>` for a spec
that does not run from its examples) and `lirenet trace specs/<id>.spec.md <suite>/Modules --runnable --results …`;
the module's code tests once, beside it.

Every rule `Passing`. A failure that is the app's (not the test's) stays a failure: hand it to
`run-triage`. What the spec got wrong goes back to the spec (and its owner), not into the test.

## A step that goes to another page

A step that leaves the page says where it lands, or the steps after it are looked for on the page it left
("The Contacts page has no control 'the tab Financial'"). Two ways, both in code:

- a control that always goes to one page: `[BddNavigatesTo(typeof(OrderPage))]` on its property;
- a step of the page itself that returns the page it lands on:
  `[BddWhen("I open the contact {name}")] public ContactViewPage OpenContact(string name)`. Use this for a page whose
  address holds an id, which "I open the … page" cannot open.

A fixture's step cannot say it: keep the fixture for what makes or removes data.
