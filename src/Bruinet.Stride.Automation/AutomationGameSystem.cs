using Bruinet.Stride.Automation.Communication;
using Stride.Core;
using Stride.Engine;
using Stride.Games;
using Stride.Input;
using Stride.UI;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Globalization;

namespace Bruinet.Stride.Automation;

/// <summary>
/// Stride GameSystem that integrates automation into the game loop.
/// Most commands (queries, clicks, text) execute directly on the pipe thread for responsiveness.
/// Key simulation commands are dispatched to the game thread via ConcurrentQueue,
/// injecting events into the real keyboard device via HandleKeyDown/HandleKeyUp (public API).
/// </summary>
public class AutomationGameSystem : GameSystemBase
{
    private AutomationServer? _server;
    private IAutomationHandler? _handler;
    private readonly AutomationServerOptions _options;
    private readonly Func<UIElement?>? _uiRootProvider;
    private readonly IAutomationHandler? _customHandler;
    private readonly IGame? _game;
    private bool _initialized;

    private readonly ConcurrentQueue<(AutomationCommand Command, TaskCompletionSource<AutomationResponse> Tcs)> _commandQueue = new();

    // Key simulation — uses the real keyboard device, not a simulated source
    private KeyboardDeviceBase? _keyboard;
    private readonly List<PendingKeyRelease> _pendingKeyReleases = [];
    private TimeSpan _gameTime;

    public AutomationGameSystem(
        IServiceRegistry registry,
        Func<UIElement?> uiRootProvider,
        AutomationServerOptions? options = null,
        IGame? game = null)
        : base(registry)
    {
        _uiRootProvider = uiRootProvider ?? throw new ArgumentNullException(nameof(uiRootProvider));
        _options = options ?? AutomationServerOptions.FromCommandLine();
        _game = game;
    }

    public AutomationGameSystem(
        IServiceRegistry registry,
        IAutomationHandler handler,
        AutomationServerOptions? options = null)
        : base(registry)
    {
        _customHandler = handler ?? throw new ArgumentNullException(nameof(handler));
        _options = options ?? AutomationServerOptions.FromCommandLine();
    }

    public override void Initialize()
    {
        base.Initialize();
        if (_initialized) return;
        _initialized = true;

        Enabled = true;

        if (_customHandler != null)
            _handler = _customHandler;
        else if (_uiRootProvider != null)
            _handler = new StrideUIHandler(_uiRootProvider, game: _game);
        else
            throw new InvalidOperationException("No handler or UI root provider specified");

        var inputManager = Services.GetService<InputManager>();
        if (inputManager?.Keyboard is KeyboardDeviceBase keyboard)
            _keyboard = keyboard;

        var dispatcher = new GameThreadDispatchHandler(this);
        _server = new AutomationServer(dispatcher, _options);
        _server.Start();
    }

    public override void Update(GameTime gameTime)
    {
        _gameTime += gameTime.Elapsed;

        // Lazily acquire keyboard if it wasn't available at Initialize time
        if (_keyboard == null)
        {
            var inputManager = Services.GetService<InputManager>();
            if (inputManager?.Keyboard is KeyboardDeviceBase keyboard)
                _keyboard = keyboard;
        }

        while (_commandQueue.TryDequeue(out var item))
        {
            try
            {
                TryHandleKeySimulation(item.Command, item.Tcs);
            }
            catch (Exception ex)
            {
                item.Tcs.SetResult(AutomationResponse.Fail($"Game thread error: {ex.Message}"));
            }
        }

        for (int i = _pendingKeyReleases.Count - 1; i >= 0; i--)
        {
            if (_gameTime >= _pendingKeyReleases[i].ReleaseAt)
            {
                _keyboard?.HandleKeyUp(_pendingKeyReleases[i].Key);
                _pendingKeyReleases[i].Tcs.SetResult(AutomationResponse.Ok(true));
                _pendingKeyReleases.RemoveAt(i);
            }
        }
    }

    // Minimum hold duration for key press — ensures the key stays down for at least
    // one full frame so that InputManager.IsKeyPressed detects the transition.
    private static readonly TimeSpan MinKeyPressDuration = TimeSpan.FromMilliseconds(32);

