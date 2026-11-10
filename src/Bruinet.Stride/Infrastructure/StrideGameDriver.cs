using Bruinet.Stride.Communication;
using Bruinet.Stride.Context;

namespace Bruinet.Stride.Infrastructure;

/// <summary>
/// Manages the lifecycle of the Stride game process for testing.
/// </summary>
public class StrideGameDriver : IDisposable
{
    private readonly StrideTestContextOptions _options;
    private Process? _gameProcess;
    private bool _disposed;

    /// <summary>
    /// The automation channel for communicating with the game.
    /// </summary>
    public IAutomationChannel? Channel { get; private set; }

    /// <summary>
    /// Whether the game is currently running.
    /// </summary>
    public bool IsRunning => _gameProcess?.HasExited == false;

    /// <summary>
    /// The game window handle (Windows only).
    /// </summary>
    public IntPtr GameWindowHandle => _gameProcess?.MainWindowHandle ?? IntPtr.Zero;

    public StrideGameDriver(StrideTestContextOptions? options = null)
    {
        _options = options ?? new StrideTestContextOptions();
    }

    /// <summary>
    /// Start the game and connect to automation.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.AttachToExisting)
            await StartGameProcessAsync(cancellationToken);

        await ConnectToAutomationAsync(cancellationToken);
    }

    /// <summary>
    /// Stop the game.
    /// </summary>
    public async Task StopAsync()
    {
        if (Channel?.IsConnected == true)
        {
            try
            {
                await Channel.SendCommandAsync(AutomationCommand.Action("Exit"));
                await Channel.DisconnectAsync();
            }
            catch
            {
                // Ignore errors during shutdown
            }
        }

        if (_gameProcess is { HasExited: false })
        {
            using var cts = new CancellationTokenSource(2000);
            try
            {
                await _gameProcess.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                { _gameProcess.Kill(entireProcessTree: true); }
                catch { /* Process may have exited */ }
            }
        }

        _gameProcess?.Dispose();
        _gameProcess = null;
        Channel = null;
    }

    private async Task StartGameProcessAsync(CancellationToken cancellationToken)
    {
        var gamePath = _options.GameExecutablePath
            ?? throw new InvalidOperationException("GameExecutablePath must be specified when not attaching to existing process");

        if (!File.Exists(gamePath))
            throw new FileNotFoundException($"Game executable not found: {gamePath}");

        var startInfo = new ProcessStartInfo
        {
            FileName = gamePath,
            Arguments = string.Join(" ", _options.GameArguments),
            UseShellExecute = false
        };

        _gameProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start game process");

        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < _options.StartupTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_gameProcess.HasExited)
                throw new InvalidOperationException($"Game process exited unexpectedly with code {_gameProcess.ExitCode}");

            _gameProcess.Refresh();
            if (_gameProcess.MainWindowHandle != IntPtr.Zero)
                break;

            await Task.Delay(100, cancellationToken);
        }
    }

    private async Task ConnectToAutomationAsync(CancellationToken cancellationToken)
    {
        Channel = new NamedPipeChannel(_options.PipeName);
        var timeout = _options.ConnectionTimeout;
        await Channel.ConnectAsync(timeout, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        StopAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}
