using Bruinet.Html;
using Bruinet.Html.UITests.PageObjects;
using Bruinet.Html.UITests.TestBase;

namespace Bruinet.Html.UITests.Tests.Controls;

public sealed class TextInputControlTests : BlazorSampleTestBase
{
    [Fact]
    public void TextInput_SetTextAndGetValue_RoundTripsValue()
    {
        NavigateToPage("/login");
        var page = new LoginPage(Context);

        page.EmailInput.SetText("hello@example.com");

        Assert.Equal("hello@example.com", page.EmailInput.GetValue());
    }

    [Fact]
    public void TextInput_Clear_RemovesText()
    {
        NavigateToPage("/login");
        var page = new LoginPage(Context);

        page.EmailInput.SetText("temp@example.com");
        page.EmailInput.Clear();

        Assert.Equal(string.Empty, page.EmailInput.GetValue());
    }

    [Fact]
    public void TextInput_TypeText_AppendsTypedCharacters()
    {
        NavigateToPage("/login");
        var page = new LoginPage(Context);

        page.UsernameInput.SetText(string.Empty);
        page.UsernameInput.TypeText("sample-user");

        Assert.Equal("sample-user", page.UsernameInput.GetValue());
    }
}
