using Bruinet.Cli.Selection;

namespace Bruinet.Cli.Tests.Selection;

// bruinet tests touch-map and affected: a suite, its app and a library on disk, and one run's timings.
public sealed class SelectionTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("bruinet-selection-").FullName;
    private readonly string _suite;
    private readonly string _app;

    public SelectionTests()
    {
        _suite = Write("Shop.UITests/Shop.UITests.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\Shop.App\Shop.App.csproj" />
                <ProjectReference Include="..\Lib\Lib.csproj" />
                <Compile Include="..\Shared\ShopFixture.cs" Link="Shared\ShopFixture.cs" />
                <Compile Include="..\Shared\Pages\*.cs" LinkBase="Shared\Pages" />
              </ItemGroup>
            </Project>
            """);
        Write("Shared/ShopFixture.cs", "public sealed class ShopFixture {}");
        Write("Shared/Pages/MenuPage.cs", "public sealed class MenuPage {}");
        Write("Shop.App/Components/Pages/Menu.razor", "<MenuList TestId=\"menu-list\" />");
        Write("Lib/Lib.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        Write("Lib/Control.cs", "class Control {}");
        _app = Write("Shop.App/Shop.App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        Write("Shop.App/Components/Pages/Counter.razor", """
            @page "/counter"
            <h1 data-testid="counter-title">Counter</h1>
            <button data-testid="increment-btn">+</button>
            """);
        Write("Shop.App/Components/Pages/Cart.razor", """
            @page "/cart"
            @foreach (var line in Lines) { <li data-testid="line-@line.Id">@line.Name</li> }
            <input id="coupon" />
            """);
        Write("Shop.App/Components/Pages/About.razor", "<p>About</p>");
        Write("Shop.App/Components/Pages/Bench.razor", "@page \"/bench/{Id}\"\n<BenchTools />");
        Write("Shared/Pages/BenchPage.cs", "public sealed class BenchPage() : PageBase(\"/bench/\") {}");
        Write("Shop.App/Components/Layout/MainLayout.razor", "@Body");
        Write("Shop.UITests/uat.config.md", "# UAT Config\n");
        Write("Shop.UITests/Features/counter.feature.md", """
            # Feature: Counter

            ## Scenario: Counting up

            ```gherkin
            Given the counter page
            ```
            """);
        Write("Shop.UITests/PageObjects/CounterPage.cs", "namespace Shop.UITests.PageObjects;\npublic sealed class CounterPage {}");
        Write("Shop.UITests/PageObjects/CartPage.cs", "namespace Shop.UITests.PageObjects;\npublic sealed class CartPage {}");
        Write("Shop.UITests/PageObjects/AboutPage.cs", "namespace Shop.UITests.PageObjects;\npublic sealed class AboutPage {}");
        Write("Shop.UITests/Tests/CartTests.cs", "namespace Shop.UITests.Tests;\npublic sealed class CartTests\n{\n    [Fact]\n    public void Coupon() {}\n}");
        Write("Shop.UITests/TestBase/ShopTestBase.cs", "namespace Shop.UITests;\npublic abstract class ShopTestBase {}");
        Write("timings/timings-1.csv", """
            Run;Head;Test;Scenario;Step;Kind;Name;Page;Control;Action;StartMs;DurationMs;Attempts;Result
            1;bunit;Shop.UITests.Tests.CounterTests.Title;;;navigate;NavigateTo;;;;0;9;;
            1;bunit;Shop.UITests.Tests.CounterTests.Title;;;call;AssertVisible;CounterPage;[data-testid='counter-title'];AssertVisible;9;1;1;ok
            1;bunit;Shop.UITests.Tests.CartTests.Coupon;;;call;Enter;CartPage;#coupon;Enter;10;1;1;ok
            1;bunit;Shop.UITests.Tests.CartTests.Lines;;;call;Count;CartPage;Rows('Css:[data-testid^='line-']');Count;11;1;1;ok
            1;bunit;Shop.UITests.Tests.ShellTests.Menu;;;call;Click;SampleShell;SampleShell;Click;12;1;1;ok
            1;bunit;Shop.UITests.Tests.MenuTests.Open;;;call;Click;MenuPage;menu-list;Click;12;1;1;ok
            1;bunit;Shop.UITests.Tests.BenchTests.Tools;;;call;Click;BenchPage;bench-tool;Click;12;1;1;ok
            1;bunit;Shop.UITests.Tests.CounterTests.Visible;;;query;TryFind;CounterPage;[data-testid='increment-btn'];TryFind;12;0;;
            1;bunit;Uat: Counting up;Counting up;3: Given the counter page;call;Click;CounterPage;[data-testid='increment-btn'];Click;13;1;1;ok
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Write(string relative, string content)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return Path.GetDirectoryName(path)!;
    }

    private string At(string relative) => Path.Combine(_root, relative);

    private TouchMap Map() => TouchMap.Read(TouchMap.CsvFiles(At("timings")), _suite);

    private Affected Changed(params string[] files) => Affected.Of(_suite, null, files.Select(At), Map());

    [Fact]
    public void TouchMap_KeysATestByItsFullName_AndAScenarioByItsAddress()
    {
        var map = Map();

        Assert.Equal("FullyQualifiedName=Shop.UITests.Tests.CounterTests.Title", map.Tests.Single(test => test.Name == "Shop.UITests.Tests.CounterTests.Title").Filter);
        var scenario = map.Tests.Single(test => test.Name == "Counting up");
        Assert.Equal("BddScenario=Features/counter.feature.md#Counting-up", scenario.Filter);
        Assert.Equal(["CounterPage"], scenario.Pages);
        Assert.Equal(["[data-testid='increment-btn']"], scenario.Controls);
    }

    [Fact]
    public void TouchMap_CountsAQueryOutsideACall()
        => Assert.Equal(["[data-testid='increment-btn']"], Map().Tests.Single(test => test.Name == "Shop.UITests.Tests.CounterTests.Visible").Controls);

    [Fact]
    public async Task Timings_WritesTheReport_BesideTheTimings()
    {
        var errors = new StringWriter();
        var exit = await BruinetCommand.RunAsync(["tests", "timings", "timings", "--top", "2"], new CliIo(TextReader.Null, TextWriter.Null, errors, _root, Interactive: false));

        Assert.Equal(0, exit);
        var report = File.ReadAllText(At("timings/" + TimingsReport.FileName));
        Assert.StartsWith("# Timings", report, StringComparison.Ordinal);
        Assert.Contains("| call |", report, StringComparison.Ordinal);
        Assert.Contains("| Shop.UITests.Tests.CounterTests.Title |", report, StringComparison.Ordinal);
        Assert.Equal(2, report.Split("## The slowest call")[1].Split('\n').Count(line => line.StartsWith("| 1 |", StringComparison.Ordinal)));
        Assert.Contains("wrote", errors.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void TouchMap_SavedAndLoaded_IsTheSame()
    {
        var map = Map();
        map.Save(At(TouchMap.FileName));

        Assert.Equal(map.Tests.Select(test => (test.Name, test.Filter, string.Join(',', test.Controls))),
            TouchMap.Load(At(TouchMap.FileName)).Tests.Select(test => (test.Name, test.Filter, string.Join(',', test.Controls))));
    }

    [Fact]
    public void APage_ReachesTheTestsThatUsedItsTestIds_AndNoOthers()
    {
        var affected = Changed("Shop.App/Components/Pages/Counter.razor");

        Assert.Null(affected.Whole);
        Assert.Equal(["Counting up", "Shop.UITests.Tests.CounterTests.Title", "Shop.UITests.Tests.CounterTests.Visible"], affected.Picked.Select(test => test.Name));
        Assert.Equal("BddScenario=Features/counter.feature.md#Counting-up|FullyQualifiedName=Shop.UITests.Tests.CounterTests.Title|FullyQualifiedName=Shop.UITests.Tests.CounterTests.Visible", affected.Filter);
        Assert.Contains("counter-title of Components/Pages/Counter.razor", affected.Picked[1].Why);
    }

    [Fact]
    public void TheMcpServersTestsAffected_GivesThePickedTests_WithWhy_AndTheFilter()
    {
        Map().Save(At("Shop.UITests/" + TouchMap.FileName));
        using var server = new Bruinet.Bdd.LanguageServer.BddMcpServer(_root);

        var text = Tool(server, "list_affected_tests", new System.Text.Json.Nodes.JsonObject
        {
            ["suite"] = "Shop.UITests",
            ["files"] = new System.Text.Json.Nodes.JsonArray("Shop.App/Components/Pages/Counter.razor"),
        });

        var answer = System.Text.Json.Nodes.JsonNode.Parse(text)!;
        Assert.Equal(1, (int?)answer["changed"]);
        Assert.Equal(3, answer["picked"]!.AsArray().Count);
        Assert.Contains("counter-title of Components/Pages/Counter.razor", (string?)answer["picked"]![1]!["why"]);
        Assert.StartsWith("BddScenario=Features/counter.feature.md#Counting-up|", (string?)answer["filter"]);
    }

    [Fact]
    public void TheMcpServersTestsAffected_WithoutATouchMap_SaysHowToMakeOne()
    {
        using var server = new Bruinet.Bdd.LanguageServer.BddMcpServer(_root);

        Assert.Contains("BRUINET_TIMINGS=1", Tool(server, "list_affected_tests", new System.Text.Json.Nodes.JsonObject { ["suite"] = "Shop.UITests", ["files"] = new System.Text.Json.Nodes.JsonArray("Lib/Control.cs") }));
    }

    [Fact]
    public void TheMcpServersTestsAffected_RefusesABase_GitWouldReadAsAnOption()
    {
        Map().Save(At("Shop.UITests/" + TouchMap.FileName));
        using var server = new Bruinet.Bdd.LanguageServer.BddMcpServer(_root);

        var result = Result(server, "list_affected_tests", new System.Text.Json.Nodes.JsonObject { ["suite"] = "Shop.UITests", ["base"] = "--output=written.txt" });

        Assert.True((bool)result["isError"]!);
        Assert.Contains("not a commit", (string?)result["content"]![0]!["text"], StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_root, "written.txt")));
    }

    [Fact]
    public void TheMcpServersReviewPage_RefusesAPath_OutsideTheSolution()
    {
        var video = At("Shop.UITests/TestResults/recordings/how.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(video)!);
        File.WriteAllBytes(video, []);
        using var server = new Bruinet.Bdd.LanguageServer.BddMcpServer(_root);

        var result = Result(server, "write_review", new System.Text.Json.Nodes.JsonObject
        {
            ["title"] = "Outside",
            ["videos"] = new System.Text.Json.Nodes.JsonArray(video),
            ["path"] = "../outside-review.html",
        });

        Assert.True((bool)result["isError"]!);
        Assert.Contains("outside the solution", (string?)result["content"]![0]!["text"], StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_root)!, "outside-review.html")));
    }

    private static System.Text.Json.Nodes.JsonNode Result(Bruinet.Bdd.LanguageServer.BddMcpServer server, string name, System.Text.Json.Nodes.JsonObject arguments)
        => server.Handle(new System.Text.Json.Nodes.JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "tools/call",
            ["params"] = new System.Text.Json.Nodes.JsonObject { ["name"] = name, ["arguments"] = arguments },
        }.ToJsonString())!["result"]!;

    private static string Tool(Bruinet.Bdd.LanguageServer.BddMcpServer server, string name, System.Text.Json.Nodes.JsonObject arguments)
        => (string)server.Handle(new System.Text.Json.Nodes.JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "tools/call",
            ["params"] = new System.Text.Json.Nodes.JsonObject { ["name"] = name, ["arguments"] = arguments },
        }.ToJsonString())!["result"]!["content"]![0]!["text"]!;

    [Fact]
    public void APagesCode_ReachesWhatItsMarkupDoes()
        => Assert.Equal(3, Changed("Shop.App/Components/Pages/Counter.razor.cs").Picked.Count);

    [Fact]
    public void AnIdBuiltAtRunTime_CountsByItsStart_AndAPlainId()
        => Assert.Equal(["Shop.UITests.Tests.CartTests.Coupon", "Shop.UITests.Tests.CartTests.Lines"], Changed("Shop.App/Components/Pages/Cart.razor").Picked.Select(test => test.Name));

    [Fact]
    public void APageNoTestReaches_RunsNothing_AndSaysSo()
    {
        var affected = Changed("Shop.App/Components/Pages/About.razor");

        Assert.Null(affected.Filter);
        Assert.Contains("Components/Pages/About.razor: no test ids", affected.Notes);
    }

    [Fact]
    public void APageObject_ReachesTheTestsThatUsedIt()
        => Assert.Equal(["Shop.UITests.Tests.CartTests.Coupon", "Shop.UITests.Tests.CartTests.Lines"], Changed("Shop.UITests/PageObjects/CartPage.cs").Picked.Select(test => test.Name));

    [Fact]
    public void ATestClass_RunsItsTests()
        => Assert.Equal("FullyQualifiedName~Shop.UITests.Tests.CartTests.", Changed("Shop.UITests/Tests/CartTests.cs").Filter);

    [Fact]
    public void APageObjectTheSuiteLinksFromElsewhere_ReachesItsTests()
        => Assert.Equal(["Shop.UITests.Tests.MenuTests.Open"], Changed("Shared/Pages/MenuPage.cs").Picked.Select(test => test.Name));

    [Fact]
    public void AComponentsTestIdParameter_CountsAsATestId()
        => Assert.Equal(["Shop.UITests.Tests.MenuTests.Open"], Changed("Shop.App/Components/Pages/Menu.razor").Picked.Select(test => test.Name));

    // Its tests use only a child component's ids; they open its route through their page object.
    [Fact]
    public void ARoutedPage_ReachesTheTestsWhosePageObjectOpensIt()
    {
        var picked = Changed("Shop.App/Components/Pages/Bench.razor").Picked;

        Assert.Equal(["Shop.UITests.Tests.BenchTests.Tools"], picked.Select(test => test.Name));
        Assert.Contains("opens /bench/ (BenchPage)", picked[0].Why);
    }

    [Fact]
    public void AFeatureFile_RunsItsScenarios()
        => Assert.Equal("BddScenario=Features/counter.feature.md#Counting-up", Changed("Shop.UITests/Features/counter.feature.md").Filter);

    [Theory]
    [InlineData("Shop.App/Components/Layout/MainLayout.razor", "shared by the app's pages")]
    [InlineData("Shop.App/Program.cs", "shared by the app's pages")]
    [InlineData("Lib/Control.cs", "in Lib, which the suite builds on")]
    [InlineData("Shop.UITests/TestBase/ShopTestBase.cs", "the suite's shared test code")]
    [InlineData("Shared/ShopFixture.cs", "the suite's shared test code")]
    [InlineData("Shop.UITests/uat.config.md", "the suite's set-up")]
    [InlineData("Directory.Packages.props", "every project's build")]
    public void SharedCode_RunsTheSuiteWhole(string file, string why)
    {
        var affected = Changed("Shop.App/Components/Pages/Counter.razor", file);

        Assert.Contains(why, affected.Whole);
        Assert.Null(affected.Filter);
    }

    [Fact]
    public void AFileOutsideTheSuiteItsAppAndItsLibraries_RunsNothing()
    {
        var affected = Changed("docs/readme.md", "Other/Other.cs");

        Assert.Null(affected.Whole);
        Assert.Empty(affected.Picked);
    }

    [Fact]
    public void TheAlwaysSet_JoinsAPick_ButNotAnEmptyOne()
    {
        Assert.EndsWith("|Gate=Smoke", Affected.Of(_suite, null, [At("Shop.App/Components/Pages/Counter.razor")], Map(), ["Gate=Smoke"]).Filter);
        Assert.Null(Affected.Of(_suite, null, [At("docs/readme.md")], Map(), ["Gate=Smoke"]).Filter);
    }

    [Fact]
    public async Task TheCommands_WriteTheMap_ThenPrintTheFilterWithWhy()
    {
        var io = new CliIo(TextReader.Null, new StringWriter(), new StringWriter(), _root, Interactive: false);
        Assert.Equal(0, await BruinetCommand.RunAsync(["tests", "touch-map", "timings", "--project", "Shop.UITests"], io));
        Assert.True(File.Exists(Path.Combine(_suite, TouchMap.FileName)));

        var output = new StringWriter();
        var exit = await BruinetCommand.RunAsync(["tests", "affected", "--project", "Shop.UITests", "--files", "Shop.UITests/PageObjects/CounterPage.cs"],
            io with { Out = output });

        Assert.Equal(0, exit);
        Assert.Contains("Counting up: uses CounterPage (PageObjects/CounterPage.cs)", output.ToString());
        Assert.Contains("--filter \"BddScenario=Features/counter.feature.md#Counting-up|FullyQualifiedName=Shop.UITests.Tests.CounterTests.Title|FullyQualifiedName=Shop.UITests.Tests.CounterTests.Visible\"", output.ToString());
    }
}
