using Bruinet.Maui.Containers;

namespace Bruinet.Maui.Controls.Navigation;

/// <summary>
/// MAUI tab bar: a collection of <see cref="TabItem{TParent}"/>.
/// </summary>
/// <remarks>
/// <para>
/// A Bruinet composite rather than a stock MAUI control - see <see cref="TabMenuMarkup"/> for
/// the ids it expects. Each tab is one item, rooted at the surface that holds that tab's
/// button and caption:
/// </para>
/// <code>
/// Tabs["Search"].Click();
/// Tabs.AssertItemCount(3);
/// Tabs["Search"].AssertSelected();
/// </code>
/// </remarks>
/// <typeparam name="TParent">The containing scope type.</typeparam>
public partial class TabMenu<TParent>
    : CollectionObjectBase<TParent, TabMenu<TParent>, TabItem<TParent>>
    where TParent : IMauiScope<TParent>
{
    /// <summary>
    /// How a tab bar finds its tabs when the caller does not say.
    /// </summary>
    public static IItemStrategy DefaultItemStrategy { get; } =
        ItemStrategy.ByAutomationId(TabMenuMarkup.TabId);

    /// <summary>
    /// Creates a tab menu within the specified scope.
    /// </summary>
    /// <param name="scope">The parent scope.</param>
    /// <param name="itemStrategy">How tabs are found. Defaults to <see cref="DefaultItemStrategy"/>.</param>
    public TabMenu(IMauiScope<TParent> scope, IItemStrategy? itemStrategy = null)
        : base(scope,
               Locator.ByAutomationId(TabMenuMarkup.RootId),
               itemStrategy ?? DefaultItemStrategy,
               NewItem)
    {
    }

    private static TabItem<TParent> NewItem(TabMenu<TParent> tabMenu, IMauiElement itemRoot, int index)
        => new(tabMenu, itemRoot, index);

    /// <summary>
    /// Matches a tab by its own element, then by its caption and its button.
    /// </summary>
    protected override bool MatchesKey(IMauiElement itemRoot, Locator key)
    {
        if (base.MatchesKey(itemRoot, key))
            return true;

        var caption = TabMenuMarkup.CaptionWithin(itemRoot);
        if (caption != null && ElementMatch.Matches(caption, key))
            return true;

        var button = TabMenuMarkup.ButtonWithin(itemRoot);
        return button != null && ElementMatch.Matches(button, key);
    }
}
