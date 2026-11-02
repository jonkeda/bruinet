namespace Bruinet.Blazor.Tests.Controls;

// Every Bruinet.Blazor control made from a Locator, not a selector: the control finds its element by that locator.
public class LocatorConstructorTests
{
    private static readonly Locator Target = Locator.ByCss("[data-testid='target']");

    public static TheoryData<string> Controls =>
    [
        "audio", "button", "date", "iframe", "image", "link", "list", "nav", "progress", "range", "table", "textarea", "time", "video",
    ];

    [Theory]
    [MemberData(nameof(Controls))]
    public void AControlMadeFromALocator_FindsItsElementByIt(string kind)
    {
        var context = MockHtmlFactory.CreateMockContext();
        var element = MockHtmlFactory.CreateMockElement();
        context.Setup(c => c.TryFindElement(Target)).Returns(element.Object);
        context.Setup(c => c.FindElement(Target)).Returns(element.Object);
        var page = new TestPage(context.Object);

        Assert.True(Make(page, kind).IsExists());
        context.Verify(c => c.TryFindElement(Target), Times.AtLeastOnce);
    }

    private static IControlObject<TestPage> Make(TestPage page, string kind) => kind switch
    {
        "audio" => new AudioControl<TestPage>(page, Target),
        "button" => new ButtonControl<TestPage>(page, Target),
        "date" => new DateInputControl<TestPage>(page, Target),
        "iframe" => new IFrameControl<TestPage>(page, Target),
        "image" => new ImageControl<TestPage>(page, Target),
        "link" => new LinkControl<TestPage>(page, Target),
        "list" => new ListControl<TestPage>(page, Target),
        "nav" => new NavMenuControl<TestPage>(page, Target),
        "progress" => new ProgressControl<TestPage>(page, Target),
        "range" => new RangeInputControl<TestPage>(page, Target),
        "table" => new TableControl<TestPage>(page, Target),
        "textarea" => new TextAreaControl<TestPage>(page, Target),
        "time" => new TimeInputControl<TestPage>(page, Target),
        "video" => new VideoControl<TestPage>(page, Target),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private sealed class TestPage : BlazorPageObjectBase<TestPage>
    {
        public TestPage(IHtmlTestContext context) : base(context) { }
    }
}
