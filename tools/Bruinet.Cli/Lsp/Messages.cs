using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bruinet.Bdd.LanguageServer;

// The messages, as they go over the wire: LSP's shapes where LSP has one, camelCase JSON.

/// <summary>LSP <c>initialize</c>: the solution folder, from <c>initializationOptions.solutionDir</c> or the root URI.</summary>
public sealed record InitializeParams(
    int? ProcessId,
    string? RootUri,
    InitializationOptions? InitializationOptions)
{
    /// <summary>What the client can do; the editor's LSP client says it takes diagnostics, the preview's does not.</summary>
    public JsonElement? Capabilities { get; init; }
}

/// <summary>What the client tells the server beyond LSP's own fields.</summary>
/// <param name="SolutionDir">The folder whose suites the server serves.</param>
public sealed record InitializationOptions(string? SolutionDir)
{
    /// <summary>Whether a suite's runner is started when one of its feature files opens, and kept between runs (AD-025); on by default.</summary>
    public bool? WarmRunner { get; init; }

    /// <summary>How long a suite's runner waits after its last feature file closes before it ends; 10 by default.</summary>
    public double? WarmRunnerIdleMinutes { get; init; }
}

/// <summary><c>bruinet/runner/warm</c>: the runner of the suite that holds a feature file, ready now.</summary>
/// <param name="Uri">The feature file.</param>
/// <param name="Suite">The suite picked for a linked file; null for the first.</param>
public sealed record RunnerWarmParams(string Uri, string? Suite);

/// <summary>Whether the suite's runner is ready.</summary>
/// <param name="Warm">Whether it is.</param>
/// <param name="Reason">Why not; null when it is.</param>
public sealed record RunnerWarmResult(bool Warm, string? Reason);

/// <summary>The server's answer to <c>initialize</c>: LSP capabilities, and the suites it found.</summary>
public sealed record InitializeResult(ServerCapabilities Capabilities, ServerInfo ServerInfo, IReadOnlyList<SuiteInfo> Suites);

/// <summary>LSP capabilities.</summary>
/// <param name="TextDocumentSync">Open and close, the whole text on each change, and save.</param>
public sealed record ServerCapabilities(TextDocumentSyncOptions TextDocumentSync)
{
    /// <summary>The outline.</summary>
    public bool DocumentSymbolProvider { get; init; }

    /// <summary>Folding of sections, fences and tables.</summary>
    public bool FoldingRangeProvider { get; init; }

    /// <summary>The colours of keywords, values, placeholders and tags.</summary>
    public SemanticTokensOptions? SemanticTokensProvider { get; init; }

    /// <summary>Completion of keywords, sections, steps and values.</summary>
    public CompletionOptions? CompletionProvider { get; init; }

    /// <summary>What a step binds to, its summary and its arguments as read.</summary>
    public bool HoverProvider { get; init; }

    /// <summary>A step to its step definition, page, control or callable.</summary>
    public bool DefinitionProvider { get; init; }

    /// <summary>Every step across the suite that binds to the same thing.</summary>
    public bool ReferencesProvider { get; init; }

    /// <summary>Quick fixes, create step definition, extract and inline a callable step.</summary>
    public CodeActionOptions? CodeActionProvider { get; init; }

    /// <summary>Renaming a callable step and every call of it.</summary>
    public RenameOptions? RenameProvider { get; init; }
}

/// <summary>LSP code action options: the kinds offered.</summary>
public sealed record CodeActionOptions(IReadOnlyList<string> CodeActionKinds);

/// <summary>LSP rename options: the client asks first what can be renamed.</summary>
public sealed record RenameOptions(bool PrepareProvider);

/// <summary>LSP completion options.</summary>
/// <param name="TriggerCharacters">Typing one of these asks for completion.</param>
public sealed record CompletionOptions(IReadOnlyList<string> TriggerCharacters);

/// <summary>LSP semantic tokens options: the whole file at once.</summary>
public sealed record SemanticTokensOptions(SemanticTokensLegend Legend, bool Full);

/// <summary>LSP semantic tokens legend.</summary>
public sealed record SemanticTokensLegend(IReadOnlyList<string> TokenTypes, IReadOnlyList<string> TokenModifiers);

