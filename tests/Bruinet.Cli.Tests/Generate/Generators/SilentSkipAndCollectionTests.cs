using Bruinet.Generator.Models;

namespace Bruinet.Generator.Tests.Generators;

/// <summary>
/// Near-miss Core methods fail generation instead of
/// vanishing, <c>[SkipGeneration]</c> declares a deliberate exclusion, and collection-valued
/// getters generate sequence-aware comparisons.
/// </summary>
/// <remarks>
/// The motivating case is <c>SelectorControlBase</c>, where two Core methods were dropped
/// silently because they were
/// <c>protected</c> without <c>virtual</c> — invisible API loss that no test could catch,
/// because nothing reported it.
/// </remarks>
public class SilentSkipAndCollectionTests
{
    private static string GenerateAll(string code)
        => ControlObjectGenerator.CreateDefault()
            .Generate(code, new GeneratorOptions { IncludeGeneratedHeader = false });

    private static string Wrap(string members) => $$"""
        namespace Test;
        public partial class Probe<TScope> where TScope : IMauiScope<TScope>
        {
            {{members}}
        }
        """;

    #region Near-miss Core methods are reported

    [Theory]
    [InlineData("protected string? GetNameCore(IMauiElement element) => null;", "virtual")]
    [InlineData("private virtual string? GetNameCore(IMauiElement element) => null;", "protected")]
    public void NearMissCoreMethod_FailsGeneration_NamingTheMethod(string member, string expectedHint)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => GenerateAll(Wrap(member)));

        Assert.Contains("GetNameCore", ex.Message);
        Assert.Contains(expectedHint, ex.Message);
        Assert.Contains("SkipGeneration", ex.Message);
    }

    /// <summary>
    /// A well-formed Core method is unaffected by the new validation.
    /// </summary>
    [Fact]
    public void WellFormedCoreMethod_StillGenerates()
    {
        var code = GenerateAll(Wrap("protected virtual string? GetNameCore(IMauiElement element) => null;"));

        Assert.Contains("public string? GetName(", code);
    }

    /// <summary>
    /// Guards are internal checks, not API, so they are exempt from the validation.
    /// </summary>
    [Fact]
    public void EnsureGuard_IsNotReportedAsNearMiss()
    {
        var code = GenerateAll(Wrap("protected void EnsureClickableCore(IMauiElement element) { }"));

        Assert.DoesNotContain("EnsureClickable(", code);
    }

    /// <summary>
    /// A method ending in Core that takes no element is a private helper, not a candidate.
    /// </summary>
    [Fact]
    public void CoreMethodWithoutElementParameter_IsNotReportedAsNearMiss()
    {
        var code = GenerateAll(Wrap("private bool ComputeCore(int value) => true;"));

        Assert.DoesNotContain("Compute(", code);
    }

    #endregion

    #region [SkipGeneration] declares intent

    [Fact]
    public void SkipGeneration_SuppressesTheMember_AndSilencesTheValidation()
    {
        var code = GenerateAll(Wrap(
            """
            [SkipGeneration("Would leak the platform element type.")]
                protected virtual IReadOnlyList<IMauiElement>? GetItemElementsCore(IMauiElement? element) => null;
            """));

        Assert.DoesNotContain("GetItemElements(", code);
    }

    /// <summary>
    /// The opted-out method keeps <c>virtual</c>, so derived controls can still override it —
    /// which dropping the keyword to hide it from the generator prevented.
    /// </summary>
    [Fact]
    public void SkipGeneration_LeavesOtherMembersGenerating()
    {
        var code = GenerateAll(Wrap(
            """
            [SkipGeneration("Deliberate.")]
                protected virtual IReadOnlyList<IMauiElement>? GetItemElementsCore(IMauiElement? element) => null;
                protected virtual string? GetNameCore(IMauiElement element) => null;
            """));

        Assert.DoesNotContain("GetItemElements(", code);
        Assert.Contains("public string? GetName(", code);
    }

    #endregion

    #region Collection comparisons

    private const string ItemTextsCore = """
        [GenerateComparisons(Comparison.SequenceEquals | Comparison.HasItem | Comparison.Count)]
            protected virtual IReadOnlyList<string>? GetItemTextsCore(IMauiElement? element) => null;
        """;

    [Fact]
    public void SequenceEquals_ComparesElementWise_NotByReference()
    {
        var code = GenerateAll(Wrap(ItemTextsCore));

        Assert.Contains("SequenceEqual(expected", code);
        // The reference-comparing default must not also be emitted: two members would share
        // the name AssertItemTexts and the file would not compile.
        Assert.DoesNotContain("GetItemTextsCore(element) == expected", code);
    }

    [Fact]
    public void HasItem_TakesAnItem_NotTheCollection()
    {
        var code = GenerateAll(Wrap(ItemTextsCore));

        Assert.Contains("AssertItemTextsHasItem(string item", code);
        Assert.Contains("WaitItemTextsHasItem(string item", code);
    }

    [Fact]
    public void Count_TakesAnInt()
    {
        var code = GenerateAll(Wrap(ItemTextsCore));

        Assert.Contains("AssertItemTextsCount(int? expected", code);
    }

    /// <summary>
    /// A string getter keeps substring <c>Contains</c>; only collections get <c>HasItem</c>.
    /// </summary>
    /// <remarks>
    /// The two mean different things, which is why they are separate variants rather than one
    /// name that changes meaning with the return type.
    /// </remarks>
    [Fact]
    public void Contains_RemainsSubstring_ForStringGetters()
    {
        var code = GenerateAll(Wrap(
            """
            [GenerateComparisons(Comparison.EqualTo | Comparison.Contains)]
                protected virtual string? GetTextCore(IMauiElement element) => null;
            """));

        Assert.Contains("AssertTextContains(string? expected", code);
        Assert.DoesNotContain("HasItem", code);
    }

    #endregion
}
