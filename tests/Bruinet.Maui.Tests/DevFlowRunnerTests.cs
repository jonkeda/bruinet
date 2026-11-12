using Bruinet.Maui.Configuration;
using Bruinet.Maui.DevFlow;
using Bruinet.Maui.Enums;
using Microsoft.Maui.DevFlow.Driver;

namespace Bruinet.Maui.Tests;

/// <summary>
/// The DevFlow runner over a fake agent: which elements a lookup finds in the agent's tree, what an
/// action asks of the agent, and how the runner is chosen (.my/devflow-runner/design.md).
/// </summary>
public class DevFlowRunnerTests
{
    [Fact]
    public void ApplyEnvironment_WithDevFlowRunnerAndPort_ChoosesDevFlow()
    {
        var options = new MauiDriverOptions();
        var variables = new Dictionary<string, string?> { ["BRUINET_MAUI_RUNNER"] = "DevFlow", ["BRUINET_DEVFLOW_PORT"] = "10230" };

        options.ApplyEnvironment(name => variables.GetValueOrDefault(name));

        Assert.Equal(MauiRunner.DevFlow, options.Runner);
        Assert.Equal(10230, options.DevFlowPort);
    }

    [Fact]
    public void ApplyEnvironment_WithUnknownRunner_Throws()
    {
        var options = new MauiDriverOptions();

        Assert.Throws<InvalidOperationException>(() => options.ApplyEnvironment(name => name == "BRUINET_MAUI_RUNNER" ? "selenium" : null));
    }

    [Fact]
    public void FindElements_OnACoveredPage_FindsNothing()
    {
        var driver = Driver(new FakeAgent(Stack()));

        Assert.Empty(driver.FindElements(Locator.ByAutomationId("Open_Buttons")));
        Assert.Single(driver.FindElements(Locator.ByAutomationId("PrimaryButton")));
    }

    [Fact]
    public void FindElements_UnderAnInvisibleLayout_FindsNothing()
    {
        var driver = Driver(new FakeAgent(Stack()));

        Assert.Empty(driver.FindElements(Locator.ByAutomationId("HiddenLabel")));
    }

    [Fact]
    public void FindElements_ByText_MatchesTheWholeText()
    {
        var driver = Driver(new FakeAgent(Stack()));

        Assert.Single(driver.FindElements(Locator.ByText("Primary")));
        Assert.Empty(driver.FindElements(Locator.ByText("Prim")));
    }

    [Fact]
    public void FindElements_ByWindowsBackButtonId_FindsTheAgentsBackButton()
    {
        var driver = Driver(new FakeAgent(Stack()));

        var back = Assert.Single(driver.FindElements(Locator.ByAutomationId("NavigationViewBackButton")));
        Assert.Equal("BackButton", back.TagName);
    }

    [Fact]
    public void FindElements_ByAnXPath_HasNoRoute()
    {
        var driver = Driver(new FakeAgent(Stack()));

        Assert.Throws<RouteUnavailableException>(() => driver.FindElements(Locator.ByXPath("//Button")));
    }

    [Fact]
    public void FindElements_ChildrenPath_GivesTheShownDirectChildren()
    {
        var driver = Driver(new FakeAgent(Stack()));
        var layout = Assert.Single(driver.FindElements(Locator.ByAutomationId("ButtonsLayout")));

        Assert.Equal(["PrimaryButton", "DisabledButton", "NameEntry"], layout.FindElements(Locator.ByXPath("./*")).Select(e => e.AutomationId));
    }

    [Fact]
    public void Click_OnAnEnabledButton_TapsItInTheTreeItWasRead()
    {
        var agent = new FakeAgent(Stack());
        var button = Driver(agent).FindElements(Locator.ByAutomationId("PrimaryButton")).Single();

        button.Click();

        Assert.Equal(["tap PrimaryButton@7"], agent.Calls);
    }

