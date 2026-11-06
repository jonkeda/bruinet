using System.Reflection;

namespace Bruinet.Narrator;

/// <summary>Turns text into speech, as a WAV file.</summary>
public interface ISpeechSynthesizer
{
    /// <summary>Whether this synthesizer works on this machine.</summary>
    bool IsAvailable { get; }

    /// <summary>Speaks <paramref name="text"/> into <paramref name="wavPath"/>.</summary>
    /// <param name="text">What to say.</param>
    /// <param name="voice">The voice, by (part of) its name; null for the default voice.</param>
    /// <param name="wavPath">The WAV file to write.</param>
    /// <param name="cancellationToken">Cancels the synthesis.</param>
    /// <returns>How long the speech lasts.</returns>
    Task<TimeSpan> SynthesizeAsync(string text, string? voice, string wavPath, CancellationToken cancellationToken);
}

/// <summary>A synthesizer that follows SSML: a line with markup reaches it as a <see cref="SpokenLine"/>, rendered for the elements it supports.</summary>
public interface ISsmlSynthesizer : ISpeechSynthesizer
{
    /// <summary>The SSML elements this synthesizer follows (<see cref="SsmlSupport"/>); the others are dropped, their words kept.</summary>
    IReadOnlySet<string> SupportedElements { get; }

    /// <summary>Speaks a line with markup into <paramref name="wavPath"/>; the synthesizer builds the SSML document around <see cref="SpokenLine.ToSsml"/>.</summary>
    /// <param name="line">What to say.</param>
    /// <param name="voice">The voice, by (part of) its name; null for the default voice.</param>
    /// <param name="wavPath">The WAV file to write.</param>
    /// <param name="cancellationToken">Cancels the synthesis.</param>
    /// <returns>How long the speech lasts.</returns>
    Task<TimeSpan> SynthesizeLineAsync(SpokenLine line, string? voice, string wavPath, CancellationToken cancellationToken);
}

/// <summary>Declares a speech synthesizer that <see cref="Speech.Discover"/> offers; it needs a public parameterless constructor.</summary>
/// <param name="synthesizerType">The synthesizer's type, an <see cref="ISpeechSynthesizer"/>.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class SpeechSynthesizerAttribute(Type synthesizerType) : Attribute
{
    /// <summary>The synthesizer's type.</summary>
    public Type SynthesizerType { get; } = synthesizerType;
}

/// <summary>A spoken line, made before the run so its length is known.</summary>
/// <param name="Text">What is said, as the captions show it: the line without its markup (<see cref="SpokenLine.Plain"/>).</param>
/// <param name="WavPath">The speech; null when no voice could make it (captions only).</param>
/// <param name="Duration">How long it lasts.</param>
public sealed record SpeechClip(string Text, string? WavPath, TimeSpan Duration);

/// <summary>Finds a synthesizer, and makes clips with it.</summary>
public static class Speech
{
    // The platform packages that may bring a voice, loaded when the run has them (RecordingPackages).
    private static readonly string[] KnownPackages = ["Bruinet.Narrator.Windows"];

    // What an estimate assumes when there is no voice: about 150 words a minute.
    private static readonly TimeSpan PerWord = TimeSpan.FromMilliseconds(400);

