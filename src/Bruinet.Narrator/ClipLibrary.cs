using System.Text.Json.Serialization;
using Bruinet.Recording;

namespace Bruinet.Narrator;

/// <summary>What a cloud voice does with a line, in a <see cref="ClipLibrary"/>.</summary>
public enum ClipLibraryMode
{
    /// <summary>A recording: a clip for the scene, and a line found nowhere is asked of the service.</summary>
    Ask,

    /// <summary>A recording that may not spend: a line found nowhere fails it.</summary>
    ReuseOnly,

    /// <summary>No clips for a scene, and nothing written: only what is there and what is missing.</summary>
    Count,

    /// <summary>No clips for a scene: the library takes what the local cache holds, and nothing is asked.</summary>
    Fill,

    /// <summary>No clips for a scene: as <see cref="Fill"/>, and a line found nowhere is asked of the service.</summary>
    Buy,
}

/// <summary>What a recording did for its lines.</summary>
/// <param name="Reused">Lines that cost nothing: found in the library or the local cache.</param>
/// <param name="Asked">Lines asked of the service.</param>
/// <param name="Characters">The characters asked for.</param>
public sealed record BddSpeechUse(int Reused, int Asked, int Characters)
{
    /// <summary>How many lines were said.</summary>
    [JsonIgnore]
    public int Lines => Reused + Asked;

    /// <summary>The use as a line.</summary>
    [JsonIgnore]
    public string Summary => $"Speech: {Lines} lines, {Reused} reused, {Asked} asked ({Characters} characters)";
}

/// <summary>A line no clip was found for, in a recording that may not spend.</summary>
/// <param name="line">What is said.</param>
public sealed class VoiceLineMissingException(string line)
    : Exception($"No clip for \"{line}\", and {ClipLibrary.ReuseVariable} says not to ask the service. `bruinet voice <suite>` lists every missing line, `--ask` buys them.");

/// <summary>Turns a WAV into a library's clip, and back.</summary>
internal interface IClipCodec
{
    Task EncodeAsync(string wav, string clip, CancellationToken cancellationToken);

    Task DecodeAsync(string clip, string wav, CancellationToken cancellationToken);
}

/// <summary>
/// A suite's cloud voice clips, committed beside it (<c>speech\&lt;key&gt;.opus</c>): a line whose words
/// did not change is never asked again, on any machine. It sits on top of the local cache, which
/// keeps its own names, so a clip already paid for moves in without being asked. Named by what
/// changes what is said and nothing else: no endpoint, so another region finds it, and no secret.
/// </summary>
public sealed class ClipLibrary
{
    /// <summary>The library's folder, when it is not the suite's <c>speech</c>.</summary>
    public const string Variable = "BRUINET_VOICE_LIBRARY";

    /// <summary>When set, a line found nowhere fails the recording instead of being paid for.</summary>
    public const string ReuseVariable = "BRUINET_VOICE_REUSE";

    /// <summary>The folder's name in a suite.</summary>
    public const string FolderName = "speech";

    /// <summary>A clip's extension.</summary>
    public const string Extension = ".opus";

    private readonly IClipCodec _codec;
    private readonly object _gate = new();
    private readonly HashSet<string> _reached = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _missing = [];
    private int _reused;
    private int _asked;
    private int _characters;

    /// <summary>Creates a library.</summary>
    /// <param name="folder">Its folder; made when the first clip is added.</param>
    public ClipLibrary(string folder)
        : this(folder, new OpusCodec())
    {
    }

    internal ClipLibrary(string folder, IClipCodec codec)
    {
        Folder = folder ?? throw new ArgumentNullException(nameof(folder));
        _codec = codec;
    }

    /// <summary>The library's folder.</summary>
    public string Folder { get; }

    /// <summary>What a voice does with a line.</summary>
    public ClipLibraryMode Mode { get; set; } = ClipLibraryMode.Ask;

    /// <summary>What was said so far; null before the first line.</summary>
    public BddSpeechUse? Use
    {
        get
        {
            lock (_gate)
            {
                return _reused + _asked == 0 ? null : new BddSpeechUse(_reused, _asked, _characters);
            }
        }
    }

    /// <summary>The lines found nowhere and not asked for.</summary>
    public IReadOnlyList<string> Missing
    {
        get
        {
            lock (_gate)
            {
                return [.. _missing];
            }
        }
    }

    /// <summary>Whether a scene gets a WAV for each line.</summary>
    internal bool MakesWavs => Mode is ClipLibraryMode.Ask or ClipLibraryMode.ReuseOnly;

