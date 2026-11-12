// docs/guides/bunit.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
using Bruinet.Blazor.Context;
using Bruinet.Core.Testing;
using Bruinet.Html.Bunit;
using Bruinet.Html.Context;
using Bruinet.Html.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bruinet.Docs.Samples.Html;

public sealed class WebSuite
{
    private IHtmlTestContext _context = null!;

    public async Task StartAsync(HtmlTestContextOptions options)
    {
        #region docs:head
        _context = WebHeads.IsBunit
            ? new BunitTestContext(new BunitTestContextOptions
            {
                RootComponent = typeof(Routes),        // the app's router, so pages open by address
                ConfigureServices = services =>         // the app's own services
                {
                    services.AddScoped<IAuthService, AuthService>();
                    services.AddSingleton<DataService>();
                },
            })
            : await BlazorTestContext.CreateAsync(options);
        #endregion
    }
}

public sealed class Routes : ComponentBase;

public interface IAuthService;

public sealed class AuthService : IAuthService;

public sealed class DataService;