    /// <summary>The first available synthesizer the platform packages declare; null when there is none.</summary>
    public static ISpeechSynthesizer? Discover()
    {
        Bruinet.Recording.RecordingPackages.Load(KnownPackages);
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetCustomAttributes<SpeechSynthesizerAttribute>())
            .Select(attribute => (ISpeechSynthesizer)Activator.CreateInstance(attribute.SynthesizerType)!)
            .FirstOrDefault(synthesizer => synthesizer.IsAvailable);
    }

    /// <summary>
    /// The synthesizer that owns a voice: Azure Speech (<c>azure-speech-service:en-GB-Emily:MAI-Voice-2.1</c>) or the Azure
    /// OpenAI voice (<c>azure:coral</c>) when its settings are found (<see cref="SpeechSecrets"/>,
    /// <see cref="VoiceSecrets"/>), otherwise a platform voice (<see cref="Discover()"/>). With no voice named,
    /// Azure OpenAI comes first when it is set up, then Azure Speech. Null when there is no voice at all.
    /// </summary>
    /// <param name="voice">The voice; null for the default.</param>
    /// <param name="instructions">How a cloud voice says its lines (Azure Speech: a style); null for its own way.</param>
    /// <param name="folder">The suite's folder, where the cloud voice's settings are looked for first.</param>
    /// <param name="read">Reads an environment variable; the process's by default.</param>
    /// <param name="home">The user's folder; <c>%USERPROFILE%</c> by default.</param>
    /// <param name="library">The suite's clips, which a cloud voice looks in first; null for its local cache only.</param>
    /// <exception cref="VoiceNotConfiguredException">A cloud voice is named, and no settings were found for it.</exception>
    public static ISpeechSynthesizer? Find(string? voice, string? instructions = null, string? folder = null, Func<string, string?>? read = null, string? home = null, ClipLibrary? library = null)
    {
        // A named cloud voice never quietly becomes another voice: the scene would differ for the wrong reason.
        if (AzureSpeech.Owns(voice))
        {
            return SpeechSecrets.Find(folder, read, home) is { } named
                ? new AzureSpeech(named) { Instructions = instructions, Library = library }
                : throw VoiceNotConfiguredException.ForSpeech(voice!, VoiceSecrets.Places(folder, home));
        }

        var secrets = voice is null || AzureOpenAiSpeech.Owns(voice) ? VoiceSecrets.Find(folder, read, home) : null;
        if (secrets is not null)
        {
            return new AzureOpenAiSpeech(secrets) { Instructions = instructions, Library = library };
        }

        if (AzureOpenAiSpeech.Owns(voice))
        {
            throw new VoiceNotConfiguredException(voice!, VoiceSecrets.Places(folder, home));
        }

        return voice is null && SpeechSecrets.Find(folder, read, home) is { } only
            ? new AzureSpeech(only) { Instructions = instructions, Library = library }
            : Discover();
    }

    /// <summary>
    /// Makes a clip: the voice's WAV and its length, or with no synthesizer, no WAV and a length
    /// estimated from the words, so the pace and the captions still work.
    /// </summary>
    /// <param name="synthesizer">The voice; null for none.</param>
    /// <param name="text">What is said.</param>
    /// <param name="voice">The voice's name; null for the default.</param>
    /// <param name="wavPath">Where the WAV goes.</param>
    /// <param name="cancellationToken">Cancels the synthesis.</param>
    public static async Task<SpeechClip> ClipAsync(ISpeechSynthesizer? synthesizer, string text, string? voice, string wavPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);

        // A line may hold SSML: captions and the estimate take its words; a voice that follows markup gets it, any other the reduced line.
        var line = SpokenLine.Parse(text);
        if (synthesizer is null)
        {
            return new SpeechClip(line.Plain, null, Estimate(line.Plain));
        }

        var duration = line.HasMarkup && synthesizer is ISsmlSynthesizer ssml
            ? await ssml.SynthesizeLineAsync(line, voice, wavPath, cancellationToken).ConfigureAwait(false)
            : await synthesizer.SynthesizeAsync(line.Reduced, voice, wavPath, cancellationToken).ConfigureAwait(false);
        return new SpeechClip(line.Plain, wavPath, duration);
    }

    /// <summary>How long <paramref name="text"/> takes to say, estimated from its words.</summary>
    /// <param name="text">What is said.</param>
    public static TimeSpan Estimate(string text)
        => PerWord * Math.Max(1, text.Split((char[])[' ', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries).Length);

    /// <summary>How long a PCM WAV file plays, from its header.</summary>
    /// <param name="path">The WAV file.</param>
    public static TimeSpan WavDuration(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        if (new string(reader.ReadChars(4)) != "RIFF")
        {
            throw new InvalidDataException($"{path} is not a WAV file.");
        }

        reader.ReadInt32();
        if (new string(reader.ReadChars(4)) != "WAVE")
        {
            throw new InvalidDataException($"{path} is not a WAV file.");
        }

        var bytesPerSecond = 0;
        while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
        {
            var chunk = new string(reader.ReadChars(4));
            var size = reader.ReadInt32();
            if (chunk == "fmt ")
            {
                reader.ReadInt16();
                reader.ReadInt16();
                reader.ReadInt32();
                bytesPerSecond = reader.ReadInt32();
                reader.BaseStream.Seek(size - 12, SeekOrigin.Current);
            }
            else if (chunk == "data")
            {
                return bytesPerSecond == 0
                    ? throw new InvalidDataException($"{path} has no format before its data.")
                    : TimeSpan.FromSeconds((double)size / bytesPerSecond);
            }
            else
            {
                reader.BaseStream.Seek(size + (size & 1), SeekOrigin.Current);
            }
        }

        throw new InvalidDataException($"{path} has no data.");
    }
}
