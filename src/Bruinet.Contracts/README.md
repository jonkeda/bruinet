# Bruinet.Contracts

API contract checks against the real service: each response against the DTO the app reads it into, each write against the request a reference client sends, created values read back unchanged, and one report per run that lists what was validated, mismatched, blocked or skipped.

```bash
dotnet add package Bruinet.Contracts
```

**Name with it:** A test project that calls the real service.

The packages come from the organisation's GitHub Packages feed; Bruinet's README says which package goes with which
app, and what each targets.
