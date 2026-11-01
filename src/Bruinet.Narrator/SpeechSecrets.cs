using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bruinet.Narrator;

/// <summary>
/// Where Azure AI Speech (<see cref="AzureSpeech"/>) is, and its key. The <c>azureSpeech</c> object of the same
/// <see cref="VoiceSecrets.FileName"/> the Azure OpenAI voice uses (its <c>azureFoundry</c> object stays that voice's),
/// found in the same places; <c>BRUINET_SPEECH_*</c> variables win over the file. The key is never written
/// anywhere but the request's header.
/// </summary>
/// <param name="Endpoint">A region (<c>eastus2</c>), the resource's URL, or a URL copied with a path.</param>
/// <param name="Key">The resource's key.</param>
public sealed record SpeechSecrets(string Endpoint, string Key)
{
    /// <summary>The key, for a build agent's secret store.</summary>
    public const string KeyVariable = "BRUINET_SPEECH_KEY";

    /// <summary>The endpoint, over the file's.</summary>
    public const string EndpointVariable = "BRUINET_SPEECH_ENDPOINT";

    /// <summary>The default voice, over the file's.</summary>
    public const string VoiceVariable = "BRUINET_SPEECH_VOICE";

    /// <summary>The sample rate, over the file's.</summary>
    public const string SampleRateVariable = "BRUINET_SPEECH_SAMPLE_RATE";

    /// <summary>The voice when neither the script nor the settings name one: Emily, a MAI voice (preview).</summary>
    public const string DefaultVoice = "en-GB-Emily:MAI-Voice-2.1";

    /// <summary>The sample rate when none is set; the Azure OpenAI voice's, so one shape reaches the composer.</summary>
    public const int DefaultSampleRate = 24000;

    /// <summary>The other sample rate.</summary>
    public const int HighSampleRate = 48000;

    /// <summary>The name of the file's object.</summary>
    internal const string Section = "azureSpeech";

    /// <summary>The voice when a script names none, e.g. <c>nl-NL-FennaNeural</c>; null for <see cref="DefaultVoice"/>.</summary>
    public string? Voice { get; init; }

    /// <summary>The audio's sample rate: <see cref="DefaultSampleRate"/> or <see cref="HighSampleRate"/>.</summary>
    public int SampleRate { get; init; } = DefaultSampleRate;

    /// <summary>The service's host, with its scheme: the region's text-to-speech host, or the resource's.</summary>
    public string Host => HostOf(Endpoint);

    /// <summary>Where a line is sent.</summary>
    public string SynthesisUrl => Host + "/cognitiveservices/v1";

    /// <summary>Where the voices are listed.</summary>
    public string VoicesUrl => Host + (IsRegional(Host) ? string.Empty : "/tts") + "/cognitiveservices/voices/list";

    /// <summary>The settings without the key, so they can be shown.</summary>
    public override string ToString() => $"{Endpoint}, voice {Voice ?? DefaultVoice}, {SampleRate} Hz, key set";

    /// <summary>The host an endpoint names: a region (<c>eastus2</c>), or a regional <c>*.api.cognitive.microsoft.com</c> URL, is made into its text-to-speech host; any other URL is cut to its scheme and host.</summary>
    /// <param name="endpoint">The endpoint as given.</param>
    public static string HostOf(string endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var text = endpoint.Trim();
        if (text.Length > 0 && text.All(character => char.IsAsciiLetterOrDigit(character)))
        {
            return $"https://{text.ToLowerInvariant()}.tts.speech.microsoft.com";
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return text.TrimEnd('/');
        }

        // The regional endpoint of a Cognitive Services key (westeurope.api.cognitive.microsoft.com) is not a text-to-speech host: its region's is.
        const string Regional = ".api.cognitive.microsoft.com";
        return uri.Host.EndsWith(Regional, StringComparison.OrdinalIgnoreCase) && uri.Host.Length > Regional.Length && !uri.Host[..^Regional.Length].Contains('.', StringComparison.Ordinal)
            ? $"https://{uri.Host[..^Regional.Length].ToLowerInvariant()}.tts.speech.microsoft.com"
            : $"{uri.Scheme}://{uri.Authority}";
    }

    /// <summary>Why an endpoint cannot be used, or null when it can.</summary>
    /// <param name="endpoint">The endpoint as given.</param>
    public static string? EndpointProblem(string? endpoint)
    {
        var text = endpoint?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return "The endpoint is needed: a region such as eastus2, or https://<resource>.cognitiveservices.azure.com.";
        }

