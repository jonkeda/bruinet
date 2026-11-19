using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bruinet.Scraper.Factory;

/// <summary>One loop of the factory: a group through walk, read, draft, build and test, and what it left.</summary>
public sealed record LoopResult
{
    /// <summary>Its number, from 1.</summary>
    public int Number { get; init; }

    /// <summary>The batch it ran in.</summary>
    public int Batch { get; init; }

    /// <summary>The group.</summary>
    public required string Group { get; init; }

    /// <summary>When it started.</summary>
    public DateTimeOffset Started { get; init; }

    /// <summary>What the walk said; empty when the loop did not walk.</summary>
    public string Walk { get; init; } = "";

    /// <summary>The places it took up: drafted, or tried and not drafted. A place a run took up is not taken up again in that run.</summary>
    public IReadOnlyList<string> Places { get; init; } = [];

    /// <summary>The classes and tests it wrote, by their files relative to the suite.</summary>
    public IReadOnlyList<string> Drafted { get; init; } = [];

    /// <summary>What it wrote and kept.</summary>
    public IReadOnlyList<string> Kept { get; init; } = [];

    /// <summary>What it wrote and put back.</summary>
    public IReadOnlyList<string> PutBack { get; init; } = [];

    /// <summary>How many of its tests ran.</summary>
    public int Tests { get; init; }

    /// <summary>How many of them passed the first time.</summary>
    public int PassedFirst { get; init; }

    /// <summary>Why something was not kept, or not drafted.</summary>
    public IReadOnlyList<Reason> Reasons { get; init; } = [];

    /// <summary>The seconds each station took: walk, read, draft, build, test.</summary>
    public IReadOnlyDictionary<string, double> Seconds { get; init; } = new Dictionary<string, double>();

    /// <summary>Why the factory must stop after this loop; null when it may go on.</summary>
    public string? Stop { get; init; }

    /// <summary>The loop's minutes, all stations.</summary>
    [JsonIgnore]
    public double Minutes => Math.Round(Seconds.Values.Sum() / 60, 1);

    /// <summary>True when the loop wrote something and kept none of it, or owed something and drafted none of it.</summary>
    [JsonIgnore]
    public bool KeptNothing => Kept.Count == 0 && (Drafted.Count > 0 || Reasons.Any(reason => reason.Code is not (ReasonCodes.NothingToCheck or ReasonCodes.ClassExists)));
}

/// <summary>What the factory remembers between runs (<c>state.json</c> in the app's own Scraper place): every loop, so a run that stops goes on.</summary>
public sealed class FactoryState
{
    /// <summary>The file's name.</summary>
    public const string FileName = "state.json";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The loops, in the order they ran.</summary>
    public List<LoopResult> Loops { get; init; } = [];

    /// <summary>Lines an agent or a person added, each with the loop it is about (0 for none).</summary>
    public List<FactoryNote> Notes { get; init; } = [];

    /// <summary>The number the next batch gets.</summary>
    [JsonIgnore]
    public int NextBatch => Loops.Count == 0 ? 1 : Loops.Max(loop => loop.Batch) + 1;

    /// <summary>The state of a folder; an empty one when it has none.</summary>
    public static FactoryState Load(string folder)
    {
        var path = Path.Combine(folder, FileName);
        return File.Exists(path) ? JsonSerializer.Deserialize<FactoryState>(File.ReadAllText(path), Json) ?? new FactoryState() : new FactoryState();
    }

    /// <summary>Writes the state into a folder.</summary>
    public void Save(string folder) =>
        File.WriteAllText(Path.Combine(folder, FileName), JsonSerializer.Serialize(this, Json).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");

    /// <summary>Adds a loop with the next number.</summary>
    public LoopResult Add(LoopResult loop)
    {
        ArgumentNullException.ThrowIfNull(loop);
        var numbered = loop with { Number = Loops.Count + 1 };
        Loops.Add(numbered);
        return numbered;
    }
}

/// <summary>A line added to the ledger by hand.</summary>
/// <param name="Loop">The loop it is about; 0 for none.</param>
/// <param name="At">When.</param>
/// <param name="Text">What it says.</param>
public sealed record FactoryNote(int Loop, DateTimeOffset At, string Text);

/// <summary>When the factory stops by itself: a batch that repeats a reason has a fault of the process, not of its groups.</summary>
public static class StopRules
{
    /// <summary>How often one reason may come in a batch before the batch stops.</summary>
    public const int SameReason = 3;

    /// <summary>How many loops in a row may keep nothing.</summary>
    public const int KeptNothing = 2;

