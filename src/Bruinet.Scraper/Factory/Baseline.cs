using Bruinet.Scraper.Testing;

namespace Bruinet.Scraper.Factory;

/// <summary>
/// What passed before a change and must pass after it: with a person's code no longer protected, this is what
/// guards it. A change that breaks a test that passed is put back, whoever wrote what it changed.
/// </summary>
public static class Baseline
{
    /// <summary>The tests that passed before and do not pass after: failed, or no longer there to run.</summary>
    /// <param name="before">The tests as they ran before the change.</param>
    /// <param name="after">The same tests as they ran after it.</param>
    public static IReadOnlyList<TestOutcome> Broken(IReadOnlyList<TestOutcome> before, IReadOnlyList<TestOutcome> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var now = after.GroupBy(test => test.Name, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        return [.. before.Where(test => test.Passed)
            .Select(test => now.TryGetValue(test.Name, out var again) ? again : test with { Passed = false, Message = "it did not run after the change: it no longer builds, or is gone" })
            .Where(test => !test.Passed)];
    }
}
