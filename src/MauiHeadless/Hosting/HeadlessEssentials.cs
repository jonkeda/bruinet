using System.Reflection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using MauiHeadless.Internals;
using Accessibility = Microsoft.Maui.Accessibility;
using Media = Microsoft.Maui.Media;

namespace MauiHeadless.Hosting;

/// <summary>What <see cref="HeadlessEssentials.Install"/> reports about the app and its device.</summary>
public sealed class HeadlessEssentialsOptions
{
    /// <summary><c>FileSystem.AppDataDirectory</c>; a folder under the temp folder by default.</summary>
    public string AppDataDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "MauiHeadless", "AppData");

    /// <summary><c>AppInfo.Name</c>.</summary>
    public string AppName { get; set; } = "MauiHeadless";

    /// <summary><c>AppInfo.PackageName</c>.</summary>
    public string PackageName { get; set; } = "headless.app";

    /// <summary><c>AppInfo.Version</c>.</summary>
    public Version AppVersion { get; set; } = new(1, 0, 0);

    /// <summary><c>DeviceInfo.Idiom</c>; desktop, as the Windows head the MauiHeadless head answers like.</summary>
    public DeviceIdiom Idiom { get; set; } = DeviceIdiom.Desktop;

    /// <summary>
    /// <c>MediaPicker.IsCaptureSupported</c>; true, as Windows answers, so an app offers its camera sources. A capture
    /// itself answers as cancelled.
    /// </summary>
    public bool CaptureSupported { get; set; } = true;
}

/// <summary>
/// Gives MAUI Essentials an implementation on plain <c>net10.0</c>, where every one throws: the file
/// system, device and app info, preferences and secure storage in memory, an online connection, a
/// phone-sized display, a silent screen reader, and media and file pickers that answer as cancelled.
/// </summary>
/// <remarks>
/// Essentials are process-wide statics with internal setters, so this is for an app whose code calls
/// them directly (<c>Preferences.Default</c>, <c>FileSystem.AppDataDirectory</c>). Code that takes the
/// interfaces from its services gets the context's <see cref="MauiTestContext.Preferences"/> instead.
/// Preferences and secure storage are made once per process, so their values survive a reinstall.
/// </remarks>
public static class HeadlessEssentials
{
    private static readonly HeadlessPreferences SharedPreferences = new();
    private static readonly HeadlessSecureStorage SharedSecureStorage = new();

    /// <summary>Installs the implementations; a later call replaces the app and device answers.</summary>
    public static void Install(HeadlessEssentialsOptions? options = null)
    {
        options ??= new HeadlessEssentialsOptions();
        Directory.CreateDirectory(options.AppDataDirectory);

        MauiInternals.SetEssential(typeof(FileSystem), "SetCurrent", new HeadlessFileSystem(options.AppDataDirectory));
        MauiInternals.SetEssential(typeof(DeviceInfo), "SetCurrent", new HeadlessDeviceInfo(options.Idiom));
        MauiInternals.SetEssential(typeof(AppInfo), "SetCurrent", new HeadlessAppInfo(options));
        MauiInternals.SetEssential(typeof(Preferences), "SetDefault", SharedPreferences);
        MauiInternals.SetEssential(typeof(SecureStorage), "SetDefault", SharedSecureStorage);
        MauiInternals.SetEssential(typeof(Connectivity), "SetCurrent", new HeadlessConnectivity());
        MauiInternals.SetEssential(typeof(DeviceDisplay), "SetCurrent", new HeadlessDeviceDisplay());
        MauiInternals.SetEssential(typeof(Accessibility.SemanticScreenReader), "SetDefault", new SilentScreenReader());
        MauiInternals.SetEssential(typeof(Media.MediaPicker), "SetDefault", CancellingMediaPicker.Create(options.CaptureSupported));
        MauiInternals.SetEssential(typeof(FilePicker), "SetDefault", Unavailable.Create<IFilePicker>());
    }

