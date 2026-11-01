namespace Bruinet.Core.Abstractions.Controls;

/// <summary>
/// Abstract base class for all control objects providing protected access to 
/// internal framework members (Locator, Scope).
/// Platform implementations inherit from this class.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent method chaining.</typeparam>
public abstract class ControlObjectBase<TScope>
{
    /// <summary>
    /// The locator used to find this control in the UI tree.
    /// Protected - for framework internal use only.
    /// </summary>
    protected Locator Locator { get; }

    /// <summary>
    /// The element scope (page or container) for this control.
    /// Protected - for framework internal use only.
    /// </summary>
    protected IElementScope Scope { get; }

    /// <summary>
    /// The page containing this control.
    /// Accessed via Scope.Page - for framework internal use only.
    /// </summary>
    protected IPageObject? Page => Scope.Page;

    /// <summary>
    /// Creates a new control object with the specified locator and scope.
    /// </summary>
    /// <param name="locator">The locator used to find this control.</param>
    /// <param name="scope">The scope (page or container) for element finding.</param>
    protected ControlObjectBase(Locator locator, IElementScope scope)
    {
        Locator = locator ?? throw new ArgumentNullException(nameof(locator));
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }
}