/// <summary>LSP text document sync options.</summary>
/// <param name="OpenClose">Whether the client sends open and close.</param>
/// <param name="Change">1: the whole text on each change.</param>
/// <param name="Save">Whether the client sends save.</param>
public sealed record TextDocumentSyncOptions(bool OpenClose, int Change, SaveOptions Save);

/// <summary>LSP save options: the server reads a saved file itself.</summary>
public sealed record SaveOptions(bool IncludeText);

/// <summary>LSP server info.</summary>
public sealed record ServerInfo(string Name, string Version);

/// <summary>A suite, for the client's picker.</summary>
/// <param name="Name">The project folder's name.</param>
/// <param name="Config">The config's full path.</param>
/// <param name="Head">The config's <c>Head</c>, or null.</param>
public sealed record SuiteInfo(string Name, string Config, string? Head);

/// <summary>LSP <c>textDocument/didOpen</c>.</summary>
public sealed record DidOpenTextDocumentParams(TextDocumentItem TextDocument);

/// <summary>LSP text document item.</summary>
public sealed record TextDocumentItem(string Uri, string? LanguageId, int Version, string Text);

/// <summary>LSP <c>textDocument/didChange</c>, full sync: the last change holds the whole text.</summary>
public sealed record DidChangeTextDocumentParams(VersionedTextDocumentIdentifier TextDocument, IReadOnlyList<TextDocumentContentChangeEvent> ContentChanges);

/// <summary>LSP versioned document id.</summary>
public sealed record VersionedTextDocumentIdentifier(string Uri, int Version);

/// <summary>LSP content change; with full sync, the whole text.</summary>
public sealed record TextDocumentContentChangeEvent(string Text);

/// <summary>LSP <c>textDocument/didClose</c>.</summary>
public sealed record DidCloseTextDocumentParams(TextDocumentIdentifier TextDocument);

/// <summary>LSP document id.</summary>
public sealed record TextDocumentIdentifier(string Uri);

/// <summary>LSP <c>textDocument/didSave</c>.</summary>
public sealed record DidSaveTextDocumentParams(TextDocumentIdentifier TextDocument);

/// <summary>LSP position: 0-based line and character.</summary>
public sealed record Position(int Line, int Character);

/// <summary>LSP range, end exclusive.</summary>
public sealed record Range(Position Start, Position End);

/// <summary>LSP location.</summary>
public sealed record Location(string Uri, Range Range);

/// <summary>LSP diagnostic.</summary>
/// <param name="Range">Where: the line's text.</param>
/// <param name="Severity">1 error, 2 warning, 3 information.</param>
/// <param name="Code">The gate's code (<c>UATB010</c>), or <c>UATRUN</c> for a step that failed in the last run.</param>
/// <param name="Source">Always <c>Bruinet BDD</c>.</param>
/// <param name="Message">The gate's text, word for word.</param>
public sealed record Diagnostic(Range Range, int Severity, string Code, string Source, string Message);

/// <summary>A request about a position: <c>completion</c>, <c>hover</c>, <c>definition</c>, <c>references</c>.</summary>
public sealed record PositionParams(TextDocumentIdentifier TextDocument, Position Position);

/// <summary>LSP completion list; incomplete, so the client asks again as the words change.</summary>
public sealed record CompletionList(bool IsIncomplete, IReadOnlyList<CompletionItem> Items);

/// <summary>LSP completion item.</summary>
/// <param name="Label">What the list shows.</param>
/// <param name="Kind">LSP completion item kind.</param>
public sealed record CompletionItem(string Label, int Kind)
{
    /// <summary>What it binds to, or where it comes from.</summary>
    public string? Detail { get; init; }

    /// <summary>The text put in, replacing <see cref="TextEdit"/>'s range.</summary>
    public TextEdit? TextEdit { get; init; }

    /// <summary>1 plain text, 2 a snippet with <c>${1:x}</c> placeholders.</summary>
    public int? InsertTextFormat { get; init; }

    /// <summary>What the client filters on: the typed words, so a match by words is not dropped for not being a prefix.</summary>
    public string? FilterText { get; init; }

    /// <summary>The order in the list.</summary>
    public string? SortText { get; init; }
}

/// <summary>LSP hover.</summary>
public sealed record Hover(MarkupContent Contents, Range? Range);

