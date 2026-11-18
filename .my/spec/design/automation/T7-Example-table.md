# T7 Example table: automation

The style: [../200-Tables.md](../200-Tables.md#t7-example-table). Two entry points, picked by where the
logic lives: **B, a scenario outline**, when the example is about the screen; **C, a spec-fed theory**, when
it is about a function. Back to the [readme](000-Readme.md).

## What the spec says

```markdown
## Examples: Title

| Typed as title | Saved as | Accepted |
| --- | --- | :---: |
| `Buy milk` | Buy milk | ☑ |
| `  Buy milk  ` | Buy milk | ☑ |
| `   ` | | ☐ |
| 100 × `x` | 100 × `x` | ☑ |
| 101 × `x` | | ☐ |
```

## What proves it, by layer

| | Unit | Integration | UI fast | UI judge |
| --- | --- | --- | --- | --- |
| Each row, when one function decides (trim, validate) | **✓** | | | |
| Each row, when it takes the screen and the store together | | ✓ `service` head | **✓** an outline | |
| That the screen uses the function at all | | | ✓ two rows | once |

## The simplest automation

**C: when one function decides** (Todo's `TodoValidation.ValidateTitle`, the trimming), a theory fed by the
spec, as in T4:

```csharp
[Theory]
[SpecTable("TOD.02", "Examples: Title")]
public void Title(SpecRow row)
{
    var result = TodoValidation.ValidateTitle(row.Value("Typed as title"));
    Assert.Equal(row.Tick("Accepted"), result.IsValid);
    if (result.IsValid) Assert.Equal(row.Value("Saved as"), result.Title);
}
```

The result's shape (`IsValid`, `Title`) stands for whatever the app's validator returns. Cells hold values
in a small fixed grammar: backticks keep spaces (`` `  Buy milk  ` ``); `100 × \`x\``
repeats; an empty cell is empty.

**B: when the example is about the screen**, the same table becomes the Examples of a scenario outline in the
spec. Bruinet runs outlines today, with named Examples sections and a row number that stays stable (AD-036):

```gherkin
Given a new todo's form
When I type <Typed as title> as the title
And I press Save
Then the list shows <Saved as>
```

The outline's placeholders are the table's column names. **The step to add**: the spec runner taking an
`## Examples: <name>` table from the spec as the outline's examples, so the table is written once, above,
for the reader, and not again under the outline.

**Pick one, not both.** If the function decides, the theory proves every row and the screen needs two rows
in an ordinary example (one accepted, one refused).

## What it needs

| Need | Exists? |
| --- | --- |
| Scenario outlines with named Examples | ✓ (AD-036) |
| An outline using a named table of the spec as its examples | **New**, small |
| `SpecTable` and the value grammar (backticks, repeats, empty) | **New**, small (Lirenet) |

## Questions

- Repeats such as `100 × x`: worth a grammar, or write the value out? Proposal: the grammar; 100 x's are
  unreadable and mistyped.
- Can one example table feed both a theory and an outline? Proposal: technically yes; Lirenet warns, since a
  rule should be proven once.
