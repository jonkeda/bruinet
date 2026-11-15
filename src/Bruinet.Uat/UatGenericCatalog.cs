using Bruinet.Bdd;

namespace Bruinet.Uat;

public static class UatGenericCatalog
{
    public static BddCommandCatalog CreateDefault()
    {
        var catalog = new BddCommandCatalog { IsGeneric = true };
        RegisterDefault(catalog);
        return catalog;
    }

    public static void RegisterDefault(BddCommandCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        // The three intrinsic page-graph verbs and their named forms — phrases only, no handler in a
        // generic catalog.
        catalog.Register(BddEffectiveStepKeyword.Given, "I am on the {page} page", "Generic.Page.Open", namesPage: true);
        catalog.Register(BddEffectiveStepKeyword.Given, "{user} is on the {page} page", "Generic.Page.Open", namesPage: true);
        catalog.Register(BddEffectiveStepKeyword.When, "I open the {page} page", "Generic.Page.Open", namesPage: true);
        catalog.Register(BddEffectiveStepKeyword.When, "{user} opens the {page} page", "Generic.Page.Open", namesPage: true);
        catalog.Register(BddEffectiveStepKeyword.Then, "I should be on the {page} page", "Generic.Page.AssertOpen", namesPage: true);
        catalog.Register(BddEffectiveStepKeyword.Then, "{user} should be on the {page} page", "Generic.Page.AssertOpen", namesPage: true);
        catalog.Register(BddEffectiveStepKeyword.Then, "the {page} page is open", "Generic.Page.AssertOpen", namesPage: true);
        catalog.Register(BddEffectiveStepKeyword.Then, "I should see {text}", "Generic.Page.AssertTextVisible");
        catalog.Register(BddEffectiveStepKeyword.Then, "{user} should see {text}", "Generic.Page.AssertTextVisible");
        catalog.Register(BddEffectiveStepKeyword.Then, UatReflectionRuntime.PausePhrase, "Generic.Pause", allowsTable: false);

        // Control vocabulary is discovered from the same [BddWhen]/[BddThen] attributes the runtime uses,
        // so the generic surface and the executable surface can never drift.
        UatCatalogBuilder.RegisterControlVerbs(
            catalog,
            UatCatalogBuilder.CoreStepTypes,
            "Generic.",
            handlerFactory: null);
    }
}
