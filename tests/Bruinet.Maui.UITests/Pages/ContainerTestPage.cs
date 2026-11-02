using Bruinet.Core.Utilities;
using Bruinet.Maui.Containers;

namespace Bruinet.Maui.UITests.Pages;

/// <summary>
/// Page object for the container module test view.
/// </summary>
/// <remarks>
/// <para>
/// Only the containers that are addressable on Windows get typed container objects.
/// <c>Frame</c>, <c>SwipeView</c>, and <c>RefreshView</c> are declared in the markup for
/// the planned Android/iOS phase but expose no <c>AutomationId</c> here, so they are
/// reached through <see cref="TryFindByAutomationId"/> and reported by the probe rather
/// than modelled as containers that would never resolve.
/// </para>
/// <para>
/// <b>Their gestures are reachable even so.</b> <c>SwipeView</c> and <c>RefreshView</c> declare
/// verbs for the Bruinet UI Automation bridge, which publishes a separate element carrying them
/// - so a test drives them by <c>AutomationId</c> through the driver without either control
/// being addressable. See <c>Tests/Gestures</c>.
/// </para>
/// </remarks>
public class ContainerTestPage : PageObjectBase<ContainerTestPage>
{
    public ContainerTestPage(IMauiTestContext context)
        : base(context)
    {
        TestGrid = new Grid<ContainerTestPage>(this, "TestGrid");
        TestBorder = new Border<ContainerTestPage>(this, "TestBorder");
        TestContentView = new ContentView<ContainerTestPage>(this, "TestContentView");
        TestScrollView = new ScrollView<ContainerTestPage>(this, "TestScrollView");
    }

    /// <inheritdoc />
    public override string Name => "ContainerPage";


    #region Observed state

    /// <summary>The page title label.</summary>
    public Label<ContainerTestPage> PageTitle => new(this, "ContainerPageTitle");

    /// <summary>The most recent container action.</summary>
    public Label<ContainerTestPage> Status => new(this, "ContainerStatusLabel");

    /// <summary>Reflects how many refreshes have completed.</summary>
    public Label<ContainerTestPage> RefreshText => new(this, "RefreshContentLabel");

    /// <summary>Completes a refresh without needing a pull gesture.</summary>
    public Button<ContainerTestPage> TriggerRefreshButton => new(this, "TriggerRefreshButton");

    /// <summary>
    /// A list control that keeps its RefreshView private, two levels down; a pull of the control
    /// reaches it.
    /// </summary>
    public RefreshView<ContainerTestPage> NestedRefreshList => new(this, "NestedRefreshList");

    /// <summary>"fetched n: rows" after the nested list's refresh asked its fake service.</summary>
    public Label<ContainerTestPage> NestedRefreshStatus => new(this, "NestedRefreshStatus");

    /// <summary>The nested list's header button, running the same refresh.</summary>
    public Button<ContainerTestPage> NestedRefreshButton => new(this, "NestedRefreshButton");

    /// <summary>Restores the initial state.</summary>
    public Button<ContainerTestPage> ResetButton => new(this, "ContainerResetButton");

    #endregion

    #region Containers

    /// <summary>A Grid holding four cells.</summary>
    public Grid<ContainerTestPage> TestGrid { get; }

    /// <summary>A Border wrapping a label and a button.</summary>
    public Border<ContainerTestPage> TestBorder { get; }

    /// <summary>A ContentView wrapping a label and a button.</summary>
    public ContentView<ContainerTestPage> TestContentView { get; }

    /// <summary>A bounded ScrollView that owns its own scrolling.</summary>
    public ScrollView<ContainerTestPage> TestScrollView { get; }

    /// <summary>The label inside <c>TestSwipeView</c>, which is addressable even though the
    /// swipe view itself is not.</summary>
    public Label<ContainerTestPage> SwipeContentLabel => new(this, "SwipeContentLabel");

    /// <summary>The BoxView, which has no children by design.</summary>
    public Label<ContainerTestPage> TestBoxView => new(this, "TestBoxView");

    #endregion

    #region Container children

    // Declared rather than reached for. These are the children the container tests assert on;
    // without them a test has nowhere to get "GridCellTopRight" from and builds a control - or
    // a raw locator - on the spot, which is how element finding leaks back into tests.

    /// <summary>Top-left cell of <see cref="TestGrid"/>.</summary>
    public Label<Grid<ContainerTestPage>> GridCellTopLeft => TestGrid.Label("GridCellTopLeft");

    /// <summary>Top-right cell of <see cref="TestGrid"/>.</summary>
    public Label<Grid<ContainerTestPage>> GridCellTopRight => TestGrid.Label("GridCellTopRight");

    /// <summary>Bottom-left cell of <see cref="TestGrid"/>.</summary>
    public Label<Grid<ContainerTestPage>> GridCellBottomLeft => TestGrid.Label("GridCellBottomLeft");

    /// <summary>The button inside <see cref="TestGrid"/>.</summary>
    public Button<Grid<ContainerTestPage>> GridButton => TestGrid.Button("GridButton");

    /// <summary>The label inside <see cref="TestBorder"/>.</summary>
    public Label<Border<ContainerTestPage>> BorderChildLabel => TestBorder.Label("BorderChildLabel");

    /// <summary>The button inside <see cref="TestBorder"/>.</summary>
    public Button<Border<ContainerTestPage>> BorderButton => TestBorder.Button("BorderButton");

    /// <summary>The label inside <see cref="TestContentView"/>.</summary>
    public Label<ContentView<ContainerTestPage>> ContentViewChildLabel
        => TestContentView.Label("ContentViewChildLabel");

    /// <summary>The button inside <see cref="TestContentView"/>.</summary>
    public Button<ContentView<ContainerTestPage>> ContentViewButton
        => TestContentView.Button("ContentViewButton");

    /// <summary>The first label inside <see cref="TestScrollView"/>.</summary>
    public Label<ScrollView<ContainerTestPage>> ScrollFirstLabel
        => TestScrollView.Label("ScrollFirstLabel");

    /// <summary>The last label inside <see cref="TestScrollView"/>, below the fold.</summary>
    public Label<ScrollView<ContainerTestPage>> ScrollLastLabel
        => TestScrollView.Label("ScrollLastLabel");

    #endregion

    /// <summary>
    /// Waits for the swipe item behind <c>TestSwipeView</c> to be revealed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The item exists in the markup from the start but is not in the automation tree until the
    /// swipe view is open, so this polls rather than resolving once. It is the observable
    /// outcome of a swipe: the <c>SwipeView</c> itself publishes no <c>AutomationId</c> on
    /// Windows, and "is it open" has to be asked about its contents instead.
    /// </para>
    /// <para>
    /// Paced, not spun. An unpaced retry loop here puts thousands of cross-process automation
    /// calls through the app in a couple of seconds and slows down the thing it is waiting for.
    /// </para>
    /// </remarks>
    /// <param name="timeoutMs">How long to wait.</param>
    /// <returns>The revealed item, or null if it never appeared.</returns>
    public IMauiElement? WaitForSwipeItem(int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        do
        {
            var item = TryFindByAutomationId("SwipeDeleteItem");
            if (item is not null)
            {
                return item;
            }

            WaitHelper.Pause(50);
        }
        while (DateTime.UtcNow < deadline);

        return null;
    }

    /// <summary>
    /// Resolves an element by automation id from page scope, or null.
    /// </summary>
    public IMauiElement? TryFindByAutomationId(string automationId)
    {
        try
        {
            return Context.TryFindElement(Locator.ByAutomationId(automationId));
        }
        catch (ElementNotFoundException)
        {
            return null;
        }
    }
}
