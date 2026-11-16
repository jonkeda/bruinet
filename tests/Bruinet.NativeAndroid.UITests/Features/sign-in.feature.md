# Feature: Signing in

## Metadata

| Field | Value |
| --- | --- |
| App | Bruinet.Samples.NativeAndroid.App |
| Area | Sign in |
| Target | NATIVEANDROID, NATIVEIOS |
| Tags | native, sign-in |

The sign-in button enables once both fields are filled. Signing in keeps the app busy for a
moment; the next step waits it out through the busy signal, not a pause.

@native @sign-in
## Scenario: A user signs in and is greeted

```gherkin
Given I am on the Sign In page
Then Sign In should be disabled
When I enter "daan" into User Name
And I enter "s3cret" into Password
Then Sign In should be enabled
When I tap Sign In
Then Status should equal "Welcome, daan"
```
