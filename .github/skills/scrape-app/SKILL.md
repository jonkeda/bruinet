---
name: scrape-app
description: Take a running web app to page objects and tests with the Scraper's factory, group by group and unattended - set it up, cut it into modules, run loops (walk, read, draft, build, test), read why a batch stopped, fix the reason at its source, check the fix with replay, and go on until every place has a class and a test or a reason. Use when asked to scrape an app or a module with the factory, to run or continue the factory, to make page objects and tests for a whole app, or when `bruinet scrape factory run` stopped and someone asks what to do next.
---

# An app to page objects and tests

The factory does what can be counted: it walks, reads, drafts, builds, tests, puts back what fails, and stops when a
reason repeats. You do what needs judgment: decide what a reason means, change one thing at its source, and let the
tool say whether that was better. Never edit a drafted file to make it pass: the next draft would bring the fault back.

Read first: the Scraper's guide (`docs/guides/scraper.md`), the suite's `AGENTS.md` (its Never list holds in every
step), and the app's own Scraper project: `setup.md`, `modules.md`, `ledger.md`.

All commands from the app's Scraper project (`<App>.Scraper`), where `bruinet scrape …` runs it.

## 0. Set up, once

The `scraper-setup` skill: a person's answers in `setup.md`, and the app cut into modules in `modules.md`. Do not
start a walk before `Writes` has an answer. `bruinet scrape factory status` says what is owed.

## 1. Run a batch

```powershell
bruinet scrape factory run --dry-run
bruinet scrape factory run --module <Module> --baseline "Module=<Module>"
```

The first says which groups would run. The second runs them: each group in loops of six places, pages before what
opens on them. `--no-walk` drafts from the captures there are (after a fix to reading or templates, no walk is
needed); `--baseline` puts a loop back whole when a test that passed no longer does.

A loop keeps a class only when its test passed on the app, twice. What it puts back gets a reason with a code; a place it
tried and did not keep is parked, and the next run does not take it up again without `--again`.

## 2. When it stops

It stops by itself when one reason came three times in the batch, when loops that kept nothing cost minutes, when
the suite no longer builds, or when the sign-in ran out. Read `ledger.md`: its Reasons table counts them.

| Reason | What it means | Where the fix is |
| --- | --- | --- |
| `no-page-class` | A dialog's page has no class yet | Draft or write the page first; a page with nothing to check needs a class a person writes |
| `does-not-build` | The draft does not fit the suite: a member the base has already, an opener the page's class lacks | `draft-templates.md`; or the page's class, when it is a person's and lacks what the dialog's test presses |
| `did-not-open` | The test did not find what the class declares: the page or the part did not open as the test opens it | A draft kind that is missing (a dialog a row opens), or the page needs a record |
| `needs-record` | A page of one record, and no record of its kind to open it on | The suite's seed records (for Bouw7: `SeedRecords`), then `--again` |
| `label-not-found` | The form does not show a label the class declares | The reading: a sign, a holder, or the Scraper itself |
| `nothing-to-check` | The place shows nothing a test could check | Nothing: it is listed. A person writes a class when a test must name something there |
| `class-exists` | A class of that name is in the suite | Nothing: its place gets the test it lacks |
| `timeout`, `signed-out` | The environment | Run again later; renew the sign-in with a test that signs in |
| `passed-second-time` | A wait is missing; the class is kept | The template's test, or the control: find what it did not wait for |
| `passed-then-failed` | The same, the other way round: the test passed and failed when it ran again. Not kept | The same |
| `unknown` | No rule fits | Read the evidence. If it is a kind of its own, it gets a rule in `ReasonClassifier` |

## 3. One fix, measured

1. `bruinet scrape factory replay` before: it must say nothing changed. If it does not, a class is stale: draft it
   again (`places draft "<place>" --name <Class> --write`) and run its test before anything else.
2. Change one thing, at the source the table names. Signs and holders: `scraper.config.md` or the control library's
   `control-signs.md`. The draft's shape: `draft-templates.md`. A control: the `html-control` skill.
3. `bruinet scrape factory replay` after: it says which fields gained or lost a control and which classes would be
   drafted differently. A field that lost its control means the change was wrong: take it back.
4. `bruinet scrape factory keep`: every changed file against what `setup.md`'s `Improve` allows. A file outside it
   is not yours to keep: say so, and leave it for a person.
5. `bruinet scrape factory note "<what changed, and why>"`, then `factory run … --again` for the groups the reason
   stopped.

## 4. Until done

A module is done when `factory status` shows nothing owed for its groups that is not parked with a reason, its
tests pass (`dotnet test --filter Module=<Module>`), and `replay` says nothing changed. Then the next module in
`modules.md`.

Report at the end of every batch, in a few lines: what was kept, what was put back and why, what is parked and
waits for whom. `setup.md`'s `Ask` says when you may ask a person; until then, park and go on.

## Never

- Edit a drafted file by hand, or weaken what a test checks, to make a loop keep something.
- Keep a class whose test did not pass on the app.
- Run two walks or two test runs on the environment at once, or build while a test run is going.
- Change a guard of `scraper.config.md`, or `Writes` in `setup.md`, to get past a stop.
- Commit or push beyond what `setup.md`'s `Commit` says.
