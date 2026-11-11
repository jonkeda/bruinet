using Bruinet.Cli.Scenario;
using Bruinet.Narrator;

namespace Bruinet.Cli.Tests.Scenario;

// bruinet voice: a suite's scripts against its clips; nothing here reaches a service.
public sealed class VoiceCommandTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("bruinet-voice-tests-").FullName;

    public VoiceCommandTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Features"));
        File.WriteAllText(Path.Combine(_root, "Suite.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(_root, "uat.config.md"), "# UAT Config\n");
        File.WriteAllText(Path.Combine(_root, "Features", "todo.feature.md"), """
            # Feature: Todo

            ## Scenario: Opening

            ```gherkin
            Given I am on the list
            When I tap Sync
            ```
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task ALineWithNoClip_IsListed_WithWhatItCosts_AndNothingIsWritten()
    {
        var line = "A line no cache holds " + Guid.NewGuid().ToString("N");
        Script(line);

        var (exit, output, _) = await Run(ClipLibraryMode.Count, prune: false);

        Assert.Equal(1, exit);
        Assert.Contains($"no clip: {line}", output, StringComparison.Ordinal);
        Assert.Contains($"1 lines have no clip ({line.Length} characters)", output, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_root, ClipLibrary.FolderName)));
    }

    [Fact]
    public async Task AClipNoScriptSays_IsPruned()
    {
        Script("A line no cache holds " + Guid.NewGuid().ToString("N"));
        var left = Clip("left-behind");

        var (_, output, _) = await Run(ClipLibraryMode.Count, prune: true);

        Assert.Contains("1 clips no script says were deleted", output, StringComparison.Ordinal);
        Assert.False(File.Exists(left));
    }

    [Fact]
    public async Task AScriptWithErrors_StopsThePrune()
    {
        File.WriteAllText(Path.Combine(_root, "Features", "broken.video.md"), "# Video: Broken\n\n## Scene: Nowhere\n\nScenario: Features/none.feature.md#None\n");
        var kept = Clip("kept");

        var (exit, _, errors) = await Run(ClipLibraryMode.Count, prune: true);

        Assert.Equal(1, exit);
        Assert.Contains("Nothing pruned", errors, StringComparison.Ordinal);
        Assert.True(File.Exists(kept));
    }

    [Fact]
    public async Task AScriptInTheOutput_IsNotASecondScript()
    {
        Directory.CreateDirectory(Path.Combine(_root, "bin", "Debug"));
        File.WriteAllText(Path.Combine(_root, "bin", "Debug", "broken.video.md"), "# Video: Broken\n");

        var (exit, _, _) = await Run(ClipLibraryMode.Count, prune: false);

        Assert.Equal(0, exit);
    }

    [Fact]
    public async Task TheIndex_ListsEachLine_BesideItsScript()
    {
        var line = "A line no cache holds " + Guid.NewGuid().ToString("N");
        Script(line);

        await Run(ClipLibraryMode.Count, prune: false, index: true);

        var index = File.ReadAllText(Path.Combine(_root, "Features", VoiceCommand.IndexFile));
        Assert.Contains("## Todo", index, StringComparison.Ordinal);
        Assert.Contains($"| no clip yet (`", index, StringComparison.Ordinal);
        Assert.Contains($"| Opening: When I tap Sync | {line} |", index, StringComparison.Ordinal);
    }

    private void Script(string line) => File.WriteAllText(Path.Combine(_root, "Features", "todo.video.md"), $"""
        # Video: Todo

        | Field | Value |
        | --- | --- |
        | Voice | azure:coral |

        ## Scene: Opening

        Scenario: Features/todo.feature.md#Opening

        | Step | Say |
        | --- | --- |
        | When I tap Sync | {line} |
        """);

    private string Clip(string key)
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, ClipLibrary.FolderName)).FullName;
        var path = Path.Combine(folder, key + ClipLibrary.Extension);
        File.WriteAllText(path, "clip");
        return path;
    }

    private async Task<(int Exit, string Output, string Errors)> Run(ClipLibraryMode mode, bool prune, bool index = false)
    {
        using var output = new StringWriter();
        using var errors = new StringWriter();
        var exit = await VoiceCommand.RunAsync(new CliIo(TextReader.Null, output, errors, _root, false), _root, mode, prune, Variable, CancellationToken.None, index);
        return (exit, output.ToString(), errors.ToString());
    }

    private static string? Variable(string name) => name switch
    {
        VoiceSecrets.EndpointVariable => "https://voice.openai.azure.com",
        VoiceSecrets.KeyVariable => "not-a-key",
        _ => null,
    };
}
