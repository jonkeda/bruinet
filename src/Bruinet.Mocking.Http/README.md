# Bruinet.Mocking.Http

HTTP replay: an app's outgoing calls recorded once against the real services and answered from the recording after, in the test process (a DelegatingHandler) or through MockApiServer. SQL replay's conventions: a recording per test, volatile values from recording twice, a miss names the call.

```bash
dotnet add package Bruinet.Mocking.Http
```

**Name with it:** Bruinet.Mocking for MockApiServer, or the app's HttpClient for the DelegatingHandler.

The packages come from the organisation's GitHub Packages feed; Bruinet's README says which package goes with which
app, and what each targets.
