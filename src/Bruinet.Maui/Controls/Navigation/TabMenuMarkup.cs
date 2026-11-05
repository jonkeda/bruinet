namespace Bruinet.Maui.Controls.Navigation;

/// <summary>
/// The automation ids a <see cref="TabMenu{TParent}"/> expects an app's tab bar to carry, and
/// the reach into one tab's parts.
/// </summary>
internal static class TabMenuMarkup
{
    /// <summary>The tab bar itself.</summary>
    internal const string RootId = "TabMenuView";

    /// <summary>One tab: the surface holding a button and a caption.</summary>
    internal const string TabId = "TabMenuView_Grid";

    /// <summary>The command-carrying button inside a tab.</summary>
    internal const string ButtonId = "TabMenuView_Button";

    /// <summary>The caption label inside a tab.</summary>
    internal const string CaptionId = "TabMenuView_Caption";

    /// <summary>The button inside a tab, or null when the tab has none.</summary>
    internal static IMauiElement? ButtonWithin(IMauiElement tabRoot) => Within(tabRoot, ButtonId);

    /// <summary>The caption label inside a tab, or null when the tab has none.</summary>
    internal static IMauiElement? CaptionWithin(IMauiElement tabRoot) => Within(tabRoot, CaptionId);

    // Kept for the attempt: a tab lookup by id, then by caption, found every tab's caption and
    // button twice. A tab bar does not scroll, so a part found once in an attempt is still there.
    private static IMauiElement? Within(IMauiElement tabRoot, string automationId)
    {
        if (tabRoot == null)
            return null;

        try
        {
            return Bruinet.Core.Calls.AttemptMemo.Remember(
                tabRoot, automationId, () => tabRoot.TryFindElement(Locator.ByAutomationId(automationId)));
        }
        catch (StaleElementException)
        {
            return null;
        }
    }
}
