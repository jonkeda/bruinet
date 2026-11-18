# Tiny: a factory of tiny prompts

Models like Opus 5.5 and Fable already know how to write a plan, a design or a root cause
analysis. Asked "create a plan", "create a design" or "create an RCA", they write the document
the owner would have templated, or written a skill for, a few months ago. If that holds, a
factory needs far less text: tiny prompts for the work, and the effort goes to what the model
cannot know and to the checks. That is a tiny factory.

## Why it works

A plan, a design, an RCA, a brief and a review are common kinds of document. The model has seen
thousands of them and knows their parts: an RCA has a timeline, a cause, five whys and actions.
A template or a skill that restates those parts teaches the model nothing. It only costs words,
and it ages as the model gets better.

## Where it breaks

- **What is ours:** the model knows what a design is, not that ours names projects, tiers and a
  task list, or that our writing rules want short sentences and no ID lists.
- **Facts:** it does not know the product, the decisions already made, or where things live.
- **Sameness:** two runs give two shapes. A person doesn't mind; a check that reads the document
  does.
- **Silent misses:** a tiny prompt never fails loudly; it leaves out what we would have asked for,
  and nobody notices until later.
- **Drift:** what a model does unasked changes with the model; a factory that relies on it changes
  with every upgrade.

## What a tiny factory still holds

- **The words:** the names of the documents per loop (plan, design, review), so a tiny prompt
  points at the right kind.
- **The facts:** a few context files about the product and its decisions, read by every prompt.
- **The corrections:** only what the model gets wrong when left alone, each written after a run
  showed it, and dropped when a newer model no longer needs it.
- **The gates:** the checks that decide whether the work is done; these stay exact, because they
  are what lets a loop run dark.
- **The loop:** which document comes after which, and who passes each gate.

So a skill shrinks to its name, a pointer to the facts, its corrections and its gate. The
template disappears.

## What I think

It is right, and it moves the work to where it belongs. The knowledge of the craft now sits in the
model; what is left for us is what makes our factory ours and the controls that let it run alone.
That is also what the loops notes say: the hard part is the controls, not the building.

Two cautions. A tiny prompt is only as good as its gate, so the checks must get stronger as the
prompts get smaller; a document whose shape varies needs either a check that reads loosely or a
model as reviewer. And "it writes what I would have written" is the owner's impression, which is
worth measuring before Frainet is built on it.

## How to find out

First run on world/580: [tiny-experiment.md](tiny-experiment.md).


- Redo three recent documents from Tomorrow's factory, a brief, a design and a review, with tiny
  prompts and the same context files, and lay each beside the original.
- Write down every difference that matters; those differences are the only corrections worth
  keeping.
- Run each tiny prompt three times to see how much the shape moves between runs.
- Count the words: the skills and templates used now against the tiny prompts plus corrections.

## Open questions

- Are tiny prompts the default for Frainet? Proposal: yes, with a correction added only after a run
  shows it is needed.
- How does a gate check a document whose shape moves? Proposal: exact checks only on what must be
  exact, such as ids and links; a cold model reviewer for the rest.
- Which model is the baseline? Proposal: the one the factory runs on, written in its description,
  and the experiment above run again at each upgrade.
