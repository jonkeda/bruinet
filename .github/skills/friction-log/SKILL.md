---
name: friction-log
description: Record what a loop stage had to work around - one row per finding in the suite's .testing/400-FrictionLog.md, with its cost, the workaround and the change it asks for in Bruinet, a control library or the suite - and at harvest time group the rows into Bruinet backlog items. Use when a test, control, spec or scrape took retries, probes or a workaround, at the end of every loop stage (stage 8, Learn), or when asked to harvest the friction log.
---

# The friction log

What makes the next module cheaper is written down here. Every stage that needed a workaround,
a retry, a probe or a person adds a row; nothing is too small if it will happen again.

## A row

`| # | Date | Stage | What happened | Cost | Workaround | Change | Where |`

- **What happened**: the symptom as it was met, with the message or the element.
- **Cost**: minutes, runs, probes, person interventions. Rough is fine; none is not.
- **Workaround**: what the stage did to go on.
- **Change**: what would have avoided it. Prefix **Done (phase N friction):** with the test that
  proves it, once it is done; `Harvest:` for an item that waits for the harvest; `None:` for a fact.
- **Where**: Bruinet, a control library, the suite, `Bouw7 (owner)` for a question to the app's owner,
  or won't fix (only the owner closes one as won't fix, with why).

## The friction step

Every phase ends by fixing its rows: each `Change` done with a test, moved to the harvest, or closed
by the owner. A phase is not done while one of its rows is open.

## Harvest

Group the rows by the change they ask for, not by symptom. For each group: the rows, the cost added
up, the change, where it goes. Rank by cost saved per module. Build the top ones, each with its test,
then measure the next module against the first (proposal, section 9).

What no single row shows (work that went fine and was slow, the same thing done by hand again) is for the
`improvement-review` skill, every few journeys.
