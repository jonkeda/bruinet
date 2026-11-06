using System.Net;
using System.Text;

namespace Bruinet.Narrator.Tests;

/// <summary>The clip library: a line paid for once, on every machine.</summary>
public sealed class ClipLibraryTests : IDisposable
{
    private const string Key = "secret-key-123";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "Bruinet", "ClipLibraryTests", Guid.NewGuid().ToString("N"));

    public ClipLibraryTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Output => Path.Combine(_root, "scene", "line.wav");

    [Fact]
    public async Task AClipInTheLibrary_IsNotAskedFor_WithAnEmptyCache()
    {
        var first = new FakeService();
        await Speech(first, Library(), "cache-one").SynthesizeAsync("Hello.", "azure:coral", Output, CancellationToken.None);

        var second = new FakeService();
        var library = Library();
        var length = await Speech(second, library, "cache-two").SynthesizeAsync("Hello.", "azure:coral", Output, CancellationToken.None);

        Assert.Single(first.Requests);
        Assert.Empty(second.Requests);
        Assert.Equal(TimeSpan.FromSeconds(1), length);
        Assert.Equal(new BddSpeechUse(1, 0, 0), library.Use);
    }

    [Fact]
    public async Task AClipOnlyInTheCache_IsAddedToTheLibrary_AndNotAskedFor()
    {
        var service = new FakeService();
        await Speech(service, null, "cache").SynthesizeAsync("Hello.", "azure:coral", Output, CancellationToken.None);

        var library = Library();
        await Speech(service, library, "cache").SynthesizeAsync("Hello.", "azure:coral", Output, CancellationToken.None);

        Assert.Single(service.Requests);
        Assert.Single(Clips());
        Assert.Equal(new BddSpeechUse(1, 0, 0), library.Use);
    }

    [Fact]
    public async Task ALineSaidOnAnotherEndpoint_IsFoundInTheLibrary()
    {
        var service = new FakeService();
        await Speech(service, Library(), "cache-one", "https://one.openai.azure.com").SynthesizeAsync("Hello.", "azure:coral", Output, CancellationToken.None);
        await Speech(service, Library(), "cache-two", "https://two.openai.azure.com").SynthesizeAsync("Hello.", "azure:coral", Output, CancellationToken.None);

        Assert.Single(service.Requests);
    }

    [Fact]
    public async Task AnAzureSpeechClip_IsFoundInTheLibrary_FromAnotherRegion()
    {
        var service = new FakeService();
        await new AzureSpeech(new SpeechSecrets("eastus2", Key), service, Path.Combine(_root, "cache-one")) { Library = Library() }.SynthesizeAsync("Hello.", "azure-speech-service:en-GB-Emily", Output, CancellationToken.None);
        await new AzureSpeech(new SpeechSecrets("westeurope", Key), service, Path.Combine(_root, "cache-two")) { Library = Library() }.SynthesizeAsync("Hello.", "azure-speech-service:en-GB-Emily", Output, CancellationToken.None);

        Assert.Single(service.Requests);
    }

    [Fact]
    public async Task AnotherToneOrVoice_IsAnotherClip()
    {
        var service = new FakeService();
        await Speech(service, Library(), "cache").SynthesizeAsync("Hello.", "azure:coral", Output, CancellationToken.None);
        await Speech(service, Library(), "cache", instructions: "excited").SynthesizeAsync("Hello.", "azure:coral", Output, CancellationToken.None);
        await Speech(service, Library(), "cache").SynthesizeAsync("Hello.", "azure:alloy", Output, CancellationToken.None);

        Assert.Equal(3, service.Requests.Count);
    }

    [Fact]
    public async Task AReworded_Line_IsAskedFor_AndTheOthersAreNot()
    {
        var service = new FakeService();
        var voice = Speech(service, Library(), "cache");
        await voice.SynthesizeAsync("One.", "azure:coral", Output, CancellationToken.None);
        await voice.SynthesizeAsync("Two.", "azure:coral", Output, CancellationToken.None);

        var library = Library();
        var again = Speech(service, library, "other-cache");
        await again.SynthesizeAsync("One.", "azure:coral", Output, CancellationToken.None);
        await again.SynthesizeAsync("Two, reworded.", "azure:coral", Output, CancellationToken.None);

        Assert.Equal(3, service.Requests.Count);
        Assert.Equal(new BddSpeechUse(1, 1, "Two, reworded.".Length), library.Use);
    }

    [Fact]
    public async Task ReuseOnly_FailsOnAMissingLine_AndNamesIt_AndAsksNothing()
    {
        var service = new FakeService();
        var library = Library();
        library.Mode = ClipLibraryMode.ReuseOnly;

        var failure = await Assert.ThrowsAsync<VoiceLineMissingException>(
            () => Speech(service, library, "cache").SynthesizeAsync("Not there yet.", "azure:coral", Output, CancellationToken.None));

        Assert.Contains("Not there yet.", failure.Message, StringComparison.Ordinal);
        Assert.Contains(ClipLibrary.ReuseVariable, failure.Message, StringComparison.Ordinal);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task TheLibrary_HoldsNoSecret()
    {
        var service = new FakeService();
        await Speech(service, Library(), "cache").SynthesizeAsync("Hello.", "azure:coral", Output, CancellationToken.None);

        var clip = Assert.Single(Clips());
        Assert.DoesNotContain(Key, Path.GetFileName(clip), StringComparison.Ordinal);
        Assert.DoesNotContain(Key, Encoding.Latin1.GetString(File.ReadAllBytes(clip)), StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(LibraryFolder, "*.part"));
    }

    [Fact]
    public void TheLibrary_IsFoundInTheSource_NotInBin()
    {
        var project = Path.Combine(_root, "Suite");
        var output = Path.Combine(project, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(project, "Suite.csproj"), "<Project />");

        Assert.Equal(Path.Combine(project, "speech"), ClipLibrary.Find(output, NoVariables)!.Folder);
    }

    [Fact]
    public void TheLibrary_FollowsItsVariable_ThenTheSuitesSource()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "Source")).FullName;

        Assert.Equal("named", ClipLibrary.Find(_root, Variables((ClipLibrary.Variable, "named")))!.Folder);
        Assert.Equal(Path.Combine(source, "speech"), ClipLibrary.Find(_root, Variables((BddScenarioSource.SourceDirectoryEnvironmentVariable, source)))!.Folder);
        Assert.Equal(ClipLibraryMode.ReuseOnly, ClipLibrary.Find(_root, Variables((ClipLibrary.ReuseVariable, "1")))!.Mode);
        Assert.Equal(ClipLibraryMode.Ask, ClipLibrary.Find(_root, Variables((ClipLibrary.ReuseVariable, "0")))!.Mode);
    }

    [Fact]
    public void NoFolder_HasNoLibrary() => Assert.Null(ClipLibrary.Find(null, NoVariables));

    [Fact]
    public async Task Count_ListsWhatIsMissing_AndChangesNothing()
    {
        var service = new FakeService();
        var library = Library();
        library.Mode = ClipLibraryMode.Count;

        await Speech(service, library, "cache").SynthesizeAsync("Not there yet.", "azure:coral", Output, CancellationToken.None);

        Assert.Empty(service.Requests);
        Assert.Equal(["Not there yet."], library.Missing);
        Assert.False(Directory.Exists(LibraryFolder));
        Assert.False(File.Exists(Output));
    }

    [Fact]
    public async Task Fill_TakesWhatTheCacheHolds_AndBuysNothing()
    {
        var service = new FakeService();
        await Speech(service, null, "cache").SynthesizeAsync("In the cache.", "azure:coral", Output, CancellationToken.None);

        var library = Library();
        library.Mode = ClipLibraryMode.Fill;
        var voice = Speech(service, library, "cache");
        await voice.SynthesizeAsync("In the cache.", "azure:coral", Output, CancellationToken.None);
        await voice.SynthesizeAsync("Nowhere.", "azure:coral", Output, CancellationToken.None);

        Assert.Single(service.Requests);
        Assert.Single(Clips());
        Assert.Equal(["Nowhere."], library.Missing);
    }

    [Fact]
    public async Task Buy_AsksForWhatIsMissing_WithoutAScene()
    {
        var service = new FakeService();
        var library = Library();
        library.Mode = ClipLibraryMode.Buy;

        await Speech(service, library, "cache").SynthesizeAsync("Nowhere.", "azure:coral", Output, CancellationToken.None);

        Assert.Single(service.Requests);
        Assert.Single(Clips());
        Assert.Equal(new BddSpeechUse(0, 1, "Nowhere.".Length), library.Use);
    }

    [Fact]
    public async Task Prune_DeletesOnlyTheClipsThisPassDidNotReach()
    {
        var service = new FakeService();
        var voice = Speech(service, Library(), "cache");
        await voice.SynthesizeAsync("Kept.", "azure:coral", Output, CancellationToken.None);
        await voice.SynthesizeAsync("Left behind.", "azure:coral", Output, CancellationToken.None);

        var library = Library();
        library.Mode = ClipLibraryMode.Count;
        await Speech(service, library, "cache").SynthesizeAsync("Kept.", "azure:coral", Output, CancellationToken.None);

        Assert.Equal(1, library.Prune());
        Assert.Single(Clips());
    }

    [FfmpegFact]
    public async Task AClipFromTheLibrary_HasTheLengthItWasRecordedWith()
    {
        var wav = Path.Combine(_root, "source.wav");
        await File.WriteAllBytesAsync(wav, Wav(seconds: 2), CancellationToken.None);
        var library = new ClipLibrary(LibraryFolder);

        await library.AddAsync("clip", wav, CancellationToken.None);
        await library.CopyToAsync("clip", Output, CancellationToken.None);

        Assert.True(library.Has("clip"));
        Assert.Equal(2d, Speech_Duration(Output), precision: 1);
        Assert.True(new FileInfo(library.PathOf("clip")).Length < new FileInfo(wav).Length / 4);
    }

    [Fact]
    public void TheUse_SaysItsLine()
    {
        Assert.Equal("Speech: 46 lines, 45 reused, 1 asked (38 characters)", new BddSpeechUse(45, 1, 38).Summary);
        Assert.Equal(46, new BddSpeechUse(45, 1, 38).Lines);
    }

    private string LibraryFolder => Path.Combine(_root, "speech");

    private static double Speech_Duration(string wav) => Bruinet.Narrator.Speech.WavDuration(wav).TotalSeconds;

    private IReadOnlyList<string> Clips() => Directory.Exists(LibraryFolder) ? Directory.GetFiles(LibraryFolder, "*" + ClipLibrary.Extension) : [];

    private ClipLibrary Library() => new(LibraryFolder, new CopyCodec());

    private AzureOpenAiSpeech Speech(FakeService service, ClipLibrary? library, string cache, string endpoint = "https://voice.openai.azure.com", string? instructions = null)
        => new(new VoiceSecrets(endpoint, VoiceSecrets.DefaultDeployment, VoiceSecrets.DefaultApiVersion, Key), service, Path.Combine(_root, cache)) { Instructions = instructions, Library = library };

    private static string? NoVariables(string name) => null;

    private static Func<string, string?> Variables(params (string Name, string Value)[] set)
        => name => set.FirstOrDefault(variable => variable.Name == name).Value;

    // One second of silence at a rate the header says, or as many as asked.
    private static byte[] Wav(int seconds = 1)
    {
        const int Rate = 24000;
        var data = Rate * 2 * seconds;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + data);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(Rate);
        writer.Write(Rate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(data);
        writer.Write(new byte[data]);
        return stream.ToArray();
    }

    // A codec that keeps the WAV as it is, so these tests need no ffmpeg.
    private sealed class CopyCodec : IClipCodec
    {
        public Task EncodeAsync(string wav, string clip, CancellationToken cancellationToken)
        {
            File.Copy(wav, clip, overwrite: true);
            return Task.CompletedTask;
        }

        public Task DecodeAsync(string clip, string wav, CancellationToken cancellationToken)
        {
            File.Copy(clip, wav, overwrite: true);
            return Task.CompletedTask;
        }
    }

    // The service: a second of speech for each request, and what was asked.
    private sealed class FakeService : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Wav()) });
        }
    }
}
