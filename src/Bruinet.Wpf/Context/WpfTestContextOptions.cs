using Bruinet.Core.Calls;

namespace Bruinet.Wpf.Context;

/// <summary>
/// How a WPF test context reaches its app: a driver it is given, a window or process to attach to,
/// or an executable to launch (in that order).
/// </summary>
public class WpfTestContextOptions : Bruinet.Core.Configuration.TestContextOptionsBase
{
    /// <summary>A driver made already (a test's own, or another head's). The context does not dispose it.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IWpfDriver? Driver { get; set; }

    /// <summary>The app's executable, launched when no driver, window or process is given.</summary>
    public string? ExecutablePath { get; set; }

    /// <summary>The app's command line.</summary>
    public string? Arguments { get; set; }

    /// <summary>Variables for the launched app's process only (its launch settings, <c>BRUINET_AUT_PLACE</c>).</summary>
    public IReadOnlyDictionary<string, string>? Environment { get; set; }

    /// <summary>
    /// How long a launched app may take to be ready for input and show its main window, as Stride's
    /// <c>StartupTimeout</c> and <c>WebAppHost</c>'s <c>StartTimeout</c> (review 02, finding 9). Scaled by
    /// <c>BRUINET_TIMEOUT_SCALE</c>.
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(Bruinet.Core.Configuration.MillisecondsTimeSpanConverter))]
    public TimeSpan StartTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>A running process to attach to.</summary>
    public int? ProcessId { get; set; }

    /// <summary>A window to attach to.</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(Bruinet.Core.Configuration.WindowHandleJsonConverter))]
    public IntPtr? WindowHandle { get; set; }

    /// <summary>A running process to attach to, by name, when no handle or id is given.</summary>
    public string? ProcessName { get; set; }




}
