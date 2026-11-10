using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bruinet.Bdd;

/// <summary>
/// Where a step session stops for the debugger: before a step whose line is listed, and after a
/// step that fails when asked to. The seam a test replaces; the running default is
/// <see cref="BddBreakpoints"/>, which only exists while a debugger is attached.
/// </summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public interface IBddBreakHook
{
    /// <summary>The break before a step, or null to run on.</summary>
    /// <param name="invocation">The step about to run.</param>
    /// <param name="context">The scenario's context, for the app and the page.</param>
    BddStepBreak? Before(BddStepInvocation invocation, BddExecutionContext context);

    /// <summary>Whether to stop after a step that failed, with its result in view.</summary>
    /// <param name="result">The failed step's result.</param>
    bool AfterFailure(BddStepResult result);

    /// <summary>Stops: <see cref="Debugger.Break"/> for the running default.</summary>
    /// <param name="stepBreak">What the developer sees in Locals.</param>
    void Break(BddStepBreak stepBreak);
}

/// <summary>What a break before a step shows: the step, its arguments as read, where it runs, and how it got there.</summary>
/// <param name="Text">The step's text, with a callable's values filled in.</param>
/// <param name="Arguments">Its arguments, as the step definition receives them.</param>
/// <param name="App">The app it runs on; null for the fixture's own.</param>
/// <param name="Page">The page the scenario is on; null when not known.</param>
/// <param name="Source">Where the step is written: the feature file, or a step file for a callable's step.</param>
/// <param name="Calls">The calls that put it there, outermost first: <c>Step: the synced list has todos (Todo.steps.md:3) at TOD.01.feature.md:34</c>.</param>
/// <param name="Result">For a break after a failure, the failed step's result; null before a step.</param>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed record BddStepBreak(
    string Text,
    IReadOnlyDictionary<string, string> Arguments,
    string? App,
    string? Page,
    BddSourceLocation Source,
    IReadOnlyList<string> Calls,
    BddStepResult? Result = null)
{
    /// <summary>How many <c>## Step:</c> calls deep the step runs: 0 for a scenario's own step.</summary>
    public int Depth { get; init; }

    /// <summary>Whether the run stops at a call (its line), before the call's first step; Step into goes inside.</summary>
    public bool IsCall { get; init; }
}

/// <summary>A move from a paused step, as the IDE's step commands write it.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public enum BddStepMove
{
    /// <summary>The next step at the paused step's depth or above: a call runs whole.</summary>
    Over,

    /// <summary>The next step, inside a call's steps when the paused step is a call.</summary>
    Into,

    /// <summary>The first step after the call the paused step is in.</summary>
    Out,
}

