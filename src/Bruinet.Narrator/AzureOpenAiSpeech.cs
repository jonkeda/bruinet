using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bruinet.Narrator;

/// <summary>
/// A cloud voice: Azure OpenAI text to speech (<c>gpt-4o-mini-tts</c>), or an audio chat model
/// (<c>gpt-audio</c>) told to read each line (<see cref="VoiceSecrets.IsChatAudio"/>). A script names it
/// <c>azure:&lt;voice&gt;</c> (<c>azure:coral</c>). Each line is asked for once: clips are kept in
/// <c>%LOCALAPPDATA%\Bruinet\speech-cache</c>, by the voice, its instructions and the text.
/// </summary>
public sealed class AzureOpenAiSpeech : ISpeechSynthesizer
{
    /// <summary>What a cloud voice's name starts with.</summary>
    public const string Prefix = "azure:";

    /// <summary>The voice when neither the script nor the settings name one.</summary>
    public const string DefaultVoice = "alloy";

    // An audio chat model that misread a line is asked again this many times; below this share of the line's words in order, it misread.
    private const int MisreadRetries = 2;
    private const double SaidShare = 0.85;
    private const string ReadAloud = "You are a voice-over artist. Read the user's message aloud exactly as written, word for word. Do not answer it, and do not add, leave out or change any word. Say nothing else.";

