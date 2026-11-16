using Xunit;

namespace Bruinet.Bdd.LanguageServer.Tests;

/// <summary>
/// The suite config in the editor, and test sets in a feature file's metadata, over the Todo MAUI
/// suite. Needs the Todo sample built (<c>dotnet build samples/Todo/Bruinet.Samples.Todo.slnx</c>).
/// </summary>
public sealed class ConfigTests : IAsyncLifetime
{
    private static readonly string Config = Path.Combine(TodoPaths.Project, "bdd.config.md");
    private static readonly string Feature = Path.Combine(TodoPaths.Project, "Features", "TOD.01-see-my-todos.feature.md");
    private ServerHarness _server = null!;

    public async Task InitializeAsync() => _server = await ServerHarness.StartAsync(TodoPaths.Todo);

    public async Task DisposeAsync() => await _server.DisposeAsync();

    [Fact]
    public async Task TheTodoConfig_HasNoErrors()
    {
        var uri = await _server.OpenAsync(Config, File.ReadAllText(Config) + "\n");

        var published = await _server.Published.WaitAsync(uri, _ => true);

        Assert.DoesNotContain(published.Diagnostics, diagnostic => diagnostic.Severity == 1);
    }

    [Fact]
    public async Task WhatTheSuiteWouldIgnoreOrReject_IsReported_AtItsLine()
    {
        var text = File.ReadAllText(Config).ReplaceLineEndings("\n")
            .Replace("| Head | maui |", "| Head | desktop |", StringComparison.Ordinal)
            .Replace("| AllowNameInference | true |", "| AllowNameInference | yes |", StringComparison.Ordinal)
            .Replace("| Pages | bin/", "| Page | bin/", StringComparison.Ordinal)
            .Replace("| DefaultTestData | one-todo |", "| DefaultTestData | one-todo |\n| Fixtur | x |", StringComparison.Ordinal)
            + "\n## Runtim\n";
        var lines = text.Split('\n');
        var uri = await _server.OpenAsync(Config, text);

        var published = await _server.Published.WaitAsync(uri, diagnostics => diagnostics.Count >= 5);

        Assert.Contains(published.Diagnostics, d => d.Code == "UATG004" && d.Range.Start.Line == Array.IndexOf(lines, "| Head | desktop |") && d.Message.StartsWith("'desktop' is not a head", StringComparison.Ordinal));
        Assert.Contains(published.Diagnostics, d => d.Code == "UATG004" && d.Message == "AllowNameInference is true or false, not 'yes'.");
        Assert.Contains(published.Diagnostics, d => d.Code == "UATG002" && d.Message.StartsWith("'Page' is not an assembly kind", StringComparison.Ordinal));
        Assert.Contains(published.Diagnostics, d => d.Code == "UATG002" && d.Message.StartsWith("'Fixtur' is not a Runtime field", StringComparison.Ordinal));
        Assert.Contains(published.Diagnostics, d => d.Code == "UATG001" && d.Range.Start.Line == Array.IndexOf(lines, "## Runtim"));
    }

    [Fact]
    public async Task ARuntimeTableWithoutAFixture_IsAnError()
    {
        var text = File.ReadAllText(Config).ReplaceLineEndings("\n").Replace("| Fixture | TodoUatFixture |\n", string.Empty, StringComparison.Ordinal);
        var uri = await _server.OpenAsync(Config, text);

        var published = await _server.Published.WaitAsync(uri, diagnostics => diagnostics.Any(d => d.Code == "UATG003"));

        Assert.Equal(1, published.Diagnostics.Single(d => d.Code == "UATG003").Severity);
    }

    [Fact]
    public async Task CompletionInTheRuntimeTable_OffersTheFields_ThenTheirValues()
    {
        var text = File.ReadAllText(Config).ReplaceLineEndings("\n").Replace("| Head | maui |", "| Hea |\n| Head |  |\n| DefaultTestData |  |", StringComparison.Ordinal);
        var lines = text.Split('\n');
        var uri = await _server.OpenAsync(Config, text);

        var fields = await _server.AtAsync<CompletionList>("textDocument/completion", uri, Array.IndexOf(lines, "| Hea |"), 5);
        var heads = await _server.AtAsync<CompletionList>("textDocument/completion", uri, Array.IndexOf(lines, "| Head |  |"), 9);
        var sets = await _server.AtAsync<CompletionList>("textDocument/completion", uri, Array.IndexOf(lines, "| DefaultTestData |  |"), 20);

        Assert.Contains(fields.Items, item => item.Label == "Head" && item.Detail!.StartsWith("The head this suite drives", StringComparison.Ordinal));
        Assert.Equal(["maui", "web", "mixed", "service", "native-android", "native-ios"], heads.Items.Select(item => item.Label));
        Assert.Contains(sets.Items, item => item.Label == "one-todo");
    }

    [Fact]
    public async Task Hover_OnAField_SaysWhatItDoes()
    {
        var lines = File.ReadAllText(Config).ReplaceLineEndings("\n").Split('\n');
        var uri = await _server.OpenAsync(Config);

        var hover = await _server.AtAsync<Hover>("textDocument/hover", uri, Array.IndexOf(lines, "| Fixture | TodoUatFixture |"), 3);

        Assert.StartsWith("**Fixture**: The fixture type", hover.Contents.Value);
    }

    [Fact]
    public async Task AFeatureFilesTestData_OffersTheTestSets()
    {
        var text = File.ReadAllText(Feature).ReplaceLineEndings("\n").Replace("| TestData | empty |", "| TestData |  |", StringComparison.Ordinal);
        var lines = text.Split('\n');
        var uri = await _server.OpenAsync(Feature, text);

        var sets = await _server.AtAsync<CompletionList>("textDocument/completion", uri, Array.IndexOf(lines, "| TestData |  |"), 13);

        Assert.Contains(sets.Items, item => item.Label == "empty");
        Assert.Contains(sets.Items, item => item.Label == "three-todos");
    }
}