/// <summary>LSP markup: <c>markdown</c>.</summary>
public sealed record MarkupContent(string Kind, string Value);

/// <summary>LSP <c>textDocument/references</c>.</summary>
public sealed record ReferenceParams(TextDocumentIdentifier TextDocument, Position Position, ReferenceContext? Context);

/// <summary>Whether the definition is one of the references.</summary>
public sealed record ReferenceContext(bool IncludeDeclaration);

/// <summary>LSP <c>textDocument/codeAction</c>.</summary>
public sealed record CodeActionParams(TextDocumentIdentifier TextDocument, Range Range, CodeActionContext Context);

/// <summary>The diagnostics at the range.</summary>
public sealed record CodeActionContext(IReadOnlyList<Diagnostic> Diagnostics);

/// <summary>LSP code action.</summary>
/// <param name="Title">What the light bulb shows.</param>
/// <param name="Kind"><c>quickfix</c>, <c>refactor.extract</c>, <c>refactor.inline</c>.</param>
/// <param name="Edit">The change.</param>
public sealed record CodeAction(string Title, string Kind, WorkspaceEdit Edit)
{
    /// <summary>The diagnostics it fixes.</summary>
    public IReadOnlyList<Diagnostic>? Diagnostics { get; init; }

    /// <summary>Whether it is the one right answer.</summary>
    public bool? IsPreferred { get; init; }
}

/// <summary>LSP workspace edit: text edits by file, or document changes with a file rename.</summary>
public sealed record WorkspaceEdit
{
    /// <summary>Text edits, by file URI.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<TextEdit>>? Changes { get; init; }

    /// <summary>Renames and text edits, in order.</summary>
    public IReadOnlyList<object>? DocumentChanges { get; init; }
}

/// <summary>LSP rename-file operation.</summary>
public sealed record RenameFile(string OldUri, string NewUri)
{
    /// <summary>Always <c>rename</c>.</summary>
    public string Kind => "rename";
}

/// <summary>LSP text document edit.</summary>
public sealed record TextDocumentEdit(VersionedTextDocumentIdentifierOrNull TextDocument, IReadOnlyList<TextEdit> Edits);

/// <summary>LSP optional versioned document id: a null version for a file edited as it is on disk.</summary>
public sealed record VersionedTextDocumentIdentifierOrNull(string Uri, int? Version);

/// <summary>LSP <c>textDocument/rename</c>.</summary>
public sealed record RenameParams(TextDocumentIdentifier TextDocument, Position Position, string NewName);

/// <summary>LSP prepare-rename answer: the phrase's range and its text.</summary>
public sealed record PrepareRenameResult(Range Range, string Placeholder);

/// <summary>LSP text edit.</summary>
public sealed record TextEdit(Range Range, string NewText);

/// <summary>A request about a whole file: <c>documentSymbol</c>, <c>foldingRange</c>, <c>semanticTokens/full</c>.</summary>
public sealed record DocumentParams(TextDocumentIdentifier TextDocument);

/// <summary>LSP document symbol: a heading, with the headings under it.</summary>
/// <param name="Name">The heading's name.</param>
/// <param name="Detail">Its kind: <c>Scenario</c>, <c>Step</c>.</param>
/// <param name="Kind">LSP symbol kind.</param>
/// <param name="Range">The whole section.</param>
/// <param name="SelectionRange">The heading line.</param>
/// <param name="Children">The headings under it.</param>
public sealed record DocumentSymbol(string Name, string? Detail, int Kind, Range Range, Range SelectionRange, IReadOnlyList<DocumentSymbol> Children);

/// <summary>LSP folding range, 0-based lines.</summary>
public sealed record FoldingRange(int StartLine, int EndLine, string? Kind = null);

/// <summary>LSP semantic tokens: five numbers per token, each relative to the one before.</summary>
public sealed record SemanticTokens(IReadOnlyList<int> Data);

/// <summary>LSP <c>textDocument/publishDiagnostics</c>: every diagnostic of a file, replacing the last.</summary>
public sealed record PublishDiagnosticsParams(string Uri, int? Version, IReadOnlyList<Diagnostic> Diagnostics);

