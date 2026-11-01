using Bruinet.Core.Services;
using Bruinet.Core.Testing;
using Bruinet.Core.Artifacts;
using Bruinet.Maui.Context;
using Bruinet.Maui.Configuration;

namespace Bruinet.Maui.Testing;

/// <summary>
/// Base fixture for MAUI UI tests that manages the Appium driver and test context lifecycle.
/// Inherit from this class in your test project and implement <see cref="GetDefaultAppPath"/>.
/// </summary>
/// <remarks>
/// Configuration is loaded from bruinet.maui.config.json. 
/// For per-test overrides, use SetupWith() before running your test.
/// Environment variables are still supported for backward compatibility but are deprecated.
/// </remarks>
public abstract class MauiTestFixtureBase : IDisposable
{
    private static int _instanceCount;
    private readonly int _instanceId;
    private readonly MauiTestContextOptions _options;
    private MauiTestContext _context;
    private IScreenshotService _screenshotService;
    private bool _disposed;

    /// <summary>
    /// Current MAUI configuration loaded from bruinet.maui.config.json
    /// </summary>
    protected BruinetMauiConfiguration Configuration { get; private set; }


    /// <summary>
    /// Initializes the fixture by loading configuration and creating the test context and screenshot service.
    /// </summary>
    protected MauiTestFixtureBase()
    {
        _instanceId = Interlocked.Increment(ref _instanceCount);
        Console.WriteLine($"[FIXTURE] {GetType().Name} #{_instanceId} CREATING at {DateTime.Now:HH:mm:ss.fff}");

        Configuration = BruinetMauiConfiguration.Load();

        _options = Options();

        _context = new MauiTestContext(_options);
        UseTheAppsCulture();

        Console.WriteLine($"[FIXTURE] {GetType().Name} #{_instanceId} CREATED - Driver session started");

        _screenshotService = CreateScreenshotService(_context);
    }

    /// <summary>
    /// Reads dates and times in the culture the app writes them in, where that is the device's.
    /// </summary>
    /// <remarks>
    /// <see cref="Controls.DateTimes.DateTimeFormats"/> defaults to this process's culture, which is
    /// the Windows app's too. A device formats with its own locale instead, and a DatePicker read
    /// with this machine's custom short date ("dd-MMM-yy") found no date in "10/25/2026".
    /// </remarks>
    private void UseTheAppsCulture()
    {
        if (_context.Driver.DeviceCulture is not { } culture)
        {
            return;
        }

        Controls.DateTimes.DateTimeFormats.Culture = culture;
        Controls.DateTimes.DateTimeFormats.Date = culture.DateTimeFormat.ShortDatePattern;
        Controls.DateTimes.DateTimeFormats.Time = culture.DateTimeFormat.ShortTimePattern;
    }

    /// <summary>
    /// Closes the app and launches it again, with the options it was first launched with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a test whose subject is a restart ("the data is still there"), and for measuring what a
    /// fresh app per test would cost. What the first launch arranged outside the app - files,
    /// servers, environment - is left as it is: <see cref="CreateTestContextOptions"/> is not
    /// called again, so a fixture that arranges state there keeps that state.
    /// </para>
    /// <para>
    /// <see cref="Context"/> and <see cref="ScreenshotService"/> are new objects afterwards. Page
    /// and control objects built on the old context are stale: build new ones.
    /// </para>
    /// </remarks>
    public void RestartApp()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Console.WriteLine($"[FIXTURE] {GetType().Name} #{_instanceId} RESTARTING at {DateTime.Now:HH:mm:ss.fff}");

        try
        {
            _context.Dispose();
        }
        catch (Exception error)
        {
            // A session being replaced because it is lost may not close cleanly; the new one is
            // what matters.
            Console.WriteLine($"[FIXTURE] {GetType().Name} #{_instanceId} old session did not close: {error.Message}");
        }

        // Headless a restart is a fresh app; on a device the first launch's options are kept.
        _context = new MauiTestContext(MauiHeads.IsHeadless ? CreateHeadlessOptions() : _options);
        UseTheAppsCulture();
        _screenshotService = CreateScreenshotService(_context);
        OnRestarted();

