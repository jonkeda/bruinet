using Bruinet.Html;
using Bruinet.Html.UITests.PageObjects;
using Bruinet.Html.UITests.TestBase;

namespace Bruinet.Html.UITests.Tests.Controls;

public sealed class SelectControlTests : BlazorSampleTestBase
{
    [Fact]
    public void Select_SelectByValue_UpdatesSelectedValue()
    {
        NavigateToPage("/form-controls");
        var page = new FormControlsPage(Context);

        page.CountrySelect.SelectByValue("de");

        Assert.Equal("de", page.CountrySelect.GetSelectedValue());
    }

    [Fact]
    public void Select_SelectByText_UpdatesSelectedValue()
    {
        NavigateToPage("/form-controls");
        var page = new FormControlsPage(Context);

        page.ColorsSelect.SelectByText("Blue");

        Assert.Equal("blue", page.ColorsSelect.GetSelectedValue());
    }

    [Fact]
    public void Select_GetSelectedValue_ReturnsCurrentValue()
    {
        NavigateToPage("/form-controls");
        var page = new FormControlsPage(Context);

        page.CountrySelect.SelectByValue("jp");
        var selected = page.CountrySelect.GetSelectedValue();

        Assert.Equal("jp", selected);
    }
}
