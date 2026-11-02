using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Collection;

/// <summary>
/// A row opened by a tap on the row itself: a Grid in the item template with a TapGestureRecognizer whose command
/// is bound through x:Reference to the page's view model, as Exact.Construction's journal and selector rows are.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Pattern", "CollectionView")]
public class RowTapTests
{
    private readonly MauiFixture _fixture;

    public RowTapTests(MauiFixture fixture)
    {
        _fixture = fixture;
        _fixture.NavigateToGridCollectionDemo();
    }

    private GridCollectionDemoPage Page => _fixture.GridCollectionDemoPage;

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Click")]
    public Task ARowWhoseRecognizerIsBoundThroughXReference_RunsThePagesCommand()
    {
        Page.Shortcuts.Row("Crane").Click();

        Page.OpenedProduct.AssertText("Opened Crane");
        return Task.CompletedTask;
    }
}
