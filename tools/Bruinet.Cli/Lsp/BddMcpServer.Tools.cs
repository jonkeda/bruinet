using System.Text.Json.Nodes;

namespace Bruinet.Bdd.LanguageServer;

/// <summary>
/// The MCP server's tools, as <c>tools/list</c> gives them (review 03, D2): two families, a suite's scenarios and the
/// Scraper's projects (prefixed <c>scrape_</c>), one verb per act, and a schema that says what is required, which
/// values a parameter takes and which tools only read.
/// </summary>
public sealed partial class BddMcpServer
{
    /// <summary>A tool: its name, what it does, its parameters, and what it may do besides answering.</summary>
    /// <param name="Name">The name an agent calls it by.</param>
    /// <param name="Description">What it does and gives, under 500 characters.</param>
    /// <param name="Schema">Its parameters, as JSON Schema.</param>
    /// <param name="Effect">Whether it only reads, deletes, or opens a browser.</param>
    public sealed record Tool(string Name, string Description, JsonObject Schema, ToolEffect Effect);

    /// <summary>What a tool may do besides answering, as the <c>annotations</c> of <c>tools/list</c> say it.</summary>
    public enum ToolEffect
    {
        /// <summary>It writes files or runs something.</summary>
        Writes,

        /// <summary>It only reads (<c>readOnlyHint</c>).</summary>
        Reads,

        /// <summary>It deletes for good (<c>destructiveHint</c>).</summary>
        Deletes,

        /// <summary>It opens a browser on the app (<c>openWorldHint</c>).</summary>
        OpensBrowser,
    }

    private const string AsksFirst = "Asks first: call again with confirm: true once the person agrees.";

    private static readonly Param Project = new("project", "string", "The scraped project; the only one when left out.");
    private static readonly Param OneSuite = new("suite", "string", "A suite name; the only one when left out.");
    private static readonly Param LinkedSuite = new("suite", "string", "The suite a linked file runs in; its own when left out.");
    private static readonly Param Feature = new("path", "string", "The feature file, relative to the solution.", Required: true);
    private static readonly Param ScenarioAt = new("line", "integer", "A line of the scenario: its heading, a tag or a step.", Required: true);
    private static readonly Param Max = new("max", "integer", "At most this many rows; 50 when left out.");
    private static readonly Param PageKey = new("page", "string", "The page key, e.g. /orders.", Required: true);
    private static readonly Param Session = new("session", "string", "The session id.", Required: true);
    private static readonly Param Confirm = new("confirm", "boolean", "The person agreed; false when left out.");
    private static readonly Param RunTimeout = new("timeoutMinutes", "integer", "End it after this long, 1 to 120; 10 when left out.");
    private static readonly Param StepTimeout = new("timeoutMinutes", "integer", "How long to wait, 1 to 10; 2 when left out.");

