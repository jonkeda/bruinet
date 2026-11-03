using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bruinet.Bdd.Workspace;
using Bruinet.Narrator;
using Bruinet.Scraper.Service;

namespace Bruinet.Bdd.LanguageServer;

/// <summary>
/// The Bruinet MCP server (phase 4, Copilot): the same engine as the language server, as tools an
/// agent calls. It gives the suites, their steps, pages and test sets, and checks a scenario draft
/// with the gate's own words, so an agent writes steps that exist rather than inventing them. It
/// holds data and checks; it calls no model. MCP over stdio: one JSON-RPC message per line.
/// </summary>
public sealed class BddMcpServer : IDisposable
{
    private const string ProtocolVersion = "2025-06-18";

    // Readable text for an agent: quotes and non-ASCII kept as they are, not \u escapes.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly BddLanguageServer _server = new();

    // The Step-through events this server has answered with: one raised since is a pause a wait gives.
    private long _stepEventsSeen;
    private readonly string _solutionDir;
    private BddAppSources? _appSources;
    private DateTime _appSourcesAt;

    /// <summary>A server for the suites under <paramref name="solutionDir"/>.</summary>
    public BddMcpServer(string solutionDir)
    {
        _solutionDir = Path.GetFullPath(solutionDir);
        _server.Initialize(new InitializeParams(null, null, new InitializationOptions(_solutionDir)));
    }