/// <summary><c>bruinet/preview</c>: a file, and the suite to bind it in when it is linked into several.</summary>
public sealed record PreviewParams(string Uri, string? Suite)
{
    /// <summary>The Recordings folder setting, whose videos the preview lists; null for the suite's results.</summary>
    public string? RecordingsFolder { get; init; }
}

/// <summary>The rendered preview.</summary>
/// <param name="Html">The file, rendered (<c>Bruinet.Bdd.Preview</c>).</param>
/// <param name="Version">The document version rendered; 0 for a file read from disk.</param>
/// <param name="Suites">The suites the file belongs to, for the picker.</param>
/// <param name="Suite">The suite it was bound in; null when none.</param>
/// <param name="BindState"><c>bound</c> or <c>parse-only</c>.</param>
/// <param name="Reason">Why it is only parsed; null when bound.</param>
public sealed record PreviewResult(string Html, int Version, IReadOnlyList<string> Suites, string? Suite, string BindState, string? Reason)
{
    /// <summary>The folder of the runs, which the client maps to <see cref="ResultsHost"/> to show screenshots.</summary>
    public string? ResultsRoot { get; init; }

    /// <summary>The virtual host the preview's screenshot URLs use.</summary>
    public string? ResultsHost { get; init; }
}

/// <summary><c>bruinet/runTarget</c>: what to run for a scenario heading's line, or the whole file when there is none.</summary>
public sealed record RunTargetParams(string Uri, int? Line, string? Suite);

/// <summary>How to run it: <c>dotnet test &lt;Project&gt; --no-build --filter &lt;Filter&gt;</c>.</summary>
/// <param name="CanRun">Whether it can be run by address.</param>
/// <param name="Reason">Why not; null when it can.</param>
/// <param name="Project">The suite's project file.</param>
/// <param name="Filter">The <c>dotnet test</c> filter on the <c>BddScenario</c> trait.</param>
/// <param name="Scenarios">The scenarios it runs; empty for the whole file.</param>
/// <param name="Suite">The suite it runs in.</param>
public sealed record RunTargetResult(bool CanRun, string? Reason, string? Project, string? Filter, IReadOnlyList<string> Scenarios, string? Suite);

/// <summary>
/// <c>bruinet/deleteResults</c>: deletes old scenario results, for good. <paramref name="Scope"/> is
/// <c>scenario</c> (the heading at <paramref name="Line"/> of <paramref name="Uri"/>, every row of
/// an outline), <c>file</c> (<paramref name="Uri"/>) or <c>suite</c> (<paramref name="Suite"/>).
/// </summary>
/// <param name="Scope"><c>scenario</c>, <c>file</c> or <c>suite</c>.</param>
/// <param name="Uri">The feature file, for a scenario or a file.</param>
/// <param name="Line">The scenario heading's line, for a scenario.</param>
/// <param name="Suite">The suite: the one picked for a linked file, or the one to clear.</param>
/// <param name="DryRun">Counts only, for the confirmation; nothing is deleted.</param>
public sealed record DeleteResultsParams(string Scope, string? Uri, int? Line, string? Suite, bool DryRun);

/// <summary>What was deleted, or would be in a dry run.</summary>
/// <param name="Done">Whether the scope was found; <paramref name="Reason"/> says why not.</param>
/// <param name="Reason">Why nothing could be done.</param>
/// <param name="What">What the results are of: <c>TOD.01.2 …</c>, <c>TOD.01-see-my-todos.feature.md</c>, a suite.</param>
/// <param name="Suite">The suite whose results they are.</param>
/// <param name="RunsChanged">Runs that kept other results.</param>
/// <param name="RunsDeleted">Runs left empty, deleted whole.</param>
/// <param name="FilesDeleted">Reports and screenshots deleted.</param>
/// <param name="Skipped">Runs not touched, and why.</param>
public sealed record DeleteResultsResult(bool Done, string? Reason, string? What, string? Suite, int RunsChanged, int RunsDeleted, int FilesDeleted, IReadOnlyList<SkippedRun> Skipped);

/// <summary>A run left alone.</summary>
public sealed record SkippedRun(string Run, string Reason);

/// <summary><c>bruinet/usages</c>: a C# member, as CodeLens names it (<c>Ns.Type.Method(int)</c>).</summary>
public sealed record UsagesParams(string Symbol);

