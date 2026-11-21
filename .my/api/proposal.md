# API testing with Bruinet: proposal

Status: built in most of its phases on 2026-11-21; [plan.md](plan.md) has what was done per task and [report.md](report.md) the account. The text below is the proposal as it was written on 2026-11-20, with two rows corrected. It continues the service-clients design
([B18](design.md)), which was written before Bouw7 had any API code; Bouw7 now has 350 lines of it
(`Bouw7.Web.UITests/Support/Api`), and this proposal starts from what those lines turned out to need.

## In short

- **Which APIs:** HTTP with JSON first, in two grades: with no description at all (Bouw7 today) and with an OpenAPI
  description. SOAP is a second adapter, built when a site has one. GraphQL, gRPC and message queues are not planned.
- **Libraries:** `HttpClient` to send, `Microsoft.OpenApi` to read a description, Kiota for typed clients,
  WireMock.Net (already here) to fake a service from a description. Nothing new for assertions.
- **The shape:** an **API object** beside the page object, on an **API session** that holds what every suite needs
  and Bouw7 wrote by hand: the environment guard, the account's token, the `Writes` guard, the run marker, clean-up.
- **Three uses, one mechanism:** set-up and clean-up (Bouw7 today), a form checked against what the API stored, and
  specs. All three read and write the same record of an **exchange** (a request and its answer), which
  `Bruinet.Contracts` and `Bruinet.Mocking.Http` already have.
- **First step:** move the generic two thirds of `Bouw7Api` into Bruinet. It is useful by itself and proves the shape
  on code that already runs.

## What exists

| Exists | What it does | What it lacks for this |
| --- | --- | --- |
| `Bouw7Api`, `Bouw7Seed` (Bouw7 suite) | Set-up, clean-up and read checks on acc: token from the saved sign-in, acc-only host check, `Writes` guard, deletes only marked records, seed records | All of it is Bouw7's own; dynamic JSON, no description, no step log |
| `RunMarker`, `CleanUpRegistry` (`Bruinet.Core`) | One marker per run in a record's name; undos run in reverse | Nothing joins the registry to a call: a create does not register its delete |
| `Bruinet.Contracts` | An exchange checked against the DTO the app reads; round trip of a write; calls no contract covers (AD-035) | Reads the app's own exchange log; has no client of its own |
| `Bruinet.Mocking`, `Bruinet.Mocking.Http` | WireMock stubs; record and replay of an app's outgoing calls, with redaction (AD-034) | Faces the other way: it answers the app, it does not call a service |
| The service head | Feature files below the UI, in the test process (AD-020) | In process only; nothing over the wire to a running environment |
| The Scraper's walk | Sees every call a press makes and holds writes back (AD-043) | Writes the held call down, keeps no answers, makes no description |
| Accounts | `TestAccounts.md`, the `Writes` column, the saved sign-in | Nothing; the API session uses it as it is |

B18 chose generated, typed clients from a description checked into the suite. Bouw7 then built the opposite: calls
learned from what the web app sends, as dynamic JSON, because no description was at hand. Both are real cases, so the
proposal carries both and lets a suite move from the first to the second.

## 1. Which kinds of API

What matters more than the protocol is how much is known about the API:

| Grade | Known | Example | What Bruinet can do |
| --- | --- | --- | --- |
| Observed | Only the calls the app was seen to make | Bouw7's back end today | Set-up, clean-up, checks by hand-written calls; a draft description from the exchanges (section 5) |
| Described | An OpenAPI or WSDL file | Bouw7 once Heimdall's description is in the suite | The above, plus a typed client, each answer checked against its schema, a fake service, the list of calls never tested |

| Kind | Proposal | Why |
| --- | --- | --- |
| HTTP with JSON, no description | **First** | It is what a web app's own back end is, and what Bouw7 has. Every other kind rides on the same session |
| OpenAPI 3.x (and 2.0) | **First**, as an add-on package | The build becomes the gate: a renamed field fails the build, not a night run (B18) |
| OData | No package | It is HTTP with JSON and a query string; a small query helper at most, when a site has one |
| SOAP | **Second**, when a site has one | A SOAP call is a POST with XML, so the session already carries it and `Mocking.Http` already redacts it. Typed calls need WCF client packages, which a suite without SOAP should not get (AD-026) |
| GraphQL | Later, on demand | One POST address; the session carries it. A typed client is its own generator |
| gRPC, queues, webhooks | Not planned | Another transport; no site asks for it |

