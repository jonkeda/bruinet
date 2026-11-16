# Runnable Specs

A Lirenet spec (`*.spec.md`, [the format](../../Lirenet/docs/spec-format.md)) runs as tests on the same engine as the
feature files: each example is a scenario, named and tagged with its rule. The example *is* the test, so nothing has
to copy it into C#, and a person can read what runs. Features stay for longer journeys told step by step; specs hold
rules and an example per rule.

## Set up

1. **Say where the specs are**, in `bdd.config.md`'s Runtime table, relative to the config. One or more folders, each
   read with its subfolders, and a folder that does not exist is left out. Specs may be anywhere: a top-level
   `specs/` folder for their readers, or beside the pages in `Modules/<Module>/Specs/`.

   ```markdown
   | Specs | ../specs, Specs |
   ```

2. **Copy them to the output**, as feature files are. A spec outside the project is linked in:

   ```xml
   <None Include="..\specs\B7.INV.01.spec.md" Link="Specs\%(RecursiveDir)%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" />
   ```

   List specs one by one while a suite moves to them; `..\specs\**\*.spec.md` once every spec's steps bind.

3. **Run.** The suite's scenario test class (`BddScenarioTestBase`) finds them with the features. Each case carries the
   traits `Rule` (`B7.INV.01.R1`) and `Spec` (`B7.INV.01`):

   ```bash
   dotnet test --filter "Spec=B7.INV.01"
   ```

## How a spec reads

| In the spec | Runs as |
| --- | --- |
| `### R1 The page at a glance` and its `gherkin` block | the scenario `B7.INV.01.R1 The page at a glance`, tagged `@B7.INV.01.R1` |
| `## Background` | steps before every example |
| `### Chapter: Creator opens the invoices` with Given and When | steps before each of its examples, `#### R1 …` |
| `### R5 …` after a chapter | an example outside the chapter |
| Intent, Rules, Choices, Not in this journey, Open questions | nothing: they are for readers and Lirenet |

Every step keeps its line, so a failure, the editors and `bruinet data validate` point into the spec.

## Making a step run

`bruinet data validate Specs/B7.INV.01.spec.md`, from the test output folder, lists each step that binds to nothing.
Make it bind, in this order:

1. **Say it the way the vocabulary does** (`bruinet data phrases`), the rule's meaning unchanged. Built-in phrases a
   spec reads well with: `When I open the {page} page`, `Then the {page} page is open`, `{control} says {value}`,
   `{control} includes {value}`, `{control} is shown`, `{control} is not shown`, `When I type {value} in {control}`, `Given I typed …`,
   `When I empty {control}`, `When I tap {control}`. A spec never says "should" (Lirenet's vague word).
2. **Name the control** where it has no fixed name: `[BddControl]` on the page's or a component's method; its
   placeholders are the method's parameters. Every verb of the returned control's type then works on it.

   ```csharp
   [BddControl("the filter above {column}")]
   public VTextField<TScope> FilterAbove(string column) => TextFilter(ColumnIndex(column));
   ```

   `[BddControl("list")]` on a page's property gives it a fixed name (`the list`); on a component's property, it names
   a control inside the component (`the footer`).
3. **Write the verb once for the control's type**, as an extension verb: a static class marked `[BddVerbs]`, the
   control first (`this`). A generic one reaches every control derived from its type:

   ```csharp
   [BddVerbs]
   public static class TableVerbs
   {
       [BddThen("{control} shows only {values}")]
       public static void ShowsOnly<TScope>(this TableControl<TScope> table, string[] values)
           where TScope : IHtmlScope<TScope> { ... }
   }
   ```

   Bruinet.Html's `TableVerbs` gives every table `{control} has the columns {columns}`, `{control} shows only
   {values}`, `{control} is empty`, `{control} has rows` and `every row of {control} has {value}`. A suite writes its
   own for its components (Bouw7's `the page offers {actions}`). A verb may take several values, each parameter
   by its placeholder's name, read as its type (`{control} counts {count} in the card {card}`, `int count`). The
   methods are ordinary C# extension methods too.
4. **A page phrase** for a page's own workflow, several controls in one intent (`I log {hours} hours on {project}`),
   or a fixture's phrase where the step makes data on a shared environment: the fixture marks it and removes it after
   the scenario (Bouw7's `I save the booking`).

## One check per rule

A rule is checked by its example or by a C# test with its `Rule` trait, never both: delete the C# test when the example
runs. A C# test stays for what an example says badly (a race, a layout, a polled value).

`lirenet trace <spec> <tests> --runnable` counts the spec's examples as the tests of their rules; with `--results` it
matches each by its case's name. The site loop (`tools/Scripts/site-loop.ps1`) passes `--runnable`, and runs
`Spec=<spec>`, for every spec the test project copies.

## Not yet

- A chapter's examples each run its steps again; sharing one run between a chapter's Then-only examples (they are
  tagged `chapter-then` and `chapter:<name>` for it) is planned, not built. With a fixture that keeps its signed-in
  browser between scenarios (Bouw7's `NonOwningContext`), the repeat costs a page load.
- One example per `gherkin` block: two examples in one block, separated by a blank line, run as one scenario.
