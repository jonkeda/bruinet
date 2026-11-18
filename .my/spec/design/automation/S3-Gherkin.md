# S3 Gherkin scenario: automation

The style: [../400-Sentences.md](../400-Sentences.md#s3-gherkin-scenario). Entry point **B: this exists
today.** A spec's Gherkin examples run as scenarios (`docs/guides/runnable-specs.md`). This document is
about running them at the cheapest layer and keeping them short. Back to the [readme](000-Readme.md).

## What exists

- Each example runs as a scenario named `<rule id> <title>`, tagged `@<rule id>`, with traits `Rule` and
  `Spec`; a chapter's steps run before each of its examples; `## Background` before every example.
- Steps bind to built-in phrases, `[BddControl]` names and `[BddVerbs]` extension verbs; `bruinet data
  validate` lists what does not bind.
- Outlines with named Examples sections (AD-036); several users (`{user}`), several apps (mixed head);
  states (`[BddState]`) and test sets.
- `lirenet trace --results` gives each rule its state from the run.

## The simplest automation, at the right layer

The same file runs on any head. Pick by what the `Then` looks at, as the "features below the UI" guide says:

| The `Then` looks at | Tag | Head |
| --- | --- | --- |
| Data, requests, stored state | `@service` | `service`: the app's services, a stub backend, no screen. Milliseconds |
| A rendered control | none, or `@web` / `@maui` | The fast head (headless, bUnit) in the loop; the browser or device at the gate |
| Both | `@service @web` | Both, with one vocabulary |

A `@service` scenario may not use a page or control step; binding fails with `UATB013` before anything runs.

## Keeping them short

Most lines in Bouw7's examples today say what the other styles now say once:

| Lines that go | Because |
| --- | --- |
| `Given … signed in`, `I am on …`, `I open …` (about 20% of Bouw7's Gherkin) | The flow (P4) navigates; a state (`[BddState]`) or `Given the Department form` starts there |
| `Then … is shown` (about 25%) | The screen card (P1) checks a screen's parts once |
| `… is marked as required`, `… complains "…"` | The field table (T1) generates them |

What stays is what only a scenario can say: **an order of actions with an outcome that depends on it** (R5
in TOD.02: an error shows only after Save, and typing takes it away), across users and apps (P7), or with a
side effect (a sync, a write).

## What it needs

| Need | Exists? |
| --- | --- |
| Everything to run examples on every head | ✓ |
| Starting an example at a screen without navigation steps | Partly (`NavigateTo<Page>` per page, `IOpenablePage`); **new**: navigation by the flow (P4) |
| Lirenet warning when an example repeats a table row | **New**, small |

## Questions

- Rewrite existing Bouw7 examples, or only write new ones short? Proposal: rewrite the three trial specs of
  the readme in [../000-Readme.md](../000-Readme.md), then the rest when touched.
- Should a scenario be allowed to repeat a table's check for readability? Proposal: no; it may point at it
  ("Then the form refuses as its field table says") once such a phrase exists.
