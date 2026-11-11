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

        var game = _gameProcess;
        await WaitForWindowAsync(
            gamePath,
            () => game.HasExited ? game.ExitCode : null,
            () =>
            {
                game.Refresh();
                return game.MainWindowHandle != IntPtr.Zero;
            },
            _options.StartupTimeout,
            cancellationToken);
    }

    /// <summary>
    /// Waits until the started game shows its window. A game that exits first, or shows none within
    /// <paramref name="startupTimeout"/>, fails here by name rather than later as a pipe error.
    /// </summary>
    /// <param name="gamePath">The game's executable, for the message.</param>
    /// <param name="exitCode">The game's exit code once it has exited; null while it runs.</param>
    /// <param name="hasWindow">Whether the game shows its main window.</param>
    /// <param name="startupTimeout">How long the game may take (<see cref="StrideTestContextOptions.StartupTimeout"/>).</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    internal static async Task WaitForWindowAsync(string gamePath, Func<int?> exitCode, Func<bool> hasWindow, TimeSpan startupTimeout, CancellationToken cancellationToken)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (exitCode() is { } code)
                throw new InvalidOperationException($"The game '{Path.GetFileName(gamePath)}' exited with code {code} before it showed a window.");

            if (hasWindow())
                return;

            if (started.Elapsed >= startupTimeout)
                throw new TimeoutException($"The game '{Path.GetFileName(gamePath)}' showed no window within StartupTimeout ({startupTimeout.TotalSeconds:0.#} s).");

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
