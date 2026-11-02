using Xunit;

namespace Bruinet.Bdd.Workspace.Tests;

/// <summary>Where an app makes the element a locator finds (AI7): a static search of the app's sources, no build step.</summary>
public sealed class BddAppSourcesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Bruinet", "BddAppSourcesTests", Guid.NewGuid().ToString("N"));

    public BddAppSourcesTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData("DataTestId:TodoList_Sync", "TodoList_Sync")]
    [InlineData("AutomationId:Counter", "Counter")]
    [InlineData("[data-testid='counter-title']", "counter-title")]
    [InlineData("Css:[data-testid=\"save\"]", "save")]
    [InlineData("#login", "login")]
    [InlineData("DataTestId:List > DataTestId:Row_3", "Row_3")]
    [InlineData("XPath://button[@id='go']", "go")]
    public void ValueOf_AFailuresLocator_GivesTheTestId(string locator, string value)
        => Assert.Equal(new BddLocatorValue(value, IsPrefix: false), BddAppSources.ValueOf(locator));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Css:h1")]
    [InlineData("button")]
    public void ValueOf_NoValueOrATagName_IsNull(string? locator) => Assert.Null(BddAppSources.ValueOf(locator));

    [Fact]
    public void ValueAt_APlainLiteral_ACssSelector_AndAnInterpolatedOne()
    {
        var file = Write("Pages/DetailPage.cs", """
            public sealed class DetailPage
            {
                public ButtonControl<DetailPage> DeleteButton => new(this, Locator.ByDataTestId("TodoDetail_Delete"));
                public LabelControl<DetailPage> Title
                    => new(this, "[data-testid='detail-title']");
                public RowControl<DetailPage> Row(int id) => new(this, Locator.ByDataTestId($"Row_{id}"));
                public LabelControl<DetailPage> Heading => new(this, Locator.ByDataTestId(nameof(Heading)));
            }
            """);

        Assert.Equal(new BddLocatorValue("TodoDetail_Delete", IsPrefix: false), BddAppSources.ValueAt(file, 3));
        Assert.Equal(new BddLocatorValue("detail-title", IsPrefix: false), BddAppSources.ValueAt(file, 4));
        Assert.Equal(new BddLocatorValue("Row_", IsPrefix: true), BddAppSources.ValueAt(file, 6));
        Assert.Null(BddAppSources.ValueAt(file, 7));
    }

    [Fact]
    public void Find_PutsTestIdsFirst_AndLeavesOutTestsAndBuildOutput()
    {
        Write("src/Shop.Web/Pages/Cart.razor", """
            <h1>Cart</h1>
            <button data-testid="cart-save">Save</button>
            """);
        Write("src/Shop.Web/Pages/Cart.razor.cs", """var name = "cart-save";""");
        Write("src/Shop.Web/obj/Cart.razor.g.cs", """builder.AddAttribute(1, "data-testid", "cart-save");""");
        Write("tests/Shop.UITests/CartPage.cs", """Locator.ByDataTestId("cart-save")""");
        Write("src/Shop.Web/Pages/Other.razor", """<button data-testid="cart-save-all">All</button>""");

        var hits = new BddAppSources(_root).Find(new BddLocatorValue("cart-save", IsPrefix: false));

        Assert.Collection(
            hits,
            hit => Assert.True(hit is { Line: 2, IsAttribute: true } && hit.File.EndsWith("Cart.razor", StringComparison.Ordinal)),
            hit => Assert.True(hit is { Line: 1, IsAttribute: false } && hit.File.EndsWith("Cart.razor.cs", StringComparison.Ordinal)));
    }

    [Fact]
    public void Find_AValueBuiltAtRunTime_FindsItsStart()
    {
        Write("src/Shop.Web/Pages/Cart.razor", """<li data-testid="Row_@item.Id">@item.Name</li>""");
        Write("src/Shop.App/Pages/Cart.xaml", """<Label AutomationId="{Binding Id, StringFormat='Row_{0}'}" />""");

        var hits = new BddAppSources(_root).Find(new BddLocatorValue("Row_", IsPrefix: true));

        Assert.Equal(2, hits.Count);
    }

    [Fact]
    public void Find_TheSameIdInAWebAndAXamlApp_PutsThePageObjectsOwnFirst()
    {
        Write("src/Shop.Web/Shop.Web.csproj", "<Project />");
        Write("src/Shop.Web/Pages/CartPage.razor", """<button data-testid="Save">Save</button>""");
        Write("src/Shop.Web/Pages/OrderPage.razor", """<button data-testid="Save">Save</button>""");
        Write("src/Shop.App/Shop.App.csproj", "<Project />");
        Write("src/Shop.App/Views/CartView.xaml", """<Button AutomationId="Save" />""");
        var sources = new BddAppSources(_root);
        var save = new BddLocatorValue("Save", IsPrefix: false);

        var web = sources.Find(save, hint: new BddAppSourceHint(null, "CartTestPage", Xaml: false));
        var xaml = sources.Find(save, hint: new BddAppSourceHint(null, "CartTestPage", Xaml: true));

        Assert.EndsWith("CartPage.razor", web[0].File, StringComparison.Ordinal);
        Assert.EndsWith("CartView.xaml", xaml[0].File, StringComparison.Ordinal);
    }

    [Fact]
    public void Find_SkipsOtherCheckoutsAndDotFolders_AndKeepsAnAppBesideTheTests()
    {
        Write("src/Shop.Web/Pages/Cart.razor", """<button data-testid="pay">Pay</button>""");
        Write(".claude/worktrees/agent/src/Shop.Web/Pages/Cart.razor", """<button data-testid="pay">Pay</button>""");
        Write("external/lib/.git", "gitdir: ../.git/modules/lib");
        Write("external/lib/Pay.razor", """<button data-testid="pay">Pay</button>""");
        Write("tests/Shop.TestSite/Pay.razor", """<button data-testid="pay">Pay</button>""");
        Write("tests/Shop.Checks/Shop.Checks.csproj", """<Project><ItemGroup><PackageReference Include="xunit" /></ItemGroup></Project>""");
        Write("tests/Shop.Checks/PayPage.cs", """Locator.ByDataTestId("pay")""");

        var files = new BddAppSources(_root).Find(new BddLocatorValue("pay", IsPrefix: false)).Select(hit => Path.GetRelativePath(_root, hit.File));

        Assert.Equal([Path.Combine("src", "Shop.Web", "Pages", "Cart.razor"), Path.Combine("tests", "Shop.TestSite", "Pay.razor")], files.Order());
    }

    [Fact]
    public void Find_ATestIdIsTheAttributesOwnValue_AndAPrefixStartsAWord()
    {
        Write("src/Shop.Web/Pages/Cart.razor", """
            <div id="cart" title="pay">Pay</div>
            <li data-testid="MyRow_@item.Id"></li>
            <li data-testid="Row_@item.Id"></li>
            """);
        var sources = new BddAppSources(_root);

        var pay = Assert.Single(sources.Find(new BddLocatorValue("pay", IsPrefix: false)));
        var row = Assert.Single(sources.Find(new BddLocatorValue("Row_", IsPrefix: true)));

        Assert.False(pay.IsAttribute);
        Assert.True(row is { Line: 3, IsAttribute: true });
    }

    [Fact]
    public void Find_TheSameIdInTwoApps_PutsTheOneNamedLikeThePageObjectsProjectFirst()
    {
        Write("src/Shop.Admin/Shop.Admin.csproj", "<Project />");
        Write("src/Shop.Admin/Pages/Cart.razor", """<button data-testid="Save">Save</button>""");
        Write("src/Shop.Store/Shop.Store.csproj", "<Project />");
        Write("src/Shop.Store/Pages/Cart.razor", """<button data-testid="Save">Save</button>""");
        var pageObject = Write("tests/Shop.Store.UITests/Pages/CartPage.cs", string.Empty);
        Write("tests/Shop.Store.UITests/Shop.Store.UITests.csproj", "<Project />");

        var hits = new BddAppSources(_root).Find(new BddLocatorValue("Save", IsPrefix: false), hint: new BddAppSourceHint(pageObject, null, null));

        Assert.Contains("Shop.Store", hits[0].File, StringComparison.Ordinal);
    }

    private string Write(string relative, string text)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }
}
