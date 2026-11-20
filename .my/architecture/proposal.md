# Software architecture with Bruinet: proposal

Status: proposal, nothing built. Written 2026-11-20. It works out section 5 of the
[lifecycle proposal](../lifecycle/lifecycle-proposal.md) (two architectures, transform rules) and fills the formats
[Migrate](../migrate/README.md) names but has not written: the architecture description and the transform rule.

## In short

- **The need:** more software will be migrated. What helped most so far was a view of the architecture, a from state
  and a to state, and instructions to get from one to the other. Today those are prose and drawings made per case,
  and nothing fails when they stop being true.
- **A state** is one architecture description: a Markdown file with the parts, where each part is in the code, what
  may depend on what, and rules with ids. The current state is **observed** from the code, a target state is
  **decided** by people. Both have the same form, so they can be laid side by side.
- **A view** is drawn from a state, never by hand, so it cannot go stale. The view that matters most shows two states
  in one picture: what stays, what goes, what comes, what moves.
- **The instructions** are a **route** (the steps between the states, each one a state that still builds and ships)
  and **transform rules** (how a thing of the source becomes a thing of the target, with a real example of each).
- **The tests:** every rule of a state is checked against the code without a model. A target's rules that the
  current code breaks are counted, and the count may only go down. That count is the migration's progress.
- **Useful without a migration:** a team that only wants its architecture to stay as decided uses one state and its
  checks. Bruinet is the first such team: `ModuleTests` is this, written by hand for one decision.

## What exists

| Exists | What it does | What it lacks for this |
| --- | --- | --- |
| `.docs/decisions/`, each with "Broken when" | A decision, what it rules out, and what shows it is broken | The "Broken when" line is prose; only some have a test |
| `ModuleTests`, `SolutionTests` | The four modules' references point one way (AD-044); every project is in the solution | The rules are C# in the test; another product cannot reuse them, and no picture comes from them |
| `BannedSymbols.*.txt`, the public API files | APIs that may not be used; the declared public surface | One kind of rule each, not tied to a description |
| `docs/architecture/` | Bruinet's structure in prose | Kept by hand; nothing says when it is wrong |
| The spec module | Rules with ids, a check without a model, a trace from rule to test | Rules about what a person sees, not about how the code is built |
| The lifecycle proposal, section 5 | Four descriptions, the aspects of an architecture, the transform rule's fields, the loop per unit | A plan; no format, no check, no view |
| `api-upgrade`, `convert-control` | Two small migrations done by rule: API changes as "Find" and "Write instead" rows; a control to the generator's format | Each its own form |
| The API proposal's rename ([here](../api/proposal.md)) | A from state, a to state and a command for consumers, on Bruinet's own packages | A first small case to try this on |

## 1. A state: the architecture description

One file per state, beside the product's specs: `architecture/<state>.arch.md`. A migration's `.migrator/` names two
of them as its source and target. The file, in this order:

| Section | Holds |
| --- | --- |
| Header table | Product; the state's name; `observed` or `decided`; `draft` or `accepted <date>`; for an observed state the commit it was read from |
| Intent | Two or three sentences: what this architecture is for, so a question the rules don't answer can be answered from it |
| Parts | A table: id, kind (system, deployable, layer, module, store, outside system), what it holds, and **where**: the projects, namespaces or folders that are this part. The "where" is what makes the rest checkable |
| Depends on | A table of which part may use which. What is not listed is not allowed |
| Rules | A table of id and rule, as in a spec: `ARC.R3`, one sentence that can be false ("A view model names no type of the UI framework") and how it is checked (section 4) |
| Aspects | Short text per aspect of the lifecycle proposal's list (screens and navigation, state, data access, API, sign-in and rights, background work), each pointing at one worked example in the code |
| Choices, Open questions | As in a spec: what was left free; what is not decided, each with a proposal |

