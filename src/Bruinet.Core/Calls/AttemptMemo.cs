namespace Bruinet.Core.Calls;

/// <summary>
/// Readiness answers kept for the length of one attempt of a call: the same element asked the same
/// question twice in one attempt is asked the driver once.
/// </summary>
/// <remarks>
/// <para>
/// A read checks its scope chain (row, list, page), and each scope re-checks its root as its child
/// resolves: one text read on Android asked the page root for its bounds four times, and readiness
/// reads were 70 % of a list test's Appium requests (.my/android-performance, P2).
/// </para>
/// <para>
/// Only within an attempt (call model R2): the next attempt asks afresh, so state the app changes
/// between attempts is seen. A call made inside an attempt has a memo of its own, not the outer
/// one: an outer attempt can act between its inner calls (a UAT step long-presses a card, then
/// reads it), and answers kept from before the action were wrong after it. A read that throws (a
/// stale element) is not kept.
/// </para>
/// </remarks>
public static class AttemptMemo
{
    [ThreadStatic]
    private static Dictionary<(object Subject, string Question), object?>? _answers;

    /// <summary>
    /// The answer to <paramref name="question"/> about <paramref name="subject"/>: read once per
    /// attempt, then kept until the attempt ends. Outside an attempt it is read every time.
    /// </summary>
    public static T Remember<T>(object subject, string question, Func<T> read)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(read);

        if (_answers is not { } answers)
        {
            return read();
        }

        if (answers.TryGetValue((subject, question), out var kept))
        {
            return (T)kept!;
        }

        var answer = read();
        answers[(subject, question)] = answer;
        return answer;
    }

    /// <summary>
    /// Whether <paramref name="question"/> about <paramref name="subject"/> was answered in this
    /// attempt. An answer is kept only when its read did not throw, so a kept one also says the
    /// subject was alive when it was read.
    /// </summary>
    public static bool Knows(object subject, string question)
        => _answers is { } answers && answers.ContainsKey((subject, question));

    /// <summary>
    /// Drops every answer kept in this attempt: an action ran, and what was read before it may be
    /// wrong after it (a scroll moves bounds, a tap enables a button). Outside an attempt it does nothing.
    /// </summary>
    public static void Forget() => _answers?.Clear();

    /// <summary>Starts an attempt's memo; the enclosing attempt's comes back when it ends.</summary>
    internal static IDisposable Begin()
    {
        var enclosing = _answers;
        _answers = [];
        return new Scope(enclosing);
    }

    private sealed class Scope(Dictionary<(object Subject, string Question), object?>? enclosing) : IDisposable
    {
        public void Dispose() => _answers = enclosing;
    }
}
