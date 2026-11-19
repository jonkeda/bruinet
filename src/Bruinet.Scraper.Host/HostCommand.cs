using System.CommandLine;
using Microsoft.Playwright;

namespace Bruinet.Scraper.Host;

/// <summary>How a command's work runs, with the exit codes every command keeps.</summary>
public static class HostCommand
{
    /// <summary>The exit code of a command stopped by Ctrl+C, as shells give it.</summary>
    public const int Stopped = 130;

    /// <summary>
    /// Runs a command's work: 0 ran and found nothing, 1 ran and found something, 2 could not run, 130 stopped by
    /// Ctrl+C. Anything a command throws is "could not run": one line on stderr, the stack trace only with
    /// <c>BRUINET_DEBUG=1</c>.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="io">Where it writes.</param>
    /// <param name="action">Its work.</param>
    /// <param name="explain">Says a host's own exceptions in a sentence; null, or null for an exception, leaves its message.</param>
    public static void Action(this Command command, HostIo io, Func<ParseResult, CancellationToken, Task<int>> action, Func<Exception, string?>? explain = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(io);
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
                return await CouldNotRun(io, explain?.Invoke(e) ?? Plain(e), e).ConfigureAwait(false);
            }
        });
    }

    /// <summary>Runs a command's work that needs no awaiting.</summary>
    public static void Action(this Command command, HostIo io, Func<ParseResult, int> action) =>
        command.Action(io, (parsed, _) => Task.FromResult(action(parsed)));

    // A file or a JSON that failed says so in a sentence; the rest say their own message.
    private static string Plain(Exception e) => e switch
    {
        System.Text.Json.JsonException json => $"the JSON could not be read: {json.Message}",
        IOException file => $"a file could not be read or written: {file.Message}",
        System.ComponentModel.Win32Exception process => $"a program could not be started: {process.Message}",
        _ => e.Message,
    };

    private static async Task<int> CouldNotRun(HostIo io, string message, Exception e)
    {
        await io.Error.WriteLineAsync($"bruinet: {message.ReplaceLineEndings(" ")}").ConfigureAwait(false);
        if (Environment.GetEnvironmentVariable("BRUINET_DEBUG") == "1")
            await io.Error.WriteLineAsync(e.ToString()).ConfigureAwait(false);

        return 2;
    }
}
