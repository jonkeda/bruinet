namespace Bruinet.Spec;

/// <summary>
/// One thing a spec breaks of the format (<c>docs/spec-format.md</c>), at the line it is on.
/// </summary>
/// <param name="Line">The 1-based line, or 0 when it is about the whole spec.</param>
/// <param name="Code">A short stable name for the kind of finding, such as <c>example-missing</c>.</param>
/// <param name="Message">What is wrong, in the words of the format.</param>
public sealed record SpecFinding(int Line, string Code, string Message)
{
    /// <inheritdoc />
    public override string ToString() => Line > 0 ? $"{Line}: {Message} ({Code})" : $"{Message} ({Code})";
}
