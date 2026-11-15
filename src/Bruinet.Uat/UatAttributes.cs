namespace Bruinet.Uat;

/// <summary>Renamed: <c>[BddPage]</c> on a page, <c>[BddControl]</c> on a control (AD-040). Still read, one release.</summary>
[Obsolete("Use [BddPage] on a page and [BddControl] on a control (AD-040).")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Method)]
public sealed class UatNameAttribute : Attribute
{
    public UatNameAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public string Name { get; }
}

/// <summary>Read by discovery only; a control's verbs are its <c>[BddWhen]</c> and <c>[BddThen]</c> (AD-040). Removed next release.</summary>
[Obsolete("A control's verbs are its [BddWhen] and [BddThen] methods (AD-040).")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class UatActionAttribute : Attribute
{
    public UatActionAttribute(string actionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionName);
        ActionName = actionName.Trim();
    }

    public string ActionName { get; }
}

/// <summary>
/// On a page's control property: tapping the control always goes to this page, so the binder knows
/// the page after it without an <c>I should be on ...</c> step.
/// </summary>
[Obsolete("Use [BddNavigatesTo] (AD-040).")]
[AttributeUsage(AttributeTargets.Property)]
public sealed class UatNavigatesToAttribute : Attribute
{
    /// <param name="page">The page object type the control goes to.</param>
    public UatNavigatesToAttribute(Type page)
    {
        ArgumentNullException.ThrowIfNull(page);
        Page = page;
    }

    /// <summary>The page object type the control goes to.</summary>
    public Type Page { get; }
}
