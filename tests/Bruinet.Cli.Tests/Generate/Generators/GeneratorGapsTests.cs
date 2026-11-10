using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Bruinet.Generator.Analysis;
using Bruinet.Generator.Generators;
using Bruinet.Generator.Models;

namespace Bruinet.Generator.Tests.Generators;

/// <summary>
/// Guard exclusion, override skipping, assertion messages, comparison variants and name
/// collisions.
/// </summary>
public class GeneratorGapsTests
{
    private static MethodDeclarationSyntax FirstMethod(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = (CompilationUnitSyntax)tree.GetRoot();
        var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        return classDecl.Members.OfType<MethodDeclarationSyntax>().First();
    }

    private static ControlObjectContext ContextFor(string code)
    {
        var analyzer = new ControlObjectAnalyzer();
        var (classDecl, root) = analyzer.FindTarget(code, null);
        return analyzer.BuildContext(classDecl!, root);
    }

    private static string GenerateAll(string code)
    {
        return ControlObjectGenerator.CreateDefault()
            .Generate(code, new GeneratorOptions { IncludeGeneratedHeader = false });
    }

    // --- Issue 4: Ensure* guards are not actions -------------------------------

    [Theory]
    [InlineData("EnsureClickableCore")]
    [InlineData("EnsureEnabledCore")]
    [InlineData("EnsureVisibleCore")]
    public void ActionGenerator_ExcludesEnsureGuards(string methodName)
    {
        var code = $@"
public class Test {{
    protected virtual void {methodName}(IMauiElement element) {{ }}
}}";
        Assert.False(new ActionGenerator().Matches(FirstMethod(code)),
            $"{methodName} is a guard and must not generate a public wrapper");
    }

    [Fact]
    public void Generate_GuardStaysVirtualAndProducesNoPublicMember()
    {
        var code = @"namespace T;
public abstract class Guarded<TScope> where TScope : IScope<TScope>
{
    protected virtual void EnsureClickableCore(IMauiElement element) { }
    protected virtual void ClickCore(IMauiElement element, int? timeoutMs = null) { }
}";
        var generated = GenerateAll(code);

        Assert.DoesNotContain("EnsureClickable(", generated);
        Assert.Contains("public TScope Click(", generated);
    }

    // --- Issue 5: overrides don't double-generate -------------------------------

    [Fact]
    public void ActionGenerator_ExcludesOverride()
    {
        var code = @"
public class Test {
    protected override void ClickCore(IMauiElement element, int? timeoutMs = null) { }
}";
        Assert.False(new ActionGenerator().Matches(FirstMethod(code)),
            "override already has a wrapper on the base class");
    }

    [Fact]
    public void IsWaitAssertGenerator_ExcludesOverride()
    {
        var code = @"
public class Test {
    protected override string? GetTextCore(IMauiElement element) => null;
}";
        Assert.False(new IsWaitAssertGenerator().Matches(FirstMethod(code)),
            "override already has the trio on the base class");
    }

    // --- Issue 3: assertion messages --------------------------------------------

    [Fact]
    public void Generate_StateAssert_IncludesSynthesizedMessage()
    {
        var code = @"namespace T;
public abstract class S<TScope> where TScope : IScope<TScope>
{
    protected virtual bool? IsClickableCore(IMauiElement? element) => true;
}";
        var generated = GenerateAll(code);

        Assert.Contains("message ??", generated);
        Assert.Contains("Expected Clickable to be", generated);
        Assert.Contains("Locator:", generated);
    }

    [Fact]
    public void Generate_GetterAssert_IncludesSynthesizedMessage()
    {
        var code = @"namespace T;
public abstract class S<TScope> where TScope : IScope<TScope>
{
    protected virtual string? GetTitleCore(IMauiElement element) => null;
}";
        var generated = GenerateAll(code);

        Assert.Contains("message ??", generated);
        Assert.Contains("Expected Title to be", generated);
    }

    // --- Issue 2: comparison variants -------------------------------------------

