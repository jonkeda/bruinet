using StreamJsonRpc;
using Xunit;

namespace Bruinet.Bdd.LanguageServer.Tests;

/// <summary>Quick fixes and refactorings, on a built Shelf suite whose steps are this assembly's <see cref="ShelfSteps"/>.</summary>
public sealed class ActionTests : IAsyncLifetime
{
    private readonly string _solution = Path.Combine(Path.GetTempPath(), "Bruinet", "ActionTests", Guid.NewGuid().ToString("N"));
    private ServerHarness _server = null!;

    private string Shelf => Path.Combine(_solution, "Shelf.Steps");

    private string Feature => Path.Combine(Shelf, "Features", "shelf.feature.md");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.Combine(Shelf, "Features"));
        var bin = Directory.CreateDirectory(Path.Combine(Shelf, "bin", "Debug", "net10.0")).FullName;
        var assembly = typeof(ActionTests).Assembly.Location;
        File.Copy(assembly, Path.Combine(bin, "Shelf.Steps.dll"));
        File.Copy(Path.ChangeExtension(assembly, ".pdb"), Path.Combine(bin, "Shelf.Steps.pdb"));
        File.WriteAllText(Path.Combine(Shelf, "bdd.config.md"), $"""
            # BDD Config

            ## Runtime

            | Field | Value |
            | --- | --- |
            | Fixture | {nameof(ShelfFixture)} |

            ## Assemblies

            | Kind | Assembly |
            | --- | --- |
            | Steps | bin/Debug/net10.0/Shelf.Steps.dll |
            """);
        File.WriteAllText(Feature, "# Feature: Shelf\n");
        _server = await ServerHarness.StartAsync(_solution);
    }

    public async Task DisposeAsync()
    {
        await _server.DisposeAsync();
        try
        {
            Directory.Delete(_solution, recursive: true);
        }
        catch (IOException)
        {
            // A load context not collected yet; the temp folder is cleaned by the OS.
        }
    }

    [Fact]
    public async Task AStepThatBindsToNothing_GetsCreateStepDefinition_InTheStepsClass()
    {
        var uri = await OpenAsync("When I dust the shelf \"Dune\" 3 times");

        var create = Assert.Single(await ActionsAsync(uri, 5, 5), action => action.Title == "Create step definition in ShelfSteps");

        var (file, edit) = Assert.Single(create.Edit.Changes!.SelectMany(change => change.Value.Select(edit => (change.Key, edit))));
        Assert.EndsWith("LanguageServerTests.cs", new Uri(file).LocalPath);
        Assert.Contains("[BddWhen(\"I dust the shelf {text} {number} times\")]", edit.NewText);
        Assert.Contains("public void IDustTheShelfTimes(string text, int number)", edit.NewText);
        Assert.Equal("}", File.ReadAllLines(new Uri(file).LocalPath)[edit.Range.Start.Line].Trim());
        Assert.Equal("UATB001", Assert.Single(create.Diagnostics!).Code);
    }

    [Fact]
    public async Task ADateWithOneRightAnswer_GetsItsFix()
    {
        var uri = await OpenAsync("When I lend \"Dune\" until 2026-05-12");

        var fix = Assert.Single(await ActionsAsync(uri, 5, 5), action => action.Kind == "quickfix" && action.Title.StartsWith("Write ", StringComparison.Ordinal));

        var edit = Assert.Single(fix.Edit.Changes![uri]);
        Assert.Equal("12-May-2026", edit.NewText);
        Assert.Equal(new Range(new Position(5, 25), new Position(5, 35)), edit.Range);
        Assert.True(fix.IsPreferred);
    }

    [Fact]
    public async Task SelectedSteps_CanBeExtracted_ToAStepOfTheFile()
    {
        var uri = await OpenAsync("When I shelve \"Dune\"\nWhen I shelve \"Emma\"");

        var extract = Assert.Single(await ActionsAsync(uri, 5, 6), action => action.Kind == "refactor.extract");

        var edits = extract.Edit.Changes![uri];
        Assert.Equal("When the extracted steps", edits[0].NewText);
        Assert.Equal(new Range(new Position(5, 0), new Position(6, 20)), edits[0].Range);
        Assert.Contains("## Step: the extracted steps\n\n```gherkin\nWhen I shelve \"Dune\"\nWhen I shelve \"Emma\"\n```", edits[1].NewText);
    }

    [Fact]
    public async Task ACall_CanBeInlined_WithItsValues()
    {
        var uri = await OpenAsync("When I tidy \"top\"", Callable);

        var inline = Assert.Single(await ActionsAsync(uri, 5, 5), action => action.Kind == "refactor.inline");

        var edit = Assert.Single(inline.Edit.Changes![uri]);
        Assert.Equal(new Range(new Position(5, 0), new Position(5, 17)), edit.Range);
        Assert.Equal("When I shelve \"top\"", edit.NewText);
    }

    [Fact]
    public async Task ACallableStep_IsRenamed_WithEveryCall_KeepingItsValues()
    {
        var uri = await OpenAsync("When I tidy \"top\"", Callable);

        var prepared = await _server.AtAsync<PrepareRenameResult>("textDocument/prepareRename", uri, 8, 10);
        var edit = await _server.Client.InvokeWithParameterObjectAsync<WorkspaceEdit>("textDocument/rename", new { textDocument = new { uri }, position = new { line = 8, character = 10 }, newName = "I tidy up {shelf}" });

        Assert.Equal("I tidy {shelf}", prepared.Placeholder);
        var edits = edit.Changes![uri].OrderBy(change => change.Range.Start.Line).ToList();
        Assert.Equal(["I tidy up \"top\"", "I tidy up {shelf}"], edits.Select(change => change.NewText));
        Assert.Equal(5, edits[0].Range.Start.Line);
        Assert.Equal(8, edits[1].Range.Start.Line);
    }

    [Fact]
    public async Task ARename_ThatDropsAPlaceholder_IsRefused()
    {
        var uri = await OpenAsync("When I tidy \"top\"", Callable);

        var refused = await Assert.ThrowsAsync<RemoteInvocationException>(() =>
            _server.Client.InvokeWithParameterObjectAsync<WorkspaceEdit>("textDocument/rename", new { textDocument = new { uri }, position = new { line = 8, character = 10 }, newName = "I tidy up" }));

        Assert.Contains("must keep the placeholders {shelf}", refused.Message);
    }

    [Fact]
    public async Task OldHeadings_AreRewritten()
    {
        var uri = await _server.OpenAsync(Feature, "# UAT: Shelf\n");

        var rewrite = Assert.Single(await ActionsAsync(uri, 0, 0), action => action.Title.StartsWith("Rewrite", StringComparison.Ordinal));

        Assert.StartsWith("# Feature: Shelf", Assert.Single(rewrite.Edit.Changes![uri]).NewText);
    }

    [Fact]
    public async Task BareSteps_ArePutInAGherkinBlock()
    {
        var uri = await _server.OpenAsync(Feature, "# Feature: Shelf\n\n## Scenario: One\n\nWhen I shelve \"Dune\"\n");

        var wrap = Assert.Single(await ActionsAsync(uri, 2, 2), action => action.Title == "Put the steps in ```gherkin blocks");

        Assert.Equal("# Feature: Shelf\n\n## Scenario: One\n\n```gherkin\nWhen I shelve \"Dune\"\n```\n", Assert.Single(wrap.Edit!.Changes![uri]).NewText);
    }

    private const string Callable = "\n\n## Step: I tidy {shelf}\n\n```gherkin\nWhen I shelve <shelf>\n```\n";

    // The feature file with one scenario whose steps start at line 5 (0-based).
    private Task<string> OpenAsync(string steps, string after = "")
        => _server.OpenAsync(Feature, $"# Feature: Shelf\n\n## Scenario: One\n\n```gherkin\n{steps}\n```{after}");

    private Task<IReadOnlyList<CodeAction>> ActionsAsync(string uri, int first, int last)
        => _server.Client.InvokeWithParameterObjectAsync<IReadOnlyList<CodeAction>>("textDocument/codeAction", new
        {
            textDocument = new { uri },
            range = new { start = new { line = first, character = 0 }, end = new { line = last, character = 1 } },
            context = new { diagnostics = Array.Empty<object>() },
        });
}
