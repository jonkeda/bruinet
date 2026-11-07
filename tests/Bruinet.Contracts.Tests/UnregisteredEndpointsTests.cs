namespace Bruinet.Contracts.Tests;

[Trait("Category", "Unit")]
[Trait("Feature", "Contract")]
[Trait("Pyramid", "Unit")]
public sealed class UnregisteredEndpointsTests
{
    private static readonly DtoContract ContactDetail = new("GET contact/{id}", typeof(object));

    [Fact]
    public void Find_GroupsUncoveredCallsByTemplate_AndSkipsCoveredOnes()
    {
        ContractExchange[] exchanges =
        [
            Exchange("GET", "https://api.example.test/v2/contact/101"),
            Exchange("GET", "https://api.example.test/v2/project/201/files"),
            Exchange("GET", "https://api.example.test/v2/project/202/files?page=1"),
            Exchange("POST", "https://api.example.test/v2/contact/101")
        ];

        var found = UnregisteredEndpoints.Find(exchanges, [ContactDetail]);

        Assert.Equal(2, found.Count);
        Assert.Contains(new UnregisteredEndpoint("GET", "api.example.test", "/v2/project/{id}/files", 2), found);
        Assert.Contains(new UnregisteredEndpoint("POST", "api.example.test", "/v2/contact/{id}", 1), found);
    }

    [Fact]
    public void Report_ListsUnregisteredEndpoints()
    {
        var markdown = ContractReport.Render("Contacts", "Acceptance", [], null,
            [new UnregisteredEndpoint("GET", "api.example.test", "/v2/contact-person", 3)]);

        Assert.Contains("## Endpoints without a contract", markdown, StringComparison.Ordinal);
        Assert.Contains("| GET | api.example.test | `/v2/contact-person` | 3 |", markdown, StringComparison.Ordinal);
    }

    private static ContractExchange Exchange(string method, string path)
        => new(Guid.NewGuid().ToString(), DateTimeOffset.UtcNow, method, path, 200, "application/json", "{}");
}
