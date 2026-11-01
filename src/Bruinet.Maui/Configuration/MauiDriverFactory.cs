using System.Diagnostics;
using System.Reflection;
using Bruinet.Maui.Enums;
using Bruinet.Maui.Interfaces;

namespace Bruinet.Maui.Configuration;

/// <summary>
/// Factory for creating platform-appropriate MAUI drivers.
/// - Windows: Always uses FlaUI (native UI Automation)
/// - Android/iOS: Uses Appium
/// 
/// Driver implementations are loaded dynamically to avoid circular dependencies.
/// </summary>
public static class MauiDriverFactory
{
    /// <summary>
    /// The Appium client assembly name.
    /// </summary>
    private const string AppiumAssemblyName = "Appium.Net";

    private const string PlacementVariable = "BRUINET_AUT_PLACE";

    private static Type? _appiumDriverType;
    private static Type? _flaUIDriverType;
    
    /// <summary>
    /// Creates a driver based on the specified options.
    /// </summary>
    /// <param name="options">Driver configuration options.</param>
    /// <returns>An IMauiDriver instance appropriate for the platform.</returns>
    /// <exception cref="ArgumentNullException">When options is null.</exception>
    /// <exception cref="ArgumentException">When required options are missing.</exception>
    /// <exception cref="PlatformNotSupportedException">When platform is not supported.</exception>
    public static IMauiDriver Create(MauiDriverOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        
        return options.Platform switch
        {
            MauiPlatform.Windows => CreateFlaUIDriver(options),
            MauiPlatform.Android => CreateAppiumDriver(options),
            MauiPlatform.iOS => CreateAppiumDriver(options),
            MauiPlatform.Headless => throw new ArgumentException(
                "MauiHeadless has no app to launch: the fixture renders it headless (CreateHeadlessOptions, MauiHeadlessHead.Options)."),
            _ => throw new ArgumentException($"Unsupported platform: {options.Platform}")
        };
    }
    
