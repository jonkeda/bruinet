using System.Diagnostics;
using Bruinet.Core.Logging;

namespace Bruinet.Core.Calls;

/// <summary>
/// One public call on a control or scope: one log entry/exit pair around the whole of it, the
/// action included (R1). Every stack logs in the same format, so one log analyser reads them all.
/// </summary>
public sealed class ControlCall
{
    private readonly ITestLogger? _logger;
    private readonly string _testName;
    private readonly string _pageName;
    private readonly string _controlId;
    private readonly NearMissSettings _nearMiss;

    /// <param name="logger">Where the entry and exit go, or null for none.</param>
    /// <param name="testName">
    /// The test context's name, so a scenario with several apps can tell their calls apart (?A10);
    /// "Test" when null.
    /// </param>
    /// <param name="nearMiss">When a call that succeeded is still reported; the defaults when null.</param>
    /// <param name="pageName">The scope the call stands in, for the log.</param>
    /// <param name="controlId">The control, for the log.</param>
    public ControlCall(ITestLogger? logger, string? testName, NearMissSettings? nearMiss, string pageName, string controlId)
    {
        _logger = logger;
        _testName = testName ?? "Test";
        _nearMiss = nearMiss ?? NearMissSettings.Default;
        _pageName = pageName;
        _controlId = controlId;
    }

    /// <summary>
    /// Runs <paramref name="body"/> as one call: logs its entry, gives it a fresh
    /// <see cref="AttemptContext"/> with a <paramref name="budgetMs"/> deadline, and logs its exit.
    /// </summary>
    /// <param name="caller">The public member's name, for the log.</param>
    /// <param name="value">The value the member was given, for the log.</param>
    /// <param name="budgetMs">The call's budget: the caller's timeout, or the default wait.</param>
    /// <param name="animationMs">The scroll throttle interval.</param>
    /// <param name="body">The call. Its first phase polls with the context it is given.</param>
    /// <param name="succeeded">For a call that answers false rather than throwing (a <c>Wait*</c>): whether the answer counts as success in the log.</param>
    public T Run<T>(
        string caller,
        string? value,
        int budgetMs,
        int animationMs,
        Func<AttemptContext, T> body,
        Func<T, bool>? succeeded = null)
    {
        var clock = Stopwatch.StartNew();
        var startMs = Timing.NowMs;
        var context = new AttemptContext(Deadline.In(budgetMs), animationMs);
        _logger?.LogEntry(_testName, _pageName, _controlId, caller, value);

        var outer = AttemptContext.Current;
        AttemptContext.Current = context;
        T result;
        try
        {
            result = body(context);
        }
        catch (Exception error)
        {
            _logger?.LogExit(_testName, _pageName, _controlId, caller,
                LogResult.Error, (int)clock.ElapsedMilliseconds, error.Message);
            Timing.Call(_pageName, _controlId, caller, startMs, (int)clock.ElapsedMilliseconds, context.Log.Attempts, "error");
            throw;
        }
        finally
        {
            AttemptContext.Current = outer;
        }

        var elapsed = (int)clock.ElapsedMilliseconds;
        var answered = succeeded == null || succeeded(result);
        Timing.Call(_pageName, _controlId, caller, startMs, elapsed, context.Log.Attempts, answered ? "success" : "error");
        if (!answered)
        {
            _logger?.LogExit(_testName, _pageName, _controlId, caller,
                LogResult.Error, elapsed, context.Log.Summary());
        }
        else if (IsNearMiss(context, elapsed))
        {
            _logger?.LogExit(_testName, _pageName, _controlId, caller,
                LogResult.Warning, elapsed, $"near-miss: {context.Log.Summary()} (budget {budgetMs} ms)");
            NearMisses.Add(new NearMisses.NearMiss(_testName, _pageName, _controlId, caller, elapsed, budgetMs, context.Log.Summary()));
        }
        else
        {
            _logger?.LogExit(_testName, _pageName, _controlId, caller, LogResult.Success, elapsed);
        }

        return result;
    }

    private bool IsNearMiss(AttemptContext context, int elapsedMs)
    {
        if (context.Log.Replacements >= _nearMiss.Replacements)
        {
            return true;
        }

        var budget = context.Deadline.BudgetMs;
        if (budget <= 0)
        {
            return false;
        }

        // Several attempts: judged on the poll alone, since an action's confirmation has a budget
        // of its own (R3). One attempt: on the call, since the time went into that attempt's own
        // work, as a Playwright action that waits for its element or its navigation does.
        return context.Log.Attempts > 1
            ? context.Log.LastAtMs > budget * _nearMiss.BudgetShare
            : elapsedMs > budget * _nearMiss.BudgetShare;
    }
}
