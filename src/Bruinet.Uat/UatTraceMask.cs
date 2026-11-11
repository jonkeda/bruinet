using System.Text.RegularExpressions;

namespace Bruinet.Uat;

/// <summary>
/// What a trace never writes (review 09, D4): a trace is on by default, an agent reads it and a CI job may upload it,
/// so the value of a password field, of a control the suite names in <c>TraceMask</c>, and the quoted values of a
/// step that names a secret, are written <c>***</c>.
/// </summary>
/// <remarks>
/// No stack answers "is this a password field" the same way, so the trace goes by name: a control whose page-object
/// member or type has the word Password, Passcode, Pin, Secret, Token or Api Key in it (a WPF <c>PasswordBox</c>, a
/// <c>PinEntry</c>; not a <c>Spinner</c>), and the names a suite lists. A password field named otherwise is listed in
/// <c>TraceMask</c>.
/// </remarks>
internal static partial class UatTraceMask
{
    public const string Masked = "***";

    private static readonly HashSet<string> SecretWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passwd", "passcode", "pin", "secret", "token", "apikey",
    };

    [GeneratedRegex(@"\b(passwords?|passwd|passcode|pin|secret|token|api ?key)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretInText();

    [GeneratedRegex("[A-Z]?[a-z]+|[A-Z]+(?![a-z])|[0-9]+")]
    private static partial Regex Words();

    [GeneratedRegex("\"[^\"]*\"|'[^']*'")]
    private static partial Regex Quoted();

    /// <summary>Whether a control's value is written <c>***</c>.</summary>
    /// <param name="name">The control's member name on its page object.</param>
    /// <param name="type">The control's type.</param>
    /// <param name="listed">The suite's <c>TraceMask</c> names.</param>
    public static bool Hides(string name, Type type, IReadOnlyCollection<string> listed)
        => NamesASecret(name) || NamesASecret(type.Name.Split('`')[0]) || listed.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>A step's text as a trace keeps it: its quoted values <c>***</c> when the step names a secret.</summary>
    /// <param name="text">The step's text.</param>
    public static string Step(string text)
        => SecretInText().IsMatch(text) ? Quoted().Replace(text, "\"" + Masked + "\"") : text;

    // By its words: PasswordBox, UserPin and ApiKeyField name one; Spinner and Shipping do not.
    private static bool NamesASecret(string name)
    {
        var words = Words().Matches(name).Select(match => match.Value).ToList();
        for (var i = 0; i < words.Count; i++)
        {
            if (SecretWords.Contains(words[i]) || (i + 1 < words.Count && SecretWords.Contains(words[i] + words[i + 1])))
            {
                return true;
            }
        }

        return false;
    }
}