    /// <summary>The tools, as <c>tools/list</c> gives them.</summary>
    public static IReadOnlyList<(string Name, string Description, JsonObject Schema)> Tools { get; } =
    [
        ("list_suites", "The UAT and BDD suites of the solution: name, head, config, and whether it is built.", Schema()),
        ("list_steps", "The steps a suite's scenarios can use: keyword, phrase with {placeholders} and their types, and where it comes from (class, page, namespace, callable step). Use only these phrases in a scenario. 'query' keeps the steps holding every word of it.", Schema(("suite", "string", "A suite name; the first suite when left out."), ("query", "string", "Words the phrase must hold."))),
        ("list_pages", "A UI suite's pages and their controls: the names {page} and {control} take.", Schema(("suite", "string", "A suite name; the first suite when left out."))),
        ("list_test_sets", "A suite's test sets: the values of a feature file's TestData metadata.", Schema(("suite", "string", "A suite name; the first suite when left out."))),
        ("validate_scenario", "Checks a feature or step file as the xUnit gate would, with its words: every step must bind. Give 'path' (where the file is, or will be, in a suite's Features folder) and 'text' for a draft; without 'text' the file on disk is checked.", Schema(("path", "string", "The file's path; required."), ("text", "string", "The draft's text; the file on disk when left out."))),
        ("find_step_usages", "The steps across every suite that use a C# member: a step definition, a page phrase or a page object's control, as Namespace.Type.Member.", Schema(("symbol", "string", "Namespace.Type.Member; required."))),
        ("record_video", "Records a video script (*.video.md), or the scenario at 'line' of a feature file, on video with its voice-over: builds the suite, runs the scenes with BRUINET_RECORD, and gives the video, its captions (.srt) and its timeline. Needs ffmpeg; says how to get it when it is missing. Takes minutes: one scene is one scenario run.", Schema(("path", "string", "The video script or the feature file; required."), ("line", "integer", "The scenario heading's line, for a feature file."), ("suite", "string", "The suite a linked file runs in; its own when left out."), ("recordingsFolder", "string", "Where the video goes; the suite's TestResults/recordings when left out."), ("voice", "string", "The voice, when the script names none."))),
        ("review_page", "Writes the review page an agent hands over when a feature is done: what it did, what changed, and each recorded video with captions, chapters and every step's result. Give the videos record_video made. Returns the page's path, to open in a browser.", Schema(("title", "string", "What the work was; required."), ("summary", "string", "A paragraph or two on what was done."), ("changes", "array", "What changed, a line each; code spans in backticks are kept."), ("videos", "array", "The videos (.mp4) record_video made; required."), ("path", "string", "Where the page goes; beside the first video when left out."))),
        ("list_scraped_projects", "The UI test projects the Scraper records into (those with a scraper.config.md): name, folder, platform, start URL, recording sessions.", Schema()),
        ("get_scraper_progress", "Where a scraped project is in the workflow (1 Project, 2 Connect, 3 Collect pages, 4 Review, 5 Controls, 6 Page objects, 7 Verify, 8 Tests): each step's state (Done, Next, Todo, Attention) and summary, and the next thing to do, as the Scraper view shows it.", Schema(("project", "string", "The project; the only one when left out."))),
        ("start_recording", "Opens the recording browser (a Chromium window the user works in) on a scraped project's start URL, and starts a recording session in the project's Recordings folder. Each page the app moves to is captured once it settles. One recording at a time.", Schema(("project", "string", "The project; the only one when left out."), ("session", "string", "What the session is about, e.g. 'invoices'; required."), ("url", "string", "Where to start; the config's StartUrl when left out."), ("autoCapture", "boolean", "Capture each page the app moves to; true when left out."), ("actions", "boolean", "A test recording: what the user does is recorded too, and the browser has Assert and Stop; false when left out."))),
        ("capture_page", "Captures the page the recording browser shows now. Without a name, a page that did not change since its last capture is not captured again.", Schema(("name", "string", "What to call the page, e.g. 'Invoice edit'."))),
        ("stop_recording", "Closes the recording browser. The session stays in the project.", Schema()),
        ("list_recorded_pages", "A scraped project's recorded pages, by page key (the path and hash route with ids as {id}): name, location and every capture (session, number, when, node count), newest first.", Schema(("project", "string", "The project; the only one when left out."))),
        ("get_page_snapshot", "A captured page as an outline: one line per node that matters, '#<node> <type> \"text\" name=... role=... #id .class [data-*]', indented by depth; wrappers with nothing of their own are left out. The newest capture unless 'session' is given. Give 'node' for one node's subtree when the page is large.", Schema(("project", "string", "The project; the only one when left out."), ("page", "string", "The page key; required."), ("session", "string", "A session id, for an older capture."), ("number", "integer", "The capture's number in that session."), ("node", "integer", "A node's number: only its subtree."), ("maxLines", "integer", "At most this many lines; 400 when left out."))),
        ("find_similar_elements", "The nodes, on every recorded page's newest capture, of the same kind as a node: the same type, role, class names (generated ones left out) and child types. A pattern on many pages is a control to make.", Schema(("project", "string", "The project; the only one when left out."), ("page", "string", "The page key; required."), ("node", "integer", "The node's number on the page's newest capture; required."))),
        ("search_recordings", "The nodes, on every recorded page's newest capture, whose text, accessible name or an attribute value holds the text (ignoring case).", Schema(("project", "string", "The project; the only one when left out."), ("text", "string", "What to look for; required."))),
        ("list_controls", "The page objects and controls a scraped project has (and the scraped projects it references): name, kind, base, file, the header saying what it was made from, and each control property with its locator. Reuse these before making new ones.", Schema(("project", "string", "The project; the only one when left out."))),
        ("detect_patterns", "What the recorded pages are built from, found without a model: parts that repeat across pages or within one (controls to make), and forms, tables, lists, navigation and dialogs (structure a page object names). Each with its kind, on how many pages, how often, examples (page, node) and the control that already stands for it.", Schema(("project", "string", "The project; the only one when left out."), ("max", "integer", "At most this many; 30 when left out."))),
        ("control_brief", "Everything known about a pattern, to make a control from it: where to write the file and what to read (nothing else), where it occurs, an example outline with its parent, its parts with relative locators and in how many occurrences each is found, and hints (what the elements are, a base, members). Decide from the facts; the hints help.", Schema(("project", "string", "The project; the only one when left out."), ("signature", "string", "The pattern's signature, from detect_patterns; required."), ("variant", "string", "The pattern's variant, from detect_patterns, when it has several."), ("lifted", "boolean", "The pattern's lifted, from detect_patterns."), ("name", "string", "The control's name; the pattern's own when left out."))),
        ("match_controls", "Which of the project's controls occur on a page (by the signature in their header), the page object made for the page, and whether the page changed since it was made.", Schema(("project", "string", "The project; the only one when left out."), ("page", "string", "The page key; required."))),
        ("diff_page", "What changed on a recorded page between two captures: nodes added, removed and changed, as outline lines. By default from the capture its page object was made from (else the one before the newest) to the newest: what to change in the page object after the app changed.", Schema(("project", "string", "The project; the only one when left out."), ("page", "string", "The page key; required."), ("fromSession", "string", "The older capture's session."), ("fromNumber", "integer", "Its number in that session."), ("toSession", "string", "The newer capture's session; the newest when left out."), ("toNumber", "integer", "Its number in that session."))),
        ("suggest_controls", "For a recorded page (or one node's subtree of it): the members a page object would have, each with a name, the Bruinet control that fits (the Bruinet.Blazor one in a Blazor project), its namespace, and the locator in the preference order (test id, accessible name or text, stable id, stable class, position last) with whether it is stable and why. Names are unique on the page; a node in a frame gives the frame member (scope) to declare it on. A starting point: rename and drop as the page needs.", Schema(("project", "string", "The project; the only one when left out."), ("page", "string", "The page key; required."), ("node", "integer", "Only this node's subtree."))),
        ("scraped_header", "The first line a generated file must have, saying what it was made from: for a page object give the page, for a control also the node it was made from. Put it above the usings.", Schema(("project", "string", "The project; the only one when left out."), ("page", "string", "The page key; required."), ("node", "integer", "For a control: the node on the page's newest capture."))),
        ("write_code", "Writes a C# file (a page object or control) into the scraped project, at a path relative to it, when its syntax is valid; otherwise gives the syntax errors and writes nothing. Show the user the code first.", Schema(("project", "string", "The project; the only one when left out."), ("path", "string", "The file, relative to the project, e.g. Pages/InvoicePage.cs; required."), ("code", "string", "The whole file; required."))),
        ("list_recorded_actions", "A recording session's events in order: its page captures and, in a test recording, the user's actions (click, input, select, check, key, navigate, dialog, assert) with the node each acted on.", Schema(("project", "string", "The project; the only one when left out."), ("session", "string", "The session id; required."))),
        ("resolve_actions", "A test recording's actions resolved, from files only: each to the page object of its page, the member whose locator finds its node, the control's verb and the catalog phrase; and the gaps, each with why (NoPageObject, NoNode, NoMember, NoVerb, NoPhrase).", Schema(("project", "string", "The project; the only one when left out."), ("session", "string", "The session id; required."))),
        ("test_brief", "A gap of a test recording as a brief: what the user did, the element in its capture, what the gap needs (a member, with the one the rules would write; a verb; a page object), the one file to change, and how to check. Fill the gap in the page objects or controls, never in the drafted test; never invent a phrase.", Schema(("project", "string", "The project; the only one when left out."), ("session", "string", "The session id; required."), ("event", "integer", "The gap's event number; the first five gaps when left out."))),
        ("draft_test", "Drafts a test recording by rules: an xUnit test calling page objects (Tests/Recorded, with the person's part beside it) and/or a .feature.md scenario of catalog phrases (Features/Recorded), with the gaps marked. Drafted again over its earlier draft, never over a person's file.", Schema(("project", "string", "The project; the only one when left out."), ("session", "string", "The session id; required."), ("target", "string", "xunit, scenario or both; the config's ## Tests Target when left out."), ("name", "string", "The test's name; the session's when left out."))),
        ("run_test", "Runs each draft of a test recording once, built first: an xUnit draft by its class, a scenario by its address. Gives each run's counts and, when it did not pass, why and the end of the output. Takes a minute or more.", Schema(("project", "string", "The project; the only one when left out."), ("session", "string", "The session id; required."))),
        ("check_code", "Builds the scraped project with its analyzers and gives the errors and warnings. Generated code is done only when this passes: fix what it reports and call it again.", Schema(("project", "string", "The project; the only one when left out."))),
        ("explore_app", "Explores a scraped project's app by itself: visits its pages and the states safe clicks show, and captures each into a new recording session, within the project's Explore guards (which clicks, which requests, which hosts) and budget. Asks first: without 'confirm' it only gives the summary of host, guards and budget; show it to the person and call again with confirm: true once they agree. A dry run (the default) follows links only and lists what it would click. Gives why it stopped, the counts and the report (explore.md).", Schema(("project", "string", "The project; the only one when left out."), ("url", "string", "Where to start; the config's StartUrl when left out."), ("dryRun", "boolean", "Links only, nothing clicked; true when left out."), ("maxPages", "integer", "Overrides MaxPages."), ("maxDepth", "integer", "Overrides MaxDepth."), ("maxMinutes", "integer", "Overrides MaxMinutes."), ("headless", "boolean", "No browser window; false when left out. A sign-in page stops the run either way."), ("confirm", "boolean", "The person agreed to the summary; false when left out."))),
        ("draft_page_objects", "Drafts a page object for each recorded page (or one), from the suggestions and no model: <Name>.scraped.cs, a partial class a person adds to in <Name>.cs, which a draft never touches. Builds the project; a draft it does not build with is put back, with its errors. Gives each page's outcome, the members a new draft removed, and the repeated patterns to make controls of (with /bruinet-scrape-pages). A page with a page object made by hand is left alone.", Schema(("project", "string", "The project; the only one when left out."), ("page", "string", "A page key; every recorded page when left out."))),
        ("verify_page_objects", "Opens each recorded page in the running app (a browser of its own, under the Explore guards) and looks up every member of the project's page objects as Bruinet would: found once, found several times, or not found. Stop a recording first.", Schema(("project", "string", "The project; the only one when left out."), ("headless", "boolean", "No browser window; true when left out."))),
        ("update_scraper_config", "Makes one change to a scraped project's scraper.config.md, keeping the rest of the file: 'set' a field (values: field, value; in 'Danger words': category, true or false), 'add' a row (values: its cells: Explore hosts [Host], Never click and Always click [Match: 'text: ...' or 'selector: ...'], Never visit [Url], Allow requests [Method, Url], Aliases [Url]) or 'remove' one (values: its first cell). Asks first: without 'confirm' it says what it would write; call again with confirm: true once the person agrees.", Schema(("project", "string", "The project; the only one when left out."), ("action", "string", "set, add or remove; required."), ("section", "string", "The section without ##, e.g. Explore, Never click; required."), ("values", "array", "The values, as strings; required."), ("confirm", "boolean", "The person agreed; false when left out."))),
        ("create_scraped_project", "Makes a scraped UI test project in the solution's folder: its .csproj (referencing Bruinet.Html, and Bruinet.Blazor for blazor), its scraper.config.md and an empty Pages folder. It is not added to the solution. Asks first: without 'confirm' it gives what it would write, or what is wrong by field; call again with confirm: true once the person agrees.", Schema(("name", "string", "The project's name; required."), ("folder", "string", "Its folder, relative to the solution's; proposed when left out."), ("platform", "string", "html or blazor; html when left out."), ("startUrl", "string", "Where recording and Explore start; required."), ("testIds", "array", "The attributes that are test ids, preferred first."), ("references", "string", "projects or packages; proposed when left out."), ("confirm", "boolean", "The person agreed; false when left out."))),
        ("list_states", "The states the suite's app can be put in, declared with [BddState] in its test project: each state's phrase, its parameters and the method that sets it (through test data or the service head). A Given may name one; set_state puts the app in one.", Schema(("suite", "string", "The suite; its only one when left out."))),
        ("set_state", "Puts the suite's app in a declared state (list_states) on the suite's runner, against its own host and database, and leaves it paused there as a Step-through: the answer is where it paused; step_state and step_screenshot read the app, step_move 'stop' ends it. Only a declared state runs. Use it to look at a screen without clicking to it, or to try a page object call from a known start.", Schema(("state", "string", "The state as a Given says it, e.g. 'signed in as Alice'; required."), ("suite", "string", "The suite; its only one when left out."), ("testData", "string", "The test set to start from; the suite's DefaultTestData when left out."), ("page", "string", "The page the app is on in that state (list_pages): checked, and the page step_state then reads; none when left out."), ("timeoutMinutes", "integer", "How long to wait for the state, 1 to 10; 2 when left out."))),
        ("run_scenario", "Runs the scenario at 'line' of a feature file (an outline's every row), or every scenario of the file, on the suite's runner, and waits for the end: each scenario's status and, for one that failed, the step (file, line, text) and the message. Runs the feature file as it is on disk, with the suite's last build: give build: true after changing C# (page objects, steps, the app). Results are kept as the gate keeps them (last_runs).", Schema(("path", "string", "The feature file; required."), ("line", "integer", "A scenario heading's line; every scenario of the file when left out."), ("suite", "string", "The suite a linked file runs in; its own when left out."), ("build", "boolean", "Build the suite's project first; false when left out."), ("timeoutMinutes", "integer", "Cancel the run after this long; 10 when left out."), ("replay", "string", "record, replay or refresh: the run's HTTP and SQL replay mode (BRUINET_HTTP_REPLAY, BRUINET_SQL_REPLAY), for a suite that replays its dependencies; the suite's own when left out."), ("trace", "string", "all: read the page after every step for the trace, not only after a failed one (get_trace); off: no trace; each scenario is traced when left out."))),
        ("last_runs", "The newest result of each scenario of a feature file, from any run (run_scenario, the IDE, the xUnit gate): status, duration and, for one that failed, the message and the step's line.", Schema(("path", "string", "The feature file; required."), ("suite", "string", "The suite a linked file runs in; its own when left out."))),
        ("step_start", "Opens the scenario at 'line' on the suite's runner, paused before its first step, and gives where it is: file, line, step text, page, the step's arguments. Then step_move goes on one step (or into a call, out of one, to the end, or stops), each answering where it paused next, whether the step just run failed and why, or how the run ended. One Step-through at a time; no run_scenario meanwhile.", Schema(("path", "string", "The feature file; required."), ("line", "integer", "The scenario heading's line; required."), ("suite", "string", "The suite a linked file runs in; its own when left out."))),
        ("step_move", "Moves the paused Step-through: 'over' runs the next step (a call as one), 'into' goes into a call, 'out' to the end of the call, 'continue' to the end (or a breakpoint in the IDE's breakpoints file), 'stop' ends it. Answers where it paused next, or how it ended. A step still running after the wait answers Waiting: then 'wait' waits again (and gives a pause that came meanwhile), or 'stop'. The server answers nothing else while it waits.", Schema(("move", "string", "over, into, out, continue, wait or stop; required."), ("timeoutMinutes", "integer", "How long to wait for the pause, 1 to 10; 2 when left out."))),
        ("step_state", "The paused Step-through's current page: each control of its page object, its kind, whether it is shown, and what it shows (a toggle's state, a range's value, a selector's choice, or text). Read without a screenshot; where to look when a step is about to fail.", Schema()),
        ("step_screenshot", "A screenshot of the app while the Step-through is paused: the file's path, to look at.", Schema()),
        ("coverage", "Which lines of the app and the suite one scenario runs: it runs the scenario at 'line' through the xUnit gate (dotnet test, by its address) with .NET's code coverage, and gives, per file, the lines hit and missed as ranges. For the given files, or the files changed in the working tree when left out. Proves a new scenario reaches the code it is about, or shows lines a passing one never ran (lines, not branches). Slower than run_scenario: a build and a process per call.", Schema(("path", "string", "The feature file; required."), ("line", "integer", "The scenario heading's line; required."), ("files", "array", "Source files to report, relative to the solution; the working tree's changed files when left out."), ("suite", "string", "The suite a linked file runs in; its own when left out."), ("build", "boolean", "Build the suite first; true when left out."), ("head", "string", "BRUINET_HEAD for the run (headless, bunit, web, ...); the server's own when left out."), ("timeoutMinutes", "integer", "End the run after this long; 10 when left out."))),
        ("why", "Why a scenario that passed before fails now: its newest failing run against its newest earlier passing run, step by step. The first step where they part (page, controls driven, what the app said, what the page showed, status), a kind (page-changed, data-changed, timing-only, scenario-changed), steps that got much slower, and the files changed since the passing run's commit that touch that step or an earlier one. SQL is not compared (the trace has none); page values only when both runs read pages (trace: all). Where to start before reading code.", Schema(("path", "string", "The feature file; required."), ("line", "integer", "The scenario heading's line; required."), ("suite", "string", "The suite a linked file runs in; its own when left out."))),
        ("get_trace", "What a scenario's newest traced run did, step by step: each step's page, control calls (a slow call or many attempts is a wait), app events, and the page's controls after the failed step. Without step: one row per step and the failed step in full; with step (a step's line): that step in full. Every run that passed or failed is traced; the page after every step only with run_scenario's trace: all.", Schema(("path", "string", "The feature file; required."), ("line", "integer", "The scenario heading's line; required."), ("step", "integer", "A step's line: that step in full."), ("suite", "string", "The suite a linked file runs in; its own when left out."))),
        ("where_is", "Where a page object's control is made: the page object member's file and line, the locator value it finds by, and the lines of the app's sources (.razor, .cshtml, XAML, code) that render an element with that value, test-id attributes first. Where to change the app or the page object when a control is not found or wrong.", Schema(("page", "string", "The page, as list_pages names it; required."), ("control", "string", "The control, as list_pages names it; required."), ("suite", "string", "The suite; its only one when left out."))),
        ("get_failure", "Why a scenario failed in its newest run (any run: run_scenario, the IDE, the xUnit gate): the failed step (file, line, text, duration), the kind (assertion, locator-not-found, timeout, app-unavailable, bind, app-exception, error), expected and actual, the locator, the step's binding, the suite's and app's code frames it came through, the screenshot, the message, and the app source lines that make the element its locator names (appSource; where_is gives one control's). run_scenario gives it for each failed scenario already.", Schema(("path", "string", "The feature file; required."), ("line", "integer", "The scenario heading's line; the file's newest failed scenario when left out."), ("suite", "string", "The suite a linked file runs in; its own when left out."))),
        ("http_calls", "The HTTP calls a scenario made in its newest run that recorded or replayed them (AI11): method, URL, status and whether the recording answered it (recorded, matched, missed), the misses, and the recorded calls it no longer makes. Shows why a replayed scenario failed on its backend.", Schema(("path", "string", "The feature file; required."), ("line", "integer", "The scenario heading's line; required."), ("suite", "string", "The suite a linked file runs in; its own when left out."))),
        ("failing", "Every scenario whose newest run failed, in one suite or all of them: file, scenario, address, when, the kind of failure and the start of its message. Where to start when tests fail; get_failure gives one in full.", Schema(("suite", "string", "A suite's name; every suite when left out."))),
        ("step_log", "What the app said during each step of a scenario's newest run (any run): its log's warnings and errors, the browser's console errors and warnings, by step (file and line; at most 20 steps). Where to look when a step passed but the app complained, or failed for a reason the UI does not show.", Schema(("path", "string", "The feature file; required."), ("line", "integer", "The scenario heading's line; required."), ("suite", "string", "The suite a linked file runs in; its own when left out."))),
        ("tests_affected", "The tests and scenarios of a UI suite that a change reaches, each with why, and one dotnet test --filter that runs them; or that the whole suite must run, and why. From the suite's touch map (touch-map.json, written by 'bruinet tests touch-map' after a full run). The change is git's since 'base' with the working tree, or the given files.", Schema(("suite", "string", "A suite's name, or a test project's folder relative to the solution; required."), ("base", "string", "Changed since this commit; HEAD (the working tree's changes) when left out."), ("files", "array", "These changed files instead of git's, relative to the solution."), ("always", "array", "Filter terms run whatever changed, e.g. Gate=Smoke."))),
        ("rename_step", "Renames a callable step (a '## Step:' heading in a steps file) and every call of it across the suites, each call keeping its values. Give the heading's or a call's line. Without apply: true it only gives the edits (file, line, before, after); with it, it writes them, then checks every changed file as validate_scenario does and puts all back when one fails.", Schema(("path", "string", "The steps file or a feature file with a call; required."), ("line", "integer", "The heading's or the call's line; required."), ("newPhrase", "string", "The new phrase, with the same {placeholders}; required."), ("apply", "boolean", "Write the edits; false when left out."))),
        ("rename_page_object", "Renames a scraped project's page object for a recorded page: its class, its draft and the person's part, in the files that declare it. Files that only use it are listed, not changed: fix them, then check_code. Asks first: without 'confirm' it only says what it would do.", Schema(("project", "string", "The project; the only one when left out."), ("page", "string", "The page key; required."), ("newName", "string", "The new class name; required."), ("confirm", "boolean", "The person agreed; false when left out."))),
        ("delete_results", "Deletes old scenario results from every run, for good: of a scenario (the heading at 'line' of 'path'), a feature file ('path') or a suite ('suite'). Only scenario reports and their screenshots; a run left empty goes. Counts only (dryRun) unless 'dryRun' is false.", Schema(("scope", "string", "scenario, file or suite; required."), ("path", "string", "The feature file, for a scenario or a file."), ("line", "integer", "The scenario heading's line, for a scenario."), ("suite", "string", "The suite, for a suite (or the one a linked file is picked in)."), ("dryRun", "boolean", "Only count; true when left out."))),
    ];

