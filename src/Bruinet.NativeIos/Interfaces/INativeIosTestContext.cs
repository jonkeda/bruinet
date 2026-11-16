using Bruinet.Core.Calls;
using Bruinet.Core.Interfaces;

namespace Bruinet.NativeIos.Interfaces;

// Core's ITestContext (its name and near-miss settings among them) and NativeIos's own
// scope, which derives from Core's IScope. See AD-010.

/// <summary>
/// One app under test on one device: the driver, the timeouts and log every call uses, and the
/// screen as the outermost scope.
/// </summary>
public interface INativeIosTestContext : ITestContext, INativeIosElementScope
{
    /// <summary>The Appium session.</summary>
    INativeIosDriver Driver { get; }
}
