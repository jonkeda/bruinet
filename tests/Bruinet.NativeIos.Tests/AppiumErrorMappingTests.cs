using OpenQA.Selenium;

namespace Bruinet.NativeIos.Tests;

/// <summary>How Appium's errors surface from the adapter (plan step 4): each as its own exception, mapped once.</summary>
public class AppiumErrorMappingTests
{
    [Fact]
    public void ARecreatedView_IsStale()
        => Assert.Throws<StaleElementException>(() => AppiumNativeIosElement.Live<int>(() => throw new StaleElementReferenceException("stale element reference")));

    [Fact]
    public void AnEndedSession_IsAppUnavailable()
        => Assert.Throws<AppUnavailableException>(() => AppiumNativeIosElement.Live<int>(() => throw new WebDriverException("invalid session id")));

    [Fact]
    public void AServerThatDoesNotAnswer_IsAppUnavailable()
        => Assert.Throws<AppUnavailableException>(() => AppiumNativeIosElement.Live<int>(() =>
            throw new WebDriverException("The HTTP request to the remote WebDriver server for URL http://127.0.0.1:4723/session/1/elements timed out after 60 seconds.")));

    [Fact]
    public void ACommandTheServerLacks_IsRouteUnavailable()
        => Assert.Throws<RouteUnavailableException>(() => AppiumNativeIosElement.Live<int>(() => throw new WebDriverException("Unknown mobile command \"replaceElementValue\"")));

    [Fact]
    public void AnyOtherError_PassesThroughUnchanged()
    {
        var error = new WebDriverException("An unknown server-side error occurred");

        Assert.Same(error, Assert.Throws<WebDriverException>(() => AppiumNativeIosElement.Live<int>(() => throw error)));
    }
}
