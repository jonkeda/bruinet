namespace Bruinet.Wpf.Interfaces;

// Core's ITestContext (its name and near-miss settings among them) and WPF's own scope, which
// derives from Core's IScope. See AD-010.

/// <summary>
/// One WPF app under test: the driver, the timeouts and log every call uses, and the app's window
/// as the outermost scope.
/// </summary>
public interface IWpfTestContext : ITestContext, IWpfElementScope
{
    /// <summary>The app's UI Automation driver.</summary>
    IWpfDriver Driver { get; }
}
