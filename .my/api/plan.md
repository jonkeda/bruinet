# Development plan: the task list for API testing

Status: A0 to A4 and A6 done and shown on Bouw7's acc; A5 built but for a client generated from a real description (A5.2, A5.5: Heimdall's description is not here); A7 waits for a site with SOAP; A8 done but for deleting this folder, which waits for the open tasks (2026-11-21). Open: A0.8, A3.8, A4.7, A5.2, A5.5, A8.4. The account: [report.md](report.md). Written 2026-11-21 on branch `api`. The owner then asked: "implement the full tasklist. make your own decisions. do not ask questions", so every 👤 below was decided by the agent and says so.
developmentplan for the api design and proposal". It turns [proposal.md](proposal.md) (2026-11-20) and the older
service-clients design [design.md](design.md) (B18) into tasks. Where the two differ the proposal wins: it is the
later one and was answered by the owner.

How to read it:

- A phase is one step of the proposal's "Order of work", with the rename placed where the proposal puts it. A phase
  is done when its **Gate** box is ticked, not when its tasks are.
- A task has an id (`A1.4`), so a commit, a changes row or a question can name it.
- **B** is Bruinet (`test-tooling`, branch `api`), **7** is Bouw7 (the container repository).
- 👤 is a person's: a decision or an answer. Everything else an agent can do.
- Sizes: **S** hours, **M** a day or two, **L** more.
- A section number (`§4`) is the proposal's.

## The phases in one table

| # | Phase | Size | Needs | Its gate is read on |
| --- | --- | --- | --- | --- |
| A0 | Decisions and a baseline | S | - | Both repositories, the tests named below |
| A1 | The API session; Bouw7's API objects on top | M to L | A0 | Bouw7's Contacts and Projects modules on acc |
| A2 | `bruinet upgrade` and the rename to the `Api` family | M | A1 | Bruinet's `samples/` and `tests/`, then Bouw7 |
| A3 | A form checked through the API | M | A1 | `PlacesSaveTests` on acc |
| A4 | The observed description: `bruinet api describe`, `diff` | M to L | A1 | One walk and one suite run of Bouw7 |
| A5 | `Bruinet.Api.OpenApi`: typed client, schema check, fake service | L | A2, a description | One suite on a real description |
| A6 | The `api` head | M | A1 | One Bouw7 spec's data examples |
| A7 | `Bruinet.Api.Soap` | - | A site with SOAP | Not planned until a site asks |
| A8 | Extract and close | S | What was built | `.docs/`, `docs/`, the skills |

A3, A4 and A6 need only A1 and may run beside each other and beside A2. A5 is the only phase that waits for
something from outside (a description).

## Found while writing this plan

- `CleanUpRegistry` exists already, in `src/Bruinet.Core/Testing/RunMarker.cs`, with tests. The proposal's table says
  it "was never built". A1 extends it, it does not write it.
- The proposal links B18 as `../service-clients/design.md`. That folder does not exist; the design is `design.md`
  beside it (A0.6).
