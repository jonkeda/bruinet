using Bruinet.Appium;
using Bruinet.Core.Recording;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.iOS;

namespace Bruinet.NativeIos.Appium;

/// <summary>
/// The Appium session on one iOS simulator or device, through XCUITest's <c>mobile:</c> commands.
/// Lookups make one attempt: the session's implicit wait is zero, and the call around a lookup is
/// what waits.
/// </summary>
internal sealed class AppiumNativeIosDriver : INativeIosDriver
{
    // queryAppState: 3 is "running in the background", 4 "in the foreground".
    private const long RunningInBackground = 3;

    // XCUITest waits for the app to be idle (no animation) before most commands; a spinner never is.
    private const string WaitForIdle = "appium:waitForIdleTimeout";

    private const double IdleWaitSeconds = 0.5;

    /// <summary>The element types that scroll: a screen-wide scroll uses the first.</summary>
    internal const string ScrollablePredicate
        = "type IN {'XCUIElementTypeTable', 'XCUIElementTypeCollectionView', 'XCUIElementTypeScrollView', 'XCUIElementTypeWebView'}";

    private readonly IOSDriver _driver;
    private readonly NativeIosTestContextOptions _options;
    private bool _disposed;

    /// <summary>Wraps a session that is already open.</summary>
    public AppiumNativeIosDriver(IOSDriver driver, NativeIosTestContextOptions? options = null)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _options = options ?? new NativeIosTestContextOptions();
        _driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
    }

    /// <summary>Opens a session with <paramref name="options"/>.</summary>
    public static AppiumNativeIosDriver Create(NativeIosTestContextOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new AppiumNativeIosDriver(new IOSDriver(options.AppiumServerUri, BuildAppiumOptions(options)), options);
    }

    /// <summary>The Appium driver, for this stack's element and a test of the adapter.</summary>
    internal IOSDriver Raw => _driver;

    /// <inheritdoc />
    public string? BundleId => _options.BundleId ?? Capability("bundleId");

    /// <inheritdoc />
    public string? CurrentBundleId
        => Mobile("mobile: activeAppInfo") is IDictionary<string, object> info && info.TryGetValue("bundleId", out var id)
            ? id?.ToString()
            : null;

    /// <inheritdoc />
    public bool IsKeyboardShown => Mobile("mobile: isKeyboardShown") is true;

    /// <inheritdoc />
    /// <remarks>A simulator records through <c>simctl</c>; a device has no recorder here, so it answers null.</remarks>
    public RecordingTarget? RecordingTarget
        => !_disposed && Udid() is { Length: > 0 } udid && IosHost.IsSimulator(udid)
            ? new RecordingTarget.IosSimulator(udid)
            : null;

    /// <inheritdoc />
    /// <remarks>
    /// The app in front (<c>activeAppInfo</c>), not <c>queryAppState</c>: a system alert in front of
    /// the app leaves it "in the foreground", but puts Springboard in front.
    /// </remarks>
    public bool IsAppInForeground()
        => BundleId is not { } bundle || string.Equals(CurrentBundleId, bundle, StringComparison.Ordinal);

    /// <summary>Whether the app under test is running at all, in front or not (<c>queryAppState</c>).</summary>
    public bool IsAppRunning()
        => BundleId is not { } bundle
           || Convert.ToInt64(Mobile("mobile: queryAppState", new() { ["bundleId"] = bundle }), CultureInfo.InvariantCulture) >= RunningInBackground;

    /// <inheritdoc />
    public IReadOnlyList<INativeIosElement> FindElements(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        var by = locator.ToIosBy();
        return AppiumNativeIosElement.Live(() => _driver.FindElements(by)
            .Select(element => (INativeIosElement)new AppiumNativeIosElement(element, this))
            .ToList());
    }

    /// <inheritdoc />
    public bool TryScrollIntoView(Locator locator)
        => TryScrollIntoView(locator, MobileBy.IosNSPredicate(ScrollablePredicate));

    /// <inheritdoc />
    public bool TryScrollIntoView(Locator locator, Locator scrollable)
    {
        ArgumentNullException.ThrowIfNull(scrollable);
        return TryScrollIntoView(locator, scrollable.ToIosBy());
    }

    // mobile: scroll with a predicate reaches only an element already in the tree: a collection view
    // keeps only the cells on the screen, so a row far down is "not matched" (measured, 2026-11-16).
    // Then the caller scrolls by hand.
    private bool TryScrollIntoView(Locator locator, By scrollable)
    {
        ArgumentNullException.ThrowIfNull(locator);
        if (locator.ToPredicate() is not { } predicate)
        {
            return false;
        }

        return AppiumNativeIosElement.Live(() =>
        {
            if (_driver.FindElements(scrollable).FirstOrDefault() is not { } container)
            {
                return false;
            }

            try
            {
                _driver.ExecuteScript("mobile: scroll", new Dictionary<string, object>
                {
                    ["elementId"] = container.Id,
                    ["predicateString"] = predicate,
                });
                return true;
            }
            catch (WebDriverException error) when (!AppiumErrors.IsSessionGone(error) && !AppiumErrors.IsElementGone(error))
            {
                // "predicate didn't match any elements": not in the tree yet. Not this attempt's answer.
                return false;
            }
        });
    }

    /// <inheritdoc />
    public void OpenDeepLink(string uri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        var arguments = new Dictionary<string, object> { ["url"] = uri };
        if (BundleId is { } bundle)
        {
            arguments["bundleId"] = bundle;
        }

        Mobile("mobile: deepLink", arguments);
    }

    /// <inheritdoc />
    public void Back()
    {
        using var _ = Interactions.Report(InteractionKind.Key, ScreenBounds, key: "Back");
        AppiumNativeIosElement.Live(() => _driver.Navigate().Back());
    }

    /// <inheritdoc />
    public void PressButton(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        using var _ = Interactions.Report(InteractionKind.Key, ScreenBounds, key: name);
        Mobile("mobile: pressButton", new() { ["name"] = name });
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
    public void ActivateApp() => Mobile("mobile: activateApp", new() { ["bundleId"] = RequireBundle(nameof(ActivateApp)) });

    /// <inheritdoc />
    public void TerminateApp() => Mobile("mobile: terminateApp", new() { ["bundleId"] = RequireBundle(nameof(TerminateApp)) });

    /// <inheritdoc />
    public void ResetAppState()
    {
        TerminateApp();
        ActivateApp();
    }

    /// <inheritdoc />
    public byte[] TakeScreenshot() => AppiumNativeIosElement.Live(() => _driver.GetScreenshot().AsByteArray);

    /// <inheritdoc />
    public void SaveScreenshot(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllBytes(path, TakeScreenshot());
    }

    /// <inheritdoc />
    public string GetPageSource() => AppiumNativeIosElement.Live(() => _driver.PageSource);

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

    internal object? Mobile(string command, Dictionary<string, object>? arguments = null)
        => AppiumNativeIosElement.Live(() => _driver.ExecuteScript(command, arguments ?? []));

    private string RequireBundle(string operation)
        => BundleId ?? throw new InvalidOperationException($"{operation} needs the app's bundle id: set {nameof(NativeIosTestContextOptions.BundleId)} or APPIUM_BUNDLE_ID.");

    private string? Udid() => _options.Udid ?? Capability("udid") ?? Capability("deviceUDID");

    private string? Capability(string name) => _driver.Capabilities.GetCapability(name)?.ToString();

    private Rectangle ScreenBounds() => new(Point.Empty, _driver.Manage().Window.Size);

    private static AppiumOptions BuildAppiumOptions(NativeIosTestContextOptions options)
    {
        var appiumOptions = new AppiumOptions
        {
            PlatformName = "iOS",
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

        AddIfPresent(appiumOptions, "udid", options.Udid);
        AddIfPresent(appiumOptions, "bundleId", options.BundleId);
        appiumOptions.AddAdditionalAppiumOption("autoAcceptAlerts", options.AutoAcceptAlerts);
        appiumOptions.AddAdditionalAppiumOption("noReset", options.NoReset);
        appiumOptions.AddAdditionalAppiumOption("fullReset", options.FullReset);

        // Bruinet waits for state itself (R2); XCUITest's own wait for an idle app only delays a busy page.
        if (!options.AdditionalCapabilities.ContainsKey(WaitForIdle) && !options.AdditionalCapabilities.ContainsKey("waitForIdleTimeout"))
        {
            appiumOptions.AddAdditionalAppiumOption("waitForIdleTimeout", IdleWaitSeconds);
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
