using Bruinet.Core.Diagnostics;
using System.Drawing;
using Bruinet.Core;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Utilities;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using System.Globalization;

namespace Bruinet.WinForms.FlaUI;

/// <summary>
/// FlaUI-based implementation of <see cref="IWinFormsDriver"/> for WinForms desktop apps.
/// </summary>
public sealed class FlaUIWinFormsDriver : IWinFormsDriver, IDisposable
{
    private readonly UIA3Automation _automation;
    private readonly Application? _application;
    private readonly AutomationElement _rootElement;
    private readonly ConditionFactory _conditionFactory;
    private bool _disposed;

    public FlaUIWinFormsDriver(IntPtr windowHandle)
    {
        _automation = new UIA3Automation();
        _rootElement = _automation.FromHandle(windowHandle);
        _conditionFactory = new ConditionFactory(_automation.PropertyLibrary);
    }

    public FlaUIWinFormsDriver(string executablePath, string? arguments = null)
    {
        _automation = new UIA3Automation();
        var processStartInfo = new ProcessStartInfo(executablePath) { Arguments = arguments ?? string.Empty };
        _application = Application.Launch(processStartInfo);
        var window = _application.GetMainWindow(_automation, TimeSpan.FromSeconds(30));
        _rootElement = window ?? throw new InvalidOperationException("Failed to get main window");
        _conditionFactory = new ConditionFactory(_automation.PropertyLibrary);
    }

    public FlaUIWinFormsDriver(Process process)
    {
        _automation = new UIA3Automation();
        _application = Application.Attach(process);
        var window = _application.GetMainWindow(_automation, TimeSpan.FromSeconds(30));
        _rootElement = window ?? throw new InvalidOperationException("Failed to get main window");
        _conditionFactory = new ConditionFactory(_automation.PropertyLibrary);
    }

    internal ConditionFactory ConditionFactory => _conditionFactory;
    internal UIA3Automation Automation => _automation;

    internal void EnsureRootWindowFocused()
    {
        try
        {
            if (_rootElement.Patterns.Window.IsSupported)
            {
                var windowPattern = _rootElement.Patterns.Window.Pattern;
                if (windowPattern.WindowVisualState.Value == WindowVisualState.Minimized)
                    windowPattern.SetWindowVisualState(WindowVisualState.Normal);
            }
        }
        catch { }

        try
        { _rootElement.SetForeground(); }
        catch
        {
            try
            { _rootElement.Focus(); }
            catch { }
        }
    }

    #region Element Finding

    public IWinFormsElement FindElement(Locator locator, int timeoutMs = 5000)
    {
        var condition = locator.ToCondition(_conditionFactory);
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);

        while (DateTime.UtcNow - startTime < timeout)
        {
            var found = _rootElement.FindFirstDescendant(condition);
            if (found != null)
                return new FlaUIWinFormsElement(found, this);
            if (timeoutMs <= 0)
                break;
            WaitHelper.Pause(100);
        }