    /// <summary>The tools, as <c>tools/list</c> gives them: the scenarios' first, then the Scraper's.</summary>
    public static IReadOnlyList<Tool> Tools { get; } =
    [
        // A suite's scenarios.
        Reads("list_suites", "The UAT and BDD suites of the solution: name, head, config, and whether it is built. A suite that cannot load (not built, or built against another Bruinet) says why, and the others still answer.", Max),
        Reads("list_steps", "The steps a suite's scenarios can use: keyword, phrase with {placeholders} and their types, and where it comes from (class, page, namespace, callable step). Use only these phrases in a scenario. 'query' keeps the phrases holding every word of it, 'page' one page's. A suite of over 200 phrases asked with neither gives its pages and how many phrases each has.", OneSuite, new("query", "string", "Words the phrase must hold."), new("page", "string", "A page's name: only its phrases."), Max),
        Reads("list_pages", "A UI suite's pages: without 'page', each page's name, class and how many controls it has; with it, that page's controls (the names {control} takes) and where each navigates to.", OneSuite, new("page", "string", "A page's name: its controls."), Max),
        Reads("list_test_sets", "A suite's test sets: the values a feature file's TestData metadata takes.", OneSuite, Max),
        Reads("check_scenario", "Checks a feature or step file as the xUnit gate would, with its words: every step must bind. Give 'text' for a draft; without it the file on disk is checked. Writes nothing.", new("path", "string", "The file, where it is or will be in a suite's Features folder.", Required: true), new("text", "string", "The draft's text; the file on disk when left out.")),
        Reads("draft_feature", "Drafts a feature file from a C# UI test: each statement becomes the suite phrase whose words match it best, or a commented gap when none does. Gives the draft, where it would go, check_scenario's answer, the gaps (the steps the suite lacks, each with its statement) and the phrases that differ only in a value. Writes nothing.", new("path", "string", "The C# test file.", Required: true), new("method", "string", "The test method; the file's first test when left out."), new("feature", "string", "Where the feature file would go; the module's Features folder when left out.")),
        Reads("find_similar_steps", "The phrases of a suite that differ only in one value (a name or a number), each group with the phrase with a {value} placeholder that could stand for them all.", OneSuite, new("query", "string", "Words a phrase of the group must hold."), Max),
        Reads("find_step_usages", "The steps across every suite that use a C# member (a step definition, a page phrase or a page object's control): file, line, text and scenario.", new("symbol", "string", "Namespace.Type.Member.", Required: true), Max),
        Writes("record_video", "Records a video script (*.video.md), or a scenario of a feature file, on video with its voice-over: builds the suite, runs the scenes with BRUINET_RECORD, and gives the video, its captions (.srt) and its timeline. Needs ffmpeg; says how to get it when it is missing. Takes minutes: one scene is one scenario run.", new("path", "string", "The video script or the feature file.", Required: true), new("line", "integer", "For a feature file, a line of the scenario; every scenario of a video script."), LinkedSuite, new("recordingsFolder", "string", "Where the video goes; the suite's TestResults/recordings when left out."), new("voice", "string", "The voice, when the script names none."), RunTimeout),
        Writes("write_review", "Writes the review page an agent hands over when a feature is done: what it did, what changed, and each video record_video made, with captions, chapters and every step's result. Gives the page's path, to open in a browser.", new("title", "string", "What the work was.", Required: true), new("summary", "string", "A paragraph or two on what was done."), new("changes", "array", "What changed, a line each; code spans in backticks are kept."), new("videos", "array", "The videos (.mp4) record_video made.", Required: true), new("path", "string", "Where the page goes; beside the first video when left out.")),
        Reads("list_states", "The states the suite's app can be put in, declared with [BddState] in its test project: each state's phrase, its parameters and the method that sets it. A Given may name one; set_state puts the app in one.", OneSuite, Max),
        Writes("set_state", "Puts the suite's app in a declared state (list_states) on the suite's runner, against its own host and database, and leaves it paused there as a Step-through: step_state and step_screenshot read the app, step_move 'stop' ends it; no run_scenario meanwhile. Use it to look at a screen without clicking to it.", new("state", "string", "The state as a Given says it, its parameters filled in, e.g. 'signed in as Alice'.", Required: true), OneSuite, new("testData", "string", "The test set to start from; the suite's DefaultTestData when left out."), new("pageObject", "string", "The page the app is on in that state (list_pages): checked, and the page step_state then reads."), StepTimeout),
        Writes("run_scenario", "Runs the scenario at 'line' of a feature file (an outline's every row), or every scenario of it, on the suite's runner, and waits: each scenario's status and, for a failed one, the step, the failure and sinceGreen (where it parted from its last green run). Runs the file on disk with the suite's last build: give build: true after changing C#. Not while a Step-through is paused.", Feature, new("line", "integer", "A line of the scenario: its heading, a tag or a step; every scenario of the file when left out."), LinkedSuite, new("build", "boolean", "Build the suite's project first; false when left out."), RunTimeout, new("replay", "string", "The run's HTTP and SQL replay mode, for a suite that replays its dependencies; the suite's own when left out.", Values: ["record", "replay", "refresh"]), new("trace", "string", "all: the page after every step; failed: after a failed one (the default); off: no trace.", Values: ["all", "failed", "off"])),
        Reads("list_results", "The newest result of each scenario, from any run (run_scenario, the IDE, the xUnit gate): suite, file, the heading's line, scenario, status, when, the kind of failure and the start of its message. Of a feature file, a suite or every suite; failed ones unless status is all. A suite that cannot load is named under notLoaded. get_trace gives what a run did, diff_since_green what changed since it passed.", new("path", "string", "A feature file: only its scenarios."), new("suite", "string", "A suite's name; every suite when left out (or the one a linked file is picked in)."), new("status", "string", "failed when left out.", Values: ["failed", "all"]), Max),
        Writes("step_start", "Opens the scenario at 'line' on the suite's runner, paused before its first step, and gives where it is: file, line, step text, page, the step's arguments. Then step_move goes on one step (or into a call, out of one, to the end, or stops), each answering where it paused next, whether the step just run failed and why, or how the run ended. One Step-through at a time; no run_scenario meanwhile.", Feature, ScenarioAt, LinkedSuite),
        Writes("step_move", "Moves the paused Step-through: 'over' runs the next step (a call as one), 'into' goes into a call, 'out' to the end of the call, 'continue' to the end (or a breakpoint in the IDE's breakpoints file), 'stop' ends it. Answers where it paused next, or how it ended. A step still running after the wait answers waiting: then 'wait' waits again, or 'stop'.", new("move", "string", "Where to go.", Required: true, Values: ["over", "into", "out", "continue", "wait", "stop"]), StepTimeout),
        Reads("step_state", "The paused Step-through's current page: each control of its page object, its kind, whether it is shown, and what it shows (a toggle's state, a range's value, a selector's choice, or text); with no page current, the page that says it is loaded. Read without a screenshot; where to look when a step is about to fail.", Max),
        Writes("step_screenshot", "A screenshot of the app while the Step-through is paused, saved in the solution's TestResults/step-screenshots: its path, to look at; a head without pixels gives the screen's element tree or markup, inline as tree."),
        Writes("get_coverage", "Which lines of the app and the suite one scenario runs: runs it through the xUnit gate (dotnet test, by its address) with .NET's code coverage and gives, per file, the lines hit and missed as ranges, for the given files or the working tree's changed ones. Proves a new scenario reaches its code (lines, not branches). Says when the build is older than the suite's sources. A process per call.", Feature, ScenarioAt, new("files", "array", "Source files to report, relative to the solution; the working tree's changed files when left out."), LinkedSuite, new("build", "boolean", "Build the suite first; false when left out."), new("head", "string", "BRUINET_HEAD for the run (headless, bunit, web, ...); the server's own when left out."), RunTimeout, Max),
        Reads("get_trace", "What a scenario's newest traced run did, step by step: each step's page, control calls (a slow call or many attempts is a wait), app events, and the page's controls after the failed step. Without step: a row per step, and the failed step with its 10 slowest calls and the controls shown; with step (a step's line): that step in full. The page after every step only with run_scenario's trace: all.", Feature, ScenarioAt, new("step", "integer", "A step's line: that step in full."), LinkedSuite),
        Reads("diff_since_green", "Why a scenario that passed before fails now, from the kept runs (it runs nothing): its newest traced failure against its newest earlier passing run. The first step where they part, a kind (page-changed, data-changed, timing-only, scenario-changed), steps that got much slower, and the files changed since the passing run's commit that touch that step or an earlier one. With no failure or no green run it says so.", Feature, ScenarioAt, LinkedSuite),
        Reads("get_http_calls", "The HTTP calls a scenario made in its newest run that recorded or replayed them: method, URL, status and whether the recording answered it (recorded, matched, missed), the misses, and the recorded calls it no longer makes. Shows why a replayed scenario failed on its backend.", Feature, ScenarioAt, LinkedSuite, Max),
        Reads("list_affected_tests", "The tests and scenarios of a UI suite that a change reaches, each with why, and one dotnet test --filter that runs them; or that the whole suite must run, and why. From the suite's touch map (touch-map.json, written by 'bruinet tests touch-map' after a full run). The change is git's since 'base' with the working tree, or the given files.", new("suite", "string", "A suite's name, or a test project's folder relative to the solution.", Required: true), new("base", "string", "Changed since this commit; HEAD (the working tree's changes) when left out."), new("files", "array", "These changed files instead of git's, relative to the solution."), new("always", "array", "Filter terms run whatever changed, e.g. Gate=Smoke."), Max),
        Writes("rename_step", "Renames a callable step (a '## Step:' heading in a steps file) and every call of it across the suites, each call keeping its values. Give the heading's or a call's line. Without confirm it gives the edits (file, line, before, after); with it, it writes them, checks every changed file as check_scenario does, and puts all back when one fails.", new("path", "string", "The steps file, or a feature file with a call.", Required: true), new("line", "integer", "The heading's or the call's line.", Required: true), new("newPhrase", "string", "The new phrase, with the same {placeholders}.", Required: true), new("confirm", "boolean", "Write the edits; false when left out.")),
        Deletes("delete_results", "Deletes old scenario results from every run, for good: of a scenario (a line of it in 'path'), a feature file ('path') or a suite ('suite'). Only scenario reports and their screenshots; a run left empty goes. Without confirm it gives the counts. " + AsksFirst, new("scope", "string", "What to delete the results of.", Required: true, Values: ["scenario", "file", "suite"]), new("path", "string", "The feature file, for a scenario or a file."), new("line", "integer", "A line of the scenario, for a scenario."), new("suite", "string", "The suite, for a suite (or the one a linked file is picked in)."), Confirm),

        // The Scraper's projects.
        Reads("scrape_list_projects", "The UI test projects the Scraper records into (those with a scraper.config.md): name, folder, platform, start URL, recording sessions. Where the Scraper's work starts.", Max),
        Reads("scrape_next", "Where a scraped project is in the workflow (1 Project, 2 Connect, 3 Collect pages, 4 Review, 5 Controls, 6 Page objects, 7 Verify, 8 Tests): each step's state (Done, Next, Todo, Attention) and summary, and the next thing to do, as the Scraper view shows it.", Project),
        OpensBrowser("scrape_start", "Opens the recording browser (a Chromium window the person works in) on a scraped project's start URL, and starts a recording session in the project's Recordings folder. Each page the app moves to is captured once it settles. One recording at a time.", Project, new("session", "string", "What the session is about, e.g. 'invoices'.", Required: true), new("url", "string", "Where to start; the config's StartUrl when left out."), new("autoCapture", "boolean", "Capture each page the app moves to; true when left out."), new("actions", "boolean", "A test recording: what the person does is recorded too, and the browser has Assert and Stop; false when left out.")),
        Writes("scrape_capture_page", "Captures the page the recording browser shows now. Without a name, a page that did not change since its last capture is not captured again.", new Param("name", "string", "What to call the page, e.g. 'Invoice edit'.")),
        Writes("scrape_stop", "Closes the recording browser. The session stays in the project."),
        Reads("scrape_list_pages", "A scraped project's recorded pages, by page key (the path and hash route with ids as {id}): name, location, the page object made for it, its newest capture (session, event, when, node count) and how many captures it has. With 'page', that page's every capture, newest first.", Project, new("page", "string", "A page key: its captures."), Max),
        Reads("scrape_get_page", "A captured page as an outline: one line per node that matters, '#<node> <type> \"text\" name=... role=... #id .class [data-*]', indented by depth; wrappers with nothing of their own are left out. The newest capture unless 'session' is given. Give 'node' for one node's subtree when the page is large.", Project, PageKey, new("session", "string", "A session id, for an older capture."), new("event", "integer", "The capture's event number in that session."), new("node", "integer", "A node's number: only its subtree."), new("max", "integer", "At most this many lines; 400 when left out.")),
        Reads("scrape_find_similar", "The nodes, on every recorded page's newest capture, of the same kind as a node: the same type, role, class names (generated ones left out) and child types. A pattern on many pages is a control to make.", Project, PageKey, new("node", "integer", "The node's number on the page's newest capture.", Required: true), Max),
        Reads("scrape_find_text", "The nodes, on every recorded page's newest capture, whose text, accessible name or an attribute value holds the words (ignoring case).", Project, new("query", "string", "What to look for.", Required: true), Max),
        Reads("scrape_list_page_objects", "The page objects and controls a scraped project has (and the scraped projects it references): name, kind, base, file, the header saying what it was made from, and each control property with its locator. Reuse these before making new ones.", Project, Max),
        Reads("scrape_detect_patterns", "What the recorded pages are built from, found without a model: parts that repeat across pages or within one (controls to make), and forms, tables, lists, navigation and dialogs (structure a page object names). Each with its kind, on how many pages, how often, examples (page, node) and the control that already stands for it.", Project, Max),
        Reads("scrape_get_control_brief", "Everything known about a pattern, to make a control from it: where to write the file and what to read (nothing else), where it occurs, an example outline with its parent, its parts with relative locators and in how many occurrences each is found, and hints (what the elements are, a base, members). Decide from the facts; the hints help.", Project, new("signature", "string", "The pattern's signature, from scrape_detect_patterns.", Required: true), new("variant", "string", "The pattern's variant, when it has several."), new("lifted", "boolean", "The pattern's lifted, from scrape_detect_patterns."), new("name", "string", "The control's name; the pattern's own when left out.")),
        Reads("scrape_match_controls", "Which of the project's controls occur on a page (by the signature in their header), the page object made for the page, and whether the page changed since it was made.", Project, PageKey, Max),
        Reads("scrape_diff_page", "What changed on a recorded page between two captures: nodes added, removed and changed, as outline lines. By default from the capture its page object was made from (else the one before the newest) to the newest: what to change in the page object after the app changed.", Project, PageKey, new("fromSession", "string", "The older capture's session."), new("fromEvent", "integer", "Its event number in that session."), new("toSession", "string", "The newer capture's session; the newest when left out."), new("toEvent", "integer", "Its event number in that session."), Max),
        Reads("scrape_suggest_controls", "For a recorded page (or one node's subtree): the members a page object would have, each with a name, the Bruinet control that fits, its namespace, and the locator in the preference order (test id, accessible name or text, stable id, stable class, position last) with whether it is stable and why. A node in a frame gives the frame member to declare it on. A starting point: rename and drop as the page needs.", Project, PageKey, new("node", "integer", "Only this node's subtree."), Max),
        Reads("scrape_get_header", "The first line a generated file must have, saying what it was made from: for a page object give the page, for a control also the node it was made from. Put it above the usings.", Project, PageKey, new("node", "integer", "For a control: the node on the page's newest capture.")),
        Writes("scrape_write_file", "Writes a C# file (a page object or control) into the scraped project, at a path relative to it, when its syntax is valid; otherwise gives the syntax errors and writes nothing. Show the person the code first.", Project, new("path", "string", "The file, relative to the project, e.g. Pages/InvoicePage.cs.", Required: true), new("code", "string", "The whole file.", Required: true)),
        Reads("scrape_list_events", "A recording session's events in order: its page captures and, in a test recording, the person's actions (click, input, select, check, key, navigate, dialog, assert) with the node each acted on.", Project, Session, Max),
        Reads("scrape_resolve_events", "A test recording's actions resolved, from files only: each to the page object of its page, the member whose locator finds its node, the control's verb and the catalog phrase; and the gaps, each with why (NoPageObject, NoNode, NoMember, NoVerb, NoPhrase).", Project, Session, Max),
        Reads("scrape_gap_brief", "A gap of a test recording as a brief: what the person did, the element in its capture, what the gap needs (a member, with the one the rules would write; a verb; a page object), the one file to change, and how to check. Fill the gap in the page objects or controls, never in the drafted test; never invent a phrase.", Project, Session, new("event", "integer", "The gap's event number; the first five gaps when left out.")),
        Writes("scrape_draft_test", "Drafts a test recording by rules: an xUnit test calling page objects (Tests/Recorded, with the person's part beside it) and/or a .feature.md scenario of catalog phrases (Features/Recorded), with the gaps marked. Drafted again over its earlier draft, never over a person's file.", Project, Session, new("target", "string", "The config's ## Tests Target when left out.", Values: ["xunit", "scenario", "both"]), new("name", "string", "The test's name; the session's when left out.")),
        Writes("scrape_run_drafts", "Runs each draft of a test recording once, built first: an xUnit draft by its class, a scenario by its address. Gives each run's counts and, when it did not pass, why and the end of the output.", Project, Session, RunTimeout),
        Writes("scrape_build", "Builds the scraped project with its analyzers and gives the errors and warnings. Generated code is done only when this passes: fix what it reports and call it again.", Project),
        OpensBrowser("scrape_explore", "Explores a scraped project's app by itself: visits its pages and the states safe clicks show, and captures each into a new recording session, within the project's Explore guards and budget. Without confirm it gives the summary of host, guards and budget to show the person. With linksOnly (the default) it follows links and lists what it would click. Gives why it stopped, the counts and the report (explore.md). " + AsksFirst, Project, new("url", "string", "Where to start; the config's StartUrl when left out."), new("linksOnly", "boolean", "Follow links only, nothing clicked; true when left out."), new("maxPages", "integer", "Overrides MaxPages."), new("maxDepth", "integer", "Overrides MaxDepth."), new("maxMinutes", "integer", "Overrides MaxMinutes."), new("headless", "boolean", "No browser window; true when left out. A sign-in page stops the run either way."), Confirm),
        Writes("scrape_draft_page_objects", "Drafts a page object for each recorded page (or one), from the suggestions and no model: <Name>.scraped.cs, a partial class a person adds to in <Name>.cs, which a draft never touches. Builds the project; a draft it does not build with is put back, with its errors. Gives each page's outcome, the members a new draft removed, and the repeated patterns to make controls of. A page object made by hand is left alone.", Project, new("page", "string", "A page key; every recorded page when left out.")),
        OpensBrowser("scrape_check_page_objects", "Opens each recorded page in the running app (a browser of its own, under the Explore guards) and looks up every member of the project's page objects as Bruinet would: found once, found several times, or not found. Writes the outcome into the project for the Scraper view. Stop a recording first.", Project, new("headless", "boolean", "No browser window; true when left out.")),
        Writes("scrape_update_config", "Makes one change to a scraped project's scraper.config.md, keeping the rest: 'set' a field (values: field, value; in Danger words: category, true or false), 'add' a row (values: its cells: Explore hosts [Host], Never click and Always click [Match: 'text: ...' or 'selector: ...'], Never visit [Url], Allow requests [Method, Url], Aliases [Url]) or 'remove' one (values: its first cell). " + AsksFirst, Project, new("action", "string", "What to do.", Required: true, Values: ["set", "add", "remove"]), new("section", "string", "The section without ##, e.g. Explore, Never click.", Required: true), new("values", "array", "The values, as strings.", Required: true), Confirm),
        Writes("scrape_create_project", "Makes a scraped UI test project in the solution's folder: its .csproj (referencing Bruinet.Html, and Bruinet.Blazor for blazor), its scraper.config.md and an empty Pages folder. It is not added to the solution. Without confirm it gives what it would write, or what is wrong by field. " + AsksFirst, new("name", "string", "The project's name.", Required: true), new("folder", "string", "Its folder, relative to the solution's; proposed when left out."), new("platform", "string", "html when left out.", Values: ["html", "blazor"]), new("startUrl", "string", "Where recording and Explore start.", Required: true), new("testIds", "array", "The attributes that are test ids, preferred first."), new("references", "string", "Proposed when left out.", Values: ["projects", "packages"]), Confirm),
        Writes("scrape_rename_page_object", "Renames a scraped project's page object for a recorded page: its class, its draft and the person's part, in the files that declare it. Files that only use it are listed, not changed: fix them, then scrape_build. " + AsksFirst, Project, PageKey, new("newName", "string", "The new class name.", Required: true), Confirm),
    ];

