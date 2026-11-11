namespace Bruinet.Bdd;

[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public static class BddDiagnosticsFormatter
{
    public static string FormatDiagnostics(IEnumerable<BddDiagnostic> diagnostics)
    {
        return string.Join(
            Environment.NewLine,
            diagnostics.SelectMany(x => x.Within.Select(inner => $"    via {inner}").Prepend($"{x.Location}: {x.Code} {x.Message}")));
    }

    /// <summary>
    /// Step results, one per line. The steps of a callable phrase are indented under their call,
    /// and a per-row callable's under <c>row n (values)</c>, so a failure points at the real step.
    /// </summary>
    public static IEnumerable<string> FormatSteps(IEnumerable<BddStepResult> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        IReadOnlyList<BddCallFrame> open = [];
        foreach (var step in steps)
        {
            var chain = step.Invocation.Call?.Chain ?? [];
            var common = 0;
            while (common < open.Count && common < chain.Count && ReferenceEquals(open[common], chain[common]))
            {
                common++;
            }

            for (var i = common; i < chain.Count; i++)
            {
                var frame = chain[i];
                var indent = Indent(chain, i);
                var sameCall = i < open.Count && ReferenceEquals(open[i].Call, frame.Call);
                if (!sameCall)
                {
                    yield return $"{indent}Call: {frame.Call.Source}: {frame.Call.Keyword} {frame.Call.Text}";
                }

                if (frame.Row is not null)
                {
                    yield return $"{indent}  {frame.Describe()}";
                }
            }

            open = chain;
            var invocation = step.Invocation;
            var scope = invocation.Scope.Length > 0 ? $"[{invocation.Scope}] " : string.Empty;
            var stepIndent = Indent(chain, chain.Count);
            yield return $"{stepIndent}{step.Status}: {invocation.Step.Source}: {scope}{invocation.CommandId}: {invocation.Step.Text}";
            if (!string.IsNullOrWhiteSpace(step.Message))
            {
                // The message on lines of its own, under its step: run together with the step it was hard to find (review 01).
                foreach (var line in step.Message.Split('\n'))
                {
                    yield return $"{stepIndent}    {line.TrimEnd('\r')}";
                }
            }
        }
    }

    private static string Indent(IReadOnlyList<BddCallFrame> chain, int level)
        => new(' ', 2 * chain.Take(level).Sum(frame => frame.Row is null ? 1 : 2));

    public static string FormatBindFailure(
        IEnumerable<BddDiagnostic> diagnostics,
        string discoveryReport,
        BddCommandCatalog catalog)
    {
        return string.Join(
            Environment.NewLine,
            [
                FormatDiagnostics(diagnostics),
                discoveryReport,
                FormatCatalog(catalog)
            ]);
    }

    public static string FormatResults(
        BddScenarioRunResult result,
        BddExecutionContext context,
        string discoveryReport,
        BddCommandCatalog catalog,
        string? evidencePath = null)
    {
        List<string> lines =
        [
            .. FormatSteps(result.Steps),
            "Runtime trace:"
        ];

        if (result.Skipped && result.SkipDecision?.Reason is not null)
        {
            lines.Add($"Skipped: {result.SkipDecision.Reason}");
        }

        if (!string.IsNullOrWhiteSpace(evidencePath))
        {
            lines.Add($"Evidence: {evidencePath}");
        }

        // Not the pages and the phrase catalog: they help a scenario that did not bind (FormatBindFailure), not a step
        // that ran and failed, where they buried the one line that mattered (review 01, finding 3).
        lines.AddRange(context.Diagnostics);
        return string.Join(Environment.NewLine, lines);
    }

    public static string FormatCatalog(BddCommandCatalog catalog)
    {
        return "Command catalog:" + Environment.NewLine + string.Join(
            Environment.NewLine,
            catalog.Patterns
                .OrderBy(pattern => pattern.Keyword)
                .ThenBy(pattern => pattern.Phrase, StringComparer.Ordinal)
                .Select(pattern => $"- {pattern.Keyword}: {pattern.Phrase} -> {pattern.CommandId}"
                    + (pattern.Namespace.Length > 0 ? $" in {pattern.Namespace}" : string.Empty)
                    + (pattern.Page is { } page ? $" on page {page}" : string.Empty)));
    }
}