- `Bouw7Api.cs` is 299 lines and `Bouw7Seed.cs` 55 (the proposal's "350 lines").
- `Bouw7Api` deletes six marked resources (contacts, projects, days off, project categories, quotations; departments
  and absences are only listed), each written out by hand.

## Decided by the agent (2026-11-21)

| Task | Decision | Why |
| --- | --- | --- |
| A0.1 | The neutral part is in `Bruinet.Core`, namespace `Bruinet.Core.Api` | It needs `HttpClient` and `System.Text.Json` only, both in .NET itself |
| A0.2 | The rename after A1, with `bruinet upgrade` first | The proposal's own order |
| A0.3 | The mock sensors stay a package, `Bruinet.Mocking.Sensors`: not moved to a platform and not dropped | Whether another project uses them cannot be asked from here; a package of their own breaks a user least (one rename row) |
| A0.4 | Schemathesis is not run on acc by anything in this plan; `docs` say how a person runs it, reads only | Generated calls on a shared environment are a person's to start |
| A0.5 | Heimdall's description is not in the suite and was not fetched; A5 is built and proven on Bruinet's own sample service and on Bouw7's observed description | Copying it is a person's act in another repository |
| A0.7 | `Bruinet.Mocking.Analyzers` follows the WireMock server: `Bruinet.Api.Mocking.Analyzers` | Its one rule (BRUINET001) is about a mock answer's body |
| A0.8 | Not done, and left open: no library of §2 was taken (`Bruinet.Api.OpenApi` depends on nothing but Bruinet), so no version or licence was checked. It is done when the first of them goes into `Directory.Packages.props` | Checking names nobody uses yet proves nothing |
| A1.1 | No separate design note: `src/Bruinet.Core/Api` and `ApiSessionTests` are it | A note written after the code would only repeat it |
| A1.7 | A create registers its delete only where the API object says so (`ApiSession.Made`); Bouw7's API objects do not, because a Bouw7 session lives for one `using` | A record deleted when the session closes would be gone before the test looked at it |

Baselines (A0.9, A0.10), 2026-11-21: `Bruinet.Core.Tests` 145 of 149 (the four that fail are repository checks:
`BroadCatchTests.NoFile_HoldsMoreBroadCatches_ThanItsAllowance` and three of `SolutionTests`; they failed before any
change of this plan), `Bruinet.Contracts.Tests` 133, `Bruinet.Mocking.Tests` 60, `Bruinet.Mocking.Http.Tests` 31,
`Bruinet.Cli.Tests` 478. Bouw7: the API's data tests and the seed run, 5 of 5 on acc.

## How each phase ended (2026-11-21)

| Task | How it ended |
| --- | --- |
| A2.4 | No throw-away rename: the real one was the proof. `bruinet upgrade` ran over Bruinet itself (267 names in 112 files), and `--check` on `samples/`, `tests/` and Bouw7 then reported nothing left but the command's own test data |
| A2.6 | The sensors are the package `Bruinet.Mocking.Sensors`, namespace unchanged; the analyzer is `Bruinet.Api.Mocking.Analyzers` |
| A2.7 | Nothing to rename: the session stays in `Bruinet.Core.Api` (A0.1) |
| A2.8 | 0.2.0, not a major: before 1.0 a minor version may break the API (`VERSIONING.md`) |
| A2 | What the rename could not see: a name written relative to `Bruinet` (`Mocking.Http.HttpCall` inside a `Bruinet.*` namespace, four places in the Todo sample). The build named them, as the command's last step is meant to |
| A3.2 | The map is not a file beside the page object: Bouw7 writes it as facts of the form in `saved-forms.jsonl` ("Name is sent as `name`"), which the list of places already reads. `FormCheck.ToJson` and `FromJson` keep one as a file where a suite wants that |
| A3.5 | `PlacesSaveTests` saves two forms (department, project category); both are checked typed, sent and stored with one call each (`StoredForm.SaveAndCheck`). Finding: the category's field Description is stored as `name` |
| A3.8 | Open. The factory does not draft the pair yet |
| A4.1 | The store is the file `BRUINET_API_EXCHANGES` names: every session's calls, every page's (fetch and XHR) and the walk's. The recordings of `Bruinet.Api.Replay` are not read into it |
| A4.2 | Our own merge was enough; Dev Proxy was not tried |
| A4.6 | Bouw7's draft: 68 operations from 1223 exchanges of a read-only walk of the contact pages and of the Contacts and Projects scenarios with the data and form tests (63 of 63 passed). Not a whole-suite run and not a whole walk |
| A4.7 | Open: needs Heimdall's description (A0.5) |
| A5.1 | The package reads OpenAPI 3 in JSON with `System.Text.Json`, without `Microsoft.OpenApi` (AD-046, "Ruled out"): YAML and OpenAPI 2 are not read |
| A5.2 | Half. `ApiSession.Client` is the seam a generated client is built on, tested, and the guide has the Kiota lines. No client was generated: there is no description to generate from |
| A5.4 | Our own stubs over `MockApiServer` (`StubDescribed`), not `WireMock.Net.OpenApiParser` |
| A5.5 | Open: needs Heimdall's description |
| A5.6 | Words only, in the guide: a person runs it, reads only. Nothing in Bruinet starts it |
| A5 gate | Not met: no suite has a real description |
| A6.3 | The format and `spec-from-app` say it; no such spec exists yet |
| A6 gate | Two scenarios of B7.CON.02 (R5, R7) pass on acc on the `api` head in 0.6 s and 2 s, and `bruinet spec trace --results` counts both as passing. The same spec's nine examples through the browser took 48 s |
| A8.1 | One record, AD-046, for all of it; AD-020 names the `api` head |
| A8.4 | Open: this folder stays until A3.8, A4.7, A5.2 and A5.5 are done or dropped |

## A0 Decisions and a baseline

- [x] A0.1 👤 Where the neutral part lives: `Bruinet.Core` (the proposal and B18) or a package `Bruinet.Api`.
  Proposed: Core, `Bruinet.Core.Api`, unless A1 needs more than `HttpClient`
- [x] A0.2 👤 When the rename is made. Proposed: after A1, with `bruinet upgrade` built first (this plan's A2)
- [x] A0.3 👤 The mock sensors of `Bruinet.Mocking` (`Sensors/`): ask the projects that use Bruinet whether one uses
  them; then move them to the platform they fake, or drop them. It blocks only A2.6
- [x] A0.4 👤 Schemathesis on acc: reads only, as Reader (proposed), or not at all. Needed by A5.6 only
- [x] A0.5 👤 Heimdall's description: who copies it into the Bouw7 suite, and which format it is (OpenAPI 2 or 3).
  Needed by A5; A4's observed description stands in until then
- [x] A0.6 **B** Repair the proposal's link to B18; correct its row on the clean-up registry
- [x] A0.7 **B** Read `Bruinet.Mocking.Analyzers` (`AnonymousMockBodyAnalyzer`): what it checks decides which package
  it follows in the rename. One line in the proposal's Names table
- [ ] A0.8 **B** Check each library of §2 online: current version, licence, target framework (the proposal's names
  are from memory). Choose `JsonSchema.Net` or `NJsonSchema`. A table in the proposal; nothing in
  `Directory.Packages.props` yet
- [x] A0.9 **B** Baseline: `Bruinet.Core.Tests`, `Bruinet.Contracts.Tests`, `Bruinet.Mocking.Tests`,
  `Bruinet.Mocking.Http.Tests`, `Bruinet.Cli.Tests`, counted
- [x] A0.10 **7** Baseline: the Contacts and Projects modules and the seed run on acc, counted (not the whole suite)
- [x] **Gate:** A0.1 and A0.2 answered, a clean tree in both repositories, two written baselines

## A1 The API session; Bouw7's API objects on top

The proposal's phase 1 and use one (§3). Test-first in `Bruinet.Core.Tests` against a WireMock server; acc is the
gate, not the workbench.

- [x] A1.1 **B** Design note in this folder (`session-design.md`): the public types, written as the Bouw7 code that
  will use them. `ApiSession`, the environment guard, the token provider, a marked resource, the seed. Reviewed
  before A1.2
- [x] A1.2 **B** The session: a base address per environment over `HttpClient`, and the environment guard (the hosts
  a suite may call, checked before every call, from the suite's settings)
- [x] A1.3 **B** The account: a token provider per way of signing in. First the web session's saved sign-in (what
  `Bouw7Api.As` reads from the storage-state file); client credentials and an API key as the same interface, built
  when a suite needs one. Renewal when a token runs out
- [x] A1.4 **B** The `Writes` guard: a write as an account whose `Writes` is `no` is refused before it is sent
- [x] A1.5 **B** Every call a step: a `DelegatingHandler` that writes the step log (account, status, masked bodies)
  and keeps the call as an exchange in the shape `Bruinet.Contracts` reads. A failing call's message names method,
  path, account and status
- [x] A1.6 **B** Marked resources: a declaration (where the list is, which field carries the marker, how it is
  deleted) and the one rule in Bruinet: only a record whose name, as the server gives it, carries a run's marker is
  deleted
- [x] A1.7 **B** `CleanUpRegistry` joined to the session: a create through the session registers its delete; a sweep
  of earlier runs' leftovers by marker at the start of a run
- [x] A1.8 **B** The seed helper: "make what is not there yet, leave what is", over the same declarations
- [x] A1.9 **B** The bridge in `Bruinet.Html.Playwright`: an API session from a browser context's saved sign-in
  (`IAPIRequestContext` or the storage state). Not in Core
- [x] A1.10 **7** `Bouw7Api` becomes API objects (`Contacts`, `Projects`, …) over the session. What stays: the bodies
  of create, the list query, "a delete sends the whole record", "a contact something refers to stays". The six
  marked resources become six declarations; `Bouw7Seed`'s three records become data
- [x] A1.11 **7** Nothing in Bouw7's tests or steps calls the session or `HttpClient`: only API objects. A banned
  symbol or an architecture test says so
- [x] A1.12 **B** `PublicAPI.Unshipped.txt`, a row in `api-upgrade`'s `changes.md`, `CHANGELOG.md`
- [x] **Gate:** `Bouw7Api` holds only what is Bouw7's; the Contacts and Projects modules and the seed run pass on acc
  as in A0.10; every API call of that run is in the step log

## A2 `bruinet upgrade` and the rename to the `Api` family

The proposal's Names section. The command first, so the rename is its first real use.

- [x] A2.1 **B** A rename row in `changes.md` gets a form a program reads: old name, new name, kind (package,
  namespace, type). `check-api-changes.ps1` accepts it; the other rows stay prose
- [x] A2.2 **B** `bruinet upgrade <folder> [--from <commit>] [--check]`: rewrites `using` lines and qualified names in
  C#, package and project references, and names held as text (`bdd.config.md`'s `## Using`, settings sections).
  `--check` lists and changes nothing. Then it builds and reports what no row explains
- [x] A2.3 **B** The same as a tool of `bruinet mcp`
- [x] A2.4 **B** Proven on a throw-away rename of a small package in `samples/` and `tests/`, then undone
- [x] A2.5 **B** The renames, each with its row: `Bruinet.Contracts` to `Bruinet.Api.Contracts`,
  `Bruinet.Mocking.Http` to `Bruinet.Api.Replay`, `Bruinet.Mocking` (server, stubs, request steps) to
  `Bruinet.Api.Mocking`; their test projects, `PackageId`s, solution folders, docs and skills
- [x] A2.6 **B** The sensors move as A0.3 decided; the analyzers as A0.7 found. `Bruinet.Mocking.Data` and its hooks
  are not renamed
- [x] A2.7 **B** The session of A1 takes its final name if A0.1 chose a package
- [x] A2.8 **B** A major version, both renames in it (`BREAKING-CHANGES-POLICY.md`, `VERSIONING.md`); no bridge
  packages
- [x] A2.9 **7** `bruinet upgrade` on Bouw7: expected to change nothing but A1's names, since Bouw7 uses neither
  renamed package. The report is the proof
- [x] **Gate:** Bruinet builds and A0.9's tests pass with the same counts under the new names; `bruinet upgrade
  --check` on `samples/`, `tests/` and Bouw7 reports nothing left

## A3 A form checked through the API

The proposal's phase 2 and use two (§4).

- [x] A3.1 **B** A page object's save returns the exchange it caused (the write and its answer), from the calls the
  web head already sees. The new record's id comes from it, not from the screen
- [x] A3.2 **B** The field map: fill each field with a value of its own, find each value in the body that was sent,
  keep the map (page object field to a place in the JSON) beside the page object. A field with no place in the body
  is a finding, listed
- [x] A3.3 **B** The three-way check: typed, sent, read back. `RoundTripCheck` compares the last two; the first is
  new
- [x] A3.4 **B** A read after a write waits by polling the API object with a named interval, never a sleep
- [x] A3.5 **7** `PlacesSaveTests` checks each saved form against the API with no line per form
- [x] A3.6 **7** The other direction for one form: a record made through the API, the page shown to have it
- [x] A3.7 **B** A suite phrase pattern for scenarios (`Then the contact "{run} Arend" is stored as a Supplier`),
  bound to an API object; no generic HTTP steps
- [ ] A3.8 **B** The Scraper's factory drafts the pair for a form it has a page object for (after A3.5 holds)
- [x] **Gate:** `PlacesSaveTests` on acc: every form it saves is checked typed, sent and stored; the findings (a
  field never sent, a value stored differently) are a list a person reads

## A4 The observed description

The proposal's phase 3 and the first half of use three (§5).

- [x] A4.1 **B** One store of exchanges, fed by the walk (which today writes a held call down and keeps no answers),
  a test run, an HTTP recording and the API objects' own calls. Redaction and the key scan of `Mocking.Http` on what
  is kept in git
- [x] A4.2 **B** The merge: addresses with their parameters found by comparing calls, each body's shape, how many
  exchanges stand behind each. Shapes only, no values. Output: a draft OpenAPI file in the suite, marked "observed"
- [x] A4.3 **B** `bruinet api describe` (write or refresh the draft) and `bruinet api diff` (two descriptions)
- [x] A4.4 **B** The calls no test makes, listed against the description (as `UnregisteredEndpoints` does for
  contracts)
- [x] A4.5 **B** The diff as a "site changed" signal in `run-triage`
- [x] A4.6 **7** Bouw7's draft from one walk and one suite run, committed
- [ ] A4.7 **7** When Heimdall's description is in the suite (A0.5): the observed one held against it, the
  differences counted. That number says how good the observing is
- [x] **Gate:** Bouw7 has a draft description from one walk and one suite run; an answer changed on purpose (in a
  recorded exchange) shows in `bruinet api diff`. If the merge proves harder than it looks, Dev Proxy is tried
  before more is built (§2)

## A5 `Bruinet.Api.OpenApi`

The proposal's phase 4. Needs a description: Heimdall's (A0.5), or A4's once it is trusted.

- [x] A5.1 **B** The package, with `Microsoft.OpenApi`, in `Directory.Packages.props` (versions from A0.8)
- [ ] A5.2 **B** The typed client: Kiota into the session (base address, token, the logging handler); NSwag only
  where a description is 2.0
- [x] A5.3 **B** Each answer checked against its schema, beside `Bruinet.Api.Contracts`, not instead of it
- [x] A5.4 **B** A fake service from a description (`WireMock.Net.OpenApiParser`), which hand-written stubs override
- [ ] A5.5 **7** Bouw7's API objects on the generated client: a renamed field fails the build
- [x] A5.6 **B** Schemathesis as an outside tool at a gate, as A0.4 decided; never a package reference
- [ ] **Gate:** one suite with a real description builds its API objects on the generated client, and its tests pass
  against the fake service with acc switched off

## A6 The `api` head

The proposal's phase 5 and the second half of use three.

- [x] A6.1 **B** The head: the same feature files, bound to API objects, against the running environment, tagged
  `@api`. The rule for which example goes there is the service head's ("ask what the assertion looks at")
- [x] A6.2 **B** `bruinet spec trace` counts an example run on the `api` head
- [x] A6.3 **B** The spec format with `API` in its `Where` row, for a product whose user is another system;
  `spec-from-app` takes exchanges as a second source
- [x] A6.4 **7** One Bouw7 spec's data examples on the head
- [x] **Gate:** those examples pass on acc on the `api` head, count in `bruinet spec trace`, and their time per
  example is written beside the same examples' time through the UI

## A7 `Bruinet.Api.Soap`

Not planned. It starts when a site has a SOAP service: `dotnet-svcutil`, `System.ServiceModel.*`, in its own package
so no other suite gets WCF (AD-026). The session of A1 already carries a SOAP call as a POST with XML.

## A8 Extract and close

AGENTS.md's rule for `.my/`: extract, then delete.

- [x] A8.1 **B** Decision records in `.docs/decisions/` for what rules something out: no `Bruinet.UI` prefix; no
  generic HTTP steps; no bridge packages; no Pact; observed and described as two grades
- [x] A8.2 **B** A guide in `docs/guides/` (API objects and the session) and the commands in `docs/run/cli.md`;
  `docs/README.md` and the README's package table show the `Api` area
- [x] A8.3 **B** A skill for writing an API object, beside `html-ui-test`; `site-setup` and `run-triage` mention the
  session and the diff
- [ ] A8.4 **B** `.my/api/` deleted, links repointed
- [ ] **Gate:** nothing in `docs/`, `.docs/` or code points into `.my/api/`

## Not in this plan

What the proposal does not propose: load and performance testing, security scanning, tests kept as `.http` or
Postman files, GraphQL, gRPC, queues, and a `Bruinet.Data.*` family for SQL replay.
