using System.Text.Json.Serialization;

namespace Bruinet.Bdd.Runner;

/// <summary>
/// A message between a host (Presenter) and the runner process that runs a suite's scenarios.
/// Text and numbers only: no engine or suite type crosses.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(BddRunnerHello), "hello")]
[JsonDerivedType(typeof(BddRunnerOpen), "open")]
[JsonDerivedType(typeof(BddRunnerOpened), "opened")]
[JsonDerivedType(typeof(BddRunnerNext), "next")]
[JsonDerivedType(typeof(BddRunnerSkip), "skip")]
[JsonDerivedType(typeof(BddRunnerStep), "step")]
[JsonDerivedType(typeof(BddRunnerRecord), "record")]
[JsonDerivedType(typeof(BddRunnerRehearse), "rehearse")]
[JsonDerivedType(typeof(BddRunnerFinishRecording), "finishRecording")]
[JsonDerivedType(typeof(BddRunnerDropRecording), "dropRecording")]
[JsonDerivedType(typeof(BddRunnerRecorded), "recorded")]
[JsonDerivedType(typeof(BddRunnerSay), "say")]
[JsonDerivedType(typeof(BddRunnerScreenshot), "screenshot")]
[JsonDerivedType(typeof(BddRunnerBreak), "break")]
[JsonDerivedType(typeof(BddRunnerScreenshotTaken), "screenshotTaken")]
[JsonDerivedType(typeof(BddRunnerReadPage), "readPage")]
[JsonDerivedType(typeof(BddRunnerPageState), "pageState")]
[JsonDerivedType(typeof(BddRunnerClose), "close")]
[JsonDerivedType(typeof(BddRunnerPrepare), "prepare")]
[JsonDerivedType(typeof(BddRunnerCancel), "cancel")]
[JsonDerivedType(typeof(BddRunnerExit), "exit")]
[JsonDerivedType(typeof(BddRunnerDone), "done")]
[JsonDerivedType(typeof(BddRunnerFailed), "failed")]
public abstract record BddRunnerMessage;

/// <summary>The runner's first message: the protocol it speaks.</summary>
/// <param name="Version">Its <see cref="BddRunnerProtocol.Version"/>.</param>
public sealed record BddRunnerHello(int Version) : BddRunnerMessage;

/// <summary>Opens a scenario: the fixture is made and the scenario bound. Answered by <see cref="BddRunnerOpened"/> or <see cref="BddRunnerFailed"/>.</summary>
/// <param name="Config">The suite's config file.</param>
/// <param name="Pages">The suite's Pages assembly, which has the fixture.</param>
/// <param name="Feature">The feature file.</param>
/// <param name="Scenario">The scenario's name.</param>
/// <param name="Mode">What the run is, in its result's metadata (<c>runner</c>, <c>step</c>, <c>run</c>); null for <c>runner</c>.</param>
public sealed record BddRunnerOpen(string Config, string Pages, string Feature, string Scenario, string? Mode = null) : BddRunnerMessage;

/// <summary>The scenario is open, before its first step.</summary>
/// <param name="Steps">How many steps it runs.</param>
/// <param name="Discovery">The discovery report.</param>
/// <param name="Catalog">The command catalog report.</param>
/// <param name="Next">Where its first step is; null when it has none.</param>
/// <param name="Skip">Why a run leaves this scenario out here (a head, <c>@browser</c>, a skip rule); null when it runs.</param>
public sealed record BddRunnerOpened(int Steps, string Discovery, string Catalog, BddRunnerPosition? Next = null, string? Skip = null) : BddRunnerMessage;

/// <summary>Runs the next step. Answered by <see cref="BddRunnerStep"/>.</summary>
public sealed record BddRunnerNext : BddRunnerMessage;

/// <summary>Passes over the next step without running it. Answered by <see cref="BddRunnerStep"/>.</summary>
/// <param name="Message">Why it was skipped.</param>
public sealed record BddRunnerSkip(string? Message) : BddRunnerMessage;

/// <summary>A step's result.</summary>
/// <param name="Index">The step, from 0.</param>
/// <param name="Status">How it ended.</param>
/// <param name="Message">What it said; a failure's message.</param>
/// <param name="HasNext">Whether a step is left.</param>
/// <param name="Report">Every step so far, as the diagnostics show them.</param>
/// <param name="Diagnostics">The context's runtime trace so far.</param>
/// <param name="Ran">Where the step that ran is.</param>
/// <param name="Next">Where the next step is; null when none is left.</param>
public sealed record BddRunnerStep(int Index, BddStepResultStatus Status, string? Message, bool HasNext, IReadOnlyList<string> Report, IReadOnlyList<string> Diagnostics, BddRunnerPosition? Ran = null, BddRunnerPosition? Next = null) : BddRunnerMessage;