    private bool TryHandleKeySimulation(AutomationCommand command, TaskCompletionSource<AutomationResponse> tcs)
    {
        if (command.Type != "Action") return false;

        if (command.Method is not ("SimulateKeyPress" or "SimulateKeyDown" or "SimulateKeyUp" or "SimulateKeyHold" or "SimulateKeyCombination"))
            return false;

        if (_keyboard == null)
        {
            tcs.SetResult(AutomationResponse.Fail("KeyboardSimulationNotAvailable"));
            return true;
        }

        if (command.Method == "SimulateKeyCombination")
        {
            var keys = new List<Keys>();
            for (int i = 0; i < (command.Args?.Length ?? 0); i++)
            {
                var kn = GetArgString(command.Args, i);
                if (kn != null && Enum.TryParse<Keys>(kn, out var k))
                    keys.Add(k);
            }
            if (keys.Count == 0)
            {
                tcs.SetResult(AutomationResponse.Fail("No valid keys specified for combination"));
                return true;
            }
            foreach (var k in keys)
                _keyboard.HandleKeyDown(k);
            for (int i = keys.Count - 1; i > 0; i--)
                _pendingKeyReleases.Add(new PendingKeyRelease(
                    keys[i], _gameTime + MinKeyPressDuration,
                    new TaskCompletionSource<AutomationResponse>(TaskCreationOptions.RunContinuationsAsynchronously)));
            _pendingKeyReleases.Add(new PendingKeyRelease(
                keys[0], _gameTime + MinKeyPressDuration, tcs));
            return true;
        }

        var keyName = GetArgString(command.Args, 0);
        if (!Enum.TryParse<Keys>(keyName, out var key))
        {
            tcs.SetResult(AutomationResponse.Fail($"Unknown Stride key: {keyName}"));
            return true;
        }

        switch (command.Method)
        {
            case "SimulateKeyPress":
                // Hold key down for at least one frame so InputManager.IsKeyPressed detects it
                _keyboard.HandleKeyDown(key);
                _pendingKeyReleases.Add(new PendingKeyRelease(
                    key, _gameTime + MinKeyPressDuration, tcs));
                break;

            case "SimulateKeyDown":
                _keyboard.HandleKeyDown(key);
                tcs.SetResult(AutomationResponse.Ok(true));
                break;

            case "SimulateKeyUp":
                _keyboard.HandleKeyUp(key);
                tcs.SetResult(AutomationResponse.Ok(true));
                break;

            case "SimulateKeyHold":
                var durationMs = GetArgInt(command.Args, 1);
                _keyboard.HandleKeyDown(key);
                // Defer TCS completion until the key is released after the hold duration
                _pendingKeyReleases.Add(new PendingKeyRelease(
                    key, _gameTime + TimeSpan.FromMilliseconds(durationMs), tcs));
                break;
        }

        return true;
    }

    private static string? GetArgString(object[]? args, int index)
    {
        if (args == null || args.Length <= index) return null;
        var arg = args[index];
        if (arg is JsonElement je) return je.GetString() ?? je.GetRawText();
        return arg?.ToString();
    }

    private static int GetArgInt(object[]? args, int index)
    {
        if (args == null || args.Length <= index) return 0;
        var arg = args[index];
        if (arg is JsonElement je)
        {
            if (je.TryGetInt32(out var i)) return i;
            if (je.TryGetDouble(out var d)) return (int)d;
            return 0;
        }
        return Convert.ToInt32(arg, CultureInfo.InvariantCulture);
    }

    protected override void Destroy()
    {
        _server?.StopAsync().GetAwaiter().GetResult();
        _server?.Dispose();
        base.Destroy();
    }

    /// <summary>
    /// IAutomationHandler that routes commands: key simulation goes to the game thread
    /// via ConcurrentQueue (needs game-thread timing), everything else is handled directly
    /// on the pipe thread by the real handler (as in the original working implementation).
    /// </summary>
    private sealed class GameThreadDispatchHandler(AutomationGameSystem gameSystem) : IAutomationHandler
    {
        public Task<AutomationResponse> HandleCommandAsync(AutomationCommand command, CancellationToken cancellationToken = default)
        {
            // Key simulation must run on the game thread (needs InputManager timing)
            if (command.Type == "Action" && command.Method is "SimulateKeyPress" or "SimulateKeyDown" or "SimulateKeyUp" or "SimulateKeyHold" or "SimulateKeyCombination")
            {
                var tcs = new TaskCompletionSource<AutomationResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
                gameSystem._commandQueue.Enqueue((command, tcs));
                return tcs.Task;
            }

            // All other commands execute directly on the pipe thread (safe and responsive)
            return gameSystem._handler!.HandleCommandAsync(command, cancellationToken);
        }
    }

    private record PendingKeyRelease(Keys Key, TimeSpan ReleaseAt, TaskCompletionSource<AutomationResponse> Tcs);
}
