# API testing: what was built

Written 2026-11-21, at the end of the work the owner asked for: "implement the full tasklist. make your own decisions.
do not ask questions. commit and push in between. create a final report when you are done". The task list is
[plan.md](plan.md), from [proposal.md](proposal.md). This is the account: what exists now, what was run, what was
decided without the owner, and what is still open.

## In short

- Six of the eight planned phases are done and shown on Bouw7's acceptance environment. The typed client from a
  real description (A5) is built up to the point where a description is needed, and Heimdall's is not here. SOAP
  (A7) was never planned to start.
- Bouw7's 350 lines of its own HTTP code are now API objects over Bruinet's API session. Its tests pass on acc as
  before, and the first form checked against what was stored turned up a finding.
- Bruinet's API packages are one family (`Bruinet.Api.*`), version 0.2.0, with a command that moves a consumer to
  the new names.
- Six tasks are open; five of them wait for something that is not in this repository.

## What exists now

| Piece | Where | What it is |
| --- | --- | --- |
| The API session | `src/Bruinet.Core/Api` | A service called as an account: the hosts a suite may call, the sign-in with renewal, no write as an account that never writes, every call in the step log and kept as a masked exchange, delete only by marker, undo, seed. `ApiSession.Client` is the session as an `HttpClient`, for a generated client |
| A browser's saved sign-in as the API's | `Bruinet.Html.Playwright.SavedSignIn` | The token the web app itself gets, from the storage-state file |
| A page's calls | `PlaywrightTestContext.Calls` | The page's fetch and XHR calls as exchanges; a save gives the write it caused |
| A form checked against what was stored | `Bruinet.Api.Contracts.FormCheck` | Each typed value found in the sent body; typed, sent and stored compared per field |
| `bruinet upgrade` | `tools/Bruinet.Cli/Upgrade`, MCP tool `upgrade` | Applies the rename rows of the changes list to a consumer, then builds |
| The `Api` family | `src/Bruinet.Api.Contracts`, `.Replay`, `.Mocking`, `.Mocking.Analyzers`; `src/Bruinet.Mocking.Sensors` | Renamed from `Bruinet.Contracts`, `Bruinet.Mocking.Http`, `Bruinet.Mocking`. The sensors left for a package of their own |
| An API's description | `src/Bruinet.Api.OpenApi`; `bruinet api describe`, `diff`, `uncalled`, `check` | Read from OpenAPI 3 JSON, or merged from the exchanges a run kept (shapes only). Two compared; a run held against one |
| A fake service from a description | `MockApiServer.StubDescribed` | A stub per operation, answering with an example of its schema |
| The `api` head | `Bruinet.Bdd` | A scenario tagged `@api` runs through API objects, without a screen |
| For people and agents | `docs/guides/api-testing.md`, AD-046, the `api-object` skill | The guide, the decision, the workflow |

In Bouw7 (the container repository):

| Piece | Where |
| --- | --- |
| The API objects: `Contacts`, `Projects`, five kinds of marked record, the seed | `Bouw7.Web.UITests/Support/Api` |
| A guard: no file outside that folder names the session or an HTTP client | `ApiObjectsOnlyTests` |
| A saved form checked against what Bouw7 stored, one call per form | `StoredForm`, used by `PlacesSaveTests` |
| A contact made through the API, shown on its page | `ContactMadeThroughApiTests` |
| Bouw7's back end as it was seen: 68 operations | `Support/Api/heimdall.observed.json` |
| The `api` head, with two scenarios of the spec B7.CON.02 | `Bouw7.Api.Tests`, `Modules/Contacts/Features/B7.CON.02-stored.feature.md` |

## What was run

| Check | Result |
| --- | --- |
| Bruinet's tests before any change | Core 145 of 149, Contracts 133, Mocking 60, Mocking.Http 31, Cli 478 |
| The same after the work | Core 158 of 162, Api.Contracts 138, Api.Mocking 61, Api.Replay 31, Api.OpenApi 8, Bdd 310 (2 skipped), Cli 485 |
| Bruinet's whole solution, after the rename | Builds, 0 errors |
| Bruinet's whole solution, at the end | Builds, 0 errors, 0 warnings |
| Bouw7 on acc, the API's data tests and the seed, after A1 | 5 of 5 |
| Bouw7 on acc, the Contacts module | 12 of 12 |
| Bouw7 on acc, the Contacts and Projects scenarios | 56 of 56 |
| Bouw7 on acc, the same with the data and form tests, exchanges kept | 63 of 63 |
| Bouw7 on acc, the `api` head | 3 of 3: the two scenarios in 0.6 s and 2 s |
| Bouw7 on acc, B7.CON.02 through the browser, with the bind check | 9 of 9 in 48 s |
| `bruinet spec trace` on B7.CON.02 with the `api` head's results | R5 and R7 passing |
| `bruinet upgrade --check` on Bouw7, `samples/` and `tests/` | Nothing left to rename but the command's own test data |
| `bruinet upgrade` on Bouw7, with its build | Renamed nothing (Bouw7 used none of the renamed packages), built Bouw7, no errors |
| `bruinet api diff` on an answer changed on purpose | Named the field that changed, exit 1 |

