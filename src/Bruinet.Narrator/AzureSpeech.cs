using System.Net;
using System.Security;
using System.Text;

namespace Bruinet.Narrator;

/// <summary>
/// A cloud voice: Azure AI Speech, a named voice for each language and region, neural or MAI
/// (<c>nl-NL-FennaNeural</c>, <c>en-GB-Emily:MAI-Voice-2.1</c>). A script names it
/// <c>azure-speech-service:&lt;voice&gt;</c>. The line goes as SSML to the service's text-to-speech endpoint, with
/// the key in a header; <see cref="Instructions"/> is a style (<c>narrator</c>, <c>educational</c>), sent as
/// <c>mstts:express-as</c>. Each line is asked for once: clips are kept in the speech cache, by the service,
/// the voice, the style, the sample rate and the text.
/// </summary>
public sealed class AzureSpeech : ISsmlSynthesizer
{
    /// <summary>What an Azure Speech voice's name starts with.</summary>
    public const string Prefix = "azure-speech-service:";

    private const string Namespace = "http://www.w3.org/2001/10/synthesis";
    private const string StyleNamespace = "http://www.w3.org/2001/mstts";

    // One client for the process: a client per synthesizer would exhaust sockets.
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromMinutes(2) };

    private readonly SpeechSecrets _secrets;
    private readonly HttpClient _client;
    private readonly string _cache;

    /// <summary>Creates the voice.</summary>
    /// <param name="secrets">Where the service is, and its key.</param>
    /// <param name="handler">Sends the requests; the network by default (tests pass their own).</param>
    /// <param name="cacheFolder">Where clips are kept; <c>%LOCALAPPDATA%\Bruinet\speech-cache</c> by default.</param>
    public AzureSpeech(SpeechSecrets secrets, HttpMessageHandler? handler = null, string? cacheFolder = null)
    {
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _client = handler is null ? SharedClient : new HttpClient(handler, disposeHandler: false);
        _cache = cacheFolder ?? CloudVoice.DefaultCacheFolder;
    }

    /// <summary>The style the lines are said in, e.g. <c>narrator</c>; null for the voice's own way.</summary>
    public string? Instructions { get; init; }

    /// <summary>The suite's clips, looked in first; null for the local cache only.</summary>
    public ClipLibrary? Library { get; init; }

    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <inheritdoc />
    public IReadOnlySet<string> SupportedElements => SsmlSupport.AzureSpeech;

    /// <summary>Whether a voice's name is an Azure Speech voice's.</summary>
    /// <param name="voice">The name.</param>
    public static bool Owns(string? voice) => voice?.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>The voice a name asks for: what follows the prefix, whole (the MAI model suffix included).</summary>
    /// <param name="voice">The name as a script writes it.</param>
    public static string VoiceOf(string voice)
    {
        ArgumentNullException.ThrowIfNull(voice);
        return Owns(voice) ? voice[Prefix.Length..].Trim() : voice.Trim();
    }

    /// <summary>The language of a voice: its name's first two parts (<c>en-GB</c> of <c>en-GB-Emily:MAI-Voice-2.1</c>).</summary>
    /// <param name="voice">The voice, without the prefix.</param>
    public static string LanguageOf(string voice)
    {
        var parts = voice.Split('-');
        return parts.Length >= 3 ? parts[0] + "-" + parts[1] : "en-US";
    }

    /// <summary>The audio format asked for at a sample rate.</summary>
    /// <param name="sampleRate">24000 or 48000.</param>
    public static string OutputFormat(int sampleRate) => $"riff-{sampleRate / 1000}khz-16bit-mono-pcm";

    /// <summary>The SSML for one line: the text escaped, one voice, and the style when there is one.</summary>
    /// <param name="text">What is said.</param>
    /// <param name="voice">The voice, without the prefix.</param>
    /// <param name="style">The style; null for the voice's own way.</param>
    public static string Ssml(string text, string voice, string? style)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(voice);
        return SsmlOf(SecurityElement.Escape(text), voice, style);
    }

    /// <summary>The SSML document around a line's inner SSML (already escaped), with the style around the line unless the line sets its own.</summary>
    /// <param name="inner">The line's SSML, from <see cref="SpokenLine.ToSsml"/>.</param>
    /// <param name="voice">The voice, without the prefix.</param>
    /// <param name="style">The style; null for the voice's own way.</param>
    /// <param name="ownStyle">Whether the line holds its own <c>mstts:express-as</c>, which then wins for the line.</param>
    public static string SsmlOf(string inner, string voice, string? style, bool ownStyle = false)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(voice);
        var said = string.IsNullOrWhiteSpace(style) || ownStyle ? inner : $"<mstts:express-as style=\"{SecurityElement.Escape(style.Trim())}\">{inner}</mstts:express-as>";
        return $"<speak version=\"1.0\" xmlns=\"{Namespace}\" xmlns:mstts=\"{StyleNamespace}\" xml:lang=\"{LanguageOf(voice)}\"><voice name=\"{SecurityElement.Escape(voice)}\">{said}</voice></speak>";
    }

    /// <inheritdoc />
    public async Task<TimeSpan> SynthesizeAsync(string text, string? voice, string wavPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(wavPath);
        var name = NameOf(voice);
        var style = string.IsNullOrWhiteSpace(Instructions) ? null : Instructions.Trim();
        return await ClipAsync(Ssml(text, name, style), name, style, text, wavPath, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TimeSpan> SynthesizeLineAsync(SpokenLine line, string? voice, string wavPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(wavPath);
        var name = NameOf(voice);
        var style = string.IsNullOrWhiteSpace(Instructions) ? null : Instructions.Trim();
        var inner = line.ToSsml(SsmlSupport.Of(Prefix + name)!);
        var ssml = SsmlOf(inner, name, style, ownStyle: line.ElementsUsed.Contains("mstts:express-as"));
        return await ClipAsync(ssml, name, style, inner, wavPath, cancellationToken).ConfigureAwait(false);
    }

    private string NameOf(string? voice) => voice is null ? _secrets.Voice ?? SpeechSecrets.DefaultVoice : VoiceOf(voice);

    // The clip's name holds the service, the voice, the style, the sample rate and what is said (the SSML of a line with markup).
    private Task<TimeSpan> ClipAsync(string ssml, string voice, string? style, string said, string wavPath, CancellationToken cancellationToken)
    {
        var rate = _secrets.SampleRate.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var names = new ClipNames(
            CloudVoice.Hash("azure-speech-service", _secrets.Host, voice, style ?? string.Empty, rate, said),
            CloudVoice.Hash("azure-speech-service", voice, style ?? string.Empty, rate, said),
            said);
        return CloudVoice.ClipAsync(_cache, names, Library, wavPath, () => RequestAsync(ssml, cancellationToken), cancellationToken);
    }

    private Task<byte[]> RequestAsync(string ssml, CancellationToken cancellationToken)
        => CloudVoice.SendAsync(
            _client,
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, _secrets.SynthesisUrl)
                {
                    Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml"),
                };
                request.Headers.Add("Ocp-Apim-Subscription-Key", _secrets.Key);
                request.Headers.Add("X-Microsoft-OutputFormat", OutputFormat(_secrets.SampleRate));
                request.Headers.TryAddWithoutValidation("User-Agent", "Bruinet.Narrator");
                return request;
            },
            _secrets.Host,
            Explain,
            cancellationToken,
            CloudVoice.IsBusy);

    private string? Explain(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => $"Azure Speech at {_secrets.Host} refused the key: it is wrong, or belongs to another region. Check the key and the endpoint in {VoiceSecrets.FileName}.",
        HttpStatusCode.BadRequest => $"Azure Speech at {_secrets.Host} refused the voice name, the style or the text: check them against the voices and styles the service lists.",
        HttpStatusCode.UnsupportedMediaType => $"Azure Speech at {_secrets.Host} refused the content type.",
        HttpStatusCode.TooManyRequests or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable
            => $"Azure Speech at {_secrets.Host} is busy: it was asked {CloudVoice.BusyRetries + 1} times.",
        _ => null,
    };
}
