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

namespace Bruinet.Wpf.FlaUI;

/// <summary>
/// FlaUI-based implementation of <see cref="IWpfDriver"/> for WPF desktop apps.
/// Provides native Windows UI Automation support.
/// </summary>
public sealed class FlaUIWpfDriver : IWpfDriver, IDisposable
{
    private readonly UIA3Automation _automation;
    private readonly Application? _application;
    private readonly AutomationElement _rootElement;
    private readonly ConditionFactory _conditionFactory;
    private bool _disposed;

    /// <summary>
    /// Creates a new FlaUIWpfDriver for an existing window.
    /// </summary>
    /// <param name="windowHandle">The window handle to attach to.</param>
    public FlaUIWpfDriver(IntPtr windowHandle)
    {
        _automation = new UIA3Automation();
        _rootElement = _automation.FromHandle(windowHandle);
        _conditionFactory = new ConditionFactory(_automation.PropertyLibrary);
    }

    /// <summary>
    /// Creates a new FlaUIWpfDriver by launching an application.
    /// </summary>
    /// <param name="executablePath">Path to the application executable.</param>
    /// <param name="arguments">Optional command line arguments.</param>
    public FlaUIWpfDriver(string executablePath, string? arguments = null)
    {
        _automation = new UIA3Automation();

        var processStartInfo = new ProcessStartInfo(executablePath)
        {
            Arguments = arguments ?? string.Empty
        };

        _application = Application.Launch(processStartInfo);

        var window = _application.GetMainWindow(_automation, TimeSpan.FromSeconds(30));
        _rootElement = window ?? throw new InvalidOperationException("Failed to get main window");
        _conditionFactory = new ConditionFactory(_automation.PropertyLibrary);
    }

    /// <summary>
    /// Creates a new FlaUIWpfDriver by attaching to a running process.
    /// </summary>
    /// <param name="process">The process to attach to.</param>
    public FlaUIWpfDriver(Process process)
    {
        _automation = new UIA3Automation();
        _application = Application.Attach(process);

        var window = _application.GetMainWindow(_automation, TimeSpan.FromSeconds(30));
        _rootElement = window ?? throw new InvalidOperationException("Failed to get main window");
        _conditionFactory = new ConditionFactory(_automation.PropertyLibrary);
    }

    #region Internal

    /// <summary>
    /// Gets the condition factory for building search conditions.
    /// </summary>
    internal ConditionFactory ConditionFactory => _conditionFactory;

    /// <summary>
    /// Gets the underlying automation instance.
    /// </summary>
    internal UIA3Automation Automation => _automation;

    /// <summary>
    /// Ensures the root window is focused and activated before interaction.
    /// </summary>
    internal void EnsureRootWindowFocused()
    {
        try
        {
            if (_rootElement.Patterns.Window.IsSupported)
            {
                var windowPattern = _rootElement.Patterns.Window.Pattern;
                if (windowPattern.WindowVisualState.Value == WindowVisualState.Minimized)
                {
                    windowPattern.SetWindowVisualState(WindowVisualState.Normal);
                }
            }
        }
        catch
        {
            // Ignore window visual state failures and continue with focus fallback.
        }

        try
        {
            _rootElement.SetForeground();
        }
        catch
        {
            try
            {
                _rootElement.Focus();
            }
            catch
            {
                // Ignore focus failures; interaction will proceed regardless.
            }
        }
    }

    #endregion

    #region Element Finding (IDriver<IWpfElement>)

    /// <inheritdoc />
    public IWpfElement FindElement(Locator locator, int timeoutMs = 5000)
    {
        var condition = locator.ToCondition(_conditionFactory);

        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);

        while (DateTime.UtcNow - startTime < timeout)
        {
            var found = _rootElement.FindFirstDescendant(condition);
            if (found != null)
                return new FlaUIWpfElement(found, this);

            if (timeoutMs <= 0)
                break;
            WaitHelper.Pause(100);
        }

        throw new ElementNotFoundException(locator);
    }

    /// <inheritdoc />
    public IReadOnlyList<IWpfElement> FindElements(Locator locator, int timeoutMs = 0)
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
                    return found.Select(e => new FlaUIWpfElement(e, this)).ToList();
                WaitHelper.Pause(100);
            }
        }

        var elements = _rootElement.FindAllDescendants(condition);
        return elements.Select(e => new FlaUIWpfElement(e, this)).ToList();
    }

    /// <inheritdoc />
    public bool TryFindElement(Locator locator, out IWpfElement? element, int timeoutMs = 0)
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

    /// <inheritdoc />
    public string CurrentWindowHandle => _rootElement.Properties.NativeWindowHandle.Value.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public IReadOnlyCollection<string> WindowHandles
    {
        get
        {
            if (_application != null)
            {
                return _application.GetAllTopLevelWindows(_automation)
                    .Select(w => w.Properties.NativeWindowHandle.Value.ToString(CultureInfo.InvariantCulture))
                    .ToList();
            }
            return new[] { CurrentWindowHandle };
        }
    }

    #endregion

    #region Session Management

    /// <inheritdoc />
    public void Quit()
    {
        _application?.Close();
    }

    /// <inheritdoc />
    public void Close()
    {
        if (_rootElement.Patterns.Window.IsSupported)
        {
            _rootElement.Patterns.Window.Pattern.Close();
        }
    }

    #endregion

    #region Screenshots

    /// <inheritdoc />
    public byte[] GetScreenshot()
    {
        var capture = Capture.Element(_rootElement);
        using var ms = new MemoryStream();
        capture.Bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }

    #endregion

    #region IDiagnosticDriver

    /// <inheritdoc />
    public string GetPageSource()
    {
        return BuildAutomationTree(_rootElement);
    }

    /// <inheritdoc />
    public string GetAutomationTree()
    {
        return BuildAutomationTree(_rootElement);
    }

    private static string BuildAutomationTree(AutomationElement element, int depth = 0)
    {
        var indent = new string(' ', depth * 2);
        var sb = new System.Text.StringBuilder();

        string automationId = "";
        string name = "";
        string className = "";
        string controlType = "Unknown";

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
            {
                sb.Append(BuildAutomationTree(child, depth + 1));
            }
        }
        catch
        {
            // Ignore errors traversing children
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"{indent}</{controlType}>");
        return sb.ToString();
    }

    #endregion

    #region Navigation

    /// <inheritdoc />
    public void NavigateTo(string destination)
    {
        throw new NotSupportedException("URL navigation is not supported for WPF desktop apps");
    }

    /// <inheritdoc />
    public void NavigateBack()
    {
        PhysicalInput.Used("FlaUIWpfDriver.NavigateBack(Alt+Left)", "a NavigateBack verb");
        Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.LEFT);
    }

    /// <inheritdoc />
    public void Refresh()
    {
        PhysicalInput.Used("FlaUIWpfDriver.Refresh(F5)", "a NavigateTo verb");
        Keyboard.Type(VirtualKeyShort.F5);
    }

    /// <inheritdoc />
    public byte[] TakeScreenshot() => GetScreenshot();

    /// <inheritdoc />
    public void ResetAppState()
    {
        _application?.Close();
    }

    #endregion

    #region IDisposable

    /// <inheritdoc />
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
