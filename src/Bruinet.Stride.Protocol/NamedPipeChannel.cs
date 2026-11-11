using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Bruinet.Stride.Communication;

/// <summary>
/// Named pipe implementation of IAutomationChannel for cross-process communication.
/// </summary>
public class NamedPipeChannel : IAutomationChannel
{
    private readonly string _pipeName;
    private readonly JsonSerializerOptions _jsonOptions;
    private NamedPipeClientStream? _pipeClient;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public const string DefaultPipeName = "Bruinet.Stride.Automation";

    public bool IsConnected => _pipeClient?.IsConnected ?? false;

    public NamedPipeChannel(string? pipeName = null)
    {
        _pipeName = pipeName ?? DefaultPipeName;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (IsConnected)
            return;

        _pipeClient = new NamedPipeClientStream(
            ".",
            _pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await _pipeClient.ConnectAsync(linkedCts.Token);
            _reader = new StreamReader(_pipeClient, Encoding.UTF8, leaveOpen: true);
            _writer = new StreamWriter(_pipeClient, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
        }
        catch (OperationCanceledException exception) when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException($"Failed to connect to pipe '{_pipeName}' within {timeout.TotalMilliseconds}ms", exception);
        }
    }

    public async Task<AutomationResponse> SendCommandAsync(AutomationCommand command, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _writer == null || _reader == null)
            throw new InvalidOperationException("Not connected to game. Call ConnectAsync first.");

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var json = JsonSerializer.Serialize(command, _jsonOptions);
            await _writer.WriteLineAsync(json.AsMemory(), linkedCts.Token);
            await _writer.FlushAsync(linkedCts.Token);

            var responseJson = await _reader.ReadLineAsync(linkedCts.Token);
            if (string.IsNullOrEmpty(responseJson))
                return AutomationResponse.Fail("Empty response from game");

            return JsonSerializer.Deserialize<AutomationResponse>(responseJson, _jsonOptions)
                ?? AutomationResponse.Fail("Failed to deserialize response");
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            return AutomationResponse.Fail("Timeout waiting for game response");
        }
        catch (IOException ex)
        {
            return AutomationResponse.Fail($"Pipe communication error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return AutomationResponse.Fail($"Invalid JSON response: {ex.Message}");
        }
    }

    public Task DisconnectAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _pipeClient?.Dispose();

        _reader = null;
        _writer = null;
        _pipeClient = null;

        GC.SuppressFinalize(this);
    }
}
