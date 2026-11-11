---
name: docs-upkeep
description: Run the closing documentation checklist when a piece of work lands in Bruinet. Use at the end of a task or phase to check whether a decision needs recording, whether `docs/` describes a surface that moved, whether anything in `.docs/` now contradicts the code, and whether the `.my/` plan is marked done. This is the step that is otherwise forgotten.
---

# Docs upkeep at the close of work

Run this when a piece of work lands. It is short by design - four questions, each with one
action.

## Checklist

1. **Did a decision get made?** If the work ruled something out, record it with the
   [decision-record](../decision-record/SKILL.md) skill. If it only did something, skip.

2. **Did a user-facing surface move?** If a control, page API, command, setting or artifact
   layout changed, update the matching page under `docs/` and, if the set of docs changed,
   `docs/README.md`. `docs/` describes what someone *using* Bruinet sees - keep it true.

3. **Does anything in `.docs/` now contradict the code?** Re-read the decisions, contracts
   and invariants the work touched. A contract that no longer matches the code is worse than
   none - fix the `.docs/` file (or the code, if the code drifted from the decision).

4. **Is the `.my/` plan finished with?** Mark it done. Then extract, then delete: once its
   decisions are in `.docs/`, anything a user needs is in `docs/`, and nothing links to it,
   delete the topic in the same commit. An AD that
   needs the evidence links to the plan at its last commit
   (`https://github.com/exact-mobile-apps/test-tooling/blob/<hash>/.my/...`). Step lists,
   progress tables, baselines and fixed-bug RCAs are not durable; git history keeps them.

## Do not

- Do not generate prose that describes the code. If a description of a subsystem is ever
  wanted, produce it on demand from the source that day - a stored copy goes stale.
- Do not copy a decision into more than one place. It lives once, in `.docs/`.
