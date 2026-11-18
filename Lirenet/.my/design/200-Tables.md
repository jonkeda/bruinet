# Table styles

Tables answer "what is there" and "what is the outcome" questions. They are the simplest exact style:
a person reads down a column, a tick can be wrong, and Bruinet can turn a column into tests. Each
style below is a proposal: what it answers, a few lines to show the idea, how it becomes tests, and a
score card (C1 read · C2 write · C3 exact · C4 check · C5 run · C6 diff · C7 draw; see the
[readme](000-Readme.md)). The exact columns are not designed yet.

## T1 Field table

**Answers:** Q2 which fields a form has; Q3 what may go in them.

| Field | Kind | Required | Default | Limits | Message |
| --- | --- | :---: | --- | --- | --- |
| Name | text | ☑ | empty | 1–100 characters | Name is required |
| Contact Type | select | ☑ | Client | Client, Supplier, Both | |
| Start date | date | ☐ | today | not before 2000 | |
| Reverse charge VAT | checkbox | ☐ | off | | |

- **One row per field, in the order on the screen.** Sections become a heading above their rows
  (`### Financial`), so "Sales price" in two sections is two rows under two headings.
- **Kinds are a short fixed list**: text, number, date, select, checkbox, radio, file, read-only.
  The kind tells Bruinet which control to use, so no step says `.Text` or `.Select` again.
- **Becomes tests:** per ticked row, "empty is refused with its message"; per row with limits, the
  edge values (100 accepted, 101 refused); per row with a default, "a new form shows it"; per
  select, "offers exactly these choices".
- **Drafted from the app:** the Scraper reads labels, kinds, required marks and choices. The person
  adds the limits and messages it cannot see.
- **Ids:** `B7.MST.02.F.Name`, or the table is one rule and each generated test names its row.

| C1 | C2 | C3 | C4 | C5 | C6 | C7 |
| --- | --- | --- | --- | --- | --- | --- |
| ●●● | ●●● | ●●● | ●●● | ●●● | ●●● | ●●● |

**Not for:** fields that depend on each other ("VAT number is required when Country is not NL").
That is a decision table (T4) or a one-line rule beside the table.

## T2 List table

**Answers:** Q4 what a list shows.

| Column | Shows | Sort | Filter |
| --- | --- | :---: | :---: |
| Name | the contact's name, as a link to its page | ☑ default, A→Z | ☑ |
| Type | Client, Supplier or Both | ☑ | ☑ |
| City | | ☑ | |

Below it, a few facts as a short list: empty list shows "No contacts found"; 25 rows per page;
actions per row: Open, Delete.

- **Becomes tests:** the header row is exactly these columns; sorting by each ticked column orders
  it; the empty text; the page size.
- **Drafted from the app:** the Scraper knows the columns and which headers sort.

| C1 | C2 | C3 | C4 | C5 | C6 | C7 |
| --- | --- | --- | --- | --- | --- | --- |
| ●●● | ●●● | ●●○ | ●●● | ●●● | ●●● | ●●● |

**Not for:** which rows appear for which data. That is an example table (T7).

## T3 Rights matrix

**Answers:** Q8 who may do what.

| Action | Admin | NormalUser | Reader |
| --- | :---: | :---: | :---: |
| See the Invoices list | ☑ | ☑ | ☑ |
| Make an invoice | ☑ | ☑ | ☐ |
| Delete an invoice | ☑ | ☐ | ☐ |
| See Settings | ☑ | ☐ | ☐ |

- **Columns are roles** from the product's role list, mapped to accounts in `TestAccounts.md` by
  the suite, never in the spec.
- **"May not" needs a meaning:** the button is not there, or it is there and refused. One column
  per meaning, or a legend: ☐ hidden, ⊘ shown but refused.
- **Becomes tests:** one per cell, signed in as that role. A grid of 4 × 3 is 12 tests nobody writes.

| C1 | C2 | C3 | C4 | C5 | C6 | C7 |
| --- | --- | --- | --- | --- | --- | --- |
| ●●● | ●●○ | ●●● | ●●● | ●●● | ●●● | ●●● |

## T4 Decision table

**Answers:** Q7 the outcome of a combination of conditions.

| Status | Due date | Shows Overdue |
| --- | --- | :---: |
| Open | before today | ☑ |
| Open | today | ☐ |
| Open | none | ☐ |
| In progress | before today | ☑ |
| Done | before today | ☐ |