    /// <summary>
    /// A stub that answers every member with its type's default: false, null, or a completed task of
    /// null, as a picker the user cancelled. It stays valid when MAUI adds members to the interface.
    /// </summary>
    public class Unavailable : DispatchProxy
    {
        /// <summary>A <typeparamref name="T"/> that answers every member with its default.</summary>
        public static T Create<T>() where T : class => Create<T, Unavailable>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var type = targetMethod?.ReturnType;
            if (type is null || type == typeof(void))
                return null;
            if (type == typeof(Task))
                return Task.CompletedTask;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var result = type.GenericTypeArguments[0];
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(result)
                    .Invoke(null, [result.IsValueType ? Activator.CreateInstance(result) : null]);
            }

            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }

    /// <summary>A media picker every pick and capture of which is cancelled, and that says whether it can capture.</summary>
    internal class CancellingMediaPicker : Unavailable
    {
        private bool _captureSupported;

        /// <summary>A picker answering <c>IsCaptureSupported</c> with <paramref name="captureSupported"/>.</summary>
        public static Media.IMediaPicker Create(bool captureSupported)
        {
            var picker = Create<Media.IMediaPicker, CancellingMediaPicker>();
            ((CancellingMediaPicker)(object)picker)._captureSupported = captureSupported;
            return picker;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod?.Name == "get_" + nameof(Media.IMediaPicker.IsCaptureSupported)
                ? _captureSupported
                : base.Invoke(targetMethod, args);
    }

    private sealed class HeadlessFileSystem(string root) : IFileSystem
    {
        public string CacheDirectory { get; } = Directory.CreateDirectory(Path.Combine(root, "cache")).FullName;
        public string AppDataDirectory { get; } = root;

        public Task<Stream> OpenAppPackageFileAsync(string filename) =>
            Task.FromResult<Stream>(File.OpenRead(Path.Combine(AppContext.BaseDirectory, filename)));

        public Task<bool> AppPackageFileExistsAsync(string filename) =>
            Task.FromResult(File.Exists(Path.Combine(AppContext.BaseDirectory, filename)));
    }

    private sealed class HeadlessDeviceInfo(DeviceIdiom idiom) : IDeviceInfo
    {
        public string Model => "MauiHeadless";
        public string Manufacturer => "MauiHeadless";
        public string Name => "MauiHeadless";
        public string VersionString => "10.0";
        public Version Version => new(10, 0);
        public DevicePlatform Platform => DevicePlatform.WinUI;
        public DeviceIdiom Idiom => idiom;
        public DeviceType DeviceType => DeviceType.Virtual;
    }

    private sealed class HeadlessAppInfo(HeadlessEssentialsOptions options) : IAppInfo
    {
        public string PackageName { get; } = options.PackageName;
        public string Name { get; } = options.AppName;
        public string VersionString { get; } = options.AppVersion.ToString();
        public Version Version { get; } = options.AppVersion;
        public string BuildString => "1";
        public AppTheme RequestedTheme => AppTheme.Light;
        public AppPackagingModel PackagingModel => AppPackagingModel.Unpackaged;
        public LayoutDirection RequestedLayoutDirection => LayoutDirection.LeftToRight;
        public void ShowSettingsUI() { }
    }

    private sealed class HeadlessConnectivity : IConnectivity
    {
        public IEnumerable<ConnectionProfile> ConnectionProfiles => [ConnectionProfile.Ethernet];
        public NetworkAccess NetworkAccess => NetworkAccess.Internet;
        public event EventHandler<ConnectivityChangedEventArgs>? ConnectivityChanged { add { } remove { } }
    }

    private sealed class HeadlessDeviceDisplay : IDeviceDisplay
    {
        public bool KeepScreenOn { get; set; }
        public DisplayInfo MainDisplayInfo => new(430, 860, 1, DisplayOrientation.Portrait, DisplayRotation.Rotation0);
        public event EventHandler<DisplayInfoChangedEventArgs>? MainDisplayInfoChanged { add { } remove { } }
    }

    private sealed class SilentScreenReader : Accessibility.ISemanticScreenReader
    {
        public void Announce(string text) { }
    }
}
