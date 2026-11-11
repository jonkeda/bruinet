using Bruinet.Cli;
using Bruinet.Mocking.Http;

namespace Bruinet.Cli.Tests.Http;

// bruinet http check, accept and stale (AI11): the gate on recordings that went in unread, and the ones a replay left behind.
public sealed class HttpCommandsTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("bruinet-http-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public async Task Check_FailsOnANewRecording_UntilItIsAccepted()
    {
        var recording = Path.Combine(_folder, "Add a todo.json");
        new HttpRecording { Exchanges = [new HttpExchange { Method = "GET", Url = "https://api.test/todos", ResponseBody = "[]" }] }.Save(recording);

        var unread = await Run("http", "check", "--recordings", _folder);
        var accepted = await Run("http", "accept", "--recordings", _folder);
        var read = await Run("http", "check", "--recordings", _folder, "--json");

        Assert.Equal(1, unread.Exit);
        Assert.Contains("new Add a todo.json", unread.Output, StringComparison.Ordinal);
        Assert.Equal(0, accepted.Exit);
        Assert.Equal(0, read.Exit);
        Assert.Equal("[]", read.Output.Trim());
    }

    [Fact]
    public async Task Stale_NamesARecordingWhoseReplayLeftACallUnused()
    {
        var recording = Path.Combine(_folder, "Sync.json");
        new HttpRecording { Exchanges = [new HttpExchange { Method = "GET", Url = "https://api.test/todos" }, new HttpExchange { Method = "GET", Url = "https://api.test/users", Index = 1 }] }.Save(recording);
        File.WriteAllText(HttpCallLog.PathFor(recording), """{ "Mode": "Replay", "Calls": [], "Unused": [1], "Misses": [] }""");

        var stale = await Run("http", "stale", "--recordings", _folder);

        Assert.Equal(1, stale.Exit);
        Assert.Contains("Sync.json: 1 recorded calls no longer made (exchanges 1)", stale.Output, StringComparison.Ordinal);
    }

    private async Task<(int Exit, string Output)> Run(params string[] args)
    {
        var output = new StringWriter();
        var exit = await BruinetCommand.RunAsync(args, new CliIo(TextReader.Null, output, TextWriter.Null, _folder, Interactive: false));
        return (exit, output.ToString());
    }
}
