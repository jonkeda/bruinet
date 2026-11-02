using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Text;

/// <summary>
/// What a field does with what is typed, and focus the app gives: an upper-case field shows and keeps
/// upper case, a field with a MaxLength keeps that many, and an app that clears a field and calls
/// <c>Focus()</c> leaves the field focused, on every head.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "Entry")]
public class FieldRulesTests
{
    private readonly TextTestPage _page;

    public FieldRulesTests(MauiFixture fixture)
    {
        fixture.Open(SamplePage.Text);
        _page = new TextTestPage(fixture.Context);
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task UpperCaseField_TypedInLowerCase_ShowsUpperCase()
    {
        _page.PlateEntry.SetText("ab-12-cd");

        _page.PlateEntry.AssertText("AB-12-CD");

        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task MaxLengthField_KeepsItsFirstNineCharacters()
    {
        _page.ReadingEntry.SetText("1234567890");

        _page.ReadingEntry.AssertText("123456789");

        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task ClearButton_EmptiesTheField_AndTheAppsFocusLandsInIt()
    {
        _page.PlateEntry.SetText("ab-12-cd");

        _page.ClearPlateButton.Click();

        _page.PlateEntry.AssertText(string.Empty);
        _page.PlateEntry.AssertFocused();

        return Task.CompletedTask;
    }
}
