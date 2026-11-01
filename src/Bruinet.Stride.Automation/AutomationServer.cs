using Bruinet.Stride.Automation.Communication;
using System.IO.Pipes;
using System.Text.Json;

namespace Bruinet.Stride.Automation;

/// <summary>
/// Options for the automation server.
/// </summary>
public class AutomationServerOptions
{
    public string PipeName { get; set; } = "Bruinet.Stride.Automation";
    public int MaxConnections { get; set; } = 4;
    public bool VerboseLogging { get; set; } = false;

    public static AutomationServerOptions FromCommandLine(string[]? args = null)
    {
        args ??= Environment.GetCommandLineArgs();
        var options = new AutomationServerOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--pipe", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.PipeName = args[i + 1];
                break;
            }

            const string pipePrefix = "--pipe=";
            if (arg.StartsWith(pipePrefix, StringComparison.OrdinalIgnoreCase))
            {
                options.PipeName = arg[pipePrefix.Length..];
                break;
            }
        }

        return options;
    }
}

/// <summary>
/// Named pipe server that runs in the game process to receive automation commands.
/// </summary>
public sealed class AutomationServer : IDisposable
{
    private readonly AutomationServerOptions _options;
    private readonly IAutomationHandler _handler;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _listenerTasks = [];
    private bool _isRunning;
    private bool _disposed;

    public bool IsRunning => _isRunning;

    public AutomationServer(IAutomationHandler handler, AutomationServerOptions? options = null)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _options = options ?? new AutomationServerOptions();
    }

    public void Start()
    {
        if (_isRunning)
            return;
        _isRunning = true;

        for (int i = 0; i < _options.MaxConnections; i++)
        {
            var task = Task.Run(() => ListenAsync(_cts.Token));
            _listenerTasks.Add(task);
        }

        Log($"Automation server started on pipe '{_options.PipeName}' with {_options.MaxConnections} listeners");
    }

    public async Task StopAsync()
    {
        if (!_isRunning)
            return;
        _isRunning = false;
        _cts.Cancel();

        try
        {
            await Task.WhenAll(_listenerTasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }

        _listenerTasks.Clear();
        Log("Automation server stopped");
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        int consecutiveErrors = 0;
        const int maxConsecutiveErrors = 5;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var pipeServer = new NamedPipeServerStream(
                    _options.PipeName,
                    PipeDirection.InOut,
                    _options.MaxConnections,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                Log("Waiting for connection...");
                consecutiveErrors = 0;

                await pipeServer.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                Log("Client connected");

                try
                {
                    await HandleConnectionAsync(pipeServer, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log($"Error handling connection: {ex.Message}");
                }
                finally
                {
                    if (pipeServer.IsConnected)
                    {
                        pipeServer.Disconnect();
                    }
                    Log("Client disconnected");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException ex) when (ex.Message.Contains("All pipe instances are busy"))
            {
                Log("Pipe instances busy - this listener will stop");
                break;
            }
            catch (Exception ex)
            {
                consecutiveErrors++;
                Log($"Error in listener ({consecutiveErrors}/{maxConsecutiveErrors}): {ex.Message}");

                if (consecutiveErrors >= maxConsecutiveErrors)
                {
                    Log("Too many consecutive errors, stopping listener");
                    break;
                }

                var delay = Math.Min(1000 * consecutiveErrors, 5000);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        var reader = new StreamReader(pipe);
        var writer = new StreamWriter(pipe) { AutoFlush = true };

        while (pipe.IsConnected && !cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (string.IsNullOrEmpty(line))
                break;

            Log($"Received: {line}");

            AutomationResponse response;
            try
            {
                var command = JsonSerializer.Deserialize<AutomationCommand>(line);
                if (command != null)
                {
                    response = await _handler.HandleCommandAsync(command, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    response = AutomationResponse.Fail("Failed to parse command");
                }
            }
            catch (Exception ex)
            {
                response = AutomationResponse.Fail($"Error: {ex.Message}");
            }

            var responseJson = JsonSerializer.Serialize(response);
            Log($"Sending: {responseJson}");
            await writer.WriteLineAsync(responseJson).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void Log(string message)
    {
        if (_options.VerboseLogging)
        {
            Console.WriteLine($"[AutomationServer] {message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }
}
