using System.Text.Json;
using System.Text.Json.Serialization;
using Bruinet.Core.Recording;

namespace Bruinet.Narrator;

/// <summary>
/// What happened when, on the video's clock (seconds from its first frame): each step's start, end
/// and result, and each spoken line. The captions, the chapters and the review page are made from it.
/// </summary>
/// <param name="Title">The video's title.</param>
/// <param name="Scenes">The scenes, in the order they play.</param>
public sealed record BddVideoTimeline(string Title, IReadOnlyList<BddSceneTimeline> Scenes)
{
    /// <summary>What the recording reused of its lines and what it paid for; null without a cloud voice.</summary>
    public BddSpeechUse? Speech { get; init; }

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Writes the timeline as JSON.</summary>
    /// <param name="path">The file.</param>
    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Json));

    /// <summary>Reads a timeline written by <see cref="Save"/>.</summary>
    /// <param name="path">The file.</param>
    public static BddVideoTimeline Load(string path)
        => JsonSerializer.Deserialize<BddVideoTimeline>(File.ReadAllText(path), Json) ?? throw new InvalidDataException($"{path} is not a timeline.");
}

/// <summary>One scene: its video, and its steps and lines on that video's clock.</summary>
/// <param name="Number">The scene's number in the script, from 1.</param>
/// <param name="Name">The scene's name; its chapter.</param>
/// <param name="Scenario">The scenario's address.</param>
/// <param name="Video">The scene's video file; null when it could not be recorded.</param>
/// <param name="Duration">How long the scene lasts, in seconds.</param>
/// <param name="Status">How the scenario ended.</param>
/// <param name="Steps">The steps as written, in the order they ran.</param>
/// <param name="Clips">The spoken lines.</param>
/// <param name="Warnings">What went less well than asked.</param>
/// <param name="Width">The video's width in pixels; 0 when there is no video.</param>
/// <param name="Height">The video's height in pixels; 0 when there is no video.</param>
/// <param name="Hash">What a kept scene's recording was made from (<see cref="BddKeptScenes.Hash"/>); null when the scene is not kept.</param>
/// <param name="Interactions">What the test did to the app, in the driver's coordinates; null when nothing was recorded.</param>
/// <param name="Source">The area of the driver's coordinates the video's frame shows; null when they are the frame's own pixels.</param>
public sealed record BddSceneTimeline(
    int Number,
    string Name,
    string? Scenario,
    string? Video,
    double Duration,
    BddStepResultStatus Status,
    IReadOnlyList<BddTimedStep> Steps,
    IReadOnlyList<BddTimedClip> Clips,
    IReadOnlyList<string> Warnings,
    int Width = 0,
    int Height = 0,
    string? Hash = null,
    IReadOnlyList<BddTimedInteraction>? Interactions = null,
    BddBox? Source = null)
{
    /// <summary>The file a scene's timeline is kept in beside its video.</summary>
    public const string FileName = "timeline.json";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Writes the scene's timeline as JSON.</summary>
    /// <param name="path">The file.</param>
    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Json));

    /// <summary>Reads a scene timeline written by <see cref="Save"/>.</summary>
    /// <param name="path">The file.</param>
    public static BddSceneTimeline Load(string path)
        => JsonSerializer.Deserialize<BddSceneTimeline>(File.ReadAllText(path), Json) ?? throw new InvalidDataException($"{path} is not a scene timeline.");
}

/// <summary>A step as written, on the video's clock.</summary>
/// <param name="Line">Its line in the feature file.</param>
/// <param name="Text">Its keyword and text.</param>
/// <param name="Start">When it started, in seconds.</param>
/// <param name="End">When it ended, in seconds.</param>
/// <param name="Status">Its result; a call's is its worst step's.</param>
/// <param name="Message">A failed step's message.</param>
public sealed record BddTimedStep(int Line, string Text, double Start, double End, BddStepResultStatus Status, string? Message = null);

/// <summary>An interaction, on the video's clock: what a touch mark is drawn from.</summary>
/// <param name="Kind">What was done.</param>
/// <param name="At">When it ran, in seconds; taken just before.</param>
/// <param name="Bounds">Its target, in the driver's coordinates; empty for a key.</param>
public sealed record BddTimedInteraction(InteractionKind Kind, double At, BddBox Bounds)
{
    /// <summary>Where a swipe starts.</summary>
    public BddSpot? From { get; init; }

    /// <summary>Where a swipe ends.</summary>
    public BddSpot? To { get; init; }

    /// <summary>How long it lasted, in seconds; zero when instant.</summary>
    public double Duration { get; init; }

    /// <summary>The target's AutomationId or name; never drawn.</summary>
    public string? Label { get; init; }

    /// <summary>The key's name, for a <see cref="InteractionKind.Key"/>.</summary>
    public string? Key { get; init; }

    /// <summary>A logged interaction on the video's clock.</summary>
    /// <param name="interaction">The logged interaction.</param>
    /// <param name="videoStart">The video's first frame, on the same clock.</param>
    public static BddTimedInteraction Of(Interaction interaction, long videoStart)
    {
        ArgumentNullException.ThrowIfNull(interaction);
        return new BddTimedInteraction(
            interaction.Kind,
            Math.Max(0, System.Diagnostics.Stopwatch.GetElapsedTime(videoStart, interaction.At).TotalSeconds),
            BddBox.Of(interaction.Bounds))
        {
            From = interaction.From is { } from ? new BddSpot(from.X, from.Y) : null,
            To = interaction.To is { } to ? new BddSpot(to.X, to.Y) : null,
            Duration = interaction.Duration.TotalSeconds,
            Label = interaction.Label,
            Key = interaction.Key,
        };
    }
}

/// <summary>A rectangle in a timeline.</summary>
/// <param name="X">Its left.</param>
/// <param name="Y">Its top.</param>
/// <param name="Width">Its width.</param>
/// <param name="Height">Its height.</param>
public sealed record BddBox(int X, int Y, int Width, int Height)
{
    /// <summary>Whether it has no area.</summary>
    [JsonIgnore]
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>The box of a rectangle.</summary>
    /// <param name="rectangle">The rectangle.</param>
    public static BddBox Of(System.Drawing.Rectangle rectangle) => new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
}

/// <summary>A point in a timeline.</summary>
/// <param name="X">Its left.</param>
/// <param name="Y">Its top.</param>
public sealed record BddSpot(int X, int Y);

/// <summary>A spoken line, on the video's clock.</summary>
/// <param name="Text">What is said.</param>
/// <param name="Wav">The speech; null when there was no voice.</param>
/// <param name="Start">When it starts, in seconds.</param>
/// <param name="End">When it ends, in seconds.</param>
public sealed record BddTimedClip(string Text, string? Wav, double Start, double End);
