using Bruinet.Html;
using Bruinet.Html.UITests.PageObjects;
using Bruinet.Html.UITests.TestBase;

namespace Bruinet.Html.UITests.Tests.Controls;

public sealed class CheckBoxControlTests : BlazorSampleTestBase
{
    [Fact]
    public void CheckBox_Check_SetsCheckedTrue()
    {
        NavigateToPage("/form-controls");
        var page = new FormControlsPage(Context);

        page.TermsCheckBox.Uncheck();
        page.TermsCheckBox.Check();

        Assert.True(page.TermsCheckBox.IsChecked());
    }

    [Fact]
    public void CheckBox_Uncheck_SetsCheckedFalse()
    {
        NavigateToPage("/form-controls");
        var page = new FormControlsPage(Context);

        page.NewsletterCheckBox.Check();
        page.NewsletterCheckBox.Uncheck();

        Assert.False(page.NewsletterCheckBox.IsChecked());
    }

    [Fact]
    public void CheckBox_Toggle_FlipsCheckedState()
    {
        NavigateToPage("/form-controls");
        var page = new FormControlsPage(Context);

        var initial = page.TermsCheckBox.IsChecked();
        page.TermsCheckBox.Toggle();

        Assert.NotEqual(initial, page.TermsCheckBox.IsChecked());
    }
}
