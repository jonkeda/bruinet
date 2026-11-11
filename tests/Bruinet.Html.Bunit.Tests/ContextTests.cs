using Bruinet.Core.Exceptions;
using Bruinet.Core.Locators;
using Bruinet.Html.Bunit.Tests.Components;
using Bruinet.Html.Controls.Buttons;
using Bruinet.Samples.Blazor.App.Components;
using Bruinet.Samples.Blazor.App.Services;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bruinet.Html.Bunit.Tests;

/// <summary>The bUnit head's context (AD-030): navigation, title, readiness, sign-in, scripts, reset.</summary>
public sealed class ContextTests
{
    private const string Base = "http://localhost";

    private static BunitTestContext Sample(string? path = null) => new(new BunitTestContextOptions
    {
        RootComponent = typeof(Routes),
        ConfigureServices = SpikeTests.SampleServices,
        BaseUrl = path is null ? null : Base + path,
    });

    private static BunitTestContext Of<T>(string? user = null, params string[] roles) => new(new BunitTestContextOptions
    {
        RootComponent = typeof(T),
        User = user,
        Roles = roles,
    });

    private static string Text(BunitTestContext context, string testId) => context.FindElement(Locator.ByDataTestId(testId)).Text ?? string.Empty;

    #region Navigation

    [Theory]
    [InlineData("https://elsewhere.test/login", "another host's page needs the browser head")]
    [InlineData("about:blank", "this app's pages only")]
    public void Navigate_ToAnAddressThisHeadCannotRender_IsAPageLoadFailure_NamingIt(string address, string why)
    {
        using var context = Sample();

        var error = Assert.Throws<PageLoadException>(() => context.NavigateTo(address));

        Assert.Contains(address, error.Message, StringComparison.Ordinal);
        Assert.Contains(why, error.Message, StringComparison.Ordinal);
        Assert.IsAssignableFrom<BruinetException>(error);
    }

    [Fact]
    public void Navigate_ToAPath_RendersThePage()
    {
        using var context = Sample();

        context.NavigateTo("/counter");

        Assert.Equal("Current count: 0", Text(context, "count-display"));
    }

    [Fact]
    public void Navigate_ToRoute_RendersThePage()
    {
        using var context = Sample();

        context.NavigateTo(Base + "/counter");

        Assert.Equal(Base + "/counter", context.CurrentUrl);
        Assert.Equal("Current count: 0", Text(context, "count-display"));
    }

    [Fact]
    public void Navigate_BaseUrl_IsWhereTheContextStarts()
    {
        using var context = Sample("/counter");

        Assert.Equal(Base + "/counter", context.CurrentUrl);
        Assert.NotNull(context.TryFindElement(Locator.ByDataTestId("increment-btn")));
    }

    [Fact]
    public void Navigate_ByALinkInTheLayout_RendersThePage()
    {
        using var context = Sample();
        var link = context.FindElements(Locator.ByCss("a[href='counter'], a[href='/counter']")).FirstOrDefault();
        Assert.NotNull(link);

        link.Click();

        Assert.NotNull(context.TryFindElement(Locator.ByDataTestId("increment-btn")));
    }

    // As Playwright's GoTo: a page load, a new circuit, the page's state gone.
    [Fact]
    public void Navigate_TheSameAddress_IsAPageLoad()
    {
        using var context = Sample("/counter");
        context.FindElement(Locator.ByDataTestId("increment-btn")).Click();

        context.NavigateTo(Base + "/counter");

        Assert.Equal("Current count: 0", Text(context, "count-display"));
    }

    [Fact]
    public void Navigate_AfterSigningIn_StaysSignedIn()
    {
        using var context = Of<Secret>();
        context.SignIn("ada@example.test", ["admin"]);

        context.NavigateTo(Base + "/");

        Assert.Equal("ada@example.test", Text(context, "who"));
        Assert.Equal("Admin", Text(context, "admin"));
    }

    [Fact]
    public void Reset_AfterSigningIn_IsSignedOut()
    {
        using var context = Of<Secret>("ada@example.test");

        context.ResetAppState();

        Assert.Equal("Not signed in", Text(context, "who"));
    }

    [Fact]
    public void Back_AfterTwoPages_ShowsTheFirst()
    {
        using var context = Sample();
        context.NavigateTo(Base + "/counter");
        context.NavigateTo(Base + "/login");

        context.NavigateBack();

        Assert.Equal(Base + "/counter", context.CurrentUrl);
        Assert.NotNull(context.TryFindElement(Locator.ByDataTestId("increment-btn")));
    }

    [Fact]
    public void Forward_AfterBack_ShowsTheSecond()
    {
        using var context = Sample();
        context.NavigateTo(Base + "/counter");
        context.NavigateTo(Base + "/login");
        context.NavigateBack();

        context.GoForward();

        Assert.Equal(Base + "/login", context.CurrentUrl);
    }