        if (text.All(character => char.IsAsciiLetterOrDigit(character)))
        {
            return null;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return "The endpoint is a region such as eastus2, or a URL such as https://<resource>.cognitiveservices.azure.com.";
        }

        return uri.Scheme == "https" ? null : $"The endpoint must start with https://, not {uri.Scheme}://.";
    }

    /// <summary>The sample rates a voice is asked for; anything else is refused by <see cref="SampleRateProblem"/>.</summary>
    /// <param name="sampleRate">The rate.</param>
    public static string? SampleRateProblem(int sampleRate)
        => sampleRate is DefaultSampleRate or HighSampleRate ? null : $"The sample rate is {DefaultSampleRate} or {HighSampleRate}, not {sampleRate}.";

    /// <summary>The settings, or null when no endpoint and key are found (a half-filled section is not enough).</summary>
    /// <param name="folder">The suite's folder; null to look only in the environment and the user's folder.</param>
    /// <param name="read">Reads an environment variable; the process's by default.</param>
    /// <param name="home">The user's folder; <c>%USERPROFILE%</c> by default.</param>
    /// <exception cref="InvalidDataException">The sample rate is neither 24000 nor 48000.</exception>
    public static SpeechSecrets? Find(string? folder, Func<string, string?>? read = null, string? home = null)
    {
        read ??= Environment.GetEnvironmentVariable;
        var file = VoiceFile.Places(folder, home).FirstOrDefault(File.Exists) is { } path ? Read(path) : null;
        var endpoint = Value(read(EndpointVariable)) ?? file?.Endpoint;
        var key = Value(read(KeyVariable)) ?? file?.Key;
        if (endpoint is null || key is null)
        {
            return null;
        }

        return new SpeechSecrets(endpoint, key)
        {
            Voice = Value(read(VoiceVariable)) ?? file?.Voice,
            SampleRate = Rate(Value(read(SampleRateVariable)) ?? file?.SampleRate),
        };
    }

    /// <summary>What is set up, and from where, without the key: for a settings page.</summary>
    /// <param name="folder">The suite's folder; null to look only in the environment and the user's folder.</param>
    /// <param name="read">Reads an environment variable; the process's by default.</param>
    /// <param name="home">The user's folder; <c>%USERPROFILE%</c> by default.</param>
    public static SpeechSecretsDescription Describe(string? folder, Func<string, string?>? read = null, string? home = null)
    {
        read ??= Environment.GetEnvironmentVariable;
        var path = VoiceFile.Places(folder, home).FirstOrDefault(File.Exists);
        var file = path is null ? null : Read(path);
        string[] variables = [KeyVariable, EndpointVariable, VoiceVariable, SampleRateVariable];
        return new SpeechSecretsDescription(
            Value(read(EndpointVariable)) ?? file?.Endpoint,
            Value(read(VoiceVariable)) ?? file?.Voice,
            Rate(Value(read(SampleRateVariable)) ?? file?.SampleRate),
            Value(read(KeyVariable)) is not null || file?.Key is not null,
            file is null ? null : path,
            variables.Where(variable => Value(read(variable)) is not null).ToList());
    }

    /// <summary>
    /// Writes the <c>azureSpeech</c> object, keeping the rest of the file (the Azure OpenAI voice's).
    /// An empty <paramref name="key"/> keeps the key the file has.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="endpoint">A region or the resource's URL.</param>
    /// <param name="key">The key; empty to keep the saved one.</param>
    /// <param name="voice">The default voice; null for none.</param>
    /// <param name="sampleRate">24000 or 48000.</param>
    /// <exception cref="InvalidOperationException">No key is given and the file has none, or the endpoint or the sample rate cannot be used.</exception>
    public static void Save(string path, string endpoint, string? key, string? voice, int sampleRate = DefaultSampleRate)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        var problem = EndpointProblem(endpoint) ?? SampleRateProblem(sampleRate);
        if (problem is not null)
        {
            throw new InvalidOperationException(problem);
        }

        var root = VoiceFile.ReadRoot(path);
        var kept = Value(key) ?? Read(path)?.Key
            ?? throw new InvalidOperationException("Azure Speech needs a key: none was given, and there is none saved.");
        var section = new JsonObject { ["endpoint"] = endpoint.Trim(), ["key"] = kept };
        if (Value(voice) is { } named)
        {
            section["voice"] = named;
        }

        if (sampleRate != DefaultSampleRate)
        {
            section["sampleRate"] = sampleRate;
        }

        root[Section] = section;
        VoiceFile.Write(path, root);
    }

    /// <summary>Removes the <c>azureSpeech</c> object, keeping the rest of the file; the file goes when nothing else is in it.</summary>
    /// <param name="path">The file.</param>
    public static void Delete(string path) => VoiceFile.RemoveSection(path, Section);

    private static int Rate(string? text)
    {
        if (text is null)
        {
            return DefaultSampleRate;
        }

        return int.TryParse(text, out var rate) && SampleRateProblem(rate) is null
            ? rate
            : throw new InvalidDataException($"The sample rate is {DefaultSampleRate} or {HighSampleRate}, not '{text}'.");
    }

    private static string? Value(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static bool IsRegional(string host) => host.EndsWith(".tts.speech.microsoft.com", StringComparison.OrdinalIgnoreCase);

    // The azureSpeech object's values; null when the file has none.
    private static SectionValues? Read(string path)
        => VoiceFile.ReadSection(path, Section) is { } text
            ? new SectionValues(text("endpoint"), text("key"), text("voice"), text("sampleRate"))
            : null;

    private sealed record SectionValues(string? Endpoint, string? Key, string? Voice, string? SampleRate);
}

