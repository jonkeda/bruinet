using Android.Content;

namespace Bruinet.Maui.AppSupport;

/// <summary>
/// Reads the launch settings a Bruinet UI test hands an Android app.
/// </summary>
/// <remarks>
/// Bruinet passes <c>MauiDriverOptions.LaunchSettings</c> to an Android app as string extras on the
/// launch intent (<c>--es name value</c>). An app that reads its settings from the environment,
/// as it does on Windows and iOS, copies them there before MAUI starts:
/// <code>
/// protected override void OnCreate(Bundle? savedInstanceState)
/// {
///     BruinetLaunchSettings.ApplyIntentExtras(Intent, "MYAPP_BACKEND_URL", "MYAPP_DATABASE_PATH");
///     base.OnCreate(savedInstanceState);
/// }
/// </code>
/// Before <c>base.OnCreate</c>, because that creates the first window and so starts the app.
/// Only the named extras are copied: an intent can come from another app, and a test build
/// should still not take arbitrary settings from one.
/// </remarks>
public static class BruinetLaunchSettings
{
    /// <summary>
    /// Copies the named string extras of <paramref name="intent"/> into the process environment,
    /// skipping the ones that are missing or empty.
    /// </summary>
    /// <param name="intent">The activity's launch intent.</param>
    /// <param name="names">The extras to copy, by name.</param>
    /// <returns>How many settings were copied.</returns>
    public static int ApplyIntentExtras(Intent? intent, params string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);
        if (intent is null)
            return 0;

        var applied = 0;
        foreach (var name in names)
        {
            if (intent.GetStringExtra(name) is { Length: > 0 } value)
            {
                Environment.SetEnvironmentVariable(name, value);
                applied++;
            }
        }

        return applied;
    }
}
