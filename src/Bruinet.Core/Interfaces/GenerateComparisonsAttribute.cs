namespace Bruinet.Core.Interfaces;

/// <summary>
/// Comparison variants a generated Assert/Wait family can emit for a
/// <c>Get*Core</c> query.
/// </summary>
[Flags]
public enum Comparison
{
    /// <summary>Exact equality — <c>AssertText</c>, <c>WaitText</c>.</summary>
    EqualTo = 1,

    /// <summary>Substring containment — <c>AssertTextContains</c>, <c>WaitTextContains</c>.</summary>
    Contains = 2,

    /// <summary>Prefix match — <c>AssertTextStartsWith</c>.</summary>
    StartsWith = 4,

    /// <summary>Suffix match — <c>AssertTextEndsWith</c>.</summary>
    EndsWith = 8,

    /// <summary>Empty / non-empty check — <c>AssertTextEmpty</c>.</summary>
    Empty = 16,

    /// <summary>
    /// Element-wise sequence equality, order significant — <c>AssertItemTexts</c>.
    /// </summary>
    /// <remarks>
    /// For a <c>Get*Core</c> returning a collection. <see cref="EqualTo"/> would compare such a
    /// return value by reference, which no caller can satisfy; this compares contents.
    /// </remarks>
    SequenceEquals = 32,

    /// <summary>
    /// Membership in a returned collection — <c>AssertItemTextsHasItem("Blue")</c>.
    /// </summary>
    /// <remarks>
    /// Deliberately not <see cref="Contains"/>, which means substring for a string-valued
    /// getter. Overloading one name across both meanings would make the generated API
    /// ambiguous at the call site.
    /// </remarks>
    HasItem = 64,

    /// <summary>
    /// Cardinality of a returned collection — <c>AssertItemTextsCount(3)</c>.
    /// </summary>
    Count = 128,

    /// <summary>
    /// Strictly greater — <c>WaitValueGreaterThan(50)</c>. For values with a <c>&gt;</c>
    /// operator, such as numbers and dates; a null actual never passes.
    /// </summary>
    /// <remarks>
    /// For a value that only ever moves past a mark - playback progress, a count that grows -
    /// and so never equals the mark at the moment it is read.
    /// </remarks>
    GreaterThan = 256,

    /// <summary>Greater or equal — <c>WaitValueAtLeast(50)</c>. See <see cref="GreaterThan"/>.</summary>
    AtLeast = 512,

    /// <summary>Strictly less — <c>WaitValueLessThan(50)</c>. See <see cref="GreaterThan"/>.</summary>
    LessThan = 1024,

    /// <summary>Less or equal — <c>WaitValueAtMost(50)</c>. See <see cref="GreaterThan"/>.</summary>
    AtMost = 2048
}

/// <summary>
/// Declares which comparison variants Bruinet.Generator should emit for a
/// <c>Get*Core</c> method. Without this attribute only <see cref="Comparison.EqualTo"/>
/// is generated.
/// </summary>
/// <example>
/// <code>
/// [GenerateComparisons(Comparison.EqualTo | Comparison.Contains)]
/// protected virtual string? GetTextCore(IMauiElement element) => element.Text;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method)]
public sealed class GenerateComparisonsAttribute : Attribute
{
    /// <summary>
    /// Creates the attribute with the comparison variants to generate.
    /// </summary>
    /// <param name="comparisons">The variants to emit.</param>
    public GenerateComparisonsAttribute(Comparison comparisons)
    {
        Comparisons = comparisons;
    }

    /// <summary>
    /// Gets the comparison variants to generate.
    /// </summary>
    public Comparison Comparisons { get; }
}