        Console.WriteLine($"[FIXTURE] {GetType().Name} #{_instanceId} RESTARTED - Driver session started");
    }

    /// <summary>
    /// Called after <see cref="RestartApp"/> has started the new session: rebuild here whatever page
    /// or control objects the fixture keeps, since those built on the old context are stale.
    /// </summary>
    protected virtual void OnRestarted()
    {
    }

    /// <summary>
    /// Whether <paramref name="error"/> says the driver session can no longer answer anything.
    /// </summary>
    /// <remarks>
    /// The app or its driver is gone: <see cref="AppUnavailableException"/>, which every driver
    /// raises for it (on Appium, <c>AppiumErrors.Live</c> reads the server's messages: the
    /// UiAutomator2 server on the device crashing, a session ended, a command not answered). One
    /// crash took more than 200 tests of a run with it before fixtures restarted on this. Looked for
    /// through wrapped errors too, as a call or a step may report it inside its own.
    /// </remarks>
    /// <param name="error">The error a navigation or lookup raised.</param>
    /// <returns>Whether a new session is the only way on.</returns>
    public static bool IsSessionLost(Exception error)
    {
        for (var current = error; current is not null; current = current.InnerException)
        {
            if (current is Bruinet.Core.Exceptions.AppUnavailableException)
            {
                return true;
            }
        }

        return false;
    }

    private IScreenshotService CreateScreenshotService(MauiTestContext context)
    {
        var screenshotSettings = new ScreenshotSettings
        {
            OutputDirectory = GetScreenshotDirectory(),
            CaptureOnFailure = true,
            IncludeTimestamp = true,
            Format = ScreenshotFormat.Png
        };
        var service = new ScreenshotService(context, context.Logger, screenshotSettings);
        ScreenshotTestAttribute.SetService(service);
        return service;
    }

    /// <summary>
    /// Gets the MAUI test context.
    /// </summary>
    public MauiTestContext Context => _context;

    /// <summary>
    /// Gets the screenshot service.
    /// </summary>
    public IScreenshotService ScreenshotService => _screenshotService;

    /// <summary>
    /// Gets the current platform: MauiHeadless on that head, else the configuration's.
    /// </summary>
    protected MauiPlatform Platform => MauiHeads.IsHeadless ? MauiPlatform.Headless : Configuration?.Maui?.Platform ?? MauiPlatform.Windows;

    #region Abstract Methods

    /// <summary>
    /// Gets the default app path based on platform. Must be implemented by derived classes.
    /// </summary>
    /// <param name="platform">The platform: "windows", "android", or "ios".</param>
    /// <returns>The path to the app executable or package.</returns>
    protected abstract string GetDefaultAppPath(MauiPlatform platform);

    #endregion

    #region Configuration Setup

    /// <summary>
    /// Allows per-test configuration overrides.
    /// Call this before your test logic to customize the configuration for a specific test.
    /// </summary>
    /// <example>
    /// <code>
    /// var fixture = new MyTestFixture();
    /// fixture.SetupWith(config => {
    ///     config.Maui.Platform = "android";
    ///     config.Maui.DeviceName = "emulator-5556";
    /// });
    /// // Now run your test with the customized configuration
    /// </code>
    /// </example>
    protected void SetupWith(Action<BruinetMauiConfiguration> configureAction)
    {
        ArgumentNullException.ThrowIfNull(configureAction);
        configureAction(Configuration);
    }

    #endregion

    #region Heads

    // The device unless BRUINET_HEAD=headless (MauiHeads).
    private MauiTestContextOptions Options()
        => MauiHeads.IsHeadless ? CreateHeadlessOptions() : CreateTestContextOptions();

    /// <summary>
    /// The suite's MauiHeadless head (<see cref="MauiHeads"/>, docs/guides/maui-headless.md): its app's first page rendered in
    /// memory, the options from <c>Bruinet.Maui.Headless</c>'s <c>MauiHeadlessHead.Options</c>. A suite that has
    /// one overrides this; by default an MauiHeadless run of the suite says it has none.
    /// </summary>
    /// <remarks>Asked again on <see cref="RestartApp"/>, so a restart is a fresh headless app.</remarks>
    protected virtual MauiTestContextOptions CreateHeadlessOptions()
        => throw new InvalidOperationException(
            $"{GetType().Name} has no MauiHeadless head: override CreateHeadlessOptions, or run it without {MauiHeads.Variable}=headless.");

    #endregion

    #region Virtual Configuration Methods

    /// <summary>
    /// Creates test context options with platform-specific capabilities.
    /// Uses configuration loaded from bruinet.maui.config.json.
    /// Override to further customize driver configuration.
    /// </summary>
    protected virtual MauiTestContextOptions CreateTestContextOptions()
    {
        ArgumentNullException.ThrowIfNull(Configuration, nameof(Configuration));
        ArgumentNullException.ThrowIfNull(Configuration.Maui, nameof(Configuration.Maui));

        var driverOptions = MauiDriverOptions.FromConfiguration(Configuration.Maui);

        driverOptions.Timeouts = new TimeoutSettings
        {
            DefaultWait = 5000,
            PageLoad = 10000,
            ElementFind = 3000,
            ElementState = 3000,
            Animation = 300,
            PollingInterval = 100
        };
        driverOptions.AppPath = string.IsNullOrWhiteSpace(Configuration.Maui.AppPath)
            ? GetDefaultAppPath(Configuration.Maui.Platform)
            : Configuration.Maui.AppPath;

        switch (Configuration.Maui.Platform)
        {
            case MauiPlatform.Android:
                ConfigureAndroidOptions(driverOptions);
                break;
            case MauiPlatform.iOS:
                ConfigureiOSOptions(driverOptions);
                break;
                // Windows uses FlaUI - no additional options needed
        }

        return new MauiTestContextOptions
        {

            DriverOptions = driverOptions,
            Timeouts = driverOptions.Timeouts
        };
    }

    /// <summary>
    /// Configures driver options for Android MAUI app testing.
    /// Override to customize Android capabilities.
    /// </summary>
    protected virtual void ConfigureAndroidOptions(MauiDriverOptions options)
    {
        var serverUri = Configuration?.Maui?.ServerUri ?? "http://127.0.0.1:4723";
        var deviceName = Configuration?.Maui?.DeviceName ?? "emulator-5554";

        options.AppiumServerUri = new Uri(serverUri);
        options.DeviceName = deviceName;
        AddUdid(options);
    }

    /// <summary>
    /// Configures driver options for iOS MAUI app testing.
    /// Override to customize iOS capabilities.
    /// </summary>
    protected virtual void ConfigureiOSOptions(MauiDriverOptions options)
    {
        var serverUri = Configuration?.Maui?.ServerUri ?? "http://127.0.0.1:4723";
        var (deviceName, platformVersion) =
            MauiDriverFactory.IosDevice(Configuration?.Maui?.DeviceName, Configuration?.Maui?.PlatformVersion);


        options.AppiumServerUri = new Uri(serverUri);
        options.DeviceName = deviceName;
        options.PlatformVersion = platformVersion;
        AddUdid(options);
    }

    private void AddUdid(MauiDriverOptions options)
    {
        if (!string.IsNullOrWhiteSpace(Configuration?.Maui?.Udid))
        {
            options.AdditionalCapabilities["udid"] = Configuration.Maui.Udid;
        }
    }

    #endregion

    #region Utility Methods

    protected virtual string GetScreenshotDirectory()
    {
        var path = GetArtifactPathProvider().ScreenshotsDirectory;
        Directory.CreateDirectory(path);
        return path;
    }

    protected virtual ITestArtifactPathProvider GetArtifactPathProvider()
    {
        return DefaultTestArtifactPathProvider.Create(Configuration.Artifacts, GetType().Assembly.GetName().Name);
    }

    /// <summary>
    /// Finds the solution root directory by searching for *.sln files.
    /// </summary>
    protected static string FindSolutionDirectory()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.sln").Length > 0)
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return Directory.GetCurrentDirectory();
    }

    #endregion

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed and unmanaged resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        Console.WriteLine($"[FIXTURE] {GetType().Name} #{_instanceId} DISPOSING at {DateTime.Now:HH:mm:ss.fff}");

        if (disposing)
        {
            _context?.Dispose();
        }

        Console.WriteLine($"[FIXTURE] {GetType().Name} #{_instanceId} DISPOSED");
        _disposed = true;
    }

    #endregion
}
