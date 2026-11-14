# Test Accounts

A test that signs in names the account it signs in as, and never holds its password (AD-038).

## The two files

`TestAccounts.md`, tracked, in the suite's folder or one above its output:

```markdown
## Accounts

| Name | Role | Organization | Writes |
| --- | --- | --- | --- |
| NormalUser | user | Demolitions | yes |
| Reader | user | Demo | no |
```

`TestAccounts.local.md`, beside it and **git-ignored**, with the same names:

```markdown
## Accounts

| Name | Username | Password | Organization |
| --- | --- | --- | --- |
| NormalUser | user@example.com | … | |
| Reader | reader@example.com | … | |
```

A pipe in a value is written `\|`. An `Organization` in the local file overrides the tracked one. Any other column of
`TestAccounts.md` is the suite's own (`account["Role"]`).

## In a test

```csharp
var account = TestAccounts.Default.Get("Reader");   // Bruinet.Core.Testing
page.Email.SetText(account.Username);
page.Password.SetSecret(account.Password);
```

`TestAccounts.Default` reads the files found upwards from the test's output folder, once per process.
`TestAccounts.ReadFile(path)` and `FindFrom(folder)` read others.

| When | Then |
| --- | --- |
| The name is not in `TestAccounts.md` | `TestAccountException`: the test fails, naming the known accounts |
| `TestAccounts.local.md` is missing, or has no row, username or password for it | `TestAccountException`, naming the account and what is missing |
| `Writes` is `no` | `account.Writes` is false; the suite keeps that account from changing data |

`SetSecret` types the value and confirms it took; the call log shows `***` and a failure message never holds the
value. `account.ToString()` is the account's name.

## In a scenario

A fixture method declares the sign-in phrase, so a scenario names the account:

```csharp
[BddGiven("{account} is signed in")]
public void SignedIn(string account) => ...;  // TestAccounts.Default.Get(account), then the site's sign-in
```

```gherkin
Given NormalUser is signed in
```

Sign in once per account and keep the browser's state for the next run: `StorageStatePath` and
`SaveStorageState` ([Playwright](../platform-guides/playwright.md#a-site-with-a-sign-in)).
