# Questions: what Frainet is and how it gets working

What to decide before Frainet grows past its first slice. Each question has a proposal and why.
The ideas behind them are in [brainstorm.md](brainstorm.md). An answered question moves to a
decision, and its line here goes.

## What Frainet is

- **Factory, method or kit?** Proposal: a kit with a method; a factory is what you make with it.
  Factories differ per case, so the parts must work alone.
- **Who uses it?** Proposal: the owner first, written so a team or someone else could pick it up.
  It keeps the writing honest without building for users we don't have.
- **What is the first factory made with it?** Proposal: Todo's. It is the smallest product and
  already has journeys, tiers and tests to check against.
- **Does Tomorrow move onto Frainet?** Proposal: not now; Frainet learns from Tomorrow's factory,
  and Tomorrow moves only when Frainet does a part of it better.
- **Where does Bruinet stop and Frainet start?** Proposal: Bruinet runs, records and shows tests,
  and may review UI; Frainet says what must be proved and reads Bruinet's results at its gates.

## Its parts

- **Is a factory described in one file?** Proposal: yes, a factory description naming its loops,
  gates, specs, tools and who passes each gate. Write Tomorrow's first, from what is there.
- **Specs at which levels?** Proposal: the journey now; the product's intent and a capability's
  brief next, because they feed the journey.
- **Skills for Claude Code only, or any agent?** Proposal: plain instructions any agent can read,
  with a short stub per agent, as Bruinet does with `.github/skills` and `.claude/skills`.
- **Which model?** Proposal: none chosen; checks without a model come first, and a model check is
  added only where an exact check cannot see.
- **Which stack?** Proposal: tools in .NET for now, formats in plain Markdown and text, so another
  stack can read them.
- **Holdout scenarios?** Proposal: try them: some examples kept from the builder and used only at
  the gate, as StrongDM does, so the work cannot fit to them.

## Getting it working

- **How do we know it works?** Proposal: per loop, how dark it runs on Shapiro's scale, how much
  work comes back, and how long gates wait for a person; first on Todo, then Tomorrow.
- **Where do we start?** Proposal: describe Tomorrow's factory in Frainet's words, and keep the
  first slice's step 5 going beside it as the first evidence.
- **How do we take from others without getting lost?** Proposal: one piece at a time; for each, a
  short look at who does it already, take the best part, and write down where it came from.
- **When does Frainet get its own repository?** Proposal: when a second product besides Todo uses it.
- **Open source?** Proposal: decide when it gets its own repository.
