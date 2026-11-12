using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Bruinet.Core.Exceptions;

namespace Bruinet.Maui.DevFlow;

/// <summary>
/// An Android app the runner launched over adb: installed, started by its launcher activity with the
/// launch settings as string extras (as the Appium runner passes them), and its agent's port on the
/// device forwarded to a free port here, which the agent's loopback-only server needs.
/// </summary>
internal sealed class AndroidLaunch : ILaunchedApp
{
    private readonly string _adb;
    private readonly string[] _device;
    private readonly string _package;

    private AndroidLaunch(string adb, string[] device, string package, int agentPort)
    {
        _adb = adb;
        _device = device;
        _package = package;
        AgentPort = agentPort;
    }

    /// <summary>The port here that reaches the app's agent on the device.</summary>
    public int AgentPort { get; }

    /// <summary>Installs <paramref name="apk"/>, starts <paramref name="package"/> and forwards its agent's port.</summary>
    /// <param name="apk">The APK, built with the agent.</param>
    /// <param name="package">The app's package.</param>
    /// <param name="serial">The device or emulator, as adb lists it; null for the only one.</param>
    /// <param name="devicePort">The agent's port on the device: 9223 unless the app was built for another.</param>
    /// <param name="settings">The launch settings, passed as string extras.</param>
    public static AndroidLaunch Start(
        string apk, string package, string? serial, int devicePort, IReadOnlyDictionary<string, string> settings)
    {
        var adb = Adb();
        string[] device = string.IsNullOrWhiteSpace(serial) ? [] : ["-s", serial];

        Run(adb, [.. device, "install", "-r", apk], TimeSpan.FromMinutes(3));
        Run(adb, [.. device, "shell", "am", "force-stop", package]);

        // MAUI hashes the activity's name, so it is asked for rather than written down.
        var activity = Run(adb, [.. device, "shell", "cmd", "package", "resolve-activity", "--brief",
                "-a", "android.intent.action.MAIN", "-c", "android.intent.category.LAUNCHER", package])
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(line => line.Contains('/', StringComparison.Ordinal))
            ?? throw new BruinetException($"'{package}' has no launcher activity on the device. Is the APK installed?");

        List<string> start = [.. device, "shell", "am", "start", "-W", "-n", activity];
        foreach (var (name, value) in settings)
        {
            if (value.Any(c => char.IsWhiteSpace(c) || c is '"' or '\''))
                throw new ArgumentException($"Launch setting '{name}' cannot travel as an Android extra: its value has whitespace or quotes.");
            start.AddRange(["--es", name, value]);
        }

        Run(adb, start);

        var hostPort = LaunchedApp.FreePort();
        Run(adb, [.. device, "forward", $"tcp:{hostPort}", $"tcp:{devicePort}"]);
        return new AndroidLaunch(adb, device, package, hostPort);
    }

    /// <summary>The app's process id on the device; 0 while it is not running.</summary>
    public int ProcessId
    {
        get
        {
            try
            {
                return int.TryParse(Run(_adb, [.. _device, "shell", "pidof", _package]).Trim().Split(' ')[0], NumberStyles.None,
                    CultureInfo.InvariantCulture, out var pid) ? pid : 0;
            }
            catch (BruinetException)
            {
                // pidof fails when nothing runs under the package.
                return 0;
            }
        }
    }

    public bool HasExited => ProcessId == 0;

    public string ExitDescription => $"'{_package}' stopped on the device";

    public void Dispose()
    {
        try
        {
            Run(_adb, [.. _device, "shell", "am", "force-stop", _package]);
            Run(_adb, [.. _device, "forward", "--remove", $"tcp:{AgentPort}"]);
        }
        catch (Exception error) when (error is BruinetException or Win32Exception)
        {
            // The device has gone: there is nothing left to stop.
        }
    }

    // adb from the SDK the environment names, else the default SDK place, else the PATH.
    private static string Adb()
    {
        foreach (var sdk in new[]
                 {
                     Environment.GetEnvironmentVariable("ANDROID_HOME"),
                     Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk"),
                 })
        {
            if (string.IsNullOrWhiteSpace(sdk))
                continue;
            var adb = Path.Combine(sdk, "platform-tools", OperatingSystem.IsWindows() ? "adb.exe" : "adb");
            if (File.Exists(adb))
                return adb;
        }

        return "adb";
    }

    private static string Run(string adb, IReadOnlyList<string> arguments, TimeSpan? timeout = null)
    {
        var start = new ProcessStartInfo(adb)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new BruinetException($"adb did not start: {adb}");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeout ?? TimeSpan.FromSeconds(60)))
        {
            process.Kill(entireProcessTree: true);
            throw new BruinetException($"adb {string.Join(' ', arguments)} did not finish in time.");
        }

        if (process.ExitCode != 0)
            throw new BruinetException($"adb {string.Join(' ', arguments)} failed: {error.Result.Trim()} {output.Result.Trim()}".Trim());
        return output.Result;
    }
}
