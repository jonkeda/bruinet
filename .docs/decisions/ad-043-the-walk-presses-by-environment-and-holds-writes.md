# AD-043: The Walk Presses By Environment And Holds Writes

**Decided:** `bruinet scrape places walk` (2026-11-18) finds an app's places by pressing what a
person can press. How much it presses is set by one field, `Environment` in `## App` of
`scraper.config.md`: `production` (the default) presses only what is known to show more;
`acceptance` and `local` press every button but what a danger word that is on forbids. On every
environment a write is held back at the request guard (`RequestGuard.HoldWrites`) unless the page
is a record the walk owns, named with `--own`: a record a test made, with the run marker in its
name. The walk reads a form and never saves it; a form is saved by a test, through its page
object, which writes down where the save led (`places add --from-run`).

**Why:** most of an app is behind its buttons, so a walk that only follows links lists the menu
again. On a shared test environment pressing is allowed, but the data there belongs to other
people's tests and demos: a button's effect is seen, and its call written down, without the call
landing. A form filled in by a crawler holds guesses; filled in through a page object it holds
what a test would type, and proves the page object on the way. Evidence: the
[design](https://github.com/jonkeda/BruinetB7/blob/bouw7-whole-app/.my/bouw7/bouw7-crawler-design.md);
`WalkTests` press Delete, Rename, Confirm and Import on the test site with no request reaching it.

**Ruled out:**
- pressing more on an environment because of a host's name: the config says what it is;
- a write on a record without the run marker, by the walk or by a test for it;
- the walk filling in or saving a form, also an empty one;
- pressing what sends, pays or signs out on any environment: a mail leaves it;
- a record's own text in the list of places: it holds labels, buttons, columns and calls.

**Broken when:** a walk on an environment that is not `acceptance` or `local` presses a button
that `ClickJudge` does not judge safe; a counter of the test site's dangerous page moves in
`WalkTests` without `--own`; `175-Places.json` holds a row's text or a field's value.
