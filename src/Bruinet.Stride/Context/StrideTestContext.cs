using System.Text.Json;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Logging;
using Bruinet.Stride.Communication;
using Bruinet.Stride.Infrastructure;
using Bruinet.Stride.Interfaces;

namespace Bruinet.Stride.Context;

/// <summary>
/// Test context for Stride game engine UI testing.
/// Implements both the new IStrideTestContext and the Core ITestContext.
/// </summary>
public class StrideTestContext : IStrideTestContext
{
    private readonly IAutomationChannel _channel;
    private readonly StrideTestContextOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    public TimeoutSettings Timeouts { get; }
    public ITestLogger Logger { get; }
    public bool IsGameReady => CheckGameReady();

    public StrideTestContext(IAutomationChannel channel, StrideTestContextOptions? options = null)
        : this(channel, new ConsoleTestLogger(), options)
    {
    }

    public StrideTestContext(IAutomationChannel channel, ITestLogger logger, StrideTestContextOptions? options = null)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new StrideTestContextOptions();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        Timeouts = _options.ToTimeoutSettings();
    }

    #region ITestContext navigation (not meaningful for Stride, but required)

    public void NavigateTo(string destination) { /* No-op for Stride */ }
    public void NavigateBack() { /* No-op for Stride */ }
    public void Refresh() { /* No-op for Stride */ }

    public byte[] TakeScreenshot()
    {
        var response = SendCommand(AutomationCommand.Action("TakeScreenshot"));
        if (response.Success && response.Result is string base64)
        {
            if (File.Exists(base64))
                return File.ReadAllBytes(base64);

            return Convert.FromBase64String(base64);
        }

        if (response.Success && response.Result is JsonElement json && json.ValueKind == JsonValueKind.String)
        {
            var value = json.GetString();
            if (!string.IsNullOrWhiteSpace(value) && File.Exists(value))
                return File.ReadAllBytes(value);

            if (!string.IsNullOrWhiteSpace(value))
                return Convert.FromBase64String(value);
        }

        return [];
    }

    public void SaveScreenshot(string path)
    {
        var bytes = TakeScreenshot();
        if (bytes.Length > 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllBytes(path, bytes);
        }
    }

    public void ResetAppState() { /* No-op for Stride */ }

    #endregion

    #region Element Operations

    public ElementState GetElementState(string automationId)
    {
        var response = SendCommand(AutomationCommand.Query("GetState", automationId));

        if (!response.Success || response.Result == null)
            return new ElementState { Exists = false };

        try
        {
            var json = response.Result.ToString()!;
            return JsonSerializer.Deserialize<ElementState>(json, _jsonOptions)
                ?? new ElementState { Exists = false };
        }
        catch
        {
            return new ElementState { Exists = false };
        }
    }

    public bool ElementExists(string automationId)
        => GetElementState(automationId).Exists;

    public bool ElementIsVisible(string automationId)
    {
        var state = GetElementState(automationId);
        return state.Exists && state.IsVisible;
    }

    public void ClickElement(string automationId)
    {
        // Server-side click via automation pipe — no physical mouse needed
        var response = SendCommand(AutomationCommand.Action("Click", automationId));
        if (!response.Success)
            throw new InvalidOperationException($"Server-side click failed for '{automationId}': {response.Error}");
    }

    public bool SetElementText(string automationId, string text)
    {
        var response = SendCommand(AutomationCommand.Action("SetElementText", automationId, text));
        return response.Success;
    }

    public bool SetSliderValue(string automationId, double value)
    {
        var response = SendCommand(AutomationCommand.Action("SetSliderValue", automationId, value));
        return response.Success;
    }

    public bool SetToggleValue(string automationId, bool value)
    {
        var response = SendCommand(AutomationCommand.Action("SetToggleValue", automationId, value));
        return response.Success;
    }

    #endregion

    #region Input

    public void PressKey(VirtualKey key)
    {
        var strideKeyName = MapVirtualKeyToStrideKeyName(key);
        var response = SendCommand(AutomationCommand.Action("SimulateKeyPress", null, strideKeyName));
        if (!response.Success)
            throw new InvalidOperationException($"Server-side key press failed for '{key}': {response.Error}");
    }

    public void HoldKey(VirtualKey key, int durationMs)
    {
        var strideKeyName = MapVirtualKeyToStrideKeyName(key);
        var response = SendCommand(AutomationCommand.Action("SimulateKeyHold", null, strideKeyName, durationMs));
        if (!response.Success)
            throw new InvalidOperationException($"Server-side key hold failed for '{key}': {response.Error}");
    }

    private static string MapVirtualKeyToStrideKeyName(VirtualKey key) => key switch
    {
        VirtualKey.Backspace => "Back",
        VirtualKey.Control => "LeftCtrl",
        VirtualKey.LeftControl => "LeftCtrl",
        VirtualKey.RightControl => "RightCtrl",
        _ => key.ToString()
    };

    #endregion

    #region Game State

    private bool CheckGameReady()
    {
        try
        {
            var response = SendCommand(AutomationCommand.Query("IsGameReady"));
            return response.Success && IsResultTrue(response.Result);
        }
        catch
        {
            return false;
        }
    }

    public bool IsGameBusy()
    {
        try
        {
            var response = SendCommand(AutomationCommand.Query("IsBusy"));
            return response.Success && IsResultTrue(response.Result);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsResultTrue(object? result)
    {
        if (result is bool b)
            return b;
        if (result is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.True)
            return true;
        return false;
    }

    public bool WaitForGameReady(int? timeoutMs = null)
        => WaitFor(() => IsGameReady, timeoutMs ?? _options.StartupTimeoutMs, "game ready");

    #endregion

    #region Wait / Command

    public bool WaitFor(Func<bool> condition, int? timeoutMs = null, string description = "condition")
    {
        return LegacyWaitHelper.WaitFor(
            condition,
            timeoutMs ?? Timeouts.DefaultWait,
            Timeouts.PollingInterval);
    }

    public AutomationResponse SendCommand(AutomationCommand command)
        => _channel.SendCommandAsync(command).GetAwaiter().GetResult();

    #endregion

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _channel.Dispose();
        GC.SuppressFinalize(this);
    }
}
