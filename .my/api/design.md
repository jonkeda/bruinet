# Service clients for test data (B18): design

Status: design, nothing built. Asked by the Bouw7 proposal (`.my/Planning/bouw7-proposal.md`,
section 8.5). Written in phase 4; the open points at the end need Bouw7's API spec and an owner.

## The need

A UI test sets up data through the UI today, and removes what it made through the UI. Bouw7's B7.HRS.01.R7
books an hour entry, deletes it in a `finally` (the row's pen, Delete, "Are you sure", Delete), and
first deletes what an earlier failed run left. That is 20 s of a 21 s test, and every create needs a
hand-written undo. Below the UI a test could create a contact in 200 ms, check it, and remove it
whatever the test did.

## What exists, and how B18 sits beside it

| Exists | What it does | B18's relation |
| --- | --- | --- |
| The service head | The app's own services, in the test process | B18 is the same idea against a running environment, over the wire |
| `Bruinet.Mocking.Http`, WireMock | Fake services for an app under test | B18 calls real services; it may record its exchanges in the same shape |
| `Bruinet.Contracts` | DTO contracts and recorded exchanges, `LiveContractGate`, a local account store | B18 reuses its DTO contracts and exchange logging; a B18 call is an exchange |
| `Bruinet.TestData` | Data definitions, no dependency | Stays dependency-free; the run marker and the clean-up registry are not data definitions, so they go in Core |
| Accounts (B14) | `TestAccounts.md` + local file, `[Account]`, Writes guard | B18 authenticates as an account and refuses a write as one whose `Writes` is `no` |

## Decisions (proposed)

| Question | Choice | Why |
| --- | --- | --- |
| What is protocol-neutral | In `Bruinet.Core`: `IServiceClient` per service and environment; `RunMarker` (`BRN-MMdd-HHmm`, one per run, set by the runner); `CleanUpRegistry` (each create registers its undo; run in reverse at the end of the test or the run, failures logged and not thrown); every call a step in the step log with masks | Core already holds accounts, masks, the step log; nothing here needs a dependency |
| Per protocol | One package each, by AD-026 R3 (a dependency users should not get): `Bruinet.Services.OpenApi` (Kiota), later `Bruinet.Services.Soap` (`dotnet-svcutil`), GraphQL, OData. An adapter only plugs the generated client into the core: base URL per environment, the token, the logging handler | A suite that has no SOAP service does not get WCF |
| Generated or dynamic | **Generated** typed clients at build time from the spec file checked into the suite | The build is the gate an agent can use; a renamed field fails the build, not a run at night |
| Kiota or NSwag | Kiota | One generator for OpenAPI 3.x with a request-adapter seam where the core's handler goes; NSwag remains if a spec is 2.0 only |
| How scenarios use it | Domain phrases written by the suite (`Given a contact "{run} Arend" exists`), never generic HTTP steps; a C# test calls the suite's helper (`Bouw7Data.Contact("Arend")`) | Scenarios stay in the screen's words and survive an API change |
| Tokens | First the UI sign-in's own token (the saved storage state already holds the cookie or bearer the web app uses); a client-credentials flow only where a service asks for one | One sign-in per account (B5) |
| Clean-up | Every create through B18 registers its delete; the registry also deletes leftovers of earlier runs by marker at the start of a run | The UI clean-up of R7 becomes one line |
| Agent loop | An agent may add a domain phrase from the spec: gate = build + one round trip on acc (create, read, delete, all as Creator) | Exact, cheap, no person needed |

## Shape

```text
Bruinet.Core
  Services/IServiceClient, ServiceEndpoint (per environment), ServiceCallLogger (a DelegatingHandler: step log + masks)
  TestData/RunMarker, CleanUpRegistry
Bruinet.Services.OpenApi
  OpenApiServiceClient<TClient>: base URL, the account's token, the logging handler, into a Kiota client
<Suite>.Web.UITests/Support/Data
  heimdall.openapi.json (checked in), the generated client, Bouw7Data (the domain helpers), the phrases
```

## Open for Bouw7

- The Heimdall API spec: the iOS repo generates a client from it. Which format (OpenAPI 2 or 3), and may a copy
  live in BruinetB7?
- Does the web app's session (cookie) authorize the API, or does the API want a bearer token from the login host?
- Does anything Bouw7 uses speak SOAP? If not, `Bruinet.Services.Soap` waits for a site that does.
- Which deletes are hard deletes on acc, and which only archive (a contact)? An archive-only clean-up leaves
  rows behind that read checks must ignore.

## Not now

Building any of it: the first build step is the core's `RunMarker` and `CleanUpRegistry` (useful with UI
clean-up too), then OpenAPI against Heimdall once its spec is here.
