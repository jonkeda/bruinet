---
name: improvement-review
description: Look back over a suite's last journeys for what would make the next ones cheaper - where the time went, what was done by hand more than twice, which failed runs a cheaper check would have caught, what the friction rows have in common - and write at most five ranked proposals with a measure each into the suite's .testing/600-Improvements.md, for the owner to pick from. Use when the site loop's report says a review is due, at the end of a wave, or when asked to look for improvements, to review how the work goes, or how the loop could be faster or better.
---

# The improvement review

The friction log holds what went **wrong**, row by row, and each row is fixed before the next module. This review
looks at what the rows do not show: work that went fine and was slow, the same thing done by hand again and again,
and what several rows have in common. It is the outer loop around the site loop: plan, do, check, **act**.

## When

- The site loop says so: its report ends with "Improvement review" when the suite's `600-Improvements.md` is behind
  by its `Every` number of done journeys (5 unless the file says otherwise).
- At the end of a wave or a phase, and when a person asks.

Not in the middle of a journey: finish it to its gate first.

## Look

Read only; change nothing while looking. Half an hour at most. Use what is already written down, not memory:

| Where | What to look for |
| --- | --- |
| `.testing/runs/*/report.md` since the last review | Seconds per gate, failed gates and why, gates run again |
| The runs' `test-timings.md` | The slowest tests and classes, and those marked slower |
| `400-FrictionLog.md` | Rows since the last review grouped by the change they ask for; rows open longer than one review; a row marked Done whose symptom came back |
| `200-Tasklist.md` | Minutes and failed runs per journey, against the journeys before the last review |
| The work itself | A command, a probe, a script or an edit done by hand more than twice; code written by hand that a draft could have made; a page or control written twice |
| The skills and `AGENTS.md` | A lesson a skill already holds and the work did not follow; a lesson no skill holds yet |
| The last review | What was built: did it give what it promised, measured the same way? |

## Write

Add one review to the suite's `.testing/600-Improvements.md`:

1. A row in **Reviews**: its number, the date, how many journeys are done (`100-Loops.md` rows with state done), and
   the measures (minutes from nothing to green, failed runs per journey, the whole suite's time).
2. A section for the review with **at most five proposals**, ranked by time saved per journey against the cost to
   build. Each has: what changes, where (Bruinet, a control library, the suite, the way of working), what it saves
   and how that will be measured, its cost, and its risk. A proposal without a measure is not one.
3. What was looked at and gave nothing, in a line each, so the next review does not look again for nothing.

A proposal the owner turned down stays in the file with why. Do not propose it again unless what made it a no has
changed; say what changed.

## Then

- The owner picks. Nothing is built in the review itself, except a change of a few minutes with no risk (a line in
  a skill, a filter in a command), which is noted as done.
- Each proposal built gets its measurement in the review's **Built** table: before, after, measured how. An
  estimate that was wrong is corrected there, not removed.
- A proposal that is a change to Bruinet goes where the friction harvest's items go (`friction-log`, Harvest).

## Not

- Not a second friction log: a single workaround is a friction row.
- Not a rewrite of what works: a proposal names the journeys it would have saved time on.
- Not more than five: a long list is not read. The sixth waits for the next review.
