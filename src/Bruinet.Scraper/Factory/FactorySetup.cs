using System.Globalization;
using System.Text;
using Bruinet.Core.Utilities;

namespace Bruinet.Scraper.Factory;

/// <summary>What the Scraper may write on the environment it works on.</summary>
public enum WritesAllowed
{
    /// <summary>Nothing: links and tabs are followed, and every write is held back.</summary>
    ReadOnly,

    /// <summary>Only on records it made itself, which carry the run's marker.</summary>
    OwnRecords,

    /// <summary>Anything but what sends, pays or signs out: an environment that is safe to write on.</summary>
    Free,
}

/// <summary>What a change the factory keeps by itself may touch.</summary>
public enum ImproveScope
{
    /// <summary>Nothing: it reports and changes nothing.</summary>
    Off,

    /// <summary>What describes the app: its signs, holders, templates and setup, and a control library's signs file.</summary>
    Data,

    /// <summary>Also the suite's own code, and a control library's.</summary>
    Suite,

    /// <summary>Also Bruinet's own code.</summary>
    Bruinet,
}

/// <summary>When the factory may ask a person something.</summary>
public enum AskWhen
{
    /// <summary>Never: what waits for an answer is parked and reported.</summary>
    Never,

    /// <summary>At the start only.</summary>
    Start,

    /// <summary>When a batch ends.</summary>
    Batch,

    /// <summary>Whenever something blocks.</summary>
    Blocked,
}

/// <summary>When the factory commits what it kept.</summary>
public enum CommitWhen
{
    /// <summary>Never: everything stays in the working tree.</summary>
    No,

    /// <summary>Once per batch.</summary>
    Batch,

    /// <summary>Once per group.</summary>
    Group,
}

/// <summary>Who accepts a spec the factory drafted.</summary>
public enum SpecAcceptor
{
    /// <summary>A person: a drafted spec stays a draft.</summary>
    Person,

    /// <summary>The agent that rewrote it.</summary>
    Agent,
}

/// <summary>
/// A person's answers about one app, asked once before the factory runs (<c>setup.md</c> in the app's own Scraper
/// place): how careful, how much, how free. A row that is missing takes the careful value.
/// </summary>
public sealed record FactorySetup
{
    /// <summary>The file's name.</summary>
    public const string FileName = "setup.md";

    private const string Section = "Setup";

    /// <summary>What may be written on the environment.</summary>
    public WritesAllowed Writes { get; init; } = WritesAllowed.ReadOnly;

    /// <summary>What runs: <c>all</c>, or the modules by name in their order; empty until a person chose.</summary>
    public IReadOnlyList<string> Run { get; init; } = [];

    /// <summary>What a change kept without a person may touch.</summary>
    public ImproveScope Improve { get; init; } = ImproveScope.Data;

    /// <summary>When a person may be asked.</summary>
    public AskWhen Ask { get; init; } = AskWhen.Batch;

    /// <summary>When what was kept is committed.</summary>
    public CommitWhen Commit { get; init; } = CommitWhen.No;

    /// <summary>Who accepts a drafted spec.</summary>
    public SpecAcceptor Specs { get; init; } = SpecAcceptor.Person;

    /// <summary>How many browsers walk at once.</summary>
    public int Walkers { get; init; } = 1;

    /// <summary>True when everything runs: the row says <c>all</c>.</summary>
    public bool RunsAll => Run is ["all"];

    /// <summary>The setup of a folder: its file's answers, the careful values where it has none or is not there.</summary>
    /// <exception cref="FormatException">An answer is not one of its question's.</exception>
    public static FactorySetup Read(string folder)
    {
        var path = Path.Combine(folder, FileName);
        return File.Exists(path) ? Parse(File.ReadAllText(path)) : new FactorySetup();
    }

