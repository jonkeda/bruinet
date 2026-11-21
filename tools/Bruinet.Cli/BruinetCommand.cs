using System.CommandLine;
using System.CommandLine.Help;
using Bruinet.Bdd.Workspace;
using Bruinet.Cli.Data;
using Bruinet.Cli.Generate;
using Bruinet.Cli.Scenario;
using Bruinet.Cli.Selection;
using Bruinet.Cli.Servers;
using Bruinet.Scraper.Snapshots;
using Bruinet.DataValidator;
using Bruinet.Scraper.Service;
using Microsoft.Playwright;

namespace Bruinet.Cli;

/// <summary><c>bruinet</c>: the command tree, and running one command. Exit 0 fine, 1 problems found, 2 could not run.</summary>
internal static class BruinetCommand
{
    public static RootCommand Build(CliIo io)
    {
        var root = new RootCommand("Bruinet's command line: the Scraper, test data and scenarios, spec files, control generation, and the servers the IDEs and agents start.")
        {
            Bruinet.Scraper.Host.ScrapeCommands.Build(io with { Services = ScraperServices(io) }),
            DataCommands.Build(io),
            ScenarioCommands.Build(io),
            SelectionCommands.Build(io),
            Friction.FrictionCommands.Build(io),
            Suite.SuiteCommands.Build(io),
            Spec.SpecCommands.Build(io),
            Sql.SqlCommands.Build(io),
            Http.HttpCommands.Build(io),
            Upgrade.UpgradeCommand.Build(io),
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
        Bruinet.Scraper.Host.HostCommand.Action(command, io, action, e => e is Microsoft.Data.SqlClient.SqlException sql ? $"the database answered: {sql.Message}" : null);

    /// <summary>The exit code of a command stopped by Ctrl+C, as shells give it.</summary>
    public const int Stopped = Bruinet.Scraper.Host.HostCommand.Stopped;

    // What bruinet gives the Scraper's commands: its control template generator and its reading of a suite's page objects.
    private static Bruinet.Scraper.Host.HostServices ScraperServices(CliIo io) => io.Services with
    {
        Templates = io.Services.Templates ?? new Generate.ControlTemplates(),
        SuiteAddresses = io.Services.SuiteAddresses
            ?? (suite => [.. Suite.SuitePages.Read(suite, null).Pages.Where(page => page.Address is not null).Select(page => page.Address!)]),
    };

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
