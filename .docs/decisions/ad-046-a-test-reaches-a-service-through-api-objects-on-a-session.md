# AD-046: A Test Reaches A Service Through API Objects On A Session

**Decided:** what a test does beside the screen on a running environment (making its data, removing it, reading what
was stored) goes through the suite's **API objects**, written over Bruinet's **API session** (2026-11-21):

| Decision | What it means |
| --- | --- |
| **The session holds what is not the app's** | `ApiSession` (`Bruinet.Core.Api`) checks the host of every call against the hosts the suite named, signs it as an account of `TestAccounts.md`, refuses a write as an account whose `Writes` is `no`, keeps every call as a masked `ApiExchange` and a line of the step log, and runs the undo of what was made. A suite writes none of this. |
| **Only a record the service says is marked is deleted** | A kind of record is declared once (`MarkedResource`). A delete reads the record from the service first and refuses one whose text there carries no run marker. The rule is in Bruinet, where no suite can forget it. |
| **A test calls API objects, never the session** | An API object is the suite's words for one subject, as a page object is for a screen. Tests, step definitions and scenarios do not name `ApiSession` or `HttpClient`; a suite has a test that says so. |
| **Two grades of knowing an API** | *Observed*: only the calls the app was seen to make; API objects are hand-written over JSON, and a description is merged from the exchanges (`bruinet api describe`). *Described*: an OpenAPI document; a generated client rides on `ApiSession.Client`, and each answer is checked against its schema. A suite moves from the first to the second without changing a test. |
| **An observed description keeps shapes, never values** | It says what was seen, never what is promised, and it is marked so (`x-observed`). |
| **The packages are one family** | `Bruinet.Api.Contracts`, `Bruinet.Api.Replay`, `Bruinet.Api.Mocking`, `Bruinet.Api.OpenApi`. The neutral part is in `Bruinet.Core`, which needs nothing but .NET for it. There is no `Bruinet.UI` prefix: the base packages serve heads without a screen, and the platform packages already say what they are. |
| **A rename is followed with a command** | A renamed package or namespace is a row of `## Renames` in the changes list, and `bruinet upgrade <folder>` applies the rows to a consumer and builds. No old package forwards to a new one. |
| **Below the screen over the wire is a head** | A scenario tagged `@api` runs on a suite whose head is `api`, bound to API objects ([AD-020](ad-020-a-tier-is-a-head.md)'s rule, against a running environment). |

**Why:** Bouw7's suite wrote 350 lines of its own for set-up and clean-up through its back end; two thirds of them
were not about Bouw7 (the host check, the token, the `Writes` rule, deleting by marker). Moved into the session, the
suite kept only what Bouw7 is: the body of a create, a list's query, "a delete sends the whole record". Reading a
saved form back through the API found on the first run that a field the screen calls Description is stored as
`name`, which no screen test shows. Evidence: the proposal and the plan at
[.my/api](https://github.com/jonkeda/bruinet/tree/api/.my/api), and Bouw7's `Support/Api`.

**Ruled out:**
- generic HTTP steps in a scenario (`When I POST /contact`), and tests kept as `.http` or Postman files: a scenario
  in the API's addresses does not survive a change of the API;
- a test, a step definition or a page object that calls the session or an HTTP client itself;
- deleting a record because a caller says it is a test's: the name the service gives decides;
- a bridge package or type forward for a renamed package: it hides the change until it breaks;
- consumer-driven contract tooling (Pact): a contract is the DTO the app reads ([AD-035](ad-035-an-api-contract-is-the-dto-the-app-reads.md));
- a library for reading descriptions (`Microsoft.OpenApi`) while Bruinet reads only OpenAPI 3 in JSON: the reader,
  the schema check and the example are a few hundred lines on `System.Text.Json`. It is the first thing to take when
  a suite brings a YAML or an OpenAPI 2 description;
- generated calls (Schemathesis) started by Bruinet on a shared environment: a person runs them, reads only;
- a value in an observed description.

**Broken when:**
- `ApiSessionTests` fail: a host that was not named is called, an account that never writes sends a write, a secret
  is written into an exchange, or a record without a marker is deleted;
- a suite's test outside its API objects names `ApiSession` or `HttpClient` and the suite's own test does not fail;
- `bruinet upgrade --check` on Bruinet's own `samples/` reports a name left to change;
- an observed description holds a value from an exchange (`DescriptionTests`);
- an untagged scenario runs on the `api` head, or an `@api` scenario binds a screen step without BDDB013.
