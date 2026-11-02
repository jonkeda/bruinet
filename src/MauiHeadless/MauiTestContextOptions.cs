using Microsoft.Maui.Controls;
using MauiHeadless.Inspection;

namespace MauiHeadless;

/// <summary>How a <see cref="MauiTestContext"/> is set up.</summary>
public sealed class MauiTestContextOptions
{
    /// <summary>
    /// Makes the application the rendered windows belong to: the app's own <c>App</c>, for its resources
    /// and styles. Null gives an empty <see cref="Application"/>. The factory runs with
    /// <see cref="Application.Current"/> set to the new application, under MauiHeadless's application lock.
    /// </summary>
    public Func<IServiceProvider, Application>? Application { get; set; }

    /// <summary>
    /// Whether <see cref="Application.Current"/> is the context's application while MauiHeadless runs the
    /// context's work, so app code that reads it (<c>Shell.Current</c>, <c>Application.Current.Resources</c>)
    /// works. On by default. It is process-wide, so contexts in parallel take turns running work;
    /// turn it off for an app that never reads it, and contexts run fully in parallel.
    /// </summary>
    public bool SetApplicationCurrent { get; set; } = true;

    /// <summary>The adapters asked before the default one, for controls MAUI's own interfaces do not describe.</summary>
    public IList<IElementAdapter> Adapters { get; } = [];

    /// <summary>
    /// Types styled to lose their platform behaviors, beside <see cref="Hosting.HeadlessBehaviors.DefaultHosts"/>:
    /// an app's control that adds one (a toolkit TouchBehavior) and has an implicit style of its own.
    /// </summary>
    public IList<Type> BehaviorHosts { get; } = [];

    /// <summary>
    /// Page types a platform draws as a popup over the window (a Mopups <c>PopupPage</c>, an app's own popup
    /// page): pushed as a modal, one is the window's active dialog, as a WinUI popup is on Windows. A
    /// subclass counts. An ordinary modal page is not a dialog.
    /// </summary>
    public IList<Type> DialogPages { get; } = [];

    /// <summary>
    /// Whether the virtual clock keeps pace with the wall clock (<see cref="Hosting.TestDispatcher.FollowsWallClock"/>):
    /// delayed work and timers then fire as real time passes, as on a device. Off by default: a test moves
    /// the clock with <c>AdvanceTime</c>. Bruinet's MauiHeadless head turns it on.
    /// </summary>
    public bool TimeFollowsWallClock { get; set; }

    /// <summary>
    /// How long <c>WaitForAssertion</c> and <c>WaitForState</c> wait by default on the wall clock, for work
    /// that real asynchrony has yet to post back. Virtual time never needs it.
    /// </summary>
    public TimeSpan WaitTimeout { get; set; } = TimeSpan.FromSeconds(2);
}
