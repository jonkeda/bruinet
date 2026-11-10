using Bruinet.Core.Configuration;

namespace Bruinet.Stride.Tests;

/// <summary>
/// Stride on the call model (upgrade plan U6), over a fake game: an element is a handle by its
/// identity, a call acts once and confirms, and a game that is gone ends the call.
/// </summary>
public sealed class StrideCallModelTests
{
    private readonly FakeGame _game = new();

    [Fact]
    public void A_Click_IsOneAction_OnTheElementByItsIdentity()
    {
        _game.Add("Save", "Button");

        new TestPage(_game.Context).Save.Click();

        Assert.Equal(["Click #Save-key"], _game.Actions);
    }

    [Fact]
    public void An_Element_TheGameNoLongerHas_IsStale()
    {
        _game.Add("Save", "Button");
        var element = _game.Context.FindElement(Locator.ByAutomationId("Save"));
        _game.Remove("Save");

        Assert.Throws<StaleElementException>(() => element.Visible);
    }

    [Fact]
    public void SetText_WritesOnce_AndConfirms()
    {
        _game.Add("Name", "EditText");

        new TestPage(_game.Context).PlayerName.SetText("Alice");

        Assert.Equal(["SetText #Name-key Alice"], _game.Actions);
        Assert.Equal("Alice", _game.Text("Name"));
    }

    [Fact]
    public void Check_ClicksOnce_AndConfirms()
    {
        _game.Add("Mute", "ToggleButton", isChecked: false);

        new TestPage(_game.Context).Mute.Check();

        Assert.Equal(["Click #Mute-key"], _game.Actions);
    }

    [Fact]
    public void A_Page_IsBusy_WhileTheGameSaysSo()
    {
        _game.Busy = true;

        Assert.True(new TestPage(_game.Context).ProbeReadiness().IsBusy);
    }

    [Fact]
    public void A_Call_OnAGameThatIsGone_EndsWithAppUnavailable()
    {
        _game.Add("Save", "Button");
        _game.Gone = true;

        Assert.ThrowsAny<AppUnavailableException>(() => new TestPage(_game.Context).Save.Click(timeoutMs: 5000));
        Assert.Empty(_game.Actions);
    }
}

/// <summary>A page over the fake game, loaded while its root (<c>TestPage</c>) is shown.</summary>
public sealed class TestPage(IStrideTestContext context) : PageObjectBase<TestPage>(context)
{
    public Button<TestPage> Save => Button("Save");

    public EditText<TestPage> PlayerName => EditText("Name");

    public ToggleButton<TestPage> Mute => ToggleButton("Mute");
}

/// <summary>A game over a mocked driver: named elements whose state the actions change.</summary>
public sealed class FakeGame
{
    private readonly Dictionary<string, ElementState> _elements = new(StringComparer.Ordinal);

    public FakeGame()
    {
        Add(nameof(TestPage), "StackPanel");
        Driver.Setup(d => d.IsGameReady()).Returns(() => !Gone ? true : throw new AppUnavailableException("the game is gone."));
        Driver.Setup(d => d.IsGameBusy()).Returns(() => Busy);
        Driver.Setup(d => d.GetState(It.IsAny<string>())).Returns<string>(State);
        Driver.Setup(d => d.Find(It.IsAny<string>(), It.IsAny<string>())).Returns<string, string>((_, target) => State(target));
        Driver.Setup(d => d.Act(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object[]>()))
            .Callback<string, string, object[]>(Act);
        Context = new StrideTestContext(Driver.Object, new StrideTestContextOptions
        {
            StartupTimeout = TimeSpan.FromMilliseconds(1000),
            Timeouts = new TimeoutSettings
            {
                DefaultWait = TimeSpan.FromMilliseconds(1000),
                PageLoad = TimeSpan.FromMilliseconds(1000),
                ElementFind = TimeSpan.FromMilliseconds(1000),
                ElementState = TimeSpan.FromMilliseconds(1000),
                Animation = TimeSpan.FromMilliseconds(150),
                PollingInterval = TimeSpan.FromMilliseconds(1),
            },
        });
    }

    public Mock<IStrideDriver> Driver { get; } = new();

    public StrideTestContext Context { get; }

    public List<string> Actions { get; } = [];

    public bool Busy { get; set; }

    public bool Gone { get; set; }

    public void Add(string name, string type, bool? isChecked = null)
        => _elements[name] = new ElementState
        {
            Exists = true,
            IsVisible = true,
            IsEnabled = true,
            Name = name,
            AutomationId = name,
            ControlType = type,
            InstanceKey = $"{name}-key",
            IsChecked = isChecked,
        };

    public void Remove(string name) => _elements.Remove(name);

    public string? Text(string name) => _elements[name].Text;

    private ElementState State(string target)
    {
        var name = target.StartsWith(StrideElement.KeyPrefix, StringComparison.Ordinal)
            ? target[StrideElement.KeyPrefix.Length..].Replace("-key", string.Empty, StringComparison.Ordinal)
            : target;
        return _elements.TryGetValue(name, out var state) ? state : new ElementState { Exists = false };
    }

    private void Act(string method, string target, object[] args)
    {
        Actions.Add($"{method} {target}{(args.Length > 0 ? " " + string.Join(' ', args) : string.Empty)}");
        var state = State(target);
        if (method == "SetText")
            state.Text = (string)args[0];
        if (method == "Click" && state.IsChecked is { } isChecked)
            state.IsChecked = !isChecked;
    }
}
