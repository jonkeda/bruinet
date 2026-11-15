# Runnable specs

Status: built 2026-11-15, except two pieces (below). Started after phase 5 of the Bouw7 proposal. Guide:
`docs/guides/runnable-specs.md`; decision: AD-041.

Built: RS1 (reader, `Specs` row, `Rule`/`Spec` traits, Background), RS2's format (chapters, Frainet check), RS3
(extension verbs, `TableVerbs` on Bruinet.Html's `TableControl` instead of a new Core `ITableControlObject`), RS4
(control names), RS5 for B7.INV.01 (its C# tests deleted, site loop green), RS6's tools (`IsScenarioFile`: editors,
CLI, workspace), RS7 (skills, docs, AD). Not built: a chapter's Then-only examples sharing one run (each runs the
chapter's steps again; they carry the `chapter-then` tag for it), Bouw7's CON, PRJ and HRS specs moved 2026-11-15, their C# rule tests deleted.

A Frainet spec's examples run as tests, on the same engine as the `.feature.md` scenarios. The spec stops being a
document that a C# test copies by hand: the example *is* the test. Features stay; they are for longer journeys told
step by step (a narrated recording, a flow over several pages). Specs hold rules and an example per rule.

What this answers:

1. Specs in the test project's `Modules/<Module>/` folders, beside the pages and features.
2. A generic way to bind a step such as `When I type "zzz-nothing" in the filter above Name` to the real control,
   with code that is written once and reused.
3. Saying `Given Creator is signed in` and `When I open the Contacts page` once, not in every example.
4. Chapters: one Given and When, then the Thens of several rules.

## Why

From Bouw7's phase 3 and 4 (friction #50, the review of the harvest):

- The C# tests repeat the spec in another language. Nothing checks that `PartOfANameIsEnough` does what R4's example
  says; they match today because one agent wrote both.
- The spec's Gherkin is never run, so a wording mistake in it is never caught.
- Four times a test disproved a spec fact. A runnable example finds that the moment it is written, not one stage later.
- `spec-from-app` already "checks every example on the app" before acceptance. Today that is manual; with runnable specs
  it is `dotnet test --filter Spec=B7.CON.01`.
- A person can read what runs. Today only a programmer can check the C# tests against the spec.

## What it builds on

Almost everything is there already, in `Bruinet.Bdd`:

| Exists | Where |
| --- | --- |
| Markdown documents with gherkin blocks | `BddMarkdownParser`, `docs/specs/uat-grammar.md` |
| `## Background`, prepended to every scenario | grammar, scenario flow |
| Control verbs on interfaces (`I enter {value} into {control}`) | AD-011, `uat-step-definitions.md` |
| Item verbs (`{item}`), verbs without `{control}` | same |
| Page phrases, step-definition classes, callable phrases (`.steps.md`) | AD-017 |
| Folder namespaces, `Using` | AD-019 |
| Feature files per module (`Features | Modules` in `uat.config.md`) | Bouw7 phase 1b, friction #9 |
| `{account} is signed in` | `Bouw7UatFixture` |
| One theory case per scenario, `BddScenario` trait | `UatScenarioTheory` |
| Rule states from a TRX | Frainet F1, `frainet trace --results` |

So the design is mostly three additions: the parser reads a spec, a step can name a control through a *control name* that a
component declares, and verbs can be declared outside the control (extension verbs). Chapters are a small grammar
addition on top.

## 1. Where specs live

Anywhere. Bruinet has no preferred place; the suite says where in its config. Both of these are valid, and a suite may
use both:

```text
Bouw7/
  specs/                              a top-level folder, as Bouw7 has today
    B7.CON.01.spec.md
  Bouw7.Web.UITests/
    uat.config.md
    Modules/
      Contacts/
        Specs/
          B7.CON.02.spec.md           or in the module, beside its pages and features
        Features/
        Pages/  Tests/
```

- `uat.config.md` gets a `Specs` row, next to `Features`. It names one or more folders, relative to the config:
  `Specs | ../../specs, Modules`. The runner reads `**/*.spec.md` under each.
- The test project copies the spec files to its output, wherever they are (`<Content Include="../specs/**/*.spec.md"
  Link="Specs/%(RecursiveDir)%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" />` for a folder
  outside the project).
- **The namespace** (AD-019) is the folder under its `Specs` root: `Modules/Contacts/Specs/` is `Contacts/Specs`,
  which sees `Contacts`' phrases. A spec in a flat `specs/` folder is in the root namespace and sees the global phrases
  only, so it names its module with `Using`. To keep that one line out of every spec, the spec's id maps to a module:
  a `## Modules` table in `uat.config.md` (`| Id | Namespace |`, `| B7.CON | Contacts |`) puts a flat spec in its
  module's namespace. Proposal; see the open questions.
- Frainet doesn't care where a spec is: `frainet check` and `frainet trace` take folders today. The site loop's gate 1
  reads the folders the `Specs` row names.

## 2. Binding a step to the real control

### The problem

`When I type "zzz-nothing" in the filter above Name` has three parts:

| Part | What it is | Reused by |
| --- | --- | --- |
| `I type {value} in {control}` | a **verb**: an action any text field can do | every text field, every page |
| `the filter above {column}` | a **control name**: how a person names one control inside a component | every page with a Heimdall table |
| `Name` | a value in the control name | |

Today a phrase is either a verb on a control the page exposes by name (`I enter {value} into First Name`), or a whole
page phrase written per page (`I filter contacts on {text}`). The first can't reach a control that has no fixed name
(the filter of the column called Name); the second is written again for every page and every verb. So in phase 4 every
example became C#.

### Proposal: control names, and verbs as extensions

**A. Control names.** A component declares how a person names the controls inside it, on the method that returns them:

```csharp
public partial class HeimdallTable<TScope>
{
    [BddControl("the filter above {column}")]
    public VTextField<TScope> FilterAbove(string column) => TextFilter(ColumnIndex(column));

    [BddControl("the footer")]
    public TextControl<TScope> Footer => ...;

    [BddControl("the row {item}")]
    public HeimdallRow<TScope> Row(string item) => ...;
}
```

`{control}` in any verb is then resolved in this order, on the current page:

1. a control the page exposes by name (today's rule: `First Name`, `the dialog`);
2. a control name declared on the page, or on a component the page exposes, that matches the text;
3. otherwise `UATB009`, listing the names the page has, fixed and declared.

Two components on one page that match the same control name (two tables) are ambiguous (`UATB002`), as everywhere else. The
step then names the component: `the filter above Name in the Contacts list` (`<control name> in <component>`, one level; see
the open questions).

The method's return type decides which verbs fit: `VTextField` implements `IEditableTextControlObject`, so every text
verb works on `the filter above Name`, and binding checks it (`UATB009` when it doesn't), before anything runs.

**Why a new attribute.** The existing ones each do something else:

| Attribute | What it declares | Why not this |
| --- | --- | --- |
| `[BddGiven]`, `[BddWhen]`, `[BddThen]` | a **step**: a whole line, matched against a step's text, that does or checks something | A control name is never a line of its own; it fills the `{control}` slot of a step. Putting it under `BddWhen` would make it a step that does nothing, and a verb couldn't find it. |
| `[UatName("Role")]` | a **fixed name** for a page or a control property | Closest in meaning, and a control name is a `UatName` with placeholders. But it lives in `Bruinet.Uat`, which control libraries (`Bruinet.Html`, `Bruinet.Web.Vuetify2`) don't reference; the step attributes moved to `Bruinet.Bdd.Abstractions` for that reason (AD-011). |

So the new attribute sits in `Bruinet.Bdd.Abstractions`, beside the step attributes. A later step could let it take
over `UatName`'s job too (a name without placeholders is a fixed name) and retire `UatName`; not in this design.

**The name.** It is not a locator: `Locator` in Bruinet is how a control is found in the DOM or the UI tree
(`Locator.ByCss(...)`), and the method behind the attribute returns a whole control object that carries one.
`[BddLocator]` would make readers expect a CSS selector. What the attribute declares is "the words that name this
control", the text that fills `{control}`, so it's `[BddControl("the filter above {column}")]` (decided 2026-11-15). Other names
weighed: `BddName` (pairs with `UatName`, but says less about what it names), `BddNoun` (the first draft; grammar
terms, not Bruinet's).

So one control name and the existing verbs give, with no new code:

```gherkin
When I type "arend" in the filter above Name
When I clear the filter above City
Then the filter above Name should equal "arend"
Then the filter above Code should be enabled
```

**B. Extension verbs.** A verb for a control or interface you don't own, or one that reads better than the control's
own method, is a static method whose first parameter is the control, marked `this`:

```csharp
[BddVerbs]
public static class TableVerbs
{
    [BddThen("{control} has the columns {columns}")]
    public static void HasColumns(this ITableControlObject table, string[] columns) { ... }

    [BddThen("{control} shows only {values}")]
    public static void ShowsOnly(this ITableControlObject table, string[] values) { ... }

    [BddThen("{control} shows no {things}")]
    public static void ShowsNone(this ITableControlObject table, string things) => table.AssertRowCount(0);

    [BddThen("every {thing} in {control} has the {column} {value}")]
    public static void EveryRowHas(this ITableControlObject table, string thing, string column, string value) { ... }
}
```

- It binds like a verb declared on the type: `{control}` is resolved (fixed name, then declared control name) and must be of the `this`
  parameter's type. It is also a real C# extension method, so a C# test can call `page.Contacts.ShowsOnly(...)`.
- The class's assembly and namespace decide who sees it (AD-019): Bruinet's verbs for everyone, a control library's
  for its users (`Bruinet.Web.Vuetify2`), a suite's for its modules.
- The verb is written once per interface, not per page. A new table type gets every table verb by implementing the
  interface.
- `ITableControlObject` is new in Core: header texts, row count, a column's text by row, the "key column" (the first
  column, or `[UatKeyColumn("Name")]`) that `shows only "Arend de Krom"` compares. Bruinet.Html's `TableControl` and
  Bouw7's `HeimdallTable` and `B7DataTable` implement it.

**Tense.** A Given often says what was done before the When (`And I typed "arend" in the filter above Name`). The
same method takes both: `[BddWhen("I type {value} in {control}")]`, `[BddGiven("I typed {value} in {control}")]`.
Bruinet's built-in verbs get their past form.

**Aliases.** "the list" for a page's one table: `[UatName("list")]` on the page property, or the rule that `the list`
finds the page's one `ITableControlObject`, as a verb without `{control}` finds its one control today.

### The other options, and why not

| Option | What it is | Why not alone |
| --- | --- | --- |
| Page phrases | `[BddWhen("I type {value} in the filter above {column}")]` on `ContactsPage` | Written per page and per verb: Projects, Contacts and Invoices would each need their own. Still right for a page's own workflow ("I log {hours} hours on {project}"). |
| Verbs without `{control}` on the table | `[BddWhen("I type {value} in the filter above {column}")]` on `HeimdallTable` | Works today, but every verb × control is a method: type, clear, should equal, should be empty... Fine as a first step for a single phrase. |
| Callable phrases (`.steps.md`) | Steps that call steps, no C# | Can't compute "the column called Name" from a name; they compose phrases, they don't make new ones. Right for naming a sequence (see section 3). |
| Step-definition classes | `[BddStepDefinitions]` with DI | For flows below the UI (an API call, test data), not for one control. |
| Resolving by a model at run time | An agent maps the sentence to a control | Not deterministic; a test that binds differently on two runs is not a test. Out. |

**Decided 2026-11-15:** extension verbs (B), and control names (A) as `[BddControl]`. Page phrases stay
for workflows, callables for sequences.

Extension verbs also stand alone: without control names, `[BddWhen("I type {value} in the filter above {column}")]`
as an extension on `ITableControlObject` (no `{control}`, the page's one table) works and is reused by every page with
a table. What control names add is that one name serves every verb, so the type, clear and should-equal forms aren't
each a method. Extension verbs come first in the plan (RS3); control names (RS4) build on them.

### What the Contacts spec binds to

| Step | Verb | Control |
| --- | --- | --- |
| `Given Creator is signed in` | fixture, `{account} is signed in` | |
| `When I open the Contacts page` | built-in, new When form of `I am on the {page} page` | page `Contacts` |
| `Then I see the title "Contacts"` | `{control} should equal {value}`, or a new `I see {control} {value}` | `[BddControl("the title")]` on `PageHeader` |
| `And I see "Export", "Deduplicate" and "+ Contact"` | extension verb `I see the actions {names}` on `PageHeader` | |
| `Then the list has the columns Name, Address, …` | `{control} has the columns {columns}` | alias `the list` |
| `Then the footer says "1-14 of 14"` | `{control} says {value}` (`AssertText`) | `[BddControl("the footer")]` |
| `When I type "arend" in the filter above Name` | `I type {value} in {control}` (`SetText`) | `[BddControl("the filter above {column}")]` |
| `Then the list shows only "Arend de Krom"` | `{control} shows only {values}` | alias `the list` |
| `When I open the filter above Contact Type` | `I open {control}` (`ISelectorControlObject.Open`) | the same |
| `Then I can choose Client, Supplier, …` | `I can choose {values}` on the open selector | the selector just opened |
| `When I empty the filter above Name` | `I empty {control}` (alias of `Clear`) | the same |

Eleven step forms; about four new verbs and three control names. All of them reach Projects, Invoices and Hours too.

## 3. Saying who and where once

Every Contacts example starts with `Given Creator is signed in` and `When I open the Contacts page` (or
`And I am on the Contacts page`). R6 even forgot the first line. Three ways, smallest first:

**a. A callable phrase** (works today, no engine change):

````markdown
## Step: {account} is on the {page} page
```gherkin
Given <account> is signed in
And I am on the <page> page
```
````

One line per example instead of two. Still repeated.

**b. `## Background` in the spec.** The grammar has it for features; a spec gets the same section, after Rules:

````markdown
## Background

```gherkin
Given Creator is signed in
And I am on the Contacts page
```
````

Its steps go in front of every example. The spec already says it in prose ("Each rule holds on the Contacts page"), so
the Background is that line made exact. Its weakness: one Background for the whole spec, so B7.INV.01's R5, which is
Reader's, can't be in the same file without overriding it, and overriding is shadowing, which Bruinet doesn't do.

**c. Chapters** (section 4): a Background per group of rules. This is the recommendation; (b) is a chapter that holds
every rule, so a spec without chapters can still have a `## Background`.

## 4. Chapters

A chapter is a group of examples that share their start: one Given and When, written once, then the Thens of each rule.

````markdown
## Examples

### Chapter: The list at a glance

Creator opens the Contacts page and looks, nothing more.

```gherkin
Given Creator is signed in
When I open the Contacts page
```

#### R1 The page at a glance

The person sees where they are and how to add, export or clean up contacts.

```gherkin
Then I see the title "Contacts"
And I see "Export", "Deduplicate" and "+ Contact"
```

#### R2 What a contact shows in the list

The columns hold what the office looks for most: who, where, and what kind of contact.

```gherkin
Then the list has the columns Name, Address, Postcode, City, Code, Contact Type and Objects
```

#### R3 How many contacts

```gherkin
Then the footer says "1-14 of 14"
```

### Chapter: Filtering

```gherkin
Given Creator is signed in
And I am on the Contacts page
```

#### R4 Part of a name is enough

People remember "Krom" or "arend", not "Arend de Krom".

```gherkin
When I type "arend" in the filter above Name
Then the list shows only "Arend de Krom"
```

#### R7 No match, no rows

```gherkin
When I type "zzz-nothing" in the filter above Name
Then the list shows no contacts
```
````

### The rules of a chapter

- **Grammar.** Under `## Examples`, a `### Chapter: <name>` heading, an optional paragraph, and one gherkin block of
  `Given` and optionally `When` steps, no `Then`. Its examples are `#### R<n> <name>`, one level lower than today.
  A spec without chapters keeps `### R<n>` and is read as today.
- **The example a reader sees** is the chapter's steps followed by the example's. Frainet's check runs on that whole:
  Given, When and Then in order, one example per rule. `frainet show <spec> --expanded` prints every example whole,
  for an owner who wants to read them one by one.
- **How it runs depends on the example:**

  | The example has | It runs | Why |
  | --- | --- | --- |
  | only `Then` steps | **once per chapter**: the chapter's steps run once, then each rule's Thens, one after another, on that state | a Then only binds to `[BddThen]`, an assertion, so it can't change what the next rule sees |
  | a `When` of its own | **on its own**: the chapter's steps as its Background, then its steps | its When changes the page; the next rule must not start from it |

  So R1, R2 and R3 above open one browser and one page, and check three rules. R4 and R7 each start fresh.
- **Each rule keeps its own result.** A chapter is one theory case per rule, not one per chapter. The first case of a
  chapter runs the chapter's steps; the others reuse the state (a chapter fixture, ordered with `BddScenarioOrderer`,
  which already orders by file and position). A rule's failing Then fails that rule only; the next rule's Thens still
  run. A failing chapter step fails every rule of the chapter with "the chapter's start failed: …".
- **Run alone.** `--filter Rule=B7.CON.01.R2` runs the chapter's steps and R2's Thens. Same steps, same result as in
  the whole run.
- **A chapter per account and start.** B7.INV.01's R5 (Reader) is its own chapter. No overrides: an example can't
  replace its chapter's Given, it can only add steps.
- **Nesting:** none. A spec-wide `## Background` comes before every chapter's steps; that's the one level above.

### Why chapters and not just Background

The Background saves lines; the chapter also saves runs. Bouw7's read-only rules (CON R1–R3, INV R1–R4, PRJ R1–R3)
each sign in and open a page today, about 15 s apiece on acc. As chapters they cost one start each. And grouping
rules by their start tells a reader something: "these are what you see when you open the page", "these are what the
filters do".

## How a spec runs

```text
*.spec.md
  -> SpecDocumentReader (Bruinet.Bdd)   the spec's own grammar: header table, Rules, Background, chapters, examples
  -> BddDocument                        one scenario per example, named "<rule id> <example name>"
  -> the binder, catalog, runner        unchanged, plus control names and extension verbs
  -> UatSpecTheory                      one case per example; traits Spec, Rule, BddScenario
```

- **Gherkin, prose, the rest.** Only the gherkin blocks run. Intent, the rule table, Choices, Not in this journey and
  Open questions are read by Frainet, not the runner.
- **Traits.** Every case carries `Rule = B7.CON.01.R4` and `Spec = B7.CON.01`, as the C# tests do. So
  `frainet trace --results` reads a spec run's TRX the same way, and `--filter Rule=…` runs a rule whether it is a
  spec example or a C# test.
- **Unbound examples.** An example whose step binds to no phrase fails at bind time with `UATB001` and the phrases
  near it. Frainet's trace gets a fifth state, `Unbound`, before `NotRun`: the rule has an example, and it can't run
  yet. The spec stays valid for readers; it is not done for the site loop.
- **Status.** Drafts run too: that is `spec-from-app` checking its examples. The site loop's gate 1 still needs
  `accepted`.
- **Test data.** An example that makes something (B7.HRS.01.R7) uses a page phrase that registers its undo with a
  scenario-scoped `CleanUpRegistry` (in the composition), disposed in `AfterScenario`, pass or fail. The marker is a
  value token, `{marker}`, resolved to `RunMarker.Current`, so the spec says
  `When I log 2 hours of Gewerkte uren on "00002 - Project 001" with the note {marker}` without saying how.
- **Checked once.** A rule is checked by its spec example or by a C# test with its `Rule` trait, never both (AGENTS.md,
  "A UAT scenario or a code UI test"). Trace warns on a rule with both. A C# test stays for what an example says
  badly: a wait, a race, a layout.

## What changes in Frainet

- `spec-format.md`: chapters, `## Background`, `####` examples under a chapter.
- `frainet check`: the whole example (chapter plus own steps) has Given, When and Then in order; a chapter has no Then;
  a Then-only example sits in a chapter.
- `frainet trace`: reads `*.spec.md` examples as tests of their own rules; the `Unbound` state from the run's results.
- `frainet show --expanded`: the examples written out whole.
- Frainet doesn't reference Bruinet. The grammar is written down in `spec-format.md`; `Bruinet.Bdd` implements the
  reading side, as it implements the feature grammar.

The format's line "the spec stays free of tests" still holds: no binding, class or method is in the spec. What
changes is that its words must be words the vocabulary knows. That is a constraint on wording, the same one features
have, and `bruinet data phrases <spec>` shows the vocabulary a spec sees.

## Plan

| Step | What | Done when |
| --- | --- | --- |
| RS1 | `SpecDocumentReader`, config `Specs` row, `UatSpecTheory` with `Rule` and `Spec` traits; spec `## Background` | B7.INV.01 runs as written (no chapters), its rules pass, trace reads its TRX |
| RS2 | Chapters: grammar, Frainet check, the chapter fixture, per-rule results | INV R1–R4 run as one chapter: one sign-in, four results, a failing R2 leaves R3 passing |
| RS3 | Extension verbs (`[BddVerbs]`, `this` parameter), `ITableControlObject`, past-tense Givens | Table verbs written once, used by Contacts, Projects and Invoices |
| RS4 | Control names (`[BddControl]`) and the `{control}` resolution order | `the filter above Name` binds on Contacts and Projects; `UATB009` lists them |
| RS5 | Bouw7 pilot: CON.01, PRJ.01, INV.01 as runnable specs, left in `Bouw7/specs/`; their C# tests deleted where the example covers the rule | Three runs green; run time measured against the C# tests |
| RS6 | Editors and tools: completion and go-to-phrase in `.spec.md` (VS Code, VS preview, MCP `list_steps`), `bruinet data phrases <spec>` | A step in a spec completes as in a feature |
| RS7 | Skills and docs: `spec-from-app` runs its examples; `scenario-from-spec` becomes "make the spec bind" (control names and verbs first, C# only for what an example says badly); an AD for runnable specs and control names; `docs/guides/runnable-specs.md` | The site loop takes a new module with no C# test |

RS1 and RS2 are the core; RS3 and RS4 are what make it worth it for Bouw7. HRS.01 (writes data, a UTC date) is the
last to move.

## Open questions

- **A control name in a component.** `the filter above Name in the Contacts list` when a page has two tables: one level of
  `<control name> in <component>`, or none until a page needs it? Proposal: none in RS4; the binder's ambiguity message names
  the components, and a page phrase covers the rare page.
- **A flat spec's module.** A spec in a top-level `specs/` folder is in the root namespace. Map its id to a module in
  the config (`| B7.CON | Contacts |`), or have it write `Using`? Proposal: the map, once per suite; `Using` still
  adds more.
- **Chapter on the Rules table.** Should the Rules table show the chapters too (a column, or a line per group)?
  Proposal: no; the rules stay a flat, numbered list, and chapters are how examples are told.
- **Then-only examples with a When in a later chapter step.** A chapter's start could also hold `And` steps after its
  When. Proposal: allowed; the start is any Given and When steps, in order.
- **Features and specs side by side.** Should a feature be able to call a spec example by its rule id
  (`Given B7.CON.01.R4 holds`)? Proposal: no; that makes a test depend on another test.
