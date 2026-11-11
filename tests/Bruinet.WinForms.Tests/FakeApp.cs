using System.Diagnostics;

namespace Bruinet.WinForms.Tests;

/// <summary>
/// A mocked context and driver for the call-model tests: the app runs unless a test says otherwise,
/// and each named element answers whatever the test gives it.
/// </summary>
public sealed class FakeApp
{
    public FakeApp()
    {
        Context.Setup(c => c.Timeouts).Returns(new TimeoutSettings
        {
            DefaultWait = TimeSpan.FromMilliseconds(1000),
            PageLoad = TimeSpan.FromMilliseconds(1000),
            PollingInterval = TimeSpan.FromMilliseconds(1),
        });
        Context.Setup(c => c.Logger).Returns(Logger.Object);
        Context.Setup(c => c.Context).Returns(() => Context.Object);
        Context.Setup(c => c.Driver).Returns(() => Driver.Object);
        Context.Setup(c => c.NearMiss).Returns(Bruinet.Core.Calls.NearMissSettings.Default);
        Context.Setup(c => c.DefaultLocatorStrategy).Returns(LocatorStrategy.AutomationId);
        Context.Setup(c => c.ProbeReadiness()).Returns(() => Running
            ? ScopeReadiness.Ready("app")
            : throw new AppUnavailableException("the application process has exited."));
        Context.Setup(c => c.FindElements(It.IsAny<Locator>())).Returns([]);
        Given(nameof(TestPage), () => Element().Object);
    }

    public Mock<IWinFormsTestContext> Context { get; } = new();

    public Mock<IWinFormsDriver> Driver { get; } = new();

    public Mock<ITestLogger> Logger { get; } = new();

    /// <summary>Whether the app runs; it has exited when false.</summary>
    public bool Running { get; set; } = true;

    public TestPage Page() => new(Context.Object);

    /// <summary>Makes the window answer <paramref name="automationId"/> with whatever <paramref name="next"/> gives.</summary>
    public void Given(string automationId, Func<IWinFormsElement?> next)
        => Context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == automationId))).Returns(next);

    public static Mock<IWinFormsElement> Element(bool visible = true, bool enabled = true, string? text = null)
    {
        var element = new Mock<IWinFormsElement>();
        element.Setup(e => e.Visible).Returns(visible);
        element.Setup(e => e.Enabled).Returns(enabled);
        element.Setup(e => e.Text).Returns(text);
        element.Setup(e => e.InstanceKey).Returns(Guid.NewGuid().ToString("N"));
        element.Setup(e => e.FindElements(It.IsAny<Locator>())).Returns([]);
        return element;
    }

    public static long Timed(Action act)
    {
        var stopwatch = Stopwatch.StartNew();
        _ = Record.Exception(act);
        return stopwatch.ElapsedMilliseconds;
    }

    public void VerifyLogPairs(int expected)
    {
        Logger.Verify(l => l.Write(It.Is<LogEvent>(e => e.Kind == LogEventKind.Entry)), Times.Exactly(expected));
        Logger.Verify(l => l.Write(It.Is<LogEvent>(e => e.Kind == LogEventKind.Exit)), Times.Exactly(expected));
    }
}

/// <summary>A page over the fake app: loaded while its root (AutomationId <c>TestPage</c>) is shown.</summary>
public sealed class TestPage(IWinFormsTestContext context) : PageObjectBase<TestPage>(context)
{
    public Button<TestPage> Target => new(this, "Target");

    public Label<TestPage> Status => new(this, "Target");

    public TextBox<TestPage> Input => new(this, "Target");

    public PasswordBox<TestPage> Secret => new(this, "Target");

    public CheckBox<TestPage> Agree => new(this, "Target");

    public ComboBox<TestPage> Role => new(this, "Target");

    public ProgressBar<TestPage> Volume => new(this, "Target");

    public TreeNode<TestPage> Details => new(this, "Target");

    public TrackBar<TestPage> Level => new(this, "Target", minimum: 0, maximum: 100);

    public NumericUpDown<TestPage> Port => new(this, "Target", minimum: 1, maximum: 65535);

    public DateTimePicker<TestPage> Start => new(this, "Target");
}
