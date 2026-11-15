using System.Net;
using Bruinet.Bdd.Workspace;
using Xunit;

namespace Bruinet.Bdd.Preview.Tests;

/// <summary>The preview of a video script shows what is said as the captions will: no tags, and a mark where a line has SSML.</summary>
public sealed class BddPreviewMarkupTests : IDisposable
{
    private readonly string _suite = Directory.CreateTempSubdirectory("bruinet-preview-markup-").FullName;

    public void Dispose() => Directory.Delete(_suite, recursive: true);

    [Fact]
    public void ALineWithMarkup_ShowsItsWordsOnly_WithAMarkNamingItsElements()
    {
        File.WriteAllText(Path.Combine(_suite, "bdd.config.md"), "# UAT Config\n");
        Directory.CreateDirectory(Path.Combine(_suite, "Features"));
        File.WriteAllText(Path.Combine(_suite, "Features", "A.feature.md"), "# Feature: A\n\n## Scenario: A.1 Opens\n\n```gherkin\nWhen I tap Sync\n```\n");
        var path = Path.Combine(_suite, "Features", "v.video.md");
        File.WriteAllText(path, "# Video: V\n\n## Scene: S\nScenario: Features/A.feature.md#A.1-Opens\n\nWelcome to <sub alias=\"Brew-ee-net\">Bruinet</sub>.\n\n| Step | Say |\n| --- | --- |\n| When I tap Sync | Tap it.<break time=\"400ms\"/> Done. |\n");

        var html = WebUtility.HtmlDecode(BddPreview.Render(File.ReadAllText(path), path).Html);

        Assert.Contains("<span class=\"uat-video-say\">Tap it. Done. <span class=\"uat-video-markup\" title=\"SSML: break\">SSML</span></span>", html);
        Assert.Contains("Welcome to Bruinet. <span class=\"uat-video-markup\" title=\"SSML: sub\">SSML</span>", html);
        Assert.DoesNotContain("&lt;break", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<break", html, StringComparison.Ordinal);
    }
}
