using Bruinet.NativeAndroid.Appium;

namespace Bruinet.NativeAndroid.Testing;

/// <summary>
/// Base fixture for native Android UI tests: one Appium session for the app under test, opened
/// from the <c>APPIUM_*</c> environment variables and the options a suite adds.
/// </summary>
public abstract class NativeAndroidTestFixtureBase
{
    private NativeAndroidTestContext? _context;

    /// <summary>The app's context; there once <see cref="InitializeAsync"/> has run.</summary>
    public INativeAndroidTestContext Context => _context
        ?? throw new InvalidOperationException("Context not initialized. Ensure InitializeAsync has completed.");

    /// <summary>The session's options: the environment's, which a suite completes with its app.</summary>
    protected virtual NativeAndroidTestContextOptions CreateOptions() => NativeAndroidTestContextOptions.FromEnvironment();

    /// <summary>Opens the session and starts the app.</summary>
    public virtual Task InitializeAsync()
    {
        _context = NativeAndroidTestContext.Create(CreateOptions());
        return Task.CompletedTask;
    }

    /// <summary>Ends the session.</summary>
    public virtual Task DisposeAsync()
    {
        _context?.Dispose();
        _context = null;
        return Task.CompletedTask;
    }
}