    [Fact]
    public void Back_WithNothingBehind_StaysPut()
    {
        using var context = Sample("/counter");

        context.NavigateBack();

        Assert.Equal(Base + "/counter", context.CurrentUrl);
    }

    [Fact]
    public void NavigateTo_RendersOnlyThatPage_NotTheStartPageFirst()
    {
        var loads = new PageLoads();
        using var context = new BunitTestContext(new BunitTestContextOptions
        {
            RootComponent = typeof(Components.Routing.LoadRoutes),
            ConfigureServices = services => services.AddSingleton(loads),
        });
        loads.Started = 0;

        context.NavigateTo(Base + "/other");

        Assert.NotNull(context.TryFindElement(Locator.ByDataTestId("other")));
        Assert.Equal(0, loads.Started);
    }

    [Fact]
    public void StartBlank_RendersNothing_UntilTheFirstNavigation()
    {
        var loads = new PageLoads();
        using var context = new BunitTestContext(new BunitTestContextOptions
        {
            RootComponent = typeof(Components.Routing.LoadRoutes),
            ConfigureServices = services => services.AddSingleton(loads),
            StartBlank = true,
        });

        Assert.Null(context.TryFindElement(Locator.ByDataTestId("start")));
        context.NavigateTo(Base + "/other");

        Assert.NotNull(context.TryFindElement(Locator.ByDataTestId("other")));
        Assert.Equal(0, loads.Started);
    }

    [Fact]
    public void Refresh_RendersThePageAgain_NotTheStartPage()
    {
        var loads = new PageLoads();
        using var context = new BunitTestContext(new BunitTestContextOptions
        {
            RootComponent = typeof(Components.Routing.LoadRoutes),
            ConfigureServices = services => services.AddSingleton(loads),
            BaseUrl = Base + "/other",
        });

        context.Refresh();

        Assert.Equal(Base + "/other", context.CurrentUrl);
        Assert.Equal(0, loads.Started);
    }

    [Fact]
    public void HistoryBack_FromThePage_GoesToTheAddressBefore()
    {
        using var context = Of<HistoryBack>();
        var start = context.CurrentUrl;
        context.FindElement(Locator.ByDataTestId("go")).Click();
        Assert.Equal(Base + "/elsewhere", context.CurrentUrl);

        context.FindElement(Locator.ByDataTestId("back")).Click();

        Assert.Equal(start, context.CurrentUrl);
    }

    // As a browser's history: a replaced entry is gone, so back skips it.
    [Fact]
    public void HistoryBack_AfterAReplace_SkipsTheReplacedEntry()
    {
        using var context = Of<HistoryBack>();
        var start = context.CurrentUrl;
        context.FindElement(Locator.ByDataTestId("go")).Click();
        context.FindElement(Locator.ByDataTestId("replace")).Click();
        Assert.Equal(Base + "/replaced", context.CurrentUrl);

        context.FindElement(Locator.ByDataTestId("back")).Click();

        Assert.Equal(start, context.CurrentUrl);
    }

    [Fact]
    public void HistoryBack_WithNothingBefore_StaysPut()
    {
        using var context = Of<HistoryBack>();
        var start = context.CurrentUrl;

        context.FindElement(Locator.ByDataTestId("back")).Click();

        Assert.Equal(start, context.CurrentUrl);
    }

    private static BunitTestContext Force() => new(new BunitTestContextOptions
    {
        RootComponent = typeof(Components.Routing.LoadRoutes),
        ConfigureServices = services => services.AddSingleton(new PageLoads()),
        BaseUrl = Base + "/force",
    });

    // The app's forceLoad is a page load, as the browser makes it: the page rendered anew, its state gone.
    [Fact]
    public void ForceLoad_TheSamePage_RendersItAnew()
    {
        using var context = Force();
        context.FindElement(Locator.ByDataTestId("bump")).Click();
        Assert.Equal("1", Text(context, "bumps"));

        context.FindElement(Locator.ByDataTestId("load-self")).Click();

        Assert.Equal("0", Text(context, "bumps"));
    }

    [Fact]
    public void ForceLoad_AnotherPage_RendersIt_AndBackReturns()
    {
        using var context = Force();

        context.FindElement(Locator.ByDataTestId("load-other")).Click();

        Assert.Equal(Base + "/other", context.CurrentUrl);
        Assert.NotNull(context.TryFindElement(Locator.ByDataTestId("other")));
        context.NavigateBack();
        Assert.NotNull(context.TryFindElement(Locator.ByDataTestId("bump")));
    }

