namespace Bruinet.Contracts.Tests;

[Trait("Category", "Unit")]
[Trait("Feature", "Contract")]
[Trait("Pyramid", "Unit")]
public sealed class ExchangeLookupTests
{
    [Theory]
    [InlineData("""[{"id":1},{"id":7,"name":"a"}]""")]
    [InlineData("""{"count":2,"items":[{"id":1},{"id":7,"name":"a"}]}""")]
    [InlineData("""[{"id":1},{"id":7,"name":"a"},{"id":9,"na""")]
    public void FindItem_FindsTheItemInARootArrayAPageObjectOrATruncatedArray(string body)
        => Assert.Equal("""{"id":7,"name":"a"}""", ExchangeLookup.FindItem(body, 7));

    [Theory]
    [InlineData("""{"count":1,"items":[{"id":1}]}""")]
    [InlineData("""{"id":7}""")]
    [InlineData("not json")]
    public void FindItem_ReturnsNullWhenNoListItemHasTheId(string body)
        => Assert.Null(ExchangeLookup.FindItem(body, 7));
}
