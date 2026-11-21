# T3 Rights matrix: automation

The style: [../200-Tables.md](../200-Tables.md#t3-rights-matrix). Entry point **A, table checks**, once at
the API and once on the screen. Back to the [readme](000-Readme.md).

## What the spec says

```markdown
## Rights: Invoices

| Action | Admin | NormalUser | Reader |
| --- | :---: | :---: | :---: |
| See the Invoices list | ☑ | ☑ | ☑ |
| Make an invoice | ☑ | ☑ | ☐ |
| Delete an invoice | ☑ | ☐ | ⊘ |

☐ not offered · ⊘ offered, refused
```

## What proves it, by layer

| | Unit | Integration | UI fast | UI judge |
| --- | --- | --- | --- | --- |
| The server refuses an action for a role | ✓ a policy function, if the app has one | **✓** the API answers 403 for that role's token | | |
| The screen does not offer it (☐) | | | ✓ if roles can be faked on the head | **✓** signed in as the role |
| The screen offers it and the server refuses (⊘) | | ✓ | | **✓** the message shown |
| The screen offers it and it works (☑) | | ✓ | | ✓ for actions that do not write; writes only on records the run owns |

The **integration** cells matter most: a hidden button is not security. The UI cells check that the screen
agrees with the server.

## The simplest automation

**One test per cell, written by nobody.** The rights check reads the matrix and, per cell:

1. Signs in as the column's role: the role is an account name in `TestAccounts.md` (Bruinet's account
   model; secrets in the git-ignored `TestAccounts.local.md`), so the spec never holds a secret and the suite
   maps roles to accounts once.
2. Finds the action: a row is a **callable phrase** or a page member. "See the Invoices list" → `the Invoices
   page is open`; "Make an invoice" → the `+ Invoice` button on the Invoices page; "Delete an invoice" → the
   Delete row action. The suite says it once, in a small table beside the matrix or by `[BddControl]` names
   that match the action's words.
3. Checks the cell: ☑ the control is shown (and for read-only actions, it works); ☐ it is not shown; ⊘ it is
   shown and pressing it shows the refusal, with nothing changed.

**At the API**, the same matrix drives a test per cell on the `service` head or against the real API with
each role's token: the action's request (from the contract the app reads, `Bruinet.Api.Contracts`) is sent and
the status compared: ☑ success, ☐ and ⊘ refused. Only actions with a request are checked there; "See the
list" is a GET.

**Writes**: ☑ on an action that creates or deletes runs only on a record the run made (Bouw7's run marker);
otherwise the cell is checked as "offered" and the write is left to the scenario that owns it.

## What Bruinet needs

| Need | Exists? |
| --- | --- |
| Accounts by name, signed in per test | ✓ `TestAccounts.md`, the sign-in cache per account |
| Several users in one scenario | ✓ `{user}` phrases (not needed here: one role per test) |
| Reading the matrix, mapping actions to controls | **New**: the rights check, with a small action map |
| The API side: a request per action, per role | Partly: contracts know the requests; **new**: sending them with another role's token |

## Limits

- Rights that depend on the record ("only the invoice's author may delete it") are not a matrix: a
  one-line rule and a scenario with two users.
- On a shared environment, roles are real accounts; the matrix cannot be checked for a role the suite has
  no account for. Lirenet reports such a column as unproven.

## Questions

- Do ☐ and ⊘ need to be told apart, or is "not allowed" enough? Proposal: tell them apart; the user's
  experience differs and so does the test.
- Where does the action map live? Proposal: in the suite (`.testing/` or beside the page objects), never in
  the spec: the spec says what a person does, the suite says how a test does it.
