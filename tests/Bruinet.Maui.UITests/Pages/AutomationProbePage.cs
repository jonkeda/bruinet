namespace Bruinet.Maui.UITests.Pages;

/// <summary>
/// Page object for the Phase 0 automation probe page.
/// </summary>
/// <remarks>
/// This page object deliberately exposes raw element lookups rather than typed
/// container objects. The question the probe answers is whether a layout type is
/// addressable by AutomationId <i>at all</i> — building a typed container object on
/// top of it would presuppose the answer.
/// </remarks>
public class AutomationProbePage : PageObjectBase<AutomationProbePage>
{
    public AutomationProbePage(IMauiTestContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public override string Name => "AutomationProbePage";


    /// <summary>The page title label.</summary>
    public Label<AutomationProbePage> PageTitle => new(this, "PageTitle");


    /// <summary>
    /// Resolves a probe container root by its automation id, from page scope.
    /// </summary>
    /// <returns>The element, or null when the layout does not expose its id.</returns>
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

    /// <summary>
    /// Resolves a probe child <i>through</i> its container, so a hit proves the
    /// container is usable as a scope rather than merely findable.
    /// </summary>
    /// <returns>
    /// The child element, or null when the container is unaddressable or the child
    /// cannot be reached from within it.
    /// </returns>
    public IMauiElement? TryFindChildThroughContainer(string containerId, string childId)
    {
        var container = TryFindByAutomationId(containerId);
        if (container == null)
            return null;

        return container.TryFindElement(Locator.ByAutomationId(childId));
    }
}
