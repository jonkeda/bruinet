# AD-035: An API Contract Is The DTO The App Reads, And The Real Service Judges It

`Bruinet.Contracts` checks the calls an app made against the types the app itself reads them with. A `DtoContract`
names one endpoint and its response DTO; the validator walks a recorded body against that type, field by field, with the
app's own `JsonSerializerOptions`. A write's contract may also hold an `ExpectedRequest`: the body the reference client
sends (for a migrated app, the old app's serializer), field by field, with the value kind each field must have.

The app maps its own exchange log into `ContractExchange`, so Bruinet does not depend on any log format. A live run
checks the newest call per contract and blocks evidence it cannot trust: no call, a call a local mock answered, a call
from another environment or from one the suite never accepts (such as production), or a body the log truncated. A
hermetic run checks every write the mock backend received against its expected request, so CI guards the request with
no service. A run writes one report per module and a `summary.md` that lists every registered module, so a module never
run shows as not run.

**Why:** the DTO is what breaks when the service changes: a renamed field, a number sent as text, a date without its
offset. Checking against it finds the break before a user does, and needs no schema the service may not publish. The
expected request comes from the reference client because that is what the service is known to accept today. Only the
real service can say whether it accepts a call, so a mock that answers never passes for it.

**Ruled out:**
- keeping the contract checks inside one app's test project: the checks know nothing of the app, and a second suite
  would copy them;
- validating against the service's OpenAPI document: not every service has one, and it describes the service, not
  what the app relies on;
- passing a contract on a mock's answer: the mock holds what the test author believed, not what the service sends;
- one fixed log format: each app already logs its calls its own way; a mapping into `ContractExchange` is one method.

**Planned:** record a live contract run through `Bruinet.Mocking.Http` (AD-034), so a replay in CI answers with what the
real service sent, and the live run stays the gate.

**Broken when:** a contract passes with no call from the real service; a contract passes on production evidence the
suite excluded; a truncated body passes as complete; a write passes the hermetic check without an expected request
compared; or a registered module is missing from the run summary.
