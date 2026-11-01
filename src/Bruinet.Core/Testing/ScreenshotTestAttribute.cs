using System.Reflection;
using Bruinet.Core.Interfaces;
using Xunit.Sdk;

namespace Bruinet.Core.Testing;

/// <summary>
/// xUnit attribute that captures screenshots on test failure.
/// Apply to test class or individual test methods.
/// </summary>
/// <remarks>
/// Usage:
/// 1. Call ScreenshotTestAttribute.SetService(service) in your test fixture
/// 2. Apply [ScreenshotTest] to test class or method
/// 3. Call ScreenshotTestAttribute.CaptureIfFailed(exception) in cleanup if using custom exception handling
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class ScreenshotTestAttribute : BeforeAfterTestAttribute
{
    private static readonly AsyncLocal<IScreenshotService?> _screenshotService = new();
    private static readonly AsyncLocal<TestAppSet?> _apps = new();
    private static readonly AsyncLocal<string?> _currentTestClass = new();
    private static readonly AsyncLocal<string?> _currentTestMethod = new();
    private static readonly AsyncLocal<bool> _testFailed = new();

    /// <summary>
    /// Sets the screenshot service to use for capturing.
    /// Call this in your test fixture initialization.
    /// </summary>
    public static void SetService(IScreenshotService? service)
    {
        _screenshotService.Value = service;
    }

    /// <summary>
    /// Registers the apps of a test that uses several: a failure then captures every started app,
    /// not only the one the screenshot service belongs to.
    /// </summary>
    /// <param name="apps">The fixture's apps, or null to go back to the single service.</param>
    public static void SetApps(TestAppSet? apps)
    {
        _apps.Value = apps;
    }

    /// <summary>
    /// Gets the currently registered screenshot service.
    /// </summary>
    public static IScreenshotService? CurrentService => _screenshotService.Value;

    /// <summary>
    /// Called before each test method runs.
    /// </summary>
    public override void Before(MethodInfo methodUnderTest)
    {
        _currentTestClass.Value = methodUnderTest.DeclaringType?.Name ?? "UnknownClass";
        _currentTestMethod.Value = methodUnderTest.Name;
        _testFailed.Value = false;
    }

    /// <summary>
    /// Called after each test method completes.
    /// </summary>
    public override void After(MethodInfo methodUnderTest)
    {
        _currentTestClass.Value = null;
        _currentTestMethod.Value = null;
        _testFailed.Value = false;
    }

    /// <summary>
    /// Captures a screenshot if the test failed.
    /// Call this from exception handling code or test cleanup.
    /// </summary>
    /// <param name="exception">The exception that caused the failure, or null if test passed.</param>
    /// <returns>Path to the saved screenshot, or empty string if not captured.</returns>
    public static string CaptureIfFailed(Exception? exception)
    {
        if (exception == null || (_screenshotService.Value == null && _apps.Value == null) || _testFailed.Value)
            return string.Empty;

        _testFailed.Value = true; // Prevent duplicate captures

        if (_apps.Value is { Started.Count: > 0 } apps)
        {
            return string.Join(
                Environment.NewLine,
                apps.CaptureAll(_currentTestClass.Value ?? "Unknown", _currentTestMethod.Value ?? "Unknown", "failure")
                    .Select(capture => capture.Path ?? $"{capture.App}: {capture.Error}"));
        }

        if (_screenshotService.Value is null)
            return string.Empty;

        return _screenshotService.Value.CaptureOnFailure(
            _currentTestClass.Value ?? "Unknown",
            _currentTestMethod.Value ?? "Unknown",
            exception);
    }

    /// <summary>
    /// Manually capture a screenshot with optional description.
    /// </summary>
    /// <param name="description">Optional description for the screenshot.</param>
    /// <returns>Path to the saved screenshot, or empty string if service not configured.</returns>
    public static string CaptureManual(string? description = null)
    {
        if (_screenshotService.Value == null)
            return string.Empty;

        return _screenshotService.Value.Capture(
            _currentTestClass.Value ?? "Manual",
            _currentTestMethod.Value ?? "Capture",
            description ?? "manual");
    }
}
