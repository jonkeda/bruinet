---
name: scrape-pages
description: Make Bruinet Html or Blazor page objects and controls from an app's recorded pages - record the pages with the Scraper into a UI test project, find the repeated parts, write controls first and pages second, and build until it passes. Use when asked to scrape, record or crawl an app or site for page objects, to generate page objects or controls from a web app, or to update them after the app changed.
---

# Page objects from recorded pages

The Scraper records an app's pages into a UI test project (`Recordings/`, beside the code) and
answers questions about them through the Bruinet MCP server. It calls no model: you do the
writing, it holds the pages, finds patterns and runs the gate. The conventions are the
[html-control](../html-control/SKILL.md) and [html-ui-test](../html-ui-test/SKILL.md) skills:
read them first.

## Workflow

1. **The project.** `scrape_list_projects`. A project is scrapeable when it has a
   `scraper.config.md` (`## App`: `Platform` html or blazor, `StartUrl`, `Namespace`, `Pages`,
   `Controls`, `Screenshots`; `## Aliases`, `## Test ids`, `## Masks`). None: ask which UI test
   project, and propose the file.
2. **Record.** `scrape_start` with a session name opens a browser window the *user* works in.
   Each page the app moves to is captured once it settles; `scrape_capture_page` with a name captures
   one more state (a dialog open, a tab chosen). `scrape_stop` when they are done. Never sign
   in or type credentials for the user: the browser keeps the project's own login.
3. **Look before writing.** `scrape_list_page_objects` (reuse what exists, in this project and the scraped
   projects it references), `scrape_list_pages`, `scrape_detect_patterns`. A `Repeated` pattern on
   several pages, or several times on one, is a control to make; `Form`, `Table`, `List`,
   `Navigation` and `Dialog` are what a page object names. `scrape_get_page` gives a page as an
   outline with node numbers; ask for a `node`'s subtree when a page is large.
   `scrape_find_similar` shows where else a part occurs.
4. **Controls first.** For each pattern the user agrees to, `scrape_get_control_brief` gives the file to
   write, the files to read (and nothing else), the facts of its occurrences and hints. Decide from
   the facts; the hints are a reading, not an order. A simple control (a family base such as
   `SelectorControlBase`), a container (`ContainerBase<TParent, TSelf>`), or a collection and its
   row (`CollectionObjectBase` / `ItemObjectBase`), in the config's `Controls` folder, as a
   `<Name>.tpl.cs` template: constructors and a `*Core` method per thing a test reads or does;
   `scrape_build` generates the public members. Its first line is the brief's. Say what it will
   offer, show the code, then `scrape_write_file`.
5. **Pages second.** One page object per recorded page the user wants, in the `Pages` folder:
   `PageObjectBase<TSelf>`, or `BlazorPageObjectBase<TSelf>` for a Blazor app that sets
   Bruinet's busy signal (`data-busy` on `<body>`, as the Blazor sample does): it waits until the
   app is interactive. A Blazor app without it (a third party's) takes `PageObjectBase`. It uses the controls (`scrape_match_controls` says which occur on the
   page). Its first line is `scrape_get_header` for the page. `scrape_suggest_controls` gives a member per
   thing a test acts on or reads, with the control that fits (the `Bruinet.Blazor.Controls` one in
   a Blazor project) and a locator: start from it, rename for what the user sees, drop what the
   page object does not need. A member with a `scope` is in a frame: declare it on that frame's
   member, `TextInputControl<FrameScope<CatalogPage>> CustomerId => new(ProfileFrame, …)`.
6. **Locators.** `scrape_suggest_controls` already follows this order, and says when it could not:
   `data-testid` (or the config's test-id attributes), the accessible name or label text, a
   stable `id`, a stable class. Never a generated class
   (`css-1x2y3z`, `jss12`, `sc-…`), a number in an id, or a position (`:nth-child`) unless
   nothing else is there, and then say so in the summary. Inside a frame, the frame is a
   `FrameScope`.
7. **The gate.** `scrape_build` builds the project with its analyzers. Fix every error it reports
   and call it again until it passes. The work is not done before that.

## Tests from a recording

A test recording (`scrape_start` with `actions`, or the view's **Record a test**) keeps what the
user did and the checks they picked. `scrape_resolve_events` maps each action to a page object's member,
its control's verb and the catalog phrase; what does not resolve is a gap with why. `scrape_gap_brief`
gives a gap as a brief. Close it in the page objects or controls (a member, a verb), never in the
drafted test, and never invent a phrase. Then `scrape_build`, `scrape_resolve_events`, `scrape_draft_test` (an
xUnit test, a scenario or both, as `## Tests` says) and `scrape_run_drafts` once.

## Refreshing

`scrape_match_controls` says whether a page changed since its page object was made (the header's
hash). Record the page again, compare with `scrape_get_page` of the old and new capture
(`session`, `number`), and change only what changed.

## Never

- Invent a page, control or element that is not in a recording.
- Write outside the project, or into `Recordings/`, `bin/` or `obj/` (`scrape_write_file` refuses).
- Put a customer's data in code: recordings are masked by the config's `## Masks`; generated
  names describe the element, not the record shown.
