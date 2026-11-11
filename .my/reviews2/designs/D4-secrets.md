# D4. Secrets in recordings and traces, and the SQL hook

Design for R2-T19, R2-T20, R2-T21, R2-T23 (review 09, P1, P2, P3, P5). Defensive: what Bruinet writes to disk and git, and who may switch replay on.

**Approved by the owner as proposed, 2026-11-11.**

## Decisions asked

1. **Redaction is on by default, by name, and a save that still holds a secret fails.** My proposal: yes, for SQL as HTTP already does. A suite opts out per name, never wholesale.
2. **Traces mask password fields always, and named controls on request.** My proposal: yes; no switch to turn the password rule off.
3. **The SQL hook answers loopback callers only, unless a key is set.** My proposal: yes, with `begin` and `end` as `POST`.

## SQL recordings (R2-T19)

- `ReplaySession` gets `Redact`: column and parameter names whose values are written as `***`. Default: `PasswordHash`, `SecurityStamp`, `ConcurrencyStamp`, `AuthenticatorKey`, `TwoFactorSecret`, and any name matching `password|secret|token|apikey|api_key` (case-insensitive).
- A redacted value matches anything on replay, as HTTP's `***` does; a miss message prints `***`, never the live value.
- Before `Save`, the same scan HTTP runs (`HttpRedaction`'s patterns, moved to one shared internal type in `Bruinet.Mocking`) stops the save and names the table, column and row.
- Set from `uat.config.md` (`| SqlRedact | Pin, Iban |`, added to the defaults) or in code. Removing a default takes `| SqlKeep | ConcurrencyStamp |`, by name.
- `sql-replay.md` gets "What a recording holds": record against seeded test data only, never a copy of production.

## Traces (R2-T20)

- `UatPageState.ReadControl` writes `***` for a control whose element is a password input (Html `type=password`, MAUI `Entry.IsPassword`, UIA `IsPassword`, Android `password` attribute), read through a new `IsPasswordCore` per stack, false where a stack cannot tell.
- `| TraceMask | Password, Pin |` in `uat.config.md` masks controls by member name.
- Step text: a step argument bound to a parameter named like a secret (same pattern as SQL) is written `***` in the trace's step text.
- `reporting-artifacts.md`: a trace holds page values and step text; `TestResults` is not for public upload.

## SQL hook (R2-T21)

- `SqlReplayHook` (AspNetCore) and `SqlReplayModule` (SystemWeb) serve `/__bruinet/sql/begin` and `/end` only when the caller is loopback, or when `BRUINET_SQL_HOOK_KEY` is set and the request carries it in `X-Bruinet-Key`. Otherwise 404, as if the hook were absent.
- `begin` and `end` are `POST`; a `GET` is 405.
- The cookie gets `Secure` when the request is HTTPS.
- `ReplayHook`'s 409 body for `end` keeps the misses but with redacted values (follows from the SQL rule above).
- `Bruinet.Mocking`'s client side sends the key when the variable is set; Bruinet's own samples and tests run loopback and need nothing.
- `sql-replay.md`: never set `BRUINET_SQL_REPLAY` on a server others can reach.

## HTTP (R2-T23)

- Streamed parts and XML/SOAP bodies redacted by name, as JSON and form bodies are.
- A `Location` header's query redacted by field name.
- Defaults gain `token`, `secret`, `code`, `code_verifier`, `assertion`, `client_assertion`, `subscription-key`.
- The scan gains a 32-hex value that follows a key-like name.
- `http-replay.md`, Secrets: bodies are kept whole, so personal data in them goes into git.

## Tests

- `Bruinet.Mocking.Data.Tests`: a recording of a users table writes `***` for `PasswordHash`; replay matches it; a secret-looking value in an unnamed column stops the save naming table and column; a miss message never shows a redacted live value.
- `Bruinet.Mocking.Data.Hook.Tests`: a non-loopback caller gets 404 (via a fake `RemoteIpAddress`); with the key set, a call without it gets 404 and with it works; `GET` is 405.
- `Bruinet.Uat.Tests`: a failed step on a page with a password field writes `***`; `TraceMask` masks by name.
- `Bruinet.Mocking.Http.Tests`: streamed part, XML body, `Location` query each redacted.

## Who changes with it

None of tomorrow, kofflard or construction uses SQL or HTTP replay today (searched 2026-11-11). Bruinet's Todo sample and `Bruinet.Sql.Spike.Tests` recordings are re-recorded or re-checked in the same task; a recording that now fails the scan is a finding, not something to skip.
