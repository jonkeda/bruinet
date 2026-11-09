using Bruinet.Core.Calls;

namespace Bruinet.Wpf.Context;

/// <summary>
/// How a WPF test context reaches its app: a driver it is given, a window or process to attach to,
/// or an executable to launch (in that order).
/// </summary>
public class WpfTestContextOptions
{
    /// <summary>A driver made already (a test's own, or another head's). The context does not dispose it.</summary>
    public IWpfDriver? Driver { get; init; }

    /// <summary>The app's executable, launched when no driver, window or process is given.</summary>
    public string? ExecutablePath { get; init; }

    /// <summary>The app's command line.</summary>
    public string? Arguments { get; init; }

    /// <summary>Variables for the launched app's process only (its launch settings, <c>BRUINET_AUT_PLACE</c>).</summary>
    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    /// <summary>A running process to attach to.</summary>
    public int? ProcessId { get; init; }

    /// <summary>A window to attach to.</summary>
    public IntPtr? WindowHandle { get; init; }

    /// <summary>The timeouts every call uses; the defaults when null.</summary>
    public TimeoutSettings? Timeouts { get; init; }

    /// <summary>The call log; <c>BRUINET_CALL_LOG</c>'s, or none, when null.</summary>
    public ITestLogger? Logger { get; init; }

    /// <summary>The context's name in call logs, when a scenario runs several apps.</summary>
    public string? Name { get; init; }

    /// <summary>When a call that passed is logged as a near-miss.</summary>
    public NearMissSettings NearMiss { get; init; } = NearMissSettings.Default;
}
