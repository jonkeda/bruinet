using Bruinet.Bdd;
using Xunit;

namespace Bruinet.Uat.Tests;

public sealed class UatDiscoveryTests
{
    [Fact]
    public void Discover_WithNameInference_FindsPagesControlsActionsAndCommandPhrases()
    {
        var result = UatDiscovery.Discover(
            new UatDiscoveryOptions(),
            typeof(UatDiscoveryTests).Assembly);

        Assert.True(result.Success, FormatDiagnostics(result));

        var settings = Assert.Single(result.Pages, x => x.Name == "Settings");
        Assert.Contains(settings.Controls, x => x.Name == "Display Name" && x.Actions.Contains("enter"));
        Assert.Contains(settings.Controls, x => x.Name == "Email Notifications" && x.Actions.Contains("check"));
        Assert.Contains(settings.Controls, x => x.Name == "Save" && x.Actions.Contains("tap"));
        Assert.DoesNotContain(settings.Controls, x => x.MemberName == nameof(SettingsPage.InternalButton));

        var login = Assert.Single(result.Pages, x => x.Name == "Login");
        Assert.Contains(login.Controls, x => x.Name == "Sign in" && x.MemberName == nameof(LoginPage.SubmitButton));

        Assert.Contains(result.Commands, x =>
            x.Keyword == BddEffectiveStepKeyword.When &&
            x.Phrase == "I sign in with credentials" &&
            x.Method.Name == nameof(LoginPage.SignInWithCredentials));
        Assert.Contains(result.Catalog.Patterns, x =>
            x.Keyword == BddEffectiveStepKeyword.When &&
            x.Phrase == "I sign in with credentials");
    }

    [Fact]
    public void Discover_WithExplicitAttributesRequired_DoesNotUseDefaultPageNaming()
    {
        var result = UatDiscovery.Discover(
            new UatDiscoveryOptions { RequireExplicitUatAttributes = true },
            typeof(UatDiscoveryTests).Assembly);

        Assert.True(result.Success, FormatDiagnostics(result));
        Assert.DoesNotContain(result.Pages, x => x.Name == "Settings");
        Assert.Contains(result.Pages, x => x.Name == "Login");
    }

    [Fact]
    public void Discover_MethodWithMultiplePhrases_RegistersEachPhrase()
    {
        var result = UatDiscovery.Discover(
            new UatDiscoveryOptions(),
            typeof(UatDiscoveryTests).Assembly);

        Assert.True(result.Success, FormatDiagnostics(result));
        Assert.Contains(result.Catalog.Patterns, x =>
            x.Keyword == BddEffectiveStepKeyword.When &&
            x.Phrase == "I save the settings");
        Assert.Contains(result.Catalog.Patterns, x =>
            x.Keyword == BddEffectiveStepKeyword.Then &&
            x.Phrase == "the settings are saved");
    }

    [Fact]
    public void Discover_StepOnAControlMethod_IsNotACommandPhrase()
    {
        var result = UatDiscovery.Discover(
            new UatDiscoveryOptions(),
            typeof(UatDiscoveryTests).Assembly);

        Assert.True(result.Success, FormatDiagnostics(result));
        Assert.Contains(
            Assert.Single(result.Pages, x => x.Name == "Settings").Controls,
            x => x.MemberName == nameof(SettingsPage.BrightnessSlider));
        Assert.DoesNotContain(result.Commands, x => x.Phrase == "I dim {control}");
    }

    private static string FormatDiagnostics(UatDiscoveryResult result)
    {
        return string.Join(
            Environment.NewLine,
            result.Diagnostics.Select(x => $"{x.Location}: {x.Code} {x.Message}"));
    }

    public sealed class SettingsPage
    {
        public DisplayNameEntry DisplayNameEntry { get; } = new();

        public EmailNotificationsSwitch EmailNotificationsSwitch { get; } = new();

        public SaveButton SaveButton { get; } = new();

        public BrightnessSlider BrightnessSlider { get; } = new();

        [BddIgnore]
        public SaveButton InternalButton { get; } = new();
    }

    [UatName("Login")]
    public sealed class LoginPage
    {
        [UatName("Sign in")]
        public SaveButton SubmitButton { get; } = new();

        [BddWhen("I sign in with credentials")]
        public void SignInWithCredentials()
        {
        }
    }

    public sealed class PhraseCatalogPage
    {
        [BddWhen("I save the settings")]
        [BddThen("the settings are saved")]
        public void SaveSettings()
        {
        }
    }

    public sealed class DisplayNameEntry
    {
        [UatAction("enter")]
        public void Enter(string value)
        {
        }
    }

    public sealed class EmailNotificationsSwitch
    {
        [UatAction("check")]
        public void Check()
        {
        }
    }

    public sealed class BrightnessSlider
    {
        [BddWhen("I dim {control}")]
        public void Dim()
        {
        }
    }

    public sealed class SaveButton
    {
        [UatAction("tap")]
        public void Tap()
        {
        }
    }
}