        throw new ElementNotFoundException(locator);
    }

    public IReadOnlyList<IWinFormsElement> FindElements(Locator locator, int timeoutMs = 0)
    {
        var condition = locator.ToCondition(_conditionFactory);

        if (timeoutMs > 0)
        {
            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromMilliseconds(timeoutMs);
            while (DateTime.UtcNow - startTime < timeout)
            {
                var found = _rootElement.FindAllDescendants(condition);
                if (found.Length > 0)
                    return found.Select(e => new FlaUIWinFormsElement(e, this)).ToList();
                WaitHelper.Pause(100);
            }
        }

        var elements = _rootElement.FindAllDescendants(condition);
        return elements.Select(e => new FlaUIWinFormsElement(e, this)).ToList();
    }

    public bool TryFindElement(Locator locator, out IWinFormsElement? element, int timeoutMs = 0)
    {
        try
        {
            element = FindElement(locator, timeoutMs);
            return true;
        }
        catch (ElementNotFoundException)
        {
            element = null;
            return false;
        }
    }

    #endregion

    #region Window Management

    public string CurrentWindowHandle => _rootElement.Properties.NativeWindowHandle.Value.ToString(CultureInfo.InvariantCulture);

    public IReadOnlyCollection<string> WindowHandles
    {
        get
        {
            if (_application != null)
                return _application.GetAllTopLevelWindows(_automation)
                    .Select(w => w.Properties.NativeWindowHandle.Value.ToString(CultureInfo.InvariantCulture)).ToList();
            return new[] { CurrentWindowHandle };
        }
    }

    public string? WindowTitle
    {
        get => _rootElement.Properties.Name.ValueOrDefault;
        set { /* WinForms title is read-only via UIA */ }
    }

    public void MaximizeWindow()
    {
        if (_rootElement.Patterns.Window.IsSupported)
            _rootElement.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);
    }

    public void MinimizeWindow()
    {
        if (_rootElement.Patterns.Window.IsSupported)
            _rootElement.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Minimized);
    }

    public void RestoreWindow()
    {
        if (_rootElement.Patterns.Window.IsSupported)
            _rootElement.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);
    }

    public void CloseWindow()
    {
        if (_rootElement.Patterns.Window.IsSupported)
            _rootElement.Patterns.Window.Pattern.Close();
    }

    public Size GetWindowSize() => new(_rootElement.BoundingRectangle.Width, _rootElement.BoundingRectangle.Height);

    public void SetWindowSize(int width, int height)
    {
        if (_rootElement.Patterns.Transform.IsSupported)
            _rootElement.Patterns.Transform.Pattern.Resize(width, height);
    }

    public Point GetWindowPosition() => new(_rootElement.BoundingRectangle.X, _rootElement.BoundingRectangle.Y);

    public void SetWindowPosition(int x, int y)
    {
        if (_rootElement.Patterns.Transform.IsSupported)
            _rootElement.Patterns.Transform.Pattern.Move(x, y);
    }

    #endregion

    #region Session Management

    public void Quit() => _application?.Close();
    public void Close()
    {
        if (_rootElement.Patterns.Window.IsSupported)
            _rootElement.Patterns.Window.Pattern.Close();
    }

    #endregion

    #region Screenshots

    public byte[] GetScreenshot() => TakeScreenshot();

    public byte[] TakeScreenshot()
    {
        var capture = Capture.Element(_rootElement);
        using var ms = new MemoryStream();
        capture.Bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }

    public void SaveScreenshot(string filePath)
    {
        var bytes = TakeScreenshot();
        File.WriteAllBytes(filePath, bytes);
    }

    #endregion

    #region Navigation

    public void NavigateTo(string destination) =>
        throw new NotSupportedException("URL navigation not supported for WinForms");

    public void NavigateBack()
    {
        PhysicalInput.Used("FlaUIWinFormsDriver.NavigateBack(Alt+Left)", "a NavigateBack verb");
        Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.LEFT);
    }

    public void Refresh()
    {
        PhysicalInput.Used("FlaUIWinFormsDriver.Refresh(F5)", "a NavigateTo verb");
        Keyboard.Type(VirtualKeyShort.F5);
    }

    public void ResetAppState() => _application?.Close();

    #endregion

    #region IDiagnosticDriver

    public string GetPageSource() => GetAutomationTree();

    public string GetAutomationTree()
    {
        return BuildAutomationTree(_rootElement);
    }

    private static string BuildAutomationTree(AutomationElement element, int depth = 0)
    {
        var indent = new string(' ', depth * 2);
        var sb = new System.Text.StringBuilder();

        string automationId = "", name = "", className = "", controlType = "Unknown";
        try
        { automationId = element.Properties.AutomationId.ValueOrDefault ?? ""; }
        catch { }
        try
        { name = element.Properties.Name.ValueOrDefault ?? ""; }
        catch { }
        try
        { className = element.Properties.ClassName.ValueOrDefault ?? ""; }
        catch { }
        try
        { controlType = element.ControlType.ToString(); }
        catch { }

        sb.AppendLine(CultureInfo.InvariantCulture, $"{indent}<{controlType} AutomationId=\"{automationId}\" Name=\"{name}\" ClassName=\"{className}\">");

        try
        {
            foreach (var child in element.FindAllChildren())
                sb.Append(BuildAutomationTree(child, depth + 1));
        }
        catch { }

        sb.AppendLine(CultureInfo.InvariantCulture, $"{indent}</{controlType}>");
        return sb.ToString();
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (!_disposed)
        {
            _application?.Close();
            _automation.Dispose();
            _disposed = true;
        }
    }

    #endregion
}
