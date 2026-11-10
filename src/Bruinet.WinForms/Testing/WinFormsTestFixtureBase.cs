using Bruinet.Core.Configuration;
using Bruinet.WinForms.Context;
using Bruinet.Core.Artifacts;

namespace Bruinet.WinForms.Testing;

/// <summary>
/// Base fixture for WinForms UI tests that manages the FlaUI driver and test context lifecycle.
/// Inherit from this class and implement <see cref="GetDefaultAppPath"/>.
/// </summary>
/// <remarks>
/// Its options are loaded from bruinet.winforms.config.json.
/// For per-test overrides, use SetupWith() before running your test.
/// Environment variables are still supported for backward compatibility but are deprecated.
/// </remarks>
public abstract class WinFormsTestFixtureBase : IDisposable
{
    private static int _instanceCount;
    private readonly int _instanceId;
    private readonly WinFormsTestContext _context;
    private bool _disposed;

    /// <summary>The settings file's name, beside the test assembly.</summary>
    public const string SettingsFile = "bruinet.winforms.config.json";

    /// <summary>The options as loaded from <see cref="SettingsFile"/>, before <see cref="CreateTestContextOptions"/> adjusts them.</summary>
    protected WinFormsTestContextOptions Options { get; }

    /// <summary>
    /// Initializes the fixture by loading configuration and creating the test context.
    /// </summary>
    protected WinFormsTestFixtureBase()
    {
        _instanceId = Interlocked.Increment(ref _instanceCount);
        Console.WriteLine($"[FIXTURE] {GetType().Name} #{_instanceId} CREATING at {DateTime.Now:HH:mm:ss.fff}");

        Options = TestContextOptionsBase.Load<WinFormsTestContextOptions>(SettingsFile);

        // The fixture's one extension point: a derived suite's options, read before its constructor runs.
#pragma warning disable MA0056
        var options = CreateTestContextOptions(Options);
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

    #region Options Setup

    /// <summary>
    /// Allows per-test configuration overrides.
    /// </summary>
    protected void SetupWith(Action<WinFormsTestContextOptions> configureAction)
    {
        ArgumentNullException.ThrowIfNull(configureAction);
        configureAction(Options);
    }

    #endregion

    #region Options Creation

    /// <summary>
    /// Creates test context options from configuration or environment variables.
    /// Override to further customize driver configuration.
    /// </summary>
    protected virtual WinFormsTestContextOptions CreateTestContextOptions(WinFormsTestContextOptions loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        var attaches = loaded.WindowHandle is { } handle && handle != IntPtr.Zero
            || loaded.ProcessId is not null
            || !string.IsNullOrWhiteSpace(loaded.ProcessName);
        if (!attaches && string.IsNullOrWhiteSpace(loaded.ExecutablePath))
        {
            loaded.ExecutablePath = GetDefaultAppPath();
        }

        return loaded;
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
        return DefaultTestArtifactPathProvider.Create(Options.Artifacts, GetType().Assembly.GetName().Name);
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

        _disposed = true;

        Console.WriteLine($"[FIXTURE] {GetType().Name} #{_instanceId} DISPOSED");
    }

    #endregion
}
