using Bruinet.Core.Exceptions;

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
/// between attempts is seen. A call made inside an attempt keeps its own answers, but its first
/// attempt also reads what the enclosing attempt already knows: a row predicate's reads asked
/// iOS again for each row's bounds and state, 1.4 s a row (.my/uitests D16). Its later attempts
/// start empty, so a call that waits for a change sees it. What that first attempt read is handed
/// back to the enclosing attempt when it ends: a row search asked each row's nested read the same
/// scope questions again, 0.4 s a row on iOS (.my/uitests D23). An action forgets the whole chain:
/// an outer attempt can act between its inner calls (a UAT step long-presses a card, then reads
/// it), and answers kept from before the action were wrong after it.
/// </para>
/// <para>
/// A read that reports its element stale is kept as that report: an element that is gone stays
/// gone, and on Android each such read made the server search the tree for it again, 2 s a time.
/// Any other throw is not kept.
/// </para>
/// </remarks>
public static class AttemptMemo
{
    [ThreadStatic]
    private static Frame? _frame;

    /// <summary>
    /// The answer to <paramref name="question"/> about <paramref name="subject"/>: read once per
    /// attempt, then kept until the attempt ends. Outside an attempt it is read every time.
    /// </summary>
    public static T Remember<T>(object subject, string question, Func<T> read)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(read);

        if (_frame is not { } frame)
        {
            return read();
        }

        var answers = frame.Answers;
        if (frame.TryRecall((subject, question), out var kept))
        {
            if (kept is Gone gone)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(gone.Report);
            }

            return (T)kept!;
        }

        try
        {
            var answer = read();
            answers[(subject, question)] = answer;
            return answer;
        }
        catch (StaleElementException stale)
        {
            answers[(subject, question)] = new Gone(stale);
            throw;
        }
    }

    /// <summary>
    /// Whether <paramref name="question"/> about <paramref name="subject"/> was answered in this
    /// attempt by a read that did not throw, so a known answer also says the subject was alive when
    /// it was read.
    /// </summary>
    public static bool Knows(object subject, string question)
        => _frame is { } frame && frame.TryRecall((subject, question), out var kept) && kept is not Gone;

    /// <summary>
    /// Drops every answer kept in this attempt and in the attempts it runs inside: an action ran,
    /// and what was read before it may be wrong after it (a scroll moves bounds, a tap enables a
    /// button). Outside an attempt it does nothing.
    /// </summary>
    public static void Forget()
    {
        for (var frame = _frame; frame is not null; frame = frame.Enclosing)
        {
            frame.Answers.Clear();
        }
    }

    /// <summary>Starts an attempt's memo; the enclosing attempt's comes back when it ends.</summary>
    /// <param name="inherit">
    /// Whether reads may also use what the enclosing attempt already knows: a call's first attempt,
    /// not a poll that waits for a change.
    /// </param>
    internal static IDisposable Begin(bool inherit = false)
    {
        var enclosing = _frame;
        var frame = new Frame(enclosing, inherit);
        _frame = frame;
        return new Scope(frame);
    }

    /// <summary>A read that found its element gone.</summary>
    private sealed record Gone(StaleElementException Report);

    /// <summary>One attempt's answers, and the attempt it runs inside.</summary>
    private sealed class Frame(Frame? enclosing, bool inherit)
    {
        public Dictionary<(object Subject, string Question), object?> Answers { get; } = [];

        public Frame? Enclosing { get; } = enclosing;

        /// <summary>This attempt's answer, else, when it inherits, the enclosing attempts' answer.</summary>
        public bool TryRecall((object Subject, string Question) key, out object? kept)
        {
            for (var frame = this; frame is not null; frame = frame.Enclosing)
            {
                if (frame.Answers.TryGetValue(key, out kept))
                {
                    return true;
                }

                if (!frame._inherit)
                {
                    break;
                }
            }

            kept = null;
            return false;
        }

        /// <summary>
        /// Hands this attempt's answers to the attempt it inherits from, where that one has none of
        /// its own: both read the app at the same moment, and an action in between cleared both.
        /// </summary>
        public void HandBack()
        {
            if (!_inherit || Enclosing is not { } enclosing)
            {
                return;
            }

            foreach (var (key, answer) in Answers)
            {
                _ = enclosing.Answers.TryAdd(key, answer);
            }
        }

        private readonly bool _inherit = inherit;
    }

    private sealed class Scope(Frame frame) : IDisposable
    {
        public void Dispose()
        {
            frame.HandBack();
            _frame = frame.Enclosing;
        }
    }
}
