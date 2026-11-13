using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Bruinet.Core.Exceptions;
using Bruinet.Maui.Configuration;
using Bruinet.Maui.Enums;

namespace Bruinet.Maui.DevFlow;

/// <summary>
/// An iOS app the runner launched on a simulator with <c>xcrun simctl</c>: installed unless the same
/// build is (<see cref="IosInstalledBuild"/>, which then clears its data), and started with the launch
/// settings as environment variables (<c>SIMCTL_CHILD_</c>). The simulator shares this Mac's loopback,
/// so the agent answers on its port here with nothing forwarded, and the app is a process of this Mac.
/// </summary>
internal sealed class IosLaunch : ILaunchedApp
{
    private readonly string _udid;
    private readonly string _bundleId;

    private IosLaunch(string udid, string bundleId, int processId)
    {
        _udid = udid;
        _bundleId = bundleId;
        ProcessId = processId;
    }

    /// <summary>Installs the <c>.app</c> when needed and starts it with <paramref name="environment"/>.</summary>
    /// <param name="app">The simulator <c>.app</c> bundle, built with the agent.</param>
    /// <param name="udid">The simulator; null for the one booted.</param>
    /// <param name="environment">The app's environment variables.</param>
    public static IosLaunch Start(string app, string? udid, IReadOnlyDictionary<string, string> environment)
    {
        udid = string.IsNullOrWhiteSpace(udid) ? BootedSimulator() : udid;
        Run(["bootstatus", udid, "-b"], TimeSpan.FromMinutes(3));

        var bundleId = Tool("plutil", ["-extract", "CFBundleIdentifier", "raw", "-o", "-", Path.Combine(app, "Info.plist")]).Trim();

        // The Appium runner's check: an unchanged build is not installed again, its data is cleared instead.
        var check = new MauiDriverOptions { Platform = MauiPlatform.iOS, AppPath = app };
        check.AdditionalCapabilities["udid"] = udid;
        var installed = IosInstalledBuild.Check(check);
        if (installed?.IsCurrent != true)
        {
            // Removed first, so the app starts with no data, as a fresh install does.
            TryRun(["terminate", udid, bundleId]);
            TryRun(["uninstall", udid, bundleId]);
            Run(["install", udid, app], TimeSpan.FromMinutes(3));
            installed?.RememberInstalled();
        }

        var children = environment.ToDictionary(pair => "SIMCTL_CHILD_" + pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var output = Run(["launch", "--terminate-running-process", udid, bundleId], TimeSpan.FromMinutes(1), children);

        // "com.example.app: 1234"
        var pid = output.Trim().Split(':').LastOrDefault()?.Trim();
        if (!int.TryParse(pid, NumberStyles.None, CultureInfo.InvariantCulture, out var processId))
            throw new BruinetException($"simctl launch gave no process id for '{bundleId}': {output.Trim()}");
        return new IosLaunch(udid, bundleId, processId);
    }

    public int ProcessId { get; }

    public bool HasExited
    {
        get
        {
            try
            {
                using var process = Process.GetProcessById(ProcessId);
                return process.HasExited;
            }
            catch (ArgumentException)
            {
                return true;
            }
        }
    }

    public string ExitDescription => $"'{_bundleId}' stopped on the simulator";

    /// <summary>The simulator's locale (<c>en_US</c>), without its region override (<c>@rg=...</c>); null when it does not say.</summary>
    public string? Locale()
    {
        var value = TryRun(["spawn", _udid, "defaults", "read", "-g", "AppleLocale"])?.Trim();
        return string.IsNullOrEmpty(value) ? null : value.Split('@')[0];
    }

    public void Dispose() => TryRun(["terminate", _udid, _bundleId]);

    private static string BootedSimulator()
    {
        using var listing = JsonDocument.Parse(Run(["list", "devices", "booted", "-j"]));
        foreach (var runtime in listing.RootElement.GetProperty("devices").EnumerateObject())
        {
            foreach (var device in runtime.Value.EnumerateArray())
            {
                if (device.TryGetProperty("udid", out var udid) && udid.GetString() is { Length: > 0 } id)
                    return id;
            }
        }

        throw new BruinetException("No iOS simulator is booted. Boot one, or name it in APPIUM_UDID.");
    }

    private static string? TryRun(IReadOnlyList<string> arguments)
    {
        try
        {
            return Run(arguments);
        }
        catch (Exception error) when (error is BruinetException or Win32Exception)
        {
            return null;
        }
    }

    private static string Run(IReadOnlyList<string> arguments, TimeSpan? timeout = null, IReadOnlyDictionary<string, string>? environment = null)
        => Tool("xcrun", ["simctl", .. arguments], timeout, environment);

    private static string Tool(string fileName, IReadOnlyList<string> arguments, TimeSpan? timeout = null, IReadOnlyDictionary<string, string>? environment = null)
    {
        var start = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        foreach (var (name, value) in environment ?? new Dictionary<string, string>())
            start.Environment[name] = value;

        using var process = Process.Start(start) ?? throw new BruinetException($"{fileName} did not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeout ?? TimeSpan.FromSeconds(60)))
        {
            process.Kill(entireProcessTree: true);
            throw new BruinetException($"{fileName} {string.Join(' ', arguments)} did not finish in time.");
        }

        if (process.ExitCode != 0)
            throw new BruinetException($"{fileName} {string.Join(' ', arguments)} failed: {error.Result.Trim()} {output.Result.Trim()}".Trim());
        return output.Result;
    }
}
