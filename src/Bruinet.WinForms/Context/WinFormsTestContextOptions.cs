using Bruinet.Core.Calls;

namespace Bruinet.WinForms.Context;

/// <summary>
/// How a WinForms test context reaches its app: a driver it is given, a window or process to attach to,
/// or an executable to launch (in that order).
/// </summary>
public class WinFormsTestContextOptions : Bruinet.Core.Configuration.TestContextOptionsBase
{
    /// <summary>A driver made already (a test's own, or another head's). The context does not dispose it.</summary>
    public IWinFormsDriver? Driver { get; init; }

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




}
