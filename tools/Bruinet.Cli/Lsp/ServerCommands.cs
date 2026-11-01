using System.CommandLine;
using Bruinet.Bdd.LanguageServer;

namespace Bruinet.Cli.Servers;

/// <summary><c>bruinet mcp</c> and <c>bruinet lsp</c>: the servers other programs start, over stdio.</summary>
internal static class ServerCommands
{
    /// <summary>The Bruinet MCP server for Copilot and other agents.</summary>
    public static Command Mcp(CliIo io)
    {
        var solution = new Option<string?>("--solution") { Description = "The solution's folder. Default: this folder." };
        var command = new Command("mcp", "For tools: the MCP server for agents, over stdio.") { solution };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            // The protocol owns stdout: anything else written to the console goes to stderr.
            var protocol = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            Console.SetOut(Console.Error);
            using var mcp = new BddMcpServer(Path.GetFullPath(parsed.GetValue(solution) ?? io.CurrentDirectory, io.CurrentDirectory));
            await mcp.RunAsync(Console.In, protocol, cancellationToken).ConfigureAwait(false);
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
