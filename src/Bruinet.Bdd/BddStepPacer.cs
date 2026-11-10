namespace Bruinet.Bdd;

/// <summary>
/// Sets the pace of a run: waited on before each step and after it. A narrated recording waits
/// until the step's spoken line has played, then a beat for its result to be seen. Null on the
/// context unless something paces the run, so a test run is never slowed.
/// </summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public interface IBddStepPacer
{
    /// <summary>Before a step runs, after the break hook.</summary>
    /// <param name="invocation">The step about to run.</param>
    /// <param name="context">The scenario's context.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    Task BeforeAsync(BddStepInvocation invocation, BddExecutionContext context, CancellationToken cancellationToken);

    /// <summary>After a step ran, whatever its result.</summary>
    /// <param name="result">The step's result.</param>
    /// <param name="context">The scenario's context.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    Task AfterAsync(BddStepResult result, BddExecutionContext context, CancellationToken cancellationToken);
}

/// <summary>Where a step is written in the scenario.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public static class BddWrittenStep
{
    /// <summary>
    /// The line of the step as the scenario writes it: its own, or for a step of a callable phrase,
    /// the line of the call (the outermost one) that put it there.
    /// </summary>
    /// <param name="invocation">A step of the run.</param>
    public static int Line(BddStepInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var frame = invocation.Call;
        while (frame?.Parent is { } parent)
        {
            frame = parent;
        }

        return frame?.Location.LineNumber ?? invocation.Step.Source.LineNumber;
    }
}
