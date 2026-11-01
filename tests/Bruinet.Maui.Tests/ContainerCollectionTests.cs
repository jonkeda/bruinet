using Bruinet.Core.Scopes;
using Bruinet.Maui.Containers;

namespace Bruinet.Maui.Tests;

/// <summary>
/// Unit tests for ContainerObject and CollectionObject scoping and fluent behaviour.
/// Uses xUnit Assert per SPEC-017b design principles (never FluentAssertions).
/// </summary>
public class ContainerCollectionTests
{
    private readonly Mock<IMauiTestContext> _context;

    public ContainerCollectionTests()
    {
        _context = new Mock<IMauiTestContext>();
        _context.Setup(c => c.Timeouts).Returns(new TimeoutSettings
        {
            DefaultWait = 300,
            PageLoad = 300,
            PollingInterval = 20
        });
        _context.Setup(c => c.DefaultLocatorStrategy).Returns(LocatorStrategy.AutomationId);

        var pageRoot = new Mock<IMauiElement>();
        pageRoot.Setup(e => e.Visible).Returns(true);
        pageRoot.Setup(e => e.TagName).Returns("Page");
        pageRoot.Setup(e => e.Rect).Returns(new System.Drawing.Rectangle(0, 0, 400, 800));
        pageRoot.Setup(e => e.TryFindElement(It.IsAny<Locator>()))
            .Returns((Locator locator) => _context.Object.FindElement(locator));
        _context.Setup(c => c.FindElements(It.Is<Locator>(l => l.Value == "TestPage")))
            .Returns([pageRoot.Object]);
    }

    #region Container scoping

    [Fact]
    [Trait("Pattern", "Scoping")]
    public void Container_ResolvesChild_ThroughItsRoot()
    {
        var page = new TestPage(_context.Object);
        var root = SetupRootElement("Form");
        SetupChild(root, "FormButton");

        Assert.True(page.Form.FormButton.IsExists());

        // Resolved element-relative, not page-wide.
        root.Verify(e => e.TryFindElement(
            It.Is<Locator>(l => l.Value == "FormButton")), Times.AtLeastOnce);
    }

    /// <summary>
    /// A container must not fall back to the parent scope when a child is missing.
    /// </summary>
    [Fact]
    [Trait("Pattern", "NoParentFallback")]
    public void Container_DoesNotFallBackToParent()
    {
        var page = new TestPage(_context.Object);
        var root = SetupRootElement("Form");

        // The child is NOT under the container root...
        root.Setup(e => e.TryFindElement(It.Is<Locator>(l => l.Value == "Elsewhere")))
            .Returns((IMauiElement?)null);

        // ...but it does exist at page level.
        var pageWide = new Mock<IMauiElement>();
        pageWide.Setup(e => e.Visible).Returns(true);
        _context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Elsewhere")))
            .Returns(pageWide.Object);

        var control = new Label<FormContainer>(page.Form, "Elsewhere");

        Assert.False(control.IsExists());
    }

    [Fact]
    [Trait("Pattern", "NestedScoping")]
    public void NestedContainer_ResolvesThroughBothRoots()
    {
        var page = new TestPage(_context.Object);
        var outerRoot = SetupRootElement("Form");
        var innerRoot = SetupChild(outerRoot, "Options");
        SetupChild(innerRoot, "OptionsCheckBox");

        Assert.True(page.Form.Options.OptionsCheckBox.IsExists());
    }

    #endregion

    #region Fluent return type

    /// <summary>
    /// A control inside a container returns the container. This already worked before
    /// the new bases; the test guards against regressing it.
    /// </summary>
    [Fact]
    [Trait("Pattern", "FluentReturn")]
    public void ControlInsideContainer_ReturnsContainer()
    {
        var page = new TestPage(_context.Object);
        var root = SetupRootElement("Form");
        SetupChild(root, "FormButton");

        FormContainer result = page.Form.FormButton.Click();

        Assert.Same(page.Form, result);
    }

