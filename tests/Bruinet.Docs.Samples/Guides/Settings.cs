// docs/guides/settings.md quotes these regions: change them here, then run tools/Scripts/update-doc-samples.ps1.
using Bruinet.Bdd;
using Bruinet.Core.Settings;

namespace Bruinet.Docs.Samples.Guides;

public static class SettingsAccess
{
    public static bool UseLiveApi(BddExecutionContext context)
    {
        #region docs:access
        var settings = context.GetSettings();
        var useLiveApi = settings.GetRequired<bool>("capabilities.liveApi");
        #endregion
        return useLiveApi;
    }
}

#region docs:section
[TestSettingsSection("capabilities")]
public sealed class CapabilitySettings
{
    public bool Hardware { get; set; }
    public bool LiveApi { get; set; }
}
#endregion
