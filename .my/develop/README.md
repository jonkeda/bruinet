# Bruinet Develop

Develop is the module of Bruinet that does the work: the loops that pick the next piece of work, brief an agent, have
the other modules judge it, and learn from what it cost. It was called Frainet. It has **no code yet**: today's loop
over a site's modules is `tools/Scripts/site-loop.ps1`, and the skills about the work are `friction-log` and
`improvement-review`. It gets `Bruinet.Develop` and a `bruinet develop` command when a case of the small-steps
proposal needs one, not before. Develop judges nothing itself: it calls `bruinet spec check`, `bruinet spec trace`
and the tests, and records their verdict ([what-goes-where](../parts/what-goes-where.md)).

Every loop turns plan, build, deploy and feedback, and the build of one loop is many turns of the loop inside it:
tasks in a capability, capabilities in a module. A loop goes dark when its feedback is an exact check. The specs loop
runs across all of them, feeding each plan, so the spec is where the factory's light stays on longest.

## The notes

Develop's work in flight, one row per topic. They were written as Frainet's; the name stays in them where it was
written.

| Topic | What is open | Status | Updated |
| --- | --- | --- | --- |
| [brainstorm.md](brainstorm.md) | What Frainet is: a factory, a method for making factories, or a kit; what factories share and where they differ; its pieces; ideas to take; why it is difficult | brainstorm | 2026-10-27 |
| [Tiny/](Tiny/Tiny_Proposal.md) | Tiny, Frainet's method: dark factories of loops, tiny skills and gates, code only in gates, one agent with skills on demand ([Tiny_Proposal.md](Tiny/Tiny_Proposal.md)); the idea that models know the craft ([tiny.md](Tiny/tiny.md)); two rounds of tiny prompts and a 75-word tiny brief skill against Tomorrow's skills ([tiny-experiment.md](Tiny/tiny-experiment.md), [tiny-skills/](Tiny/tiny-skills/brief.md), [tiny-runs/](Tiny/tiny-runs/)) | proposal; two experiment rounds | 2026-10-27 |
| [questions.md](questions.md) | What to decide before Frainet grows: what it is, its parts, getting it working; each with a proposal | open | 2026-10-27 |
| [plan.md](plan.md) | The first slice: Todo's TOD.02 as a Frainet spec, and the checks on it; step 1 accepted ([TOD.02.spec.md](../../samples/Todo/specs/TOD.02.spec.md)), step 2 written ([spec-format.md](../../docs/spec/spec-format.md)), step 3 built (`frainet check`, now `bruinet spec check`), step 4 built (`frainet trace`, now `bruinet spec trace`; 10 of 11 rules reached); next an agent adds R11's test from the spec alone | step 4 of 5 | 2026-10-26 |
| [competitors.md](competitors.md) | What else does what Frainet wants: spec-driven toolkits (Spec Kit, Kiro, OpenSpec, Tessl, BMAD), executable specs, requirements traced as code (StrictDoc, Doorstop, sphinx-needs), management suites; where Frainet differs, what to borrow | research | 2026-10-26 |
| [loops/](loops/02_loops_generic.md) | Loops in loops, the dark factory: plan, build, deploy, feedback ([02](loops/02_loops_generic.md)); the same by PDCA ([03](loops/03_loops_pdca.md)); how Tomorrow and the Migrator use it ([01](loops/01_loops.md)). Moved from Tomorrow's `.my/loops` | background | 2026-10-26 |
| [specs/](specs/01_AI_Software_Factory_Specifications_2026.md) | Research: specification layers, agentic PRDs, decision boundaries ([01](specs/01_AI_Software_Factory_Specifications_2026.md)); the factory blueprint, agents, governance, metrics ([02](specs/02_AI_Software_Factory_Blueprint_2026.md)) | background | 2026-10-26 |
