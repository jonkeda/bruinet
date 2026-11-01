using System.Globalization;
using Bruinet.Generator.Models;

namespace Bruinet.Generator.Tests.Generators;

/// <summary>
/// Warnings for Core methods that start a second unit of work inside their generated wrapper.
/// </summary>
public class NestedUnitOfWorkTests
{
    private const string Component = @"namespace T;
public partial class Player<TScope> : ComponentObjectBase<TScope, Player<TScope>>
    where TScope : IMauiScope<TScope>
{{
    public Button<Player<TScope>> PlayButton => new(this, ""Play"");
    public Label<Player<TScope>> Status => new(this, ""Status"");
    private Label<Player<TScope>> Row(string id) => new(this, id);
{0}
}}";

    private static (string Code, IReadOnlyList<string> Warnings) Generate(string members)
    {
        var generator = ControlObjectGenerator.CreateDefault();
        var code = generator.Generate(
            string.Format(CultureInfo.InvariantCulture, Component, members), new GeneratorOptions { IncludeGeneratedHeader = false });
        return (code, generator.Warnings);
    }

    [Theory]
    [InlineData("protected virtual bool? IsBusyCore(IMauiElement? element) => Status.GetText() == \"busy\";", "Status.GetText")]
    [InlineData("protected virtual void StartCore(IMauiElement element, int? timeoutMs = null) { PlayButton.Click(timeoutMs); }", "PlayButton.Click")]
    [InlineData("protected virtual void CloseCore(IMauiElement element, string id, int? timeoutMs = null) { Button(id).Click(); }", "Button(id).Click")]
    [InlineData("protected virtual string? GetRowCore(IMauiElement? element, string id) => Row(id).GetText();", "Row(id).GetText")]
    public void CoreMethodCallingAPart_IsWarnedButStillGenerated(string member, string call)
    {
        var (code, warnings) = Generate(member);

        var warning = Assert.Single(warnings);
        Assert.Contains($"'{call}'", warning);
        Assert.Contains("nested unit of work", warning);
        Assert.Contains("In Player:", warning);
        Assert.Contains("public ", code);
    }

    [Theory]
    [InlineData("RunWait(() => true, timeoutMs)", "RunWait")]
    [InlineData("RunWaitWithElement(true, e => true, timeoutMs)", "RunWaitWithElement")]
    [InlineData("RunDo(() => { }, timeoutMs)", "RunDo")]
    public void CoreMethodCallingARunHelper_IsWarned(string call, string helper)
    {
        var (_, warnings) = Generate(
            $"protected virtual void WaitCore(IMauiElement element, int? timeoutMs = null) {{ {call}; }}");

        var warning = Assert.Single(warnings);
        Assert.Contains($"'{helper}'", warning);
        Assert.Contains("Wait with Confirm instead", warning);
    }

    [Fact]
    public void CoreMethodReadingItsOwnElementAndWaitingWithConfirm_IsNotWarned()
    {
        var (_, warnings) = Generate(
            "protected virtual void StopCore(IMauiElement element, int? timeoutMs = null)\n" +
            "{\n" +
            "    element.Invoke();\n" +
            "    element.FindElement(Locator.ByAutomationId(\"x\"), 0).Invoke();\n" +
            "    Confirm(() => element.Name, name => name == \"Play\", timeoutMs);\n" +
            "}");

        Assert.Empty(warnings);
    }

    [Fact]
    public void HandWrittenMemberCallingPartsInSequence_IsNotWarned()
    {
        var (_, warnings) = Generate(
            "public Player<TScope> Restart(int? timeoutMs = null)\n" +
            "{\n" +
            "    PlayButton.Click(timeoutMs);\n" +
            "    RunWait(() => Status.GetText() == \"playing\", timeoutMs);\n" +
            "    return this;\n" +
            "}");

        Assert.Empty(warnings);
    }

    [Fact]
    public void Warnings_AreResetForEachGeneration()
    {
        var generator = ControlObjectGenerator.CreateDefault();
        var options = new GeneratorOptions { IncludeGeneratedHeader = false };

        generator.Generate(string.Format(CultureInfo.InvariantCulture, Component,
            "protected virtual bool? IsBusyCore(IMauiElement? element) => Status.GetText() == \"busy\";"), options);
        Assert.NotEmpty(generator.Warnings);

        generator.Generate(string.Format(CultureInfo.InvariantCulture, Component, ""), options);
        Assert.Empty(generator.Warnings);
    }
}
