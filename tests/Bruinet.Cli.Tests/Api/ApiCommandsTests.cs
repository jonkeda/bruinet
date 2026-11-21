using Bruinet.Cli;
using Bruinet.Core.Api;

namespace Bruinet.Cli.Tests.Api;

// bruinet api describe, diff, uncalled and check: a description from a run's exchanges, and a run held against one.
public sealed class ApiCommandsTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("bruinet-api-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public async Task Describe_WritesWhatTheExchangesShow_AndDiffNamesWhatALaterRunChanged()
    {
        var monday = Exchanges("monday.jsonl", ("GET", "https://api-acc.example.com/contact/1", 200, """{"id":1,"name":"Arend"}"""), ("GET", "https://cdn.example.org/font", 200, "{}"));
        var tuesday = Exchanges("tuesday.jsonl", ("GET", "https://api-acc.example.com/contact/2", 200, """{"id":2,"name":"Bas","city":"Delft"}"""));

        var described = await Run("api", "describe", "--exchanges", monday, "--out", "monday.json", "--title", "Contacts", "--host", "api-acc.example.com");
        await Run("api", "describe", "--exchanges", tuesday, "--out", "tuesday.json", "--title", "Contacts");
        var same = await Run("api", "diff", "monday.json", "monday.json");
        var changed = await Run("api", "diff", "monday.json", "tuesday.json");

        Assert.Equal(0, described.Exit);
        var written = File.ReadAllText(Path.Combine(_folder, "monday.json"));
        Assert.Contains("\"/contact/{id}\"", written, StringComparison.Ordinal);
        Assert.DoesNotContain("Arend", written, StringComparison.Ordinal);
        Assert.DoesNotContain("font", written, StringComparison.Ordinal);
        Assert.Equal(0, same.Exit);
        Assert.Equal(1, changed.Exit);
        Assert.Equal("GET /contact/{id} 200: city added (string!)", changed.Output.Trim());
    }

    [Fact]
    public async Task Uncalled_And_Check_HoldARunAgainstADescription()
    {
        var first = Exchanges("first.jsonl", ("GET", "https://a.test/contact/1", 200, """{"id":1,"name":"Arend"}"""), ("POST", "https://a.test/contact", 200, """{"id":2}"""));
        await Run("api", "describe", "--exchanges", first, "--out", "api.json");
        var later = Exchanges("later.jsonl", ("GET", "https://a.test/contact/3", 200, """{"id":"3","name":"Cor"}"""), ("GET", "https://a.test/contact/4", 200, """{"id":"4","name":"Dik"}"""));

        var uncalled = await Run("api", "uncalled", "--description", "api.json", "--exchanges", later);
        var check = await Run("api", "check", "--description", "api.json", "--exchanges", _folder);
        var fits = await Run("api", "check", "--description", "api.json", "--exchanges", first);

        Assert.Equal(1, uncalled.Exit);
        Assert.Equal("POST /contact", uncalled.Output.Trim());
        Assert.Equal(1, check.Exit);
        Assert.Equal("Failure: GET /contact/{id} 200 $.id: A string, where the description says integer.", check.Output.Trim());
        Assert.Equal(0, fits.Exit);
        Assert.Equal(string.Empty, fits.Output.Trim());
    }

    private string Exchanges(string name, params (string Method, string Url, int Status, string Body)[] calls)
    {
        var path = Path.Combine(_folder, name);
        foreach (var call in calls)
        {
            ApiExchangeLog.Append(path, new ApiExchange(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, call.Method, call.Url, call.Status, "application/json", call.Body));
        }

        return path;
    }

    private async Task<(int Exit, string Output)> Run(params string[] args)
    {
        var output = new StringWriter();
        var exit = await BruinetCommand.RunAsync(args, new CliIo(TextReader.Null, output, TextWriter.Null, _folder, Interactive: false));
        return (exit, output.ToString());
    }
}
