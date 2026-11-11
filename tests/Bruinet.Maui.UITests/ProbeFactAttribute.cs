namespace Bruinet.Maui.UITests;

/// <summary>
/// A probe: it measures or reports, and asserts little or nothing, so it must not count as a passing test (review 08,
/// finding 10). Skipped unless <c>BRUINET_PROBE=1</c>.
/// </summary>
public sealed class ProbeFactAttribute : FactAttribute
{
    /// <param name="what">What the probe reports, shown in the skip.</param>
    public ProbeFactAttribute(string what = "a measurement for the stale-readiness plan")
    {
        if (Environment.GetEnvironmentVariable("BRUINET_PROBE") != "1")
        {
            Skip = $"A probe ({what}). Set BRUINET_PROBE=1 to run it.";
        }
    }
}
