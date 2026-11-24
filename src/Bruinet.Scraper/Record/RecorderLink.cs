using System.IO.Pipes;
using System.Text.Json;

namespace Bruinet.Scraper.Record;

/// <summary>What is asked of a running Recorder.</summary>
/// <param name="Do"><c>status</c>, <c>open</c>, <c>start</c>, <c>stop</c> or <c>wait</c>.</param>
/// <param name="Folder">For <c>open</c>: the Scraper project to save in; null leaves it.</param>
/// <param name="Url">For <c>open</c>: the address to go to; null leaves it.</param>
/// <param name="Minutes">For <c>wait</c>: how long at most.</param>
public sealed record RecorderRequest(string Do, string? Folder = null, string? Url = null, double Minutes = 0);

/// <summary>How a Recorder is, as it answers every request.</summary>
/// <param name="Recording">A recording is on.</param>
/// <param name="Folder">The project it saves in.</param>
/// <param name="Address">Where its page is.</param>
/// <param name="Session">The session being written, or the last one written.</param>
/// <param name="Pages">The pages captured in it.</param>
/// <param name="Presses">The presses recorded in it.</param>
/// <param name="Values">Whether values are recorded.</param>
/// <param name="Ended">For <c>wait</c>: a recording ended while waiting.</param>
/// <param name="Problem">Why the request was not done, or what went wrong while recording; null when nothing.</param>
public sealed record RecorderStatus(bool Recording, string? Folder, string? Address, string? Session, int Pages, int Presses, bool Values, bool Ended = false, string? Problem = null);

/// <summary>
/// The line between a running Recorder and the command line or the MCP server: a pipe on this machine, open to
/// the same user only. A request is one line of JSON, and so is its answer. Nothing on it presses or types in
/// the page.
/// </summary>
public static class RecorderLink
{
    /// <summary>Set to another name, the pipe has that name: for a test's own Recorder.</summary>
    public const string PipeVariable = "BRUINET_RECORDER_PIPE";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The pipe's name.</summary>
    public static string PipeName => Environment.GetEnvironmentVariable(PipeVariable) is { Length: > 0 } name ? name : "bruinet-recorder";

    /// <summary>Answers requests until cancelled, each on its own, so one that waits does not hold up the next.</summary>
    /// <param name="answer">Does what is asked and says how the Recorder is then.</param>
    /// <param name="cancellationToken">Ends it.</param>
    /// <param name="pipe">The pipe's name; <see cref="PipeName"/> when null.</param>
    public static async Task ServeAsync(Func<RecorderRequest, CancellationToken, Task<RecorderStatus>> answer, CancellationToken cancellationToken, string? pipe = null)
    {
        ArgumentNullException.ThrowIfNull(answer);
        while (!cancellationToken.IsCancellationRequested)
        {
            var server = new NamedPipeServerStream(pipe ?? PipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await server.DisposeAsync().ConfigureAwait(false);
                return;
            }

            _ = AnswerAsync(server, answer, cancellationToken);
        }
    }

    /// <summary>Asks the running Recorder. Null when none is running.</summary>
    /// <param name="request">What to do.</param>
    /// <param name="cancellationToken">Stops waiting for the answer.</param>
    /// <param name="pipe">The pipe's name; <see cref="PipeName"/> when null.</param>
    public static async Task<RecorderStatus?> AskAsync(RecorderRequest request, CancellationToken cancellationToken = default, string? pipe = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var client = new NamedPipeClientStream(".", pipe ?? PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await using (client.ConfigureAwait(false))
        {
            try
            {
                await client.ConnectAsync(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                return null;
            }

            using var reader = new StreamReader(client, leaveOpen: true);
            var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
            await using (writer.ConfigureAwait(false))
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(request, Json).AsMemory(), cancellationToken).ConfigureAwait(false);
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                return line is null ? null : JsonSerializer.Deserialize<RecorderStatus>(line, Json);
            }
        }
    }

    private static async Task AnswerAsync(NamedPipeServerStream server, Func<RecorderRequest, CancellationToken, Task<RecorderStatus>> answer, CancellationToken cancellationToken)
    {
        await using (server.ConfigureAwait(false))
        {
            try
            {
                using var reader = new StreamReader(server, leaveOpen: true);
                var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };
                await using (writer.ConfigureAwait(false))
                {
                    var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (line is null || JsonSerializer.Deserialize<RecorderRequest>(line, Json) is not { } request)
                        return;
                    var status = await answer(request, cancellationToken).ConfigureAwait(false);
                    await writer.WriteLineAsync(JsonSerializer.Serialize(status, Json).AsMemory(), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception e) when (e is IOException or JsonException or OperationCanceledException)
            {
                // The one who asked went away, or sent something else than a request: nothing to answer.
            }
        }
    }
}
