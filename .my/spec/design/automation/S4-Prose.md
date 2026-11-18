# S4 Prose: automation

The style: [../400-Sentences.md](../400-Sentences.md#s4-prose). **Nothing to run.** Prose is the intent and
the "why" above an example: it explains, it cannot be false, so it cannot be tested. Back to the
[readme](000-Readme.md).

## What is automated

| Check | How | Exists? |
| --- | --- | --- |
| Every spec has an Intent, every example a "why" paragraph | Lirenet `check` | ✓ (`example-why`, section checks) |
| No vague words, no code words | Lirenet `check` | ✓ (`vague-word`, `code-word`) |
| **No rule hides in prose**: a sentence in prose with "must", "always", "never", "only", a number or a quoted message | Lirenet `check`, a warning: say it as a rule or a table row, so something proves it | **New**, small |
| Is the intent still true? | A person, at acceptance; a model-based review may ask (the `spec-review` skill of the Lirenet proposal) | — |

The third check is the one worth adding: prose that quietly states a rule is a rule nothing proves. Saying
it again as a rule makes it testable; the prose keeps the why.

## Questions

- Is the warning too noisy ("always" in an explanation)? Proposal: try it on TOD.02 and Bouw7's 30 specs;
  keep it if fewer than one in five warnings is noise.
