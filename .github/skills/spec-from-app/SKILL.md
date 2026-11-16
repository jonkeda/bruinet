---
name: spec-from-app
description: Draft a Lirenet spec for one journey of a running app - from its recordings and the app itself, in Lirenet's format, with every unclear point as an open question with a proposal - and check every example on the app before a person accepts it. Use when asked to write, draft or reverse-engineer a spec, rules or acceptance criteria for an existing app or module, or for loop stage 1 (Spec).
---

# A spec from a running app

Stage 1 of the site loop. The output is `specs/<journey id>.spec.md` in Lirenet's format
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
7. `lirenet check specs/` until it passes. Then set the module in `.testing/100-Loops.md` to
   `1 Spec, waiting: 👤 accept B7.XXX.01 (N open questions)`.

## Accepting

The owner accepts by setting `Status: accepted <date>`; open questions must be gone by then: each
becomes a rule, a choice (`## Choices`) or a line under Not in this journey. If the owner delegated
the decision, say so in the commit and keep each decided question visible as a choice.
