namespace Bruinet.Narrator.Tests;

/// <summary>A suite's pronunciations.md: names said once, applied to every line before it is spoken.</summary>
public sealed class PronunciationsTests : IDisposable
{
    private const string Table = """
        # Pronunciations

        How this suite's names are said.

        | Term | Say | IPA |
        | --- | --- | --- |
        | Bruinet | Brew-ee-net | |
        | Todo | | ˈtuːduː |
        | MAUI | Maui | |
        | Bruinet Studio | Brew-ee-net Studio | |
        """;

    private readonly string _suite = Directory.CreateTempSubdirectory("bruinet-pron-").FullName;

    public void Dispose() => Directory.Delete(_suite, recursive: true);

    [Fact]
    public void ATermIsWrappedInASub_WholeWordAndCaseSensitive()
    {
        var pronunciations = Pronunciations.Parse(Table, "p.md");

        Assert.Equal("Welcome to <sub alias=\"Brew-ee-net\">Bruinet</sub>, not bruinet or Bruinets or xBruinet.", pronunciations.Apply("Welcome to Bruinet, not bruinet or Bruinets or xBruinet."));
        Assert.Equal("Tap <sub alias=\"Maui\">MAUI</sub>.", pronunciations.Apply("Tap MAUI."));
    }

    [Fact]
    public void ATermWithOnlyAnIpa_IsAPhoneme()
        => Assert.Equal("The <phoneme alphabet=\"ipa\" ph=\"ˈtuːduː\">Todo</phoneme> app.", Pronunciations.Parse(Table, "p.md").Apply("The Todo app."));

    [Fact]
    public void TheLongestTermWins()
        => Assert.Equal("<sub alias=\"Brew-ee-net Studio\">Bruinet Studio</sub> and <sub alias=\"Brew-ee-net\">Bruinet</sub>.", Pronunciations.Parse(Table, "p.md").Apply("Bruinet Studio and Bruinet."));

    [Fact]
    public void ATermInsideATagTheAuthorWrote_IsLeftAlone()
    {
        var pronunciations = Pronunciations.Parse(Table, "p.md");

        Assert.Equal("Say <sub alias=\"Bru-net\">Bruinet</sub> then <sub alias=\"Brew-ee-net\">Bruinet</sub>.", pronunciations.Apply("Say <sub alias=\"Bru-net\">Bruinet</sub> then Bruinet."));
        Assert.Equal("<phoneme ph=\"x\">Todo</phoneme> <sub alias=\"Brew-ee-net\">Bruinet</sub>", pronunciations.Apply("<phoneme ph=\"x\">Todo</phoneme> Bruinet"));
        Assert.Equal("<break time=\"1s\"/> <sub alias=\"Brew-ee-net\">Bruinet</sub> <emphasis><sub alias=\"Maui\">MAUI</sub></emphasis>", pronunciations.Apply("<break time=\"1s\"/> Bruinet <emphasis>MAUI</emphasis>"));
    }

    [Fact]
    public void ATermInAnAttribute_IsNotTouched()
        => Assert.Equal("<sub alias=\"Bruinet\">B</sub>", Pronunciations.Parse(Table, "p.md").Apply("<sub alias=\"Bruinet\">B</sub>"));

    [Fact]
    public void AWrappedLine_ShowsTheTermInCaptions_AndTheAliasToAVoiceWithoutSsml()
    {
        var line = SpokenLine.Parse(Pronunciations.Parse(Table, "p.md").Apply("Welcome to Bruinet."));

        Assert.Equal("Welcome to Bruinet.", line.Plain);
        Assert.Equal("Welcome to Brew-ee-net.", line.Reduced);
    }

    [Fact]
    public void ALineWithNoTerm_IsReturnedAsItWas()
        => Assert.Equal("Tom & Jerry x < 5.", Pronunciations.Parse(Table, "p.md").Apply("Tom & Jerry x < 5."));

    [Fact]
    public void ATermListedTwice_OrWithNothingToSay_IsVID025_AtItsLine()
    {
        var pronunciations = Pronunciations.Parse("| Term | Say | IPA |\n| --- | --- | --- |\n| Bruinet | Brew-ee-net | |\n| Bruinet | Bru-net | |\n| Nothing | | |\n", "p.md");

        Assert.Equal([(4, "'Bruinet' is listed twice."), (5, "'Nothing' has nothing to say: give it a Say or an IPA.")], pronunciations.Problems);
        Assert.Single(pronunciations.Entries);
        Assert.Equal("Brew-ee-net", pronunciations.Entries[0].Say);
    }

    [Fact]
    public void ThereIsNoneWhenThereIsNoFile_OrNoTerm()
    {
        Assert.Null(Pronunciations.Load(_suite));
        Assert.Null(Pronunciations.Load(null));

        File.WriteAllText(Path.Combine(_suite, Pronunciations.FileName), "# Pronunciations\n\nNothing yet.\n");
        Assert.Null(Pronunciations.Load(_suite));
    }

