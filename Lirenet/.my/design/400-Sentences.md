# Sentence styles

Sentences answer "what must hold" and "what happens in what order". They are what Lirenet has today.
The proposal is not to drop them, but to use each only for the questions it answers best. Score
cards as in the [readme](000-Readme.md).

## S1 Checklist

**Answers:** Q15 what a smoke run must see; small facts with no steps.

```markdown
Departments page
- [ ] opens from Master data → Departments
- [ ] shows the title "Departments"
- [ ] shows the button "+ Department"
- [ ] lists at least one department
```

- The lightest style there is. A non-programmer writes it.
- Becomes a test only line by line with a phrase per line, so it is weaker than a screen card,
  which says the same in fixed columns. Best as a **first draft** that becomes a card, or for
  facts that fit no table.

| C1 | C2 | C3 | C4 | C5 | C6 | C7 |
| --- | --- | --- | --- | --- | --- | --- |
| ●●● | ●●● | ●●○ | ●○○ | ●●○ | ●●● | ●●● |

## S2 One-line rule (EARS)

**Answers:** Q11 what always holds; Q12 what happens when it goes wrong.

| Id | Rule |
| --- | --- |
| TOD.02.R9 | When a new todo is saved, the app shall keep it on the device and show "Local only" until the next sync. |
| TOD.02.R4 | If a save is refused, then the app shall keep nothing and leave the form open with what was typed. |

- The rule format of the [EARS proposal](../../../../.my/ears/ears-proposal.md): one sentence, one
  response, checkable by keyword.
- **The right home for what tables cannot hold:** dependencies between fields, timing, what is
  kept and what is lost.
- Becomes tests only through an example or a scenario; the sentence alone is not runnable.

| C1 | C2 | C3 | C4 | C5 | C6 | C7 |
| --- | --- | --- | --- | --- | --- | --- |
| ●●● | ●●○ | ●●● | ●●● | ○○○ | ●●● | ●●● |

## S3 Gherkin scenario

**Answers:** Q10 what happens step by step; Q12 and Q13 when recovery or a second app has steps.

```gherkin
Given the list holds "Buy milk"
And no connection to the server
When I add "Write the report"
And the connection comes back
Then "Write the report" shows "Synced" within 30 seconds
And the list shows "Write the report" and "Buy milk"
```

- **Keep it for order and side effects.** Not for what is on a screen (a card), not for which
  fields are required (a field table), not for getting there (a flow).
- **Shorter by the other styles:** when the flow says how to reach a screen and the screen card
  says what is on it, a scenario starts at the point that matters and checks only the outcome.
  The Bouw7 count in the [readme](000-Readme.md) suggests about half of today's lines go.
- Becomes tests as now, through phrases bound to page objects.

| C1 | C2 | C3 | C4 | C5 | C6 | C7 |
| --- | --- | --- | --- | --- | --- | --- |
| ●●○ | ●○○ | ●●● | ●●○ | ●●● | ●●○ | ●●● |

## S4 Prose

**Answers:** Q16 what the person wants, and why.

Two or three sentences of intent, as specs have now, and the short "why" paragraph above an
example. Never a rule: prose cannot be checked, so nothing that must be true lives only in prose.

## Which sentence when

| If the thing to say is… | Use |
| --- | --- |
| A fact that is true on its own | One-line rule |
| A fact about a screen's parts | Not a sentence: a screen card or a field table |
| Something that happens in order | Gherkin scenario |
| What a smoke run must find, before the screen card exists | Checklist |
| Why it matters | Prose |

## Questions about sentences

- Does every one-line rule still need a Gherkin example, as specs do now? Proposal: no. A rule
  needs **one** proof: a table row, an example table, or a scenario. Whichever is simplest.
- May a scenario point at a table row instead of repeating it ("Then the form refuses as the field
  table says")? Proposal: yes; that is how a scenario stays short.
- Is the checklist worth keeping when the screen card exists? Proposal: as a draft only; Lirenet
  offers to turn a checklist into a card.
