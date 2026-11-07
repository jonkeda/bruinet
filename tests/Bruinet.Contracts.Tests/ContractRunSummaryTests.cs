namespace Bruinet.Contracts.Tests;

[Trait("Category", "Unit")]
[Trait("Feature", "Contract")]
[Trait("Pyramid", "Unit")]
public sealed class ContractRunSummaryTests
{
    private static readonly DtoContract List = new("GET /contacts", typeof(object[]));
    private static readonly DtoContract Detail = new("GET /contacts/{id}", typeof(object));
    private static readonly DtoContract Create = new("POST /contacts", typeof(object));

    [Fact]
    public void Render_UnreportedEndpointIsSkippedAndTheWorstOutcomeWins()
    {
        var mismatch = new ContractResult(Detail.Endpoint);
        mismatch.Add(ContractSeverity.Failure, "$.name", "missing");
        var recorded = new List<(string, ContractResult)>
        {
            ("Contacts", new ContractResult(List.Endpoint)),
            ("Contacts", new ContractResult(Detail.Endpoint)),
            (ContractRunSummary.ModuleName("Contacts-Create"), mismatch),
            ("Contacts", new ContractResult("Create round trip")),
        };

        var markdown = ContractRunSummary.Render([("Contacts", [List, Detail, Create])], recorded);

        Assert.Contains("| Contacts | `GET /contacts` | read | Validated |", markdown);
        Assert.Contains("| Contacts | `GET /contacts/{id}` | read | Mismatch |", markdown);
        Assert.Contains("| Contacts | `POST /contacts` | write | Skipped |", markdown);
        Assert.Contains("| Contacts | Create round trip | Validated |", markdown);
        Assert.Contains("Mismatch 1, Skipped 1, Validated 1 of 3 registered endpoints", markdown);
    }
}
