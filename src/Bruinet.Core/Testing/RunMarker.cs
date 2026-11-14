using System.Globalization;

namespace Bruinet.Core.Testing;

/// <summary>
/// The mark of one test run on a shared environment: every name a run creates starts with it (<c>BRN-1115-0041</c>), so
/// clean-up finds what this run, or an earlier one that failed, left behind. One per process, unless the runner sets
/// <c>BRUINET_RUN_MARKER</c> so that several processes share it.
/// </summary>
public static class RunMarker
{
    /// <summary>What every marker starts with.</summary>
    public const string Prefix = "BRN-";

    /// <summary>The environment variable that sets the marker for the whole run.</summary>
    public const string Variable = "BRUINET_RUN_MARKER";

    private static readonly Lazy<string> Marker = new(() =>
        Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } given ? given : For(DateTime.Now));

    /// <summary>This run's marker.</summary>
    public static string Current => Marker.Value;

    /// <summary>The marker of a run started at <paramref name="started"/>: <c>BRN-MMdd-HHmm</c>.</summary>
    public static string For(DateTime started) => Prefix + started.ToString("MMdd-HHmm", CultureInfo.InvariantCulture);

    /// <summary>Whether <paramref name="text"/> carries a run's marker (any run's), at its start.</summary>
    public static bool IsMarked(string? text) =>
        text is { Length: >= 13 } && text.StartsWith(Prefix, StringComparison.Ordinal)
        && DateTime.TryParseExact(text.AsSpan(4, 9), "MMdd-HHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}

/// <summary>
/// What a test made, undone in reverse order when it is disposed: each create registers its undo right after it
/// succeeds, so a failing test still cleans up. An undo that fails is collected, not thrown over the test's own failure.
/// </summary>
public sealed class CleanUpRegistry : IDisposable
{
    private readonly Stack<(string What, Action Undo)> _undos = new();
    private readonly List<string> _failures = [];

    /// <summary>The undos that failed, each as "what: why".</summary>
    public IReadOnlyList<string> Failures => _failures;

    /// <summary>Registers the undo of something just made.</summary>
    /// <param name="what">What it undoes, for the failure list ("delete hour entry BRN-1115-0041").</param>
    /// <param name="undo">The undo.</param>
    public void Add(string what, Action undo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(what);
        ArgumentNullException.ThrowIfNull(undo);
        _undos.Push((what, undo));
    }

    /// <summary>Runs every undo, the last made first.</summary>
    public void Dispose()
    {
        while (_undos.TryPop(out var item))
        {
            try
            {
                item.Undo();
            }
#pragma warning disable CA1031 // An undo's failure must not hide the test's own; it is collected instead.
            catch (Exception e)
#pragma warning restore CA1031
            {
                _failures.Add($"{item.What}: {e.Message}");
            }
        }
    }
}
