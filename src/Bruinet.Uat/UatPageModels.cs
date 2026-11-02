namespace Bruinet.Uat;

/// <summary>
/// The pages of a catalog, by page key (<see cref="UatPageModel.Key"/>), which a step's page scope
/// follows (<see cref="UatPageTracker"/>).
/// </summary>
/// <param name="Pages">The pages and their controls.</param>
public sealed record UatPageSet(IReadOnlyDictionary<string, UatPageModel> Pages) : IStepScopeModel
{
    private static readonly IReadOnlyDictionary<string, UatPageModel> None = new Dictionary<string, UatPageModel>();

    /// <inheritdoc />
    public IEnumerable<string> Namespaces => Pages.Values.Select(page => page.Namespace);

    /// <summary>The pages of <paramref name="catalog"/>; empty when it has none.</summary>
    /// <param name="catalog">A catalog.</param>
    public static IReadOnlyDictionary<string, UatPageModel> Of(BddCommandCatalog catalog)
        => (catalog.Scope as UatPageSet)?.Pages ?? None;

    /// <inheritdoc />
    public IStepScope StartWalk(BddCatalogSet catalogs, HashSet<string>? used) => new UatPageTracker(catalogs, used);
}

/// <summary>A page the binder can follow: its name, its page object type and its controls.</summary>
public sealed record UatPageModel(string Name, Type PageType, IReadOnlyList<UatControlModel> Controls)
{
    /// <summary>The page's namespace (<see cref="BddNamespaces"/>); <c>""</c> when every scenario sees it.</summary>
    public string Namespace { get; init; } = string.Empty;

    /// <summary>The page as steps and the runtime know it: <c>Invoicing/Detail</c>, or <see cref="Name"/>.</summary>
    public string Key => BddNamespaces.PageKey(Namespace, Name);
}

/// <summary>A control of a page, as the binder sees it.</summary>
/// <param name="Name">The UAT name.</param>
/// <param name="ControlType">The property's type.</param>
/// <param name="NavigatesTo">The key of the page tapping it always goes to (<see cref="UatNavigatesToAttribute"/>); null when not declared.</param>
public sealed record UatControlModel(string Name, Type ControlType, string? NavigatesTo)
{
    /// <summary>The page object's member it is (<c>EditButton</c> for "Edit"); null when not known.</summary>
    public string? MemberName { get; init; }
}
