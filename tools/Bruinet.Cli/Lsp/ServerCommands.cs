using System.CommandLine;
using Bruinet.Bdd.LanguageServer;

namespace Bruinet.Cli.Servers;

/// <summary><c>bruinet mcp</c> and <c>bruinet lsp</c>: the servers other programs start, over stdio.</summary>
internal static class ServerCommands
{
    /// <summary>The Bruinet MCP server for Copilot and other agents.</summary>
    public static Command Mcp(CliIo io)
    {
        var solution = CommonOptions.Solution();
        var command = new Command("mcp", "For tools: the MCP server for agents, over stdio.") { solution };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            // The protocol owns stdout: anything else written to the console goes to stderr.
            var protocol = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            Console.SetOut(Console.Error);
            using var mcp = new BddMcpServer(CommonOptions.SolutionFolder(parsed.GetValue(solution), io.CurrentDirectory));
            await mcp.RunAsync(Console.In, protocol, cancellationToken).ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    /// <summary>
    /// The MCP server for a session while Bruinet is developed: runs <c>bruinet mcp</c> from a snapshot of a build folder,
    /// and again on each new build there, the session kept.
    /// </summary>
    public static Command McpHost(CliIo io)
    {
        var solution = CommonOptions.Solution();
        var build = new Option<string>("--build") { Description = "The build folder to run and watch (.bruinet/mcp).", Required = true };
        var runs = new Option<string?>("--runs") { Description = "Where the snapshots go. Default: the build folder's sibling 'run'." };
        var command = new Command("mcp-host", "For tools: the MCP server, run from a build folder and started again on each new build there.") { solution, build, runs };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            var protocol = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            Console.SetOut(Console.Error);
            var buildFolder = Path.GetFullPath(parsed.GetValue(build)!, io.CurrentDirectory);
            var runFolder = parsed.GetValue(runs) is { } given ? Path.GetFullPath(given, io.CurrentDirectory) : Path.Combine(Path.GetDirectoryName(buildFolder.TrimEnd('\\', '/'))!, "run");
            var solutionFolder = CommonOptions.SolutionFolder(parsed.GetValue(solution), io.CurrentDirectory);
            using var host = new McpReloadingHost(buildFolder, runFolder, ["mcp", "--solution", solutionFolder]);
            host.Reloaded += stamp => Console.Error.WriteLine($"bruinet mcp-host: a new build ({stamp}), the server started again.");
            await host.RunAsync(Console.In, protocol, cancellationToken).ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    /// <summary>The language server every IDE talks to about feature and step files, and the Scraper view.</summary>
    public static Command Lsp()
    {
        var command = new Command("lsp", "For tools: the language server the IDE extensions start, over stdio.");
        command.SetAction(async (_, _) =>
        {
            using var server = new BddLanguageServer();
            using var rpc = server.Attach(Console.OpenStandardOutput(), Console.OpenStandardInput());
            var exit = new TaskCompletionSource();
            server.ExitRequested += (_, _) => exit.TrySetResult();
            await Task.WhenAny(rpc.Completion, exit.Task).ConfigureAwait(false);
            return 0;
        });
        return command;
    }
}