/// <summary>
/// The step breakpoints an IDE sets (R2 of the IDE plan): a per-user file,
/// <c>%LOCALAPPDATA%\Bruinet\bdd-breakpoints.json</c>, with a list of lines per file. It is a file,
/// not an environment variable, so a Debug started from Test Explorer honours it too. It is only
/// read while a debugger is attached, and read again when it changes.
/// </summary>
/// <remarks>
/// <code>{ "files": { "C:\\src\\Suite\\Features\\TOD.01.feature.md": [19, 25] }, "breakOnFailure": false,
///   "next": { "mode": "over", "depth": 1, "process": 4711 } }</code>
/// A file matches a step's file when the paths are equal, or when both are under a
/// <c>Features</c> folder with the same path below it: a test runs the build output's copy of a
/// file the IDE lists by its source path. <c>next</c> is a one-shot move from a paused step
/// (<see cref="BddStepMove"/>), for the test process that paused; the break it makes removes it.
/// While paused, <see cref="PausedPath"/> says where: the IDE's step commands and preview read it.
/// </remarks>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed class BddBreakpoints : IBddBreakHook
{
    private readonly string _path;
    private readonly Func<string, string?> _read;
    private readonly Action<string, string?> _write;
    private readonly Action _stop;
    private IReadOnlyList<(string File, HashSet<int> Lines)> _files = [];
    private bool _breakOnFailure;
    private (BddStepMove Mode, int Depth, int Process)? _next;
    private BddCallFrame? _lastCall;
    private BddCallFrame? _previousCall;
    private List<BddStepBreak> _positions = [];

    /// <summary>Reads breakpoints from <paramref name="path"/>.</summary>
    /// <param name="path">The breakpoints file.</param>
    /// <param name="read">Reads a file; null when it does not exist.</param>
    /// <param name="write">Writes a file whole, or deletes it for null.</param>
    /// <param name="stop">Stops for the debugger; <see cref="Debugger.Break"/> by default.</param>
    public BddBreakpoints(string path, Func<string, string?>? read = null, Action<string, string?>? write = null, Action? stop = null)
    {
        _path = path;
        _read = read ?? ReadWhole;
        _write = write ?? WriteWhole;
        _stop = stop ?? Debugger.Break;
    }

    /// <summary>The file that says where a run is paused, beside the breakpoints file: <c>bdd-paused.json</c>.</summary>
    public string PausedPath => Path.Combine(Path.GetDirectoryName(_path) ?? string.Empty, "bdd-paused.json");

    /// <summary>The per-user breakpoints file.</summary>
    public static string DefaultPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Bruinet", "bdd-breakpoints.json");

    /// <summary>The breakpoints of <see cref="DefaultPath"/> while a debugger is attached; null otherwise, so nothing is read.</summary>
    /// <param name="attached">Whether a debugger is attached; <see cref="Debugger.IsAttached"/> by default.</param>
    /// <param name="path">The breakpoints file; <see cref="DefaultPath"/> by default.</param>
    public static IBddBreakHook? ForDebugger(Func<bool>? attached = null, string? path = null)
        => (attached ?? (() => Debugger.IsAttached))() ? new BddBreakpoints(path ?? DefaultPath) : null;

    /// <summary>
    /// Whether the breakpoints file lists a line now, matched as a debugged run matches it (a build
    /// output's copy of a file matches its source): for a host that pauses between steps itself.
    /// </summary>
    /// <param name="file">The step's or call's file.</param>
    /// <param name="line">Its line.</param>
    /// <param name="path">The breakpoints file; <see cref="DefaultPath"/> by default.</param>
    public static bool Lists(string? file, int line, string? path = null)
    {
        var breakpoints = new BddBreakpoints(path ?? DefaultPath);
        breakpoints.Refresh();
        return breakpoints.IsListed(new BddSourceLocation(file, line));
    }

    /// <inheritdoc />
    public BddStepBreak? Before(BddStepInvocation invocation, BddExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(context);
        Refresh();

        // The step's own line (in a step file: each call of it), or the call's line, once, before its first step.
        var call = invocation.Call;
        var outermost = call is { } frame ? Outermost(frame) : null;
        var atCall = outermost is not null && !ReferenceEquals(outermost, _lastCall) && IsListed(outermost.Call.Source);
        _lastCall = outermost;
        var firstNew = FirstNewCall(call);
        var moved = TakeMove(call, firstNew);
        _previousCall = call;
        _positions = [];
        if (!IsListed(invocation.Step.Source) && !atCall && !moved)
        {
            return null;
        }

        // Where it stops: at the outermost call this step starts, then (Into) each call inside it,
        // then the step. A breakpoint on the step's own line stops at the step.
        var depth = call?.Depth ?? 0;
        var from = (atCall || moved) && firstNew is { } level ? level : depth + 1;
        for (var at = from; at <= depth; at++)
        {
            _positions.Add(DescribeCall(call!.Chain[at - 1], context));
        }

        _positions.Add(Describe(invocation, context));
        return _positions[0];
    }

    /// <inheritdoc />
    public bool AfterFailure(BddStepResult result)
    {
        Refresh();
        return _breakOnFailure;
    }

    /// <summary>
    /// Says where the run is paused (<see cref="PausedPath"/>), stops, and clears it when the run
    /// goes on. Paused at a call, a Step into moves to the next position inside it and stops again,
    /// before anything runs.
    /// </summary>
    /// <param name="stepBreak">The position the run stops at, or the failed step.</param>
    [DebuggerHidden]
    public void Break(BddStepBreak stepBreak)
    {
        ArgumentNullException.ThrowIfNull(stepBreak);
        var at = _positions.IndexOf(stepBreak);
        var current = stepBreak;
        try
        {
            while (true)
            {
                WritePaused(current);
                _stop();
                if (at < 0 || at + 1 >= _positions.Count || !TakeInto(current.Depth))
                {
                    break;
                }

                current = _positions[++at];
            }
        }
        finally
        {
            _write(PausedPath, null);
        }
    }

    private void WritePaused(BddStepBreak position)
        => _write(PausedPath, JsonSerializer.Serialize(new
        {
            process = Environment.ProcessId,
            file = position.Source.FilePath,
            line = position.Source.LineNumber,
            depth = position.Depth,
            isCall = position.IsCall,
            text = position.Text,
            calls = position.Calls,
            failed = position.Result is not null,
            arguments = position.IsCall || position.Arguments.Count == 0 ? null : position.Arguments,
            app = position.App,
            page = position.Page,
        }));

    // A move for this process that the step fits: it stops here, once. "Over" at depth d stops at a
    // step at depth d or less, or at a call starting at level d + 1; "out" of depth d is "over" at
    // d - 1; "into" stops at the next step.
    private bool TakeMove(BddCallFrame? call, int? firstNew)
    {
        if (_next is not { } move || move.Process != Environment.ProcessId)
        {
            return false;
        }

        var depth = call?.Depth ?? 0;
        var level = move.Mode == BddStepMove.Out ? move.Depth - 1 : move.Depth;
        var fits = move.Mode == BddStepMove.Into || depth <= level || firstNew <= level + 1;
        if (fits)
        {
            Consume();
        }

        return fits;
    }

    // Paused at a call: a Step into for it moves inside without running anything.
    private bool TakeInto(int depth)
    {
        Refresh();
        if (_next is not { Mode: BddStepMove.Into } move || move.Process != Environment.ProcessId || move.Depth != depth)
        {
            return false;
        }

        Consume();
        return true;
    }

    private void Consume()
    {
        _next = null;
        if (_read(_path) is not { } json)
        {
            return;
        }

        try
        {
            if (JsonNode.Parse(json) is JsonObject root && root.Remove("next"))
            {
                _write(_path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch (JsonException)
        {
            // Being rewritten by the IDE: the move is already taken in memory.
        }
    }

    // The outermost level (1 = a call written in the scenario) at which this step starts a call:
    // its frame there is not the step before's. Null when it starts none.
    private int? FirstNewCall(BddCallFrame? call)
    {
        var chain = call?.Chain ?? [];
        var previous = _previousCall?.Chain ?? [];
        for (var level = 1; level <= chain.Count; level++)
        {
            if (level > previous.Count || !ReferenceEquals(chain[level - 1], previous[level - 1]))
            {
                return level;
            }
        }

        return null;
    }

    // A call as a position to stop at: its line, the calls around it, and the step depth it is written at.
    private static BddStepBreak DescribeCall(BddCallFrame frame, BddExecutionContext context)
        => new(
            frame.Call.Text,
            frame.Values,
            context.CurrentSession,
            context.CurrentPageName,
            frame.Location,
            frame.Parent?.Chain.Select(outer => $"{outer.Callable} ({Where(outer.Callable.Source)}) at {Where(outer.Location)}").ToArray() ?? [])
        {
            Depth = frame.Depth - 1,
            IsCall = true,
        };

    // Written beside and moved into place, so a reader never sees half a file. The IDE, or a virus
    // scan, may hold the file for a moment: the write is tried again, and then given up, since
    // these files only inform the IDE and must never fail the step the run is on.
    private static void WriteWhole(string file, string? content)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (content is null)
                {
                    File.Delete(file);
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                var temporary = file + ".tmp";
                File.WriteAllText(temporary, content);
                File.Move(temporary, file, overwrite: true);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt == WriteAttempts)
                {
                    Trace.TraceWarning($"Bruinet could not write {file}: {exception.Message}");
                    return;
                }

                Thread.Sleep(WriteRetryPause);
            }
        }
    }

    // A file the IDE is replacing just now is read again, as a write is.
    private static string? ReadWhole(string file)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return File.Exists(file) ? File.ReadAllText(file) : null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt == WriteAttempts)
                {
                    Trace.TraceWarning($"Bruinet could not read {file}: {exception.Message}");
                    return null;
                }

                Thread.Sleep(WriteRetryPause);
            }
        }
    }

    // How often, and how far apart, a file another process holds is tried.
    private const int WriteAttempts = 10;
    private static readonly TimeSpan WriteRetryPause = TimeSpan.FromMilliseconds(20);

    /// <summary>What a break shows for a step.</summary>
    /// <param name="invocation">The step.</param>
    /// <param name="context">Its scenario's context.</param>
    /// <param name="result">The failed result, for a break after a failure.</param>
    public static BddStepBreak Describe(BddStepInvocation invocation, BddExecutionContext context, BddStepResult? result = null)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(context);
        var calls = invocation.Call?.Chain.Select(frame => $"{frame.Callable} ({Where(frame.Callable.Source)}) at {Where(frame.Location)}").ToArray() ?? [];
        return new BddStepBreak(
            invocation.Step.Text,
            invocation.Arguments,
            invocation.App ?? context.CurrentSession,
            context.CurrentPageName,
            invocation.Step.Source,
            calls,
            result)
        {
            Depth = invocation.Call?.Depth ?? 0,
        };
    }

    private bool IsListed(BddSourceLocation location)
        => location.FilePath is { } file && _files.Any(listed => listed.Lines.Contains(location.LineNumber) && SameFile(listed.File, file));

    // Equal, or the same below a Features folder: the build output's copy of a listed source file.
    private static bool SameFile(string listed, string actual)
    {
        var a = Path.GetFullPath(listed);
        var b = Path.GetFullPath(actual);
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return BelowFeatures(a) is { } below && string.Equals(below, BelowFeatures(b), StringComparison.OrdinalIgnoreCase);
    }

    private static string? BelowFeatures(string path)
    {
        var root = BddScenarioFiles.ScenarioRoot(path);
        return Path.GetFileName(root).Equals(BddFileFormat.FeatureFolder, StringComparison.OrdinalIgnoreCase)
            ? Path.GetRelativePath(root, path)
            : null;
    }

    private void Refresh()
    {
        // Read at every step: a small file, only while debugging, and a move the IDE writes within the
        // same clock tick as the last read must not be missed.
        _files = [];
        _breakOnFailure = false;
        _next = null;
        if (_read(_path) is not { } json)
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Object)
            {
                _files = files.EnumerateObject()
                    .Select(file => (file.Name, file.Value.ValueKind == JsonValueKind.Array
                        ? file.Value.EnumerateArray().Where(line => line.ValueKind == JsonValueKind.Number).Select(line => line.GetInt32()).ToHashSet()
                        : []))
                    .ToList();
            }

            _breakOnFailure = root.TryGetProperty("breakOnFailure", out var onFailure) && onFailure.ValueKind == JsonValueKind.True;
            if (root.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.Object &&
                next.TryGetProperty("mode", out var mode) && Enum.TryParse<BddStepMove>(mode.GetString(), ignoreCase: true, out var move) &&
                next.TryGetProperty("depth", out var depth) && depth.TryGetInt32(out var atDepth) &&
                next.TryGetProperty("process", out var process) && process.TryGetInt32(out var processId))
            {
                _next = (move, atDepth, processId);
            }
        }
        catch (JsonException)
        {
            // A file the IDE is writing just now: no breakpoints until it is whole.
            _files = [];
        }
    }

    private static BddCallFrame Outermost(BddCallFrame frame)
    {
        while (frame.Parent is { } parent)
        {
            frame = parent;
        }

        return frame;
    }

    private static string Where(BddSourceLocation location)
        => location.FilePath is { } file ? $"{Path.GetFileName(file)}:{location.LineNumber}" : $"line {location.LineNumber}";
}
