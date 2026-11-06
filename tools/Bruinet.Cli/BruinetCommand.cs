using System.CommandLine;
using System.CommandLine.Help;
using System.Reflection;
using Bruinet.Bdd.Workspace;
using Bruinet.Cli.Data;
using Bruinet.Cli.Generate;
using Bruinet.Cli.Scenario;
using Bruinet.Cli.Scrape;
using Bruinet.Cli.Selection;
using Bruinet.Cli.Servers;
using Bruinet.Core.Scraping;
using Bruinet.DataValidator;
using Bruinet.Scraper.Service;
using Microsoft.Playwright;

namespace Bruinet.Cli;

/// <summary>Where a command reads and writes: the console, or a test's readers and writers.</summary>
/// <param name="In">Answers to questions, and Enter after a sign-in.</param>
/// <param name="Out">Results.</param>
/// <param name="Error">Progress, questions and problems.</param>
/// <param name="CurrentDirectory">Where relative paths, and the solution and project, are found from.</param>
/// <param name="Interactive">A person is at a terminal: questions may be asked and a browser shown.</param>
internal sealed record CliIo(TextReader In, TextWriter Out, TextWriter Error, string CurrentDirectory, bool Interactive)
{
    /// <summary>The Scraper's page source by platform; the default one when null.</summary>
    public Func<string, IScrapeSource>? SourceFor { get; init; }

    public static CliIo Console() =>
        new(System.Console.In, System.Console.Out, System.Console.Error, Environment.CurrentDirectory, !System.Console.IsInputRedirected);
}

/// <summary><c>bruinet</c>: the command tree, and running one command. Exit 0 fine, 1 problems found, 2 could not run.</summary>
internal static class BruinetCommand
{
    public static RootCommand Build(CliIo io)
    {
        var root = new RootCommand("Bruinet's command line: the Scraper, test data and scenarios, control generation, and the servers the IDEs and agents start.")
        {
            ScrapeCommands.Build(io),
            DataCommands.Build(io),
            ScenarioCommands.Build(io),
            SelectionCommands.Build(io),
            Sql.SqlCommands.Build(io),
            Http.HttpCommands.Build(io),
            Scenario.WhyCommand.Build(io),
            Scenario.VoiceCommand.Build(io),
            GenerateCommand.Build(io),
            Version(io),
            ServerCommands.Mcp(io),
            ServerCommands.McpHost(io),
            ServerCommands.Lsp(),
        };
        HelpWhenBare(root);
        return root;
    }

    public static async Task<int> RunAsync(string[] args, CliIo io, CancellationToken cancellationToken = default)
    {
        var parsed = Build(io).Parse(args);
        if (parsed.Errors.Count > 0)
        {
            foreach (var error in parsed.Errors)
                await io.Error.WriteLineAsync(error.Message).ConfigureAwait(false);
            var command = string.Join(' ', parsed.CommandResult.Command is RootCommand ? ["bruinet"] : Path(parsed.CommandResult));
            await io.Error.WriteLineAsync($"See '{command} --help'.").ConfigureAwait(false);
            return 2;
        }

        return await parsed.InvokeAsync(new InvocationConfiguration { Output = io.Out, Error = io.Error }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs a command's work: a problem it can name is a message and exit 2, not a stack trace.</summary>
    public static void Action(this Command command, CliIo io, Func<ParseResult, CancellationToken, Task<int>> action) =>
        command.SetAction(async (parsed, cancellationToken) =>
        {
            try
            {
                return await action(parsed, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await io.Error.WriteLineAsync("Stopped.").ConfigureAwait(false);
                return 1;
            }
            catch (PlaywrightException e)
            {
                await io.Error.WriteLineAsync($"The browser could not open the app: {e.Message.ReplaceLineEndings(" ")}").ConfigureAwait(false);
                return 2;
            }
            catch (TimeoutException e)
            {
                await io.Error.WriteLineAsync($"The browser could not open the app: {e.Message.ReplaceLineEndings(" ")}").ConfigureAwait(false);
                return 2;
            }
            catch (Exception e) when (e is ScraperProblem or ToolException or BddWorkspaceException or FormatException or FileNotFoundException or DirectoryNotFoundException or ArgumentException or InvalidOperationException)
            {
                await io.Error.WriteLineAsync(e.Message).ConfigureAwait(false);
                return 2;
            }
        });

    /// <summary>Runs a command's work that needs no awaiting.</summary>
    public static void Action(this Command command, CliIo io, Func<ParseResult, int> action) =>
        command.Action(io, (parsed, _) => Task.FromResult(action(parsed)));

    private static Command Version(CliIo io)
    {
        var command = new Command("version", "The version of this bruinet.");
        command.Action(io, _ =>
        {
            var assembly = typeof(BruinetCommand).Assembly;
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown";
            io.Out.WriteLine($"bruinet {version.Split('+')[0]}");
            return 0;
        });
        return command;
    }

    // A group on its own (bruinet, bruinet scrape) shows what comes next.
    private static void HelpWhenBare(Command command)
    {
        if (command.Action is null)
        {
            command.SetAction(parsed =>
            {
                new HelpAction().Invoke(parsed);
                return 0;
            });
        }

        foreach (var sub in command.Subcommands)
            HelpWhenBare(sub);
    }

    private static IEnumerable<string> Path(System.CommandLine.Parsing.CommandResult result)
    {
        var names = new Stack<string>();
        for (var at = result; at is not null; at = at.Parent as System.CommandLine.Parsing.CommandResult)
            names.Push(at.Command is RootCommand ? "bruinet" : at.Command.Name);
        return names;
    }
}
