using Bruinet.Core.Exceptions;
using Bruinet.Html.Playwright;
using Xunit;

namespace Bruinet.Html.UITests.Tests.Scenarios;

/// <summary>
/// What a suite asks of the browser context through the options: the hosts its page may go to, and a
/// saved sign-in to start from. No suite writes its own Playwright set-up for them.
/// </summary>
public sealed class ContextOptionsTests
{
    [Fact]
    public async Task A_NavigationToAHostNotAllowed_IsRefused_AndListed()
    {
        await using var context = await PlaywrightTestContext.CreateAsync(new HtmlTestContextOptions
        {
            Headless = true,
            AllowedHosts = ["allowed.invalid"],
        });

        Assert.Throws<PageLoadException>(() => context.NavigateTo("https://elsewhere.invalid/page"));

        Assert.Equal(["https://elsewhere.invalid/page"], context.RefusedNavigations);
    }

    [Fact]
    public async Task A_SavedSignIn_StartsTheNextContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bruinet-state-{Guid.NewGuid():N}", "state.json");
        try
        {
            await using (var first = await PlaywrightTestContext.CreateAsync(new HtmlTestContextOptions { Headless = true }))
            {
                first.SaveStorageState(path);
            }

            Assert.True(File.Exists(path));
            await using var second = await PlaywrightTestContext.CreateAsync(new HtmlTestContextOptions
            {
                Headless = true,
                StorageStatePath = path,
                Locale = "nl-NL",
                ViewportWidth = 1024,
                ViewportHeight = 700,
            });

            Assert.Empty(second.RefusedNavigations);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
