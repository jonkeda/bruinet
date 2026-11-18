# P3 Annotated screenshot: automation

The style: [../300-Pictures.md](../300-Pictures.md#p3-annotated-screenshot). **Not a test**: a drift report.
The screenshot answers "does it look right", which only a person can judge; automation keeps it current
and says when to look again. Back to the [readme](000-Readme.md).

## Why not a test

A pixel comparison fails on a font update, a date on the screen, an animation, a different machine. It
tells nobody what broke, and teams learn to re-approve it without looking. The facts a screenshot shows are
already checked by the screen card (P1) and the field table (T1). What is left, the look, is a person's call.

## What is automated

| Step | How | Exists? |
| --- | --- | --- |
| **Take** the screenshot | Bruinet takes one per step already (artifacts), and the Scraper per captured page; a run on the judge head stores the screen's | ✓ |
| **Number** the controls | Draw a mark per control of the page object, numbered, with a table of number → member and `[BddControl]` name; the narrated recordings already draw touch marks | **New**, small, reusing the touch marks |
| **Compare** with the stored one | A structural comparison, not pixels: the same controls, in roughly the same places, with the same texts. Changed → the report lists what moved, appeared or vanished | **New**, medium |
| **Ask** a person | A changed screenshot goes into the review page (`write_review`, narrated-review skill) with the old and new side by side; the person accepts, and the new one is committed | ✓ the review page; **new**: the screenshot pair in it |

The comparison uses what Bruinet knows, not what the image shows: the control's bounds and text from the
page object at the moment of the shot. So a date in a label or a different font does not count; a button
that moved from bottom to top does.

## Layer

The judge head only (browser or device): a headless or bUnit head has no pixels.

## In the trace

Lirenet shows a screenshot as **current** (taken by the last gate run, unchanged) or **changed, waiting for a
person**. It never shows it as passed or failed.

## Questions

- Stored where? Proposal: beside the spec (`screens/department-form.png`), small PNGs in git, replaced only
  when a person accepts the change.
- How often? Proposal: at the gate only; a fast run never takes them.
