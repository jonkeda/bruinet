using System.Diagnostics;
using Bruinet.Core.Scopes;
using Bruinet.NativeAndroid.Controls.Buttons;
using Bruinet.NativeAndroid.Controls.Selection;
using Bruinet.NativeAndroid.Controls.Text;
using Bruinet.NativeAndroid.Controls.Toggle;

namespace Bruinet.NativeAndroid.Tests;

/// <summary>
/// A mocked context and driver for the call-model tests: the app is in front unless a test says
/// otherwise, nothing scrolls, and each named view answers whatever the test gives it.
/// </summary>
public sealed class FakeApp
{
    public FakeApp()
    {
        Context.Setup(c => c.Timeouts).Returns(new TimeoutSettings
        {
            DefaultWait = TimeSpan.FromMilliseconds(1000),
            PageLoad = TimeSpan.FromMilliseconds(1000),
            ElementFind = TimeSpan.FromMilliseconds(1000),
            PollingInterval = TimeSpan.FromMilliseconds(1),
        });
        Context.Setup(c => c.Logger).Returns(Logger.Object);
        Context.Setup(c => c.Context).Returns(() => Context.Object);
        Context.Setup(c => c.Driver).Returns(() => Driver.Object);
        Context.Setup(c => c.NearMiss).Returns(Bruinet.Core.Calls.NearMissSettings.Default);
        Context.Setup(c => c.DefaultLocatorStrategy).Returns(LocatorStrategy.Id);
        Context.Setup(c => c.ProbeReadiness()).Returns(() => InFront
            ? ScopeReadiness.Ready("app")
            : new ScopeReadiness("app", ScopeReadinessState.NotLoaded, "a system dialog covers the app"));
        Context.Setup(c => c.FindElements(It.IsAny<Locator>())).Returns([]);
    }

    public Mock<INativeAndroidTestContext> Context { get; } = new();

    public Mock<INativeAndroidDriver> Driver { get; } = new();

    public Mock<ITestLogger> Logger { get; } = new();

    /// <summary>Whether the app under test is in front; a system dialog covers it when false.</summary>
    public bool InFront { get; set; } = true;

    public TestPage Page() => new(Context.Object);

    /// <summary>Makes the screen answer <paramref name="id"/> with whatever <paramref name="next"/> gives.</summary>
    public void Given(string id, Func<INativeAndroidElement?> next)
        => Context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == id))).Returns(next);

    public static Mock<INativeAndroidElement> View(bool visible = true, bool enabled = true, string? text = null)
    {
        var element = new Mock<INativeAndroidElement>();
        element.Setup(e => e.Visible).Returns(visible);
        element.Setup(e => e.Enabled).Returns(enabled);
        element.Setup(e => e.Text).Returns(text);
        element.Setup(e => e.InstanceKey).Returns(Guid.NewGuid().ToString("N"));
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

/// <summary>A page over the fake app; the busy view is never there unless a test puts it there.</summary>
public sealed class TestPage(INativeAndroidTestContext context) : PageObjectBase<TestPage>(context)
{
    public Button<TestPage> Target => new(this, "Target");

    public TextView<TestPage> Status => new(this, "Target");

    public EditText<TestPage> Input => new(this, "Target");

    public CheckBox<TestPage> Agree => new(this, "Target");

    public RadioGroup<TestPage> Size => new(this, "Target");
}
