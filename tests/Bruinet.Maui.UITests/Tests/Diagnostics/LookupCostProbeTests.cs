using System.Diagnostics;
using Bruinet.Maui.UITests.Pages;
using Xunit;
using Xunit.Abstractions;

namespace Bruinet.Maui.UITests.Tests.Diagnostics;

/// <summary>
/// What one lookup and one alive check cost against the real app.
/// </summary>
/// <remarks>
/// For the call model's root rule: a scope keeps its root for the
/// object's lifetime and checks it with the alive rule (one <c>InstanceKey</c> read) on every
/// access, unless that check costs more than finding the root again. The numbers are written to
/// the test output and recorded in the plan's baseline table.
/// </remarks>
[Collection("Maui")]
[Trait("Category", "UITest")]
public class LookupCostProbeTests
{
    private const int Repetitions = 50;

    private readonly MauiFixture _fixture;
    private readonly ITestOutputHelper _output;

    public LookupCostProbeTests(MauiFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _fixture.Open(SamplePage.Buttons);
    }

    [ProbeFact]
    public void LookupAndAliveCheck_CostPerCall()
    {
        var context = _fixture.Context;
        var pageLocator = Locator.ByAutomationId(nameof(ButtonsTestPage));
        var childLocator = Locator.ByAutomationId("StatusLabel");

        var root = context.TryFindElement(pageLocator);
        Assert.NotNull(root);

        var appWide = Measure(() => context.TryFindElement(pageLocator));
        var child = Measure(() => root!.TryFindElement(childLocator));
        var alive = Measure(() => root!.InstanceKey);

        _output.WriteLine($"app-wide lookup (page root): {appWide:F1} ms per call");
        _output.WriteLine($"child lookup under the page root: {child:F1} ms per call");
        _output.WriteLine($"alive check (InstanceKey read): {alive:F1} ms per call");
    }

    private static double Measure(Func<object?> call)
    {
        call(); // warm-up
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < Repetitions; i++)
        {
            call();
        }

        return stopwatch.Elapsed.TotalMilliseconds / Repetitions;
    }
}
