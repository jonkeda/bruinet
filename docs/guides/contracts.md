# API Contracts

`Bruinet.Contracts` checks the HTTP calls an app makes against the DTOs the app reads them with. A live run
proves the real service still answers in that shape. A hermetic run proves every write the app sends to a mock
backend still matches the request the reference client sends. The decision is
[AD-035](../../.docs/decisions/ad-035-an-api-contract-is-the-dto-the-app-reads.md).

## Contracts

A `DtoContract` names an endpoint, such as `GET /contact/{id}`, and the response type. Group a module's contracts in
a class that implements `IContractModule`; `ContractModules.FromAssembly` finds every module in a test assembly.

```csharp
public sealed class ContactsContracts : IContractModule
{
    public static readonly DtoContract ContactDetail = new("GET /contact/{id}", typeof(ContactDto));

    public string Name => "Contacts";
    public IReadOnlyList<DtoContract> Contracts => [ContactDetail];
    public IReadOnlyList<MockContractRequest> MockRequests => [new(ContactDetail, "/contact/101")];
}
```

A write's contract may add an `ExpectedRequest`: the body kind (JSON, form or multipart), where it came from, and
each field with the kind of value it must hold. Take it from the reference client's serializer, not from the new app.

## The app's exchange log

The app logs its calls its own way. Map each entry into a `ContractExchange` with the method, the absolute URL, the
status, the response and request bodies, whether the body was truncated, and the environment.

## A live run

1. Run the app against the real service and read its exchange log while the flow runs.
2. For each contract, take `ContractExchanges.Newest(exchanges, contract, started)`.
3. Check it with `ExchangeContractCheck.Check(exchange, contract, options, evidence)`. `ContractEvidence` names the
   environment the run targets and the ones it never accepts; a call a local host answered is blocked.
4. Write the results with `ContractRunFolder.Write`. It writes `{report}/report.md` and `{report}/outcomes.json`,
   then rewrites the run's `summary.md` from every report in the folder, so several test processes with the same
   run folder share one summary. `UnregisteredEndpoints.Find` lists the calls no contract covers.

For a create or update, `RoundTripCheck` compares what the app sent with what the service returned when read back.

## A hermetic run

Collect the writes the mock backend received as `RecordedWrite`s and pass them to
`ExpectedRequestMockCheck.Failures(contracts, writes)`. Fail the test when it returns anything. The mock contract
requests (`MockRequests`) also let a unit test check that each mock answer still deserializes into its DTO.
