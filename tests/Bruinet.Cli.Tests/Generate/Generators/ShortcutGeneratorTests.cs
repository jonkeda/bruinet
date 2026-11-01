using System.Globalization;
using System.Text.RegularExpressions;
using Bruinet.Generator.Models;

namespace Bruinet.Generator.Tests.Generators;

/// <summary>
/// Shortcuts: a component's one-line forwards to its parts, and the ordered comparisons they
/// forward.
/// </summary>
public class ShortcutGeneratorTests
{
    private const string Component = @"namespace T;
public partial class Player<TScope> : ComponentObjectBase<TScope, Player<TScope>>
    where TScope : IMauiScope<TScope>
{{
{0}
}}";

    private static string Generate(string members)
        => ControlObjectGenerator.CreateDefault()
            .Generate(string.Format(CultureInfo.InvariantCulture, Component, members), new GeneratorOptions { IncludeGeneratedHeader = false });

    /// <summary>Collapses whitespace so assertions do not depend on the formatter's line breaks.</summary>
    private static string Flat(string code) => Regex.Replace(code, @"\s+", " ");

    [Fact]
    public void StateShortcut_ForwardsTheTrioToThePart()
    {
        var generated = Flat(Generate(
            "protected bool? IsPlayingShortcut() => PlayPauseButton.IsPlaying();"));

        Assert.Contains("public bool? IsPlaying() => PlayPauseButton.IsPlaying();", generated);
        Assert.Contains(
            "public bool WaitPlaying(bool? expected = true, int? timeoutMs = null) => PlayPauseButton.WaitPlaying(expected, timeoutMs);",
            generated);
        Assert.Contains(
            "public Player<TScope> AssertPlaying(bool? expected = true, string? message = null, int? timeoutMs = null) => PlayPauseButton.AssertPlaying(expected, message, timeoutMs);",
            generated);
    }

    [Fact]
    public void Shortcut_EmitsNoWrapperOfItsOwn()
    {
        // The part's member is the unit of work; a Run* helper around it would poll twice.
        var generated = Generate(
            "protected bool? IsPlayingShortcut() => PlayPauseButton.IsPlaying();\n" +
            "protected double? GetProgressShortcut() => ProgressSlider.GetValue();\n" +
            "protected Player<TScope> PlayShortcut(int? timeoutMs = null) => PlayPauseButton.Play(timeoutMs);");

        Assert.DoesNotContain("Run", generated);
    }

    [Fact]
    public void GetterShortcut_NamesTheComponentFromItsOwnStem_AndThePartFromThePartMember()
    {
        var generated = Flat(Generate(
            "protected double? GetProgressShortcut() => ProgressSlider.GetValue();"));

        Assert.Contains(
            "public double? GetProgress(int? timeoutMs = null) => ProgressSlider.GetValue(timeoutMs);",
            generated);
        Assert.Contains(
            "public bool WaitProgress(double? expected, int? timeoutMs = null) => ProgressSlider.WaitValue(expected, timeoutMs);",
            generated);
        Assert.Contains(
            "public Player<TScope> AssertProgress(double? expected, string? message = null, int? timeoutMs = null) => ProgressSlider.AssertValue(expected, message, timeoutMs);",
            generated);
    }

    [Fact]
    public void GetterShortcut_ForwardsDeclaredComparisons()
    {
        var generated = Flat(Generate(
            "[GenerateComparisons(Comparison.Equals | Comparison.GreaterThan)]\n" +
            "protected double? GetProgressShortcut() => ProgressSlider.GetValue();\n" +
            "[GenerateComparisons(Comparison.Equals | Comparison.Empty)]\n" +
            "protected string? GetStatusShortcut() => StatusLabel.GetText();"));

        Assert.Contains(
            "public bool WaitProgressGreaterThan(double? expected, int? timeoutMs = null) => ProgressSlider.WaitValueGreaterThan(expected, timeoutMs);",
            generated);
        Assert.Contains(
            "public Player<TScope> AssertProgressGreaterThan(double? expected, string? message = null, int? timeoutMs = null) => ProgressSlider.AssertValueGreaterThan(expected, message, timeoutMs);",
            generated);
        Assert.Contains(
            "public bool WaitStatusEmpty(bool? expected = true, int? timeoutMs = null) => StatusLabel.WaitTextEmpty(expected, timeoutMs);",
            generated);
        Assert.DoesNotContain("StatusContains", generated);
        Assert.DoesNotContain("ProgressLessThan", generated);
    }

    [Fact]
    public void GetterShortcut_PassesItsArgumentsAndParametersFirst()
    {
        // The part's GetAttribute(name, timeoutMs) and WaitAttribute(name, expected, timeoutMs).
        var generated = Flat(Generate(
            "protected string? GetCaptionShortcut() => PlayPauseButton.GetAttribute(\"Name\");\n" +
            "protected string? GetPartAttributeShortcut(string name, int? timeoutMs = null) => PlayPauseButton.GetAttribute(name, timeoutMs);"));

        Assert.Contains(
            "public string? GetCaption(int? timeoutMs = null) => PlayPauseButton.GetAttribute(\"Name\", timeoutMs);",
            generated);
        Assert.Contains(
            "public bool WaitCaption(string? expected, int? timeoutMs = null) => PlayPauseButton.WaitAttribute(\"Name\", expected, timeoutMs);",
            generated);
        Assert.Contains(
            "public string? GetPartAttribute(string name, int? timeoutMs = null) => PlayPauseButton.GetAttribute(name, timeoutMs);",
            generated);
        Assert.Contains(
            "public Player<TScope> AssertPartAttribute(string name, string? expected, string? message = null, int? timeoutMs = null) => PlayPauseButton.AssertAttribute(name, expected, message, timeoutMs);",
            generated);
    }

