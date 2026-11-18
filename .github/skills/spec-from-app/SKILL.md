---
name: spec-from-app
description: Draft a Lirenet spec for one journey of a running app - from its recordings and the app itself, in Lirenet's format, with every unclear point as an open question with a proposal - and check every example on the app before a person accepts it. Use when asked to write, draft or reverse-engineer a spec, rules or acceptance criteria for an existing app or module, or for loop stage 1 (Spec).
---

# A spec from a running app

Stage 1 of the site loop. The output is `<journey id>.spec.md` in its module's `Specs` folder (`Modules/<Module>/Specs/`, beside the pages and
tests it is about; a suite without modules keeps `specs/`), in Lirenet's format
(`Lirenet/docs/spec-format.md`: read it first), `Status: draft`, passing `lirenet check`. A person
accepts it; you never do, unless the person has said in so many words that you decide for them.

## Inputs

- The journey: an id (`B7.HRS.01`: product, module, number) and a name ("Log hours").
- The module's recordings (`Recordings/<run>/*.nodes.jsonl`, from Explore) and its page objects.
- The running app on acc, signed in as the accounts of `TestAccounts.md`.

## Steps

1. **Intent first.** Two or three sentences: who does what, and why it matters to them. Write it
   from the person's side, never the code's.
2. **Rules from what the app shows.** Read the recordings for the journey's pages: titles, buttons,
   columns, fields marked required, messages, placeholders, options of selects and filters. Each
   rule is one sentence a person can check on the screen. Name things by the words on the screen.
3. **One example per rule**, `### R<n> <title>`, a sentence on why it matters, then gherkin with
   `Given` (the account and the page), `When`, `Then`. The account in `Given` is the one whose
   data the example is true for: organizations differ (statuses, counts, column order). Examples
   that start the same way go in a chapter: `### Chapter: <name>` with the Given and When once,
   then `#### R<n> <title>` with the Then (and a When of its own where the example acts). Write the
   steps in the suite's vocabulary (`bruinet data phrases`), so the spec can run.
4. **Check every example on the app** before you hand the spec over. Facts read from recordings
   alone were wrong in Bouw7 four times (a default that was empty, three options of six, statuses of
   another organization, a button that did less than its name). A runnable spec checks itself: run
   it (`dotnet test --filter Spec=<id>`) — a draft runs too. Otherwise a throwaway probe test,
   deleted after. What a run disproves goes into the rule, not around it.
5. **Unclear points are open questions**, each with `Proposal:`. Behaviour that looks like a bug
   (a date in UTC, "Clear filters" keeping the search) is a question for the owner, not a rule.
6. **Not in this journey**: neighbouring journeys and features you saw and left out.
7. `lirenet check <the spec>` until it passes. Then set the module in `.testing/100-Loops.md` to
   `1 Spec, waiting: 👤 accept B7.XXX.01 (N open questions)`.

**A form or a list the journey touches gets its page object first**, with a member per field and per column
(`html-ui-test`, "Members have names"). The spec's "the field ..." and "the column ..." are then looked up in it, and
the bind check says when one is not there.

**How a step names a control.** By what the page calls it: `Name`, `Hourly rate`, `Create`. Add the kind's word only
where the sentence reads better with it or two things share a name (`the Create button`, `the tab Financial`). Say
where a field is (`Sales price in Financial`) only when the bind check asks for it. A form that is filled in with
more than two fields takes one step and a table, where the suite has that step:

```gherkin
When I fill in the form with:
  | Field        | Value  |
  | ------------ | ------ |
  | Contact Type | Client |
  | Hourly rate  | 45     |
```

## Accepting

The owner accepts by setting `Status: accepted <date>`; open questions must be gone by then: each
becomes a rule, a choice (`## Choices`) or a line under Not in this journey. If the owner delegated
the decision, say so in the commit and keep each decided question visible as a choice.

## Facts without a probe

A fact the recordings do not hold (a form's required fields, what an empty submit says, a list's options, a dialog's
question) comes from one command, not from a throwaway test:

```bash
bruinet scrape look --url /contact#/create --click "Create"
bruinet scrape look --url /project/create --click "field:Status"
```

`bruinet scrape find <text>` reads the recordings; `look` reads the running app, under the project's guards. What a
filled and saved form does is not a look's to find: nothing is typed. That is the example's own run.

A look says a table's rows too (how many, and the first three), so an empty list and a full one do not look the
same. A page only another account sees: `--as Reader`. A sign-in that ran out is renewed by the suite's own command
(`RenewSignIn` in `scraper.config.md`); without that row, run a test that signs in as the account.

## A module's read-only journeys as one batch

Journeys of one module that only read (lists, tabs, a page at a glance) are made together, not one after another:

1. Every look for all of them first, one command each.
2. All their specs. Run `lirenet check` on each before anything is built: it costs nothing and names what is wrong.
3. Their pages and steps, one build, and the bind test (`--filter Category=Bind`).
4. One run of all their examples (`--filter "Spec=<id>|Spec=<id>"`): the failures of all of them in one run.
5. A gate per spec (`scenario-from-spec`), with `-AtOnce`.

Four journeys of Bouw7's Hours took 12 minutes each this way, against 20 for one alone. Keep a batch to one module,
and keep a journey that writes data out of it: it is made alone, one rule at a time.
