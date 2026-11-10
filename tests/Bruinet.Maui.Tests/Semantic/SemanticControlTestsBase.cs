namespace Bruinet.Maui.Tests.Semantic;

public abstract class SemanticControlTestsBase
{
    protected readonly Mock<IMauiTestContext> Context = new();
    protected readonly TestPage Page;

    protected SemanticControlTestsBase()
    {
        Context.Setup(c => c.Timeouts).Returns(new TimeoutSettings
        {
            DefaultWait = TimeSpan.FromMilliseconds(100),
            PageLoad = TimeSpan.FromMilliseconds(100),
            PollingInterval = TimeSpan.FromMilliseconds(1)
        });
        Context.Setup(c => c.DefaultLocatorStrategy).Returns(LocatorStrategy.AutomationId);

        var pageRoot = new Mock<IMauiElement>();
        pageRoot.Setup(e => e.Visible).Returns(true);
        pageRoot.Setup(e => e.TagName).Returns("Page");
        pageRoot.Setup(e => e.Rect).Returns(new System.Drawing.Rectangle(0, 0, 400, 800));
        pageRoot.Setup(e => e.TryFindElement(It.IsAny<Locator>()))
            .Returns((Locator locator) => Context.Object.TryFindElement(locator));
        pageRoot.Setup(e => e.FindElements(It.IsAny<Locator>()))
            .Returns((Locator locator) => Context.Object.FindElements(locator));
        Context.Setup(c => c.FindElements(It.Is<Locator>(l => l.Value == "TestPage")))
            .Returns([pageRoot.Object]);

        Page = new TestPage(Context.Object);
    }

    protected static Mock<IMauiElement> CreateElement(
        string automationId,
        int x,
        int y,
        int width,
        int height)
    {
        var element = new Mock<IMauiElement>();
        element.Setup(e => e.Visible).Returns(true);
        element.Setup(e => e.Enabled).Returns(true);
        element.Setup(e => e.Rect).Returns(new System.Drawing.Rectangle(x, y, width, height));
        element.Setup(e => e.GetAttribute("AutomationId")).Returns(automationId);
        return element;
    }

    protected static Mock<IMauiElement> CreateInvokableElement(
        string automationId,
        int x,
        int y,
        int width,
        int height,
        Action? onInvoke = null)
    {
        var element = CreateElement(automationId, x, y, width, height);
        element.Setup(e => e.Invoke()).Callback(() => onInvoke?.Invoke());
        return element;
    }

    protected static Mock<IMauiElement> CreateSelectableElement(
        string automationId,
        int x,
        int y,
        int width,
        int height,
        Action? onSelect = null)
    {
        var element = CreateElement(automationId, x, y, width, height);

        // The operation a control asks for. FlaUIMauiElement.Select runs the SelectionItem
        // pattern behind it; AppiumMauiElement taps.
        element.Setup(e => e.Select()).Callback(() => onSelect?.Invoke());
        return element;
    }

    /// <summary>A Windows-shaped toggle: checked state, a toggle, and a set-state operation.</summary>
    protected static Mock<IMauiElement> CreateToggleElement(
        string automationId,
        int x,
        int y,
        int width,
        int height,
        bool initialState)
    {
        var isChecked = initialState;
        var element = CreateElement(automationId, x, y, width, height);
        element.Setup(e => e.Checked).Returns(() => isChecked);
        element.Setup(e => e.SetChecked(It.IsAny<bool>())).Callback<bool>(value => isChecked = value);

        // FlaUIMauiElement.Toggle runs the Toggle pattern and throws if it is absent or refuses;
        // the mock stands in for the working case.
        element.Setup(e => e.Toggle()).Callback(() => isChecked = !isChecked);

        return element;
    }

    protected sealed class TestPage : PageObjectBase<TestPage>
    {
        public TestPage(IMauiTestContext context)
            : base(context)
        {
        }

        public override string Name => "TestPage";

        public override bool IsLoaded() => true;

        public Editor<TestPage> Notes => new(this, "Notes");

        public Button<TestPage> PromptOk => new(this, "PromptDialogView_OKButton");

        public Button<TestPage> NativeDialogDelete => new(this, "NativeDialog_Delete");

        public ContentDialog<TestPage> Dialog => new(this);

        public TabMenu<TestPage> Tabs => new(this);

        public CheckBox<TestPage> IncludeProblemReports => new(this, "IncludeProblemReports");

        public TestCollection TypedList => new(this, "TestList");
    }

    /// <summary>
    /// A collection whose rows are discovered by a repeating automation id.
    /// </summary>
    /// <remarks>
    /// The rows deliberately share one id ("ListItem") rather than carrying indexed ids.
    /// Item scoping, not the id, is what keeps them distinct.
    /// </remarks>
    protected sealed class TestCollection
        : CollectionObjectBase<TestPage, TestCollection, TestListItem>
    {
        public TestCollection(IMauiScope<TestPage> scope, string automationId)
            : base(scope,
                   automationId,
                   ItemStrategy.ByLocator(Locator.ByControlType("ListItem")),
                   (collection, itemRoot, index) => new TestListItem(collection, itemRoot, index))
        {
        }
    }

    protected sealed class TestListItem : ItemObjectBase<TestCollection, TestListItem>
    {
        public TestListItem(TestCollection collection, IMauiElement itemRoot, int index)
            : base(collection, itemRoot, index)
        {
        }
    }
}