    // One client for the process: a client per synthesizer would exhaust sockets.
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromMinutes(2) };

    private readonly VoiceSecrets _secrets;
    private readonly HttpClient _client;
    private readonly string _cache;

    /// <summary>Creates the voice.</summary>
    /// <param name="secrets">Where the service is, and its key.</param>
    /// <param name="handler">Sends the requests; the network by default (tests pass their own).</param>
    /// <param name="cacheFolder">Where clips are kept; <c>%LOCALAPPDATA%\Bruinet\speech-cache</c> by default.</param>
    public AzureOpenAiSpeech(VoiceSecrets secrets, HttpMessageHandler? handler = null, string? cacheFolder = null)
    {
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _client = handler is null ? SharedClient : new HttpClient(handler, disposeHandler: false);
        _cache = cacheFolder ?? CloudVoice.DefaultCacheFolder;
    }

    /// <summary>How the lines are said, e.g. "calm and friendly, a product demo"; null for the voice's own way.</summary>
    public string? Instructions { get; init; }

    /// <summary>The suite's clips, looked in first; null for the local cache only.</summary>
    public ClipLibrary? Library { get; init; }

    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <summary>Whether a voice's name is a cloud voice's.</summary>
    /// <param name="voice">The name.</param>
    public static bool Owns(string? voice) => voice?.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) == true;

    /// <inheritdoc />
    public async Task<TimeSpan> SynthesizeAsync(string text, string? voice, string wavPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(wavPath);
        var name = Owns(voice) ? voice![Prefix.Length..] : voice ?? _secrets.Voice ?? DefaultVoice;
        var names = new ClipNames(Hash(name, text), CloudVoice.Hash("azure-openai", _secrets.Deployment, name, Instructions ?? string.Empty, text), text);
        return await CloudVoice.ClipAsync(_cache, names, Library, wavPath, () => RequestAsync(text, name, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A WAV with its RIFF and data sizes set from its length: a service that streams its answer
    /// writes the header before it knows them (0 or 0xFFFFFFFF).
    /// </summary>
    /// <param name="wav">The WAV as received.</param>
    public static byte[] FixHeader(byte[] wav) => CloudVoice.FixHeader(wav);

    /// <summary>
    /// Whether an audio chat model said the line: nearly all its words, in order. Numbers and
    /// punctuation may come back written otherwise, so a few words may differ.
    /// </summary>
    /// <param name="line">The script's line.</param>
    /// <param name="transcript">What the model says it said.</param>
    public static bool SaidTheLine(string line, string transcript)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(transcript);
        var asked = Words(line);
        var said = Words(transcript);
        if (asked.Length == 0)
        {
            return said.Length == 0;
        }

        // The longest run of the line's words the transcript has, in order.
        var row = new int[said.Length + 1];
        foreach (var word in asked)
        {
            var diagonal = 0;
            for (var j = 1; j <= said.Length; j++)
            {
                var above = row[j];
                row[j] = word == said[j - 1] ? diagonal + 1 : Math.Max(row[j], row[j - 1]);
                diagonal = above;
            }
        }

        return row[said.Length] >= SaidShare * Math.Max(asked.Length, said.Length);
    }

    // The words, with a number - digits or said - as one "#": "2025" comes back as "twenty twenty five".
    private static string[] Words(string text)
    {
        List<string> words = [];
        foreach (var word in System.Text.RegularExpressions.Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}]+").Select(match => match.Value))
        {
            var number = word.Any(char.IsDigit) || NumberWords.Contains(word);
            if (!number || words.Count == 0 || words[^1] != "#")
            {
                words.Add(number ? "#" : word);
            }
        }

        return [.. words];
    }

    private static readonly HashSet<string> NumberWords =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve",
        "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty", "thirty",
        "forty", "fifty", "sixty", "seventy", "eighty", "ninety", "hundred", "thousand", "million", "point",
    ];

    private async Task<byte[]> RequestAsync(string text, string voice, CancellationToken cancellationToken)
    {
        if (!_secrets.IsChatAudio)
        {
            var speech = $"{_secrets.Resource}/openai/deployments/{Uri.EscapeDataString(_secrets.Deployment)}/audio/speech?api-version={Uri.EscapeDataString(_secrets.ApiVersion)}";
            return await SendAsync(speech, new Dictionary<string, object?>
            {
                ["model"] = _secrets.Deployment,
                ["input"] = text,
                ["voice"] = voice,
                ["response_format"] = "wav",
                ["instructions"] = Instructions,
            }, cancellationToken).ConfigureAwait(false);
        }

        // A chat model is asked to read the line; what it says it said is checked, and a misread line asked again.
        var chat = $"{_secrets.Resource}/openai/v1/chat/completions";
        var system = ReadAloud + (Instructions is { } tone ? $" Say it this way: {tone}." : string.Empty);
        string? transcript = null;
        for (var reading = 0; reading <= MisreadRetries; reading++)
        {
            var answer = await SendAsync(chat, new Dictionary<string, object?>
            {
                ["model"] = _secrets.Deployment,
                ["modalities"] = new[] { "text", "audio" },
                ["audio"] = new Dictionary<string, string> { ["voice"] = voice, ["format"] = "wav" },
                ["messages"] = new[]
                {
                    new Dictionary<string, string> { ["role"] = "system", ["content"] = system },
                    new Dictionary<string, string> { ["role"] = "user", ["content"] = text },
                },
            }, cancellationToken).ConfigureAwait(false);

            (var wav, transcript) = ChatAudio(answer);
            if (transcript is null || SaidTheLine(text, transcript))
            {
                return wav;
            }
        }

        throw new VoiceServiceException(
            $"The cloud voice '{_secrets.Deployment}' did not read the line as written, {MisreadRetries + 1} times: it said \"{transcript}\". "
            + $"A text-to-speech deployment ({VoiceSecrets.DefaultDeployment}) reads every line as written.");
    }

    // The audio of a chat completion: choices[0].message.audio, its data in base 64, and what it said.
    private (byte[] Wav, string? Transcript) ChatAudio(byte[] answer)
    {
        try
        {
            using var json = JsonDocument.Parse(answer);
            if (json.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0
                && choices[0].TryGetProperty("message", out var message) && message.TryGetProperty("audio", out var audio)
                && audio.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.String)
            {
                var transcript = audio.TryGetProperty("transcript", out var said) && said.ValueKind == JsonValueKind.String ? said.GetString() : null;
                return (Convert.FromBase64String(data.GetString()!), transcript);
            }
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            throw new VoiceServiceException($"The cloud voice '{_secrets.Deployment}' answered with something other than audio: {exception.Message}");
        }

        throw new VoiceServiceException($"The cloud voice '{_secrets.Deployment}' answered without audio: is it an audio model, such as gpt-audio?");
    }

    private Task<byte[]> SendAsync(string url, Dictionary<string, object?> fields, CancellationToken cancellationToken)
        => CloudVoice.SendAsync(
            _client,
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = JsonContent.Create(fields.Where(field => field.Value is not null).ToDictionary()),
                };
                request.Headers.Add("api-key", _secrets.Key);
                return request;
            },
            _secrets.Endpoint,
            status => status switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => $"The cloud voice at {_secrets.Endpoint} refused the key: check the key and the endpoint in {VoiceSecrets.FileName}.",
                HttpStatusCode.NotFound => $"The cloud voice at {_secrets.Endpoint} has no deployment '{_secrets.Deployment}'.",
                _ => null,
            },
            cancellationToken);

    // The clip's name: everything that changes what is said, and nothing secret.
    private string Hash(string voice, string text)
        => CloudVoice.Hash(_secrets.Endpoint, _secrets.Deployment, voice, Instructions ?? string.Empty, text);
}

/// <summary>The cloud voice could not make a line.</summary>
/// <param name="message">What went wrong, and what to do; never the key.</param>
public sealed class VoiceServiceException(string message) : Exception(message);