    // The instructions an agent reads once: the two families, each from its entry tool (M10).
    private const string Instructions = """
        Bruinet: two families of tools.
        Scenarios (a suite's feature files), from list_suites:
        - list_steps before writing a step; check_scenario before proposing a feature file.
        - run_scenario runs one (build: true after changing C#); a failure comes with its why.
        - On red: list_results names the failures, get_trace gives what a run did, diff_since_green what changed since it passed.
        - step_start and step_move step through one; set_state puts the app in a declared state.
        - When a feature is done: record_video, then write_review.
        Scraper (page objects and tests from recordings), from scrape_list_projects:
        - scrape_next says what to do next.
        - Pages: scrape_list_pages, scrape_get_page, scrape_detect_patterns, scrape_draft_page_objects, scrape_build.
        - A recorded test: scrape_resolve_events, scrape_gap_brief per gap (fill it in page objects, never in the draft), scrape_draft_test, scrape_run_drafts.
        Never invent a phrase: leave a step that has none as a marked gap.
        The person browses and signs in, in the recording browser; never sign in for them.
        """;

    private static Tool Reads(string name, string description, params Param[] parameters) => new(name, description, Schema(parameters), ToolEffect.Reads);

    private static Tool Writes(string name, string description, params Param[] parameters) => new(name, description, Schema(parameters), ToolEffect.Writes);

