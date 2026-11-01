using Bruinet.Maui.UITests.Pages;
using Xunit.Abstractions;
using System.Globalization;

namespace Bruinet.Maui.UITests.Tests.Container;

/// <summary>
/// Phase 0 probe: which MAUI layout types expose their AutomationId to UI Automation
/// on Windows, and are therefore usable as Bruinet container scopes.
/// </summary>
/// <remarks>
/// <para>
/// This is a measurement, not a regression suite. Bare MAUI layouts have no
/// AutomationPeer on Windows, which is why
/// <c>samples/Bruinet.Samples.Maui.App/Controls/AutomationContainer.cs</c> exists at
/// all. Before writing a container object for every layout type, we establish which
/// ones can back one.
/// </para>
/// <para>
/// Only two tests here assert. <see cref="AutomationContainer_IsAddressable"/> is the
/// control group and must pass — if the known-good case fails, the instrument is
/// broken and no other reading means anything. <see cref="ProbePage_Renders"/>
/// guards the same thing at the page level. Everything else is reported through
/// <see cref="ProbeAllLayouts_ReportsAddressability"/>, which deliberately does not
/// fail on a negative result: "VerticalStackLayout is not addressable" is a finding
/// to record, not a defect to fix.
/// </para>
/// </remarks>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Pattern", "AutomationProbe")]
public class AutomationProbeTests
{
    /// <summary>
    /// The layout types under test, in the order they appear on the probe page.
    /// The first entry is the control group.
    /// </summary>
    private static readonly string[] ProbeSubjects =
    [
        "AutomationContainer",
        "Grid",
        "VerticalStackLayout",
        "HorizontalStackLayout",
        "StackLayout",
        "FlexLayout",
        "AbsoluteLayout",
        "Border",
        "Frame",
        "ContentView",
        "ScrollView",
        "SwipeView",
        "RefreshView",
    ];

    private readonly MauiFixture _fixture;
    private readonly ITestOutputHelper _output;

    public AutomationProbeTests(MauiFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _fixture.NavigateToAutomationProbe();
    }

    private AutomationProbePage Page => _fixture.AutomationProbePage;

    /// <summary>The probe page rendered at all. Guards every reading below.</summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task ProbePage_Renders()
    {
        Page.PageTitle.AssertExists();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Control group. AutomationContainer supplies an AutomationPeer on Windows, so
    /// it must be both findable and usable as a scope. A failure here invalidates the
    /// whole probe.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task AutomationContainer_IsAddressable()
    {
        var root = Page.TryFindByAutomationId("ProbeAutomationContainer");
        Assert.True(root != null,
            "AutomationContainer was not addressable. This is the probe's control group: "
            + "if it fails, the probe page or the automation session is broken and no "
            + "other result on this page can be trusted.");

        var child = Page.TryFindChildThroughContainer(
            "ProbeAutomationContainer", "ProbeAutomationContainerChild");
        Assert.True(child != null,
            "AutomationContainer was findable but could not be used as a scope. "
            + "Scoped search through the control group must work.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Measures every layout type and reports a table. Records findings; does not
    /// fail on a negative, because a negative is the answer we came for.
    /// </summary>
    [Fact(Timeout = TestConstants.LongTestTimeoutMs)]
    public Task ProbeAllLayouts_ReportsAddressability()
    {
        var report = new StringBuilder();
        report.AppendLine();
        report.AppendLine("| Layout | Root addressable | Usable as scope |");
        report.AppendLine("|---|---|---|");

        var scopable = new List<string>();
        var rootOnly = new List<string>();
        var unaddressable = new List<string>();

        foreach (var subject in ProbeSubjects)
        {
            var rootFound = Page.TryFindByAutomationId($"Probe{subject}") != null;
            var childFound = rootFound
                && Page.TryFindChildThroughContainer($"Probe{subject}", $"Probe{subject}Child") != null;

            report.AppendLine(CultureInfo.InvariantCulture, $"| {subject} | {(rootFound ? "yes" : "NO")} | {(childFound ? "yes" : "NO")} |");

            if (childFound) scopable.Add(subject);
            else if (rootFound) rootOnly.Add(subject);
            else unaddressable.Add(subject);
        }

        report.AppendLine();
        report.AppendLine(CultureInfo.InvariantCulture, $"Usable as scope ({scopable.Count}): {Join(scopable)}");
        report.AppendLine(CultureInfo.InvariantCulture, $"Root only, NOT scopable ({rootOnly.Count}): {Join(rootOnly)}");
        report.AppendLine(CultureInfo.InvariantCulture, $"Not addressable ({unaddressable.Count}): {Join(unaddressable)}");

        _output.WriteLine(report.ToString());

        // The control group must be in the scopable set, otherwise the reading is void.
        Assert.Contains("AutomationContainer", scopable);

        return Task.CompletedTask;
    }

    private static string Join(IReadOnlyCollection<string> items)
        => items.Count == 0 ? "(none)" : string.Join(", ", items);
}
