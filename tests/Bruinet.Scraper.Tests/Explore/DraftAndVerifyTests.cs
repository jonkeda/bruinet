using Bruinet.Scraper.Code;
using Bruinet.Scraper.Explore;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Service;
using Bruinet.Scraper.TestSite;

namespace Bruinet.Scraper.Tests.Explore;

// The test site explored, its pages drafted into a project that builds against Bruinet.Html, and the
// drafts verified against the site. Headless Chromium and a real build: a minute or two.
[Trait("Category", "Browser")]
[Trait("Category", "Build")]
public sealed class DraftAndVerifyTests : IAsyncLifetime
{
    private readonly TestSiteServer _site = new();
    private readonly string _solution = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));
    private ScraperService _service = null!;
    private string _folder = null!;

    public async Task InitializeAsync()
    {
        _folder = Path.Combine(_solution, "tests", "Shop.UITests");
        Directory.CreateDirectory(_folder);
        var html = Path.Combine(RepositoryRoot(), "src", "Bruinet.Html", "Bruinet.Html.csproj");
        // Restored from nuget.org as Bruinet is, not from the feeds of the machine's own config.
        File.Copy(Path.Combine(RepositoryRoot(), "nuget.config"), Path.Combine(_solution, "nuget.config"));
        File.WriteAllText(Path.Combine(_folder, "Shop.UITests.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="{html}" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(_folder, ScraperConfig.FileName),
            $"## App\n\n| Field | Value |\n| --- | --- |\n| StartUrl | {_site.Url("pages/account.html")} |\n| Namespace | Shop.UITests.Pages |\n\n## Explore\n\n| Field | Value |\n| --- | --- |\n| DelayMs | 1 |\n| MaxDepth | 1 |\n");
        _service = new ScraperService(_solution, _ => new PlaywrightScrapeSource());
        var explored = await _service.ExploreAsync(null, new ExploreOptions { Visible = false });
        Assert.True(explored.WithinGuards, explored.StopDetail);
    }

    public async Task DisposeAsync()
    {
        await _service.DisposeAsync();
        _site.Dispose();
        if (Directory.Exists(_solution))
            Directory.Delete(_solution, recursive: true);
    }

    private string Pages(string file) => Path.Combine(_folder, "Pages", file);

    [Fact]
    public async Task The_explored_pages_are_drafted_built_and_verified_and_a_second_draft_changes_nothing()
    {
        var drafted = await _service.DraftAsync(null);

        Assert.True(drafted.Build!.Succeeded, string.Join('\n', drafted.Build.Problems) + drafted.Build.Output);
        var account = Assert.Single(drafted.Pages, p => p.Key == "/pages/account.html");
        Assert.Equal((DraftOutcome.Written, "AccountPage"), (account.Outcome, account.ClassName));
        var code = File.ReadAllText(Pages("AccountPage.scraped.cs"));
        Assert.Contains("public sealed partial class AccountPage(IHtmlTestContext context) : PageObjectBase<AccountPage>(context)", code);
        Assert.Contains("OrderDelete => new(this, Locator.ByDataTestId(\"order-delete\"));", code);
        // The dialog's close button is in the draft though the page opens with the dialog closed.
        Assert.Contains("OrderClose => new(this, Locator.ByDataTestId(\"order-close\"));", code);

        var verified = await _service.VerifyAsync(null);

        var page = Assert.Single(verified.Pages, p => p.ClassName == "AccountPage");
        Assert.True(verified.Passed, string.Join('\n', verified.Pages.SelectMany(p => p.Members.Where(m => m.Verdict != MemberVerdict.Once).Select(m => $"{p.ClassName}.{m.Name}: {m.Locator} {m.Count}"))));
        Assert.Contains(page.Members, m => m.Name == "OrderClose" && m.Verdict == MemberVerdict.Once);

        var again = await _service.DraftAsync(null);

        Assert.All(again.Pages, p => Assert.Equal(DraftOutcome.Unchanged, p.Outcome));
        Assert.Null(again.Build);
    }

    [Fact]
    public async Task A_person_s_part_is_kept_and_a_member_it_uses_that_a_new_draft_drops_puts_the_draft_back()
    {
        await _service.DraftAsync(null);
        // As if an earlier recording had a Gone element, and a person's part uses it.
        var scraped = Pages("AccountPage.scraped.cs");
        var withGone = File.ReadAllText(scraped).Replace("public sealed partial class AccountPage(IHtmlTestContext context) : PageObjectBase<AccountPage>(context)\n{\n",
            "public sealed partial class AccountPage(IHtmlTestContext context) : PageObjectBase<AccountPage>(context)\n{\n    public ButtonControl<AccountPage> Gone => new(this, Locator.ByDataTestId(\"gone\"));\n\n", StringComparison.Ordinal);
        File.WriteAllText(scraped, withGone);
        File.WriteAllText(Pages("AccountPage.cs"), "namespace Shop.UITests.Pages;\n\npublic sealed partial class AccountPage\n{\n    public object Both => (Gone, OrderDelete);\n}\n");

        var drafted = await _service.DraftAsync(null, "/pages/account.html");

        var account = Assert.Single(drafted.Pages);
        Assert.Equal(DraftOutcome.Reverted, account.Outcome);
        Assert.Equal("Gone", Assert.Single(account.Removed).Name);
        Assert.Contains(account.Problems, p => p.Code == "CS0103" && p.Message.Contains("Gone", StringComparison.Ordinal));
        Assert.Equal(withGone, File.ReadAllText(scraped));
        Assert.StartsWith("namespace Shop.UITests.Pages;", File.ReadAllText(Pages("AccountPage.cs")));
        Assert.True(drafted.Build!.Succeeded);
    }

    [Fact]
    public async Task Verify_says_which_member_is_not_found_and_which_is_found_several_times()
    {
        await _service.DraftAsync(null);
        File.WriteAllText(Pages("AccountPage.cs"), """
            using Bruinet.Core.Locators;
            using Bruinet.Html.Controls.Buttons;

            namespace Shop.UITests.Pages;

            public sealed partial class AccountPage
            {
                public ButtonControl<AccountPage> Missing => new(this, Locator.ByDataTestId("not-there"));

                public ButtonControl<AccountPage> AnyTab => new(this, Locator.ByCss("[role=tab]"));
            }
            """);

        var verified = await _service.VerifyAsync(null);

        Assert.False(verified.Passed);
        var members = verified.Pages.Single(p => p.ClassName == "AccountPage").Members;
        Assert.Contains(members, m => m.Name == "Missing" && m.Verdict == MemberVerdict.NotFound);
        Assert.Contains(members, m => m.Name == "AnyTab" && m.Verdict == MemberVerdict.Several && m.Count == 2);
    }

    [Fact]
    public async Task A_member_of_the_project_s_own_control_is_not_read_as_a_selector()
    {
        await _service.DraftAsync(null);
        File.WriteAllText(Path.Combine(_folder, "TabStrip.cs"), """
            using Bruinet.Core.Locators;
            using Bruinet.Html.Controls;
            using Bruinet.Html.Interfaces;

            namespace Shop.UITests;

            public sealed class TabStrip<TParent>(IHtmlScope<TParent> parent, string label)
                : ContainerBase<TParent, TabStrip<TParent>>(parent, Locator.ByCss($"[aria-label='{label}']"))
                where TParent : IHtmlScope<TParent>
            {
                public override TabStrip<TParent> Self => this;
            }
            """);
        File.WriteAllText(Pages("AccountPage.cs"), """
            namespace Shop.UITests.Pages;

            public sealed partial class AccountPage
            {
                public TabStrip<AccountPage> Strip => new(this, "Account");
            }
            """);

        var verified = await _service.VerifyAsync(null);

        var strip = verified.Pages.Single(p => p.ClassName == "AccountPage").Members.Single(m => m.Name == "Strip");
        Assert.Equal(MemberVerdict.Unreadable, strip.Verdict);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Bruinet.Html")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("The tests do not run inside the Bruinet repository.");
    }
}