The four tests of `Bruinet.Core.Tests` that fail are repository checks that failed before this work and are not
about it: a broad catch in `ScraperService.Places.cs`, the iOS test projects not named in a script, an iOS sample not
in the solution, and a platform name in `MediaElement.tpl.cs`.

Not run: Bouw7's whole suite (16 to 43 minutes; the owner's rule), and Bruinet's UI suites (MAUI, WPF, the Html and
Blazor browser suites). The rename touched the Todo sample's fixtures, which build; they were not run.

## The finding on Bouw7

The first form checked typed, sent and stored was the project category. Its field labelled **Description** is sent
and stored as `name`. Nothing is wrong with what is stored, but a test or a spec that calls the field Description and
an API object that calls it `name` are speaking of the same thing: worth one line in the spec of that screen.
`PlacesSaveTests` now writes such a line for each form it saves, as a fact of the form.

## Decided without the owner

| Question | Decision | Why |
| --- | --- | --- |
| Where the neutral part lives | `Bruinet.Core`, namespace `Bruinet.Core.Api` | It needs only `HttpClient` and `System.Text.Json` |
| When the rename is made | After the session, with `bruinet upgrade` first | The proposal's own order |
| The mock sensors | A package of their own, `Bruinet.Mocking.Sensors`, namespace unchanged | Whether another project uses them could not be asked; this breaks a user least |
| The version | 0.2.0, not a major | Before 1.0 a minor version may break the API (`VERSIONING.md`) |
| The library for descriptions | None: OpenAPI 3 in JSON is read with `System.Text.Json` | A few hundred lines against a dependency whose API could not be checked here. YAML and OpenAPI 2 are not read. This departs from the proposal, which named `Microsoft.OpenApi` and `WireMock.Net.OpenApiParser` |
| Schemathesis | Words in the guide only; nothing starts it | Generated calls on a shared environment are a person's to start |
| Heimdall's description | Not fetched | It lives in another repository |
| A create registering its delete | Offered (`ApiSession.Made`), not used by Bouw7 | A Bouw7 session lives for one `using`; the record would go before the test looked |
| Where the branch is pushed | `api` to the `jonkeda` remote of `test-tooling` only | The saved rule: `origin` is the work repository and stays on the work account |

## Open

| Task | What is open | It waits for |
| --- | --- | --- |
| A0.8 | The libraries of the proposal's section 2 were not checked online | The first of them being taken |
| A3.8 | The Scraper's factory does not draft the stored-check for a form | Nothing: it is work not done |
| A4.7 | The observed description held against Heimdall's own | Heimdall's description in the Bouw7 suite |
| A5.2 | No client was generated; the seam it is built on exists and is tested | A description |
| A5.5 | Bouw7's API objects on a generated client | The same |
| A8.4 | `.my/api/` is not deleted | The tasks above done or dropped |

## To know before relying on it

- **The container points at a commit of the branch `api`.** `.gitmodules` still names the branch `move` for
  `test-tooling`, so `git submodule update --remote` would take the submodule back to `move`, and Bouw7 would no
  longer build. Merging `api` into `move`, or changing that line, is the owner's call.
- **The rename breaks every other project that uses Bruinet** until it runs `bruinet upgrade`. A project that used
  the mock sensors also needs a reference to `Bruinet.Mocking.Sensors`. Bouw7 used none of the renamed packages.
- **A name written relative to `Bruinet` is not renamed** (`Mocking.Http.HttpCall` inside a `Bruinet.*` namespace):
  the build after the renames names it. Four such places were in the Todo sample.
- **One commit in the middle does not build for .NET 8 and 9** (`1c4cbba9`: a method that exists from .NET 9 in
  `ApiSession`). The next commit (`4f20dc13`) repairs it. Do not bisect onto the first.
- **Tested only on acc, not by a Bruinet test:** `PageCalls` (the page's calls) and the walk's feed into the exchange
  file. **Not exercised at all:** `ApiSignIns.ClientCredentials`, `ApiKeySignIn`, and the MCP tool `upgrade` beyond
  being listed.
- **The observed description is from a part of Bouw7**: a read-only walk of the contact pages and the Contacts and
  Projects scenarios. It has 68 operations; the back end has more.
- **A scenario's name over 50 characters does not match its result** in `bruinet spec trace --results` (the test
  runner cuts the name). It was there before; the `api` scenario was shortened for it.
- On acc the tests made and deleted contacts, projects, a department and a category, all with the run's marker. The
  walk's changes to Bouw7's list of places were put back.

## Commits

`test-tooling`, branch `api` (pushed to `jonkeda`):

| Commit | What |
| --- | --- |
| `aec1681d` | The API session in Core; the plan |
| `8f11cb50` | `bruinet upgrade`; a page's calls |
| `1c4cbba9` | The `Api` family, 0.2.0; the form check; the description package |
| `4f20dc13` | `bruinet api`; the fake service; the `api` head; the guide, AD-046, the skill |
| the one after | The plan's state and this report |

The container, branch `main` (pushed to `origin`): `1d9331a` (the API objects), `3921b1d` (the form check, the
observed description, the `api` head), and one more for the submodule's last commit.