    // A Razor Page or an endpoint, not a component: only a browser can load it.
    [Fact]
    public void ForceLoad_AnAddressNoComponentServes_IsBrowserOnly()
    {
        using var context = Force();

        context.FindElement(Locator.ByDataTestId("load-server")).Click();

        var error = Assert.Throws<BrowserOnlyException>(() => context.FindElement(Locator.ByDataTestId("bumps")));
        Assert.Contains("/Account/Logout", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refresh_RendersThePageAgain_FromItsStart()
    {
        using var context = Sample("/counter");
        context.FindElement(Locator.ByDataTestId("increment-btn")).Click();
        Assert.Equal("Current count: 1", Text(context, "count-display"));

        context.Refresh();

        Assert.Equal(Base + "/counter", context.CurrentUrl);
        Assert.Equal("Current count: 0", Text(context, "count-display"));
    }

    [Fact]
    public void Reset_StartsFromTheRoot()
    {
        using var context = Sample("/counter");
        context.NavigateTo(Base + "/login");

        context.ResetAppState();

        Assert.Equal(Base + "/counter", context.CurrentUrl);
        context.NavigateBack();
        Assert.Equal(Base + "/counter", context.CurrentUrl);
    }

    #endregion

    #region Title, markup and services

    [Fact]
    public void Title_IsThePagesPageTitle()
    {
        using var context = Sample("/counter");

        Assert.Equal("Counter - Bruinet Blazor Sample", context.PageTitle);

        context.NavigateTo(Base + "/login");
        Assert.Equal("Login - Bruinet Blazor Sample", context.PageTitle);
    }

    [Fact]
    public void Title_WithoutAPageTitle_IsEmpty()
    {
        using var context = Of<Probe>();

        Assert.Equal(string.Empty, context.PageTitle);
    }

    [Fact]
    public void Screenshot_WritesMarkup()
    {
        using var context = Sample("/counter");
        var folder = Directory.CreateTempSubdirectory("bruinet-bunit-");
        var path = Path.Combine(folder.FullName, "counter.png");

        context.SaveScreenshot(path);

        var written = File.ReadAllText(Path.Combine(folder.FullName, "counter.html"));
        Assert.Contains("increment-btn", written, StringComparison.Ordinal);
        Assert.Equal(context.Markup, System.Text.Encoding.UTF8.GetString(context.TakeScreenshot()));
        folder.Delete(recursive: true);
    }

    [Fact]
    public void Services_AreTheAppsOwn()
    {
        using var context = Sample();

        Assert.Same(context.Services.GetRequiredService<DataService>(), context.Services.GetRequiredService<DataService>());
    }

    #endregion

    #region Readiness

    [Fact]
    public void Busy_FollowsTheLayoutsBusyScript()
    {
        using var context = Of<Busy>();
        var page = new BusyPage(context);
        Assert.False(page.IsBusy());

        page.Work.Click();

        Assert.True(page.IsBusy());
        Assert.True(page.WaitBusy(false, timeoutMs: 3000));
        Assert.Equal("Done", Text(context, "state"));
    }

    [Fact]
    public void Busy_WithoutTheScript_IsInteractive()
    {
        using var context = Of<Probe>();

        Assert.False(new ProbePage(context).IsBusy());
        Assert.True(context.ProbeReadiness().IsReady);
    }

    [Fact]
    public void Busy_SamplePage_IsReadyAfterItsLayoutRenders()
    {
        using var context = Sample("/counter");

        Assert.Equal("false", context.ProbePage("data-busy").Busy);
    }

    #endregion

    #region Sign-in

    [Fact]
    public void SignedOut_AuthorizedPage_ShowsNotAuthorized()
    {
        using var context = Of<Secret>();

        Assert.Equal("Not signed in", Text(context, "who"));
        Assert.Equal("Not an admin", Text(context, "admin"));
    }

    [Fact]
    public void SignedIn_WithoutTheRole_ShowsTheUserOnly()
    {
        using var context = Of<Secret>("ada@example.test");

        Assert.Equal("ada@example.test", Text(context, "who"));
        Assert.Equal("Not an admin", Text(context, "admin"));
    }

    [Fact]
    public void SignedIn_WithTheRole_ShowsThePage()
    {
        using var context = Of<Secret>("ada@example.test", "admin");

        Assert.Equal("Admin", Text(context, "admin"));
    }

    [Fact]
    public void SignIn_WhileThePageIsRendered_ShowsTheUser()
    {
        using var context = Of<Secret>();
        Assert.Equal("Not signed in", Text(context, "who"));

        context.SignIn("ada@example.test", ["admin"]);

        Assert.Equal("ada@example.test", Text(context, "who"));
        Assert.Equal("Admin", Text(context, "admin"));
    }

    [Fact]
    public void SignOut_WhileThePageIsRendered_ShowsSignedOut()
    {
        using var context = Of<Secret>("ada@example.test");

        context.SignOut();

        Assert.Equal("Not signed in", Text(context, "who"));
    }

    #endregion

    #region Services

    [Fact]
    public void Fallback_AServiceTheContextDidNotRegister_ComesFromTheAppsProvider()
    {
        var app = new ServiceCollection().AddSingleton<IGreeting>(new FixedGreeting("from the app")).BuildServiceProvider();
        using var context = new BunitTestContext(new BunitTestContextOptions { RootComponent = typeof(Greeting), FallbackServices = app });

        Assert.Equal("from the app", Text(context, "greeting"));
    }

    [Fact]
    public void Fallback_AScopedService_IsNewAfterAReset_AsEachCircuitHasItsOwn()
    {
        var app = new ServiceCollection().AddScoped<IGreeting>(_ => new FixedGreeting(Guid.NewGuid().ToString("N"))).BuildServiceProvider();
        using var context = new BunitTestContext(new BunitTestContextOptions { RootComponent = typeof(Greeting), FallbackServices = app });
        var first = Text(context, "greeting");

        context.ResetAppState();

        Assert.NotEqual(first, Text(context, "greeting"));
    }

    // The app's host has its own navigation manager, not the circuit's: a host service that takes it is named, not used.
    [Fact]
    public void Fallback_AServiceThatTakesACircuitService_IsNamed()
    {
        var app = new ServiceCollection()
            .AddScoped<NavigationManager, HostNavigationManager>()
            .AddScoped<IGreeting, NavigatingGreeting>()
            .BuildServiceProvider();

        var error = Assert.ThrowsAny<Exception>(() => new BunitTestContext(new BunitTestContextOptions { RootComponent = typeof(Greeting), FallbackServices = app }));

        var message = error.ToString();
        Assert.Contains(nameof(NavigatingGreeting), message, StringComparison.Ordinal);
        Assert.Contains(nameof(NavigationManager), message, StringComparison.Ordinal);
        Assert.Contains("ConfigureServices", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Fallback_AServiceThatTakesACircuitService_RegisteredAgain_GetsBunits()
    {
        var app = new ServiceCollection()
            .AddScoped<NavigationManager, HostNavigationManager>()
            .AddScoped<IGreeting, NavigatingGreeting>()
            .BuildServiceProvider();

        using var context = new BunitTestContext(new BunitTestContextOptions
        {
            RootComponent = typeof(Greeting),
            FallbackServices = app,
            ConfigureServices = own => own.AddScoped<IGreeting, NavigatingGreeting>(),
        });

        Assert.StartsWith("http://localhost", Text(context, "greeting"), StringComparison.Ordinal);
    }

    // An app started again: its new services, in the same context, so what holds the context keeps a live one.
    [Fact]
    public void RestartApp_RendersWithTheNewServices_InTheSameContext()
    {
        var before = new ServiceCollection().AddSingleton<IGreeting>(new FixedGreeting("before")).BuildServiceProvider();
        var after = new ServiceCollection().AddSingleton<IGreeting>(new FixedGreeting("after")).BuildServiceProvider();
        using var context = new BunitTestContext(new BunitTestContextOptions { RootComponent = typeof(Greeting), FallbackServices = before });
        Assert.Equal("before", Text(context, "greeting"));

        context.RestartApp(after);
        before.Dispose();

        Assert.Equal("after", Text(context, "greeting"));
    }

    #endregion

    #region Rendering

    [Fact]
    public void Renderer_IsInteractiveServer_AsTheAppsCircuit()
    {
        using var context = Of<RenderedBy>();

        Assert.Equal("Server True", Text(context, "renderer"));
    }

    #endregion

    #region Scripts

    [Fact]
    public void Js_Loose_UnplannedCallGetsADefault()
    {
        using var context = Of<Scripted>();

        Assert.Equal(string.Empty, Text(context, "greeting"));
    }

    [Fact]
    public void Js_Strict_PlannedCallAnswers()
    {
        using var context = new BunitTestContext(new BunitTestContextOptions
        {
            RootComponent = typeof(Scripted),
            JsInterop = JSRuntimeMode.Strict,
            ConfigureJs = js => js.Setup<string>("app.greet").SetResult("hello"),
        });

        Assert.Equal("hello", Text(context, "greeting"));
    }

    [Fact]
    public void Js_Strict_UnplannedCallIsTheAppFailing()
    {
        var error = Record.Exception(() => new BunitTestContext(new BunitTestContextOptions
        {
            RootComponent = typeof(Scripted),
            JsInterop = JSRuntimeMode.Strict,
        }).Dispose());

        Assert.NotNull(error);
        Assert.Contains("app.greet", error.ToString(), StringComparison.Ordinal);
    }

    #endregion

    private sealed class BusyPage(IHtmlTestContext context) : BlazorPageObjectBase<BusyPage>(context)
    {
        public ButtonControl<BusyPage> Work => new(this, "[data-testid='work']");
    }
}