    [Fact]
    public void Generate_WithoutAttribute_EmitsEqualityOnly()
    {
        var code = @"namespace T;
public abstract class S<TScope> where TScope : IScope<TScope>
{
    protected virtual string? GetTextCore(IMauiElement element) => null;
}";
        var generated = GenerateAll(code);

        Assert.Contains("public TScope AssertText(", generated);
        Assert.DoesNotContain("AssertTextContains", generated);
        Assert.DoesNotContain("AssertTextStartsWith", generated);
    }

    [Fact]
    public void Generate_WithComparisons_EmitsRequestedVariants()
    {
        var code = @"namespace T;
public abstract class S<TScope> where TScope : IScope<TScope>
{
    [GenerateComparisons(Comparison.EqualTo | Comparison.Contains | Comparison.StartsWith)]
    protected virtual string? GetTextCore(IMauiElement element) => null;
}";
        var generated = GenerateAll(code);

        Assert.Contains("public TScope AssertText(", generated);
        Assert.Contains("public TScope AssertTextContains(", generated);
        Assert.Contains("public bool WaitTextContains(", generated);
        Assert.Contains("public TScope AssertTextStartsWith(", generated);
        // Not requested.
        Assert.DoesNotContain("AssertTextEndsWith", generated);
    }

    [Fact]
    public void Generate_EmptyComparison_UsesBoolPredicate()
    {
        var code = @"namespace T;
public abstract class S<TScope> where TScope : IScope<TScope>
{
    [GenerateComparisons(Comparison.EqualTo | Comparison.Empty)]
    protected virtual string? GetTextCore(IMauiElement element) => null;
}";
        var generated = GenerateAll(code);

        Assert.Contains("public TScope AssertTextEmpty(bool? expected = true", generated);
        Assert.Contains("string.IsNullOrEmpty(GetTextCore(element))", generated);
    }

    // --- Issue 7: name collisions ------------------------------------------------

    [Fact]
    public void Generate_CollidingGeneratedNames_Throws()
    {
        var code = @"namespace T;
public abstract class S<TScope> where TScope : IScope<TScope>
{
    protected virtual string? GetValueCore(IMauiElement element) => null;
    protected virtual string? GetValueCore(IMauiElement element, string name) => null;
}";

        var ex = Assert.Throws<InvalidOperationException>(() => GenerateAll(code));

        Assert.Contains("Value", ex.Message);
        Assert.Contains("GetValueCore", ex.Message);
    }

    /// <summary>
    /// An action and a state query about the same thing do not collide: the emitted names
    /// are what can clash, not the stem they share.
    /// </summary>
    [Fact]
    public void Generate_ActionAndStateWithTheSameStem_DoesNotThrow()
    {
        var code = @"namespace T;
public abstract class S<TScope> where TScope : IScope<TScope>
{
    protected virtual void OpenCore(IMauiElement element, int? timeoutMs = null) { }
    protected virtual bool? IsOpenCore(IMauiElement? element) => null;
}";
        var generated = GenerateAll(code);

        Assert.Contains("Open(int? timeoutMs = null)", generated);
        Assert.Contains("IsOpen()", generated);
        Assert.Contains("WaitOpen(", generated);
        Assert.Contains("AssertOpen(", generated);
    }

    /// <summary>
    /// A getter and a state query <em>do</em> collide: both emit Wait and Assert on the stem.
    /// </summary>
    [Fact]
    public void Generate_GetterAndStateWithTheSameStem_Throws()
    {
        var code = @"namespace T;
public abstract class S<TScope> where TScope : IScope<TScope>
{
    protected virtual string? GetOpenCore(IMauiElement element) => null;
    protected virtual bool? IsOpenCore(IMauiElement? element) => null;
}";

        var ex = Assert.Throws<InvalidOperationException>(() => GenerateAll(code));

        Assert.Contains("WaitOpen", ex.Message);
    }

    [Fact]
    public void Generate_DistinctNames_DoesNotThrow()
    {
        var code = @"namespace T;
public abstract class S<TScope> where TScope : IScope<TScope>
{
    protected virtual string? GetValueCore(IMauiElement element) => null;
    protected virtual string? GetLabelCore(IMauiElement element) => null;
}";
        var generated = GenerateAll(code);

        Assert.Contains("GetValue(", generated);
        Assert.Contains("GetLabel(", generated);
    }
}