    [Fact]
    public async Task TheLinesOfAScene_AreSpokenWithTheirTermsWrapped_AndTheHashNoticesAChange()
    {
        File.WriteAllText(Path.Combine(_suite, BddFileFormat.ConfigFileName), "# UAT Config\n");
        Directory.CreateDirectory(Path.Combine(_suite, "Features"));
        File.WriteAllText(Path.Combine(_suite, "Features", "A.feature.md"), "# Feature: A\n\n## Scenario: A.1 Opens\n\n```gherkin\nWhen I tap Sync\n```\n");
        var script = "# Video: V\n\n## Scene: S\nScenario: Features/A.feature.md#A.1-Opens\n\nWelcome to Bruinet.\n\n| Step | Say |\n| --- | --- |\n| When I tap Sync | Tap it in Todo. |\n";
        var path = Path.Combine(_suite, "Features", "v.video.md");

        var without = BddVideoScriptCheck.Check(script, path);
        File.WriteAllText(Path.Combine(_suite, Pronunciations.FileName), Table);
        var with = BddVideoScriptCheck.Check(script, path);
        var plain = new Recorder();
        var clips = await BddSceneClips.SpeakAsync(with.Scenes[0], plain, null, Path.Combine(_suite, "clips"), default);

        Assert.Empty(with.Diagnostics);
        Assert.Equal(["Welcome to Brew-ee-net.", "Tap it in Todo."], plain.Spoken);
        Assert.Equal("Welcome to Bruinet.", clips.Intro!.Text);
        Assert.Equal("Tap it in Todo.", clips.Says.Single().Value.Text);
        Assert.NotEqual(BddKeptScenes.Hash(without.Scenes[0], null, TimeSpan.Zero), BddKeptScenes.Hash(with.Scenes[0], null, TimeSpan.Zero));

        File.WriteAllText(Path.Combine(_suite, Pronunciations.FileName), Table.Replace("Brew-ee-net |", "Bru-ee-net |", StringComparison.Ordinal));
        var changed = BddVideoScriptCheck.Check(script, path);
        Assert.NotEqual(BddKeptScenes.Hash(with.Scenes[0], null, TimeSpan.Zero), BddKeptScenes.Hash(changed.Scenes[0], null, TimeSpan.Zero));
    }

    [Fact]
    public void AScriptWithoutPronunciations_KeepsItsHash()
    {
        File.WriteAllText(Path.Combine(_suite, BddFileFormat.ConfigFileName), "# UAT Config\n");
        Directory.CreateDirectory(Path.Combine(_suite, "Features"));
        File.WriteAllText(Path.Combine(_suite, "Features", "A.feature.md"), "# Feature: A\n\n## Scenario: A.1 Opens\n\n```gherkin\nWhen I tap Sync\n```\n");
        var path = Path.Combine(_suite, "Features", "v.video.md");
        var check = BddVideoScriptCheck.Check("# Video: V\n\n## Scene: S\nScenario: Features/A.feature.md#A.1-Opens\n\n| Step | Say |\n| --- | --- |\n| When I tap Sync | Tap. |\n", path);

        Assert.Null(check.Scenes[0].Pronunciations);
        Assert.Equal(BddKeptScenes.Hash(check.Scenes[0], "v", TimeSpan.Zero), BddKeptScenes.Hash(check.Scenes[0] with { Pronunciations = null }, "v", TimeSpan.Zero));
    }

    [Fact]
    public void TheFilesProblems_AreReportedInTheFile_NotAtTheScriptsLine()
    {
        File.WriteAllText(Path.Combine(_suite, BddFileFormat.ConfigFileName), "# UAT Config\n");
        File.WriteAllText(Path.Combine(_suite, Pronunciations.FileName), "| Term | Say |\n| --- | --- |\n| A | a |\n| A | b |\n");
        Directory.CreateDirectory(Path.Combine(_suite, "Features"));
        var path = Path.Combine(_suite, "Features", "v.video.md");

        var check = BddVideoScriptCheck.Check("# Video: V\n", path);

        var problem = Assert.Single(check.Diagnostics);
        Assert.Equal((BddVideoScriptCheck.PronunciationsCode, BddDiagnosticSeverity.Error, Path.Combine(_suite, Pronunciations.FileName), 4), (problem.Code, problem.Severity, problem.Location.FilePath, problem.Location.LineNumber));
    }

    private sealed class Recorder : ISpeechSynthesizer
    {
        public List<string> Spoken { get; } = [];

        public bool IsAvailable => true;

        public Task<TimeSpan> SynthesizeAsync(string text, string? voice, string wavPath, CancellationToken cancellationToken)
        {
            Spoken.Add(text);
            return Task.FromResult(TimeSpan.FromSeconds(1));
        }
    }
}