    [Fact]
    public void Click_OnADisabledButton_IsNotReady()
    {
        var agent = new FakeAgent(Stack());
        var button = Driver(agent).FindElements(Locator.ByAutomationId("DisabledButton")).Single();

        var refusal = Assert.Throws<ElementNotReadyException>(button.Click);

        Assert.Contains("disabled", refusal.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(agent.Calls);
    }

    [Fact]
    public void SendKeys_AppendsAsTyping_AndSetValueReplaces()
    {
        var agent = new FakeAgent(Stack());
        var entry = Driver(agent).FindElements(Locator.ByAutomationId("NameEntry")).Single();

        entry.SendKeys("na");
        entry.SendKeys("Ada", TextInputMethod.SetValue);

        Assert.Equal(["fill NameEntry 'Adana'", "fill NameEntry 'Ada'"], agent.Calls);
    }

    [Fact]
    public void Element_GoneFromTheTree_IsNotVisibleAndStale()
    {
        var agent = new FakeAgent(Stack());
        var driver = Driver(agent);
        var button = driver.FindElements(Locator.ByAutomationId("PrimaryButton")).Single();

        agent.Windows = [Node("w", "Window", children: [Node("nav", "NavigationPage", children: [Node("PageHub", "HubPage")])])];
        driver.Refresh();

        Assert.False(button.Visible);
        Assert.Throws<StaleElementException>(() => button.Text);
    }

    [Fact]
    public void NavigationDepthAndRoute_ComeFromTheStack()
    {
        var driver = Driver(new FakeAgent(Stack()));

        Assert.Equal(2, driver.NavigationDepth());
        Assert.Equal("ButtonsTestPage", driver.CurrentRoute());
    }

    [Fact]
    public void Swipe_TakesTheLargerMovementsDirection()
    {
        var agent = new FakeAgent(Stack());
        var layout = Driver(agent).FindElements(Locator.ByAutomationId("ButtonsLayout")).Single();

        layout.Swipe(300, 100, 20, 140);

        Assert.Equal(["swipe ButtonsLayout left 280"], agent.Calls);
    }

    [Fact]
    public void Hover_HasNoRoute()
    {
        var button = Driver(new FakeAgent(Stack())).FindElements(Locator.ByAutomationId("PrimaryButton")).Single();

        Assert.Throws<RouteUnavailableException>(button.Hover);
    }

    [Fact]
    public void ListRows_AreInReadingOrder_WithoutAParkedContainer()
    {
        // The platform's container order, and a recycled container parked far above the rows.
        var driver = Driver(new FakeAgent(ListPage(("Parked", -18793), ("Row 3", 535), ("Row 1", 415), ("Row 2", 475))));

        var rows = driver.FindElements(Locator.ByControlType("ListItem"));

        Assert.Equal(["Row 1", "Row 2", "Row 3"], rows.Select(row => row.FindElements(Locator.ByControlType("text")).Single().Text));
    }

    [Fact]
    public void ListRowElement_WhenItsContainerShowsAnotherItem_IsStale()
    {
        var agent = new FakeAgent(ListPage(("Row 1", 415), ("Row 2", 475)));
        var driver = Driver(agent);
        var label = driver.FindElements(Locator.ByText("Row 1")).Single();

        // The same container, recycled for another item as the list scrolled.
        agent.Windows = ListPage(("Row 9", 415), ("Row 2", 475));
        driver.Refresh();

        Assert.False(label.Visible);
        Assert.Throws<StaleElementException>(() => label.Text);
    }

    [Fact]
    public void NestedScrollView_ScrolledToItsEnd_ShowsItsContentAtTheEnd()
    {
        // A ScrollView 120 high around 200 of content, inside the page's ScrollView: the agent would
        // spend an amount on the page's, so the inner one is moved by showing a child.
        var agent = new FakeAgent(NestedScroller());
        agent.Properties["Inner.Height"] = "120";
        agent.Properties["Inner.ContentSize.Height"] = "200";
        agent.Properties["Inner.ScrollY"] = "0";
        var inner = Driver(agent).FindElements(Locator.ByAutomationId("Inner")).Single();

        inner.SetVerticalScrollPercent(100);

        Assert.Equal(["scroll InnerContent End"], agent.Calls);
    }

    [Fact]
    public void OutermostScrollView_IsScrolledByTheDistance()
    {
        var agent = new FakeAgent(NestedScroller());
        agent.Properties["Outer.Height"] = "500";
        agent.Properties["Outer.ContentSize.Height"] = "900";
        agent.Properties["Outer.ScrollY"] = "100";
        var outer = Driver(agent).FindElements(Locator.ByAutomationId("Outer")).Single();

        outer.SetVerticalScrollPercent(50);

        Assert.Equal(["scroll Outer 100"], agent.Calls);
    }

    [Fact]
    public void ShellPage_UnderShellsInvisibleStructure_IsFoundAndEnabled_ALeftTabsPageIsNot()
    {
        var driver = Driver(new FakeAgent(ShellApp()));

        var button = Assert.Single(driver.FindElements(Locator.ByAutomationId("HomeButton")));
        Assert.True(button.Enabled);
        Assert.Empty(driver.FindElements(Locator.ByAutomationId("ControlsButton")));

        // The flyout item's template repeats its id; the agent's own FlyoutItem node stands for it.
        Assert.Equal(["FlyoutItem"], driver.FindElements(Locator.ByAutomationId("ShellMainItem")).Select(e => e.TagName));
    }

    [Fact]
    public void ShellChrome_FindsTheAgentsTabsAndFlyoutItems_UnderTheShell()
    {
        var driver = Driver(new FakeAgent(ShellApp()));
        var shell = driver.FindElements(Locator.ByAutomationId("AppShell")).Single();

        var host = Assert.Single(shell.FindElements(driver.ShellChrome.TabHost));
        Assert.Equal(["Home", "Controls"], host.FindElements(driver.ShellChrome.Tab).Select(tab => tab.Text));
        Assert.Equal(["Main"], host.FindElements(driver.ShellChrome.FlyoutItem).Select(item => item.Text));
    }

    [Fact]
    public void InvokeMenuItem_GoesThroughTheAppsBridge()
    {
        var agent = new FakeAgent(Stack());
        agent.Bridge["- InvokeMenuItem"] = new BridgeAnswer(true, true, 0, string.Empty, 0, string.Empty);

        Driver(agent).InvokeMenuItem("PageMenuFileNew");

        Assert.Contains("bridge - InvokeMenuItem PageMenuFileNew", agent.Calls);
    }

    [Fact]
    public void InvokeMenuItem_WithoutABridge_HasNoRoute()
    {
        var driver = Driver(new FakeAgent(Stack()));

        Assert.Throws<RouteUnavailableException>(() => driver.InvokeMenuItem("PageMenuFileNew"));
    }

    [Fact]
    public void ReadState_TakesTheAppsAnswerFirst()
    {
        var agent = new FakeAgent(Stack());
        agent.Bridge["NameEntry GetState"] = new BridgeAnswer(true, true, 0, "2\nOne\nTwo", 0, string.Empty);
        var entry = Driver(agent).FindElements(Locator.ByAutomationId("NameEntry")).Single();

        Assert.Equal("2\nOne\nTwo", entry.ReadState("Items"));
    }

    [Fact]
    public void ClearFocus_WithoutTheUnfocusVerb_HasNoRoute()
    {
        var entry = Driver(new FakeAgent(Stack())).FindElements(Locator.ByAutomationId("NameEntry")).Single();

        Assert.Throws<RouteUnavailableException>(entry.ClearFocus);
    }

    [Fact]
    public void MediaTransport_StandsInUnderWinUIsIds_ReadingTheElementsState()
    {
        var agent = new FakeAgent(MediaPage());
        agent.Properties["Clip.CurrentState"] = "Paused";
        agent.Properties["Clip.Position"] = "00:00:03";
        agent.Properties["Clip.Duration"] = "00:00:30";
        var driver = Driver(agent);

        Assert.Equal("Play", driver.FindElements(Locator.ByAutomationId("PlayPauseButton")).Single().Name);
        Assert.Equal("0:00:03", driver.FindElements(Locator.ByAutomationId("TimeElapsedElement")).Single().Name);
        Assert.Equal("0:00:27", driver.FindElements(Locator.ByAutomationId("TimeRemainingElement")).Single().Name);
        Assert.Equal(3, driver.FindElements(Locator.ByAutomationId("ProgressSlider")).Single().RangeValue);
    }

    [Fact]
    public void MediaPlayPause_PlaysThroughTheAppsBridge()
    {
        var agent = new FakeAgent(MediaPage());
        agent.Properties["Clip.CurrentState"] = "Paused";
        agent.Bridge["Clip Media"] = new BridgeAnswer(true, true, 0, string.Empty, 0, string.Empty);

        Driver(agent).FindElements(Locator.ByAutomationId("PlayPauseButton")).Single().Click();

        Assert.Contains("bridge Clip Media Play 0", agent.Calls);
    }

    private static DevFlowMauiDriver Driver(FakeAgent agent) => new(agent, MauiPlatform.Windows);

    // A page with one MediaElement, whose transport controls the agent's tree does not show.
    private static List<ElementInfo> MediaPage() =>
    [
        Node("w", "Window", visible: false, enabled: false, children:
        [
            Node("MediaPage", "ContentPage", children: [Placed(Node("Clip", "MediaElement"), 140, 120)]),
        ]),
    ];

    // A Shell as the agent shows it: its structure reported invisible and disabled, the Home tab's
    // page on screen, the Controls tab's page left (no height), and the agent's own chrome nodes.
    private static List<ElementInfo> ShellApp() =>
    [
        Node("w", "Window", visible: false, enabled: false, children:
        [
            Placed(Node("AppShell", "AppShell", children:
            [
                Shell("Main", "FlyoutItem", children:
                [
                    Shell("Home", "Tab", children:
                    [
                        Shell("", "ShellContent", children:
                        [
                            Placed(Node("HomePage", "HomePage", children: [Placed(Node("HomeButton", "Button", text: "Go"), 100)]), 80, 520),
                        ]),
                    ]),
                    Shell("Controls", "Tab", children:
                    [
                        Shell("", "ShellContent", children:
                        [
                            Placed(Node("ControlsPage", "ControlsPage", children: [Node("ControlsButton", "Button", text: "Go")]), 80, 0),
                        ]),
                    ]),
                    Node("ShellMainItem", "Grid", children: [Node("MainLabel", "Label", text: "Main")]),
                ]),
                Node("Tab_home", "Tab", text: "Home", automationId: "ShellHomeTab", synthetic: true),
                Node("Tab_controls", "Tab", text: "Controls", automationId: "ShellControlsTab", synthetic: true),
                Node("FlyoutItem_main", "FlyoutItem", text: "Main", automationId: "ShellMainItem", synthetic: true),
            ]), 0, 650),
        ]),
    ];

    private static ElementInfo Shell(string title, string type, List<ElementInfo> children)
        => Node($"{type}_{title}_s", type, text: title, visible: false, enabled: false, children: children);

    // The page's ScrollView around an inner one whose content (at window Y 300) holds two labels.
    private static List<ElementInfo> NestedScroller() =>
    [
        Node("w", "Window", visible: false, enabled: false, children:
        [
            Node("ScrollPage", "ContentPage", children:
            [
                Placed(Node("Outer", "ScrollView", children:
                [
                    Placed(Node("Inner", "ScrollView", children:
                    [
                        Placed(Node("InnerContent", "VerticalStackLayout", children:
                        [
                            Placed(Node("First", "Label", text: "First"), 300, 20),
                            Placed(Node("Last", "Label", text: "Last"), 480, 20),
                        ]), 300, 200),
                    ]), 300, 120),
                ]), 0, 500),
            ]),
        ]),
    ];

    // A page with one CollectionView whose rows (containers c0, c1, ...) each hold a label, at the given window Y.
    private static List<ElementInfo> ListPage(params (string Text, double Y)[] rows) =>
    [
        Node("w", "Window", visible: false, enabled: false, children:
        [
            Node("ListPage", "ContentPage", children:
            [
                Placed(Node("Products", "CollectionView", children:
                [
                    .. rows.Select((row, index) => Placed(
                        Node($"Row_c{index}", "Grid", children: [Placed(Node($"Label_c{index}", "Label", text: row.Text), row.Y)]),
                        row.Y)),
                ]), 415, 320),
            ]),
        ]),
    ];

    private static ElementInfo Placed(ElementInfo node, double y, double height = 60)
    {
        node.WindowBounds = new BoundsInfo { X = 20, Y = y, Width = 400, Height = height };
        return node;
    }

    // A NavigationPage with the hub under the Buttons page, as the sample app's tree is after one push.
    private static List<ElementInfo> Stack() =>
    [
        Node("w", "Window", visible: false, enabled: false, children:
        [
            Node("nav", "NavigationPage", children:
            [
                Node("NavBarTitle_nav", "NavBarTitle", text: "Buttons", synthetic: true),
                Node("BackButton_nav", "BackButton", automationId: "BackButton", synthetic: true),
                Node("PageHub", "HubPage", children: [Node("Open_Buttons", "Button", text: "Buttons")]),
                Node("ButtonsTestPage", "ButtonsPage", children:
                [
                    Node("ButtonsLayout", "VerticalStackLayout", children:
                    [
                        Node("PrimaryButton", "Button", text: "Primary"),
                        Node("DisabledButton", "Button", text: "Off", enabled: false),
                        Node("NameEntry", "Entry", text: "Ada"),
                        Node("HiddenLayout", "Grid", visible: false, children: [Node("HiddenLabel", "Label", text: "Secret")]),
                    ]),
                ]),
            ]),
        ]),
    ];

    private static ElementInfo Node(
        string id, string type, string? text = null, string? automationId = null, bool visible = true, bool enabled = true,
        bool synthetic = false, List<ElementInfo>? children = null)
        => new()
        {
            Id = id,
            Type = type,
            FullType = (synthetic ? "Microsoft.Maui.DevFlow.Agent.Core." : "Microsoft.Maui.Controls.") + type,
            AutomationId = automationId ?? (id.Contains('_', StringComparison.Ordinal) || id is "w" or "nav" ? null : id),
            Text = text,
            IsVisible = visible,
            IsEnabled = enabled,
            CaptureEpoch = 7,
            Children = children,
        };

    private sealed class FakeAgent(List<ElementInfo> windows) : IDevFlowAgent
    {
        public List<ElementInfo> Windows { get; set; } = windows;

        public List<string> Calls { get; } = [];

        public AgentStatus? Status() => new() { Running = true };

        public IReadOnlyList<ElementInfo> Tree(bool includeNative) => Windows;

        public IReadOnlyList<string> QueryCss(string selector) => [];

        public bool Tap(ElementInfo element) => Record($"tap {element.Id}@{element.CaptureEpoch}");

        public bool Fill(ElementInfo element, string text) => Record($"fill {element.Id} '{text}'");

        public bool Clear(ElementInfo element) => Record($"clear {element.Id}");

        public bool Focus(ElementInfo element) => Record($"focus {element.Id}");

        public bool Gesture(ElementInfo element, string type, string? direction = null, double? distance = null, int? durationMs = null)
            => Record($"{type} {element.Id} {direction} {distance}");

        public bool Scroll(ElementInfo? element, double deltaX, double deltaY, int? itemIndex = null, string? position = null)
            => Record($"scroll {element?.Id} {position ?? deltaY.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

        public bool Navigate(string route) => Record($"navigate {route}");

        public bool Back() => Record("back");

        public Dictionary<string, string> Properties { get; } = [];

        public string? GetProperty(string elementId, string name) => Properties.GetValueOrDefault($"{elementId}.{name}");

        public bool SetProperty(ElementInfo element, string name, string value) => Record($"set {element.Id}.{name}={value}");

        public byte[] Screenshot(string? elementId = null) => [];

        public void ReleaseLease()
        {
        }

        /// <summary>What the app's bridge answers, by "automationId verb" ("- verb" anywhere); none declares by default.</summary>
        public Dictionary<string, BridgeAnswer> Bridge { get; } = [];

        public BridgeAnswer BridgeInvoke(string? automationId, string verb, int arg1 = 0, int arg2 = 0)
            => Answer(automationId, verb, $"{arg1},{arg2}");

        public BridgeAnswer BridgeExchange(string? automationId, string verb, string argument = "")
            => Answer(automationId, verb, argument);

        public bool BridgeDeclares(string? automationId, string verb) => Bridge.ContainsKey($"{automationId ?? "-"} {verb}");

        public BridgeAnswer Media(string automationId, string action, double seconds = 0)
            => Answer(automationId, "Media", $"{action} {seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

        private BridgeAnswer Answer(string? automationId, string verb, string argument)
        {
            if (!Bridge.TryGetValue($"{automationId ?? "-"} {verb}", out var answer))
                return BridgeAnswer.None("no bridge");
            Calls.Add($"bridge {automationId ?? "-"} {verb} {argument}".TrimEnd());
            return answer;
        }

        public void Dispose()
        {
        }

        private bool Record(string call)
        {
            Calls.Add(call);
            return true;
        }
    }
}
