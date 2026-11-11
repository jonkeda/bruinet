using Bruinet.Narrator;
using Bruinet.Narrator.Windows;

namespace Bruinet.Presenter.Tests.Services;

/// <summary>Windows' own voices follow the SSML a script holds: a pause is audible in the clip's length, and no caption holds a tag. Offline; needs a Windows voice.</summary>
public sealed class WindowsSsmlTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("bruinet-ssml-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public async Task ABreakOfASecond_MakesTheClipAboutASecondLonger_AndTheCaptionHasNoTag()
    {
        var voice = new WindowsSpeech();
        if (!voice.IsAvailable)
        {
            return;
        }

        var plain = await Speech.ClipAsync(voice, "Save the todo then sync it now.", null, Path.Combine(_folder, "plain.wav"), default);
        var paused = await Speech.ClipAsync(voice, "Save the todo<break time=\"1s\"/> then sync it now.", null, Path.Combine(_folder, "paused.wav"), default);

        Assert.InRange((paused.Duration - plain.Duration).TotalSeconds, 0.8, 1.4);
        Assert.Equal("Save the todo then sync it now.", paused.Text);
        Assert.DoesNotContain('<', paused.Text);
    }

    [Fact]
    public async Task AnAliasIsSpoken_AndAnElementWindowsLacksKeepsItsWords()
    {
        var voice = new WindowsSpeech();
        if (!voice.IsAvailable)
        {
            return;
        }

        var said = await Speech.ClipAsync(voice, "Welcome to <sub alias=\"Brew ee net\">Bruinet</sub>, in <say-as interpret-as=\"cardinal\">3</say-as> steps.", null, Path.Combine(_folder, "a.wav"), default);

        Assert.Equal("Welcome to Bruinet, in 3 steps.", said.Text);
        Assert.True(said.Duration > TimeSpan.FromSeconds(1));
        Assert.True(File.Exists(said.WavPath));
    }
}
