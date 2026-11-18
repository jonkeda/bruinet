# Tiny prompts against the skills: world/580

On one capability, tiny prompts gave briefs, a design and a review as good as the skill-made ones
in substance, and on some points better: they raised issues the originals found only at review or
play. What they lost was the house shape, a small scope and the process steps, which is what
[tiny.md](tiny.md) says a factory must still hold. The skills can lose their templates; a short
list of corrections and the loop's gates remain.

## What was run

The capability was Tomorrow's world/580, water as shapes, built 2026-10-18 and 19. Each run was a
cold agent with a tiny prompt ("Create a brief for…", "Create a design for…", "Create a review
of…") and a checkout from just before the brief, so the built code was not there to copy. The
agents had the project's CLAUDE.md and writing rules, as every factory run does, but not the skills,
the templates, the method or any other capability's documents.

- Briefs: five runs from the plan item. Runs 1 and 3 read the original brief by mistake and are set
  aside; run 2 saw its first lines; runs 4 and 5 are clean.
- Design: one run from the original brief.
- Review: one run from the build commit, the brief and the design, without building or running
  tests.

The documents are in [tiny-runs/world-580/](tiny-runs/world-580/); the originals are in Tomorrow's
`.sdlc/modules/world/580-water-as-shapes/`.

## What the tiny prompts did well

- Every brief saw that Edam lies outside the dev area of the day (the Hoorn area stops at 52.58° N,
  Edam is at 52.51°) and proposed Enkhuizen. The original asked the owner to check Edam; the owner
  played it, probably through the full import, so this is a careful point rather than a miss.
- Every brief, and the review, asked whether a tile's water cover should still paint where a shape
  exists; the plan said shapes replace the tiles, the original brief said "beside".
- Every brief named the size of the big lakes as unknown; the original found it at review ("big
  lakes projected whole per cell").
- The design made the original's core choices (a water field in the cell body, version 4, the table
  emptied, three passes, the multipolygon code shared) and also planned two fixes the original
  found only at review: a narrow canal crossing a cell without its centre, and a way that is also a
  relation's outer ring.
- The review found the original review's blocker (one bad area stops a whole import), two of its
  other fixes, and a few more points of its own.

## What they lost

- **Shape:** each brief chose its own headings ("What you will play", "What is not known yet",
  "Next"); no two match, and none matches the template.
- **Gherkin:** briefs 4 and 5 wrote scenarios as bullets; the method needs Given, When, Then that a
  page object can perform, because they become the acceptance tests.
- **Small scope:** briefs 4 and 5 widened the step (water names on hover, a one-square channel rule,
  a new area, a decision record); the original kept it small and named what comes later.
- **Length:** briefs of 660 to 950 words against 394, a design of 1,860 against 580; the writing
  rules ask for a brief read in two minutes.
- **Process:** the design asked no architect or test designer; the review ran no tests, by the
  experiment's rule, and so missed the two faults the original's tests found; neither wrote the
  owner's "how to play" or the record of gates passed without the owner.

## What it costs

The skills and templates behind these three documents are about 2,570 words (brief 437, design
1,358, review 773), with the 4,685-word method beside them. A tiny prompt is about 30 words. Most of
the skills' words are not the template: the design skill's pre-check, the lessons from what the
architect kept blocking on, is about 700 words on its own. Those are corrections, and they stay.

## The corrections this run shows

1. A brief's scenarios are Gherkin, with steps a page object can perform.
2. Keep the capability small; name what grows later instead of building it.
3. A brief reads in two minutes, a design stays to what the build needs.
4. A review runs the tests its diff touches and says how to play the result.
5. A design is reviewed by the architect before it passes; that is the loop's gate, not the
   document's text.
6. Where a gate or a check reads a document, its section names are fixed; elsewhere the shape may
   vary.

## How far to trust this

One capability, a single design and review run, two clean briefs and one nearly clean. The
comparison is mine, not the owner's. The review could not run tests, so it is weaker than a real
one by design. Next is the owner's read of the runs, then a second capability of another kind (a
page, not an import) before the skills change.

## Round 2: the tiny skill

A tiny brief skill, [tiny-skills/brief.md](tiny-skills/brief.md), is the prompt plus the three brief
corrections and a gate: 75 words against the 437 of step-brief and its template. It ran twice on
world/580 and twice on ui/275 (Region you can zoom and pan, a page rather than an import), each run
cold, in a checkout from before its brief, with a folder holding only its plan item. The briefs are
`skill-brief-1.md` and `skill-brief-2.md` in [tiny-runs/world-580/](tiny-runs/world-580/) and
[tiny-runs/ui-275/](tiny-runs/ui-275/).

The corrections held. All four briefs pass the gate ([brief-gate.py](tiny-skills/brief-gate.py)):
488 to 496 words, two or three scenarios each in Gherkin, written in the suite's own phrases ("I tap
Admin", "I pick the area") with new steps only where the behaviour is new. The original world/580
brief passes too; the original ui/275 brief fails on length, at 571 words.

| | Bare prompt (round 1) | Tiny skill (round 2) |
| --- | --- | --- |
| Gherkin scenarios | 1 of 3 clean briefs | 4 of 4 |
| Under 500 words | 0 of 3 | 4 of 4 |
| Scope kept small | 1 of 3 | 3 of 4; one adds naming the water on hover |
| Headings | own per run | still own per run |

The substance stayed good. On ui/275 both briefs saw that the arrow keys already walk the survivor
on the game and proposed panning only while the map has focus; the original found that in its second
design round. Both also asked how to test a Noord-Holland-sized area when the test set holds only
Hoorn. On world/580 both kept the question of tiles against shapes.

Some care went under the word cap. Neither world/580 brief raised Edam lying outside the dev area or
the size of the big lakes, which every bare brief did. A cap of 500 words seems to cost the unknowns
first.

The gate taught its own lesson. Its first version also failed a `Feature:` line and a When after a
Then, which Gherkin allows; that was a house style the skill never stated. A gate stricter than its
skill fails good work, so a gate checks exactly what the skill says and no more.

## Open questions

- Shrink Tomorrow's skills now? Proposal: try step-brief as this tiny skill on the next real step,
  with an "Unknown" line added to the corrections so the cap doesn't cost the unknowns, and compare at
  its review.
- Fix the headings? Proposal: only those a check reads (for a brief, the scenarios); let the rest
  vary and see whether the owner minds.
- Is the extra insight real or luck? Proposal: count, over the next few steps, the points a tiny run
  raises that the skill run didn't, and the reverse.
