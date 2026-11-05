using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Bruinet.Maui.Enums;

namespace Bruinet.Maui.Configuration;

/// <summary>
/// Whether the simulator build a suite asks XCUITest to install is already the one installed, so
/// the session can start without removing and installing it again.
/// </summary>
/// <remarks>
/// <para>
/// Without <c>noReset</c>, XCUITest removes the app and installs it at every session start: about
/// 5 s a class on an iPhone 17 simulator, which is a quarter of the session start
/// (.my/performance). The <c>.app</c>'s fingerprint and the installed bundle's container are
/// recorded on the host after an install; a session whose <c>.app</c> and container still match
/// both starts with <c>noReset</c>.
/// </para>
/// <para>
/// Such a session still starts clean: the app is stopped, its preferences deleted and its data
/// container emptied before the session, which is what the removal did. The keychain survives
/// either way. <c>BRUINET_ALWAYS_REINSTALL=1</c> installs every time; a suite that sets
/// <c>noReset</c> or <c>fullReset</c> itself, and anything simctl cannot answer, does too.
/// </para>
/// </remarks>
internal sealed class IosInstalledBuild
{
    private const string NoReset = "noReset";

    // Top-level folders of a fresh data container; their contents are the app's state.
    private static readonly string[] DataFolders = ["Documents", "Library", "tmp"];

    private readonly string _udid;
    private readonly string _bundleId;
    private readonly string _fingerprint;

    private IosInstalledBuild(string udid, string bundleId, string fingerprint, bool current)
    {
        _udid = udid;
        _bundleId = bundleId;
        _fingerprint = fingerprint;
        IsCurrent = current;
    }

    /// <summary>Whether the simulator has this build installed, so the session skips the install.</summary>
    public bool IsCurrent { get; }

    /// <summary>
    /// Checks the simulator for an iOS session. When the build there is this one, its data is
    /// cleared and the session gets <c>noReset</c>. Null when it does not apply.
    /// </summary>
    public static IosInstalledBuild? Check(MauiDriverOptions options)
    {
        if (options.Platform != MauiPlatform.iOS
            || !OperatingSystem.IsMacOS()
            || options.AppPath is not { } app
            || !Directory.Exists(app)
            || options.AdditionalCapabilities.ContainsKey(NoReset)
            || options.AdditionalCapabilities.ContainsKey("fullReset")
            || Environment.GetEnvironmentVariable("BRUINET_ALWAYS_REINSTALL") == "1"
            || !(options.AdditionalCapabilities.TryGetValue("udid", out var value) && value is string { Length: > 0 } udid))
        {
            return null;
        }

        try
        {
            if (Tool("plutil", ["-extract", "CFBundleIdentifier", "raw", "-o", "-", Path.Combine(app, "Info.plist")]) is not { Length: > 0 } bundleId)
            {
                return null;
            }

            var fingerprint = FingerprintOf(app);
            var recorded = File.Exists(MarkerPath(udid, bundleId)) ? File.ReadAllText(MarkerPath(udid, bundleId)).Trim() : null;
            var current = InstalledAt(udid, bundleId) is { } installed
                          && recorded == $"{fingerprint} {installed}"
                          && ClearData(udid, bundleId);
            if (current)
            {
                options.AdditionalCapabilities[NoReset] = true;
            }

            return new IosInstalledBuild(udid, bundleId, fingerprint, current);
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>After the session started: records what was installed, if this session installed it.</summary>
    public void RememberInstalled()
    {
        if (IsCurrent)
        {
            return;
        }

        try
        {
            if (InstalledAt(_udid, _bundleId) is { } installed)
            {
                File.WriteAllText(MarkerPath(_udid, _bundleId), $"{_fingerprint} {installed}");
            }
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception or IOException or UnauthorizedAccessException)
        {
            // Not recorded: the next session installs again, as it always did.
        }
    }

    private static string MarkerPath(string udid, string bundleId)
        => Path.Combine(Path.GetTempPath(), $"bruinet-ios-{udid}-{bundleId}.build");

    // The installed bundle's container: every install gets a new one, so a record left by an
    // install that was then replaced no longer matches.
    private static string? InstalledAt(string udid, string bundleId)
        => Tool("xcrun", ["simctl", "get_app_container", udid, bundleId, "app"]);

    // What the removal and install did to the app's state: preferences and data container empty.
    private static bool ClearData(string udid, string bundleId)
    {
        _ = Tool("xcrun", ["simctl", "terminate", udid, bundleId]);
        // Through cfprefsd, which would otherwise write its cached copy back.
        _ = Tool("xcrun", ["simctl", "spawn", udid, "defaults", "delete", bundleId]);

        if (Tool("xcrun", ["simctl", "get_app_container", udid, bundleId, "data"]) is not { } data || !Directory.Exists(data))
        {
            return false;
        }

        foreach (var folder in DataFolders.Select(name => Path.Combine(data, name)).Where(Directory.Exists))
        {
            foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos())
            {
                if (entry is DirectoryInfo directory)
                    directory.Delete(recursive: true);
                else
                    entry.Delete();
            }
        }

        return true;
    }

    // Paths, sizes and write times: a rebuild changes at least one, and it costs no file reads.
    private static string FingerprintOf(string app)
    {
        var listing = new StringBuilder();
        foreach (var file in new DirectoryInfo(app).EnumerateFiles("*", SearchOption.AllDirectories)
                     .OrderBy(file => file.FullName, StringComparer.Ordinal))
        {
            listing.Append(Path.GetRelativePath(app, file.FullName)).Append('|')
                .Append(file.Length).Append('|')
                .Append(file.LastWriteTimeUtc.Ticks).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(listing.ToString())))[..32].ToLowerInvariant();
    }

    /// <summary>The tool's trimmed output, or null when it failed.</summary>
    private static string? Tool(string fileName, IReadOnlyList<string> arguments)
    {
        var info = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException($"{fileName} could not be started.");
        var output = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0 ? output.Trim() : null;
    }
}