## 2. Libraries to build on

As WireMock is under `Bruinet.Mocking` and Playwright under `Bruinet.Html.Playwright`: one library per job, behind
Bruinet's own types, in a package only the suites that need it take.

| Job | Library | Proposal |
| --- | --- | --- |
| Send a call | `HttpClient` with a `DelegatingHandler` (.NET itself) | **Use.** No dependency, works for every stack, and `HttpReplayHandler` is the same kind of handler, so an API test can be replayed too |
| Send a call as the browser's session | Playwright's `IAPIRequestContext` | **Use as a bridge only**, in `Bruinet.Html.Playwright`: an API session made from a browser context's saved sign-in. `Bouw7Api.As` does this by hand from the storage-state file. Not the core, because a MAUI suite has no Playwright |
| Read and write an OpenAPI description | `Microsoft.OpenApi` | **Use**, in the OpenAPI package |
| Check an answer against its schema | `JsonSchema.Net` or `NJsonSchema` | **Use one**, chosen at build time. Beside `Bruinet.Contracts`, not instead: the DTO says what the app reads, the schema what the service promises |
| Typed client from a description | Kiota; NSwag where a description is 2.0 only | **Use** (B18's choice stands) |
| Typed client with no description | Refit | Not now: a hand-written API object over the session is as short and has no generator |
| Fake service from a description | `WireMock.Net.OpenApiParser` | **Use**: stubs for every described call, which hand-written stubs then override |
| Description from observed calls | Microsoft Dev Proxy (its OpenAPI generator), mitmproxy2swagger, a Playwright HAR | **Write our own** over the exchange record: Bruinet already holds the exchanges, it is a merge of JSON shapes, and nothing leaves the machine. Dev Proxy is the fallback if the merge proves harder than it looks |
| Generated calls from a description (property-based) | Schemathesis | **Outside tool at a gate**, never a package reference: it is a Python command line. Only on an environment where writes are safe |
| What changed between two descriptions | oasdiff | **Outside tool**, optional; our own diff of two observed descriptions may be enough |
| SOAP client | `dotnet-svcutil`, `System.ServiceModel.*` | When SOAP is built |
| Consumer-driven contracts | Pact | **No**: AD-035 already decided a contract is the DTO the app reads |
| Another test vocabulary | RestSharp, RestAssured.Net, Karate, Postman and Newman, `.http` files | **No**: each brings its own way to write a test beside page objects and scenarios |

The names are from memory and were not checked online for this document: check each package's current version and
licence when it goes into `Directory.Packages.props`.

## The shape

```text
Tests, scenarios, specs
  API objects (the suite's: Contacts, Projects)        beside page objects
    Bruinet API session and its guards                  beside controls and contexts
      Bruinet.Core: accounts, masks, step log, RunMarker, exchange
        HttpClient, a generated client (Kiota), later a SOAP client
```

**API session** (generic, in Bruinet). Everything `Bouw7Api` does that is not about contacts or projects:

- A base address per environment, and an **environment guard**: the hosts a suite may call, checked before every
  call. Bouw7's "only an acc host" becomes a line in the suite's settings.
- **The account**: a token provider per way of signing in (the web session's saved sign-in, client credentials, an
  API key), renewed when it runs out; a write as an account whose `Writes` is `no` is refused.
- **Every call a step** in the step log, with the account, the status and the masked bodies, and kept as an exchange
  in the shape `Bruinet.Contracts` reads. A failing call's message names the method, path, account and status.
- **Clean-up**: B18's registry (each create registers its undo, run in reverse at the end), and a declared
  **marked resource**: where its list is, which field carries the marker, how it is deleted. `Bouw7Api` has six of
  these written out by hand; each becomes one line. The rule that only a record whose name, as the server gives it,
  carries a run's marker is deleted moves into Bruinet, where no suite can forget it.
- **Seed**: "make what is not there yet, leave what is" (`Bouw7Seed.Ensure`), over the same declarations.

**API object** (the suite's). What a page object is for a screen: the suite's own words for one subject, such as
`Contacts.Create(name, type)` and `Contacts.Named(text)`. Hand-written over dynamic JSON at the observed grade, a
thin layer over the generated client at the described grade. Tests and step definitions call API objects, never the
session and never `HttpClient`, as they never call the driver.

**Packages.** B18's cut stands: the neutral parts in `Bruinet.Core` (no dependency), one package per protocol:
`Bruinet.Api.OpenApi`, later `Bruinet.Api.Soap`, in place of B18's `Bruinet.Services.*`, so they are not taken
for the service head. What the existing packages are called beside them is the next section.

## Names: an `Api` family, and whether the rest becomes `UI`

Asked in review: should `Bruinet.Mocking` and `Bruinet.Contracts` move under `Bruinet.Api`, should the other projects
then become `Bruinet.UI.*`, and how do projects that already use Bruinet follow.

### The `Api` family: yes

| Today | Becomes | Note |
| --- | --- | --- |
| (new) | `Bruinet.Api.OpenApi`, later `Bruinet.Api.Soap` | As above |
| `Bruinet.Contracts` | `Bruinet.Api.Contracts` | It checks API calls and nothing else |
| `Bruinet.Mocking.Http` | `Bruinet.Api.Replay` | It records and replays calls; "replay" is what its guide and its types already say |
| `Bruinet.Mocking` (the WireMock server, stubs, request steps) | `Bruinet.Api.Mocking` | `Mocking`, not `Mock`: a namespace named `Mock` gets in the way of every type and variable of that name |
| `Bruinet.Mocking` (`Sensors/`: mock audio, frames, input) | Stays out of `Api` | These fake a device, not a service. They need a home first: their platform package, or a small package of their own if AD-026 gives one a reason |
| `Bruinet.Mocking.Data` and its hooks | Not in this rename | SQL replay is a database, not an API. A later `Bruinet.Data.*` is its own question |
| `Bruinet.Mocking.Analyzers` | `Bruinet.Api.Mocking.Analyzers` | Read 2026-11-21: its one rule (BRUINET001, AD-012) is about a mock answer's body, so it follows the WireMock server |

What it costs, counted as files that name the package (sources, projects and docs, 2026-11-20):

| Package | Files in Bruinet | Files in Bouw7 |
| --- | --- | --- |
| `Bruinet.Mocking` (with `.Http` and `.Data`) | 150 | 0 |
| `Bruinet.Contracts` | 42 | 0 |

Bouw7 uses neither, so for Bouw7 this rename is free. Other projects that use Bruinet were not counted.

### A `Bruinet.UI` prefix for the rest: no (decided)

- **It is the large rename.** `Bruinet.Core` is named in 1020 files in Bruinet and 171 in Bouw7, `Bruinet.Html` in
  294 and 182, `Bruinet.Bdd` in 369 and 152. Every suite that uses Bruinet changes nearly every file it has.
- **The base is not UI.** `Core`, `Bdd`, `Uat`, `TestData`, `Traits` and `Logging` serve the service head and would
  serve the `api` head: `Bruinet.Bdd` runs "with no UI" by its own description. They could not go under `UI`, so
  the prefix would only reach the platform packages (`Html`, `Maui`, `Wpf`, …), which already say what they are.
- **AD-044 names modules by prefix and makes Test the unprefixed one.** `Bruinet.Spec*` exists already; `Api` and
  `UI` are not modules but areas inside Test. An area can be shown without a rename: a solution folder, a column in
  the README's package table, a section in `docs/`.
- **`Api` earns its prefix where `UI` does not**, because the API packages are new or small and unused by Bouw7,
  and without the prefix nothing ties `Contracts`, `Mocking.Http` and the new session together.

### How consumers follow: a command, not hand work

A rename of a package and its namespace is the most mechanical change there is, and Bruinet already has the place
for it: the `api-upgrade` skill keeps a row per API change in `references/changes.md` ("Find" and "Write instead"),
and `check-api-changes.ps1` fails a change that has no row. Today an agent reads the rows and applies them by hand.

| Part | Proposal |
| --- | --- |
| The list | A rename row gets a form a program can read: old name, new name, kind (package, namespace, type). Other rows stay prose |
| The command | `bruinet upgrade <folder> [--from <commit>] [--check]`: applies every rename row between the consumer's old and new Bruinet. It rewrites `using` lines and qualified names in C#, package and project references, and the names a suite holds as text (`bdd.config.md`'s `## Using` table, settings sections). `--check` lists what it would change and changes nothing. Then it builds, and reports what no row explains |
| MCP | The same command as a tool of `bruinet mcp`, so an agent in a consumer's repository can run it and read the report. It adds no power over the command; the command is what does the work |
| The skill | `api-upgrade` keeps the rows that are not renames (a changed signature, a type made internal), which need judgement |
| No bridge packages | A type forward keeps a type's namespace, so it cannot carry a namespace rename, and an old package that only points at the new one hides the change until it breaks. The command at upgrade time is the bridge |
| The version | A rename is a breaking change by `BREAKING-CHANGES-POLICY.md`: a major version, both renames in the same one |

A text rewrite is enough for a namespace rename, because the old names are whole tokens that mean nothing else
(`Bruinet.Mocking.Http`). The command is proven first on Bruinet's own `samples/` and `tests/`, which the skill
already calls the first consumers, then on Bouw7.

## 3. Use one: set-up, clean-up and checks

Bouw7 does this today, and the proposal is to make it cost less per suite and per record.

| In `Bouw7Api` today | Goes to |
| --- | --- |
| Token from the saved sign-in, acc host check, `Writes` guard, error messages | The API session |
| `DeleteMarked`, `Sweep`, six `Marked…` and `DeleteMarked…` pairs | Marked-resource declarations and the clean-up registry |
| `Bouw7Seed.Ensure` | The seed helper, with Bouw7's three records as data |
| The bodies of `CreateContact` and `CreateProject`, the list query, "a delete sends the whole record", "a contact something refers to stays" | Stay in Bouw7's API objects: this is what Bouw7 is |

In a scenario the words stay the suite's (`Given a contact "{run} Arend" exists`), bound to an API object. Generic
HTTP steps (`When I POST /contact`) are not offered: B18 ruled them out, and a scenario in the API's addresses does
not survive a change of the API.

## 4. Use two: a form, checked through the API

A UI test that saves a form today proves the screen says it was saved. Reading the record through the API proves
what was stored, which catches what a screen hides: a rounded amount, a date a day off, a field that was shown but
never sent. It also saves the walk back to a list to find the record.

| Step | How |
| --- | --- |
| 1. Know which call a save made | The web head already sees the page's calls. A page object's save returns the exchange it caused (the write and its answer), so a test has the new record's id without reading it from the screen |
| 2. Know which field is which | Fill each field with a value of its own, as `PlacesSaveTests` already does with the run's marker, and find each value in the body that was sent. That gives a map from the page object's field to a place in the JSON, with no model and no hand work. Kept beside the page object; a field with no place in the body is a finding |
| 3. Check | Three values per field: what was typed, what was sent, what the API gives back when the record is read. `RoundTripCheck` in `Bruinet.Contracts` already compares the last two |
| 4. Say it | A code test asserts on the API object's record. A scenario uses a suite phrase (`Then the contact "{run} Arend" is stored as a Supplier`) |

The other direction is use one seen from the screen: make a record through the API, then check the page shows it.
Together they give each form a cheap pair of tests, which the Scraper's factory could draft for every form it has a
page object for.

A read after a write waits for the record, by polling the API object with a named interval, never by a sleep.

## 5. Use three: specs

"Specs" can mean two things here, and both are worth having.

**A description of the API, where there is none.** Every exchange Bruinet sees (the walk, a test run, an HTTP
recording, the API objects' own calls) is merged into a draft OpenAPI file in the suite: each address with its
parameters found by comparing calls, each body's shape, and how many exchanges stand behind it. It says what was
observed, never what is promised. It then pays for itself four ways:

- a **diff** between two days' descriptions shows that the back end changed, as a "site changed" signal for
  run-triage before any test fails;
- the **calls no test makes** are listed against it (`UnregisteredEndpoints` does this for contracts);
- a **fake back end** for the suite comes from it through WireMock, so page-object work can go on without acc;
- an agent writing an API object reads one file instead of probing acc.

Proposed commands: `bruinet api describe` (write or refresh the draft) and `bruinet api diff`.

**A Bruinet spec (`*.spec.md`) that an API can prove.** Two cases:

- *A product people use through a screen* (Bouw7). No new specs. An example whose Then is about stored data runs
  below the UI, by the rule the service head already has ("ask what the assertion looks at"), on a new **`api`
  head**: the same feature files, bound to API objects, against the running environment, tagged `@api`. It runs in
  under a second per example instead of twenty.
- *A product whose user is another system* (an integration API). The spec format holds as it is, with `API` in its
  `Where` row and rules about what a caller can observe: a status, a field, a message. The examples are in the
  API objects' words. `spec-from-app` gets the exchanges as a second source beside the recordings.

## Order of work

| Phase | Builds | Done when |
| --- | --- | --- |
| 1 | The API session, its guards, marked resources, the clean-up registry, the seed helper; Bouw7's API objects on top | `Bouw7Api` holds only what is Bouw7's, the Contacts and Projects modules and the seed run pass on acc, every API call is in the step log |
| 2 | The exchange a save caused, the field map, the three-way check | `PlacesSaveTests` checks each saved form against the API with no line per form |
| 3 | The observed description: `bruinet api describe`, `bruinet api diff` | Bouw7 has a draft description from one walk and one suite run; a changed answer shows in the diff |
| 4 | `Bruinet.Api.OpenApi`: typed client, schema check, WireMock from a description | One suite with a real description builds its API objects on the generated client |
| 5 | The `api` head | One Bouw7 spec's data examples run on it and count in `bruinet spec trace` |
| 6 | `Bruinet.Api.Soap` | A site has a SOAP service |

Phases 1 to 3 need nothing from outside. Phase 4 needs a description: Heimdall's, which may be copied into the
suite, or the observed one of phase 3 once it is trusted. The rename to the `Api` family and `bruinet upgrade` sit
between phases 1 and 2.

## Answered by the owner (2026-11-20)

- "Creating specs" means both kinds of section 5; the description comes first because the rest builds on it.
- A copy of Heimdall's description (the iOS repository generates its client from it) may live in the Bouw7 suite.
  Until it is there the observed description stands in, and comparing the two shows how good the observing is.
- Exchanges with their bodies may be kept in git on an acceptance environment, with `Mocking.Http`'s redaction and
  key scan. The description keeps shapes only, no values.
- The `Api` family is renamed as the Names section has it. There is no `Bruinet.UI` prefix.

## Open questions

- Where do the mock sensors of `Bruinet.Mocking` go when the rest becomes `Bruinet.Api.Mocking`? In Bruinet
  only their own tests use them (`MockSensorTests`); whether another project does was not checked. Proposal: ask the
  projects that use Bruinet, then move them to the platform they fake or drop them; it blocks only that one
  package's rename.
- When is the rename made? Proposal: after phase 1, with `bruinet upgrade` built first, so the new session is born
  under its final name and the old packages move once.
- Should the generated calls of Schemathesis ever run on acc? Proposal: reads only, as Reader, until an environment
  exists that may be filled with rubbish.
- Does the neutral part go into `Bruinet.Core` (B18) or a package `Bruinet.Api`? Proposal: Core, unless phase 1 shows
  it needs more than `HttpClient`.

## Not proposed

- Load and performance testing, and security scanning: other tools, other gates.
- Generic HTTP steps in scenarios, and tests kept as `.http` or Postman files.
- Replacing `Bruinet.Contracts` or `Bruinet.Mocking.Http`: the API session feeds them exchanges and is replayed by them.
