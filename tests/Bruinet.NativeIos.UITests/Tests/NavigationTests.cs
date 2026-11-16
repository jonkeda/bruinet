namespace Bruinet.NativeIos.UITests.Tests;

/// <summary>A segmented control as tabs, a tab bar, a navigation bar's buttons and its back button.</summary>
[Collection(GalleryCollection.Name)]
public sealed class NavigationTests(GalleryFixture gallery)
{
    private NavigationPage Open()
    {
        gallery.Open("navigation");
        var page = new NavigationPage(gallery.Context);
        page.AssertIdle();
        return page;
    }

    [Fact]
    public void Tabs_SelectByTitle()
    {
        var page = Open();

        page.Tabs.AssertSelectedText("One");
        page.Tabs.SelectByText("Three");

        page.Status.AssertText("Tab Three, Home");
        Assert.Equal(["One", "Two", "Three"], page.Tabs.GetItemTexts());
    }

    [Fact]
    public void TheTabBar_SelectsASection()
    {
        var page = Open();

        page.Sections.SelectByText("Profile");

        page.Status.AssertText("Tab One, Profile");
        page.Sections.AssertSelectedText("Profile");
    }

    [Fact]
    public void ANavigationBarButton_IsTapped()
    {
        var page = Open();

        page.Bar.Action("Refresh").Click();

        page.Status.AssertText("Refresh chosen");
    }

    [Fact]
    public void TheBackButton_LeavesTheScreen()
    {
        var page = Open();

        page.Bar.Title.AssertText("Navigation");
        page.Bar.BackButton.Click();

        page.WaitLoaded(false);
    }
}
