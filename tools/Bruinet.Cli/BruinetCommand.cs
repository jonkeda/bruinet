using System.CommandLine;
using System.CommandLine.Help;
using Bruinet.Bdd.Workspace;
using Bruinet.Cli.Data;
using Bruinet.Cli.Generate;
using Bruinet.Cli.Scenario;
using Bruinet.Cli.Scrape;
using Bruinet.Cli.Selection;
using Bruinet.Cli.Servers;
using Bruinet.Scraper.Snapshots;
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
            Scenario.VoiceCommand.Build(io),
            GenerateCommand.Build(io),
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

        // The tool's own handler, not System.CommandLine's, which exits 1 on an exception: 1 means "found" (D3).
        return await parsed.InvokeAsync(new InvocationConfiguration { Output = io.Out, Error = io.Error, EnableDefaultExceptionHandler = false }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a command's work. The exit codes every command keeps (D3): 0 ran and found nothing, 1 ran and found something, 2 could not run, 130
    /// stopped by Ctrl+C. Anything a command throws is "could not run": one line on stderr, the stack trace only with
    /// <c>BRUINET_DEBUG=1</c>.
    /// </summary>
    public static void Action(this Command command, CliIo io, Func<ParseResult, CancellationToken, Task<int>> action) =>
        command.SetAction(async (parsed, cancellationToken) =>
        {
            try
            {
                return await action(parsed, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await io.Error.WriteLineAsync("bruinet: stopped.").ConfigureAwait(false);
                return Stopped;
            }
            catch (Exception e) when (e is PlaywrightException or TimeoutException)
            {
                return await CouldNotRun(io, $"the browser could not open the app: {e.Message.ReplaceLineEndings(" ")}", e).ConfigureAwait(false);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                // The tool's boundary: whatever a command throws ends here as "could not run".
                return await CouldNotRun(io, Plain(e), e).ConfigureAwait(false);
            }
        });

    /// <summary>The exit code of a command stopped by Ctrl+C, as shells give it.</summary>
    public const int Stopped = 130;

    // A database, a file or a JSON that failed says so in a sentence; the rest say their own message.
    private static string Plain(Exception e) => e switch
    {
        Microsoft.Data.SqlClient.SqlException sql => $"the database answered: {sql.Message}",
        System.Text.Json.JsonException json => $"the JSON could not be read: {json.Message}",
        IOException file => $"a file could not be read or written: {file.Message}",
        System.ComponentModel.Win32Exception process => $"a program could not be started: {process.Message}",
        _ => e.Message,
    };

    private static async Task<int> CouldNotRun(CliIo io, string message, Exception e)
    {
        await io.Error.WriteLineAsync($"bruinet: {message.ReplaceLineEndings(" ")}").ConfigureAwait(false);
        if (Environment.GetEnvironmentVariable("BRUINET_DEBUG") == "1")
        {
            await io.Error.WriteLineAsync(e.ToString()).ConfigureAwait(false);
        }

        return 2;
    }

    /// <summary>Runs a command's work that needs no awaiting.</summary>
    public static void Action(this Command command, CliIo io, Func<ParseResult, int> action) =>
        command.Action(io, (parsed, _) => Task.FromResult(action(parsed)));

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
