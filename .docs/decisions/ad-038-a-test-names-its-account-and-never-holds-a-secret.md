# AD-038: A Test Names Its Account And Never Holds A Secret

A test or a scenario signs in as an account it names (`NormalUser`, `Reader`), from two markdown files side by
side: `TestAccounts.md`, tracked, with each account's name, role, organization and whether it writes; and
`TestAccounts.local.md`, git-ignored, with the same names and their usernames and passwords. `TestAccounts`
(`Bruinet.Core.Testing`) reads them by name; nothing else is read. An account that is not in the first file, or
has no username or password in the second, **fails** the test that names it, saying which. A secret is typed with
`SetSecret`, which logs `***` and never puts the value in a message; an account's `ToString` is its name.

**Why:** a password changes, or the person behind an account does, in one file, and no test or scenario changes.
One place for the values makes it always clear where one came from. A missing account is a broken set-up: a skip
would hide it until nobody remembers the suite was not running. The call log, step log and failure messages travel
(artifacts, reports, an agent's context), so a value that reaches them has leaked.

**Ruled out:**
- credentials from environment variables, user secrets or a config file a test reads directly;
- a username, password or token in a tracked file, a log, a report or a failure message;
- skipping a test whose account is missing;
- typing a secret with `SetText`, `TypeText` or `Enter`, which log the value.

**Broken when:** a test reads `Environment.GetEnvironmentVariable` for a credential; `TestAccounts.local.md` is
tracked; a run with a missing account reports a skip; a call log or a failure message contains a password.

Evidence: the Bouw7 suite's first sign-in on acc (BruinetB7, `Bouw7/.testing/400-FrictionLog.md`, rows 1 and 7).