    [Fact]
    public void ActionShortcut_IsEmittedAsItselfUnderItsPublicName()
    {
        var generated = Flat(Generate(
            "protected Player<TScope> PlayShortcut(int? timeoutMs = null) => PlayPauseButton.Play(timeoutMs);\n" +
            "protected Player<TScope> SetPlayingShortcut(bool? playing, int? timeoutMs = null) => PlayPauseButton.SetPlaying(playing, timeoutMs);"));

        Assert.Contains(
            "public Player<TScope> Play(int? timeoutMs = null) => PlayPauseButton.Play(timeoutMs);",
            generated);
        Assert.Contains(
            "public Player<TScope> SetPlaying(bool? playing, int? timeoutMs = null) => PlayPauseButton.SetPlaying(playing, timeoutMs);",
            generated);
    }

    [Theory]
    [InlineData("protected virtual bool? IsPlayingShortcut() => PlayPauseButton.IsPlaying();", "must not be virtual")]
    [InlineData("public bool? IsPlayingShortcut() => PlayPauseButton.IsPlaying();", "must be 'protected'")]
    [InlineData("protected bool? IsPlayingShortcut() { return PlayPauseButton.IsPlaying(); }", "must be expression-bodied")]
    [InlineData("protected bool? IsPlayingShortcut() => PlayPauseButton.IsPlaying() && Ready;", "must be expression-bodied")]
    [InlineData("protected bool? IsPlayingShortcut() => PlayPauseButton.GetValue() > 0;", "must be expression-bodied")]
    [InlineData("protected bool? IsPlayingShortcut() => PlayPauseButton.HasFocus();", "must call an 'Is*' member")]
    public void MalformedStateShortcut_FailsGenerationNamingTheRule(string member, string expectedMessage)
    {
        var error = Assert.Throws<InvalidOperationException>(() => Generate(member));

        Assert.Contains("'IsPlayingShortcut'", error.Message);
        Assert.Contains(expectedMessage, error.Message);
    }

    [Fact]
    public void StateShortcut_WithAParameter_ForwardsItFirst()
    {
        var generated = Flat(Generate(
            "protected bool? IsShowingShortcut(string viewId) => States.IsShowing(viewId);"));

        Assert.Contains("public bool? IsShowing(string viewId) => States.IsShowing(viewId);", generated);
        Assert.Contains(
            "public bool WaitShowing(string viewId, bool? expected = true, int? timeoutMs = null) => States.WaitShowing(viewId, expected, timeoutMs);",
            generated);
        Assert.Contains(
            "public Player<TScope> AssertShowing(string viewId, bool? expected = true, string? message = null, int? timeoutMs = null) => States.AssertShowing(viewId, expected, message, timeoutMs);",
            generated);
    }

    [Fact]
    public void GetterShortcut_CallingANonGetMember_FailsGeneration()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Generate(
            "protected double? GetProgressShortcut() => ProgressSlider.ReadValue();"));

        Assert.Contains("'GetProgressShortcut'", error.Message);
        Assert.Contains("must call a 'Get*' member", error.Message);
    }

    [Fact]
    public void ShortcutAndCoreClaimingTheSameName_Throws()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Generate(
            "protected bool? IsPlayingShortcut() => PlayPauseButton.IsPlaying();\n" +
            "protected virtual bool? IsPlayingCore(IMauiElement? element) => null;"));

        Assert.Contains("'IsPlaying'", error.Message);
    }

    [Fact]
    public void SkippedShortcut_IsNeitherGeneratedNorReported()
    {
        var generated = Generate(
            "[SkipGeneration(\"hand-written\")]\n" +
            "protected virtual bool? IsPlayingShortcut() => PlayPauseButton.IsPlaying();");

        Assert.DoesNotContain("WaitPlaying", generated);
    }

    [Theory]
    [InlineData("GreaterThan", ">", "greater than")]
    [InlineData("AtLeast", ">=", "at least")]
    [InlineData("LessThan", "<", "less than")]
    [InlineData("AtMost", "<=", "at most")]
    public void OrderedComparison_OnCoreMethod_EmitsWaitAndAssertWithTheOperator(
        string comparison, string op, string words)
    {
        var code = $@"namespace T;
public abstract class S<TScope> where TScope : IScope<TScope>
{{
    [GenerateComparisons(Comparison.Equals | Comparison.{comparison})]
    protected virtual double? GetValueCore(IMauiElement? element) => null;
}}";
        var generated = Flat(ControlObjectGenerator.CreateDefault()
            .Generate(code, new GeneratorOptions { IncludeGeneratedHeader = false }));

        Assert.Contains($"public bool WaitValue{comparison}(double? expected, int? timeoutMs = null)", generated);
        Assert.Contains($"element => GetValueCore(element) {op} expected,", generated);
        Assert.Contains($"public TScope AssertValue{comparison}(double? expected, string? message = null, int? timeoutMs = null)", generated);
        Assert.Contains($"(actual, expected1) => (actual {op} expected1)", generated);
        Assert.Contains($"Expected Value to be {words} '{{expected}}'", generated);
    }

    [Fact]
    public void OrderedComparison_IsNotMistakenForAnotherVariant()
    {
        var code = @"namespace T;
public abstract class S<TScope> where TScope : IScope<TScope>
{
    [GenerateComparisons(Comparison.Equals | Comparison.GreaterThan)]
    protected virtual double? GetValueCore(IMauiElement? element) => null;
}";
        var generated = ControlObjectGenerator.CreateDefault()
            .Generate(code, new GeneratorOptions { IncludeGeneratedHeader = false });

        Assert.DoesNotContain("AtLeast", generated);
        Assert.DoesNotContain("LessThan", generated);
    }
}