- **Conditions left, outcomes right.** A dash means "any". Every combination that matters is a row;
  a missing combination is a question for the owner.
- **Check:** Lirenet can find overlapping rows and, for small fixed lists, combinations no row covers.
- **Becomes tests:** one per row, as a `[Theory]` row in a unit test or an example in a scenario.
  Todo's TOD.05.2 is tested this way already (a rule table: status × due × today).

| C1 | C2 | C3 | C4 | C5 | C6 | C7 |
| --- | --- | --- | --- | --- | --- | --- |
| ●●○ | ●●○ | ●●● | ●●● | ●●● | ●●● | ●●● |

**Not for:** more than about four conditions; then the rows explode. Split it.

## T5 Message list

**Answers:** part of Q3 and Q12: every text the app shows when something is refused or fails.

| When | Where | Message |
| --- | --- | --- |
| Name empty on Create | under Name | Name is required |
| Title over 100 characters | under Title | Title can be at most 100 characters |
| The server cannot be reached | a bar at the top | No connection. Your changes are kept on this device. |

- One place for the exact words, so a spec, a test and a translation agree.
- **Becomes tests:** only through the rules and fields that point at a row; the list itself is the
  vocabulary. Lirenet checks every quoted message in a rule exists here.

| C1 | C2 | C3 | C4 | C5 | C6 | C7 |
| --- | --- | --- | --- | --- | --- | --- |
| ●●● | ●●● | ●●● | ●●● | ●○○ | ●●● | ●●● |

## T6 State transition table

**Answers:** Q6, when a diagram would be crowded.

| From | Action | To | Who |
| --- | --- | --- | --- |
| Draft | Send | Sent | NormalUser, Admin |
| Sent | Mark paid | Paid | Admin |
| Sent | Credit | Credited | Admin |
| Draft | Delete | (gone) | NormalUser, Admin |

- The same content as a state diagram (P5 in [300-Pictures.md](300-Pictures.md)); one can be made
  from the other. The table carries more per arrow (who, conditions); the diagram reads faster.
- **Becomes tests:** per row, "from this state, this action leads to that state"; and the
  forbidden ones: an action not in a row for a state is not offered there.

| C1 | C2 | C3 | C4 | C5 | C6 | C7 |
| --- | --- | --- | --- | --- | --- | --- |
| ●●○ | ●●● | ●●● | ●●● | ●●○ | ●●● | ●●○ |

## T7 Example table

**Answers:** Q4 and Q7 by example: these inputs give these results. What Gherkin calls an outline.

| Typed as title | Saved as | Accepted |
| --- | --- | :---: |
| `Buy milk` | Buy milk | ☑ |
| `  Buy milk  ` | Buy milk | ☑ |
| `   ` | | ☐ |
| 100 × `x` | 100 × `x` | ☑ |
| 101 × `x` | | ☐ |

- Close to the decision table, but by concrete values, not by conditions. Good when the rule is
  easier shown than said.
- **Becomes tests:** one per row, with the columns bound to a step ("type … as the title").

| C1 | C2 | C3 | C4 | C5 | C6 | C7 |
| --- | --- | --- | --- | --- | --- | --- |
| ●●● | ●●○ | ●●● | ●●○ | ●●○ | ●●● | ●●● |

## Comparing the tables

| Style | Best for | Generates tests by itself | Drafted from the app |
| --- | --- | :---: | :---: |
| T1 Field table | Forms | ☑ | ☑ mostly |
| T2 List table | Lists | ☑ | ☑ mostly |
| T3 Rights matrix | Roles | ☑ | ☐ (needs every role signed in) |
| T4 Decision table | Combinations | ☑ | ☐ |
| T5 Message list | Exact words | ☐ (a vocabulary) | ☑ partly, as messages are seen |
| T6 State transitions | Life cycles | ☑ | ☐ |
| T7 Example table | Edge values | ☑ with a step | ☐ |

## Questions about tables

- Ticks as `☑`/`☐`, `x`/blank, or `yes`/`no`? Proposal: `☑` and `☐`: they read at a glance and
  cannot be confused with a value.
- Is a whole table one rule (one id) or is each row a rule? Proposal: the table is one rule, and
  its generated tests name the row (`B7.MST.02.F.Name.required`). The trace shows per row.
- Where a table and a one-line rule say the same, which wins? Proposal: never both; Lirenet warns
  when a rule repeats a cell.
