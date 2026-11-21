---
name: site-setup
description: Set up a UI test suite for a new web site as a child folder of a container repository - solution, Bruinet references, scraper.config.md with careful guards, TestAccounts.md, a sign-in fixture, AGENTS.md and the .testing documents - so the site loop can run on it. Use when asked to start testing a new site, add a site or product to a container repository, or bootstrap a Bruinet suite for an existing web app.
---

# A new site

The shape is Bouw7's (`BruinetB7/Bouw7/`); copy its structure, not its content.

## Steps

1. **Ask** the URL of the test environment (never production), the platform (`html`), the accounts
   (names, roles, which may write) and the organization each signs in to. Never ask for passwords in
   chat: the person writes them into `TestAccounts.local.md`, which is git-ignored.
2. **Folder** `<Site>/`: `<Site>.slnx`, `Directory.Build.props`, `Directory.Packages.props`,
   `global.json`, `nuget.config` (Bouw7's), a `<Site>.Web.UITests` project referencing Bruinet from
   the submodule.
3. **The Scraper's own project** `<Site>.Scraper`, beside the suite and in the solution: an `Exe` that references
   `Bruinet.Scraper.Host`, whose `Program.cs` is `return await ScraperApp.RunAsync(args);`, with the
   `scraper.config.md` (its `## App` names the suite: `| Suite | ../<Site>.Web.UITests |`), the draft templates,
   the recordings and the list of places. The suite does not reference it (Bouw7's `Bouw7.Scraper`, the Scraper's
   guide).
   **Guards** in that `scraper.config.md` before anything runs: `## App` with the start URL and
   `AllowedHosts` (test hosts only), Never visit, danger words, `ClickPolicy safe`, masks;
   `Privacy` stays off on a test environment.
4. **Accounts**: `TestAccounts.md` (name, role, organization, `Writes`, environment, purpose) and
   the local file. A session helper that signs in once per account and saves its state.
   When tests will make or remove data beside the screen, the suite gets a `Support/Api` folder from the start: a
   class that opens Bruinet's API session as an account, with the hosts it may call in the suite's settings, and
   API objects over it (skill `api-object`). No suite writes its own HTTP client, host check or delete-by-marker.
5. **Docs**: `AGENTS.md` (Never, Where code goes, Running) and `.testing/` with `000-Readme.md`,
   `100-Loops.md` (one row per module), `150-Structure.md`, `200-Tasklist.md`, `300-Environment.md`,
   `400-FrictionLog.md`, `600-Improvements.md` (its `Every` row and an empty Reviews table: the
   `improvement-review` skill), `decisions/`. Once the suite has its first pages: `180-Pages.md`, the page checklist,
   written by `bruinet suite pages --write` and written again after a page is added or changed; and
   `<Site>.Scraper/175-Places.md`, the app's places (`bruinet scrape places seed`, `routes`, `walk`), which the checklist is held against. The walk
   presses by `Environment` in `## App`: leave it `production` unless the owner says the site is an acceptance
   environment (AD-043).
6. **Gate**: a test opens the start page signed in as the reading account, locally. Then the
   site loop (`tools/Scripts/site-loop.ps1`) takes over per module.