/// <summary>
/// Where a step is, for a host that pauses before steps and steps over, into and out of
/// <c>## Step:</c> calls: the step's own line, and the calls that put it there.
/// </summary>
/// <param name="File">The step's file: the feature file, or the step file of a call's body.</param>
/// <param name="Line">The step's line.</param>
/// <param name="Text">The step's text, with a call's values filled in.</param>
/// <param name="Depth">How many calls deep it is: 0 for a step written in the scenario.</param>
/// <param name="Calls">The calls around it, outermost first.</param>
public sealed record BddRunnerPosition(string? File, int Line, string Text, int Depth, IReadOnlyList<BddRunnerCall> Calls)
{
    /// <summary>The step's arguments, as its definition receives them; null when not known.</summary>
    public IReadOnlyDictionary<string, string>? Arguments { get; init; }

    /// <summary>The app it runs on; null for the fixture's own.</summary>
    public string? App { get; init; }

    /// <summary>The page the scenario is on when the step is reached; null when not known.</summary>
    public string? Page { get; init; }
}

/// <summary>A <c>## Step:</c> call a step runs inside.</summary>
/// <param name="Text">The call as written.</param>
/// <param name="File">The file the call is written in.</param>
/// <param name="Line">The call's line, or its row's for a per-row call.</param>
/// <param name="Depth">1 for a call written in the scenario.</param>
public sealed record BddRunnerCall(string Text, string? File, int Line, int Depth);

/// <summary>A spoken line, made before the run.</summary>
/// <param name="Text">What is said.</param>
/// <param name="Wav">Its sound file; null when only its length is known.</param>
/// <param name="Seconds">How long it takes.</param>
public sealed record BddRunnerClip(string Text, string? Wav, double Seconds);

/// <summary>A scene's spoken lines.</summary>
/// <param name="Intro">Said before the first step.</param>
/// <param name="Says">Said before a step, by the step's line in the feature file.</param>
/// <param name="Outro">Said after the last step.</param>
public sealed record BddRunnerClips(BddRunnerClip? Intro, IReadOnlyDictionary<int, BddRunnerClip> Says, BddRunnerClip? Outro);

/// <summary>Records the open scenario as a scene, from its first step. Answered by <see cref="BddRunnerDone"/>.</summary>
/// <param name="Number">The scene's number, from 1.</param>
/// <param name="Name">The scene's name.</param>
/// <param name="Scenario">The scenario's address.</param>
/// <param name="Folder">The scene's folder: its video and timeline go there.</param>
/// <param name="Hash">What the recording is made from; null when the scene is not kept.</param>
/// <param name="Clips">Its spoken lines.</param>
/// <param name="BeatMilliseconds">The wait after a step.</param>
/// <param name="SpeakAloud">Whether each line is announced (<see cref="BddRunnerSay"/>) for the host to play.</param>
/// <param name="PauseMilliseconds">The least time an action takes while the scene records; null for the default.</param>
/// <param name="SayBefore">Whether a step waits for its line, rather than running while it plays.</param>
public sealed record BddRunnerRecord(int Number, string Name, string Scenario, string Folder, string? Hash, BddRunnerClips Clips, double BeatMilliseconds, bool SpeakAloud, double? PauseMilliseconds = null, bool SayBefore = false) : BddRunnerMessage;

/// <summary>Paces the open scenario by its spoken lines without recording it. Answered by <see cref="BddRunnerDone"/>.</summary>
/// <param name="Clips">The spoken lines.</param>
/// <param name="BeatMilliseconds">The wait after a step.</param>
/// <param name="SpeakAloud">Whether each line is announced for the host to play.</param>
public sealed record BddRunnerRehearse(BddRunnerClips Clips, double BeatMilliseconds, bool SpeakAloud) : BddRunnerMessage;

/// <summary>After the last step: the outro, and a recording's video and timeline are finished. Answered by <see cref="BddRunnerRecorded"/>.</summary>
public sealed record BddRunnerFinishRecording : BddRunnerMessage;

/// <summary>Gives up the recording or rehearsal. Answered by <see cref="BddRunnerDone"/>.</summary>
public sealed record BddRunnerDropRecording : BddRunnerMessage;

