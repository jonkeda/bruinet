using Bruinet.Uat;
using Bruinet.Presenter.Bdd.Tests.PageObjects;
using Microsoft.Extensions.DependencyInjection;

namespace Bruinet.Presenter.Bdd.Tests.Runtime;

[Collection(PresenterUatCollection.CollectionName)]
[Trait("Category", "UAT")]
[Trait("Target", "Presenter")]
public sealed class PresenterUatScenarioTests(PresenterFixture fixture)
    : UatScenarioTestBase<PresenterFixture>(fixture)
{
    public static IEnumerable<object[]> ScenarioFiles => GetScenarioFiles();

    protected override UatRuntimeValidationOptions RuntimeValidation { get; } =
        new(Target: "MAUI", Fixture: "FlaUI");

    [Theory(Timeout = 120000)]
    [MemberData(nameof(ScenarioFiles))]
    public Task UatFile_Passes(string filePath)
    {
        return RunUatFileAsync(filePath);
    }

    protected override void BeforeScenario(BddBoundScenario scenario)
    {
        ResetPresenter();
    }

    private void ResetPresenter()
    {
        using var scope = Fixture.Composition.CreateScope();
        var presenterPage = scope.ServiceProvider.GetRequiredService<PresenterPage>();

        if (!presenterPage.IsLoaded())
        {
            return;
        }

        // Settings closed: a scenario that failed with it open would cover the run toolbar.
        if (presenterPage.NarratorSettings.Exists())
        {
            presenterPage.SettingsButton.Click();
        }

        // Back to Present mode: a scenario may have left Narrator mode on.
        presenterPage.PresentModeButton.Click();
        presenterPage.StopButton.Click();
        presenterPage.ReloadButton.Click();
        presenterPage.StatusSummary.AssertTextContains("Ready", timeoutMs: 30000);
        presenterPage.RunButton.AssertClickable(timeoutMs: 30000);
    }
}
