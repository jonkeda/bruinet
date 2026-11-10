using System.Text.Json;

namespace Bruinet.Stride.Automation;

/// <summary>
/// The automation pipe to the Stride game, as the call model asks of a driver: each read and lookup
/// one command, actions that fail loudly, and a game that is gone (the pipe broken or closed)
/// reported as <see cref="AppUnavailableException"/> rather than as an element that is not there.
/// </summary>
internal sealed class StrideAutomationDriver : IStrideDriver
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IAutomationChannel _channel;
    private readonly Func<nint> _windowHandle;

    /// <summary>The driver over an open automation channel.</summary>
    /// <param name="channel">The connected pipe to the game.</param>
    /// <param name="windowHandle">The game window's handle, for a recorder; zero when unknown.</param>
    public StrideAutomationDriver(IAutomationChannel channel, Func<nint>? windowHandle = null)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _windowHandle = windowHandle ?? (() => 0);
    }

    /// <inheritdoc />
    public nint WindowHandle => _windowHandle();

    /// <inheritdoc />
    public ElementState GetState(string target) => Read<ElementState>(AutomationCommand.Query("GetState", target)) ?? Missing(target);

    /// <inheritdoc />
    public ElementState Find(string scope, string target) => Read<ElementState>(AutomationCommand.Query("Find", scope, target)) ?? Missing(target);

    /// <inheritdoc />
    public IReadOnlyList<ElementState> FindAll(string scope, string target) => Read<List<ElementState>>(AutomationCommand.Query("FindAll", scope, target)) ?? [];

    /// <inheritdoc />
    public IReadOnlyList<ElementState> GetChildren(string scope) => Read<List<ElementState>>(AutomationCommand.Query("GetChildren", scope)) ?? [];

    /// <inheritdoc />
    public void Act(string method, string target, params object[] args)
    {
        var response = Send(AutomationCommand.Action(method, target, args));
        if (!response.Success)
        {
            throw new InvalidOperationException($"The game refused {method} on '{target}': {response.Error}");
        }
    }

    /// <inheritdoc />
    public bool IsGameReady() => IsTrue(Send(AutomationCommand.Query("IsGameReady")));

    /// <inheritdoc />
    public bool IsGameBusy() => IsTrue(Send(AutomationCommand.Query("IsBusy")));

    /// <inheritdoc />
    public void PressKey(VirtualKey key) => Act("SimulateKeyPress", string.Empty, StrideKeyName(key));

    /// <inheritdoc />
    public void HoldKey(VirtualKey key, int durationMs) => Act("SimulateKeyHold", string.Empty, StrideKeyName(key), durationMs);

    /// <inheritdoc />
    public byte[] TakeScreenshot()
    {
        var response = Send(AutomationCommand.Action("TakeScreenshot"));
        var value = response.Success ? response.Result switch
        {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
            _ => null,
        } : null;

        if (string.IsNullOrWhiteSpace(value))
            return [];

        return File.Exists(value) ? File.ReadAllBytes(value) : Convert.FromBase64String(value);
    }

    /// <inheritdoc />
    public void Dispose() => _channel.Dispose();

    private T? Read<T>(AutomationCommand command)
        where T : class
    {
        var response = Send(command);
        if (!response.Success || response.Result is null)
            return null;

        var json = response.Result is JsonElement element ? element.GetRawText() : JsonSerializer.Serialize(response.Result, Json);
        return JsonSerializer.Deserialize<T>(json, Json);
    }

    // One command. A pipe that is closed or broken means the game is gone (R0: end the call).
    private AutomationResponse Send(AutomationCommand command)
    {
        if (!_channel.IsConnected)
            throw new AppUnavailableException("the game's automation pipe is closed.");

        AutomationResponse response;
        try
        {
            response = _channel.SendCommandAsync(command).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException error)
        {
            throw new AppUnavailableException("the game's automation pipe is not connected.", error);
        }

        if (!response.Success && response.Error?.StartsWith("Pipe communication error", StringComparison.Ordinal) == true)
            throw new AppUnavailableException($"the game stopped answering ({response.Error}).");

        return response;
    }

    private static ElementState Missing(string target) => new() { AutomationId = target, Exists = false };

    private static bool IsTrue(AutomationResponse response)
        => response.Success && response.Result switch
        {
            bool value => value,
            JsonElement { ValueKind: JsonValueKind.True } => true,
            _ => false,
        };

    private static string StrideKeyName(VirtualKey key) => key switch
    {
        VirtualKey.Backspace => "Back",
        VirtualKey.Control => "LeftCtrl",
        VirtualKey.LeftControl => "LeftCtrl",
        VirtualKey.RightControl => "RightCtrl",
        _ => key.ToString(),
    };
}
