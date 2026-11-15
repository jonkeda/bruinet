using System.Reflection;

namespace Bruinet.Bdd;

// CommandId, Literal and HasLiteral belong to control verbs; on a phrase method they break the phrase (AD-011, AD-016).
internal static class BddPhraseMethods
{
    public static string? ControlVerbOnlyError(MethodInfo method, BddStepAttribute phrase)
    {
        string? property = phrase.CommandId is not null ? nameof(BddStepAttribute.CommandId)
            : phrase.HasLiteral ? nameof(BddStepAttribute.HasLiteral)
            : phrase.Literal ? nameof(BddStepAttribute.Literal)
            : null;
        return property is null
            ? null
            : $"Phrase '{phrase.Phrase}' on {method.DeclaringType?.FullName}.{method.Name} sets " +
              $"{property}, which only a control verb may set.";
    }
}
