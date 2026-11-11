using System.Globalization;
using System.Text.RegularExpressions;
using Bruinet.Generator.Models;

namespace Bruinet.Generator.Tests.Generators;

/// <summary>
/// Extra parameters on <c>Is*Core</c>, and <c>[AbsenceTolerant]</c> on <c>Get*Core</c>.
/// </summary>
public class QueryParameterAndAbsenceTests
{
    private const string Control = @"namespace T;
public abstract class S<TScope> where TScope : IScope<TScope>
{{
{0}
}}";

    private static string Generate(string members)
        => Regex.Replace(
            ControlObjectGenerator.CreateDefault().Generate(
                string.Format(CultureInfo.InvariantCulture, Control, members), new GeneratorOptions { IncludeGeneratedHeader = false }),
            @"\s+", " ");

    [Fact]
    public void StateQuery_WithAnExtraParameter_PutsItFirstInEveryMember()
    {
        var generated = Generate(
            "protected virtual bool? IsShowingCore(IMauiElement? element, string viewAutomationId) => null;");

        Assert.Contains("public bool IsShowing(string viewAutomationId) { return IsShowingCore(TryFindElement(), viewAutomationId) == true; }", generated);
        Assert.Contains("public bool WaitShowing(string viewAutomationId, bool? expected = true, int? timeoutMs = null)", generated);
        Assert.Contains("element => IsShowingCore(element, viewAutomationId) == expected!.Value,", generated);
        Assert.Contains("public TScope AssertShowing(string viewAutomationId, bool? expected = true, string? message = null, int? timeoutMs = null)", generated);
        Assert.Contains("element => IsShowingCore(element, viewAutomationId), (actual, expected1) => (actual == expected1),", generated);
    }

    [Fact]
    public void StateQuery_WithOnlyTheElement_IsGeneratedAsBefore()
    {
        var generated = Generate("protected virtual bool? IsOpenCore(IMauiElement? element) => null;");

        Assert.Contains("public bool IsOpen() { return IsOpenCore(TryFindElement()) == true; }", generated);
        Assert.Contains("public bool WaitOpen(bool? expected = true, int? timeoutMs = null)", generated);
        // The method group, not a lambda: existing output does not move.
        Assert.Contains("RunAssertWithElement(expected, IsOpenCore, (actual, expected1)", generated);
    }

    [Fact]
    public void AbsenceTolerantGetter_ReadsOnceAndResolvesOptionally()
    {
        var generated = Generate(
            "[AbsenceTolerant]\n" +
            "[GenerateComparisons(Comparison.EqualTo | Comparison.Contains | Comparison.Empty)]\n" +
            "protected virtual string? GetInitialsCore(IMauiElement? element) => null;");

        Assert.Contains("return RunGetWithOptionalElement(element => GetInitialsCore(element), timeoutMs);", generated);
        Assert.Contains("return RunWaitWithOptionalElement(expected, element => GetInitialsCore(element) == expected,", generated);
        Assert.Contains("return RunAssertWithOptionalElement(expected, element => GetInitialsCore(element),", generated);
        Assert.Contains("public bool WaitInitialsContains(", generated);
        Assert.Contains("public bool WaitInitialsEmpty(", generated);
        Assert.DoesNotContain("RunGetWithElement", generated);
        Assert.DoesNotContain("RunWaitWithElement", generated);
        Assert.DoesNotContain("RunAssertWithElement", generated);
    }

    [Fact]
    public void GetterWithoutTheAttribute_StillWaitsForItsElement()
    {
        var generated = Generate("protected virtual string? GetTextCore(IMauiElement? element) => null;");

        Assert.Contains("return RunGetWithElement(element => GetTextCore(element), timeoutMs);", generated);
        Assert.DoesNotContain("Optional", generated);
    }
}