    /// <summary>
    /// A suite's library: <c>BRUINET_VOICE_LIBRARY</c>, else <c>speech</c> in the source folder a host
    /// named, else in the nearest folder up that holds a project. A <c>dotnet test</c> suite runs from
    /// its output, and clips kept there would go with the next clean. Null without a folder.
    /// </summary>
    /// <param name="configDirectory">The suite's folder, or any folder in its project.</param>
    /// <param name="read">Reads a process variable; <see cref="Environment.GetEnvironmentVariable(string)"/> by default.</param>
    public static ClipLibrary? Find(string? configDirectory, Func<string, string?>? read = null)
    {
        read ??= Environment.GetEnvironmentVariable;
        var folder = FolderOf(configDirectory, read);
        if (folder is null)
        {
            return null;
        }

        var reuse = read(ReuseVariable)?.Trim();
        var reuseOnly = !string.IsNullOrEmpty(reuse) && reuse is not "0" && !reuse.Equals("false", StringComparison.OrdinalIgnoreCase);
        return new ClipLibrary(folder) { Mode = reuseOnly ? ClipLibraryMode.ReuseOnly : ClipLibraryMode.Ask };
    }

    /// <summary>A clip's file.</summary>
    /// <param name="key">The clip's name.</param>
    public string PathOf(string key) => Path.Combine(Folder, key + Extension);

    /// <summary>Whether the library holds a clip.</summary>
    /// <param name="key">The clip's name.</param>
    public bool Has(string key) => File.Exists(PathOf(key));

    /// <summary>Writes a clip out as a WAV.</summary>
    /// <param name="key">The clip's name.</param>
    /// <param name="wavPath">The WAV to write.</param>
    /// <param name="cancellationToken">Stops the decoding.</param>
    public async Task CopyToAsync(string key, string wavPath, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(wavPath))!);
        await _codec.DecodeAsync(PathOf(key), wavPath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Adds a clip from a WAV.</summary>
    /// <param name="key">The clip's name.</param>
    /// <param name="wavPath">The WAV.</param>
    /// <param name="cancellationToken">Stops the encoding.</param>
    public async Task AddAsync(string key, string wavPath, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Folder);

        // Written whole, then moved: a cancelled or failed write never leaves a clip behind.
        var partial = PathOf(key) + "." + Guid.NewGuid().ToString("N") + ".part";
        try
        {
            await _codec.EncodeAsync(wavPath, partial, cancellationToken).ConfigureAwait(false);
            File.Move(partial, PathOf(key), overwrite: true);
        }
        finally
        {
            File.Delete(partial);
        }
    }

    /// <summary>Deletes the clips no line of this pass reached; run after every script's lines were gone through.</summary>
    /// <returns>How many went.</returns>
    public int Prune()
    {
        if (!Directory.Exists(Folder))
        {
            return 0;
        }

        var left = Directory.GetFiles(Folder, "*" + Extension)
            .Where(clip => !_reached.Contains(Path.GetFileNameWithoutExtension(clip)))
            .ToList();
        left.ForEach(File.Delete);
        return left.Count;
    }

    internal void Reused(string key)
    {
        lock (_gate)
        {
            _reached.Add(key);
            _reused++;
        }
    }

    internal void Asked(string key, string said)
    {
        lock (_gate)
        {
            _reached.Add(key);
            _asked++;
            _characters += said.Length;
        }
    }

    internal void NotFound(string said)
    {
        lock (_gate)
        {
            _missing.Add(said);
        }
    }

    private static string? FolderOf(string? configDirectory, Func<string, string?> read)
    {
        if (read(Variable) is { Length: > 0 } named)
        {
            return named;
        }

        if (read(BddScenarioSource.SourceDirectoryEnvironmentVariable) is { Length: > 0 } source && Directory.Exists(source))
        {
            return Path.Combine(source, FolderName);
        }

        if (configDirectory is null)
        {
            return null;
        }

        for (var folder = new DirectoryInfo(configDirectory); folder is not null; folder = folder.Parent)
        {
            if (folder.Exists && folder.EnumerateFiles("*.csproj").Any())
            {
                return Path.Combine(folder.FullName, FolderName);
            }
        }

        return Path.Combine(configDirectory, FolderName);
    }

    // Speech at 32 kbit/s is a twelfth of its WAV and sounds the same once it is the video's AAC;
    // a clip stays in the repository's history for good, so the size counts.
    private sealed class OpusCodec : IClipCodec
    {
        public async Task EncodeAsync(string wav, string clip, CancellationToken cancellationToken)
        {
            try
            {
                await Ffmpeg.RunAsync(["-y", "-i", wav, "-map_metadata", "-1", "-c:a", "libopus", "-b:a", "32k", "-f", "opus", clip], cancellationToken).ConfigureAwait(false);
            }
            catch (FfmpegException exception) when (exception.Message.Contains("libopus", StringComparison.OrdinalIgnoreCase))
            {
                throw new FfmpegException($"This ffmpeg cannot write Opus, which the voice clips are kept in. A full build can (Windows: winget install Gyan.FFmpeg).{Environment.NewLine}{exception.Message}");
            }
        }

        public Task DecodeAsync(string clip, string wav, CancellationToken cancellationToken)
            => Ffmpeg.RunAsync(["-y", "-i", clip, "-c:a", "pcm_s16le", wav], cancellationToken);
    }
}