    /// <summary>Serves until the input ends.</summary>
    public async Task RunAsync(TextReader input, TextWriter output, CancellationToken cancellationToken = default)
    {
        while (await input.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (line.Trim().Length == 0)
            {
                continue;
            }

            if (Handle(line) is { } response)
            {
                await output.WriteLineAsync(response.ToJsonString()).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>One message in, its response out; null for a notification.</summary>
    public JsonObject? Handle(string message)
    {
        JsonObject request;
        try
        {
            request = JsonNode.Parse(message)?.AsObject() ?? throw new JsonException("Not an object.");
        }
        catch (JsonException exception)
        {
            return Error(null, -32700, $"Not JSON: {exception.Message}");
        }

        var id = request["id"]?.DeepClone();
        var method = (string?)request["method"];
        if (id is null)
        {
            return null;
        }

        try
        {
            JsonNode result = method switch
            {
                "initialize" => new JsonObject
                {
                    ["protocolVersion"] = ProtocolVersion,
                    ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
                    ["serverInfo"] = new JsonObject { ["name"] = "bruinet-bdd", ["version"] = typeof(BddMcpServer).Assembly.GetName().Version?.ToString() ?? "0" },
                    ["instructions"] = "Bruinet BDD: use list_steps before writing a scenario step, and validate_scenario before proposing a feature file. When tests fail, failing names them in one call. To check a scenario works, run it with run_scenario (build: true after changing C#): a failed one comes with its failure (kind, expected and actual, locator, binding, the code frames to look in), get_failure gives it again; last_runs gives the newest results; coverage shows which lines of the app one scenario runs; where_is gives the page object line of a control and the app line (razor, XAML) that makes it; get_trace gives what a failed run did step by step (each step's page, control calls and waits, app events, the page after the failure); for a scenario that passed before, why gives where its failing run first parted from the last green one and the files changed since that touch it; to look at a failure as it happens, step through it with step_start and step_move; to look at a screen without clicking to it, set_state puts the app in a declared state (list_states); a suite that replays its dependencies runs with run_scenario's replay, and http_calls shows the HTTP calls a replayed scenario made and missed; after a change, tests_affected names the tests it reaches. Never invent a phrase: leave a step that has none as a marked gap. When a feature is done: record its scenarios (record_video) and hand over a review_page. Page objects from recorded pages: list_recorded_pages, get_page_snapshot and find_similar_elements. A recorded test: resolve_actions, test_brief per gap (fill it in page objects, never in the draft), draft_test, run_test; the user browses in the recording browser, never sign in for them.",
                },
                "ping" => new JsonObject(),
                "tools/list" => new JsonObject
                {
                    ["tools"] = new JsonArray([.. Tools.Select(tool => (JsonNode)new JsonObject
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["inputSchema"] = tool.Schema.DeepClone(),
                    })]),
                },
                "tools/call" => Call((string?)request["params"]?["name"] ?? string.Empty, request["params"]?["arguments"] as JsonObject ?? []),
                _ => throw new McpException(-32601, $"No method '{method}'."),
            };
            return new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };
        }
        catch (McpException exception)
        {
            return Error(id, exception.Code, exception.Message);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _server.Dispose();

    // A tool's answer is text: JSON an agent reads, or the reason it could not answer (isError).
    private JsonObject Call(string name, JsonObject arguments)
    {
        try
        {
            object answer = name switch
            {
                "list_suites" => ListSuites(),
                "list_steps" => ListSteps(Suite(arguments), (string?)arguments["query"]),
                "list_states" => ListStates(Suite(arguments)),
                "set_state" => SetState(arguments),
                "list_pages" => ListPages(Suite(arguments)),
                "list_test_sets" => Suite(arguments).TestDataDefinitions().SelectMany(definition => definition.TestSetNames).Distinct().ToList(),
                "validate_scenario" => Validate(Required(arguments, "path"), (string?)arguments["text"]),
                "find_step_usages" => _server.Usages(new UsagesParams(Required(arguments, "symbol"))),
                "run_scenario" => RunScenario(arguments),
                "coverage" => Coverage(arguments),
                "step_start" => StepStart(arguments),
                "step_move" => StepMove(arguments),
                "step_state" => _server.StepReadPageAsync().GetAwaiter().GetResult() is var read && read.State is { } state
                    ? new { state.Page, state.Reason, Controls = state.Controls.Take(60).Select(control => new { control.Name, control.Kind, control.Shown, control.Value, control.Unreadable }), More = state.Controls.Count > 60 ? state.Controls.Count - 60 : (int?)null }
                    : throw new ToolProblem($"No page state: {read.Reason}"),
                "step_screenshot" => _server.StepScreenshotAsync().GetAwaiter().GetResult() is var shot && shot.Path is { } taken ? new { Screenshot = taken } : throw new ToolProblem($"No screenshot: {shot.Reason}"),
                "last_runs" => LastRuns(arguments),
                "get_failure" => GetFailure(arguments),
                "where_is" => WhereIs(arguments),
                "get_trace" => GetTrace(arguments),
                "why" => Why(arguments),
                "http_calls" => HttpCalls(arguments),
                "failing" => Failing(arguments),
                "step_log" => StepLogOf(arguments),
                "tests_affected" => TestsAffected(arguments),
                "rename_step" => RenameStep(arguments),
                "rename_page_object" => Scraper(() => RenamePageObject(arguments)),
                "delete_results" => DeleteResults(arguments),
                "record_video" => RecordVideo(arguments),
                "review_page" => ReviewPage(arguments),
                "list_scraped_projects" => Scraper(() => _server.ScraperProjects()),
                "get_scraper_progress" => Scraper(() => _server.Scraper.Progress((string?)arguments["project"])),
                "start_recording" => Scraper(() => _server.Scraper.StartRecordingAsync((string?)arguments["project"], Required(arguments, "session"), (string?)arguments["url"], (bool?)arguments["autoCapture"] ?? true, actions: (bool?)arguments["actions"] ?? false).GetAwaiter().GetResult()),
                "list_recorded_actions" => Scraper(() => _server.Scraper.RecordedActions((string?)arguments["project"], Required(arguments, "session"))),
                "resolve_actions" => Scraper(() => _server.Scraper.ResolveActions((string?)arguments["project"], Required(arguments, "session"))),
                "test_brief" => Scraper(() => string.Join("\n\n---\n\n", _server.Scraper.TestBriefs((string?)arguments["project"], Required(arguments, "session"), (int?)arguments["event"]))),
                "draft_test" => Scraper(() => _server.ScraperDraftTest(new ScraperTestParams((string?)arguments["project"], Required(arguments, "session"), (string?)arguments["target"], (string?)arguments["name"])) switch { { Problem: { } problem } => throw new ScraperProblem(problem), var drafted => drafted.Value! }),
                "run_test" => Scraper(() => _server.ScraperRunTest(new ScraperTestParams((string?)arguments["project"], Required(arguments, "session"), null, null)).GetAwaiter().GetResult() switch { { Problem: { } problem } => throw new ScraperProblem(problem), var runs => runs.Value! }),
                "capture_page" => Scraper(() => (object?)_server.Scraper.CapturePageAsync((string?)arguments["name"]).GetAwaiter().GetResult() ?? new { Captured = false, Reason = "The page did not change since its last capture; give a name to capture it anyway." }),
                "stop_recording" => Scraper(() => (object?)_server.Scraper.StopRecordingAsync().GetAwaiter().GetResult() ?? new { Stopped = false, Reason = "No recording was in progress." }),
                "list_recorded_pages" => Scraper(() => _server.ScraperPages(new ScraperProjectParams((string?)arguments["project"])) switch { { Problem: { } problem } => throw new ScraperProblem(problem), var pages => pages.Value! }),
                "get_page_snapshot" => Scraper(() => SnapshotOutline.Render(_server.Scraper.Snapshot((string?)arguments["project"], Required(arguments, "page"), (string?)arguments["session"], (int?)arguments["number"]).Snapshot, (int?)arguments["node"] ?? 0, (int?)arguments["maxLines"] ?? 400)),
                "find_similar_elements" => Scraper(() => _server.Scraper.Similar((string?)arguments["project"], Required(arguments, "page"), (int?)arguments["node"] ?? throw new ToolProblem("'node' is required."))),
                "list_controls" => Scraper(() => _server.Scraper.Controls((string?)arguments["project"])),
                "detect_patterns" => Scraper(() => _server.Scraper.Patterns((string?)arguments["project"], (int?)arguments["max"] ?? 30)),
                "control_brief" => Scraper(() => _server.Scraper.ControlBrief((string?)arguments["project"], Required(arguments, "signature"), (string?)arguments["name"], (string?)arguments["variant"], (bool?)arguments["lifted"] ?? false).Text),
                "match_controls" => Scraper(() => _server.Scraper.Match((string?)arguments["project"], Required(arguments, "page"))),
                "diff_page" => Scraper(() => _server.Scraper.Diff((string?)arguments["project"], Required(arguments, "page"), (string?)arguments["fromSession"], (int?)arguments["fromNumber"], (string?)arguments["toSession"], (int?)arguments["toNumber"])),
                "suggest_controls" => Scraper(() => _server.Scraper.Suggest((string?)arguments["project"], Required(arguments, "page"), (int?)arguments["node"])),
                "scraped_header" => Scraper(() => _server.Scraper.Header((string?)arguments["project"], Required(arguments, "page"), (int?)arguments["node"])),
                "write_code" => Scraper(() => WriteCode(arguments)),
                "check_code" => Scraper(() => _server.Scraper.CheckCodeAsync((string?)arguments["project"]).GetAwaiter().GetResult()),
                "explore_app" => Scraper(() => ExploreApp(arguments)),
                "update_scraper_config" => Scraper(() => UpdateConfig(arguments)),
                "create_scraped_project" => Scraper(() => CreateProject(arguments)),
                "draft_page_objects" => Scraper(() => _server.Scraper.DraftAsync((string?)arguments["project"], (string?)arguments["page"]).GetAwaiter().GetResult()),
                "verify_page_objects" => Scraper(() => _server.Scraper.VerifyAsync((string?)arguments["project"], visible: !((bool?)arguments["headless"] ?? true)).GetAwaiter().GetResult()),
                "search_recordings" => Scraper(() => _server.Scraper.Search((string?)arguments["project"], Required(arguments, "text"))),
                _ => throw new McpException(-32602, $"No tool '{name}'. The tools are {string.Join(", ", Tools.Select(tool => tool.Name))}."),
            };
            // Text (an outline) as it is; anything else as JSON.
            return Text(answer as string ?? JsonSerializer.Serialize(answer, Json), error: false);
        }
        catch (ToolProblem problem)
        {
            return Text(problem.Message, error: true);
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or McpException))
        {
            // Never the server's end: an argument of the wrong kind, or a fault, is the tool's answer.
            return Text($"{name} failed: {exception.GetType().Name}: {exception.Message}", error: true);
        }
    }

    // The Scraper says why it cannot answer as a problem; the agent reads it as the tool's result.
    private static object Scraper(Func<object> work)
    {
        try
        {
            return work();
        }
        catch (Exception exception) when (exception is ScraperProblem or FormatException or FileNotFoundException or InvalidOperationException or Microsoft.Playwright.PlaywrightException)
        {
            throw new ToolProblem(exception.Message);
        }
    }

    // Asks first: the summary until the person agreed, then the run.
    private object ExploreApp(JsonObject arguments)
    {
        var options = new Bruinet.Scraper.Explore.ExploreOptions
        {
            DryRun = (bool?)arguments["dryRun"] ?? true,
            Url = (string?)arguments["url"],
            MaxPages = (int?)arguments["maxPages"],
            MaxDepth = (int?)arguments["maxDepth"],
            MaxMinutes = (int?)arguments["maxMinutes"],
            Visible = !((bool?)arguments["headless"] ?? false),
            StopAtSignIn = true,
        };
        var project = (string?)arguments["project"];
        var summary = _server.Scraper.ExploreSummary(project, options);
        if ((bool?)arguments["confirm"] != true)
            return new { Started = false, Summary = summary, Next = "Show the summary to the person. Call again with confirm: true once they agree." };
        var result = _server.Scraper.ExploreAsync(project, options).GetAwaiter().GetResult();
        return new
        {
            result.Stop,
            result.StopDetail,
            result.WithinGuards,
            result.DryRun,
            Captures = result.Captures.Count,
            result.Clicks,
            Skipped = result.Skipped.Count,
            Blocked = result.Blocked.Count,
            result.Report,
            Session = result.Folder is null ? null : Path.GetFileName(result.Folder),
        };
    }

    private object UpdateConfig(JsonObject arguments)
    {
        var values = arguments["values"] as JsonArray ?? throw new ToolProblem("'values' is required: an array of strings.");
        var change = new Bruinet.Scraper.Projects.ConfigChange(Required(arguments, "action"), Required(arguments, "section"), [.. values.Select(v => (string?)v ?? "")]);
        var confirmed = (bool?)arguments["confirm"] == true;
        var edit = _server.Scraper.EditConfig((string?)arguments["project"], change, write: confirmed);
        return new
        {
            edit.Changed,
            edit.Description,
            Written = confirmed && edit.Changed,
            Next = confirmed || !edit.Changed ? null : "Tell the person what would change. Call again with confirm: true once they agree.",
        };
    }

    private object CreateProject(JsonObject arguments)
    {
        var name = Required(arguments, "name");
        var defaults = _server.Scraper.NewProjectDefaults(name);
        var request = new Bruinet.Scraper.Projects.NewProject(
            name,
            (string?)arguments["folder"] ?? defaults.Folder,
            (string?)arguments["platform"] ?? "html",
            Required(arguments, "startUrl"),
            (arguments["testIds"] as JsonArray)?.Select(v => (string?)v ?? "").ToList() ?? [],
            (string?)arguments["references"] is { } references
                ? (string.Equals(references, "packages", StringComparison.OrdinalIgnoreCase) ? Bruinet.Scraper.Projects.BruinetReferences.Packages : Bruinet.Scraper.Projects.BruinetReferences.Projects)
                : defaults.References);
        var problems = _server.Scraper.NewProjectProblems(request);
        if (problems.Count > 0 || (bool?)arguments["confirm"] != true)
        {
            return new { Written = false, Request = request, Problems = problems, Next = problems.Count > 0 ? "Fix what is wrong, then ask again." : "Tell the person what would be written. Call again with confirm: true once they agree." };
        }

        var project = _server.Scraper.NewProject(request);
        return new { Written = true, Project = project.Name, project.Folder, Next = "Add the project to the solution (dotnet sln add), then record or explore it." };
    }

    // A file that is not written is a tool error with the reason and each syntax error's place.
    private object WriteCode(JsonObject arguments)
    {
        var result = _server.Scraper.WriteCode((string?)arguments["project"], Required(arguments, "path"), Required(arguments, "code"));
        return result.Written
            ? result
            : throw new ToolProblem(result.Reason + string.Concat(result.Problems.Select(problem => $"{Environment.NewLine}line {problem.Line}, column {problem.Column}: {problem.Code} {problem.Message}")));
    }

    // The preview's Run, waited for: the suite built first when asked, then how each scenario went.
    private object RunScenario(JsonObject arguments)
    {
        var uri = FileUri(Required(arguments, "path"));
        var suite = (string?)arguments["suite"];
        if ((bool?)arguments["build"] == true)
        {
            var project = _server.Context(new Uri(uri).LocalPath, suite).Suite?.ProjectFile ?? throw new ToolProblem("No suite with a project file holds or links this feature file.");
            var build = Run("dotnet", ["build", project, "-v:minimal", "-nologo"], new Dictionary<string, string>());
            if (build.Code != 0)
            {
                throw new ToolProblem($"The build failed, so nothing ran:{Environment.NewLine}{build.Tail}");
            }

            _server.BuildDone();
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(Whole(arguments, "timeoutMinutes", 1, 120) ?? 10));
        var (started, ended) = _server.RunToEndAsync(new RunStartParams(uri, Whole(arguments, "line", 1, int.MaxValue), suite, RunEnvironment(arguments)), timeout.Token).GetAwaiter().GetResult();
        if (ended is null)
        {
            throw new ToolProblem(started.Reason ?? "The run did not start.");
        }

        // The run's reports are written: a failed scenario's failure from its own.
        var runs = ended.Results.Any(result => result.Status == "failed") ? _server.NewestRuns(uri, suite) : null;
        return new
        {
            started.Suite,
            Status = ended.Status == "canceled" && timeout.IsCancellationRequested ? "timed out" : ended.Status,
            Message = Cap(ended.Message, 2000),
            Scenarios = ended.Results.Select(result => new
            {
                result.Scenario,
                result.Address,
                result.Status,
                result.DurationMs,
                FailedStep = result.FailedStep is { } step ? new { File = step.File is null ? null : Path.GetRelativePath(_solutionDir, step.File), step.Line, step.Text } : null,
                Failure = runs?.GetValueOrDefault(result.Scenario) is { } run ? Failure(run) : null,
                AppSource = runs?.GetValueOrDefault(result.Scenario) is { } failedRun ? AppSource(Failure(failedRun), SuiteFolder(uri, suite)) : null,
                Message = Cap(result.Message, 2000),
            }),
        };
    }

    // The replay mode a run asks for, as the variables both replays read; SQL replay has no refresh, so it records.
    // The run's variables: its replay mode and its trace (BRUINET_TRACE); a warm runner with other ones is not reused.
    private static Dictionary<string, string>? RunEnvironment(JsonObject arguments)
    {
        var environment = ReplayEnvironment((string?)arguments["replay"]);
        if ((string?)arguments["trace"] is { } trace)
        {
            environment ??= [];
            environment[Bruinet.Core.Logging.StepTrace.Variable] = trace.Trim().ToLowerInvariant() switch
            {
                "all" => "all",
                "off" => "off",
                "failed" => "failed",
                _ => throw new ToolProblem($"trace '{trace}': all, failed or off."),
            };
        }

        return environment;
    }

    private static Dictionary<string, string>? ReplayEnvironment(string? replay)
        => replay?.Trim().ToLowerInvariant() switch
        {
            null or "" => null,
            "record" => new() { [Bruinet.Mocking.Http.HttpReplaySession.ModeVariable] = "Record", [Bruinet.Mocking.Data.Replay.ReplayRegistry.ModeVariable] = "Record" },
            "replay" => new() { [Bruinet.Mocking.Http.HttpReplaySession.ModeVariable] = "Replay", [Bruinet.Mocking.Data.Replay.ReplayRegistry.ModeVariable] = "Replay" },
            "refresh" => new() { [Bruinet.Mocking.Http.HttpReplaySession.ModeVariable] = "Refresh", [Bruinet.Mocking.Data.Replay.ReplayRegistry.ModeVariable] = "Record" },
            _ => throw new ToolProblem($"replay '{replay}': record, replay or refresh."),
        };

    // A scenario's call log, beside its HTTP recording (HttpReplaySession.PathFor): found under the suite's project by name.
    private object HttpCalls(JsonObject arguments)
    {
        var uri = FileUri(Required(arguments, "path"));
        var line = Whole(arguments, "line", 1, int.MaxValue) ?? throw new ToolProblem("'line' is required.");
        var name = _server.ScenarioNames(uri, line).FirstOrDefault() ?? throw new ToolProblem("There is no scenario heading at that line.");
        var project = _server.Context(new Uri(uri).LocalPath, (string?)arguments["suite"]).Suite?.ProjectDirectory
            ?? throw new ToolProblem("No suite holds or links this feature file.");
        var file = Path.GetFileName(Bruinet.Mocking.Http.HttpCallLog.PathFor(Bruinet.Mocking.Http.HttpReplaySession.PathFor(project, name)));
        var path = Directory.EnumerateFiles(project, file, SearchOption.AllDirectories)
            .Where(found => !found.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                            && !found.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault()
            ?? throw new ToolProblem($"No HTTP calls of '{name}' yet: run it with run_scenario and replay set, in a suite whose host gives the app an HttpReplaySession.");
        var log = Bruinet.Mocking.Http.HttpCallLog.Load(path)!;
        return new
        {
            Scenario = name,
            Log = Path.GetRelativePath(_solutionDir, path),
            log.Mode,
            Calls = log.Calls.Take(50).Select(call => new { call.Method, Url = Cap(call.Url, 200), call.Status, call.Outcome }),
            More = Math.Max(0, log.Calls.Count - 50),
            Misses = log.Misses.Take(5).Select(miss => Cap(miss, 500)),
            UnusedExchanges = log.Unused.Take(20),
        };
    }

    // The suite's declared states: [BddState] phrases, each a Given. None is an empty list.
    private static object ListStates(BddSuite suite)
        => Catalogs(suite).Default.Patterns
            .Where(pattern => pattern.IsState)
            .Select(pattern => new
            {
                pattern.Phrase,
                Parameters = pattern.Parameters.Select(parameter => parameter.ToString()).ToList(),
                From = pattern.CommandId,
            })
            .OrderBy(state => state.Phrase, StringComparer.OrdinalIgnoreCase)
            .ToList();

    // A declared state, set by a Step-through of a scenario of its own: the state's Given (and the page check), then a step
    // that does nothing, where it stays paused. Only a declared state runs: the text binds to its state, in the catalog the
    // runner binds with, and no argument can add a line or a table cell to the scenario.
    private object SetState(JsonObject arguments)
    {
        var suite = Suite(arguments);
        var state = OneLine(Required(arguments, "state"), "state");
        if (state.StartsWith("Given ", StringComparison.OrdinalIgnoreCase))
        {
            state = state["Given ".Length..].Trim();
        }

        var page = (string?)arguments["page"] is { Length: > 0 } named ? OneLine(named, "page") : null;
        var testData = (string?)arguments["testData"] is { Length: > 0 } set ? OneLine(set, "testData") : null;
        if (testData is not null && !suite.TestDataDefinitions().SelectMany(definition => definition.TestSetNames).Contains(testData, StringComparer.Ordinal))
        {
            throw new ToolProblem($"'{testData}' is not a test set of {suite.Name}: list_test_sets names them.");
        }

        var catalog = Catalogs(suite).Default;
        var step = new BddStep(BddStepKeyword.Given, Bruinet.Core.Testing.BddEffectiveStepKeyword.Given, state, null, new BddSourceLocation(null, 1));
        if (!catalog.Patterns.Any(pattern => pattern.IsState && pattern.TryMatch(step) is not null))
        {
            throw new ToolProblem($"'{state}' is not a declared state of {suite.Name}: list_states names them.");
        }

        // The runner binds the step against every phrase: a more specific Given that is no state would win there.
        var bound = catalog.Match(step);
        if (bound is not [{ Pattern.IsState: true } match])
        {
            throw new ToolProblem($"'{state}' binds to {string.Join(" and ", bound.Select(other => other.Pattern.CommandId))}, not to a state alone: say the state as list_states gives it.");
        }

        var metadata = new List<string>();
        if (testData is not null)
        {
            metadata.Add($"| {BddMetadataFields.TestData} | {testData} |");
        }

        if (match.Pattern.Namespace.Length > 0)
        {
            metadata.Add($"| {BddMetadataFields.Using} | {match.Pattern.Namespace} |");
        }

        var lines = new List<string> { "# Feature: Set state", string.Empty };
        if (metadata.Count > 0)
        {
            lines.AddRange(["## Metadata", string.Empty, "| Field | Value |", "| --- | --- |", .. metadata, string.Empty]);
        }

        var line = lines.Count + 1;
        // With a page, the built-in check of it, which also makes it the page step_state reads.
        lines.AddRange([$"## Scenario: {state}", string.Empty, "```gherkin", $"Given {state}"]);
        if (page is not null)
        {
            lines.Add($"Then I should be on the {page} page");
        }

        lines.AddRange([$"Then {Bruinet.Uat.UatReflectionRuntime.PausePhrase}", "```", string.Empty]);

        // In the suite's project, so the suite holds it; a file of this call's own, gone once the runner has read it, so
        // no run, preview or second call finds it.
        var path = Path.Combine(suite.ProjectDirectory, ".bruinet", $"set-state-{Guid.NewGuid():N}.feature.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Join("\n", lines));
        try
        {
            StepStart(new JsonObject { ["path"] = path, ["line"] = line, ["suite"] = suite.Name });
        }
        finally
        {
            File.Delete(path);
        }

        var timeout = Whole(arguments, "timeoutMinutes", 1, 10) ?? 2;
        var paused = StepMove(new JsonObject { ["move"] = "over", ["timeoutMinutes"] = timeout });
        if (page is null)
        {
            // The agent check's first try: paused with no page, step_state had nothing to read.
            if (paused is JsonObject { } noPage && noPage["paused"] is not null && noPage["page"] is null)
            {
                noPage["next"] = "No page is current, so step_state reads nothing: give 'page' (list_pages names them) to set it.";
            }

            return paused;
        }

        return paused is not JsonObject { } after || after["lastStepFailed"] is not null || after["paused"] is null
            ? paused
            : StepMove(new JsonObject { ["move"] = "over", ["timeoutMinutes"] = timeout });
    }

    // A value written into a scenario: one line, no table bar or backtick, so it adds no step, cell or code fence.
    private static string OneLine(string value, string name)
        => value.Any(character => char.IsControl(character) || character is '|' or '`')
            ? throw new ToolProblem($"'{name}' must be one line, with no | or `.")
            : value.Trim();

    // A Step-through, waited for: where it paused, or how it ended.
    private object StepStart(JsonObject arguments)
    {
        var uri = FileUri(Required(arguments, "path"));
        var line = Whole(arguments, "line", 1, int.MaxValue) ?? throw new ToolProblem("'line' is required.");
        var upcoming = TakeStepEvent();
        var started = _server.StepStartAsync(new StepStartParams(uri, line, (string?)arguments["suite"], null)).GetAwaiter().GetResult();
        return started.Started ? StepEvent(upcoming, TimeSpan.FromMinutes(2), untilEnded: false) : throw new ToolProblem(started.Reason ?? "The Step-through did not start.");
    }

    private object StepMove(JsonObject arguments)
    {
        var move = Required(arguments, "move");
        var wait = TimeSpan.FromMinutes(Whole(arguments, "timeoutMinutes", 1, 10) ?? 2);

        // A pause that came after a Waiting answer is given now; otherwise the next one is waited for.
        if (move == "wait")
        {
            var (count, last) = _server.LastStepEvent;
            if (count > Interlocked.Read(ref _stepEventsSeen) && last is not null)
            {
                Interlocked.Exchange(ref _stepEventsSeen, count);
                return Describe(last);
            }

            return StepEvent(TakeStepEvent(), wait, untilEnded: false);
        }

        var upcoming = TakeStepEvent();
        var moved = _server.StepMove(new StepMoveParams(move));
        return moved.Taken
            ? StepEvent(upcoming, wait, untilEnded: move == "stop")
            : throw new ToolProblem(moved.Reason ?? "The move was not taken.");
    }

    private Task<object> TakeStepEvent()
    {
        Interlocked.Exchange(ref _stepEventsSeen, _server.LastStepEvent.Count);
        return _server.UpcomingStepEvent;
    }

    // The next event; for a stop, the end (a pause raised as the stop came is passed over).
    private object StepEvent(Task<object> upcoming, TimeSpan wait, bool untilEnded)
    {
        var deadline = DateTime.UtcNow + wait;
        while (true)
        {
            var left = deadline - DateTime.UtcNow;
            if (left <= TimeSpan.Zero || !upcoming.Wait(left))
            {
                return new { Waiting = true, Next = "The step is still running: step_move 'wait' waits again, 'stop' ends it." };
            }

            Interlocked.Exchange(ref _stepEventsSeen, _server.LastStepEvent.Count);
            if (!untilEnded || upcoming.Result is StepEndedParams)
            {
                return Describe(upcoming.Result);
            }

            upcoming = _server.UpcomingStepEvent;
        }
    }

    // Only what is there: an agent reads every field.
    private JsonObject Describe(object stepEvent)
    {
        JsonObject answer = stepEvent switch
        {
            StepPausedParams paused => new()
            {
                ["paused"] = true,
                ["file"] = paused.At is { } at ? Path.GetRelativePath(_solutionDir, new Uri(at).LocalPath) : null,
                ["line"] = paused.Line,
                ["text"] = paused.Text,
                ["depth"] = paused.Depth,
                ["page"] = paused.Page,
                ["app"] = paused.App,
                ["arguments"] = paused.Arguments is { Count: > 0 } values ? new JsonObject([.. values.Select(pair => new KeyValuePair<string, JsonNode?>(pair.Key, Cap(pair.Value, 300)))]) : null,
                ["lastStepFailed"] = paused.Failed ? true : null,
                ["message"] = Cap(paused.Message, 2000),
            },
            StepEndedParams ended => new() { ["ended"] = true, ["scenario"] = ended.Scenario, ["status"] = ended.Status, ["message"] = Cap(ended.Message, 2000) },
            var other => new() { ["event"] = other.GetType().Name },
        };

        foreach (var empty in answer.Where(pair => pair.Value is null).Select(pair => pair.Key).ToList())
        {
            answer.Remove(empty);
        }

        return answer;
    }

    // One scenario through the gate with .NET's code coverage (Microsoft.NET.Test.Sdk brings it): the lines it ran.
    private object Coverage(JsonObject arguments)
    {
        const int FilesShown = 20;
        var uri = FileUri(Required(arguments, "path"));
        var line = Whole(arguments, "line", 1, int.MaxValue) ?? throw new ToolProblem("'line' is required.");
        var timeout = TimeSpan.FromMinutes(Whole(arguments, "timeoutMinutes", 1, 120) ?? 10);
        var path = new Uri(uri).LocalPath;
        var file = _server.Context(path, (string?)arguments["suite"]);
        var suite = file.Suite ?? throw new ToolProblem("No suite holds or links this feature file.");
        var project = suite.ProjectFile ?? throw new ToolProblem($"{suite.Name} has no project file to run with dotnet test.");
        if (file.Catalogs is null || !suite.HasScenarioTheory)
        {
            throw new ToolProblem(file.Catalogs is null
                ? $"{suite.Name} is not bound: {file.Reason}"
                : $"{suite.Name} runs its scenarios with [Theory], so one has no address to run by. Change it to [UatScenarioTheory].");
        }

        var names = _server.ScenarioNames(uri, line);
        if (names.Count == 0)
        {
            throw new ToolProblem($"There is no scenario heading at line {line}.");
        }

        // The files first: a run is slow, and without them there is nothing to report.
        var wanted = CoverageFiles(arguments);
        var relative = suite.RelativeToConfig(path);
        var filter = string.Join('|', names.Select(name => $"{BddScenarioAddress.Trait}={BddScenarioAddress.Of(relative, name)}"));
        var results = Path.Combine(Path.GetTempPath(), "Bruinet", "coverage", Guid.NewGuid().ToString("N"));
        List<string> test = ["test", project, "--filter", filter, "--collect", "Code Coverage;Format=cobertura", "--results-directory", results, "--logger", "console;verbosity=normal"];
        var built = (bool?)arguments["build"] != false;
        if (!built)
        {
            test.Add("--no-build");
        }

        Dictionary<string, string> environment = [];
        if ((string?)arguments["head"] is { Length: > 0 } head)
        {
            environment["BRUINET_HEAD"] = head;
        }

        try
        {
            var run = Run("dotnet", test, environment, timeout);
            if (built)
            {
                _server.BuildDone();
            }

            if (run.TimedOut)
            {
                throw new ToolProblem($"The run took longer than {timeout.TotalMinutes} minutes and was ended:{Environment.NewLine}{Cap(run.Tail, 2000, fromEnd: true)}");
            }

            if (run.Tail.Contains("No test matches the given testcase filter", StringComparison.Ordinal) || run.Tail.Contains("Total tests: 0", StringComparison.Ordinal))
            {
                throw new ToolProblem($"No test ran by the filter {filter}: is the suite built with this scenario?");
            }

            var report = Directory.Exists(results) ? Directory.EnumerateFiles(results, "*.cobertura.xml", SearchOption.AllDirectories).FirstOrDefault() : null;
            if (report is null)
            {
                throw new ToolProblem($"No coverage was written (exit code {run.Code}):{Environment.NewLine}{Cap(run.Tail, 2000, fromEnd: true)}");
            }

            var lines = CoverageReport.Read(report);
            return new
            {
                Scenarios = names,
                Passed = run.Code == 0,
                Files = wanted.Take(FilesShown).Select(source => lines.TryGetValue(source, out var hits)
                    ? new { File = Path.GetRelativePath(_solutionDir, source), Hit = Cap(CoverageReport.Ranges(hits.Where(pair => pair.Value).Select(pair => pair.Key)), 300), Missed = Cap(CoverageReport.Ranges(hits.Where(pair => !pair.Value).Select(pair => pair.Key)), 300), Note = (string?)null }
                    : new { File = Path.GetRelativePath(_solutionDir, source), Hit = (string?)null, Missed = (string?)null, Note = (string?)"not in the report: no line of it ran, or the collector left its assembly out" })
                    .ToList(),
                MoreFiles = wanted.Count > FilesShown ? wanted.Count - FilesShown : (int?)null,
                Output = run.Code == 0 ? null : Cap(run.Tail, 2000, fromEnd: true),
            };
        }
        finally
        {
            TryDelete(results);
        }
    }

    // The given files, or the working tree's changed code files that still exist.
    private List<string> CoverageFiles(JsonObject arguments)
    {
        if ((arguments["files"] as JsonArray)?.Select(node => (string?)node).OfType<string>().Select(file => Path.GetFullPath(file, _solutionDir)).ToList() is { Count: > 0 } given)
        {
            return given;
        }

        try
        {
            return [.. Bruinet.Cli.Selection.SelectionCommands.Changed(_solutionDir, "HEAD").Where(file => CoverageReport.IsSource(file) && File.Exists(file))];
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new ToolProblem($"Give 'files': there is no git working tree here to take the changed files from ({exception.Message}).");
        }
    }

    // A temp folder removed; one still held open (a collector closing) is left, and said so.
    private static void TryDelete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Could not delete {folder}: {exception.Message}");
        }
    }

    // The newest failed run of the scenario at a heading (or of the file), as an agent acts on it.
    private object GetFailure(JsonObject arguments)
    {
        var uri = FileUri(Required(arguments, "path"));
        var suite = (string?)arguments["suite"];
        var names = _server.ScenarioNames(uri, Whole(arguments, "line", 1, int.MaxValue));
        if (names.Count == 0)
        {
            throw new ToolProblem("There is no scenario heading at that line.");
        }

        var runs = _server.NewestRuns(uri, suite);
        var failed = names.Select(name => runs.GetValueOrDefault(name))
            .OfType<Bruinet.Bdd.Preview.BddScenarioRun>()
            .Where(run => run.Status == "failed")
            .OrderByDescending(run => run.At)
            .FirstOrDefault();
        if (failed is null)
        {
            var newest = names.Select(name => runs.GetValueOrDefault(name)).OfType<Bruinet.Bdd.Preview.BddScenarioRun>().OrderByDescending(run => run.At).FirstOrDefault();
            throw new ToolProblem(newest is null ? "No run of it has a result yet: run it with run_scenario." : $"Its newest run {newest.Status} ({newest.At:u}): nothing failed.");
        }

        var step = failed.Steps.FirstOrDefault(candidate => candidate.Status == "Failed");

        // The report's file is relative to the suite's config: relative to the solution here, as run_scenario's, with the step's text.
        var config = _server.Context(new Uri(uri).LocalPath, suite).Suite?.ConfigPath is { } configPath ? Path.GetDirectoryName(configPath)! : _solutionDir;
        var file = step?.File is { } written ? Path.GetFullPath(written, config) : new Uri(uri).LocalPath;
        var text = step is not null && File.Exists(file) && File.ReadLines(file).Skip(step.Line - 1).FirstOrDefault() is { } line ? line.Trim() : null;
        var failure = Failure(failed);
        return new
        {
            failed.Scenario,
            failed.Status,
            At = failed.At.ToString("u", CultureInfo.InvariantCulture),
            FailedStep = step is null ? null : new { File = Path.GetRelativePath(_solutionDir, file), step.Line, Text = text, step.CallLine, step.DurationMs },
            Failure = failure,
            AppSource = AppSource(failure, config),
            AppEvents = step?.AppEvents is { } said ? JsonNode.Parse(said) : null,
            Screenshot = failed.EvidencePath,
            Message = Cap(step?.Message, 2000),
        };
    }

    // Each suite's feature files, and of each the scenarios whose newest run failed: the newest first.
    private object Failing(JsonObject arguments)
    {
        const int Shown = 50;
        var suites = (string?)arguments["suite"] is not null
            ? [Suite(arguments)]
            : _server.Explorer(new ExplorerParams(null, null)).Suites.Select(SuiteNamed).ToList();
        var failed = suites
            .SelectMany(suite => suite.FeatureFiles.Concat(suite.LinkedFiles)
                .Where(path => path.EndsWith(BddFileFormat.FeatureExtension, StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .SelectMany(path => _server.NewestRuns(new Uri(path).AbsoluteUri, suite.Name).Values
                    .Where(run => run.Status == "failed")
                    .Select(run => (Suite: suite, Path: path, Run: run))))
            .OrderByDescending(item => item.Run.At)
            .ToList();
        return new
        {
            Count = failed.Count,
            Failing = failed.Take(Shown).Select(item => new
            {
                item.Suite.Name,
                File = Path.GetRelativePath(_solutionDir, item.Path),
                item.Run.Scenario,
                Address = BddScenarioAddress.Of(item.Suite.RelativeToConfig(item.Path), item.Run.Scenario),
                At = item.Run.At.ToString("u", CultureInfo.InvariantCulture),
                Kind = (string?)Failure(item.Run)?["kind"],
                Message = Cap(item.Run.Steps.FirstOrDefault(step => step.Status == "Failed")?.Message, 300),
            }),
            More = failed.Count > Shown ? failed.Count - Shown : (int?)null,
        };
    }

    // The newest run of the scenario at a heading: each step that has app events, with them.
    private object StepLogOf(JsonObject arguments)
    {
        var uri = FileUri(Required(arguments, "path"));
        var names = _server.ScenarioNames(uri, Whole(arguments, "line", 1, int.MaxValue) ?? throw new ToolProblem("'line' is required."));
        var runs = _server.NewestRuns(uri, (string?)arguments["suite"]);
        var run = names.Select(name => runs.GetValueOrDefault(name)).OfType<Bruinet.Bdd.Preview.BddScenarioRun>().OrderByDescending(candidate => candidate.At).FirstOrDefault()
            ?? throw new ToolProblem("No run of it has a result yet: run it with run_scenario.");
        return new
        {
            run.Scenario,
            run.Status,
            At = run.At.ToString("u", CultureInfo.InvariantCulture),
            Steps = run.Steps.Where(step => step.AppEvents is not null).Take(20).Select(step => new { step.File, step.Line, step.CallLine, step.Status, Events = JsonNode.Parse(step.AppEvents!) }),
            Note = run.Steps.Any(step => step.AppEvents is not null) ? null : "The app said nothing at warning level or up (BRUINET_STEP_LOG=all keeps information too), or its log is not wired (AddBruinetStepLog).",
        };
    }

    // Since last green (AI9): the newest failing run's trace against the newest earlier passing one's, and the files
    // changed since that run's commit that touch the step where they part.
    private object Why(JsonObject arguments)
    {
        var uri = FileUri(Required(arguments, "path"));
        var suite = (string?)arguments["suite"];
        var names = _server.ScenarioNames(uri, Whole(arguments, "line", 1, int.MaxValue) ?? throw new ToolProblem("line is required: the scenario heading's line."));
        if (names.Count == 0)
        {
            throw new ToolProblem("There is no scenario heading at that line.");
        }

        static bool Traced(Bruinet.Bdd.Preview.BddScenarioRun run) => run.TracePath is { } path && File.Exists(path);
        var failed = _server.NewestRuns(uri, suite, run => run.Status == "failed" && Traced(run));
        var (name, failing) = names.Select(name => (name, Run: failed.GetValueOrDefault(name))).Where(found => found.Run is not null).OrderByDescending(found => found.Run!.At).FirstOrDefault();
        if (failing is null)
        {
            throw new ToolProblem("No failed run of it has a trace: run it with run_scenario (a run with trace: off keeps none).");
        }

        var newest = _server.NewestRuns(uri, suite).GetValueOrDefault(name);
        var green = Bruinet.Uat.UatTraceDiff.LastGreen(failing.TracePath!)
            ?? throw new ToolProblem($"No run of it passed before the failure of {failing.At:u}: there is no green run to compare with. get_trace gives what the failing run did.");
        var (now, then) = (Bruinet.Uat.UatTraceDiff.Read(failing.TracePath!), Bruinet.Uat.UatTraceDiff.Read(green));
        if (now is null || then is null)
        {
            throw new ToolProblem("A trace cannot be read: run the scenario again.");
        }

        var difference = Bruinet.Uat.UatTraceDiff.Compare(now, then);
        var at = Math.Max(difference.Index, 0);
        var suiteFolder = _server.Context(new Uri(uri).LocalPath, suite).Suite?.ConfigPath is { } config ? Path.GetDirectoryName(config)! : _solutionDir;
        var files = ChangedFiles(
            suiteFolder,
            (string?)then["commit"],
            (string?)now["commit"],
            StepNames(now, at, at).Concat(StepNames(then, at, at)).ToList(),
            StepNames(now, 0, at - 1).ToList());
        return new
        {
            Scenario = name,
            Failing = new { At = failing.At.ToString("u", CultureInfo.InvariantCulture), Commit = Short((string?)now["commit"]) },
            Green = new { At = File.GetLastWriteTimeUtc(green).ToString("u", CultureInfo.InvariantCulture), Commit = Short((string?)then["commit"]) },
            difference.Kind,
            Step = difference.Index < 0 ? null : new { difference.Line, difference.Text, difference.Index },
            difference.Differences,
            Slower = difference.Slower.Count == 0 ? null : difference.Slower,
            Files = files.Answer,
            Note = string.Join(" ", new[]
            {
                newest is { Status: "passed" } && newest.At > failing.At ? $"Its newest run passed ({newest.At:u}): this is an older failure." : null,
                difference.Kind == Bruinet.Uat.UatTraceDiff.None ? "The failing run's steps match the green one's: get_failure gives the failure." : null,
                Bruinet.Uat.UatTraceDiff.ReadPages(then) ? null : "Page values were not compared: the green run did not read pages (run_scenario's trace: all).",
                files.Note,
            }.OfType<string>()) is { Length: > 0 } note ? note : null,
        };

        static string? Short(string? commit) => commit is { Length: > 8 } ? commit[..8] : commit;
    }

    // The pages and controls the steps from first to last drove: what ties a changed file to them.
    private static IEnumerable<(string Name, bool IsPage)> StepNames(JsonObject trace, int first, int last)
    {
        var steps = trace["steps"]?.AsArray().OfType<JsonObject>().ToList() ?? [];
        for (var index = Math.Max(first, 0); index <= last && index < steps.Count; index++)
        {
            var step = steps[index];
            foreach (var call in step["calls"]?.AsArray().OfType<JsonObject>() ?? [])
            {
                yield return ((string?)call["page"] ?? string.Empty, true);
                yield return ((string?)call["control"] ?? string.Empty, false);
            }

            yield return ((string?)step["page"] ?? string.Empty, true);
        }
    }

    // The files changed since the green run's commit (to the failing run's when HEAD moved on since, else to the working tree,
    // new files too), in the repository the suite is in: those that touch the step where the runs part, those that touch an
    // earlier step of the scenario (a cause can come before its symptom), and how many others. Bounded: 2000 files read
    // at most, 20 named in each list.
    private static WhyFiles ChangedFiles(string folder, string? green, string? failing, IReadOnlyList<(string Name, bool IsPage)> step, IReadOnlyList<(string Name, bool IsPage)> before)
    {
        if (green is null)
        {
            return new WhyFiles([], [], [], "The green run's commit is not known (not a git checkout): no files compared.");
        }

        var since = green[..Math.Min(8, green.Length)];
        List<string> changed;
        string root;
        try
        {
            root = Bruinet.Cli.Sql.SqlCommands.GitText(folder, "rev-parse", "--show-toplevel").Trim();
            var head = Bruinet.Cli.Sql.SqlCommands.GitText(root, "rev-parse", "HEAD").Trim();
            var toFailing = failing is not null && !string.Equals(head, failing, StringComparison.OrdinalIgnoreCase);
            changed = [.. Lines(toFailing
                    ? Bruinet.Cli.Sql.SqlCommands.GitText(root, "-c", "core.quotePath=false", "diff", "--name-only", green, failing!, "--")
                    : Bruinet.Cli.Sql.SqlCommands.GitText(root, "-c", "core.quotePath=false", "diff", "--name-only", green, "--"))
                .Concat(toFailing ? [] : Lines(Bruinet.Cli.Sql.SqlCommands.GitText(root, "-c", "core.quotePath=false", "ls-files", "--others", "--exclude-standard")))
                .Distinct(StringComparer.Ordinal)
                .Take(2000)];
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new WhyFiles([], [], [], $"The files since {since} could not be listed ({exception.Message}).");
        }

        var (touching, earlier, other) = WhyFiles.Sort(changed, file => Path.Combine(root, file), step, before);
        return new WhyFiles(touching, earlier, other, changed.Count == 0 ? $"No file changed since {since}: the difference is in data, timing or the environment." : null);

        static IEnumerable<string> Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    // A name's words, lower case, without the ones every page has: "TodoDetailPage" and "Todo Detail" give todo, detail.
    internal static HashSet<string> Words(string name)
        => [.. System.Text.RegularExpressions.Regex.Matches(name, "[A-Z]?[a-z]+|[A-Z]+(?![a-z])|[0-9]+")
            .Select(word => word.Value.ToLowerInvariant())
            .Where(word => word is not ("page" or "view" or "test" or "tests" or "control"))];

    // A scenario's newest trace: a row per step and the failed step in full, or one step in full.
    private object GetTrace(JsonObject arguments)
    {
        var uri = FileUri(Required(arguments, "path"));
        var names = _server.ScenarioNames(uri, Whole(arguments, "line", 1, int.MaxValue) ?? throw new ToolProblem("line is required: the scenario heading's line."));
        if (names.Count == 0)
        {
            throw new ToolProblem("There is no scenario heading at that line.");
        }

        // Of an outline's rows, the newest with a trace: a newer row that passed has none.
        var runs = _server.NewestRuns(uri, (string?)arguments["suite"]);
        var ran = names.Select(name => runs.GetValueOrDefault(name)).OfType<Bruinet.Bdd.Preview.BddScenarioRun>().OrderByDescending(run => run.At).ToList();
        var newest = ran.FirstOrDefault(run => run.TracePath is { } traced && File.Exists(traced));
        if (newest?.TracePath is not { } path)
        {
            throw new ToolProblem(ran.Count == 0
                ? "No run of it has a result yet: run it with run_scenario."
                : $"Its newest run {ran[0].Status} ({ran[0].At:u}) has no trace: it ran with trace: off, or was skipped. Run it again with run_scenario.");
        }

        var (trace, steps) = ReadTrace(path);
        if (Whole(arguments, "step", 1, int.MaxValue) is { } stepLine)
        {
            return TraceStep(steps, stepLine, Path.GetFileName(new Uri(uri).LocalPath));
        }

        return new
        {
            Scenario = (string?)trace["scenario"],
            Status = (string?)trace["status"],
            At = newest.At.ToString("u", CultureInfo.InvariantCulture),
            Steps = steps.Select(TraceRow),
            Failed = steps.FirstOrDefault(step => (string?)step["status"] == "Failed")?.DeepClone(),
        };
    }

    private (JsonObject Trace, List<JsonObject> Steps) ReadTrace(string path)
    {
        try
        {
            var trace = JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? throw new JsonException("empty");
            return (trace, trace["steps"]?.AsArray().OfType<JsonObject>().ToList() ?? throw new JsonException("no steps"));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or IOException)
        {
            throw new ToolProblem($"The trace at {Path.GetRelativePath(_solutionDir, path)} cannot be read ({exception.Message}): run the scenario again.");
        }
    }

    // A step at a line of the feature file: a callable's inner steps share its call line, so the failed one of them.
    private static JsonObject TraceStep(List<JsonObject> steps, int line, string feature)
    {
        var at = steps.Where(step => (int?)step["callLine"] == line).ToList();
        if (at.Count == 0)
        {
            at = [.. steps.Where(step => (int?)step["line"] == line && ((string?)step["file"])?.EndsWith(feature, StringComparison.OrdinalIgnoreCase) != false)];
        }

        return at.FirstOrDefault(step => (string?)step["status"] == "Failed") ?? at.FirstOrDefault()
            ?? throw new ToolProblem($"The trace has no step at line {line}: its steps are at {string.Join(", ", steps.Select(step => (int?)step["callLine"] ?? (int?)step["line"]).Distinct())}.");
    }

    // A step in a line: its page, how many calls, the slowest (a wait), how many app events.
    private static object TraceRow(JsonObject step)
    {
        var calls = step["calls"]?.AsArray().OfType<JsonObject>().ToList() ?? [];
        var slow = calls.MaxBy(call => (int?)call["ms"] ?? 0);
        return new
        {
            Line = (int?)step["callLine"] ?? (int?)step["line"],
            Text = (string?)step["text"],
            Status = (string?)step["status"],
            Ms = (long?)step["durationMs"],
            Page = (string?)step["page"],
            Calls = calls.Count + ((int?)step["callsLeftOut"] ?? 0),
            Slowest = slow is null ? null : $"{slow["page"]}.{slow["control"]}.{slow["action"]} {slow["ms"]}ms" + ((int?)slow["attempts"] > 1 ? $" x{slow["attempts"]}" : string.Empty),
            Events = step["appEvents"]?.AsArray().Count ?? 0,
        };
    }

    // A page object control: its member's line, the locator value it declares, and where the app makes that element.
    private object WhereIs(JsonObject arguments)
    {
        var suite = Suite(arguments);
        var pageName = Required(arguments, "page");
        var controlName = Required(arguments, "control");
        var pages = UatPageSet.Of(Catalogs(suite).Default);
        var page = pages.Values.FirstOrDefault(model => string.Equals(model.Key, pageName, StringComparison.OrdinalIgnoreCase) || string.Equals(model.Name, pageName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ToolProblem($"{suite.Name} has no page '{pageName}': list_pages names them.");
        var control = page.Controls.FirstOrDefault(model => string.Equals(Compact(model.Name), Compact(controlName), StringComparison.OrdinalIgnoreCase))
            ?? throw new ToolProblem($"The page {page.Name} has no control '{controlName}': list_pages names its controls.");

        var at = BddSourceLocator.Of(page.PageType, control.MemberName ?? Compact(control.Name));
        var value = at is { FilePath: { } file } ? BddAppSources.ValueAt(file, at.LineNumber) : null;
        var app = value is null ? [] : AppSources().Find(value, hint: new BddAppSourceHint(at?.FilePath, page.PageType.Name, IsXaml(control.ControlType)));
        return new
        {
            Page = page.Name,
            Control = control.Name,
            PageObject = at is { FilePath: { } declared } ? $"{Path.GetRelativePath(_solutionDir, declared)}:{at.LineNumber}" : null,
            Locator = value is null ? null : value.IsPrefix ? value.Value + "…" : value.Value,
            App = app.Select(hit => new { File = $"{Path.GetRelativePath(_solutionDir, hit.File)}:{hit.Line}", hit.Text, TestId = hit.IsAttribute }),
            Note = at?.FilePath is null ? "The page object's source was not found: build the suite with its PDB (Debug)."
                : value is null ? "The member declares no string to find by (a constant or nameof): look for it in the page object."
                : app.Count == 0 ? "No app source has this value: the app may build it some other way, or the element is gone." : null,
        };

        static string Compact(string name) => name.Replace(" ", string.Empty, StringComparison.Ordinal);

    }

    // A MAUI, Avalonia or WPF control's app is XAML, an Html or Blazor one's web markup: by the first Bruinet assembly in
    // its bases (a suite's own control derives from one); null for neither.
    private static bool? IsXaml(Type? controlType)
    {
        for (var type = controlType; type is not null; type = type.BaseType)
        {
            var name = type.Assembly.GetName().Name ?? string.Empty;
            if (!name.StartsWith("Bruinet.", StringComparison.Ordinal))
            {
                continue;
            }

            return name.Contains("Maui", StringComparison.Ordinal) || name.Contains("Avalonia", StringComparison.Ordinal) || name.Contains("Wpf", StringComparison.Ordinal) ? true
                : name.Contains("Html", StringComparison.Ordinal) || name.Contains("Blazor", StringComparison.Ordinal) ? false
                : null;
        }

        return null;
    }

    // The app's sources, their file list kept a minute rather than walked again per call.
    private BddAppSources AppSources()
    {
        if (_appSources is null || DateTime.UtcNow - _appSourcesAt > TimeSpan.FromMinutes(1))
        {
            _appSources = new BddAppSources(_solutionDir);
            _appSourcesAt = DateTime.UtcNow;
        }

        return _appSources;
    }

    // The folder of the suite a feature file runs in; the solution's when none.
    private string SuiteFolder(string uri, string? suite)
        => _server.Context(new Uri(uri).LocalPath, suite).Suite?.ConfigPath is { } configPath ? Path.GetDirectoryName(configPath)! : _solutionDir;

    // Where the app makes the element a failure's locator names: the top three lines, the suite's own app first; null
    // when the failure names none.
    private object? AppSource(JsonNode? failure, string suiteFolder)
    {
        if (failure?["locator"] is not JsonValue locator || BddAppSources.ValueOf(locator.ToString()) is not { } value)
        {
            return null;
        }

        // Only where the value is a test id: a string elsewhere is as likely a test's or the framework's.
        var hits = AppSources().Find(value, max: 3, hint: new BddAppSourceHint(Path.Combine(suiteFolder, "uat.config.md"), null, null))
            .Where(hit => hit.IsAttribute)
            .ToList();
        return hits.Count == 0 ? null : hits.Select(hit => $"{Path.GetRelativePath(_solutionDir, hit.File)}:{hit.Line}: {hit.Text}").ToList();
    }

    // A failed run's failure, as its report wrote it; null for a report written before failures were.
    private static JsonNode? Failure(Bruinet.Bdd.Preview.BddScenarioRun run)
        => run.Steps.FirstOrDefault(step => step.Status == "Failed")?.Failure is { } json ? JsonNode.Parse(json) : null;

    private object LastRuns(JsonObject arguments)
        => _server.LastRuns(new PreviewParams(FileUri(Required(arguments, "path")), (string?)arguments["suite"]))
            .Select(run => run with { Message = Cap(run.Message, 2000) })
            .ToList();

    // A whole-number argument in its range, or a tool error saying what it must be; null when left out.
    private static int? Whole(JsonObject arguments, string name, int least, int most)
    {
        if (arguments[name] is not { } node)
        {
            return null;
        }

        return node is JsonValue value && value.TryGetValue<int>(out var number) && number >= least && number <= most
            ? number
            : throw new ToolProblem($"'{name}' must be a whole number from {least} to {most}.");
    }

    // bruinet tests affected for a suite, in process: what an agent runs after a change, with why.
    private object TestsAffected(JsonObject arguments)
    {
        var named = Required(arguments, "suite");
        var folder = Path.GetFullPath(named, _solutionDir) is var path && Directory.Exists(path)
            ? path
            : Suite(arguments).ProjectDirectory;
        var map = Path.Combine(folder, Bruinet.Cli.Selection.TouchMap.FileName);
        if (!File.Exists(map))
        {
            throw new ToolProblem($"No touch map at {map}: run the suite whole once with BRUINET_TIMINGS=1, then 'bruinet tests touch-map <timings> --suite {folder}'.");
        }

        List<string> changed;
        try
        {
            changed = (arguments["files"] as JsonArray)?.Select(node => (string?)node).OfType<string>().Select(file => Path.GetFullPath(file, _solutionDir)).ToList() is { Count: > 0 } given
                ? given
                : Bruinet.Cli.Selection.SelectionCommands.Changed(_solutionDir, (string?)arguments["base"] ?? "HEAD");
        }
        catch (InvalidOperationException exception)
        {
            throw new ToolProblem(exception.Message);
        }

        var always = (arguments["always"] as JsonArray)?.Select(node => (string?)node).OfType<string>().ToList();
        var affected = Bruinet.Cli.Selection.Affected.Of(folder, null, changed, Bruinet.Cli.Selection.TouchMap.Load(map), always);
        const int Shown = 50;
        return new
        {
            Changed = changed.Count,
            affected.Whole,
            affected.Filter,
            Picked = affected.Picked.Take(Shown).Select(test => new { test.Name, test.Why }),
            More = affected.Picked.Count > Shown ? affected.Picked.Count - Shown : (int?)null,
            affected.Notes,
            Next = affected.Whole is not null ? "Run the suite whole." : affected.Filter is null ? "Nothing to run." : "Run them with dotnet test <suite project> --filter \"<filter>\", or a scenario with run_scenario.",
        };
    }

    // The language server's rename, as edits an agent reads first; applied, every changed file must still bind.
    private object RenameStep(JsonObject arguments)
    {
        var uri = FileUri(Required(arguments, "path"));
        var line = (int?)arguments["line"] ?? throw new ToolProblem("'line' is required.");
        WorkspaceEdit? edit;
        try
        {
            edit = _server.Rename(new RenameParams(new TextDocumentIdentifier(uri), new Position(line - 1, 0), Required(arguments, "newPhrase")));
        }
        catch (StreamJsonRpc.LocalRpcException exception)
        {
            throw new ToolProblem(exception.Message);
        }

        if (edit?.Changes is not { Count: > 0 } changes)
        {
            throw new ToolProblem($"Line {line} is not a '## Step:' heading or a call of one.");
        }

        // Each file's text after its edits, its lines' before and after; edits applied from the last.
        var files = changes.Select(pair =>
        {
            var path = new Uri(pair.Key).LocalPath;
            var before = File.ReadAllText(path);
            var lines = before.Split('\n');
            var after = lines.ToArray();
            foreach (var change in pair.Value.OrderByDescending(change => change.Range.Start.Line).ThenByDescending(change => change.Range.Start.Character))
            {
                var at = change.Range.Start.Line;
                var text = after[at];
                after[at] = text[..change.Range.Start.Character] + change.NewText + text[change.Range.End.Character..];
            }

            var edits = pair.Value.Select(change => change.Range.Start.Line).Distinct().Order()
                .Select(at => new { Line = at + 1, Before = lines[at].Trim(), After = after[at].Trim() })
                .ToList();
            return (Path: path, Before: before, After: string.Join('\n', after), Edits: edits);
        }).ToList();

        var shown = files.Select(file => new { File = Path.GetRelativePath(_solutionDir, file.Path), file.Edits }).ToList();
        if ((bool?)arguments["apply"] != true)
        {
            return new { Applied = false, Files = shown, Next = "Call again with apply: true to write these edits." };
        }

        foreach (var file in files)
        {
            File.WriteAllText(file.Path, file.After);
        }

        var problems = files
            .Select(file => Validate(file.Path, null))
            .Where(check => !check.Valid)
            .Select(check => new { File = Path.GetRelativePath(_solutionDir, check.File), check.Reason, check.Problems })
            .ToList();
        if (problems.Count > 0)
        {
            foreach (var file in files)
            {
                File.WriteAllText(file.Path, file.Before);
            }

            return new { Applied = false, Files = shown, PutBack = true, Problems = problems };
        }

        return new { Applied = true, Files = shown };
    }

    private object RenamePageObject(JsonObject arguments)
    {
        var project = (string?)arguments["project"];
        var page = Required(arguments, "page");
        var name = Required(arguments, "newName");
        if ((bool?)arguments["confirm"] != true)
        {
            return new { Renamed = false, Would = $"Rename the page object of '{page}' to {name}, in the files that declare it; the files that only use it are listed for you to fix.", Next = "Tell the person. Call again with confirm: true once they agree." };
        }

        var result = _server.Scraper.RenamePageObject(project, page, name);
        return new { Renamed = true, result.From, result.To, result.Files, result.UsedElsewhere, Next = result.UsedElsewhere.Count > 0 ? "Change the files that use it, then check_code." : "check_code." };
    }

    private string FileUri(string path) => new Uri(Path.GetFullPath(path, _solutionDir)).AbsoluteUri;

    private static string? Cap(string? text, int length, bool fromEnd = false)
        => text is null || text.Length <= length ? text
            : fromEnd ? $"({text.Length - length} characters before) …{text[^length..]}"
            : $"{text[..length]}… ({text.Length - length} more characters)";

    // An agent is not asked first, so it only counts unless it says dryRun: false.
    private DeleteResultsResult DeleteResults(JsonObject arguments)
    {
        var path = (string?)arguments["path"];
        var result = _server.DeleteResults(new DeleteResultsParams(
            Required(arguments, "scope"),
            path is null ? null : new Uri(Path.GetFullPath(path, _solutionDir)).AbsoluteUri,
            (int?)arguments["line"],
            (string?)arguments["suite"],
            (bool?)arguments["dryRun"] ?? true));
        return result.Done ? result : throw new ToolProblem(result.Reason ?? "Nothing to delete.");
    }

    // Build, then the gate run with the recording variables; the video it made, and how the run went.
    private object RecordVideo(JsonObject arguments)
    {
        var path = Path.GetFullPath(Required(arguments, "path"), _solutionDir);
        var recordingsFolder = (string?)arguments["recordingsFolder"];
        var suite = (string?)arguments["suite"];
        var target = _server.RecordTarget(new RecordTargetParams(new Uri(path).AbsoluteUri, (int?)arguments["line"], suite, recordingsFolder, (string?)arguments["voice"], null));
        if (!target.CanRecord || target.Project is not { } project || target.Filter is not { } filter)
        {
            throw new ToolProblem($"This cannot be recorded: {target.Reason}");
        }

        var build = Run("dotnet", ["build", project, "-v:minimal", "-nologo"], new Dictionary<string, string>());
        if (build.Code != 0)
        {
            throw new ToolProblem($"The build failed, so nothing was recorded:{Environment.NewLine}{build.Tail}");
        }

        var test = Run("dotnet", ["test", project, "--no-build", "--filter", filter, "--logger", "console;verbosity=normal"], target.Environment);
        var videos = _server.Recordings(new RecordingsParams(new Uri(path).AbsoluteUri, suite, recordingsFolder));
        var video = target.Video is { } expected ? videos.FirstOrDefault(candidate => candidate.Name == expected) : videos.FirstOrDefault();
        if (video is null)
        {
            throw new ToolProblem($"The run made no video (exit code {test.Code}):{Environment.NewLine}{test.Tail}");
        }

        var timeline = Path.ChangeExtension(video.Path, ".timeline.json");
        return new
        {
            Video = video.Path,
            video.Captions,
            Timeline = File.Exists(timeline) ? timeline : null,
            Passed = test.Code == 0,
            Output = test.Code == 0 ? null : test.Tail,
        };
    }

    private static object ReviewPage(JsonObject arguments)
    {
        var videos = (arguments["videos"] as JsonArray)?.Select(node => (string?)node).OfType<string>().Select(Path.GetFullPath).ToList() ?? [];
        if (videos.Count == 0)
        {
            throw new ToolProblem("'videos' is required: the videos record_video made.");
        }

        var missing = videos.Where(video => !File.Exists(video)).ToList();
        if (missing.Count > 0)
        {
            throw new ToolProblem($"No video at {string.Join(", ", missing)}.");
        }

        var page = (string?)arguments["path"] is { Length: > 0 } named
            ? Path.GetFullPath(named)
            : Path.Combine(Path.GetDirectoryName(videos[0])!, $"review-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.html");
        var changes = (arguments["changes"] as JsonArray)?.Select(node => (string?)node).OfType<string>().ToList() ?? [];
        BddReviewPage.Write(page, Required(arguments, "title"), (string?)arguments["summary"], changes, videos);
        return new { Page = page, Url = new Uri(page).AbsoluteUri };
    }

    // A process to its end, its output kept: the last lines say why a build or a run failed.
    private static (int Code, string Tail) Run(string command, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> variables)
    {
        var (code, tail, _) = Run(command, arguments, variables, Timeout.InfiniteTimeSpan);
        return (code, tail);
    }

    // As above, ended with its process tree when it runs longer than timeout.
    private static (int Code, string Tail, bool TimedOut) Run(string command, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> variables, TimeSpan timeout)
    {
        var info = new ProcessStartInfo(command) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in variables)
        {
            info.Environment[name] = value;
        }

        var lines = new Queue<string>();
        void Keep(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (lines)
            {
                lines.Enqueue(line);
                if (lines.Count > 40)
                {
                    lines.Dequeue();
                }
            }
        }

        using var process = Process.Start(info) ?? throw new ToolProblem($"{command} did not start.");
        process.OutputDataReceived += (_, e) => Keep(e.Data);
        process.ErrorDataReceived += (_, e) => Keep(e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var timedOut = !process.WaitForExit(timeout);
        if (timedOut)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }

        lock (lines)
        {
            return (timedOut ? -1 : process.ExitCode, string.Join(Environment.NewLine, lines), timedOut);
        }
    }

    private object ListSuites()
        => _server.Explorer(new ExplorerParams(null, null)).Suites
            .Select(name => SuiteNamed(name))
            .Select(suite => new
            {
                suite.Name,
                Head = TryHead(suite),
                Config = suite.ConfigPath,
                Built = suite.TryGetCatalogs(out var reason) is not null,
                Reason = reason,
            })
            .ToList();

    private static object ListSteps(BddSuite suite, string? query)
    {
        var catalogs = Catalogs(suite);
        var words = (query ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var steps = catalogs.Default.Patterns
            .Where(pattern => pattern.Callable is null)
            .Select(pattern => new
            {
                Keyword = pattern.Keyword.ToString(),
                pattern.Phrase,
                Parameters = pattern.Parameters.Select(parameter => parameter.ToString()).ToList(),
                From = pattern.ControlVerb is not null ? "control verb, on the current page"
                    : pattern.Page is { } page ? $"page {page}"
                    : pattern.Namespace.Length > 0 ? $"namespace {pattern.Namespace} (the file must use it)"
                    : pattern.CommandId,
            })
            .Concat(suite.StepFiles.SelectMany(path => BddScenarioFiles.ParsePhraseFile(path).Document?.Callables ?? []).Select(callable => new
            {
                Keyword = "Any",
                callable.Phrase,
                Parameters = callable.Declared.Select(parameter => $"{{{parameter.Name}: {parameter.TypeName}}}").ToList(),
                From = $"{callable} in {Path.GetFileName(callable.Source.FilePath)} (a feature file under that folder sees it)",
            }))
            .Where(step => words.All(word => step.Phrase.Contains(word, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(step => step.Keyword)
            .ThenBy(step => step.Phrase, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new { Suite = suite.Name, Count = steps.Count, Steps = steps };
    }

    private static object ListPages(BddSuite suite)
        => UatPageSet.Of(Catalogs(suite).Default).Values
            .OrderBy(page => page.Key, StringComparer.OrdinalIgnoreCase)
            .Select(page => new
            {
                Page = page.Key,
                Class = page.PageType.FullName,
                Controls = page.Controls.Select(control => new { control.Name, Type = control.ControlType.Name, control.NavigatesTo }).ToList(),
            })
            .ToList();

    // The draft checked in the suite that holds its path, as the editor checks unsaved text.
    private Validation Validate(string path, string? text)
    {
        var full = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(_solutionDir, path));
        if (text is null && !File.Exists(full))
        {
            throw new ToolProblem($"'{full}' does not exist; give its text.");
        }

        var uri = new Uri(full).AbsoluteUri;
        _server.DidOpen(new DidOpenTextDocumentParams(new TextDocumentItem(uri, "markdown", 1, text ?? File.ReadAllText(full))));
        try
        {
            var file = _server.Context(full);
            var diagnostics = _server.DiagnosticsOf(file);
            return new Validation(
                full,
                file.Suite?.Name,
                file.Catalogs is not null,
                file.Reason,
                diagnostics.All(diagnostic => diagnostic.Severity != 1) && file.Catalogs is not null,
                [.. diagnostics.Select(diagnostic => new ValidationProblem(diagnostic.Range.Start.Line + 1, diagnostic.Severity == 1 ? "error" : "warning", diagnostic.Code, diagnostic.Message))]);
        }
        finally
        {
            _server.DidClose(new DidCloseTextDocumentParams(new TextDocumentIdentifier(uri)));
        }
    }

    private BddSuite Suite(JsonObject arguments)
    {
        var name = (string?)arguments["suite"];
        var names = _server.Explorer(new ExplorerParams(null, null)).Suites;
        if (names.Count == 0)
        {
            throw new ToolProblem($"No suite under {_solutionDir}: no project has a uat.config.md or bdd.config.md.");
        }

        var found = name is null ? names[0] : names.FirstOrDefault(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));
        return found is null
            ? throw new ToolProblem($"No suite '{name}'. The suites are {string.Join(", ", names)}.")
            : SuiteNamed(found);
    }

    private BddSuite SuiteNamed(string name) => _server.SuiteNamed(name) ?? throw new ToolProblem($"No suite '{name}'.");

    private static BddCatalogSet Catalogs(BddSuite suite)
        => suite.TryGetCatalogs(out var reason) ?? throw new ToolProblem($"{suite.Name} is not bound: {reason}");

    private static string? TryHead(BddSuite suite)
    {
        try
        {
            return suite.Head;
        }
        catch (BddWorkspaceException)
        {
            return null;
        }
    }

    private static string Required(JsonObject arguments, string name)
        => (string?)arguments[name] is { Length: > 0 } value ? value : throw new ToolProblem($"'{name}' is required.");

    private static JsonObject Text(string text, bool error)
        => new() { ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }), ["isError"] = error };

    private static JsonObject Error(JsonNode? id, int code, string message)
        => new() { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = code, ["message"] = message } };

    private static JsonObject Schema(params (string Name, string Type, string Description)[] properties)
        => new()
        {
            ["type"] = "object",
            ["properties"] = new JsonObject([.. properties.Select(property => new KeyValuePair<string, JsonNode?>(property.Name, new JsonObject { ["type"] = property.Type, ["description"] = property.Description }))]),
        };

    private sealed class McpException(int code, string message) : Exception(message)
    {
        public int Code { get; } = code;
    }

    // validate_scenario's answer: whether the file binds, and each problem by line.
    private sealed record Validation(string File, string? Suite, bool Bound, string? Reason, bool Valid, IReadOnlyList<ValidationProblem> Problems);

    private sealed record ValidationProblem(int Line, string Severity, string? Code, string Message);

    // A tool that cannot answer says why, as its result: the agent reads it and can act on it.
    private sealed class ToolProblem(string message) : Exception(message);
}