    /// <summary>Reads a setup's text.</summary>
    /// <exception cref="FormatException">An answer is not one of its question's.</exception>
    public static FactorySetup Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var setup = new FactorySetup();
        if (!MarkdownTables.ReadSections(markdown).TryGetValue(Section, out var section))
            return setup;
        foreach (var row in section.Rows)
            setup = setup.With(row.Cells.GetValueOrDefault("Question", "").Trim(), row.Cells.GetValueOrDefault("Answer", "").Trim().Trim('`'), row.LineNumber);
        return setup;
    }

    /// <summary>The setup with one answer changed.</summary>
    /// <exception cref="FormatException">The question is not one of the setup's, or the answer not one of the question's.</exception>
    public FactorySetup With(string question, string answer, int line = 0)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(answer);
        var at = line > 0 ? $"Line {line}: " : "";
        return question.ToLowerInvariant() switch
        {
            "writes" => this with { Writes = One<WritesAllowed>(answer, at, question) },
            "run" => this with { Run = [.. answer.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)] },
            "improve" => this with { Improve = One<ImproveScope>(answer, at, question) },
            "ask" => this with { Ask = One<AskWhen>(answer, at, question) },
            "commit" => this with { Commit = One<CommitWhen>(answer, at, question) },
            "specs" => this with { Specs = One<SpecAcceptor>(answer, at, question) },
            "walkers" => this with { Walkers = int.TryParse(answer, NumberStyles.None, CultureInfo.InvariantCulture, out var walkers) && walkers is >= 1 and <= 16 ? walkers : throw new FormatException($"{at}Walkers is a number from 1 to 16, not '{answer}'.") },
            _ => throw new FormatException($"{at}'{question}' is not a question of the setup (Writes, Run, Improve, Ask, Commit, Specs, Walkers)."),
        };
    }

    /// <summary>The setup as its file: each question with its answer and what the answers are.</summary>
    public string ToMarkdown()
    {
        var text = new StringBuilder();
        text.Append("# Setup\n\nWhat a person answered about this app, once, before the factory runs. Change an answer here, or with\n`bruinet scrape scraper setup --set Question=answer`. A row that is missing takes the careful value.\n\n## Setup\n\n");
        text.Append("| Question | Answer | The answers |\n| --- | --- | --- |\n");
        text.Append(CultureInfo.InvariantCulture, $"| Writes | {Name(Writes)} | `read-only`: links and tabs only, every write held back. `own-records`: writes only on records the Scraper made itself. `free`: anything but what sends, pays or signs out |\n");
        text.Append(CultureInfo.InvariantCulture, $"| Run | {string.Join(", ", Run)} | `all`, or the modules by name in their order (`modules.md`). Empty: nothing runs until a person chose |\n");
        text.Append(CultureInfo.InvariantCulture, $"| Improve | {Name(Improve)} | What a change kept without a person may touch. `off`: nothing. `data`: the app's signs, holders, templates and setup. `suite`: also the suite's code. `bruinet`: also Bruinet's own |\n");
        text.Append(CultureInfo.InvariantCulture, $"| Ask | {Name(Ask)} | When a person may be asked: `never`, `start`, `batch` (when a batch ends), `blocked` (whenever something blocks) |\n");
        text.Append(CultureInfo.InvariantCulture, $"| Commit | {Name(Commit)} | `no`, `batch` or `group`: when what was kept is committed. Nothing is ever pushed |\n");
        text.Append(CultureInfo.InvariantCulture, $"| Specs | {Name(Specs)} | Who accepts a drafted spec: `person` or `agent` |\n");
        text.Append(CultureInfo.InvariantCulture, $"| Walkers | {Walkers} | How many browsers walk at once, 1 to 16 |\n");
        return text.ToString();
    }

    /// <summary>An answer as the file writes it: <c>own-records</c> for <see cref="WritesAllowed.OwnRecords"/>.</summary>
    public static string Name<T>(T value)
        where T : struct, Enum
    {
        var name = value.ToString();
        var text = new StringBuilder();
        foreach (var letter in name)
        {
            if (char.IsUpper(letter) && text.Length > 0)
                text.Append('-');
            text.Append(char.ToLowerInvariant(letter));
        }

        return text.ToString();
    }

    private static T One<T>(string answer, string at, string question)
        where T : struct, Enum
    {
        foreach (var value in Enum.GetValues<T>())
        {
            if (string.Equals(Name(value), answer, StringComparison.OrdinalIgnoreCase))
                return value;
        }

        throw new FormatException($"{at}{question} is one of {string.Join(", ", Enum.GetValues<T>().Select(Name))}, not '{answer}'.");
    }
}