/// <summary>What Azure Speech's settings are, and where they come from; never the key itself.</summary>
/// <param name="Endpoint">The endpoint; null when none is set.</param>
/// <param name="Voice">The default voice; null for <see cref="SpeechSecrets.DefaultVoice"/>.</param>
/// <param name="SampleRate">The sample rate.</param>
/// <param name="HasKey">Whether a key is set.</param>
/// <param name="File">The file with an <c>azureSpeech</c> object; null when there is none.</param>
/// <param name="FromEnvironment">The environment variables that are set, over the file.</param>
public sealed record SpeechSecretsDescription(string? Endpoint, string? Voice, int SampleRate, bool HasKey, string? File, IReadOnlyList<string> FromEnvironment)
{
    /// <summary>Whether Azure Speech can be used: an endpoint and a key.</summary>
    public bool IsSetUp => Endpoint is not null && HasKey;
}

/// <summary>The one file, <see cref="VoiceSecrets.FileName"/>, that holds both cloud voices' settings, an object each.</summary>
internal static class VoiceFile
{
    internal static IReadOnlyList<string> Places(string? folder, string? home) => VoiceSecrets.Places(folder, home);

    /// <summary>The file's fields; empty when there is no file.</summary>
    internal static JsonObject ReadRoot(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject ?? [];
        }
        catch (JsonException exception)
        {
            // The message names the file and the position only: the parser does not quote the text,
            // and the parser's exception is left out because its message may.
#pragma warning disable MA0054
            throw new InvalidDataException($"{path} is not valid JSON (line {exception.LineNumber + 1}): see voice.secrets.example.json for its shape.");
#pragma warning restore MA0054
        }
    }

    /// <summary>Reads the fields of the object <paramref name="name"/>, trimmed, null when blank; null when the file has no such object.</summary>
    internal static Func<string, string?>? ReadSection(string path, string name)
    {
        if (ReadRoot(path)[name] is not JsonObject section)
        {
            return null;
        }

        return field => section[field] is JsonValue value && (value.TryGetValue<string>(out var text) ? text : value.ToString()) is { } found && !string.IsNullOrWhiteSpace(found)
            ? found.Trim()
            : null;
    }

    /// <summary>Removes the object <paramref name="name"/>, keeping the rest; the file goes when nothing else is in it.</summary>
    internal static void RemoveSection(string path, string name)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var root = ReadRoot(path);
        root.Remove(name);
        if (root.Count == 0)
        {
            File.Delete(path);
        }
        else
        {
            Write(path, root);
        }
    }

    /// <summary>Writes the file whole, readable by the current user only on Windows.</summary>
    internal static void Write(string path, JsonObject root)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(folder);
        var partial = Path.Combine(folder, $".{VoiceSecrets.FileName}.{Guid.NewGuid():N}.part");
        File.WriteAllText(partial, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        if (OperatingSystem.IsWindows())
        {
            OwnerOnly(partial);
        }

        File.Move(partial, path, overwrite: true);
    }

    // Only the current user may read or change the key: inherited access is removed.
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void OwnerOnly(string path)
    {
        var user = System.Security.Principal.WindowsIdentity.GetCurrent().User!;
        var security = new System.Security.AccessControl.FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(user, System.Security.AccessControl.FileSystemRights.FullControl, System.Security.AccessControl.AccessControlType.Allow));
        new FileInfo(path).SetAccessControl(security);
    }
}
