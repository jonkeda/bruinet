namespace Bruinet.Narrator.Tests;

/// <summary>The script check's SSML codes (VID020 to VID024) at the row that holds the markup.</summary>
public sealed class BddVideoScriptMarkupTests : IDisposable
{
    private readonly string _suite = Directory.CreateTempSubdirectory("bruinet-markup-").FullName;

    public BddVideoScriptMarkupTests()
    {
        File.WriteAllText(Path.Combine(_suite, BddFileFormat.ConfigFileName), "# UAT Config\n");
        Directory.CreateDirectory(Path.Combine(_suite, "Features"));
        File.WriteAllText(Path.Combine(_suite, "Features", "A.feature.md"), "# Feature: A\n\n## Scenario: A.1 Opens\n\n```gherkin\nWhen I tap Sync\nThen the list shows \"Buy bread\"\n```\n");
    }

    public void Dispose() => Directory.Delete(_suite, recursive: true);

    [Fact]
    public void APlainScript_HasNoMarkupDiagnostics()
    {
        var check = Check("| When I tap Sync | Tap it & wait: x < 5. |", intro: "Welcome.");

        Assert.Empty(check.Diagnostics);
    }

    [Fact]
    public void WellFormedMarkup_OnAVoiceThatFollowsIt_IsClean()
    {
        var check = Check("| When I tap Sync | Tap it.<break time=\"400ms\"/> Your <emphasis level=\"strong\">todo</emphasis>. |", voice: "Microsoft Zira");

        Assert.Empty(check.Diagnostics);
    }

    [Fact]
    public void AnUnclosedElement_IsVID020_AtItsRow()
    {
        var check = Check("| When I tap Sync | Your <emphasis>todo is saved. |");

        var diagnostic = Assert.Single(check.Diagnostics);
        Assert.Equal((BddVideoScriptCheck.MarkupCode, BddDiagnosticSeverity.Error, 8), (diagnostic.Code, diagnostic.Severity, diagnostic.Location.LineNumber));
        Assert.False(check.Success);
    }

    [Fact]
    public void ATagThatIsNoElement_IsAVID021Warning_SoATypoIsNeverSpoken()
    {
        var check = Check("| When I tap Sync | Tap<brek/> it. |");

        var diagnostic = Assert.Single(check.Diagnostics);
        Assert.Equal((BddVideoScriptCheck.UnknownTagCode, BddDiagnosticSeverity.Warning), (diagnostic.Code, diagnostic.Severity));
        Assert.True(check.Success);
    }

    [Theory]
    [InlineData("<break time=\"2 minutes\"/>")]
    [InlineData("<break time=\"21s\"/>")]
    [InlineData("<break time=\"20001ms\"/>")]
    [InlineData("<break strength=\"huge\"/>")]
    [InlineData("<prosody rate=\"fastish\">x</prosody>")]
    [InlineData("<prosody>x</prosody>")]
    [InlineData("<say-as interpret-as=\"pirate\">3</say-as>")]
    [InlineData("<sub>x</sub>")]
    [InlineData("<phoneme>x</phoneme>")]
    [InlineData("<emphasis level=\"loudest\">x</emphasis>")]
    public void ABadAttribute_IsVID022(string markup)
    {
        var check = Check($"| When I tap Sync | Tap {markup} it. |");

        var diagnostic = Assert.Single(check.Diagnostics);
        Assert.Equal((BddVideoScriptCheck.BadAttributeCode, BddDiagnosticSeverity.Error), (diagnostic.Code, diagnostic.Severity));
    }

    [Theory]
    [InlineData("<break time=\"500ms\"/>")]
    [InlineData("<break time=\"2s\"/>")]
    [InlineData("<break time=\"20s\"/>")]
    [InlineData("<prosody rate=\"slow\" pitch=\"+2st\" volume=\"loud\">x</prosody>")]
    [InlineData("<prosody rate=\"85%\">x</prosody>")]
    [InlineData("<say-as interpret-as=\"digits\">3</say-as>")]
    public void AGoodAttribute_IsClean(string markup)
        => Assert.Empty(Check($"| When I tap Sync | Tap {markup} it. |", voice: "azure-speech-service:en-GB-SoniaNeural").Diagnostics);

    [Fact]
    public void AnElementWhereItMayNotGo_IsVID024()
    {
        var check = Check("| When I tap Sync | <say-as interpret-as=\"cardinal\">3 <emphasis>x</emphasis></say-as> |");

        Assert.Equal(BddVideoScriptCheck.MisplacedElementCode, Assert.Single(check.Diagnostics).Code);
    }

    [Fact]
    public void AnElementTheScriptsVoiceDoesNotFollow_IsAVID023Warning_NamingTheVoice()
    {
        var windows = Check("| When I tap Sync | There are <say-as interpret-as=\"cardinal\">3</say-as> todos. |", voice: "Microsoft Zira");
        var openAi = Check("| When I tap Sync | Tap.<break time=\"1s\"/> |", voice: "azure:coral");
        var speech = Check("| When I tap Sync | There are <say-as interpret-as=\"cardinal\">3</say-as> todos. |", voice: "azure-speech-service:en-GB-Emily:MAI-Voice-2.1");

        var dropped = Assert.Single(windows.Diagnostics);
        Assert.Equal((BddVideoScriptCheck.UnsupportedElementCode, BddDiagnosticSeverity.Warning), (dropped.Code, dropped.Severity));
        Assert.Contains("Microsoft Zira", dropped.Message, StringComparison.Ordinal);
        Assert.Contains("cannot follow markup", Assert.Single(openAi.Diagnostics).Message, StringComparison.Ordinal);
        Assert.Empty(speech.Diagnostics);
    }

    [Fact]
    public void AMaiVoiceDoesNotFollowABreak_AndTheWarningSaysWhatToUseInstead()
    {
        var check = Check("| When I tap Sync | Tap.<break time=\"1s\"/> Then <s>go.</s> |", voice: "azure-speech-service:en-GB-Emily:MAI-Voice-2.1");

        var dropped = Assert.Single(check.Diagnostics);
        Assert.Equal(BddVideoScriptCheck.UnsupportedElementCode, dropped.Code);
        Assert.Contains("<p>", dropped.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithNoVoiceNamed_NothingIsSaidAboutWhatItFollows()
        => Assert.Empty(Check("| When I tap Sync | Tap.<break time=\"1s\"/> |").Diagnostics);

    [Fact]
    public void TheIntroAndOutro_AreCheckedToo_AtTheScenesHeading()
    {
        var check = Check("| When I tap Sync | Tap. |", intro: "Welcome <emphasis>friend.", outro: "Bye<brek/>.");

        Assert.Equal([BddVideoScriptCheck.MarkupCode, BddVideoScriptCheck.UnknownTagCode], check.Diagnostics.Select(diagnostic => diagnostic.Code).Order());
        Assert.All(check.Diagnostics, diagnostic => Assert.Equal(3, diagnostic.Location.LineNumber));
    }

    private BddVideoScriptCheck Check(string rows, string? voice = null, string? intro = null, string? outro = null)
    {
        var header = voice is null ? string.Empty : $"| Field | Value |\n| --- | --- |\n| Voice | {voice} |\n\n";
        var text = $"# Video: V\n\n{header}## Scene: S\nScenario: Features/A.feature.md#A.1-Opens\n\n{(intro is null ? string.Empty : intro + "\n\n")}| Step | Say |\n| --- | --- |\n{rows}\n{(outro is null ? string.Empty : "\n" + outro + "\n")}";
        return BddVideoScriptCheck.Check(text, Path.Combine(_suite, "Features", "v.video.md"));
    }
}
