using System.Net;
using Bruinet.Core.Testing;
using Xunit;

namespace Bruinet.Bdd.Preview.Tests;

/// <summary>The preview with a catalog: what each step binds to, and the gate's words when it binds to nothing.</summary>
public sealed class BddPreviewBindingTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "Bruinet", "BddPreviewBindingTests", Guid.NewGuid().ToString("N"), "Features");
    private readonly BddCatalogSet _catalogs = new(BddStepDefinitionRuntime.FromTypes(new object(), typeof(ShelfSteps)).CreateCommandCatalog());

    public BddPreviewBindingTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_folder)!, recursive: true);

    [Fact]
    public void ABoundStep_SaysWhatItBindsTo()
    {
        var html = Render("""
            # Feature: Shelf

            ## Scenario: One

            ```gherkin
            When I shelve "Dune"
            ```
            """);

        Assert.Contains("<li class=\"uat-step uat-bound\" data-line=\"6\" data-binds=\"binds to ", html);
        Assert.Contains("ShelfSteps.Shelve", html);
    }

    [Fact]
    public void AStepThatBindsToNothing_ShowsTheGatesDiagnostic_AtItsLine()
    {
        var html = Render("""
            # Feature: Shelf

            ## Scenario: One

            ```gherkin
            When I shelve "Dune"
            And I polish the shelf
            ```
            """);

        Assert.Contains("data-code=\"BDDB001\" data-line=\"7\"", html);
        Assert.Contains("<li class=\"uat-step uat-bound\" data-line=\"6\"", html);
    }

    [Fact]
    public void ACallableCall_UnfoldsIntoItsBody_WithTheValuesFilledIn()
    {
        File.WriteAllText(Path.Combine(_folder, "Shelf.steps.md"), """
            # Steps: Shelf

            ## Step: the shelf holds {title}

            ```gherkin
            When I shelve <title>
            And I shelve "Emma"
            ```
            """);

        var html = WebUtility.HtmlDecode(Render("""
            # Feature: Shelf

            ## Scenario: One

            ```gherkin
            Given the shelf holds "Dune"
            ```
            """));

        Assert.Contains("calls Step: the shelf holds {title}", html);
        Assert.Contains("<summary>Step: the shelf holds {title} (2 steps)</summary>", html);
        Assert.Contains("data-source=\"Shelf.steps.md:6\"", html);
        Assert.Contains("I shelve <span class=\"uat-value\">\"Dune\"</span>", html);
    }

    [Fact]
    public void AnOutline_ShowsTheFirstRowBound_AndEachRowCanBeOpened()
    {
        var html = WebUtility.HtmlDecode(Render("""
            # Feature: Shelf

            ## Scenario Outline: Shelve a book

            ```gherkin
            When I shelve <title>
            ```

            ### Examples

            | title  |
            | ------ |
            | "Dune" |
            | "Emma" |
            """));

        Assert.Contains("<li class=\"uat-step uat-bound\" data-line=\"6\"", html);
        Assert.Contains("<details class=\"uat-outline-row\" data-row=\"1\"><summary>Shelve a book [1]</summary>", html);
        Assert.Contains("<details class=\"uat-outline-row\" data-row=\"2\"><summary>Shelve a book [2]</summary>", html);
        Assert.Contains("I shelve <span class=\"uat-value\">\"Emma\"</span>", html);
    }

    [Fact]
    public void UnsavedText_IsWhatIsBound()
    {
        var path = Path.Combine(_folder, "shelf.feature.md");
        File.WriteAllText(path, "# Feature: Saved\n");

        var result = BddPreview.Render("""
            # Feature: Unsaved

            ## Scenario: One

            ```gherkin
            When I shelve "Dune"
            ```
            """, path, new BddPreviewOptions { Catalogs = _catalogs });

        Assert.Contains("Unsaved", result.Html);
        Assert.Contains("uat-bound", result.Html);
    }

    private string Render(string text)
        => BddPreview.Render(text, Path.Combine(_folder, "shelf.feature.md"), new BddPreviewOptions { Catalogs = _catalogs }).Html;

    [BddStepDefinitions]
    public sealed class ShelfSteps
    {
        [BddWhen("I shelve {title}")]
        public void Shelve(string title)
        {
        }
    }
}
