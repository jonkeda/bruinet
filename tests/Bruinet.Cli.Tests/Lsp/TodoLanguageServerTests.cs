using Bruinet.Bdd;
using Nerdbank.Streams;
using StreamJsonRpc;
using Xunit;

namespace Bruinet.Bdd.LanguageServer.Tests;

/// <summary>
/// The server over the Todo sample, as an editor sees it. Needs the Todo sample built
/// (<c>dotnet build samples/Todo/Bruinet.Samples.Todo.slnx</c>).
/// </summary>
public sealed class TodoLanguageServerTests : IAsyncLifetime
{
    private static readonly string Todo = FindTodo();
    private readonly BddLanguageServer _server = new();
    private readonly PublishedDiagnostics _published = new();
    private JsonRpc _client = null!;
    private JsonRpc _serverRpc = null!;

    private static string Project => Path.Combine(Todo, "tests", "Bruinet.Samples.Todo.Uat");

    public async Task InitializeAsync()
    {
        var (clientStream, serverStream) = FullDuplexStream.CreatePair();
        _serverRpc = _server.Attach(serverStream, serverStream);
        var formatter = new SystemTextJsonFormatter { JsonSerializerOptions = { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase } };
        _client = new JsonRpc(new HeaderDelimitedMessageHandler(clientStream, clientStream, formatter));
        _client.AddLocalRpcTarget(_published, new JsonRpcTargetOptions { UseSingleObjectParameterDeserialization = true });
        _client.StartListening();
        await _client.InvokeWithParameterObjectAsync<InitializeResult>("initialize", new
        {
            initializationOptions = new { solutionDir = Todo },
            capabilities = new { textDocument = new { publishDiagnostics = new { } } },
        });
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _serverRpc.Dispose();
        _server.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task TheWithoutUsingFile_GetsUatb010_WordForWordAsTheGateReportsIt()
    {
        var path = Path.Combine(Project, "ExpectedFailures", "TOD.11-without-using.feature.md");
        var uri = new Uri(path).AbsoluteUri;
        var gate = GateDiagnostics(path).Where(d => d.Code == "BDDB010").ToList();

        await _client.NotifyWithParameterObjectAsync("textDocument/didOpen", new { textDocument = new { uri, version = 1, text = File.ReadAllText(path) } });
        var published = await _published.WaitAsync(uri, diagnostics => diagnostics.Any(d => d.Code == "BDDB010"));

        Assert.NotEmpty(gate);
        Assert.All(gate, expected => Assert.Contains(published.Diagnostics, d => d.Code == "BDDB010" && d.Message == expected.Message && d.Range.Start.Line == expected.Location.LineNumber - 1));
    }

    private static IReadOnlyList<BddDiagnostic> GateDiagnostics(string path)
    {
        using var suite = Workspace.BddSuite.Open(Path.Combine(Project, BddFileFormat.UiConfigFileName));
        var catalogs = suite.TryGetCatalogs(out var reason) ?? throw new InvalidOperationException($"The Todo MAUI suite cannot be bound: {reason} Build samples/Todo first.");
        return UatScenarioCheck.CheckFile(path, catalogs);
    }

    private static string FindTodo()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var todo = Path.Combine(folder.FullName, "samples", "Todo");
            if (Directory.Exists(todo))
            {
                return todo;
            }
        }

        throw new DirectoryNotFoundException("samples/Todo is not above the test's folder.");
    }
}
