# P1 Screen card: automation

The style: [../300-Pictures.md](../300-Pictures.md#p1-screen-card). Entry point **A, table checks**. The
card is also what other checks lean on: it says how to reach a screen and which button submits it. Back to
the [readme](000-Readme.md).

## What the spec says

```markdown
## Screen: Department form

| Department form | |
| --- | --- |
| Reached by | "+ Department" on Departments |
| Title | Create department |
| Top buttons | Back |
| Bottom buttons | Cancel · **Create** |
| Fields | see the field table |
```

## What proves it, by layer

Only the screen can: **UI fast** for every line, the **judge head** once per screen at the gate (the
smoke run, Q15 of the question map).

## The simplest automation

**One generated test per line, one smoke test per card.**

| Line | Generated check | Uses |
| --- | --- | --- |
| Reached by | From the named page, pressing the named control opens this screen | `the {page} page is open`, `I tap {control}`, the page's `[BddPage]` name |
| Title | The screen's title says it | The page header's title (`[BddControl("the title")]` in Bouw7) |
| Buttons | Exactly these buttons are shown in that place, no others | The page object's button members; `AssertExactly` over the place's buttons |
| The bold button | It is the form's submit: the field table's checks press it | Marked for T1, not a test of its own |
| Fields | Hands over to the field table (T1) | — |

The whole card, run on the judge head, **is the page's smoke test**: open it the way it is reached, see the
title and the buttons. Today Bouw7 writes that as an eight-line example per page (B7.MST.02 R1); the card
replaces it with a generated test, and the trace shows one state per line.

**Binding**: the card's screen name is the page object's `[BddPage]` name ("Department Create"), or the
card says which (`| Page | Department Create |`). The buttons are found by their `[BddControl]` names, as
steps find them. A button on the card with no member is a binding error before the run, naming the line.

## What it needs

| Need | Exists? |
| --- | --- |
| Pages and controls found by name | ✓ (phrases do it) |
| Opening a page by its address, or by a control on another page | ✓ (`IOpenablePage`, page flow) |
| "These buttons and no others" | **New**: a page or region's buttons as a set; Bouw7's page checklist (`bruinet suite pages`) already lists a page's named members |
| Reading the card, running the checks | **New**: the screen card check |

## Drafted from the app

The card is the cheapest style to draft: the Scraper and Bouw7's page checklist know a page's title,
buttons and how it was reached. A draft card per page, accepted by the owner, gives every page a smoke
test with no Gherkin written.

## Questions

- "Exactly these buttons" or "at least these"? Proposal: exactly, per place (top, bottom); a new button is a
  change the owner should see.
- Do Wireframe screens (P2) produce cards? Proposal: yes; see the next document.
