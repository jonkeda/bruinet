using Bruinet.Html.Bunit.Tests.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Bruinet.Html.Bunit.Tests;

/// <summary>What is left of the bUnit head's spike (AD-030): a held element reads the new render, and the sample renders.</summary>
public sealed class SpikeTests(ITestOutputHelper output)
{
    private static BunitTestContext Probe() => new(new BunitTestContextOptions { RootComponent = typeof(Probe) });

    [Fact]
    public void Find_ThenRerender_ElementReadsTheNewText()
    {
        using var context = Probe();
        var count = context.FindElement(Core.Locators.Locator.ByCss("[data-testid='count']"));

        context.FindElement(Core.Locators.Locator.ByCss("[data-testid='inc']")).Click();

        Assert.Equal("Count: 1", count.Text);
    }

    [Fact]
    public void Render_SampleCounterPage_HasItsButton()
    {
        using var context = new BunitTestContext(new BunitTestContextOptions
        {
            RootComponent = typeof(Samples.Blazor.App.Components.Routes),
            ConfigureServices = SampleServices,
            BaseUrl = "http://localhost/counter",
        });

        var button = context.FindElement(Core.Locators.Locator.ByCss("[data-testid='increment-btn']"));

        Assert.True(button.Visible);
        output.WriteLine($"{context.CurrentUrl}: {context.Markup.Length} characters of markup.");
    }

    internal static void SampleServices(IServiceCollection services)
    {
        services.AddScoped<Samples.Blazor.App.Services.IAuthService, Samples.Blazor.App.Services.AuthService>();
        services.AddSingleton<Samples.Blazor.App.Services.DataService>();
        services.AddSingleton<Samples.Blazor.App.Services.MediaService>();
        services.AddSingleton<Samples.Blazor.App.Services.ToastService>();
    }
}