    private static Tool Deletes(string name, string description, params Param[] parameters) => new(name, description, Schema(parameters), ToolEffect.Deletes);

    private static Tool OpensBrowser(string name, string description, params Param[] parameters) => new(name, description, Schema(parameters), ToolEffect.OpensBrowser);

    // A tool as tools/list gives it: every hint said, since a client takes a missing destructiveHint or openWorldHint as true.
    private static JsonObject Listed(Tool tool)
    {
        var annotations = new JsonObject { ["readOnlyHint"] = tool.Effect == ToolEffect.Reads };
        if (tool.Effect != ToolEffect.Reads)
        {
            annotations["destructiveHint"] = tool.Effect == ToolEffect.Deletes;
        }

        annotations["openWorldHint"] = tool.Effect == ToolEffect.OpensBrowser;
        return new JsonObject
        {
            ["name"] = tool.Name,
            ["description"] = tool.Description,
            ["inputSchema"] = tool.Schema.DeepClone(),
            ["annotations"] = annotations,
        };
    }

    private static JsonObject Schema(Param[] parameters)
    {
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject([.. parameters.Select(parameter => new KeyValuePair<string, JsonNode?>(parameter.Name, parameter.ToJson()))]),
        };
        if (parameters.Where(parameter => parameter.Required).Select(parameter => (JsonNode?)parameter.Name).ToArray() is { Length: > 0 } required)
        {
            schema["required"] = new JsonArray(required);
        }

        return schema;
    }

    // A parameter: its name, JSON type, what it is, whether a call must give it, and the values it takes.
    private sealed record Param(string Name, string Type, string Description, bool Required = false, string[]? Values = null)
    {
        public JsonObject ToJson()
        {
            var property = new JsonObject { ["type"] = Type, ["description"] = Description };
            if (Values is not null)
            {
                property["enum"] = new JsonArray([.. Values.Select(value => (JsonNode?)value)]);
            }

            if (Type == "array")
            {
                property["items"] = new JsonObject { ["type"] = "string" };
            }

            return property;
        }
    }
}
