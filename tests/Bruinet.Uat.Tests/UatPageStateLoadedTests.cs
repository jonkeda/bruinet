using Bruinet.Uat;
using Xunit;

namespace Bruinet.Uat.Tests;

// step_state with no page current reads the page that shows (the benchmark's T4b runs: set_state names no page).
public sealed class UatPageStateLoadedTests
{
    [Fact]
    public void ThePagesThatSayTheyAreLoaded_AreFound_MauiAndWebAlike()
    {
        var loaded = UatPageState.Loaded(
        [
            (Page("Todos"), () => new MauiPage(loaded: true)),
            (Page("Detail"), () => new MauiPage(loaded: false)),
            (Page("Settings"), () => new WebPage(loaded: true)),
            (Page("About"), () => null),
        ]);

        Assert.Equal(["Todos", "Settings"], loaded.Select(page => page.Name));
    }

    [Fact]
    public void APageWhoseCheckOrMakingThrows_IsNotLoaded()
    {
        Assert.Empty(UatPageState.Loaded(
        [
            (Page("Todos"), () => new ThrowingPage()),
            (Page("Frame"), () => throw new InvalidOperationException("Unable to resolve service for type 'System.String'")),
        ]));
    }

    private static UatRuntimePage Page(string name) => new(name, typeof(object), null, [], null);

    public sealed class MauiPage(bool loaded)
    {
        public bool IsLoaded(int? timeoutMs = null) => loaded;
    }

    public sealed class WebPage(bool loaded)
    {
        public bool IsLoaded() => loaded;
    }

    public sealed class ThrowingPage
    {
        public bool IsLoaded(int? timeoutMs = null) => throw new InvalidOperationException("gone");
    }
}
