using System.Text;

namespace Bruinet.Bdd;

/// <summary>
/// A scenario's stable address: <c>&lt;file relative to the config&gt;#&lt;slug&gt;</c>, such as
/// <c>Features/TOD.11-manage-users.feature.md#TOD.11.1-Users-lists-available-users-first</c>. The
/// test case's <c>BddScenario</c> trait, so <c>dotnet test --filter "BddScenario=..."</c> runs one
/// scenario; an IDE computes the same address from a line.
/// </summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public static class BddScenarioAddress
{
    /// <summary>The trait that holds the address.</summary>
    public const string Trait = "BddScenario";

    /// <summary>The address of a scenario.</summary>
    /// <param name="fileRelativeToConfig">The feature file, relative to the config's folder.</param>
    /// <param name="scenarioName">The scenario's name; an outline row's has its <c>[n]</c>.</param>
    public static string Of(string fileRelativeToConfig, string scenarioName)
    {
        ArgumentNullException.ThrowIfNull(fileRelativeToConfig);
        ArgumentNullException.ThrowIfNull(scenarioName);
        return $"{fileRelativeToConfig.Replace('\\', '/')}#{Slug(scenarioName)}";
    }

    /// <summary>
    /// A name in the characters a <c>dotnet test</c> filter needs no escaping for
    /// (<c>[A-Za-z0-9._/-]</c>): every other run of characters is one <c>-</c>.
    /// </summary>
    /// <param name="scenarioName">The scenario's name.</param>
    public static string Slug(string scenarioName)
    {
        ArgumentNullException.ThrowIfNull(scenarioName);
        var builder = new StringBuilder(scenarioName.Length);
        foreach (var character in scenarioName)
        {
            if (char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '/' or '-')
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().TrimEnd('-');
    }
}
