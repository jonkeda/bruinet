using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Bruinet.Appium;

/// <summary>
/// The iOS simulator through <c>xcrun simctl</c>: start it, install and remove the app, open a link
/// and grant a permission. The simulator shares this Mac's loopback, so an app in it reaches a server
/// on <c>localhost</c> here as it is: unlike Android, no address changes.
/// </summary>
public static class IosHost
{
    /// <summary>
    /// Whether <paramref name="udid"/> names a simulator: a simulator's UDID is a GUID, a device's is
    /// not (<c>00008110-001A2C...</c> or 40 hex digits).
    /// </summary>
    public static bool IsSimulator(string? udid) => Guid.TryParseExact(udid, "D", out _);

    /// <summary>The simulator that is booted; the first when there are more.</summary>
    /// <exception cref="InvalidOperationException">No simulator is booted, or simctl failed.</exception>
    public static string BootedSimulator()
    {
        using var listing = JsonDocument.Parse(Simctl("list", "devices", "booted", "-j"));
        foreach (var runtime in listing.RootElement.GetProperty("devices").EnumerateObject())
        {
            foreach (var device in runtime.Value.EnumerateArray())
            {
                if (device.TryGetProperty("udid", out var udid) && udid.GetString() is { Length: > 0 } id)
                {
                    return id;
                }
            }
        }

        throw new InvalidOperationException("No iOS simulator is booted. Boot one, or name it in APPIUM_UDID.");
    }

    /// <summary>Boots <paramref name="udid"/> when it is not, and waits until it has finished booting.</summary>
    public static void Boot(string udid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(udid);
        Simctl(TimeSpan.FromMinutes(3), "bootstatus", udid, "-b");
    }

    /// <summary>Installs the simulator <c>.app</c> at <paramref name="appPath"/>.</summary>
    public static void Install(string udid, string appPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(udid);
        ArgumentException.ThrowIfNullOrWhiteSpace(appPath);
        Simctl(TimeSpan.FromMinutes(3), "install", udid, appPath);
    }

    /// <summary>Removes the app with <paramref name="bundleId"/>, and its data. An app that is not installed is no error.</summary>
    public static void Uninstall(string udid, string bundleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(udid);
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleId);
        Simctl("uninstall", udid, bundleId);
    }

    /// <summary>Erases the simulator: every app and setting. It must be shut down.</summary>
    public static void Erase(string udid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(udid);
        Simctl(TimeSpan.FromMinutes(2), "erase", udid);
    }

    /// <summary>Opens <paramref name="url"/> on the simulator: a deep link goes to the app that handles it.</summary>
    public static void OpenUrl(string udid, string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(udid);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        Simctl("openurl", udid, url);
    }

    /// <summary>
    /// Grants the app a permission before it asks, so no system alert shows: <c>photos</c>,
    /// <c>camera</c>, <c>location</c>, <c>contacts</c>, <c>microphone</c>, ... (<c>simctl privacy</c>).
    /// </summary>
    public static void Grant(string udid, string service, string bundleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(udid);
        ArgumentException.ThrowIfNullOrWhiteSpace(service);
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleId);
        Simctl("privacy", udid, "grant", service, bundleId);
    }

    /// <summary>The screen's size in pixels and its scale (pixels per point), from the simulator's environment.</summary>
    /// <exception cref="InvalidOperationException">simctl failed, or the simulator does not say.</exception>
    public static (int Width, int Height, double Scale) Screen(string udid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(udid);
        return (
            (int)Number(udid, "SIMULATOR_MAINSCREEN_WIDTH"),
            (int)Number(udid, "SIMULATOR_MAINSCREEN_HEIGHT"),
            Number(udid, "SIMULATOR_MAINSCREEN_SCALE"));
    }

    private static double Number(string udid, string name)
        => double.TryParse(Simctl("getenv", udid, name).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : throw new InvalidOperationException($"The simulator {udid} does not give {name}.");

    private static string Simctl(params string[] arguments) => Simctl(TimeSpan.FromSeconds(60), arguments);

    /// <summary>Runs <c>xcrun simctl</c> and returns what it printed.</summary>
    /// <exception cref="InvalidOperationException">It failed or did not finish; the message carries its output.</exception>
    internal static string Simctl(TimeSpan timeout, params string[] arguments)
    {
        var start = new ProcessStartInfo("xcrun")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("simctl");
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("xcrun could not be started. Is Xcode installed?");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeout))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException($"simctl {string.Join(' ', arguments)} did not finish in {timeout.TotalSeconds:0} s.");
        }

        return process.ExitCode == 0
            ? output.Result
            : throw new InvalidOperationException($"simctl {string.Join(' ', arguments)} failed ({process.ExitCode}): {(error.Result + output.Result).Trim()}");
    }
}
