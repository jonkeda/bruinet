namespace Bruinet.Bdd;

/// <summary>
/// The Gherkin keyword a UAT step resolves to after And/But are folded into the
/// preceding Given/When/Then.
/// </summary>
public enum BddEffectiveStepKeyword
{
    Given,
    When,
    Then
}
