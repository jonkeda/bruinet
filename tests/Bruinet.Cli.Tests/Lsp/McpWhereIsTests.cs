using System.Text.Json.Nodes;
using Xunit;

namespace Bruinet.Bdd.LanguageServer.Tests;

/// <summary>
/// The MCP server's where_is (AI7) over this solution's Todo suites: a control's page object line and the app line that
/// makes it, the web app's for the web suite and the MAUI app's for the MAUI one. Needs the Todo suites built.
/// </summary>
public sealed class McpWhereIsTests : IDisposable
{
    private readonly BddMcpServer _server = new(Solution());

    public void Dispose() => _server.Dispose();

    [Fact]
    public void WhereIs_AWebControl_GivesItsMember_ItsTestId_AndTheRazorLine()
    {
        var where = Answer(Call("where_is", new JsonObject { ["suite"] = "Bruinet.Samples.Todo.Web.Uat", ["page"] = "Todo Detail", ["control"] = "Delete" }));

        Assert.Matches(@"TodoDetailPage\.cs:\d+$", (string)where["pageObject"]!);
        Assert.Equal("TodoDetail_Delete", (string)where["locator"]!);
        var top = where["app"]![0]!;
        Assert.Contains("Bruinet.Samples.Todo.Web", (string)top["file"]!, StringComparison.Ordinal);
        Assert.Matches(@"TodoDetailPage\.razor:\d+$", (string)top["file"]!);
        Assert.True((bool)top["testId"]!);
    }

    [Fact]
    public void WhereIs_TheSameControlInTheMauiSuite_GivesTheXamlLine()
    {
        var where = Answer(Call("where_is", new JsonObject { ["suite"] = "Bruinet.Samples.Todo.Uat", ["page"] = "Todo Detail", ["control"] = "Delete" }));

        Assert.Matches(@"TodoDetailPage\.xaml:\d+$", (string)where["app"]![0]!["file"]!);
    }

    [Fact]
    public void WhereIs_AnUnknownControl_SaysWhereTheNamesAre()
    {
        var result = Call("where_is", new JsonObject { ["suite"] = "Bruinet.Samples.Todo.Web.Uat", ["page"] = "Todo Detail", ["control"] = "Frobnicate" });

        Assert.True((bool)result["isError"]!);
        Assert.Contains("list_pages", Text(result), StringComparison.Ordinal);
    }

    private static string Solution()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Bruinet.sln")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("No Bruinet.sln above the test's folder.");
    }

    private JsonObject Call(string tool, JsonObject arguments)
        => _server.Handle(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call", ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = arguments } }.ToJsonString())!["result"]!.AsObject();

    private static string Text(JsonObject result) => (string)result["content"]![0]!["text"]!;

    private static JsonNode Answer(JsonObject result) => JsonNode.Parse(Text(result))!;
}
