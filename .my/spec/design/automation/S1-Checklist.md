# S1 Checklist: automation

The style: [../400-Sentences.md](../400-Sentences.md#s1-checklist). Entry point **B**: each line is one step
on a page, run by the existing spec runner. Back to the [readme](000-Readme.md).

## What the spec says

```markdown
Departments page
- [ ] opens from Master data → Departments
- [ ] shows the title "Departments"
- [ ] shows the button "+ Department"
- [ ] lists at least one department
```

## The simplest automation

**Each line is a `Then` on the page named above the list.** The spec runner reads a checklist as a scenario:
`Given the Departments page`, then one step per line. A line binds the way a step does; most bind to
built-in phrases with a small rewording that Lirenet can suggest:

| Line | Binds to |
| --- | --- |
| shows the title "Departments" | `the title says "Departments"` (`{control} says {value}`) |
| shows the button "+ Department" | `+ Department is shown` (`{control} is shown`) |
| lists at least one department | a list verb: `the list has rows` (Bruinet's `AssertHasRows`) |
| opens from Master data → Departments | a navigation: open Master data's menu, press Departments, the page is open |

Each line is its own test case (a failing line does not hide the next), named by the line, with the rule id
of the checklist. The ticked boxes in the file are **not** written by the run; the trace shows the state.

## When to move on

A checklist is the first draft of a **screen card** (P1): the same facts in fixed columns, which need no
phrases at all. Lirenet offers the conversion when every line of a checklist is a title, a button, a field
or a "reached from". Lines that fit no column stay as checklist lines or become one-line rules.

## What it needs

| Need | Exists? |
| --- | --- |
| Running steps against a page, built-in phrases | ✓ |
| A checklist under a page name read as a scenario of independent checks | **New**, small, in the spec runner |
| Suggesting the phrase for a line | ✓ `bruinet data phrases`, `bruinet data validate`; **new**: a suggestion per unbound line |

## Questions

- Free wording per line, or the phrase vocabulary only? Proposal: free wording in a draft; a checklist that
  is accepted must bind, as an example must.
- Does a checklist line need an id? Proposal: no; the checklist has one, and a line is named by its text.