/// <summary>The steps across every suite that use a C# member.</summary>
/// <param name="Steps">How many steps.</param>
/// <param name="Scenarios">How many scenarios they are in.</param>
/// <param name="Usages">Each step.</param>
public sealed record UsagesResult(int Steps, int Scenarios, IReadOnlyList<Usage> Usages);

/// <summary>A step that uses a C# member.</summary>
/// <param name="Uri">Its file.</param>
/// <param name="Line">Its 0-based line.</param>
/// <param name="Text">The line's text.</param>
/// <param name="Scenario">The scenario it is in.</param>
public sealed record Usage(string Uri, int Line, string Text, string Scenario);

/// <summary><c>bruinet/explorer</c>: a view (<c>steps</c>, <c>pages</c>, <c>results</c>) of a suite.</summary>
public sealed record ExplorerParams(string? View, string? Suite);

/// <summary>A view of a suite, as HTML for the explorer's page.</summary>
/// <param name="View">The view shown.</param>
/// <param name="Html">The view.</param>
/// <param name="Suites">Every suite, for the picker.</param>
/// <param name="Suite">The suite shown; null when there is none.</param>
public sealed record ExplorerResult(string View, string Html, IReadOnlyList<string> Suites, string? Suite);

/// <summary>A suite's scenarios, for an IDE's test explorer.</summary>
/// <param name="Suite">The suite's name.</param>
/// <param name="Project">Its project file, which <c>dotnet test</c> runs.</param>
/// <param name="Runnable">Whether its scenarios run by address (<c>[UatScenarioTheory]</c>, built).</param>
/// <param name="Reason">Why not; null when they do.</param>
/// <param name="Files">Its feature files, linked ones included.</param>
public sealed record SuiteScenarios(string Suite, string? Project, bool Runnable, string? Reason, IReadOnlyList<FileScenarios> Files);

/// <summary>A feature file's scenarios.</summary>
/// <param name="Uri">The file.</param>
/// <param name="Relative">Its path as the suite's tests see it, relative to the config.</param>
/// <param name="Scenarios">Its scenarios, an outline once.</param>
public sealed record FileScenarios(string Uri, string Relative, IReadOnlyList<ScenarioTest> Scenarios);

/// <summary>A scenario as a test.</summary>
/// <param name="Name">Its name; an outline's name for all its rows.</param>
/// <param name="Line">Its heading's line.</param>
/// <param name="Filter">The <c>dotnet test --filter</c> that runs it: its address, or each row's.</param>
public sealed record ScenarioTest(string Name, int Line, string Filter);

/// <summary>A scenario's last run.</summary>
/// <param name="Scenario">Its name; an outline row's has its <c>[n]</c>.</param>
/// <param name="Status"><c>passed</c>, <c>failed</c>, <c>skipped</c> or <c>canceled</c>.</param>
/// <param name="DurationMs">How long it took.</param>
/// <param name="Message">The failed step's message; null when none failed.</param>
/// <param name="Line">The failed step's line in the feature file; null when none failed.</param>
public sealed record ScenarioResult(string Scenario, string Status, long DurationMs, string? Message, int? Line);

/// <summary>The preview's host page.</summary>
/// <param name="Html">A whole page with the stylesheet and the page script (<c>BddPreview.HostPage</c>).</param>
public sealed record PreviewHostResult(string Html);

internal static class Json
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

/// <summary>
/// <c>bruinet/recordTarget</c>: what to record: a video script, or the scenario at a heading's
/// line; with the IDE's settings.
/// </summary>
/// <param name="Uri">The video script, or the feature file.</param>
/// <param name="Line">The scenario heading's line, for a feature file.</param>
/// <param name="Suite">The suite picked for a linked file; null for the first.</param>
/// <param name="RecordingsFolder">The Recordings folder setting; null for the suite's results.</param>
/// <param name="Voice">The voice setting; null for the default voice.</param>
/// <param name="Beat">The beat setting, in milliseconds; null for the default.</param>
/// <param name="TouchColor">The touch marks' accent setting, <c>#RRGGBB</c>; null for Exact red.</param>
public sealed record RecordTargetParams(string Uri, int? Line, string? Suite, string? RecordingsFolder, string? Voice, int? Beat, string? TouchColor = null);

