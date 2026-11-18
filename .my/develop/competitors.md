# What else does what Frainet wants to do

Frainet wants three things together: specs a person can read without knowing code, a check on
the spec before anything is built, and a check that the work covers every rule. No product found
on 2026-10-26 does all three. Each group below does one or two of them well, and has something
Frainet can borrow.

## Spec-driven toolkits for coding agents

These turn intent into a spec, a plan and tasks for an agent. They all run roughly the same loop
of specify, plan, implement and verify, and most leave the verify step to the agent's own tests.

| Product | What it is | Next to Frainet |
| --- | --- | --- |
| GitHub Spec Kit | Free, open-source CLI with commands for spec, plan and tasks, for many agents | Writes specs; no gate, no check that the work meets them |
| Kiro (AWS) | An IDE that turns a prompt into requirements in EARS notation, a design and tasks | The most structured; specs live in its IDE, and it is paid |
| OpenSpec | A light layer of proposals and specs kept in the repo, aimed at changing existing code | Closest in spirit; no rule-level trace to tests |
| Tessl | A platform where the spec is the source of truth and code is made from it | The same ambition; closed platform with a public registry |
| BMAD Method | Agent roles (analyst, architect, developer) handing over documents | A method, like our `/step`; specs are long documents, not rules |

## Executable specifications

Examples in Given, When and Then that run as tests. They prove the work, but the spec is the test
itself, so writing one still needs a programmer for the steps.

| Product | What it is | Next to Frainet |
| --- | --- | --- |
| Cucumber, Reqnroll | Gherkin feature files bound to step code | Frainet's examples use the same words; Bruinet.Uat is already this |
| Gauge | Markdown specs with steps bound to code | Markdown like ours; no rules above the examples |
| Concordion, FitNesse | Specs as pages whose examples are checked | Specification by example; old, little agent support |

## Requirements traced as code

Requirements kept as text files in git, with ids, links and a trace to code and tests. They check
the trace well but say nothing of whether a requirement is exact.

| Product | What it is | Next to Frainet |
| --- | --- | --- |
| StrictDoc | Open source; requirements in its own text format, traced to source both ways | The nearest to `frainet trace`; heavy, aimed at safety work |
| Doorstop | Open source; one YAML file per requirement, a tree of documents, trace checks | Simple and in git; YAML is not for a person to read |
| sphinx-needs | A Sphinx extension for requirements, specs and tests with links | Strong on standards such as ISO 26262; needs Sphinx |

## Requirements and test management suites

Jama Connect, Siemens Polarion, IBM DOORS, and Jira with Xray or Zephyr. They trace requirements to
tests across a company, with reviews and sign-off. They live outside the repo, so an agent cannot
read or change them as files, and a night run cannot use them as its gate.

## Where Frainet differs

- The spec is small, plain and per journey: rules of one sentence, an example each, no code.
- The spec is checked before it is used, without a model: `frainet check`.
- The work is traced rule by rule from the test sources as text: `frainet trace`.
- It belongs to a dark factory's loops, so both checks are gates an agent can pass alone.

## What to borrow

- Kiro's EARS notation, a fixed sentence shape for rules ("When <trigger>, the <system> shall
  <response>"), as a stricter rule check.
- StrictDoc's trace both ways, into code as well as tests, once a rule must say where it is built.
- OpenSpec's change proposals, for how a changed rule reaches a spec that is already accepted.
- Spec Kit's agent commands, as the way an agent asks for a spec or a trace.

## Open questions

- Should Frainet's specs follow EARS? Proposal: not yet; try it on TOD.02's rules after step 5 and
  keep it only if the rules read as well to a person.
- Should Frainet read Spec Kit's or OpenSpec's files? Proposal: no, until a product of ours uses one.
- Is "dark factory" a field yet? Proposal: watch it; Dan Shapiro's five levels and StrongDM's
  non-interactive team are the references, and no spec checker for it was found.

## Sources

- [7 spec-driven development tools (SSOJet)](https://ssojet.com/blog/best-spec-driven-development-tools)
- [Spec Kit vs Kiro vs Claude Code SDD workflows (Glukhov)](https://glukhov.org/ai-devtools/ai-coding-assistants/spec-kit-vs-kiro-vs-claude-code/)
- [Spec-driven development in 2026 (CodeMySpec, a vendor)](https://codemyspec.com/blog/spec-driven-development)
- [Kiro, Spec Kit and BMAD-METHOD (Medium)](https://medium.com/@visrow/comprehensive-guide-to-spec-driven-development-kiro-github-spec-kit-and-bmad-method-5d28ff61b9b1)
- [The five levels, Simon Willison](https://simonwillison.net/2026/jan/28/the-five-levels)
- [Software factory at StrongDM, Simon Willison](https://simonwillison.net/2026/Feb/7/software-factory)
- [StrictDoc documentation](https://strictdoc.readthedocs.io/en/stable/stable/docs/strictdoc_24_development_plan.html)
- [Sphinx-Needs setup (useblocks, a vendor)](https://x-as-code.useblocks.com/tutorials/sphinx-needs-setup.html)
- [RTEMS requirements tooling, with Doorstop](https://git.rtems.org/rtems-docs/tree/eng/req/tooling.rst)
