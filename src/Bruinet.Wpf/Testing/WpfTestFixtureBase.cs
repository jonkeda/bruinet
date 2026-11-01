using Bruinet.Core.Configuration;
using Bruinet.Wpf.Context;
using Bruinet.Core.Artifacts;

namespace Bruinet.Wpf.Testing;

/// <summary>
/// Base fixture for WPF UI tests that manages the FlaUI driver and test context lifecycle.
/// Inherit from this class and implement <see cref="GetDefaultAppPath"/>.
/// </summary>
/// <remarks>
/// Configuration is loaded from bruinet.wpf.config.json.
/// For per-test overrides, use SetupWith() before running your test.
/// Environment variables are still supported for backward compatibility but are deprecated.
/// </remarks>
public abstract class WpfTestFixtureBase : IDisposable
{
    private static int _instanceCount;
    private readonly int _instanceId;
    private readonly WpfTestContext _context;
    private bool _disposed;

    /// <summary>
    /// Current WPF configuration loaded from bruinet.wpf.config.json
    /// </summary>
    protected BruinetWpfConfiguration Configuration { get; private set; }

    /// <summary>
    /// Initializes the fixture by loading configuration and creating the test context.
    /// </summary>
    protected WpfTestFixtureBase()
    {
        _instanceId = Interlocked.Increment(ref _instanceCount);
        Console.WriteLine($"[FIXTURE] {GetType().Name} #{_instanceId} CREATING at {DateTime.Now:HH:mm:ss.fff}");

        Configuration = BruinetWpfConfiguration.Load();

        // The fixture's one extension point: a derived suite's options, read before its constructor runs.
#pragma warning disable MA0056
        var options = CreateTestContextOptions();
#pragma warning restore MA0056
        _context = new WpfTestContext(options);

        Console.WriteLine($"[FIXTURE] {GetType().Name} #{_instanceId} CREATED - Driver session started");
    }

    /// <summary>
    /// Gets the WPF test context.
    /// </summary>
    public WpfTestContext Context => _context;

    #region Abstract Methods

    /// <summary>
    /// Gets the default path to the WPF application executable.
    /// Must be implemented by derived classes.
    /// </summary>
    protected abstract string GetDefaultAppPath();

    #endregion

    #region Configuration Setup

    /// <summary>
    /// Allows per-test configuration overrides.
    /// Call this before your test logic to customize the configuration for a specific test.
    /// </summary>
    protected void SetupWith(Action<BruinetWpfConfiguration> configureAction)
    {
        ArgumentNullException.ThrowIfNull(configureAction);
        configureAction(Configuration);
    }

    #endregion

    #region Virtual Configuration Methods

    /// <summary>
    /// Creates test context options from configuration or environment variables.
    /// Override to further customize driver configuration.
    /// </summary>
    protected virtual WpfTestContextOptions CreateTestContextOptions()
    {
        ArgumentNullException.ThrowIfNull(Configuration, nameof(Configuration));
        ArgumentNullException.ThrowIfNull(Configuration.Wpf, nameof(Configuration.Wpf));

        var attachToRunning = Configuration.Wpf.AttachToRunning;
        var processName = Configuration.Wpf.ProcessName;
        var windowHandleStr = Configuration.Wpf.WindowHandle;
        var windowHandle = ParseWindowHandle(windowHandleStr);

        var appPath = Configuration.Wpf.AppPath;
        if (string.IsNullOrWhiteSpace(appPath) && !attachToRunning)
        {
            appPath = GetDefaultAppPath();
        }

        var timeouts = new TimeoutSettings
        {
            DefaultWait = 5000,
            PageLoad = 10000,
            ElementFind = 3000,
            ElementState = 3000,
            Animation = 300,
            PollingInterval = 100
        };

        if (attachToRunning && windowHandle != IntPtr.Zero)
        {
            return new WpfTestContextOptions
            {
                WindowHandle = windowHandle,
                Timeouts = timeouts
            };
        }

        if (attachToRunning && !string.IsNullOrWhiteSpace(processName))
        {
            var process = Process.GetProcessesByName(processName).FirstOrDefault()
                ?? throw new InvalidOperationException($"Process '{processName}' not found");
            return new WpfTestContextOptions
            {
                ProcessId = process.Id,
                Timeouts = timeouts
            };
        }

        return new WpfTestContextOptions
        {
            ExecutablePath = appPath,
            Timeouts = timeouts
        };
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Gets the screenshot output directory path.
    /// Override to customize the screenshot location.
    /// </summary>
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

    private static bool ParseBool(string? value)
    {
        return bool.TryParse(value, out var result) && result;
    }

    private static IntPtr ParseWindowHandle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return IntPtr.Zero;
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (long.TryParse(value[2..], System.Globalization.NumberStyles.HexNumber, null, out var hex))
                return new IntPtr(hex);
        }
        if (long.TryParse(value, out var dec))
            return new IntPtr(dec);
        return IntPtr.Zero;
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
        if (_disposed) return;

        Console.WriteLine($"[FIXTURE] {GetType().Name} #{_instanceId} DISPOSING at {DateTime.Now:HH:mm:ss.fff}");

        if (disposing)
        {
            _context?.Dispose();
        }

        _disposed = true;

        Console.WriteLine($"[FIXTURE] {GetType().Name} #{_instanceId} DISPOSED");
    }

    #endregion
}
