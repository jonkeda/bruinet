using Bruinet.Core.Configuration;
using Bruinet.Core.Calls;
using Bruinet.Core.Recording;
using Bruinet.Core.Scopes;
using Bruinet.Stride.Automation;
using Bruinet.Stride.Pages;

namespace Bruinet.Stride.Context;

/// <summary>
/// One Stride game under test, and its UI root as the outermost scope: ready while the game says it
/// is (its scene loaded). A game that is gone ends the call with <see cref="AppUnavailableException"/> (R0).
/// </summary>
public sealed class StrideTestContext : IStrideTestContext, IRecordable
{
    private readonly StrideTestContextOptions _options;
    private bool _disposed;

    /// <summary>The context over an open automation channel.</summary>
    /// <param name="channel">The connected pipe to the game.</param>
    /// <param name="options">The timeouts and names; the defaults when null.</param>
    /// <param name="windowHandle">The game window's handle, for a recorder; zero when unknown.</param>
    public StrideTestContext(IAutomationChannel channel, StrideTestContextOptions? options = null, Func<nint>? windowHandle = null)
        : this(new StrideAutomationDriver(channel, windowHandle), options)
    {
    }

    /// <summary>The context over a driver.</summary>
    public StrideTestContext(IStrideDriver driver, StrideTestContextOptions? options = null)
    {
        Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _options = options ?? new StrideTestContextOptions();
        Timeouts = _options.Timeouts;
        Logger = _options.Logger ?? (ITestLogger?)OpenCallLog() ?? NullTestLogger.Instance;
    }

    /// <inheritdoc />
    public IStrideDriver Driver { get; }

    /// <inheritdoc />
    public string? Name => _options.Name;

    /// <inheritdoc />
    public NearMissSettings NearMiss => _options.NearMiss;

    /// <inheritdoc />
    public TimeoutSettings Timeouts { get; }

    /// <inheritdoc />
    public ITestLogger Logger { get; }

    /// <inheritdoc />
    public IStrideTestContext Context => this;

    /// <inheritdoc />
    public LocatorStrategy DefaultLocatorStrategy => LocatorStrategy.AutomationId;

    /// <summary>None: the context is the game's UI root, not a page.</summary>
    public IStridePage? Page => null;

    /// <inheritdoc />
    public RecordingTarget? RecordingTarget
        => _disposed || Driver.WindowHandle == 0 ? null : new RecordingTarget.Window(Driver.WindowHandle);

    /// <summary>Ready while the game says it is.</summary>
    public ScopeReadiness ProbeReadiness()
        => Driver.IsGameReady()
            ? ScopeReadiness.Ready(nameof(StrideTestContext))
            : new ScopeReadiness(nameof(StrideTestContext), ScopeReadinessState.NotLoaded, "the game is not ready");

    /// <inheritdoc />
    public int WaitFirst(int? timeoutMs, params Func<bool>[] outcomes) => new AppRoot(this).WaitFirst(timeoutMs, outcomes);

    /// <inheritdoc />
    public bool WaitReady(int? timeoutMs = null) => new AppRoot(this).WaitReady(timeoutMs ?? Timeouts.PageLoad.ToMs());

    // One attempt (R2): the call around it is what waits.
    /// <inheritdoc />
    public IStrideElement? TryFindElement(Locator locator)
        => StrideElement.From(Driver, Driver.Find(string.Empty, StrideLocators.TargetOf(locator)));

    /// <inheritdoc />
    public IStrideElement FindElement(Locator locator) => TryFindElement(locator) ?? throw new ElementNotFoundException(locator);

    /// <inheritdoc />
    public IReadOnlyList<IStrideElement> FindElements(Locator locator)
        => [.. Driver.FindAll(string.Empty, StrideLocators.TargetOf(locator)).Select(state => StrideElement.From(Driver, state)).OfType<IStrideElement>()];

    /// <inheritdoc />
    public void PressKey(VirtualKey key) => Driver.PressKey(key);

    /// <inheritdoc />
    public void HoldKey(VirtualKey key, int durationMs) => Driver.HoldKey(key, durationMs);

    /// <summary>A game has no destination to go to: its page objects navigate with its controls and keys.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public void NavigateTo(string destination)
        => throw new NotSupportedException("A Stride game has no destination to navigate to; navigate with its page objects' controls and keys.");

    /// <summary>A game has no history to go back in.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public void NavigateBack()
        => throw new NotSupportedException("A Stride game has no history to go back in; navigate with its page objects' controls and keys.");

    /// <summary>A game has no page to reload.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public void Refresh()
        => throw new NotSupportedException("A Stride game has no page to reload; start a new context for a fresh game.");

    /// <inheritdoc />
    public byte[] TakeScreenshot() => Driver.TakeScreenshot();

    /// <inheritdoc />
    public void SaveScreenshot(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var bytes = Driver.TakeScreenshot();
        if (bytes.Length > 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
            File.WriteAllBytes(path, bytes);
        }
    }

    /// <summary>A game cannot be reset from outside: start a new context for a fresh game.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public void ResetAppState()
        => throw new NotSupportedException("A Stride game cannot be reset from outside; start a new context for a fresh game.");

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Driver.Dispose();
        Logger.Flush();
        Logger.Dispose();
    }

    /// <summary>
    /// A call log in the folder <c>BRUINET_CALL_LOG</c> names, when it is set: the same switch as
    /// the other stacks', and only when the options give no logger of their own.
    /// </summary>
    private static CsvTestLogger? OpenCallLog()
    {
        var folder = Environment.GetEnvironmentVariable("BRUINET_CALL_LOG");
        return string.IsNullOrWhiteSpace(folder)
            ? null
            : new CsvTestLogger(Path.Combine(folder,
                $"calls-stride-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}-{Guid.NewGuid().ToString("N")[..8]}.csv"));
    }
}