What a state never holds: a drawing made by hand, a class-by-class listing (the inventory is Migrate's), or how to
get to another state (that is the route).

**Observed and decided.** `bruinet architecture describe` writes a draft of the current state from the code:
the projects and their references, then namespaces and the types that cross between them. A developer names the
parts and corrects the draft; after that the command only reports where the code and the file differ. A target
state is written by people, as decisions, and shown in one worked example, because an agent copies an example far
better than it follows prose. The same two grades as the API proposal has for an API's description.

## 2. Views

A view is a Mermaid diagram written by `bruinet architecture view` into the file itself or a document beside it,
so it reads in GitHub and in an editor with nothing installed. `--check` fails when a view is older than its state.

| View | Shows | For |
| --- | --- | --- |
| Context | The system and the outside systems it talks to | Someone new; the owner |
| Parts | The parts and what may depend on what, at one level (deployables, or the layers inside one) | The developer deciding where code goes |
| **From and to** | Two states in one picture: a part that stays, goes, comes or moves, and the transform rule on each arrow between an old part and a new one | Everyone in a migration: the one picture that says what is happening |
| Progress | The from-and-to view with each part's state from the ledger: not started, in work, at parity, switched | The weekly look; it comes from facts, not from a slide |
| Breaks | A parts view with each dependency the code has and the state does not allow | The developer fixing them |

The levels follow the C4 model's (context, containers, components) without its tools: the names are known, and a
team that already has such drawings can check them against these.

## 3. From one state to another: the route and the rules

**The gap** comes first, and a command makes it: `bruinet architecture gap <from> <to>` lists the parts only the
source has (to retire), the parts only the target has (to build), the pairs a transform rule joins, the parts no
rule reaches yet, and per rule of the target how often today's code breaks it.

**The route** (`architecture/route-<from>-to-<to>.md`) cuts the gap into steps. A migration is rarely one jump, and
the steps between are states of their own:

| Field of a step | Holds |
| --- | --- |
| Id and name | `S2 Views and view models split` |
| Moves | Which parts, or which units of the inventory |
| By | The transform rules that apply |
| Leaves | The state after it: the target's rules that hold from this step on. The product builds, passes its behaviour tests and can ship after every step |
| Done when | Checks only: those rules' counts at zero, parity for the moved units |
| Needs | The steps that must be done first |

**A transform rule** (`architecture/transforms/T.UI.03.md`) keeps the fields of the lifecycle proposal (id, kind,
from, to, unless, checked by) and adds what makes it an instruction:

| Field | Holds |
| --- | --- |
| From, To | The shape on each side, each with a **real example in a file of its own**, so it can be built and tested |
| How | The steps, in order, for a person or an agent. Short: what the example does not already show |
| Done by | `tool` (a program applies it: a rename, a Roslyn code fix), `agent` (a skill applies it, a check judges it) or `person` |
| Unless | The cases it does not cover, each pointing at another rule or at a person |
| Checked by | The target rule ids it makes true, and the layer of parity that proves nothing else changed |

An agent that meets a thing no rule covers does not improvise: it writes the case as an open question with a
proposed rule and takes another unit (lifecycle proposal, 5.3). Rules for a pair of stacks go into a pack and serve
the next migration of that pair.

## 4. Tests and specs

Behaviour stays the fixed point: the specs and the parity runs of Spec and Test say the product still does what it
did. What this proposal adds is the test of the structure:

| What is tested | How | Needs a model? |
| --- | --- | --- |
| The files' form | `bruinet architecture check`: sections in order, ids never reused, every part with a "where", every rule one sentence with a check, every transform rule with both examples | No |
| The code against a state | Every project and namespace belongs to exactly one part; every dependency is allowed; every rule holds | No |
| A state on the way | A **baseline** per rule: the places that break it today, written to a file. A new break fails; the list may only get shorter; a step is done when its rules' lists are empty | No |
| A rule is really checked | The trace, as `bruinet spec trace` has it: a rule no check reaches is prose, and the trace fails on it | No |
| A transform rule | `tool`: applying it to the From example gives the To example, exactly. `agent` and `person`: the To example passes the target's checks, so an example cannot rot | No |
| A converted unit | The ledger's row: by which rules, and the checks that passed. A rule that keeps failing parity is a query for the units done with it | No |
| A decision | An AD's "Broken when" line names rule ids, and the trace covers them | No |

How a rule is checked, by its kind:

| Kind of rule | Example | Checked by |
| --- | --- | --- |
| Reference | "Spec references only Spec" | The engine, from the projects' references: what `ModuleTests` does in sixty lines |
| Dependency | "A view names no type of data access" | The engine, from the compiled types |
| Placement | "Every page object is in its module's `Pages` folder" | The engine, from names and paths |
| Naming | "A view model's name ends in `ViewModel`" | The engine |
| Banned API | "No `Thread.Sleep` in a test" | `BannedSymbols` files, written from the state |
| Anything else | "A screen's data is loaded in one call" | A test the team writes, naming the rule with `[Trait("Rule", "ARC.R7")]`, as a spec's rule is named |

## 5. Libraries to build on

| Job | Library | Proposal |
| --- | --- | --- |
| Read projects and references | MSBuild's project files, as `ModuleTests` reads them | **Use**: no dependency |
| Read types and what they use | Roslyn (`Microsoft.CodeAnalysis`), already under the analyzers | **Use**, in a package of its own (a dependency most suites should not get, AD-026) |
| Type-level rules | ArchUnitNET; NetArchTest | **Try ArchUnitNET under the engine** before writing type analysis ourselves. Not as what a team writes: rules stay in the state file |
| Banned APIs, public surface | `BannedApiAnalyzers`, `PublicApiAnalyzers` | **Keep**, fed from the state |
| Draw | Mermaid | **Use**: text, renders where the docs are read |
| An architecture model with views | Structurizr's DSL, LikeC4 | **No** as the format: a second language beside Markdown and its own tools. The C4 level names are kept |
| Front ends in JavaScript and TypeScript | dependency-cruiser | **Outside tool**, when a product's front end is in scope: it reads and judges dependencies by rules, and our state can write its rules |
| Change code by rule | Roslyn code fixes for C#; ast-grep or jscodeshift elsewhere; `bruinet upgrade` of the API proposal for renames | **Use per rule** marked `tool`. Most rules will be `agent` |
| Move a .NET project to a newer framework | Microsoft's upgrade tooling | **Outside tool**, named in a pack's rule where it applies |
| Trends and rules over a code base | NDepend, Sonar | **No**: commercial or a server; the baseline file gives the one number needed |

The names are from memory and were not checked online for this document: check each one's current state and licence
before it is used.

## Where it lives

| Part | Module | Why |
| --- | --- | --- |
| The state's format, its reader and the form check | **Spec** | It is a description with rule ids, checked without a model, and Migrate and Test both read it: by AD-044 it goes in the lowest module that needs it |
| Observing code, the engine, the baseline, the views | **Test**, as `Bruinet.Architecture` | It tests a product, below the UI and below the API; it needs Roslyn |
| The route, the transform rules, the gap, the ledger | **Migrate** | It is the conversion; Migrate gets its first code when a real migration needs it |

Commands, under `bruinet architecture`: `describe`, `check`, `view`, `gap`. An MCP tool per command, so an agent
asks "may this part use that one" and "what breaks rule R3" instead of reading the whole state.

## Order of work

| Phase | Builds | Done when |
| --- | --- | --- |
| 1 | The state's format and form check; Bruinet's own state, from AD-001, AD-026 and AD-044; the engine for reference rules | `ModuleTests` reads the state file and holds no table of its own; the parts view in `docs/architecture/` is written by the command |
| 2 | `describe` from a solution, down to namespaces and types; dependency, placement and naming rules; the baseline | A second code base gets a draft state and a list of what breaks it, with no hand-written test |
| 3 | Two states: the gap and the from-and-to view. Tried on the API proposal's rename, with today's packages as the source and the `Api` family as the target | The rename's steps come from the gap, and its done-when is a baseline at zero |
| 4 | The route and transform rule formats, their check, the examples as tests | One real migration's first step is planned and judged with them |
| 5 | Migrate's ledger joined to the views (progress); packs | A migration's weekly picture needs no hand work |
| 6 | Other languages | A product in scope has a part the .NET reading cannot see |

Phases 1 to 3 need no migration: they pay for themselves on Bruinet. Phase 4 waits for a real one, as Migrate does.

## Open questions

- Which migration is next, and from which stack to which? Proposal: phase 4's formats are settled on it, not before,
  so they are shaped by a real case.
- How deep does a state go: projects, namespaces, or types? Proposal: as deep as its rules need and no deeper;
  Bruinet's own needs only projects.
- Is the current state kept in the repository or only made when asked? Proposal: kept and accepted, so a change of
  architecture shows as a change of that file in a review.
- Are a rule's sentences written in EARS, as the EARS proposal asks of a spec's rules? Proposal: yes, with the part
  as the system ("A view model shall name no type of the UI framework").
- Do the format and check go into Spec, or is architecture a module of its own? Proposal: Spec for the format, Test
  for the checks, as above; a fifth module needs a user group of its own (AD-044).
- Should an observed state also read what runs (deployables, queues, databases) and not only code? Proposal: not
  now; those parts are written by hand and carry no "where" until a case needs it.

## Not proposed

- A drawing tool, or diagrams kept by hand.
- Judging whether an architecture is good: the tools say whether the code is what was decided, people decide.
- Converting code inside Bruinet's engine with a model: an agent converts, by a rule, and the checks judge
  (lifecycle proposal, 5.4).
