using Foundation;
using ObjCRuntime;
using UIKit;

namespace Bruinet.Recorder;

/// <summary>The Mac app's delegate.</summary>
[Register("AppDelegate")]
public sealed class AppDelegate : MauiUIApplicationDelegate
{
    /// <inheritdoc/>
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

/// <summary>The Mac app's entry.</summary>
public static class Program
{
    private static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}