/// <summary>A recording or rehearsal ended.</summary>
/// <param name="Timeline">The scene's timeline file; null for a rehearsal.</param>
/// <param name="Warnings">What went less well than asked.</param>
public sealed record BddRunnerRecorded(string? Timeline, IReadOnlyList<string> Warnings) : BddRunnerMessage;

/// <summary>From the runner, while a step waits for its line: the host plays it. Not answered.</summary>
/// <param name="Wav">The line's sound file.</param>
/// <param name="Seconds">How long it takes.</param>
public sealed record BddRunnerSay(string Wav, double Seconds) : BddRunnerMessage;

/// <summary>Takes a screenshot of the open scenario's app between steps. Answered by <see cref="BddRunnerScreenshotTaken"/> or <see cref="BddRunnerFailed"/>.</summary>
/// <param name="Description">Put in the file's name.</param>
public sealed record BddRunnerScreenshot(string Description) : BddRunnerMessage;

/// <summary>
/// Debug from here (AD-022): while paused between steps, waits for a debugger to
/// attach (or asks Windows for one), then breaks before the next step that runs, so the debugger
/// stops there and Step into goes into its step definition. Answered by <see cref="BddRunnerDone"/>
/// once a debugger is attached, or <see cref="BddRunnerFailed"/> when none came.
/// </summary>
/// <param name="WaitSeconds">How long to wait for the debugger.</param>
/// <param name="Launch">Ask Windows to start one (<c>Debugger.Launch</c>), for a host that is no IDE (the Presenter).</param>
public sealed record BddRunnerBreak(double WaitSeconds, bool Launch = false) : BddRunnerMessage;

/// <summary>A screenshot was taken.</summary>
/// <param name="Path">Its file.</param>
public sealed record BddRunnerScreenshotTaken(string Path) : BddRunnerMessage;

/// <summary>Reads the current page's controls between steps. Answered by <see cref="BddRunnerPageState"/> or <see cref="BddRunnerFailed"/>.</summary>
public sealed record BddRunnerReadPage : BddRunnerMessage;

/// <summary>The current page's controls and what each shows.</summary>
/// <param name="Page">The page's name; null when no page is current.</param>
/// <param name="Controls">Each control of its page object.</param>
/// <param name="Reason">Why there are none, when there are none.</param>
public sealed record BddRunnerPageState(string? Page, IReadOnlyList<BddRunnerControlState> Controls, string? Reason) : BddRunnerMessage;

/// <summary>A control of the current page and what it shows.</summary>
/// <param name="Name">The control's name on its page object.</param>
/// <param name="Kind">The control's type, without its scope.</param>
/// <param name="Shown">Whether it is shown; null when it could not be read.</param>
/// <param name="Value">Its toggle state, value, choice or text; null for a control without one, or one not shown.</param>
/// <param name="Unreadable">Why it could not be read, when it could not.</param>
public sealed record BddRunnerControlState(string Name, string Kind, bool? Shown, string? Value, string? Unreadable);

/// <summary>Closes the open scenario: its fixture and app. Answered by <see cref="BddRunnerDone"/>.</summary>
/// <param name="Keep">
/// Keep the fixture and its app for the next open of the same suite, when the fixture starts a
/// scenario clean without a launch (<c>IUatScenarioLifecycle.StartsCleanWithoutLaunch</c>; AD-025).
/// </param>
public sealed record BddRunnerClose(bool Keep = false) : BddRunnerMessage;

/// <summary>
/// Makes the suite's fixture, and so starts its app, before any scenario opens: the next open uses
/// it (a warm runner's, AD-025). Answered by <see cref="BddRunnerDone"/> or <see cref="BddRunnerFailed"/>.
/// </summary>
/// <param name="Config">The suite's config file.</param>
/// <param name="Pages">The suite's Pages assembly, which has the fixture.</param>
public sealed record BddRunnerPrepare(string Config, string Pages) : BddRunnerMessage;

/// <summary>Cancels the step that is running. Not answered: the step's own answer says it was canceled.</summary>
public sealed record BddRunnerCancel : BddRunnerMessage;

/// <summary>Ends the runner. Not answered.</summary>
public sealed record BddRunnerExit : BddRunnerMessage;

/// <summary>A request was carried out.</summary>
public sealed record BddRunnerDone : BddRunnerMessage;

/// <summary>A request failed.</summary>
/// <param name="Message">Why.</param>
public sealed record BddRunnerFailed(string Message) : BddRunnerMessage;