    /// <summary>
    /// A container's own member returns the container, not the page, so the fluent chain
    /// stays inside it.
    /// </summary>
    [Fact]
    [Trait("Pattern", "FluentReturn")]
    public void ContainerOwnAssertion_ReturnsContainer()
    {
        var page = new TestPage(_context.Object);
        SetupRootElement("Form");

        FormContainer result = page.Form.AssertVisible(true);

        Assert.Same(page.Form, result);
        Assert.IsNotType<TestPage>(result);
    }

    [Fact]
    [Trait("Property", "Parent")]
    public void Parent_ReturnsPage()
    {
        var page = new TestPage(_context.Object);
        SetupRootElement("Form");

        Assert.Same(page, page.Form.Parent);
    }

    [Fact]
    [Trait("Property", "Parent")]
    public void Parent_ChainsUpFromNestedContainer()
    {
        var page = new TestPage(_context.Object);
        var outerRoot = SetupRootElement("Form");
        SetupChild(outerRoot, "Options");

        Assert.Same(page.Form, page.Form.Options.Parent);
        Assert.Same(page, page.Form.Options.Parent.Parent);
    }

    #endregion

    #region Container readiness and caching

    [Fact]
    [Trait("Method", "IsReady")]
    public void IsReady_FalseWhenRootMissing()
    {
        var page = new TestPage(_context.Object);
        _context.Setup(c => c.TryFindElement(It.IsAny<Locator>())).Returns((IMauiElement?)null);
        _context.Setup(c => c.FindElement(It.IsAny<Locator>()))
            .Throws(new ElementNotFoundException("missing"));

        Assert.False(page.Form.IsReady());
    }

    /// <summary>
    /// Readiness is the parent, then the root, then ProbeContentReadiness (design 7.2).
    /// The default hook returns true, preserving the previous behaviour.
    /// </summary>
    [Fact]
    [Trait("Method", "ProbeContentReadiness")]
    public void ProbeContentReadiness_DefaultsToReady()
    {
        var page = new TestPage(_context.Object);
        SetupRootElement("Form");

        Assert.True(page.Form.WaitReady());
        Assert.True(page.Form.IsReady());
    }

    /// <summary>
    /// An overridden readiness hook gates IsReady even when the root exists.
    /// </summary>
    [Fact]
    [Trait("Method", "ProbeContentReadiness")]
    public void ProbeContentReadiness_OverrideGatesReadiness()
    {
        var page = new TestPage(_context.Object);
        SetupRootElement("Slow");

        Assert.False(page.Slow.IsReady());

        page.Slow.ContentIsReady = true;
        Assert.True(page.Slow.IsReady());
    }

    /// <summary>
    /// The root is cached, so repeated child lookups do not re-find it.
    /// </summary>
    [Fact]
    [Trait("Pattern", "RootCaching")]
    public void ContainerRoot_IsCachedAcrossLookups()
    {
        var page = new TestPage(_context.Object);
        var root = SetupRootElement("Form");
        SetupChild(root, "FormButton");
        var form = page.Form;

        _ = form.FormButton.IsExists();
        _ = form.FormButton.IsExists();
        _ = form.FormButton.IsExists();

        _context.Verify(c => c.FindElement(It.Is<Locator>(l => l.Value == "Form")), Times.Once);
    }

    [Fact]
    [Trait("Method", "InvalidateCache")]
    public void InvalidateCache_ForcesRootRefind()
    {
        var page = new TestPage(_context.Object);
        var root = SetupRootElement("Form");
        SetupChild(root, "FormButton");
        var form = page.Form;

        _ = form.FormButton.IsExists();
        form.InvalidateCache();
        _ = form.FormButton.IsExists();

        _context.Verify(c => c.FindElement(It.Is<Locator>(l => l.Value == "Form")), Times.Exactly(2));
    }

