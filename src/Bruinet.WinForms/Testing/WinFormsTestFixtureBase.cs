using Bruinet.Core.Configuration;
using Bruinet.WinForms.Context;
using Bruinet.Core.Artifacts;

namespace Bruinet.WinForms.Testing;

/// <summary>
/// Base fixture for WinForms UI tests that manages the FlaUI driver and test context lifecycle.
/// Inherit from this class and implement <see cref="GetDefaultAppPath"/>.
/// </summary>
/// <remarks>
/// Configuration is loaded from bruinet.winforms.config.json.
/// For per-test overrides, use SetupWith() before running your test.
/// Environment variables are still supported for backward compatibility but are deprecated.
/// </remarks>
public abstract class WinFormsTestFixtureBase : IDisposable
{
    private static int _instanceCount;
    private readonly int _instanceId;
    private readonly WinFormsTestContext _context;
    private bool _disposed;

    /// <summary>
    /// Current WinForms configuration loaded from bruinet.winforms.config.json
    /// </summary>
    protected BruinetWinFormsConfiguration Configuration { get; private set; }

    /// <summary>
    /// Initializes the fixture by loading configuration and creating the test context.
    /// </summary>
    protected WinFormsTestFixtureBase()
    {
        _instanceId = Interlocked.Increment(ref _instanceCount);
        Console.WriteLine($"[FIXTURE] {GetType().Name} #{_instanceId} CREATING at {DateTime.Now:HH:mm:ss.fff}");

        Configuration = BruinetWinFormsConfiguration.Load();

        // The fixture's one extension point: a derived suite's options, read before its constructor runs.
#pragma warning disable MA0056
        var options = CreateTestContextOptions();
#pragma warning restore MA0056
        _context = new WinFormsTestContext(options);

        Console.WriteLine($"[FIXTURE] {GetType().Name} #{_instanceId} CREATED - Driver session started");
    }

    /// <summary>
    /// Gets the WinForms test context.
    /// </summary>
    public WinFormsTestContext Context => _context;

    #region Abstract Methods

    /// <summary>
    /// Gets the default path to the WinForms application executable.
    /// Must be implemented by derived classes.
    /// </summary>
    protected abstract string GetDefaultAppPath();

    #endregion

    #region Configuration Setup

    /// <summary>
    /// Allows per-test configuration overrides.
    /// </summary>
    protected void SetupWith(Action<BruinetWinFormsConfiguration> configureAction)
    {
        ArgumentNullException.ThrowIfNull(configureAction);
        configureAction(Configuration);
    }

    #endregion

    #region Options Creation

    /// <summary>
    /// Creates test context options from configuration or environment variables.
    /// Override to further customize driver configuration.
    /// </summary>
    protected virtual WinFormsTestContextOptions CreateTestContextOptions()
    {
        ArgumentNullException.ThrowIfNull(Configuration, nameof(Configuration));
        ArgumentNullException.ThrowIfNull(Configuration.WinForms, nameof(Configuration.WinForms));

        var attachToRunning = Configuration.WinForms.AttachToRunning;
        var processName = Configuration.WinForms.ProcessName;
        var windowHandleStr = Configuration.WinForms.WindowHandle;
        var windowHandle = ParseWindowHandle(windowHandleStr);

        var appPath = Configuration.WinForms.AppPath;
        if (string.IsNullOrWhiteSpace(appPath) && !attachToRunning)
        {
            appPath = GetDefaultAppPath();
        }

        var timeouts = new TimeoutSettings
        {
            DefaultWait = TimeSpan.FromMilliseconds(5000),
            PageLoad = TimeSpan.FromMilliseconds(10000),
            ElementFind = TimeSpan.FromMilliseconds(3000),
            ElementState = TimeSpan.FromMilliseconds(3000),
            Animation = TimeSpan.FromMilliseconds(300),
            PollingInterval = TimeSpan.FromMilliseconds(100)
        };

        if (attachToRunning && windowHandle != IntPtr.Zero)
        {
            return new WinFormsTestContextOptions
            {
                WindowHandle = windowHandle,
                Timeouts = timeouts
            };
        }

        if (attachToRunning && !string.IsNullOrWhiteSpace(processName))
        {
            var process = Process.GetProcessesByName(processName).FirstOrDefault()
                ?? throw new InvalidOperationException($"Process '{processName}' not found");
            return new WinFormsTestContextOptions
            {
                ProcessId = process.Id,
                Timeouts = timeouts
            };
        }

        return new WinFormsTestContextOptions
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
        if (string.IsNullOrWhiteSpace(value))
            return IntPtr.Zero;
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
        if (_disposed)
            return;

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
