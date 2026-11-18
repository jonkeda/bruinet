# P2 Sketch and Wireframe: automation

The style: [../300-Pictures.md](../300-Pictures.md#p2-sketch-a-wireframe-in-mermaid), and the screen source
format of [../310-Screens.md](../310-Screens.md) and [../330-Salt-vs-Wireframe.md](../330-Salt-vs-Wireframe.md).
Entry point **A**, by turning the picture into a screen card and a field table. Back to the
[readme](000-Readme.md).

## The principle

A picture is not tested as a picture. What it **says** is tested: which screen, which fields of which kind,
which are required, which buttons, where each leads. Those are exactly the screen card (P1), the field table
(T1) and the flow (P4). So the simplest automation is a **reader** that turns the picture's source into
those three, and their checks do the rest. Layout (`gap=16`, `justify=end`) is never checked.

## From a Wireframe screen

```wireframe
wireframe sketch
    %title: Department form
    Vertical
        Horizontal
            Link "Back" @Departments
            Heading "Create department"
        /Horizontal
        TextInput :Name label="Name" required message="Name is required"
        Dropdown :Branch label="Branch"
            Option "Main office"
            Option "North"
        /Dropdown
        Horizontal justify=end
            Button "Cancel" @Departments
            Button "Create" :Create primary @Departments
        /Horizontal
    /Vertical
/wireframe
```

| In the Wireframe | Becomes | Checked by |
| --- | --- | --- |
| `%title`, `Heading` | The card's screen and title | P1 |
| `Button`, `Link`, `primary` | The card's buttons, the submit | P1 |
| `TextInput`, `Dropdown`, … with `label=` | Field rows, Kind from the control type | T1 |
| `required`, `message=`, `min=`/`max=`, `Option`s | Required, Message, Limits, choices | T1 |
| `:Name` ids | The page object member to bind to, by name | T1, and a drift check: id ↔ member |
| `@Departments` on a button | A flow arrow: this button leads there | P4 |
| `validations` table | Extra field rules with messages | T1 |
| `if … else` | Two states of the screen; each state's card checked in a state put there by a `[BddState]` named like the condition | P1 |

**How**: the Wireframe CLI now has `wire parse <file>` (the tree as JSON, added in the review fixes). Bruinet
or Lirenet reads that JSON, or a small C# reader of the same subset, and builds the card, fields and arrows
in memory. Nothing is written back into the spec.

## From a Mermaid sketch

A `block-beta` sketch has boxes and labels, no kinds and no `required`. It can give only the **names** of
what is on the screen: a check that every label in the sketch is a control of the page object. That is the
"exactly these" of P1 in a weaker form, and is all a sketch can prove. Use it for screens whose grouping
matters, with a card or field table beside it for the facts.

## From PlantUML Salt

330 found that Salt has no ids, no attributes, no required mark, no link from a button to a screen, and no
way to hand its tree to another tool. A reader of its text (ours to write) could give the card's button
labels and the texts beside inputs, as a sketch does; every fact (kind, required, messages, where a button
leads) would stay in a field table and a flow beside it.

## What it needs

| Need | Exists? |
| --- | --- |
| A parse tree from the screen source | ✓ `wire parse` (Wireframe); Mermaid and Salt: **new**, a label list only |
| Wireframe → card, fields, arrows | **New**, small, after the P1 and T1 checks exist |
| Ids ↔ page object members, as a drift report | **New**, small |

## Questions

- Does the reader need Node (`wire parse`) at test time? Proposal: no; run `wire parse` when the screen
  changes and keep the JSON beside the `.wire` file, checked by a stale check, as the committed SVG is.
- If a screen has both a Wireframe and a field table, which wins? Proposal: one source; the field table is
  generated from the Wireframe for readers, never written by hand beside it.