/// <summary>How to record it: <c>dotnet test &lt;Project&gt; --no-build --filter &lt;Filter&gt;</c> with <paramref name="Environment"/> set.</summary>
/// <param name="CanRecord">Whether it can be recorded here.</param>
/// <param name="Reason">Why not (no ffmpeg, a script with errors, a suite that cannot run by address); null when it can.</param>
/// <param name="Project">The suite's project file.</param>
/// <param name="Filter">The <c>dotnet test</c> filter on the <c>BddScenario</c> trait.</param>
/// <param name="Environment">The gate run's variables (<c>BRUINET_RECORD</c> and the rest).</param>
/// <param name="Video">The video's file name in <paramref name="Folder"/>, for a script; null for a scenario, whose name the run stamps.</param>
/// <param name="Folder">The Recordings folder.</param>
/// <param name="Suite">The suite it runs in.</param>
public sealed record RecordTargetResult(bool CanRecord, string? Reason, string? Project, string? Filter, IReadOnlyDictionary<string, string> Environment, string? Video, string? Folder, string? Suite)
{
    /// <summary>It cannot be recorded, for <paramref name="reason"/>.</summary>
    public static RecordTargetResult Not(string reason) => new(false, reason, null, null, new Dictionary<string, string>(), null, null, null);
}

/// <summary><c>bruinet/recordings</c>: a video script or a feature file, and the Recordings folder setting.</summary>
public sealed record RecordingsParams(string Uri, string? Suite, string? RecordingsFolder);

/// <summary>A recorded video.</summary>
/// <param name="Path">The video file.</param>
/// <param name="Name">Its file name.</param>
/// <param name="Written">When it was made.</param>
/// <param name="Captions">Its <c>.srt</c>; null when it has none.</param>
public sealed record RecordingEntry(string Path, string Name, DateTime Written, string? Captions);

/// <summary><c>bruinet/step/start</c>: Step through a scenario in the suite's runner process (AD-022).</summary>
/// <param name="Uri">The feature file.</param>
/// <param name="Line">The scenario heading's line.</param>
/// <param name="Suite">The suite picked for a linked file; null for the first.</param>
/// <param name="Scenario">An outline's row by name; null for the first scenario under the heading.</param>
public sealed record StepStartParams(string Uri, int Line, string? Suite, string? Scenario);

/// <summary>Whether the run started; it then pauses before its first step (<c>bruinet/step/paused</c>).</summary>
/// <param name="Started">Whether it started.</param>
/// <param name="Reason">Why not; null when it started.</param>
/// <param name="Scenario">The scenario it runs.</param>
/// <param name="Suite">The suite it runs in.</param>
public sealed record StepStartResult(bool Started, string? Reason, string? Scenario, string? Suite)
{
    /// <summary>It did not start, for <paramref name="reason"/>.</summary>
    public static StepStartResult Not(string reason) => new(false, reason, null, null);
}

/// <summary><c>bruinet/step/move</c>: <c>over</c>, <c>into</c>, <c>out</c>, <c>continue</c> or <c>stop</c>.</summary>
public sealed record StepMoveParams(string Move);

/// <summary>Whether the move was taken: a run is paused (or, for <c>stop</c>, going).</summary>
/// <param name="Taken">Whether it was taken.</param>
/// <param name="Reason">Why not; null when it was.</param>
public sealed record StepMoveResult(bool Taken, string? Reason);

/// <summary><c>bruinet/step/paused</c>: where a Step-through run is paused.</summary>
/// <param name="Uri">The feature file the run is for.</param>
/// <param name="Scenario">The scenario.</param>
/// <param name="At">The file paused in: the feature file, or a step file inside a call.</param>
/// <param name="Line">The paused line.</param>
/// <param name="Text">The step or call there.</param>
/// <param name="Depth">How many calls deep: 0 in the scenario.</param>
/// <param name="IsCall">Whether it is paused at a call, so Step into goes inside.</param>
/// <param name="Calls">The calls around it, outermost first.</param>
/// <param name="Failed">Whether it is paused after the step failed.</param>
/// <param name="Message">The failure's message.</param>
/// <param name="Arguments">The step's arguments as read; null at a call or when it has none.</param>
/// <param name="App">The app the step runs on; null for the fixture's own.</param>
/// <param name="Page">The page the scenario is on; null when not known.</param>
public sealed record StepPausedParams(string Uri, string Scenario, string? At, int Line, string Text, int Depth, bool IsCall, IReadOnlyList<Bruinet.Bdd.Runner.BddRunnerCall> Calls, bool Failed, string? Message, IReadOnlyDictionary<string, string>? Arguments = null, string? App = null, string? Page = null);

