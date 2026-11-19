using Bruinet.Html.Pages;

namespace Bruinet.Html.Tests;

/// <summary>A page whose address has a record's id in it is at its address on any record, and on no other page.</summary>
public sealed class PageAddressTests
{
    [Theory]
    [InlineData("/orders/{id}", "https://app.example/orders/12", true)]
    [InlineData("/orders/{id}", "https://app.example/orders/12?tab=lines", true)]
    [InlineData("/orders/{id}", "https://app.example/orders", false)]
    [InlineData("/orders/{id}", "https://app.example/orders/12/edit", false)]
    [InlineData("/orders/{id}/edit", "https://app.example/orders/12/edit", true)]
    [InlineData("/contact#/view/{id}", "https://app.example/contact#/view/55313", true)]
    [InlineData("/contact#/view/{id}", "https://app.example/contact?_=1a2b#/view/55313", true)]
    [InlineData("/contact#/view/{id}", "https://app.example/contact#/update/55313", false)]
    [InlineData("/contact#/view/{id}", "https://app.example/contact#/", false)]
    [InlineData("/contact#/view/{id}", "https://app.example/project#/view/55313", false)]
    [InlineData("/project/view?id={id}#/planning", "https://app.example/project/view?planTab=project&id=17805#/planning", true)]
    [InlineData("/project/view?id={id}#/planning", "https://app.example/project/view?id=17805#/waste", false)]
    [InlineData("/project/view?id={id}", "https://app.example/project/view?id=17805", true)]
    public void An_address_with_an_id_matches_any_record_of_it(string address, string location, bool expected)
        => Assert.Equal(expected, PageAddress.Matches(address, new Uri(location)));

    [Fact]
    public void Only_an_address_with_the_placeholder_has_an_id()
    {
        Assert.True(PageAddress.HasId("/orders/{id}"));
        Assert.False(PageAddress.HasId("/orders"));
    }
}
