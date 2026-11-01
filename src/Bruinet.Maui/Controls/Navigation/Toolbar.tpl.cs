using Bruinet.Maui.Containers;

namespace Bruinet.Maui.Controls.Navigation;

/// <summary>
/// MAUI Toolbar: a collection of <see cref="ToolbarItem{TParent}"/>, plus whatever else the
/// bar itself shows.
/// </summary>
/// <remarks>
/// <para>
/// Items are addressed on the toolbar and answered by an object:
/// <c>Toolbar["Save"].Click()</c>, <c>Toolbar["Save"].AssertEnabled()</c>,
/// <c>Toolbar.AssertItemCount(3)</c>. The string key is the item's caption - see
/// <see cref="CollectionObjectBase{TParent, TSelf, TItem}.MatchesKey"/>.
/// </para>
/// <para>
/// Being a container, the toolbar also scopes controls of its own, so an item with a unique
/// automation id can still be reached as a control:
/// <c>new Button&lt;Toolbar&lt;MyPage&gt;&gt;(toolbar, "ToolbarSaveButton")</c>. Both routes
/// search within the toolbar, never the page.
/// </para>
/// </remarks>
/// <typeparam name="TParent">The containing scope type.</typeparam>
public partial class Toolbar<TParent>
    : CollectionObjectBase<TParent, Toolbar<TParent>, ToolbarItem<TParent>>
    where TParent : IMauiScope<TParent>
{
    /// <summary>
    /// How a toolbar finds its items when the caller does not say: every button inside it.
    /// </summary>
    /// <remarks>
    /// A toolbar holds commands, and a command is a button on every platform Bruinet drives.
    /// A bar that holds something else - a search field, a segmented control - passes its own
    /// strategy to the constructor rather than having this guess for it.
    /// </remarks>
    public static IItemStrategy DefaultItemStrategy { get; } =
        ItemStrategy.ByLocator(Locator.ByControlType("Button"));

    /// <summary>
    /// Creates a Toolbar with a locator, finding items with <see cref="DefaultItemStrategy"/>.
    /// </summary>
    public Toolbar(IMauiScope<TParent> scope, Locator locator)
        : this(scope, locator, DefaultItemStrategy)
    {
    }

    /// <summary>
    /// Creates a Toolbar with a locator and an explicit item strategy.
    /// </summary>
    public Toolbar(IMauiScope<TParent> scope, Locator locator, IItemStrategy itemStrategy)
        : base(scope, locator, itemStrategy, (toolbar, itemRoot, index) => new ToolbarItem<TParent>(toolbar, itemRoot, index))
    {
    }

    /// <summary>
    /// Creates a Toolbar with an automation ID, finding items with <see cref="DefaultItemStrategy"/>.
    /// </summary>
    public Toolbar(IMauiScope<TParent> scope, string automationId)
        : this(scope, automationId, DefaultItemStrategy)
    {
    }

    /// <summary>
    /// Creates a Toolbar with an automation ID and an explicit item strategy.
    /// </summary>
    public Toolbar(IMauiScope<TParent> scope, string automationId, IItemStrategy itemStrategy)
        : base(scope, automationId, itemStrategy, (toolbar, itemRoot, index) => new ToolbarItem<TParent>(toolbar, itemRoot, index))
    {
    }

    #region Core Methods (Element-Aware, No Logging)

    /// <summary>
    /// Gets the title text displayed in the toolbar.
    /// </summary>
    /// <param name="element">The toolbar's own element (may be null).</param>
    /// <returns>The title text, or null if not available.</returns>
    protected virtual string? GetTitleCore(IMauiElement? element)
    {
        if (element == null)
            return null;

        // A toolbar's caption is whatever it renders: no platform publishes a Title property for
        // a container, and a layout with no text of its own answers null, which is the truth.
        return element.Text;
    }

    #endregion
}
