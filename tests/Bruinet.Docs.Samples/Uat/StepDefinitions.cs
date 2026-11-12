// docs/guides/uat-step-definitions.md quotes these regions: change them here, then run tools/Scripts/update-doc-samples.ps1.
// The control verbs and phrases the guide shows by their declaration only are interface members here.
using Bruinet.Bdd;
using Xunit;

namespace Bruinet.Docs.Samples.Uat;

public interface IEnterVerb<TScope>
{
    #region docs:pattern
    [BddWhen("I enter {value} into {control}")]
    TScope Enter(string? text, int? timeoutMs = null);
    #endregion
}

public interface ISetTextVerb<TScope>
{
    #region docs:user-forms
    [BddWhen("I set {control} to {value}", CommandId = "Control.SetText")]
    [BddWhen("{user} sets {control} to {value}", CommandId = "Control.SetText")]
    TScope SetText(string? text, int? timeoutMs = null);
    #endregion
}

public interface ITodoPhrases
{
    #region docs:arguments
    [BddGiven("the server also has todos")]
    public void ServerAlsoHas(IReadOnlyList<ServerTodoRow> todos);   // a row per todo

    [BddWhen("I create the todo")]
    public void Create(TodoRow todo);                                 // a Field | Value table, or one row, or a JSON doc string

    [BddThen("the todos should be open")]
    public void TodosAreOpen(List<string> titles);                    // a one-column table

    [BddWhen("I write the note")]
    public void Write(string note);                                   // a """ doc string
    #endregion

    #region docs:method-name
    [BddWhen]                                          // I open the todo {title}
    [BddWhen("{user} opens the todo {title}")]
    public void I_open_the_todo_TITLE(string title);
    #endregion
}

public interface ITodoStatusVerbs<TScope>
{
    #region docs:control-verbs
    [BddWhen("I advance {control} to {value}")]
    public TodoStatus<TScope> AdvanceTo(string? expected, int? timeoutMs = null);

    [BddThen("{control} should show status {value}")]
    public TodoStatus<TScope> AssertShowsStatus(
        string? expected, string? message = null, int? timeoutMs = null);
    #endregion
}

public interface ITodoListVerbs
{
    #region docs:collection-verb
    [BddThen("{control} should have todo {value}")]
    public TodoList AssertRow(string title, bool present = true, int? timeoutMs = null);
    #endregion

    #region docs:item-verbs
    [BddWhen("I open the todo {item}")]
    public TodoDetailPage Open(string title, int? timeoutMs = null);

    [BddThen("the todo {item} should show status {value}")]
    public TodoList AssertRowStatus(string title, string status);

    [BddThen("the todo {item} should be waiting to sync", HasLiteral = true, Literal = true)]
    [BddThen("the todo {item} should be synced", HasLiteral = true, Literal = false)]
    public TodoList AssertPending(string title, bool expected = true, int? timeoutMs = null);
    #endregion
}

public sealed class InvoiceApi
{
    public void AddLine(string text)
    {
    }
}

#region docs:namespace
[BddStepDefinitions(Namespace = "Invoicing")]
public sealed class InvoicingPhrases(InvoiceApi api) : BddStepDefinitionsBase
{
    [BddWhen("I add a line {text}")]
    public void AddLine(string text) => api.AddLine(text);
}
#endregion

public sealed class TrainingFixture
{
    public string? RememberedTrainedPlayer { get; private set; }

    #region docs:root-phrases
    [BddGiven("I remember {name} as a trained player")]
    public void RememberTrainedPlayer(string name)
    {
        RememberedTrainedPlayer = name;
    }

    [BddThen("remembered trained player should be {name}")]
    public void AssertRememberedTrainedPlayer(string name)
    {
        Assert.Equal(name, RememberedTrainedPlayer);
    }
    #endregion
}

public sealed class CompositionState
{
    public int ActionCount { get; set; }
}

#region docs:step-definitions
[BddStepDefinitions]
public sealed class CompositionPhrases : BddStepDefinitionsBase
{
    private readonly CompositionState _state;

    public CompositionPhrases(CompositionState state)
    {
        _state = state;
    }

    [BddWhen("I choose DI flow")]
    public void ChooseDiFlow()
    {
        _state.ActionCount++;
    }

    [BddThen("scoped action count should be {count}")]
    public void AssertScopedActionCount(int count)
    {
        Assert.Equal(count, _state.ActionCount);
    }
}
#endregion

public static class ManualPatterns
{
    public static void Register(BddCommandCatalog catalog)
    {
        #region docs:register
        catalog.Register(
            BddEffectiveStepKeyword.When,
            "I sign in with credentials",
            "Login.SignInWithCredentials",
            requiresTable: true);
        #endregion
    }
}

#region docs:hooks
[BddStepDefinitions]
public sealed class BackendHooks(ITodoArrangement fixture) : BddStepDefinitionsBase
{
    [BddBeforeFeature]
    public static void WarmUp() { }

    [BddBeforeScenario("backend")]
    public void ResetServer() => fixture.ResetBackend();

    [BddAfterStep]
    public void Log(BddStepResult result) { }

    [BddAfterScenario]
    public void TearDown() { }
}
#endregion

[BddStepDefinitions]
public sealed class TestSetStates(TodoAppFixture fixture) : BddStepDefinitionsBase
{
    #region docs:state
    [BddState("the app starts from the {testSet} test set")]
    public void StartFrom(string testSet, BddExecutionContext context)
    {
        fixture.UseTestSet(testSet);
        fixture.RescopeAfterRestart(context);
    }
    #endregion
}