    /// <summary>
    /// A stale root is re-found rather than surfacing the exception.
    /// </summary>
    [Fact]
    [Trait("Pattern", "StaleRecovery")]
    public void StaleRoot_IsRecovered()
    {
        var page = new TestPage(_context.Object);

        var stale = new Mock<IMauiElement>();
        stale.Setup(e => e.InstanceKey).Throws(new StaleElementException());

        var fresh = new Mock<IMauiElement>();
        fresh.Setup(e => e.Visible).Returns(true);
        fresh.Setup(e => e.TagName).Returns("Grid");
        SetupChild(fresh, "FormButton");

        _context.SetupSequence(c => c.FindElement(It.Is<Locator>(l => l.Value == "Form")))
            .Returns(stale.Object)
            .Returns(fresh.Object);

        var form = page.Form;
        _ = form.IsExists();

        Assert.True(form.FormButton.IsExists());
    }

    #endregion

    #region Collection item scoping

    /// <summary>
    /// The core requirement: each row resolves its controls within its own root, so the
    /// same AutomationId on every row still yields distinct values.
    /// </summary>
    [Fact]
    [Trait("Pattern", "ItemScoping")]
    public void Rows_WithRepeatingIds_ResolveIndependently()
    {
        var page = new TestPage(_context.Object);
        SetupCollection("Rows", ("Alpha", "1.00"), ("Beta", "2.00"));

        Assert.Equal("Alpha", page.Rows.Item(0).Name.GetText());
        Assert.Equal("Beta", page.Rows.Item(1).Name.GetText());
    }

