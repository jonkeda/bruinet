using System.Text.Json;

namespace Bruinet.Scraper.Projects;

/// <summary>
/// What the workflow remembers that the project's files do not say: the last app check and verify, the
/// controls a person accepted, and whether controls were skipped. Kept beside the config in
/// <c>.scraper-state.json</c>, which is the person's and not committed.
/// </summary>
public sealed record WorkflowState
{
    /// <summary>The file's name, in the project's folder.</summary>
    public const string FileName = ".scraper-state.json";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>The last app check, or null.</summary>
    public AppCheck? LastCheck { get; init; }

    /// <summary>The last verify, or null.</summary>
    public VerifySummary? LastVerify { get; init; }

    /// <summary>The person chose page objects without controls.</summary>
    public bool SkipControls { get; init; }

    /// <summary>The controls the person accepted as they are, by class name.</summary>
    public IReadOnlyList<string> AcceptedControls { get; init; } = [];

    /// <summary>The chat agent (or mode) the person chose for the Scraper's prompts; null for the config's.</summary>
    public string? Agent { get; init; }

    /// <summary>The model the person chose for the Scraper's prompts; null for the config's, empty for the IDE's own.</summary>
    public string? Model { get; init; }

    /// <summary>The project's state, or an empty one.</summary>
    public static WorkflowState Read(ScrapeProject project)
    {
        var path = Path.Combine(project.Folder, FileName);
        if (!File.Exists(path))
            return new WorkflowState();
        try
        {
            return JsonSerializer.Deserialize<WorkflowState>(File.ReadAllText(path), Json) ?? new WorkflowState();
        }
        catch (JsonException)
        {
            return new WorkflowState(); // a file broken by hand is started over
        }
    }

    /// <summary>Writes the project's state.</summary>
    public void Write(ScrapeProject project) =>
        File.WriteAllText(Path.Combine(project.Folder, FileName), JsonSerializer.Serialize(this, Json));
}

/// <summary>Whether the app could be reached: answered, signed in, on a host Explore may go to.</summary>
/// <param name="At">When it was checked.</param>
/// <param name="Url">The address checked.</param>
/// <param name="Status">The HTTP status, or null when nothing answered.</param>
/// <param name="Location">Where the browser ended up.</param>
/// <param name="SignIn">A sign-in page came instead.</param>
/// <param name="Refused">Why Explore may not go there, or null.</param>
/// <param name="Detail">What the check found, in a sentence.</param>
public sealed record AppCheck(DateTimeOffset At, string Url, int? Status, string? Location, bool SignIn, string? Refused, string Detail)
{
    /// <summary>Answered, signed in, and allowed.</summary>
    public bool Passed => Status is >= 200 and < 400 && !SignIn && Refused is null;
}

/// <summary>The last verify: when, and how many members were found once of how many.</summary>
public sealed record VerifySummary(DateTimeOffset At, bool Passed, int Found, int Members, int Problems);
