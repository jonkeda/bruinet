# Brainstorm: what Frainet is

Frainet is meant to become the factory, or a method for making factories, because software
factories will differ per case. Tomorrow is a game built by a dark factory made by hand; it is an
example Frainet learns from, and may later run on Frainet. Bruinet is the test tool, and maybe the
review tool. This note collects ideas; [questions.md](questions.md) asks what to decide.

## Three readings

- **A factory:** one way of building, ready to run on any product.
- **A method for making factories:** the parts and rules to put together a factory that fits a case.
- **A kit with a method:** parts that work alone (spec format, checks, gates, records, skills) and a
  method that says how to choose and join them.

The lean is the third: factories differ, so the parts must stand alone, and a method says how to
join them. Tomorrow's `/step` and the Migrator are two factories made the same way by hand, which is
the evidence that a method exists to be found.

## What every factory may share

- Loops in loops, each turning plan, build, deploy and feedback (or plan, do, check, act).
- A loop's state in files, so any turn can start cold.
- Gates with three outcomes, pass, pass with follow-ups or stop, and a gate passed without a person
  recorded as delegated.
- A spec at each level and an exact check at each gate.
- An orchestrator, specialists that return evidence, and reviewers that start cold.
- A management loop that changes the factory from what the runs left behind.

## What differs per factory

- **The product:** a game is judged on taste, a migration on matching the old system, a business app
  on its rules, a library on its interface.
- **Where the spec comes from:** talks with an owner, an existing system, a regulator, a customer.
- **How dark each loop runs:** which gates need a person, and why.
- **The tests and the stack:** the tiers, the tools, where it deploys.
- **The people and the budget:** one owner or a team, how many model calls a night may spend.

## The pieces Frainet might hold

| Piece | What it does | There now |
| --- | --- | --- |
| Spec format and check | A plain spec per journey, checked without a model | Started, TOD.02 |
| Trace | Each rule reached by a test | Started |
| Factory description | One file naming a factory's loops, gates, specs and tools | No |
| Loop records | Brief, design, review, status, per turn of a loop | In Tomorrow's `.sdlc` |
| Gate records | What passed, who passed it, delegated or not | Tomorrow's `900-status.json` |
| Skills | One short skill per loop that runs its tool until its gate passes | Tomorrow's `/step` skills, heavy |
| Factory review | The management loop: what the runs show, what to change | Tomorrow's SDLC review |
| Factory maker | A new factory for a new product, from a description | No |

## Who does what

- **Frainet:** the method and its parts, and the gates that decide.
- **Bruinet:** runs and records the tests, shows them as videos for acceptance, and may review UI; its
  results are evidence at Frainet's gates.
- **Tomorrow:** the first factory, made by hand, and the source of lessons.
- **Todo:** the smallest product, to try a factory made with Frainet.

## Ideas to take from others

- **Spec Kit:** a constitution of standing principles; `/clarify` for gaps; `/analyze` for whether spec,
  plan and tasks agree.
- **Kiro:** steering files for product, tech and structure; EARS rules; hooks that run on events.
- **OpenSpec:** specs of what holds now, kept apart from proposed changes; `validate`; a change
  archived into the specs when done.
- **BMAD:** roles that hand over documents; the QA role's trace and its gate of pass, concerns, fail
  or waived.
- **StrongDM:** scenarios kept outside the code, like a holdout set, so the builder cannot fit to
  them; stand-ins for outside services to test against.
- **Tessl:** a registry of specs for how to use libraries, shared between products.
- **StrictDoc:** the trace both ways, from rule to code and test and back.
- **Dan Shapiro's five levels:** a scale for how dark a loop is.
- **Lean manufacturing:** stop the line on a defect (jidoka), mistake-proofing (poka-yoke), small
  steady improvement (kaizen); the factory metaphor already fits.

## Why it is difficult

- Specs are hard to write: people think in examples and taste, not in rules.
- The controls are the hard part, not the building; a check too loose lets bad work through, one too
  strict stops the line.
- Agents fit to their checks: tests that pass without proving anything, reviews that agree.
- A factory drifts from its method; instructions are worked around and records grow unread.
- Factories differ, and a method is harder to make than one factory.
- People's feedback sets the pace, however fast agents build.
- It costs model calls and time, and the cost must be seen to be steered.
- Showing it works needs measures, not impressions.

## Ways to get it working

- **Extract, don't invent:** describe Tomorrow's factory, and the Migrator's, in Frainet's words; what
  they share is Frainet, what differs is a choice in the method.
- **Make a second factory small:** Todo's, made with Frainet, and compare it with Tomorrow's.
- **Take before building:** for each piece, look at who does it already and take the best part.
- **Grow on evidence:** a piece is added when a factory needs it, and dropped when nobody uses it.