    [Fact]
    [Trait("Pattern", "ItemScoping")]
    public void Row_ResolvesThroughItsOwnRoot_NotThePage()
    {
        var page = new TestPage(_context.Object);
        var roots = SetupCollection("Rows", ("Alpha", "1.00"));

        _ = page.Rows.Item(0).Name.GetText();

        roots[0].Verify(e => e.TryFindElement(
            It.Is<Locator>(l => l.Value == "RowName")), Times.AtLeastOnce);
        _context.Verify(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "RowName")), Times.Never);
        _context.Verify(c => c.FindElement(It.Is<Locator>(l => l.Value == "RowName")), Times.Never);
    }

    [Fact]
    [Trait("Pattern", "ItemScoping")]
    public void Row_MissingChild_IsNotSweptFor_SoAnotherRowsChildCannotAnswer()
    {
        var page = new TestPage(_context.Object);
        var roots = SetupCollection("Rows", ("Alpha", "1.00"));
        roots[0].Setup(e => e.TryFindElement(It.Is<Locator>(l => l.Value == "RowDue")))
            .Returns((IMauiElement?)null);

        // As Android answers a sweep: it walks the whole list and finds the next row's label.
        var app = new Mock<IMauiElement>();
        app.Setup(a => a.TryFindByScrolling(It.IsAny<Locator>()))
            .Returns(Mock.Of<IMauiElement>(e => e.Visible == true));
        _context.Setup(c => c.AppElement).Returns(app.Object);

        Assert.False(page.Rows.Item(0).Due.IsExists());
        app.Verify(a => a.TryFindByScrolling(It.IsAny<Locator>()), Times.Never);
    }

    [Fact]
    [Trait("Property", "Index")]
    public void Row_TracksItsIndex()
    {
        var page = new TestPage(_context.Object);
        SetupCollection("Rows", ("Alpha", "1.00"), ("Beta", "2.00"));

        Assert.Equal(0, page.Rows.Item(0).Index);
        Assert.Equal(1, page.Rows.Item(1).Index);
    }

    #endregion

    #region Collection counting and retrieval

    /// <summary>
    /// Count comes from one enumeration, not N sequential probes.
    /// </summary>
    [Fact]
    [Trait("Method", "GetItemCount")]
    public void GetItemCount_EnumeratesInOneCall()
    {
        var page = new TestPage(_context.Object);
        SetupCollection("Rows", ("A", "1"), ("B", "2"), ("C", "3"));

        Assert.Equal(3, page.Rows.GetItemCount());
    }

    /// <summary>
    /// The old List capped at 100 and silently truncated. This one does not.
    /// </summary>
    [Fact]
    [Trait("Method", "GetItemCount")]
    public void GetItemCount_NotCappedAt100()
    {
        var page = new TestPage(_context.Object);
        var rows = Enumerable.Range(0, 150).Select(i => ($"Item{i}", "1")).ToArray();
        SetupCollection("Rows", rows);

        Assert.Equal(150, page.Rows.GetItemCount());
    }

    [Fact]
    [Trait("Method", "Indexer")]
    public void Indexer_AndItem_AreEquivalent()
    {
        var page = new TestPage(_context.Object);
        SetupCollection("Rows", ("Alpha", "1"), ("Beta", "2"));

        Assert.Equal(page.Rows.Item(1).Index, page.Rows[1].Index);
        Assert.Equal(page.Rows.Item(1).Name.GetText(), page.Rows[1].Name.GetText());
    }

    [Fact]
    [Trait("Method", "TryItem")]
    public void TryItem_OutOfRange_ReturnsNull()
    {
        var page = new TestPage(_context.Object);
        SetupCollection("Rows", ("Alpha", "1"));

        Assert.Null(page.Rows.TryItem(5));
    }

    [Fact]
    [Trait("Method", "Item")]
    public void Item_OutOfRange_Throws()
    {
        var page = new TestPage(_context.Object);
        SetupCollection("Rows", ("Alpha", "1"));

        Assert.Throws<ElementNotFoundException>(() => page.Rows.Item(5));
    }

    [Fact]
    [Trait("Method", "Item")]
    public void Item_NegativeIndex_Throws()
    {
        var page = new TestPage(_context.Object);
        SetupCollection("Rows", ("Alpha", "1"));

        Assert.Throws<ArgumentOutOfRangeException>(() => page.Rows.Item(-1));
    }

    [Fact]
    [Trait("Method", "IsEmpty")]
    public void IsEmpty_TrueWhenNoItems()
    {
        var page = new TestPage(_context.Object);
        SetupCollection("Rows");

        Assert.True(page.Rows.IsEmpty());
        Assert.Equal(0, page.Rows.GetItemCount());
    }

    #endregion

    #region Items enumeration

    /// <summary>
    /// Design 8.2: Items is lazy, so First stops at the match.
    /// </summary>
    [Fact]
    [Trait("Property", "Items")]
    public void Items_IsLazy_StopsAtFirstMatch()
    {
        var page = new TestPage(_context.Object);
        SetupCollection("Rows", ("Alpha", "1"), ("Beta", "2"), ("Gamma", "3"));

        var first = page.Rows.Items.First(r => r.Name.GetText() == "Alpha");

        Assert.Equal(0, first.Index);
    }

    [Fact]
    [Trait("Method", "ToList")]
    public void ToList_MaterializesEveryRow()
    {
        var page = new TestPage(_context.Object);
        SetupCollection("Rows", ("Alpha", "1"), ("Beta", "2"));

        Assert.Equal(2, page.Rows.ToList().Count);
    }

    [Fact]
    [Trait("Property", "Items")]
    public void Items_AreOrderedByIndex()
    {
        var page = new TestPage(_context.Object);
        SetupCollection("Rows", ("A", "1"), ("B", "2"), ("C", "3"));

        var rows = page.Rows.ToList();
        for (var i = 0; i < rows.Count; i++)
        {
            Assert.Equal(i, rows[i].Index);
        }
    }

    #endregion

    #region Collection search

    [Fact]
    [Trait("Method", "FindItem")]
    public void FindItem_ReturnsMatchingRow()
    {
        var page = new TestPage(_context.Object);
        SetupCollection("Rows", ("Alpha", "1"), ("Beta", "2"));

        var row = page.Rows.FindItem(r => r.Name.GetText() == "Beta");

        Assert.NotNull(row);
        Assert.Equal(1, row!.Index);
    }

    [Fact]
    [Trait("Method", "FindItem")]
    public void FindItem_ReturnsNullWhenNoMatch()
    {
        var page = new TestPage(_context.Object);
        SetupCollection("Rows", ("Alpha", "1"));

        Assert.Null(page.Rows.FindItem(r => r.Name.GetText() == "Zeta"));
    }

    [Fact]
    [Trait("Method", "ItemWhere")]
    public void ItemWhere_ThrowsWhenNoMatch()
    {
        var page = new TestPage(_context.Object);
        SetupCollection("Rows", ("Alpha", "1"));

        Assert.Throws<ElementNotFoundException>(
            () => page.Rows.ItemWhere(r => r.Name.GetText() == "Zeta"));
    }

    #endregion

    #region Collection as a scope

    /// <summary>
    /// A collection is a container, so it resolves its own non-item controls.
    /// </summary>
    [Fact]
    [Trait("Pattern", "CollectionIsScope")]
    public void Collection_ResolvesItsOwnControls()
    {
        var page = new TestPage(_context.Object);
        var root = SetupRootElement("Rows");
        root.Setup(e => e.FindElements(It.IsAny<Locator>()))
            .Returns(Array.Empty<IMauiElement>());
        SetupChild(root, "RowsEmptyLabel");

        Assert.True(page.Rows.EmptyLabel.IsExists());
    }

    [Fact]
    [Trait("Pattern", "FluentReturn")]
    public void CollectionAssertion_ReturnsCollection()
    {
        var page = new TestPage(_context.Object);
        SetupCollection("Rows", ("Alpha", "1"));

        RowCollection result = page.Rows.AssertItemCount(1);

        Assert.Same(page.Rows, result);
    }

    [Fact]
    [Trait("Pattern", "FluentReturn")]
    public void RowAction_ReturnsRow_ParentReturnsCollection()
    {
        var page = new TestPage(_context.Object);
        SetupCollection("Rows", ("Alpha", "1"));
        var row = page.Rows.Item(0);

        RowContainer afterClick = row.Delete.Click();

        Assert.Same(row, afterClick);
        Assert.Same(page.Rows, afterClick.Parent);
        Assert.Same(page, afterClick.Parent.Parent);
    }

    #endregion

    #region Item keys

    /// <summary>
    /// An id match anywhere in the collection beats a caption match, rather than the first
    /// item that matches either way winning.
    /// </summary>
    [Fact]
    [Trait("Pattern", "ItemKey")]
    public void ItemKey_PrefersAutomationId_AcrossTheWholeCollection()
    {
        var page = new TestPage(_context.Object);
        SetupKeyedCollection("Rows",
            (Id: "Second", Text: "First", TagName: "Button"),
            (Id: "First", Text: "Second", TagName: "Button"));

        // Item 0 answers to "First" by caption; item 1 answers by id. The id wins.
        Assert.Equal(1, page.Rows["First"].Index);
    }

    [Fact]
    [Trait("Pattern", "ItemKey")]
    public void ItemKey_FallsBackToTheCaption_WhenNoIdMatches()
    {
        var page = new TestPage(_context.Object);
        SetupKeyedCollection("Rows",
            (Id: "ToolbarSaveButton", Text: "Save", TagName: "Button"),
            (Id: "ToolbarDeleteButton", Text: "Delete", TagName: "Button"));

        Assert.Equal(1, page.Rows["Delete"].Index);
        Assert.Equal(0, page.Rows["ToolbarSaveButton"].Index);
    }

    /// <summary>
    /// Captions are compared leniently because the platform renders them; ids exactly
    /// because the app author writes them.
    /// </summary>
    [Fact]
    [Trait("Pattern", "ItemKey")]
    public void ItemKey_ComparesCaptionsLoosely_AndIdsExactly()
    {
        var page = new TestPage(_context.Object);
        SetupKeyedCollection("Rows", (Id: "SaveButton", Text: "  SAVE  ", TagName: "Button"));

        Assert.Equal(0, page.Rows["Save"].Index);
        Assert.Throws<ElementNotFoundException>(() => page.Rows["savebutton"]);
    }

    [Fact]
    [Trait("Pattern", "ItemKey")]
    public void ItemKey_ByLocator_SelectsTheStrategy()
    {
        var page = new TestPage(_context.Object);
        SetupKeyedCollection("Rows",
            (Id: "Delete", Text: "Save", TagName: "Button"),
            (Id: "Save", Text: "Delete", TagName: "Button"));

        Assert.Equal(0, page.Rows[Locator.ByText("Save")].Index);
        Assert.Equal(1, page.Rows[Locator.ByAutomationId("Save")].Index);
    }

    /// <summary>
    /// A control type is matched on the last segment of the platform's own name, so one key
    /// works on both platforms wherever the two agree on the name.
    /// </summary>
    [Fact]
    [Trait("Pattern", "ItemKey")]
    public void ItemKey_ByControlType_MatchesThePlatformTypeName()
    {
        var page = new TestPage(_context.Object);
        SetupKeyedCollection("Rows",
            (Id: "Title", Text: "Heading", TagName: "android.widget.TextView"),
            (Id: "Save", Text: "Save", TagName: "android.widget.Button"));

        Assert.Equal(1, page.Rows[Locator.ByControlType("Button")].Index);
    }

    /// <summary>
    /// The named forms select the same item as the locator they stand for.
    /// </summary>
    [Fact]
    [Trait("Pattern", "ItemKey")]
    public void ItemKey_NamedSelectors_MatchTheirLocatorForm()
    {
        var page = new TestPage(_context.Object);
        SetupKeyedCollection("Rows",
            (Id: "Delete", Text: "Save", TagName: "android.widget.TextView"),
            (Id: "Save", Text: "Delete", TagName: "android.widget.Button"));

        Assert.Equal(1, page.Rows.ItemByAutomationId("Save").Index);
        Assert.Equal(0, page.Rows.ItemByText("Save").Index);
        Assert.Equal(1, page.Rows.ItemByControlType("Button").Index);

        Assert.Null(page.Rows.TryItemByAutomationId("Publish"));
        Assert.Null(page.Rows.TryItemByText("Publish"));
        Assert.Null(page.Rows.TryItemByControlType("Slider"));

        Assert.Throws<ElementNotFoundException>(() => page.Rows.ItemByText("Publish"));
    }

    [Fact]
    [Trait("Pattern", "ItemKey")]
    public void ItemKey_Missing_Throws()
    {
        var page = new TestPage(_context.Object);
        SetupKeyedCollection("Rows", (Id: "Save", Text: "Save", TagName: "Button"));

        Assert.Null(page.Rows.TryItem("Publish"));
        Assert.Throws<ElementNotFoundException>(() => page.Rows["Publish"]);
    }

    /// <summary>
    /// A keyed item carries the index it was found at, so it can re-resolve itself the same
    /// way an indexed one does.
    /// </summary>
    [Fact]
    [Trait("Pattern", "ItemKey")]
    public void ItemKey_ItemIsScopedToItsOwnRow()
    {
        var page = new TestPage(_context.Object);
        var rows = SetupKeyedCollection("Rows",
            (Id: "First", Text: "First", TagName: "Grid"),
            (Id: "Second", Text: "Second", TagName: "Grid"));
        SetupChild(rows[1], "RowName", "Widget");

        var row = page.Rows["Second"];

        Assert.Equal(1, row.Index);
        Assert.Equal("Widget", row.Name.GetText());
    }

    #endregion

    #region Mock helpers

    /// <summary>Creates a page-level element and registers it with the context.</summary>
    private Mock<IMauiElement> SetupRootElement(string automationId)
    {
        var element = new Mock<IMauiElement>();
        element.Setup(e => e.Visible).Returns(true);
        element.Setup(e => e.Enabled).Returns(true);
        element.Setup(e => e.TagName).Returns("Grid");
        element.Setup(e => e.Rect).Returns(new System.Drawing.Rectangle(0, 0, 300, 200));

        _context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == automationId)))
            .Returns(element.Object);
        _context.Setup(c => c.FindElement(It.Is<Locator>(l => l.Value == automationId)))
            .Returns(element.Object);

        return element;
    }

    /// <summary>Attaches a child element beneath a parent element.</summary>
    private static Mock<IMauiElement> SetupChild(
        Mock<IMauiElement> parent, string automationId, string? text = null)
    {
        var child = new Mock<IMauiElement>();
        child.Setup(e => e.Visible).Returns(true);
        child.Setup(e => e.Enabled).Returns(true);
        child.Setup(e => e.TagName).Returns("Element");
        child.Setup(e => e.Rect).Returns(new System.Drawing.Rectangle(0, 0, 100, 30));
        if (text != null)
        {
            child.Setup(e => e.Text).Returns(text);
        }

        parent.Setup(e => e.TryFindElement(
                It.Is<Locator>(l => l.Value == automationId)))
            .Returns(child.Object);
        parent.Setup(e => e.FindElements(
                It.Is<Locator>(l => l.Value == automationId)))
            .Returns(new[] { child.Object });

        return child;
    }

    /// <summary>
    /// Builds a collection whose row roots carry an id, a caption and a type of their own -
    /// what item-key matching reads.
    /// </summary>
    private List<Mock<IMauiElement>> SetupKeyedCollection(
        string collectionId, params (string Id, string Text, string TagName)[] rows)
    {
        var root = SetupRootElement(collectionId);
        var rowMocks = new List<Mock<IMauiElement>>();

        foreach (var (id, text, tagName) in rows)
        {
            var rowRoot = new Mock<IMauiElement>();
            rowRoot.Setup(e => e.Visible).Returns(true);
            rowRoot.Setup(e => e.Enabled).Returns(true);
            rowRoot.Setup(e => e.AutomationId).Returns(id);
            rowRoot.Setup(e => e.Text).Returns(text);
            rowRoot.Setup(e => e.TagName).Returns(tagName);
            rowRoot.Setup(e => e.Rect).Returns(new System.Drawing.Rectangle(0, 0, 300, 40));

            rowMocks.Add(rowRoot);
        }

        root.Setup(e => e.FindElements(
                It.Is<Locator>(l => l.Value == "RowRoot")))
            .Returns(rowMocks.Select(m => m.Object).ToArray());

        return rowMocks;
    }

    /// <summary>
    /// Builds a collection root whose "RowRoot" lookup returns one root per row, each
    /// carrying RowName / RowPrice / RowDeleteButton children. Every row deliberately
    /// uses the SAME child AutomationIds.
    /// </summary>
    private List<Mock<IMauiElement>> SetupCollection(
        string collectionId, params (string Name, string Price)[] rows)
    {
        var root = SetupRootElement(collectionId);
        var rowMocks = new List<Mock<IMauiElement>>();

        foreach (var (name, price) in rows)
        {
            var rowRoot = new Mock<IMauiElement>();
            rowRoot.Setup(e => e.Visible).Returns(true);
            rowRoot.Setup(e => e.Enabled).Returns(true);
            rowRoot.Setup(e => e.TagName).Returns("Grid");
            rowRoot.Setup(e => e.Rect).Returns(new System.Drawing.Rectangle(0, 0, 300, 40));

            SetupChild(rowRoot, "RowName", name);
            SetupChild(rowRoot, "RowPrice", price);
            SetupChild(rowRoot, "RowDeleteButton");

            rowMocks.Add(rowRoot);
        }

        root.Setup(e => e.FindElements(
                It.Is<Locator>(l => l.Value == "RowRoot")))
            .Returns(rowMocks.Select(m => m.Object).ToArray());

        return rowMocks;
    }

    #endregion

    #region Selection per platform

    [Fact]
    [Trait("Pattern", "Selection")]
    public void SelectItem_OnAndroid_TapsTheRow_WithoutAskingForAListItem()
    {
        _context.Setup(c => c.Platform).Returns(Bruinet.Maui.Enums.MauiPlatform.Android);
        var page = new TestPage(_context.Object);
        var rows = SetupCollection("Rows", ("Alpha", "1.00"), ("Beta", "2.00"));

        // As the Appium driver answers: ListItem is a UI Automation control type with no
        // UiAutomator2 counterpart, and asking for it throws.
        var root = Mock.Get(_context.Object.FindElement(Locator.ByAutomationId("Rows")));
        root.Setup(e => e.FindElements(It.Is<Locator>(l => l.Value == "ListItem")))
            .Throws(new ArgumentOutOfRangeException("controlType", "Control type 'ListItem' is not supported on Android."));

        page.Rows.SelectItem(1);

        rows[1].Verify(row => row.Select(), Times.Once);
        rows[0].Verify(row => row.Select(), Times.Never);
    }

    [Fact]
    [Trait("Pattern", "Selection")]
    public void SelectItem_WhereRowsAreActivatedThroughTheirContainer_SelectsTheListItemAroundTheRow()
    {
        // As the FlaUI driver answers: UI Automation selects the ListItem that holds a row.
        var driver = new Mock<IMauiDriver>();
        driver.Setup(d => d.ActivatesRowsThroughContainers).Returns(true);
        _context.Setup(c => c.Driver).Returns(driver.Object);
        var page = new TestPage(_context.Object);
        var rows = SetupCollection("Rows", ("Alpha", "1.00"), ("Beta", "2.00"));
        var listItem = new Mock<IMauiElement>();
        listItem.Setup(e => e.Visible).Returns(true);
        listItem.Setup(e => e.Rect).Returns(new System.Drawing.Rectangle(0, 0, 300, 40));
        var root = Mock.Get(_context.Object.FindElement(Locator.ByAutomationId("Rows")));
        root.Setup(e => e.FindElements(It.Is<Locator>(l => l.Value == "ListItem"))).Returns([listItem.Object]);

        page.Rows.SelectItem(1);

        listItem.Verify(item => item.Select(), Times.Once);
        rows[1].Verify(row => row.Select(), Times.Never);
    }

    #endregion

    #region Test page objects

    private class TestPage : PageObjectBase<TestPage>
    {
        public TestPage(IMauiTestContext context) : base(context)
        {
            Form = new FormContainer(this, "Form");
            Rows = new RowCollection(this, "Rows");
            Slow = new SlowContainer(this, "Slow");
        }

        public override string Name => "TestPage";
        public override bool IsLoaded() => true;

        public FormContainer Form { get; }
        public RowCollection Rows { get; }
        public SlowContainer Slow { get; }
    }

    private class FormContainer : ContainerObjectBase<TestPage, FormContainer>
    {
        public FormContainer(IMauiScope<TestPage> scope, string automationId)
            : base(scope, automationId) { }

        public Button<FormContainer> FormButton => new(this, "FormButton");
        public OptionsContainer Options => new(this, "Options");
    }

    private class OptionsContainer : ContainerObjectBase<FormContainer, OptionsContainer>
    {
        public OptionsContainer(IMauiScope<FormContainer> scope, string automationId)
            : base(scope, automationId) { }

        public CheckBox<OptionsContainer> OptionsCheckBox => new(this, "OptionsCheckBox");
    }

    /// <summary>A container whose content loads asynchronously.</summary>
    private class SlowContainer : ContainerObjectBase<TestPage, SlowContainer>
    {
        public SlowContainer(IMauiScope<TestPage> scope, string automationId)
            : base(scope, automationId) { }

        public bool ContentIsReady { get; set; }

        protected override ScopeReadiness ProbeContentReadiness(IMauiElement root)
            => ContentIsReady ? ContentReady() : ContentNotReady("still loading");
    }

    private class RowCollection : CollectionObjectBase<TestPage, RowCollection, RowContainer>
    {
        public RowCollection(IMauiScope<TestPage> scope, string automationId)
            : base(scope,
                   automationId,
                   ItemStrategy.ByAutomationId("RowRoot"),
                   (collection, itemRoot, index) => new RowContainer(collection, itemRoot, index))
        { }

        public Label<RowCollection> EmptyLabel => new(this, "RowsEmptyLabel");
    }

    private class RowContainer : ItemObjectBase<RowCollection, RowContainer>
    {
        public RowContainer(RowCollection collection, IMauiElement itemRoot, int index)
            : base(collection, itemRoot, index) { }

        public Label<RowContainer> Name => new(this, "RowName");
        public Label<RowContainer> Price => new(this, "RowPrice");
        public Button<RowContainer> Delete => new(this, "RowDeleteButton");
        public Label<RowContainer> Due => new(this, "RowDue");
    }

    #endregion
}
