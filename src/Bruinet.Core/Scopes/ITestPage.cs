namespace Bruinet.Core.Scopes;

/// <summary>
/// A page of the app under test: a root scope with load, busy and title state. The element-free
/// half of every stack's page (AD-010); test composition and UAT find page objects by it. Checks
/// (<c>Is*</c>, <c>Get*</c>) answer about now; waits (<c>Wait*</c>, <c>Assert*</c>) are one call
/// each and take a timeout.
/// </summary>
/// <remarks>Not <c>IPage</c>, which Playwright's page interface is called in every Html suite.</remarks>
public interface ITestPage : IScope
{
    /// <summary>The page's name, for logs and messages; on most stacks also what locates its root.</summary>
    string Name { get; }

    /// <summary>Whether the page is loaded now.</summary>
    bool IsLoaded();

    /// <summary>Waits until the loaded state matches <paramref name="expected"/>. Null skips.</summary>
    bool WaitLoaded(bool? expected = true, int? timeoutMs = null);

    /// <summary>Asserts the loaded state. Null skips.</summary>
    void AssertLoaded(bool? expected = true, string? message = null, int? timeoutMs = null);

    /// <summary>Whether the page reports itself busy now.</summary>
    bool IsBusy();

    /// <summary>Waits until the busy state matches <paramref name="expected"/>. Null skips.</summary>
    bool WaitBusy(bool? expected = true, int? timeoutMs = null);

    /// <summary>Asserts that the page becomes idle.</summary>
    void AssertIdle(string? message = null, int? timeoutMs = null);

    /// <summary>The page's title now.</summary>
    string? GetTitle();

    /// <summary>Waits until the title matches <paramref name="expected"/>. Null skips.</summary>
    bool WaitTitle(string? expected, int? timeoutMs = null);

    /// <summary>Asserts the title. Null skips.</summary>
    void AssertTitle(string? expected, string? message = null, int? timeoutMs = null);

    /// <summary>Saves a screenshot of the app.</summary>
    void TakeScreenshot(string? filename = null);
}
