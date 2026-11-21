using System.Net;
using System.Text.Json.Nodes;
using Bruinet.Api.OpenApi;
using Bruinet.Core.Api;

namespace Bruinet.Api.Mocking.Tests;

// A fake service from a description: every operation answers with an example of its schema, and a test's own stub wins.
public sealed class DescribedStubsTests
{
    [Fact]
    public async Task EveryDescribedOperationAnswers_WithWhatFitsItsSchema_AndATestsOwnStubWins()
    {
        var description = ObservedDescription.Merge(
        [
            new ApiExchange("1", DateTimeOffset.UtcNow, "GET", "https://api-acc.example.com/contact/12", 200, "application/json", """{"id":12,"name":"Arend","lines":[{"amount":2.5}]}"""),
            new ApiExchange("2", DateTimeOffset.UtcNow, "DELETE", "https://api-acc.example.com/contact/12", 204, null, null),
            new ApiExchange("3", DateTimeOffset.UtcNow, "GET", "https://api-acc.example.com/contact/12/references", 200, "application/json", """{"canDeleteContact":true}"""),
        ]);
        using var server = MockApiServer.Start();
        using var http = new HttpClient { BaseAddress = new Uri(server.BaseUrl) };

        var stubbed = server.StubDescribed(description);
        var body = await http.GetStringAsync(new Uri("/contact/77", UriKind.Relative));
        using var deleted = await http.DeleteAsync(new Uri("/contact/77", UriKind.Relative));
        server.Get("/contact/5").ReturnsText("""{"id":5,"name":"Mine"}""", 200, "application/json");
        var own = await http.GetStringAsync(new Uri("/contact/5", UriKind.Relative));

        Assert.Equal(3, stubbed);
        Assert.Equal("""{"id":0,"name":"text","lines":[{"amount":0}]}""", JsonNode.Parse(body)!.ToJsonString());
        Assert.True(DescriptionCheck.Check("sample", description.Find("GET", "/contact/77")!.Responses["200"]!, JsonNode.Parse(body)).Passed);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Contains("Mine", own, StringComparison.Ordinal);
    }
}
