using Bruinet.Stride.Communication;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Events;
using Stride.UI.Panels;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Globalization;

namespace Bruinet.Stride.Automation;

/// <summary>
/// Default handler for Stride UI automation commands.
/// Processes element queries, actions, and game-level queries from the test process.
/// </summary>
public class StrideUIHandler : IAutomationHandler
{
    private readonly Func<UIElement?> _rootProvider;
    private readonly Func<bool>? _isReadyProvider;
    private readonly Func<bool>? _isBusyProvider;
    private readonly IGame? _game;

    public StrideUIHandler(
        Func<UIElement?> rootProvider,
        Func<bool>? isReadyProvider = null,
        Func<bool>? isBusyProvider = null,
        IGame? game = null)
    {
        _rootProvider = rootProvider ?? throw new ArgumentNullException(nameof(rootProvider));
        _isReadyProvider = isReadyProvider;
        _isBusyProvider = isBusyProvider;
        _game = game;
    }

    public Task<AutomationResponse> HandleCommandAsync(AutomationCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = command.Type switch
            {
                "Query" => HandleQuery(command),
                "Action" => HandleAction(command),
                "GameQuery" => HandleGameQuery(command),
                _ => AutomationResponse.Fail($"Unknown command type: {command.Type}")
            };

            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            return Task.FromResult(AutomationResponse.Fail($"Error: {ex.Message}"));
        }
    }

    private AutomationResponse HandleQuery(AutomationCommand command)
    {
        var target = command.Target ?? "";
        return command.Method switch
        {
            "GetState" => GetElementState(target),
            // One-attempt lookups inside a scope (the target: an AutomationId, "#key", or "" for the root).
            "Find" => Find(target, GetArgString(command.Args, 0) ?? ""),
            "FindAll" => FindAll(target, GetArgString(command.Args, 0) ?? ""),
            "GetChildren" => GetChildren(target),
            "Exists" => CheckExists(target),
            "IsVisible" => CheckVisible(target),
            "IsEnabled" => CheckEnabled(target),
            // The Bruinet.Stride client sends these as Query type
            "IsGameReady" or "IsReady" => AutomationResponse.Ok(_isReadyProvider?.Invoke() ?? true),
            "IsBusy" => AutomationResponse.Ok(_isBusyProvider?.Invoke() ?? false),
            "GetWindowInfo" => GetWindowInfo(),
            _ => AutomationResponse.Fail($"Unknown query method: {command.Method}")
        };
    }

    private AutomationResponse HandleAction(AutomationCommand command)
    {
        var target = command.Target ?? "";

        if (command.Method == "TakeScreenshot")
        {
            var screenshotName = command.Args?.FirstOrDefault()?.ToString() ?? "screenshot";
            if (OperatingSystem.IsWindows())
                return TakeScreenshot(screenshotName);
            return AutomationResponse.Fail("Screenshot is only supported on Windows");
        }

        if (command.Method is "SimulateKeyDown" or "SimulateKeyUp" or "SimulateKeyPress" or "SimulateKeyHold")
            return AutomationResponse.Fail("NotSupported:KeyboardSimulation:UseWindowsSendInput");

        if (command.Method == "Exit")
        {
            return AutomationResponse.Ok(true);
        }

        var element = FindElement(target);
        if (element == null)
            return AutomationResponse.Fail($"NotFound:{target}:{command.Method}");

        return command.Method switch
        {
            "Click" => PerformClick(element),
            "SetText" => SetElementText(element, GetArgString(command.Args, 0) ?? ""),
            "SetElementText" => SetElementText(element, GetArgString(command.Args, 0) ?? ""),
            "SetSliderValue" => SetSliderValue(element, GetArgDouble(command.Args, 0)),
            "Toggle" => PerformToggle(element),
            "SelectAll" => SelectAllText(element),
            "SelectIndex" => SelectByIndex(element, GetArgInt(command.Args, 0)),
            "ScrollToIndex" => ScrollToIndex(element, GetArgInt(command.Args, 0)),
            _ => AutomationResponse.Fail($"Unknown action method: {command.Method}")
        };
    }

    private AutomationResponse HandleGameQuery(AutomationCommand command)
    {
        return command.Method switch
        {
            "IsReady" => AutomationResponse.Ok(_isReadyProvider?.Invoke() ?? true),
            "IsBusy" => AutomationResponse.Ok(_isBusyProvider?.Invoke() ?? false),
            "GetWindowInfo" => GetWindowInfo(),
            _ => AutomationResponse.Fail($"Unknown game query: {command.Method}")
        };
    }

    #region Argument Helpers

    private static string? GetArgString(object[]? args, int index)
    {
        if (args == null || args.Length <= index)
            return null;
        var arg = args[index];
        if (arg is JsonElement je)
            return je.GetString() ?? je.GetRawText();
        return arg?.ToString();
    }

    private static double GetArgDouble(object[]? args, int index)
    {
        if (args == null || args.Length <= index)
            return 0;
        var arg = args[index];
        if (arg is JsonElement je)
        {
            if (je.TryGetDouble(out var d))
                return d;
            if (je.TryGetInt32(out var i))
                return i;
            return 0;
        }
        return Convert.ToDouble(arg, CultureInfo.InvariantCulture);
    }

    private static int GetArgInt(object[]? args, int index)
    {
        if (args == null || args.Length <= index)
            return 0;
        var arg = args[index];
        if (arg is JsonElement je)
        {
            if (je.TryGetInt32(out var i))
                return i;
            if (je.TryGetDouble(out var d))
                return (int)d;
            return 0;
        }
        return Convert.ToInt32(arg, CultureInfo.InvariantCulture);
    }

    #endregion

    #region Element Finding

    /// <summary>The prefix of a target that names an element by its identity (<c>UIElement.Id</c>) rather than its name.</summary>
    public const string KeyPrefix = "#";

    /// <summary>
    /// The element a target names: <c>#key</c> by its identity, anything else by its name; the root
    /// for an empty target. One walk of the tree, no waiting.
    /// </summary>
    private UIElement? FindElement(string target)
    {
        var root = _rootProvider();
        if (root == null)
            return null;
        if (string.IsNullOrEmpty(target))
            return root;
        return Descendants(root, includeSelf: true).FirstOrDefault(element => Matches(element, target));
    }

    private static bool Matches(UIElement element, string target)
        => target.StartsWith(KeyPrefix, StringComparison.Ordinal)
            ? KeyOf(element) == target[KeyPrefix.Length..]
            : element.Name == target;

    /// <summary>An element's identity, as a command's <c>#key</c> target names it.</summary>
    public static string KeyOf(UIElement element) => element.Id.ToString("N");

    /// <summary>
    /// The element's children as automation sees them: a panel's children, a content control's or
    /// scroll viewer's content.
    /// </summary>
    private static IEnumerable<UIElement> ChildrenOf(UIElement element) => element switch
    {
        Panel panel => panel.Children,
        ContentControl { Content: UIElement content } => [content],
        ScrollViewer { Content: UIElement scrollContent } => [scrollContent],
        _ => [],
    };

    private static IEnumerable<UIElement> Descendants(UIElement element, bool includeSelf)
    {
        if (includeSelf)
            yield return element;

        foreach (var child in ChildrenOf(element))
        {
            foreach (var descendant in Descendants(child, includeSelf: true))
                yield return descendant;
        }
    }

    private AutomationResponse Find(string scope, string target)
    {
        var within = FindElement(scope);
        // From the root, the root itself counts: a game whose UI root is a page's panel is found by it.
        var found = within is null ? null : Descendants(within, includeSelf: string.IsNullOrEmpty(scope)).FirstOrDefault(element => Matches(element, target));
        return AutomationResponse.Ok(CreateElementState(found, target));
    }

    private AutomationResponse FindAll(string scope, string target)
    {
        var within = FindElement(scope);
        var found = within is null ? [] : Descendants(within, includeSelf: string.IsNullOrEmpty(scope)).Where(element => Matches(element, target));
        return AutomationResponse.Ok(found.Select(element => CreateElementState(element, element.Name)).ToList());
    }

    private AutomationResponse GetChildren(string scope)
    {
        var within = FindElement(scope);
        var children = within is null ? [] : ChildrenOf(within);
        return AutomationResponse.Ok(children.Select(element => CreateElementState(element, element.Name)).ToList());
    }

    #endregion

    #region Element State

    private AutomationResponse GetElementState(string target)
    {
        var element = FindElement(target);
        var state = CreateElementState(element, target);
        return AutomationResponse.Ok(state);
    }

    private AutomationResponse CheckExists(string automationId)
        => AutomationResponse.Ok(FindElement(automationId) != null);

    private AutomationResponse CheckVisible(string automationId)
        => AutomationResponse.Ok(FindElement(automationId)?.IsVisible ?? false);

    private AutomationResponse CheckEnabled(string automationId)
        => AutomationResponse.Ok(FindElement(automationId)?.IsEnabled ?? false);

    private ElementState CreateElementState(UIElement? element, string automationId)
    {
        if (element == null)
            return new ElementState { AutomationId = automationId, Exists = false };

        return new ElementState
        {
            AutomationId = element.Name ?? automationId,
            Name = element.Name,
            InstanceKey = KeyOf(element),
            ControlType = element.GetType().Name,
            Exists = true,
            IsVisible = IsElementActuallyVisible(element),
            IsEnabled = IsElementActuallyEnabled(element),
            IsHitTestVisible = IsElementActuallyVisible(element) && IsElementActuallyEnabled(element),
            IsFocused = false,
            Bounds = GetElementBounds(element),
            Text = GetElementText(element),
            IsChecked = GetToggleState(element),
            Value = GetRangeValue(element),
            Minimum = GetRangeMinimum(element),
            Maximum = GetRangeMaximum(element),
            Items = GetItems(element),
            SelectedIndex = GetSelectedIndex(element),
            SelectedText = GetSelectedText(element)
        };
    }

    private bool IsElementActuallyVisible(UIElement element)
    {
        if (!element.IsVisible)
            return false;
        var current = element.VisualParent as UIElement;
        while (current != null)
        {
            if (!current.IsVisible)
                return false;
            current = current.VisualParent as UIElement;
        }
        if (element.Opacity <= 0.01f)
            return false;
        return true;
    }

    private bool IsElementActuallyEnabled(UIElement element)
    {
        if (!element.IsEnabled)
            return false;
        var current = element.VisualParent as UIElement;
        while (current != null)
        {
            if (!current.IsEnabled)
                return false;
            current = current.VisualParent as UIElement;
        }
        return true;
    }

    private ElementBounds GetElementBounds(UIElement element)
    {
        var worldMatrix = element.WorldMatrix;
        var renderSize = element.RenderSize;

        var width = renderSize.X;
        var height = renderSize.Y;
        var minWidth = 60f;
        var minHeight = 25f;

        if (width < minWidth)
        {
            width = element.Width;
            if (float.IsNaN(width) || width < minWidth)
                width = element.MinimumWidth;
            if (float.IsNaN(width) || width < minWidth)
                width = minWidth;
        }
        if (height < minHeight)
        {
            height = element.Height;
            if (float.IsNaN(height) || height < minHeight)
                height = element.MinimumHeight;
            if (float.IsNaN(height) || height < minHeight)
                height = minHeight;
        }

        var uiCenterX = 640f;
        var uiCenterY = 360f;
        var elementCenterX = uiCenterX + worldMatrix.TranslationVector.X;
        var elementCenterY = uiCenterY + worldMatrix.TranslationVector.Y;
        var x = elementCenterX - width / 2;
        var y = elementCenterY - height / 2;

        return new ElementBounds
        {
            X = (int)x,
            Y = (int)y,
            Width = (int)width,
            Height = (int)height
        };
    }

    private string? GetElementText(UIElement element) => element switch
    {
        TextBlock textBlock => textBlock.Text,
        EditText editText => editText.Text,
        Button button => (button.Content as TextBlock)?.Text,
        _ => null
    };

    private bool? GetToggleState(UIElement element) =>
        element is ToggleButton toggle ? toggle.State == ToggleState.Checked : null;

    private double? GetRangeValue(UIElement element) =>
        element is Slider slider ? slider.Value : null;

    private double? GetRangeMinimum(UIElement element) =>
        element is Slider slider ? slider.Minimum : null;

    private double? GetRangeMaximum(UIElement element) =>
        element is Slider slider ? slider.Maximum : null;

    private List<string>? GetItems(UIElement element) => null;
    private int GetSelectedIndex(UIElement element) => -1;
    private string? GetSelectedText(UIElement element) => null;

    #endregion

    #region Actions

    private AutomationResponse PerformClick(UIElement element)
    {
        if (element is ButtonBase button)
        {
            // Raise the Click routed event directly on the game thread.
            // This triggers all Click handlers including ToggleButton.OnClick → GoToNextState().
            var args = new RoutedEventArgs { RoutedEvent = ButtonBase.ClickEvent };
            button.RaiseEvent(args);
            return AutomationResponse.Ok(true);
        }

        return AutomationResponse.Fail($"Element '{element.Name}' is not a ButtonBase");
    }

    private AutomationResponse SetElementText(UIElement element, string text)
    {
        if (element is EditText editText)
        {
            editText.Text = text;
            return AutomationResponse.Ok(true);
        }
        return AutomationResponse.Fail("Element is not an EditText");
    }

    private AutomationResponse SelectAllText(UIElement element)
    {
        if (element is EditText editText)
        {
            editText.Select(0, editText.Text?.Length ?? 0);
            return AutomationResponse.Ok(true);
        }
        return AutomationResponse.Fail("Element is not an EditText");
    }

    private AutomationResponse SetSliderValue(UIElement element, double value)
    {
        if (element is Slider slider)
        {
            var clampedValue = Math.Clamp(value, slider.Minimum, slider.Maximum);
            slider.Value = (float)clampedValue;
            return AutomationResponse.Ok(true);
        }
        return AutomationResponse.Fail("Element is not a Slider");
    }

    private AutomationResponse PerformToggle(UIElement element)
    {
        if (element is ToggleButton toggle)
        {
            toggle.State = toggle.State switch
            {
                ToggleState.UnChecked => ToggleState.Checked,
                ToggleState.Checked => ToggleState.UnChecked,
                ToggleState.Indeterminate => ToggleState.Checked,
                _ => ToggleState.Checked
            };
            return AutomationResponse.Ok(true);
        }
        return AutomationResponse.Fail("Element is not a ToggleButton");
    }

    private AutomationResponse SelectByIndex(UIElement element, int index)
    {
        if (element is Panel panel && panel.Children.Count > index)
            return AutomationResponse.Ok(true);
        return AutomationResponse.Fail($"Cannot select index {index}");
    }

    private AutomationResponse ScrollToIndex(UIElement element, int index)
    {
        if (element is ScrollViewer)
            return AutomationResponse.Ok(true);
        return AutomationResponse.Fail("Element is not scrollable");
    }

    #endregion

    #region Window Info

    private AutomationResponse GetWindowInfo()
    {
        var windowInfo = new Dictionary<string, object>
        {
            ["windowX"] = 0,
            ["windowY"] = 0,
            ["windowWidth"] = 1280,
            ["windowHeight"] = 720,
            ["uiResolutionX"] = 1280,
            ["uiResolutionY"] = 720
        };

        if (_game?.Window != null)
        {
            var window = _game.Window;
            windowInfo["windowWidth"] = window.ClientBounds.Width;
            windowInfo["windowHeight"] = window.ClientBounds.Height;

            var hwnd = window.NativeWindow.Handle;
            if (hwnd != IntPtr.Zero && GetWindowRect(hwnd, out var rect))
            {
                var clientPoint = new POINT { X = 0, Y = 0 };
                ClientToScreen(hwnd, ref clientPoint);
                windowInfo["windowX"] = clientPoint.X;
                windowInfo["windowY"] = clientPoint.Y;
            }
        }

        return AutomationResponse.Ok(windowInfo);
    }

    #endregion

    #region Screenshot

    [SupportedOSPlatform("windows")]
    private AutomationResponse TakeScreenshot(string screenshotName)
    {
        try
        {
            var screenshotDir = Path.Combine(Path.GetTempPath(), "stride_screenshots");
            Directory.CreateDirectory(screenshotDir);
            var screenshotPath = Path.Combine(screenshotDir, $"{screenshotName}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            if (_game?.Window == null)
                return AutomationResponse.Fail("Game window is not available");

            var hwnd = _game.Window.NativeWindow.Handle;
            if (hwnd == IntPtr.Zero)
                return AutomationResponse.Fail("Invalid window handle");

            CaptureWindowScreenshot(hwnd, screenshotPath);
            return AutomationResponse.Ok(screenshotPath);
        }
        catch (Exception ex)
        {
            return AutomationResponse.Fail($"Failed to take screenshot: {ex.Message}");
        }
    }

    [SupportedOSPlatform("windows")]
    private void CaptureWindowScreenshot(IntPtr hwnd, string filePath)
    {
        BringWindowToCaptureSurface(hwnd);

        if (!GetClientRect(hwnd, out var clientRect))
            throw new InvalidOperationException("Failed to get client rect");

        var width = clientRect.Right - clientRect.Left;
        var height = clientRect.Bottom - clientRect.Top;
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("Window client area is empty");

        var clientPoint = new POINT { X = 0, Y = 0 };
        ClientToScreen(hwnd, ref clientPoint);

        var hdcScreen = GetDC(IntPtr.Zero);
        if (hdcScreen == IntPtr.Zero)
            throw new InvalidOperationException("Failed to get screen device context");

        try
        {
            var hdcMemDC = CreateCompatibleDC(hdcScreen);
            if (hdcMemDC == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create compatible DC");

            try
            {
                var hBitmap = CreateCompatibleBitmap(hdcScreen, width, height);
                if (hBitmap == IntPtr.Zero)
                    throw new InvalidOperationException("Failed to create compatible bitmap");

                try
                {
                    var hOldBitmap = SelectObject(hdcMemDC, hBitmap);
                    if (!BitBlt(hdcMemDC, 0, 0, width, height, hdcScreen, clientPoint.X, clientPoint.Y, 0x00CC0020))
                        throw new InvalidOperationException("BitBlt failed");
                    SelectObject(hdcMemDC, hOldBitmap);

                    using var bitmap = System.Drawing.Image.FromHbitmap(hBitmap);
                    bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
            finally
            {
                DeleteDC(hdcMemDC);
            }
        }
        finally
        {
            _ = ReleaseDC(IntPtr.Zero, hdcScreen);
            SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void BringWindowToCaptureSurface(IntPtr hwnd)
    {
        ShowWindow(hwnd, SW_RESTORE);
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        SetForegroundWindow(hwnd);

        // Wait on what the copy needs rather than a guess (review 02, finding 1): the window in front, then one
        // frame composed since, so the pixels copied are the game's and not what covered it.
        SpinWait.SpinUntil(() => GetForegroundWindow() == hwnd, millisecondsTimeout: 1_000);
        _ = DwmFlush();
    }

    #endregion

    #region Windows API

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    // Blocks until the desktop compositor presents its next frame.
    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
        IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);
    private const int SW_RESTORE = 9;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    #endregion
}
