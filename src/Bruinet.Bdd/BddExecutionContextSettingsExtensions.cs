using Bruinet.Core.Settings;

namespace Bruinet.Bdd;

[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public static class BddExecutionContextSettingsExtensions
{
    private const string SettingsItemKey = "Bruinet.Uat.Settings";
    private const string TypedSettingsItemKeyPrefix = "Bruinet.Uat.Settings.Typed.";

    public static void SetSettings(this BddExecutionContext context, TestSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        context.Items[SettingsItemKey] = settings;
    }

    public static TestSettings GetSettings(this BddExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Items.TryGetValue(SettingsItemKey, out var value) &&
               value is TestSettings settings
            ? settings
            : TestSettings.Empty;
    }

    public static object GetSettings(this BddExecutionContext context, Type settingsType)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settingsType);

        if (settingsType == typeof(TestSettings))
        {
            return context.GetSettings();
        }

        var cacheKey = TypedSettingsItemKeyPrefix + settingsType.AssemblyQualifiedName;
        if (context.Items.TryGetValue(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var settings = context.GetSettings().Bind(settingsType);
        context.Items[cacheKey] = settings;
        return settings;
    }
}
