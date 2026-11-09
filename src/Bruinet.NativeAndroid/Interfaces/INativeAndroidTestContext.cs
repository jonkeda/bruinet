using Bruinet.Core.Calls;
using Bruinet.Core.Interfaces;

namespace Bruinet.NativeAndroid.Interfaces;

// Core's ITestContext (its name and near-miss settings among them) and NativeAndroid's own
// scope, which derives from Core's IScope. See AD-010.

/// <summary>
/// One app under test on one device: the driver, the timeouts and log every call uses, and the
/// screen as the outermost scope.
/// </summary>
public interface INativeAndroidTestContext : ITestContext, INativeAndroidElementScope
{
    /// <summary>The Appium session.</summary>
    INativeAndroidDriver Driver { get; }
}
