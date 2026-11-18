# Spec styles: the right shape for each question

Status: **proposals**, for discussion. Date: 2026-11-18. No style here is designed yet: each says what
it is for, shows a few lines so the idea is clear, and leaves the exact format for later.

**Names since this was written** (2026-11-18): Lirenet is Bruinet's **Spec** module, Frainet **Develop**, Gaminet
**Migrate** ([one-or-four](../../structure/one-or-four.md)); `lirenet check` and `lirenet trace` are
`bruinet spec check` and `bruinet spec trace`. The text below keeps the names it was written with.

## Why

Today a Lirenet spec has one shape for everything: a rule as a sentence, and Given / When / Then
under it. That fits behaviour that happens in steps. It fits badly for much of what a spec must say.
Bouw7's 30 specs show it (counted on 2026-11-18):

| In Bouw7's specs | Lines | Share |
| --- | --- | --- |
| Gherkin step lines in all | 702 | |
| `Then … is shown` / `And … is shown` | 175 | 25% |
| `Given … signed in`, `I am on …`, `I open …` (getting there) | 138 | 20% |
| Lines about a list, a row or a column | 83 | 12% |
| Lines that mention "required" | 71 | |

So roughly half of the Gherkin says **what is on a screen** or **how to get there**. A table or a
picture says that in a few lines, and a person checks it at a glance. B7.MST.02 R1 takes eight
lines to say a form has a title and three buttons.

The idea: **pick the style by the question the spec answers.** "Which fields are required?" is a
table with a tick. "How do I get from the list to the form?" is a flow. "What happens when the save
fails halfway?" is a scenario. One spec may use several styles, each for its own part.

## The documents

| File | Holds |
| --- | --- |
| [100-Questions.md](100-Questions.md) | **The core.** The questions a spec must answer, and which style answers each best, second best, and worst |
| [200-Tables.md](200-Tables.md) | Field table, list table, rights matrix, decision table, message list, state transition table, example table |
| [300-Pictures.md](300-Pictures.md) | Screen card, sketch, annotated screenshot, flow, state diagram, domain model, sequence diagram, journey map |
| [310-Screens.md](310-Screens.md) | Which source format for a screen: ASCII, Wireframe (jonkeda/Wireframes), HTML, or something else |
| [320-Wireframe-review.md](320-Wireframe-review.md) | Review of jonkeda/Wireframes as the source of screens: what fits, what loses content, what to fix |
| [330-Salt-vs-Wireframe.md](330-Salt-vs-Wireframe.md) | PlantUML Salt set beside Wireframe on the eight needs: Salt draws a screen, Wireframe describes one |
| [automation/](automation/000-Readme.md) | How each style becomes tests, the simplest way: table checks, spec scenarios, spec-fed unit tests, one document per style |
| [400-Sentences.md](400-Sentences.md) | Checklist, one-line rule (EARS), Gherkin scenario, and a short note on what prose is still for |
| [500-Mixed.md](500-Mixed.md) | Two sketches of whole specs in mixed styles (B7.MST.02 and TOD.02), with line counts against today |

## How each style is judged

The same seven questions for every style, so they can be compared:

| # | Criterion | Asks |
| --- | --- | --- |
| C1 | **Read** | Can the product owner read and judge it in a minute, without training? |
| C2 | **Write** | Can it be written, or drafted from the running app, with little effort? |
| C3 | **Exact** | Can it be false? Is there one reading? |
| C4 | **Check** | Can Lirenet check its form, and against the other files, without a model? |
| C5 | **Run** | Can Bruinet turn it into tests by itself, without hand-written steps per line? |
| C6 | **Diff** | Does a change show clearly in a pull request? |
| C7 | **Draw** | Does it render as a picture in GitHub, VS Code and the Presenter, with no tool to install? |

Each style gets a small score card on these, ●●● good to ○○○ poor. The scores are a first guess, to
be checked in the trial (below).

## Principles across the styles

- **Simplest that is exact.** A tick in a table beats a sentence; a sentence beats a scenario. A
  scenario is for what really happens in steps.
- **Plain Markdown and Mermaid only.** It renders on GitHub and in VS Code without plug-ins, it
  diffs as text, and Lirenet can read it. No separate drawing files as the source.
- **Every style has ids**, so a test can name what it proves: a rule id, or a row or cell id where
  a table is the rule.
- **A style that can be generated into tests should be.** A field table with "required" ticked
  makes the "empty is refused" test for every ticked field. Nobody writes those by hand again.
- **Words from one place.** Screens, fields, buttons and states are named once (the screens and the
  domain model of the Lirenet proposal); every style uses those names.
- **Pictures are drafted from the app**, not drawn by hand, wherever the app exists: Bruinet's
  Scraper already knows the screens, the fields and the ways between them.

## How to find out which works

A small trial, after the formats are sketched:

1. Pick three Bouw7 specs of different kinds: a form (B7.MST.02), a list (B7.CON.01), and one with
   roles (B7.INV.01).
2. Rewrite each in mixed styles, by the question map.
3. Count lines, and time the owner reading old and new. Ask the owner one question per spec
   ("is Name required?", "can a Reader delete?") and time the answer.
4. For each style: could Lirenet check it, could Bruinet run it without new step code?
5. Keep the styles that win; drop the rest.

## Open questions

- Is a spec still one file per journey, with the styles as sections in it, or do the tables and
  pictures live per screen (`screens/Department form.md`) and the journey points to them?
  Proposal: per screen for what a screen *is* (fields, buttons, columns); per journey for what a
  person *does*.
- Which visual for a screen: a card (a table), a sketch (Mermaid), or a screenshot with numbers?
  Proposal: try all three on the department form; see [300-Pictures.md](300-Pictures.md).
- Are generated tests listed anywhere a person sees them, or only in the trace? Proposal: the
  trace lists them under the row they came from, so the owner sees what was proven.
- Do Dutch screens get Dutch column headers in the tables ("Verplicht")? Proposal: headers in
  English, the screen's own words in the cells.
- How many styles at most? Proposal: about six in use; more means a reader must learn too much.
