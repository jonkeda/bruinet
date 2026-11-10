using Bruinet.Core.Configuration;
using Bruinet.Stride.Communication;
using Bruinet.Stride.Context;
using Bruinet.Stride.Infrastructure;
using Bruinet.Stride.Interfaces;
using Bruinet.Core.Artifacts;

namespace Bruinet.Stride.Testing;

/// <summary>
/// Base fixture for Stride UI tests that manages game process and automation lifecycle.
/// Provides async Initialize/Dispose for game lifecycle management.
/// Inherit from this class and implement <see cref="GetDefaultAppPath"/>.
/// </summary>
public abstract class StrideTestFixtureBase : IDisposable
{
    private StrideGameDriver? _driver;
    private StrideTestContext? _context;
    private bool _disposed;

    /// <summary>The settings file's name, beside the test assembly.</summary>
    public const string SettingsFile = "bruinet.stride.config.json";

    /// <summary>The options as loaded from <see cref="SettingsFile"/>, before <see cref="CreateOptions"/> adjusts them.</summary>
    protected StrideTestContextOptions Options { get; } = TestContextOptionsBase.Load<StrideTestContextOptions>(SettingsFile);

    /// <summary>
    /// Gets the Stride test context. Available after <see cref="InitializeAsync"/>.
    /// </summary>
    public IStrideTestContext Context => _context ?? throw new InvalidOperationException("Context not initialized. Call InitializeAsync first.");

    /// <summary>
    /// Gets the underlying game driver.
    /// </summary>
    protected StrideGameDriver Driver => _driver ?? throw new InvalidOperationException("Driver not initialized.");

    /// <summary>
    /// Gets the game window handle.
    /// </summary>
    protected IntPtr GameWindowHandle => _driver?.GameWindowHandle ?? IntPtr.Zero;

    #region Abstract / Virtual Methods

    /// <summary>
    /// Gets the default path to the game executable.
    /// </summary>
    protected abstract string GetDefaultAppPath();

    /// <summary>
    /// Creates the test context options. Override to customize.
    /// </summary>
    protected virtual StrideTestContextOptions CreateOptions(StrideTestContextOptions loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        loaded.GameExecutablePath ??= Environment.GetEnvironmentVariable("STRIDE_APP_PATH") ?? GetDefaultAppPath();
        if (loaded.GameArguments.Length == 0)
        {
            loaded.GameArguments = ["--automation"];
        }

        loaded.ScreenshotDirectory = GetScreenshotDirectory();
        loaded.LogDirectory = GetArtifactPathProvider().LogsDirectory;
        return loaded;
    }

    /// <summary>
    /// Gets the screenshot output directory. Override to customize.
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

    #endregion

    #region Options Setup

    /// <summary>
    /// Allows per-test configuration overrides.
    /// </summary>
    protected void SetupWith(Action<StrideTestContextOptions> configureAction)
    {
        ArgumentNullException.ThrowIfNull(configureAction);
        configureAction(Options);
    }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Starts the game process and connects to automation.
    /// </summary>
    public async Task InitializeAsync()
    {
        var options = CreateOptions(Options);

        // Generate a unique pipe name if the fixture hasn't set a custom one,
        // preventing orphaned game processes from blocking new test runs.
        if (options.PipeName == Communication.NamedPipeChannel.DefaultPipeName)
        {
            var uniquePipe = $"Bruinet.Stride.{Guid.NewGuid():N}";
            options.PipeName = uniquePipe;

            // Inject --pipe arg so the game-side automation server uses the same name
            var args = new List<string>(options.GameArguments);
            args.AddRange(["--pipe", uniquePipe]);
            options.GameArguments = args.ToArray();
        }

        _driver = new StrideGameDriver(options);
        await _driver.StartAsync();

        if (_driver.Channel == null)
            throw new InvalidOperationException("Failed to establish automation channel");

        var driver = _driver;
        _context = new StrideTestContext(driver.Channel, options, () => driver.GameWindowHandle);

        // Wait for game to be ready
        _context.WaitReady(options.StartupTimeout.ToMs());
    }

    /// <summary>
    /// Stops the game process and cleans up.
    /// </summary>
    public async Task DisposeAsync()
    {
        _context?.Dispose();
        _context = null;

        if (_driver != null)
        {
            await _driver.StopAsync();
            _driver.Dispose();
            _driver = null;
        }
    }

    #endregion

    #region Utility


    /// <summary>
    /// Finds the solution root directory.
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

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        _disposed = true;

        if (disposing)
        {
            DisposeAsync().GetAwaiter().GetResult();
        }
    }

    #endregion
}
