---
name: improvement-review
description: Look back over a suite's last journeys for what would make the next ones better on four counts - speed (what a journey costs), ease of use (can the next person write a test from the pages and controls alone), trust (does a green run mean what it says, is the environment left as found) and reach (how much of the app the tests say something about) - and write at most five ranked proposals, each with a measure and its price on the other counts, into the suite's .testing/600-Improvements.md, for the owner to pick from. Use when the site loop's report says a review is due, at the end of a wave, or when asked to look for improvements, to review how the work goes, or how the loop could be faster, easier or better.
---

# The improvement review

The friction log holds what went **wrong**, row by row, and each row is fixed before the next module. This review
looks at what the rows do not show: work that went fine and was slow, the same thing done by hand again and again,
pages and controls that work and are hard to use, a green run that says less than it seems. It is the outer loop
around the site loop: plan, do, check, **act**.

**Speed is one of four questions.** A review that asks only "what is cheaper" makes the work cheaper at the cost of
what it does not measure: the fastest page object to write is one member that takes a label, and nobody can read a
form from it. So every review answers all four, and every proposal says what it costs the other three.

## When

- The site loop says so: its report ends with "Improvement review" when the suite's `600-Improvements.md` is behind
  by its `Every` number of done journeys (5 unless the file says otherwise).
- At the end of a wave or a phase, and when a person asks.

Not in the middle of a journey: finish it to its gate first.

## The four questions

Each is counted the same way every review, so one review can be set beside the next.

| Question | For whom | Measures |
| --- | --- | --- |
| **Speed**: what does a journey cost? | Whoever waits for it | Minutes from nothing to green; failed runs per journey; the whole suite's time |
| **Ease of use**: can the next person write a test from the pages and controls alone? | Whoever writes the next test or spec | Members that stand for fixed structure by a text, of all members (`html-ui-test`, "Members have names"); forms and lists without named members; controls that leave their kind to the caller; steps written for one kind of record that a control could hold once; the **cold write** |
| **Trust**: does a green run mean what it says, and is the environment left as found? | Whoever reads the report | Examples that failed once and passed on a rerun; records a run left behind, and for how long; clean-ups that failed; rules whose example checks less than the rule says |
| **Reach**: how much of the app do the tests say something about? | The owner | Routes at each depth of the suite's map; journeys of the plan done; pages that cannot be read |

**The cold write.** Once per review a fresh agent gets one small task that the last journeys make possible ("write a
test that makes a contact with an hourly rate of 45"), the suite's page objects, controls and steps, and **no
access to the app**: no browser, no look, no recording. Count: did its test build, did every step bind, did it pass
on its first run on the app, and what did it have to guess or could not find. The same kind of task each review.
It measures the pages, not the agent: what is compared is this review with the last.

## Look

Read only; change nothing while looking. Half an hour at most, the cold write beside it. Use what is written down,
not memory:

| Where | What to look for |
| --- | --- |
| `.testing/runs/*/report.md` since the last review | Seconds per gate, failed gates and why, gates run again, an example that failed once |
| The runs' `test-timings.md` | The slowest tests and classes, and those marked slower |
| `400-FrictionLog.md` | Rows since the last review grouped by the change they ask for; rows open longer than one review; a row marked Done whose symptom came back; rows about leftovers and clean-up |
| `200-Tasklist.md` | Minutes and failed runs per journey; a person's checkpoint open for longer than one review |
| `180-Pages.md` (`bruinet suite pages`) | The counts for ease of use and reach, not a search written for the occasion: pages with a gap (a member by a text, a table without a typed row, nothing named, nothing that visits it), the places without a page, and what a person wrote as owed |
| `175-Places.md` (`bruinet scrape places show`) | Reach against the app itself: places still to open, places known from the code with no address, forms not saved |
| **The newest page objects, controls and steps** | Members by a text for fixed structure; a kind left to the caller; the same step written again for another kind of record; a name nobody would guess |
| The work itself | A command, a probe, a script or an edit done by hand more than twice; code written by hand that a draft could have made |
| The skills and `AGENTS.md` | A lesson a skill already holds and the work did not follow; a lesson no skill holds yet |
| The last review | What was built: did it give what it promised, measured the same way? And what did it cost the other questions? |

## Write

Add one review to the suite's `.testing/600-Improvements.md`:

1. A row in **Reviews**: its number, the date, how many journeys are done (`100-Loops.md` rows with state done), and
   **a column per question** with its measures.
2. **First, in a line, any measure that got worse** since the last review, whichever question it is under.
3. A section with **at most five proposals**. Each has: what changes, where (Bruinet, a control library, the suite,
   the way of working), **which question it serves and how that will be measured**, **what it costs the other
   three**, its cost to build, and its risk. A proposal without a measure, or that hides its price, is not one.
4. **Ranked by what they give against their cost across the four questions, not by time saved.** When a measure got
   worse, a proposal for it is on the list, and first.
5. What was looked at and gave nothing, in a line each, so the next review does not look again for nothing.

A proposal the owner turned down stays in the file with why. Do not propose it again unless what made it a no has
changed; say what changed.

## Then

- The owner picks. Nothing is built in the review itself, except a change of a few minutes with no risk (a line in
  a skill, a filter in a command), which is noted as done.
- Each proposal built gets its measurement in the review's **Built** table: before, after, measured how, **on its
  own question and on the others**. An estimate that was wrong is corrected there, not removed.
- A proposal that is a change to Bruinet goes where the friction harvest's items go (`friction-log`, Harvest).

## Not

- Not a second friction log: a single workaround is a friction row.
- Not a rewrite of what works: a proposal names the journeys, or the next person's task, it would have helped.
- Not more than five: a long list is not read. The sixth waits for the next review.
- Not speed alone: a review whose five proposals all serve one question says why.
