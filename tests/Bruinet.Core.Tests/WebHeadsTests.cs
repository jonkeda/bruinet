using Bruinet.Core.Testing;

namespace Bruinet.Core.Tests;

public sealed class WebHeadsTests
{
    [Theory]
    [InlineData("bunit", WebHead.Bunit)]
    [InlineData(" BUnit ", WebHead.Bunit)]
    [InlineData("browser", WebHead.Browser)]
    [InlineData("", WebHead.Browser)]
    [InlineData(null, WebHead.Browser)]
    [InlineData("chromium", WebHead.Browser)]
    public void Parse_NamesTheHead_TheBrowserUnlessBunit(string? value, WebHead expected)
        => Assert.Equal(expected, WebHeads.Parse(value));
}
