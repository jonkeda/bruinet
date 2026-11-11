using System.Net;
using System.Net.Sockets;
using Bruinet.Core.Exceptions;
using Bruinet.Html.Playwright;
using Xunit;

namespace Bruinet.Html.UITests.Tests.Scenarios;

/// <summary>
/// A navigation the browser could not make is one typed failure naming the address (review 01, finding 5; R2-T35),
/// not Playwright's raw net::ERR_CONNECTION_REFUSED.
/// </summary>
public sealed class NavigationFailureTests : IAsyncLifetime
{
    private PlaywrightTestContext? _context;

    public async Task InitializeAsync()
        => _context = await PlaywrightTestContext.CreateAsync(new HtmlTestContextOptions { Headless = true });

    public async Task DisposeAsync()
    {
        if (_context != null)
        {
            await _context.DisposeAsync();
        }
    }

    [Fact]
    public void An_AddressNobodyAnswers_IsAPageLoadFailure_NamingIt()
    {
        var address = $"http://127.0.0.1:{FreePort()}/counter";

        var error = Assert.Throws<PageLoadException>(() => _context!.NavigateTo(address));

        Assert.StartsWith($"Could not open {address}:", error.Message, StringComparison.Ordinal);
        Assert.Contains("ERR_CONNECTION_REFUSED", error.Message, StringComparison.Ordinal);
    }

    // A port that was free a moment ago, so nothing listens on it.
    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
