namespace Bruinet.WinForms.Interfaces;

// Core's ITestContext (its name and near-miss settings among them) and WinForms's own scope, which
// derives from Core's IScope. See AD-010.

/// <summary>
/// One WinForms app under test: the driver, the timeouts and log every call uses, and the app's window
/// as the outermost scope.
/// </summary>
public interface IWinFormsTestContext : ITestContext, IWinFormsElementScope
{
    /// <summary>The app's UI Automation driver.</summary>
    IWinFormsDriver Driver { get; }
}