    /// <summary>Why the batch stops after its last loop; null when it goes on.</summary>
    /// <param name="batch">The batch's loops so far, in order.</param>
    public static string? Check(IReadOnlyList<LoopResult> batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Count == 0)
            return null;
        if (batch[^1].Stop is { } own)
            return own;
        var repeated = batch.SelectMany(loop => loop.Reasons).Where(reason => reason.Code is not (ReasonCodes.PassedSecondTime or ReasonCodes.NothingToCheck or ReasonCodes.ClassExists))
            .GroupBy(reason => reason.Code, StringComparer.Ordinal).Where(group => group.Count() >= SameReason).OrderByDescending(group => group.Count()).FirstOrDefault();
        if (repeated is not null)
            return string.Create(CultureInfo.InvariantCulture, $"the reason '{repeated.Key}' came {repeated.Count()} times in this batch: it is a fault of the process, to fix at {repeated.First().FixAt} before the next group (for example {repeated.First().Place}: {repeated.First().Evidence})");
        return batch.Count >= KeptNothing && batch.TakeLast(KeptNothing).All(loop => loop.KeptNothing)
            ? string.Create(CultureInfo.InvariantCulture, $"{KeptNothing} loops in a row kept nothing ({string.Join(", ", batch.TakeLast(KeptNothing).Select(loop => loop.Group))})")
            : null;
    }
}

/// <summary>The factory's ledger as a document (<c>ledger.md</c>), written from its state.</summary>
public static class FactoryLedger
{
    /// <summary>The file's name.</summary>
    public const string FileName = "ledger.md";

    /// <summary>The ledger: a row per loop, the reasons counted, and the notes.</summary>
    public static string ToMarkdown(FactoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var text = new StringBuilder();
        text.Append("# The factory's ledger\n\nA row per loop: a group through walk, read, draft, build and test. Written from `state.json` by\n`bruinet scrape factory run`; not edited by hand (`bruinet scrape factory note` adds a line).\n\n## Loops\n\n");
        text.Append("| # | Batch | Group | Kept | Put back | Tests, first run | Minutes | Walk | Why not kept |\n| --- | --- | --- | --- | --- | --- | --- | --- | --- |\n");
        foreach (var loop in state.Loops)
        {
            var why = string.Join("; ", loop.Reasons.GroupBy(reason => reason.Code, StringComparer.Ordinal).Select(group => group.Count() == 1 ? $"{group.Key} ({Cell(group.First().Class.Length > 0 ? group.First().Class : group.First().Place)})" : $"{group.Key} x{group.Count()}"));
            text.Append(CultureInfo.InvariantCulture, $"| {loop.Number} | {loop.Batch} | `{loop.Group}` | {Files(loop.Kept)} | {Files(loop.PutBack)} | {(loop.Tests == 0 ? "-" : $"{loop.PassedFirst} of {loop.Tests}")} | {loop.Minutes} | {Cell(loop.Walk)} | {Cell(why)}{(loop.Stop is null ? "" : " **Stopped:** " + Cell(loop.Stop))} |\n");
        }

        var reasons = state.Loops.SelectMany(loop => loop.Reasons).GroupBy(reason => reason.Code, StringComparer.Ordinal).OrderByDescending(group => group.Count()).ToList();
        if (reasons.Count > 0)
        {
            text.Append("\n## Reasons\n\n| Reason | Times | Groups | Fix at | For example |\n| --- | --- | --- | --- | --- |\n");
            foreach (var reason in reasons)
            {
                var groups = state.Loops.Count(loop => loop.Reasons.Any(each => each.Code == reason.Key));
                text.Append(CultureInfo.InvariantCulture, $"| {reason.Key} | {reason.Count()} | {groups} | {reason.First().FixAt} | {Cell(reason.First().Place)}: {Cell(reason.First().Evidence)} |\n");
            }
        }

        if (state.Notes.Count > 0)
        {
            text.Append("\n## Notes\n\n| Loop | When | Note |\n| --- | --- | --- |\n");
            foreach (var note in state.Notes)
                text.Append(CultureInfo.InvariantCulture, $"| {(note.Loop == 0 ? "-" : note.Loop.ToString(CultureInfo.InvariantCulture))} | {note.At:yyyy-MM-dd HH:mm} | {Cell(note.Text)} |\n");
        }

        return text.ToString();
    }

    private static string Files(IReadOnlyList<string> files) =>
        files.Count == 0 ? "none" : string.Join(", ", files.Select(file => $"`{Path.GetFileNameWithoutExtension(file).Replace(".scraped", "", StringComparison.Ordinal)}`"));

    private static string Cell(string text) => text.Replace("|", "/", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}