    private static IMauiDriver CreateFlaUIDriver(MauiDriverOptions options)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "FlaUI driver is only available on Windows. " +
                "Use APPIUM_PLATFORM=android or APPIUM_PLATFORM=ios for other platforms.");
        }
        
        var driverType = GetFlaUIDriverType();
        
        if (options.WindowHandle.HasValue)
        {
            var ctor = driverType.GetConstructor([typeof(IntPtr)])
                ?? throw new InvalidOperationException("FlaUIMauiDriver(IntPtr) constructor not found");
            return (IMauiDriver)ctor.Invoke([options.WindowHandle.Value]);
        }

        if (!string.IsNullOrEmpty(options.ProcessName))
        {
            var process = Process.GetProcessesByName(options.ProcessName).FirstOrDefault()
                          ?? throw new InvalidOperationException(
                              $"Process not found: {options.ProcessName}. " +
                              "Ensure the application is running or use AppPath to launch it.");
            
            var ctor = driverType.GetConstructor([typeof(Process)])
                       ?? throw new InvalidOperationException("FlaUIMauiDriver(Process) constructor not found");
            return (IMauiDriver)ctor.Invoke([process]);
        }

        if (!string.IsNullOrEmpty(options.AppPath))
        {
            var ctor = driverType.GetConstructor([typeof(string), typeof(string), typeof(IReadOnlyDictionary<string, string>)])
                       ?? throw new InvalidOperationException("FlaUIMauiDriver(string, string, IReadOnlyDictionary) constructor not found");
            // The placement travels with the launch, as the app's own settings do: the driver reads
            // it from the launch environment before the process-wide BRUINET_AUT_PLACE.
            var environment = new Dictionary<string, string>(options.LaunchSettings, StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(options.Placement))
            {
                environment[PlacementVariable] = options.Placement;
            }

            return (IMauiDriver)ctor.Invoke([options.AppPath, null, environment]);
        }

        throw new ArgumentException(
            "FlaUI driver requires AppPath, ProcessName, or WindowHandle. " +
            "Set APPIUM_APP_PATH environment variable or configure options.",
            nameof(options));
    }
    
    private static IMauiDriver CreateAppiumDriver(MauiDriverOptions options)
    {
        ValidateAppiumOptions(options);

        // An unchanged build is not installed again (.my/android-performance, P1).
        var build = AndroidInstalledBuild.Check(options);

        var appiumOptionsType = LoadAppiumOptionsType();
        var appiumDriverType = GetAppiumDriverType();
        
        var appiumOptions = BuildAppiumOptionsReflection(options, appiumOptionsType);
        
        var rawDriver = CreatePlatformDriverReflection(options.AppiumServerUri, appiumOptions, options.Platform);
        
        var ctor = appiumDriverType.GetConstructor([rawDriver.GetType(), typeof(MauiPlatform)])
            ?? appiumDriverType.GetConstructors().First();

        build?.RememberInstalled();
        return (IMauiDriver)ctor.Invoke([rawDriver, options.Platform]);
    }
    
    private static void ValidateAppiumOptions(MauiDriverOptions options)
    {
        if (string.IsNullOrEmpty(options.AppPath))
        {
            throw new ArgumentException(
                "AppPath is required for Appium driver. " +
                "Set APPIUM_APP_PATH environment variable or options.AppPath.",
                nameof(options));
        }
    }
    
    private static Type GetFlaUIDriverType()
    {
        if (_flaUIDriverType != null) return _flaUIDriverType;
        
        try
        {
            var assembly = Assembly.Load("Bruinet.Maui.FlaUI");
            _flaUIDriverType = assembly.GetType("Bruinet.Maui.FlaUI.FlaUIMauiDriver")
                ?? throw new InvalidOperationException("FlaUIMauiDriver type not found in Bruinet.Maui.FlaUI assembly");
            return _flaUIDriverType;
        }
        catch (FileNotFoundException exception)
        {
            throw new InvalidOperationException(
                "Bruinet.Maui.FlaUI assembly not found. " +
                "Ensure the Bruinet.Maui.FlaUI package is referenced in your project.", exception);
        }
    }
    
    private static Type GetAppiumDriverType()
    {
        if (_appiumDriverType != null) return _appiumDriverType;
        
        try
        {
            var assembly = Assembly.Load("Bruinet.Maui.Appium");
            _appiumDriverType = assembly.GetType("Bruinet.Maui.Appium.AppiumMauiDriver")
                ?? throw new InvalidOperationException("AppiumMauiDriver type not found in Bruinet.Maui.Appium assembly");
            return _appiumDriverType;
        }
        catch (FileNotFoundException exception)
        {
            throw new InvalidOperationException(
                "Bruinet.Maui.Appium assembly not found. " +
                "Ensure the Bruinet.Maui.Appium package is referenced in your project.", exception);
        }
    }
    
    private static Type LoadAppiumOptionsType()
    {
        try
        {
            var assembly = Assembly.Load(AppiumAssemblyName);
            return assembly.GetType("OpenQA.Selenium.Appium.AppiumOptions")
                ?? throw new InvalidOperationException("AppiumOptions type not found");
        }
        catch (FileNotFoundException exception)
        {
            throw new InvalidOperationException(
                $"{AppiumAssemblyName} assembly not found. " +
                "Ensure the Appium.WebDriver package is referenced by the test project.", exception);
        }
    }
    
    /// <summary>
    /// The session settings every MAUI suite on <paramref name="platform"/> starts with, in one
    /// place; a suite's own <see cref="MauiDriverOptions.AdditionalCapabilities"/> override them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Android.</b> UiAutomator2 waits for the app to be idle before every command, up to 10 s,
    /// and a page with anything moving (a playing MediaElement, an ActivityIndicator) never is:
    /// each lookup there took 11 s. 100 ms bounds it (0 crashed the UiAutomator2 server in one
    /// night's runs, 100 was clean); Bruinet waits for state itself. Window animations off, so a
    /// fresh emulator without the runbook's <c>adb shell settings</c> does not tap during a
    /// transition.
    /// </para>
    /// <para>
    /// <b>iOS</b>, not yet run on a simulator (<c>.my/ios/README.md</c>): XCUITest waits for the
    /// app to be quiescent before each command by default, the same trap on an animating page, so
    /// quiescence is off and its idle wait (in seconds there) is bounded to one.
    /// </para>
    /// </remarks>
    internal static IReadOnlyDictionary<string, object> SessionDefaults(MauiPlatform platform) => platform switch
    {
        MauiPlatform.Android => new Dictionary<string, object>
        {
            ["settings[waitForIdleTimeout]"] = 100,
            ["disableWindowAnimation"] = true,
        },
        MauiPlatform.iOS => new Dictionary<string, object>
        {
            ["waitForQuiescence"] = false,
            ["settings[waitForIdleTimeout]"] = 1,
        },
        _ => new Dictionary<string, object>(),
    };

    /// <summary>
    /// The simulator an iOS session asks for when the suite names none: the one
    /// <c>docs/run/maui-ios.md</c> runs on. The fixture and the factory both read it.
    /// </summary>
    internal static (string DeviceName, string PlatformVersion) IosDevice(string? deviceName, string? platformVersion)
        => (deviceName ?? "iPhone 17 Pro Max", platformVersion ?? "26.5");

    /// <summary>The capabilities a session starts with: the platform's defaults, then the suite's own over them.</summary>
    internal static IReadOnlyDictionary<string, object> SessionCapabilities(MauiDriverOptions options)
    {
        var capabilities = new Dictionary<string, object>(SessionDefaults(options.Platform));
        foreach (var (name, value) in options.AdditionalCapabilities)
        {
            capabilities[name] = value;
        }

        return capabilities;
    }

    private static object BuildAppiumOptionsReflection(MauiDriverOptions options, Type appiumOptionsType)
    {
        var appiumOptions = Activator.CreateInstance(appiumOptionsType)!;
        
        var platformNameProp = appiumOptionsType.GetProperty("PlatformName");
        var automationNameProp = appiumOptionsType.GetProperty("AutomationName");
        var deviceNameProp = appiumOptionsType.GetProperty("DeviceName");
        var platformVersionProp = appiumOptionsType.GetProperty("PlatformVersion");
        var appProp = appiumOptionsType.GetProperty("App");
        
        switch (options.Platform)
        {
            case MauiPlatform.Android:
                platformNameProp?.SetValue(appiumOptions, "Android");
                automationNameProp?.SetValue(appiumOptions, "UiAutomator2");
                deviceNameProp?.SetValue(appiumOptions, options.DeviceName ?? "emulator-5554");
                appProp?.SetValue(appiumOptions, options.AppPath);
                break;
                
            case MauiPlatform.iOS:
                platformNameProp?.SetValue(appiumOptions, "iOS");
                automationNameProp?.SetValue(appiumOptions, "XCUITest");
                var (deviceName, platformVersion) = IosDevice(options.DeviceName, options.PlatformVersion);
                deviceNameProp?.SetValue(appiumOptions, deviceName);
                platformVersionProp?.SetValue(appiumOptions, platformVersion);
                appProp?.SetValue(appiumOptions, options.AppPath);
                break;
        }
        
        var addCapMethod = appiumOptionsType.GetMethod("AddAdditionalAppiumOption");
        if (addCapMethod != null)
        {
            foreach (var cap in SessionCapabilities(options))
            {
                addCapMethod.Invoke(appiumOptions, [cap.Key, cap.Value]);
            }

            if (options.Platform == MauiPlatform.iOS)
            {
                if (LaunchProcessArguments(options) is { } processArguments)
                {
                    addCapMethod.Invoke(appiumOptions, ["processArguments", processArguments]);
                }
            }
            else if (LaunchIntentArguments(options) is { } intentArguments)
            {
                addCapMethod.Invoke(appiumOptions, ["optionalIntentArguments", intentArguments]);
            }
        }
        
        return appiumOptions;
    }
    
    /// <summary>
    /// <see cref="MauiDriverOptions.LaunchSettings"/> as launch-intent string extras, or null when
    /// there are none.
    /// </summary>
    /// <exception cref="ArgumentException">A name or value cannot travel on the command line.</exception>
    /// <exception cref="NotSupportedException">Launch settings on a platform that has no route for them yet.</exception>
    internal static string? LaunchIntentArguments(MauiDriverOptions options)
    {
        if (options.LaunchSettings.Count == 0)
        {
            return null;
        }

        if (options.Platform != MauiPlatform.Android)
        {
            throw new NotSupportedException(
                $"Launch settings travel as intent extras on Android only, not on {options.Platform}.");
        }

        var parts = new List<string>();
        foreach (var (name, value) in options.LaunchSettings)
        {
            if (!IsCommandLineSafe(name) || !IsCommandLineSafe(value))
            {
                throw new ArgumentException(
                    $"Launch setting '{name}' = '{value}' cannot be passed as an Android intent extra: "
                    + "names and values may not be empty or contain whitespace or quotes.",
                    nameof(options));
            }

            parts.Add($"--es {name} {value}");
        }

        return string.Join(' ', parts);

        static bool IsCommandLineSafe(string text)
            => text.Length > 0 && !text.Any(c => char.IsWhiteSpace(c) || c is '"' or '\'' or '`' or '\\');
    }

    /// <summary>
    /// <see cref="MauiDriverOptions.LaunchSettings"/> as XCUITest <c>processArguments</c>
    /// (<c>{ env: { name: value } }</c>), or null when there are none.
    /// </summary>
    internal static Dictionary<string, object>? LaunchProcessArguments(MauiDriverOptions options)
    {
        if (options.LaunchSettings.Count == 0)
        {
            return null;
        }

        return new Dictionary<string, object>
        {
            ["env"] = new Dictionary<string, string>(options.LaunchSettings, StringComparer.Ordinal)
        };
    }

    private static object CreatePlatformDriverReflection(Uri serverUri, object appiumOptions, MauiPlatform platform)
    {
        try
        {
            var assembly = Assembly.Load(AppiumAssemblyName);
            
            Type driverType = platform switch
            {
                MauiPlatform.Android => assembly.GetType("OpenQA.Selenium.Appium.Android.AndroidDriver")!,
                MauiPlatform.iOS => assembly.GetType("OpenQA.Selenium.Appium.iOS.IOSDriver")!,
                _ => throw new ArgumentException($"Appium not supported for platform: {platform}")
            };
            
            var ctor = driverType.GetConstructor([typeof(Uri), appiumOptions.GetType()])
                ?? driverType.GetConstructors().First();
            
            return ctor.Invoke([serverUri, appiumOptions]);
        }
        catch (FileNotFoundException exception)
        {
            throw new InvalidOperationException(
                $"{AppiumAssemblyName} assembly not found. " +
                "Ensure the Appium.WebDriver package is referenced by the test project.", exception);
        }
    }
}
