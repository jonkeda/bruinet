# Bruinet.Api.OpenApi

A description of an API, for tests: read from an OpenAPI 3 document in JSON, or merged from the calls Bruinet saw
(an observed description: shapes only, no values, and what was seen, never what is promised).

```bash
dotnet add package Bruinet.Api.OpenApi
```

- `ApiDescription.Read` reads a description; `ObservedDescription.Merge` makes one from exchanges.
- `DescriptionDiff.Compare` names what two descriptions differ in: the sign that a back end changed.
- `DescriptionCheck.Uncalled` lists the operations no exchange reached; `DescriptionCheck.Check` holds an answer
  against its schema.
- `SchemaSample.Of` gives a value that fits a schema, for a fake service.

**Name with it:** Bruinet.Core (the API session, which keeps the exchanges) and Bruinet.Api.Mocking for a fake
service from a description. The command line has it as `bruinet api describe`, `diff`, `uncalled` and `check`.

The packages come from the organisation's GitHub Packages feed; Bruinet's README says which package goes with which
app, and what each targets.
