namespace Bruinet.Uia.Tests;

/// <summary>
/// A probe: it reports, and asserts little, so it must not count as a passing test (review 08, finding 10). Skipped
/// unless <c>BRUINET_PROBE=1</c>.
/// </summary>
public sealed class ProbeFactAttribute : FactAttribute
{
    /// <param name="what">What the probe reports, shown in the skip.</param>
    public ProbeFactAttribute(string what)
    {
        if (Environment.GetEnvironmentVariable("BRUINET_PROBE") != "1")
        {
            Skip = $"A probe ({what}). Set BRUINET_PROBE=1 to run it.";
        }
    }
}
