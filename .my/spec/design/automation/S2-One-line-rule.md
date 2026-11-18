# S2 One-line rule (EARS): automation

The style: [../400-Sentences.md](../400-Sentences.md#s2-one-line-rule-ears), and the EARS proposal
(`.my/ears/ears-proposal.md` in the container). **A sentence is not run; its proof is.** A rule is automated
by the simplest proof that can make it false, and the rule's pattern says which. Back to the
[readme](000-Readme.md).

## What the spec says

| Id | Rule |
| --- | --- |
| TOD.02.R9 | When a new todo is saved, the app shall keep it on the device and show "Local only" until the next sync. |
| TOD.02.R4 | If a save is refused, then the app shall keep nothing and leave the form open with what was typed. |
| TOD.02.R14 | The app shall accept a due date in the past. |

## The pattern points to the proof

| EARS pattern | Usually proven by | Layer |
| --- | --- | --- |
| **Ubiquitous** ("The app shall accept a due date in the past") | A table row (T1 Limits) or a unit test | Unit |
| **When** (a trigger) | An example: the trigger as `When`, the response as `Then` | The layer the response lives in |
| **While** (a state) | An example that starts in the state: a `[BddState]` or test set as `Given` | Integration or UI |
| **If … then** (unwanted) | An example where the unwanted thing happens: a refused input (T1), a lost connection (stub backend down, HTTP replay of an error), a server error | Integration first |
| **Where** (a place or feature) | The same proof, run on that place's head; the trace checks it ran there | The named head |

The examples are the existing spec examples (S3), run by the spec runner. What is new is only that
**Lirenet uses the pattern to check the proof fits**: a When rule whose example has no `When`, an If rule
whose examples never make the unwanted thing happen, a While rule whose example never puts the state in.
Those are the example checks of the EARS proposal.

## Choosing the layer for the three rules above

| Rule | Simplest proof |
| --- | --- |
| R9 (When saved, kept on the device, "Local only") | `service` head: save with the backend unreachable, the local store has it; one UI example shows "Local only" |
| R4 (If refused, keep nothing) | Unit: the validator refuses; integration: nothing in the store or sent (stub log empty); UI: the form keeps its text, once |
| R14 (accept a past due date) | A row in the field table's Limits (`any date, past allowed`), or one unit case |

## What it needs

| Need | Exists? |
| --- | --- |
| Examples run as tests with the rule's id | ✓ runnable specs |
| States, stubs, replays for While and If | ✓ |
| Pattern-aware example checks | **New**, in Lirenet (EARS proposal, step 4) |
| "Proven at a layer" in the trace | **New**: the trace shows which head or test kind proved a rule |

## Questions

- May a one-line rule be proven by a table row instead of an example? Proposal: yes; the row's generated test
  carries the rule id too (`Proven by` filled in by the trace).
- Must every If rule be proven below the UI? Proposal: when it is about what is stored or sent, yes; when it
  is about what is shown, the UI is the place.
