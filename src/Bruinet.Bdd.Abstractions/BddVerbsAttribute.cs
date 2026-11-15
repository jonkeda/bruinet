namespace Bruinet.Bdd;

/// <summary>
/// A static class of extension verbs: each public static method with a <c>[BddWhen]</c> or <c>[BddThen]</c> is a control
/// verb of the type its first (<c>this</c>) parameter takes, as if the control declared it. So a verb is written once for
/// an interface or a base class (every table), or for a control a suite does not own. The method is also an ordinary
/// C# extension method.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class BddVerbsAttribute : Attribute
{
}
