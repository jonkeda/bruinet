# The questions a spec answers, and the style that answers each best

The core of these proposals. Every spec answers some of these questions. For each, the style that
answers it most simply, a second choice, and what to avoid. The styles are described in
[200-Tables.md](200-Tables.md), [300-Pictures.md](300-Pictures.md) and [400-Sentences.md](400-Sentences.md).

## The map

| # | Question | Best | Second | Avoid |
| --- | --- | --- | --- | --- |
| Q1 | **What is on this screen?** Title, buttons, sections, tabs | Screen card (table) | Sketch, annotated screenshot | Gherkin: one `And … is shown` per item |
| Q2 | **Which fields does this form have?** Kind, required, default, limits | Field table | Screen card, if few fields | Gherkin per field; prose |
| Q3 | **What may go in a field, and what is refused?** Limits, formats, messages | Field table (limits column) plus a message list | Example table for the edge values | A scenario per limit |
| Q4 | **What does a list show?** Columns, order, empty state, paging | List table | Example table: these rows in, these shown | Gherkin `Then the list shows … And …` |
| Q5 | **How do I get from here to there?** | Flow | Journey map, for a long way | Navigation as `Given I am on …` repeated in every example |
| Q6 | **Which states can a thing be in, and what moves it?** | State diagram | State transition table | Prose; scattered rules |
| Q7 | **What is the outcome of this combination?** Several conditions together | Decision table | Example table | One scenario per combination |
| Q8 | **Who may do what?** | Rights matrix | One-line rules per role | A scenario per role and action |
| Q9 | **What does a thing consist of, and how do things relate?** | Domain model | Field table per thing | Prose |
| Q10 | **What happens, step by step, when a person does this?** Order matters, side effects | Gherkin scenario | Flow with notes | A table: order gets lost |
| Q11 | **What must always hold?** One fact, no steps | One-line rule (EARS) | Checklist | A scenario with one `Then` |
| Q12 | **What happens when it goes wrong?** Errors, lost connection, refusals | One-line rule (`If … then …`) plus a message list | Gherkin, when recovery has steps | Hidden in a happy-path scenario |
| Q13 | **What crosses systems or time?** Phone → office, a sync, a job | Sequence diagram | Gherkin with two subjects | Prose |
| Q14 | **Does it look right?** Layout, what stands out | Annotated screenshot | Sketch | Any text: it cannot say it |
| Q15 | **What must a smoke run see?** Page opens, the essentials are there | Checklist | Screen card (the card *is* the checklist) | Gherkin |
| Q16 | **What does the person want, and why?** | Prose: the intent, two or three sentences | — | Leaving it out |

## What the map says

- **Ten of the sixteen questions are best answered by a table or a picture.** Gherkin is best for
  one (Q10), and good for two more where steps matter (Q12, Q13).
- **The tables and pictures that describe a screen (Q1–Q4) belong to the screen, not the
  journey.** Every journey that uses the form reads the same field table. Today the same buttons
  are listed again in each spec that opens the form.
- **Navigation (Q5) leaves the examples.** The flow says once how to reach the form. A test that
  needs the form gets there by the flow; the example starts at the form. That alone removes about
  a fifth of Bouw7's Gherkin (the 138 "getting there" lines).
- **Tables with ticks are the ones Bruinet can turn into tests itself** (Q2, Q3, Q4, Q7, Q8). A
  scenario needs step code; a tick needs one generic test per kind of column.

## Questions by who asks them

| Who | Asks mostly | So they read mostly |
| --- | --- | --- |
| Product owner | Q16, Q5, Q6, Q8, Q14 | Intent, flow, state diagram, rights matrix, screenshots |
| Tester | Q2, Q3, Q4, Q7, Q12, Q15 | Field table, list table, decision table, messages, checklist |
| Developer or building agent | Q1, Q2, Q9, Q10, Q13 | Screen card, field table, domain model, scenarios, sequence |
| Migration (Gaminet) | Q1–Q9: what the old system *is* | Everything drafted from the running app |

## The same question, three answers

"Is Name required on the department form, and what does it say?"

Gherkin, as in B7.MST.02 today (4 lines, plus a heading and a paragraph):

```gherkin
When I tap the Create button
Then the Department Create page is open
And Name is marked as required
And Name complains "Name is required"
```

One-line rule (1 line):

> If Create is tapped with no Name, then the web app shall keep the form open and show "Name is required".

Field table (1 row, and the answer is found by looking down one column):

| Field | Kind | Required | Message when empty |
| --- | --- | :---: | --- |
| Name | text | ☑ | Name is required |

The table also answers the next question ("and the other fields?") without another line: a field
with no tick is not required. The test is generated from the tick.