/// <summary><c>bruinet/run/start</c>: Run on the suite's runner (AD-022), with no stops.</summary>
/// <param name="Uri">The feature file.</param>
/// <param name="Line">A scenario heading's line; null for every scenario of the file.</param>
/// <param name="Suite">The suite picked for a linked file; null for the first.</param>
/// <param name="Environment">Variables the runner gets beside its own: a replay mode (AI11); a runner with others is a new one.</param>
public sealed record RunStartParams(string Uri, int? Line, string? Suite, IReadOnlyDictionary<string, string>? Environment = null);

/// <summary>Whether the run started; <c>bruinet/run/ended</c> says how it went.</summary>
/// <param name="Started">Whether it started.</param>
/// <param name="Reason">Why not; null when it started.</param>
/// <param name="Scenarios">The scenarios it runs, in order (an outline's rows by their <c>[n]</c> names).</param>
/// <param name="Suite">The suite it runs in.</param>
public sealed record RunStartResult(bool Started, string? Reason, IReadOnlyList<string>? Scenarios, string? Suite)
{
    /// <summary>It did not start, for <paramref name="reason"/>.</summary>
    public static RunStartResult Not(string reason) => new(false, reason, null, null);
}

/// <summary><c>bruinet/run/ended</c>: a Run on the runner is over.</summary>
/// <param name="Uri">The feature file the run was for.</param>
/// <param name="Status"><c>passed</c>, <c>failed</c>, <c>canceled</c> or <c>error</c>.</param>
/// <param name="Message">What went wrong, for <c>error</c>.</param>
/// <param name="Results">Each scenario that ran, in order.</param>
public sealed record RunEndedParams(string Uri, string Status, string? Message, IReadOnlyList<RunScenarioResult> Results);

/// <summary>How a scenario of a Run on the runner went.</summary>
/// <param name="Scenario">Its name.</param>
/// <param name="Status"><c>passed</c>, <c>failed</c> or <c>error</c>.</param>
/// <param name="Message">The failure's or the error's message.</param>
/// <param name="FailedStep">The step that failed, when the runner said which.</param>
/// <param name="Address">The address it runs by alone (the gate's <c>BddScenario</c> trait).</param>
/// <param name="DurationMs">How long it took, opening and closing included.</param>
public sealed record RunScenarioResult(string Scenario, string Status, string? Message, RunFailedStep? FailedStep = null, string? Address = null, long? DurationMs = null);

/// <summary>The step a scenario of a Run on the runner failed at.</summary>
/// <param name="File">Its file: the feature file, or the step file of a call's body.</param>
/// <param name="Line">Its line.</param>
/// <param name="Text">Its text, with a call's values filled in.</param>
public sealed record RunFailedStep(string? File, int Line, string Text);

/// <summary>
/// The answer to <c>bruinet/step/debug</c> (Debug from here, AD-022): the runner
/// process to attach the debugger to. Once attached, a move runs the next step, which breaks in it.
/// </summary>
/// <param name="ProcessId">The runner's process; null when the run is not paused.</param>
/// <param name="Reason">Why not; null when it is.</param>
public sealed record StepDebugResult(int? ProcessId, string? Reason);

/// <summary>The answer to <c>bruinet/step/screenshot</c>: a screenshot of the app while the run is paused.</summary>
/// <param name="Path">The screenshot's file; null when none was taken.</param>
/// <param name="Reason">Why not; null when it was.</param>
public sealed record StepScreenshotResult(string? Path, string? Reason);

/// <summary><c>bruinet/step/ended</c>: the run is over.</summary>
/// <param name="Uri">The feature file the run was for.</param>
/// <param name="Scenario">The scenario.</param>
/// <param name="Status"><c>passed</c>, <c>failed</c>, <c>stopped</c> or <c>error</c>.</param>
/// <param name="Message">A failure's or an error's message.</param>
public sealed record StepEndedParams(string Uri, string Scenario, string Status, string? Message);
