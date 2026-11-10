using Bruinet.Bdd;

namespace Bruinet.Uat;

public static class UatSpecCommandCatalog
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
        // spec catalog.
        catalog.Register(BddEffectiveStepKeyword.Given, "I am on the {page} page", "Spec.Page.Open", namesPage: true);
        catalog.Register(BddEffectiveStepKeyword.Given, "{user} is on the {page} page", "Spec.Page.Open", namesPage: true);
        catalog.Register(BddEffectiveStepKeyword.Then, "I should be on the {page} page", "Spec.Page.AssertOpen", namesPage: true);
        catalog.Register(BddEffectiveStepKeyword.Then, "{user} should be on the {page} page", "Spec.Page.AssertOpen", namesPage: true);
        catalog.Register(BddEffectiveStepKeyword.Then, "I should see {text}", "Spec.Page.AssertTextVisible");
        catalog.Register(BddEffectiveStepKeyword.Then, "{user} should see {text}", "Spec.Page.AssertTextVisible");
        catalog.Register(BddEffectiveStepKeyword.Then, UatReflectionRuntime.PausePhrase, "Spec.Pause", allowsTable: false);

        // Control vocabulary is discovered from the same [BddWhen]/[BddThen] attributes the runtime uses,
        // so the spec surface and the executable surface can never drift.
        UatCatalogBuilder.RegisterControlVerbs(
            catalog,
            UatCatalogBuilder.CoreStepTypes,
            "Spec.",
            handlerFactory: null);
    }
}
