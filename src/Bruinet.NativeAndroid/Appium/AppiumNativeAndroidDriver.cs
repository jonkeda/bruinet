using Bruinet.Appium;
using Bruinet.Core.Recording;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;

namespace Bruinet.NativeAndroid.Appium;

/// <summary>
/// The Appium session on one Android device, through UiAutomator2's <c>mobile:</c> commands.
/// Lookups make one attempt: the session's implicit wait is zero, and the call around a lookup is
/// what waits.
/// </summary>
internal sealed class AppiumNativeAndroidDriver : INativeAndroidDriver
{
    // queryAppState: 3 is "running in the background", 4 "in the foreground".
    private const long RunningInBackground = 3;

    private const string MultiWindows = "settings[enableMultiWindows]";

    private const string WaitForIdle = "settings[waitForIdleTimeout]";

    private const int IdleWaitMs = 500;

    // How many swipes one UiScrollable sweep may make: one attempt must stay short (R2).
    private const int MaxSearchSwipes = 15;

    private readonly AndroidDriver _driver;
    private readonly NativeAndroidTestContextOptions _options;
    private bool _disposed;

    /// <summary>Wraps a session that is already open.</summary>
    public AppiumNativeAndroidDriver(AndroidDriver driver, NativeAndroidTestContextOptions? options = null)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _options = options ?? new NativeAndroidTestContextOptions();
        _driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
    }

    /// <summary>Opens a session with <paramref name="options"/>.</summary>
    public static AppiumNativeAndroidDriver Create(NativeAndroidTestContextOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new AppiumNativeAndroidDriver(new AndroidDriver(options.AppiumServerUri, BuildAppiumOptions(options)), options);
    }

    /// <summary>The Appium driver, for this stack's element and a test of the adapter.</summary>
    internal AndroidDriver Raw => _driver;

    /// <inheritdoc />
    public string? AppPackage => _options.AppPackage ?? Capability("appPackage");

    /// <inheritdoc />
    public string? CurrentPackage => Mobile("mobile: getCurrentPackage") as string;

    /// <inheritdoc />
    public string? CurrentActivity => Mobile("mobile: getCurrentActivity") as string;

    /// <inheritdoc />
    public bool IsKeyboardShown => Mobile("mobile: isKeyboardShown") is true;

    /// <inheritdoc />
    public RecordingTarget? RecordingTarget
        => !_disposed && (Capability("deviceUDID") ?? Capability("udid")) is { Length: > 0 } serial
            ? new RecordingTarget.AndroidDevice(serial)
            : null;

    /// <inheritdoc />
    /// <remarks>
    /// The package in front, not <c>queryAppState</c>: that is an adb shell call (about 470 ms on the
    /// emulator, against 155 ms), and it runs on every attempt of every call (measured, 2026-10-07).
    /// </remarks>
    public bool IsAppInForeground()
        => AppPackage is not { } package || string.Equals(CurrentPackage, package, StringComparison.Ordinal);

    /// <summary>Whether the app under test is running at all, in front or not (<c>queryAppState</c>).</summary>
    public bool IsAppRunning()
        => AppPackage is not { } package
           || Convert.ToInt64(Mobile("mobile: queryAppState", new() { ["appId"] = package }), CultureInfo.InvariantCulture) >= RunningInBackground;

    /// <inheritdoc />
    public IReadOnlyList<INativeAndroidElement> FindElements(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        var by = locator.ToAndroidBy();
        return AppiumNativeAndroidElement.Live(() => _driver.FindElements(by)
            .Select(element => (INativeAndroidElement)new AppiumNativeAndroidElement(element, this))
            .ToList());
    }

    /// <inheritdoc />
    public bool TryScrollIntoView(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        return locator.ToUiSelector() is { } selector && ScrollIntoView("new UiSelector().scrollable(true).instance(0)", selector);
    }

    /// <inheritdoc />
    public bool TryScrollIntoView(Locator locator, Locator scrollable)
    {
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentNullException.ThrowIfNull(scrollable);
        return locator.ToUiSelector() is { } selector && scrollable.ToUiSelector() is { } within && ScrollIntoView(within, selector);
    }

    private bool ScrollIntoView(string scrollable, string selector)
    {
        var query = $"new UiScrollable({scrollable}).setMaxSearchSwipes({MaxSearchSwipes}).scrollIntoView({selector})";
        try
        {
            return AppiumNativeAndroidElement.Live(() => _driver.FindElements(MobileBy.AndroidUIAutomator(query)).Count > 0);
        }
        catch (NoSuchElementException)
        {
            // Nothing on the screen scrolls, or the view is not in it: not this attempt's answer.
            return false;
        }
    }

    /// <inheritdoc />
    public void OpenDeepLink(string uri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        var arguments = new Dictionary<string, object> { ["url"] = uri };
        if (AppPackage is { } package)
        {
            arguments["package"] = package;
        }

        Mobile("mobile: deepLink", arguments);
    }

    /// <inheritdoc />
    public void Back()
    {
        using var _ = Interactions.Report(InteractionKind.Key, ScreenBounds, key: "Back");
        AppiumNativeAndroidElement.Live(() => _driver.Navigate().Back());
    }

    /// <inheritdoc />
    public void PressKeyCode(int keyCode)
    {
        using var _ = Interactions.Report(InteractionKind.Key, ScreenBounds, key: keyCode.ToString(CultureInfo.InvariantCulture));
        Mobile("mobile: pressKey", new() { ["keycode"] = keyCode });
    }

    /// <inheritdoc />
    public void HideKeyboard()
    {
        if (IsKeyboardShown)
        {
            Mobile("mobile: hideKeyboard");
        }
    }

    /// <inheritdoc />
    public void ActivateApp() => Mobile("mobile: activateApp", new() { ["appId"] = RequirePackage(nameof(ActivateApp)) });

    /// <inheritdoc />
    public void TerminateApp() => Mobile("mobile: terminateApp", new() { ["appId"] = RequirePackage(nameof(TerminateApp)) });

    /// <inheritdoc />
    public void ResetAppState()
    {
        TerminateApp();
        ActivateApp();
    }

    /// <inheritdoc />
    public byte[] TakeScreenshot() => AppiumNativeAndroidElement.Live(() => _driver.GetScreenshot().AsByteArray);

    /// <inheritdoc />
    public void SaveScreenshot(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllBytes(path, TakeScreenshot());
    }

    /// <inheritdoc />
    public string GetPageSource() => AppiumNativeAndroidElement.Live(() => _driver.PageSource);

    /// <inheritdoc />
    public string GetAutomationTree() => GetPageSource();

    /// <summary>Ends the session. A session that already ended is not an error here.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _driver.Quit();
        }
        catch (WebDriverException error) when (AppiumErrors.IsSessionGone(error))
        {
            // Already gone: nothing to end.
        }
    }

    private object? Mobile(string command, Dictionary<string, object>? arguments = null)
        => AppiumNativeAndroidElement.Live(() => _driver.ExecuteScript(command, arguments ?? []));

    private string RequirePackage(string operation)
        => AppPackage ?? throw new InvalidOperationException($"{operation} needs the app's package: set {nameof(NativeAndroidTestContextOptions.AppPackage)} or APPIUM_APP_PACKAGE.");

    private string? Capability(string name) => _driver.Capabilities.GetCapability(name)?.ToString();

    private Rectangle ScreenBounds() => new(Point.Empty, _driver.Manage().Window.Size);

    private static AppiumOptions BuildAppiumOptions(NativeAndroidTestContextOptions options)
    {
        var appiumOptions = new AppiumOptions
        {
            PlatformName = "Android",
            AutomationName = options.AutomationName,
            DeviceName = options.DeviceName,
        };

        if (!string.IsNullOrWhiteSpace(options.PlatformVersion))
        {
            appiumOptions.PlatformVersion = options.PlatformVersion;
        }

        if (!string.IsNullOrWhiteSpace(options.AppPath))
        {
            appiumOptions.App = options.AppPath;
        }

        AddIfPresent(appiumOptions, "appPackage", options.AppPackage);
        AddIfPresent(appiumOptions, "appActivity", options.AppActivity);
        appiumOptions.AddAdditionalAppiumOption("autoGrantPermissions", options.AutoGrantPermissions);
        appiumOptions.AddAdditionalAppiumOption("noReset", options.NoReset);
        appiumOptions.AddAdditionalAppiumOption("fullReset", options.FullReset);

        // A suggestion list or a menu is a window of its own that does not take focus; UiAutomator2
        // shows only the focused window unless asked for all of them.
        if (!options.AdditionalCapabilities.ContainsKey(MultiWindows))
        {
            appiumOptions.AddAdditionalAppiumOption(MultiWindows, true);
        }

        // UiAutomator2 waits up to 10 s for the screen to be idle before every command. A spinner is
        // never idle, so every command on a busy page took 10 s, and a UiScrollable sweep of 30 steps
        // never came back. With no wait at all a sweep overshot its item (measured, 2026-10-07):
        // a short one bounds both. Bruinet waits for state itself (R2).
        if (!options.AdditionalCapabilities.ContainsKey(WaitForIdle))
        {
            appiumOptions.AddAdditionalAppiumOption(WaitForIdle, IdleWaitMs);
        }

        foreach (var capability in options.AdditionalCapabilities)
        {
            appiumOptions.AddAdditionalAppiumOption(capability.Key, capability.Value);
        }

        return appiumOptions;
    }

    private static void AddIfPresent(AppiumOptions appiumOptions, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            appiumOptions.AddAdditionalAppiumOption(name, value);
        }
    }
}
