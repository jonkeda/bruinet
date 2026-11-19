using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Host;

/// <summary>Where a command reads and writes: the console, or a test's readers and writers.</summary>
/// <param name="In">Answers to questions, and Enter after a sign-in.</param>
/// <param name="Out">Results.</param>
/// <param name="Error">Progress, questions and problems.</param>
/// <param name="CurrentDirectory">Where relative paths, and the solution and project, are found from.</param>
/// <param name="Interactive">A person is at a terminal: questions may be asked and a browser shown.</param>
public sealed record HostIo(TextReader In, TextWriter Out, TextWriter Error, string CurrentDirectory, bool Interactive)
{
    /// <summary>The Scraper's page source by platform; the default one when null.</summary>
    public Func<string, IScrapeSource>? SourceFor { get; init; }

    /// <summary>What the host gives the Scraper's commands that the engine does not have itself.</summary>
    public HostServices Services { get; init; } = new();

    /// <summary>The process's own console and folder.</summary>
    public static HostIo Console() =>
        new(System.Console.In, System.Console.Out, System.Console.Error, Environment.CurrentDirectory, !System.Console.IsInputRedirected);
}
