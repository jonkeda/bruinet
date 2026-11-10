using Bruinet.Bdd;
using Xunit;
using Bruinet.Core.Settings;

namespace Bruinet.Uat.Tests;

public sealed class UatReflectionRuntimeTests
{
    [Fact]
    public void FromRoot_DiscoversPageAndControlNames()
    {
        var runtime = UatReflectionRuntime.FromRoot(new Fixture());

        var main = Assert.Single(runtime.Pages, page => page.Name == "Main");
        Assert.Contains(main.Controls, control => control.Name == "Name");
        Assert.Contains(main.Controls, control => control.Name == "Greet");
        Assert.Contains(main.Controls, control => control.Name == "Greeting");

        var report = string.Join(Environment.NewLine, runtime.DescribeDiscovery());
        Assert.Contains("Main", report);
        Assert.Contains("Greeting", report);
    }

    [Fact]
    public async Task CreatedCatalog_RunsScenarioThroughDiscoveredObjects()
    {
        var fixture = new Fixture();
        var runtime = UatReflectionRuntime.FromRoot(fixture);
        var scenario = BindScenario(runtime, """
            # Feature: Greeting

            ## Scenario: User receives greeting

            ```gherkin
            Given I am on the Main page
            When I clear Name
            And I enter "Alice" into Name
            And I tap Greet
            Then Greeting should contain "Hello, Alice!"
            ```
            """);

        var result = await new BddScenarioRunner().RunAsync(scenario);

        Assert.True(result.Passed, FormatResults(result));
        Assert.True(fixture.MainPage.Navigated);
        Assert.Equal("Alice", fixture.MainPage.NameEntry.Text);
        Assert.Equal("Hello, Alice!", fixture.MainPage.GreetingLabel.Text);
    }

    [Fact]
    public async Task CreatedCatalog_SelectsAndChecksDiscoveredControls()
    {
        var fixture = new Fixture();
        var runtime = UatReflectionRuntime.FromRoot(fixture);
        var scenario = BindScenario(runtime, """
            # Feature: User Form

            ## Scenario: User profile values are entered

            ```gherkin
            Given I am on the User Form page
            When I enter "Ada" into First Name
            And I check Terms
            And I select "United States" from Country
            Then First Name should contain "Ada"
            And Terms should be checked
            And Country should have selected "United States"
            ```
            """);

        var result = await new BddScenarioRunner().RunAsync(scenario);

        Assert.True(result.Passed, FormatResults(result));
        Assert.True(fixture.UserFormPage.Navigated);
        Assert.Equal("Ada", fixture.UserFormPage.FirstNameEntry.Text);
        Assert.True(fixture.UserFormPage.TermsCheckBox.Checked);
        Assert.Equal("United States", fixture.UserFormPage.CountryPicker.SelectedText);
    }

    [Fact]
    public void CreatedCatalog_WithoutCurrentPage_FailsBinding()
    {
        var runtime = UatReflectionRuntime.FromRoot(new Fixture());

        var diagnostic = Assert.Single(BindErrors(runtime, """
            # Feature: Missing Page

            ## Scenario: Missing current page

            ```gherkin
            When I tap Greet
            ```
            """));

        Assert.Equal("UATB008", diagnostic.Code);
        Assert.Contains("Start with 'Given I am on the ... page'", diagnostic.Message);
    }

    [Fact]
    public void CreatedCatalog_MissingControl_FailsBinding_WithTheControlNames()
    {
        var runtime = UatReflectionRuntime.FromRoot(new Fixture());

        var diagnostic = Assert.Single(BindErrors(runtime, """
            # Feature: Missing Control

            ## Scenario: Missing control

            ```gherkin
            Given I am on the Main page
            When I tap Imaginary Button
            ```
            """));

        Assert.Equal("UATB009", diagnostic.Code);
        Assert.Contains("the Main page has no control 'Imaginary Button'", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Greet", diagnostic.Message);
    }

    [Fact]
    public async Task CreatedCatalog_RunsCustomRootPhrases()
    {
        var fixture = new Fixture();
        var runtime = UatReflectionRuntime.FromRoot(fixture);
        var scenario = BindScenario(runtime, """
            # Feature: Custom Root Phrase

            ## Scenario: Root phrase can store scenario state

            ```gherkin
            Given I remember "Rush Alpha" as a trained player
            Then remembered trained player should be "Rush Alpha"
            ```
            """);

        var result = await new BddScenarioRunner().RunAsync(scenario);

        Assert.True(result.Passed, FormatResults(result));
        Assert.Equal("Rush Alpha", fixture.RememberedTrainedPlayer);
    }

    [Fact]
    public void BddGiven_OnFixtureMethod_RegistersARootPhrase()
    {
        var catalog = UatReflectionRuntime.FromRoot(new Fixture()).CreateCommandCatalog();

        var pattern = Assert.Single(catalog.Patterns, pattern =>
            pattern.Phrase == "I remember {name} as a trained player");
        Assert.Equal(BddEffectiveStepKeyword.Given, pattern.Keyword);
        Assert.Equal($"{typeof(Fixture).FullName}.{nameof(Fixture.RememberTrainedPlayer)}", pattern.CommandId);
    }

    [Fact]
    public async Task BddThen_Phrase_RunsWhenWrittenAfterBut()
    {
        var fixture = new Fixture();
        var runtime = UatReflectionRuntime.FromRoot(fixture);
        var scenario = BindScenario(runtime, """
            # Feature: But

            ## Scenario: A Then phrase follows But

            ```gherkin
            Given I remember "Rush Alpha" as a trained player
            Then remembered trained player should be "Rush Alpha"
            But no other player should be remembered than "Rush Alpha"
            ```
            """);

        var result = await new BddScenarioRunner().RunAsync(scenario);

        Assert.True(result.Passed, FormatResults(result));
        Assert.Equal(1, fixture.OtherPlayerChecks);
    }

    [Fact]
    public void PhraseMethod_WithCommandId_IsBroken_AndFailsOnlyTheStepThatUsesIt()
    {
        // The catalog still builds; the step that binds to the broken phrase names the problem (AD-016).
        var catalog = UatReflectionRuntime.FromRoot(new CommandIdFixture()).CreateCommandCatalog();
        var parse = BddMarkdownParser.Parse("""
            # Feature: Broken

            ## Scenario: Uses the broken phrase

            ```gherkin
            Given I remember something
            ```
            """);

        var bind = BddBinder.Bind(parse.Document!, catalog);

        var error = Assert.Single(bind.Diagnostics);
        Assert.Equal("UATA013", error.Code);
        Assert.Contains(nameof(CommandIdFixture.Remember), error.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(BddStepAttribute.CommandId), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreatedCatalog_InjectsRawAndTypedTestSettingsIntoRootPhrases()
    {
        var fixture = new SettingsFixture();
        var runtime = UatReflectionRuntime.FromRoot(fixture);
        var scenario = BindScenario(runtime, """
            # Feature: Settings Injection

            ## Scenario: Settings are available to phrases

            ```gherkin
            Given the A9 camera settings are available
            Then the raw A9 host setting should match
            ```
            """);
        var runner = new BddScenarioRunner();
        runner.Context.SetSettings(new TestSettingsProviderStub().Resolve(new TestSettingsRequest(
            ProjectDirectory: Directory.GetCurrentDirectory())));

        var result = await runner.RunAsync(scenario);

        Assert.True(result.Passed, FormatResults(result));
        Assert.Equal("192.168.168.1", fixture.TypedHost);
        Assert.Equal("192.168.168.1", fixture.RawHost);
    }

    private static IReadOnlyList<BddDiagnostic> BindErrors(UatReflectionRuntime runtime, string markdown)
    {
        var parse = BddMarkdownParser.Parse(markdown);
        Assert.True(parse.Success, FormatDiagnostics(parse));

        var bind = BddBinder.Bind(parse.Document!, runtime.CreateCommandCatalog());
        Assert.False(bind.Success);
        return bind.Diagnostics.Where(x => x.Severity == BddDiagnosticSeverity.Error).ToList();
    }

    private static BddBoundScenario BindScenario(UatReflectionRuntime runtime, string markdown)
    {
        var parse = BddMarkdownParser.Parse(markdown);
        Assert.True(parse.Success, FormatDiagnostics(parse));
        Assert.NotNull(parse.Document);

        var bind = BddBinder.Bind(parse.Document, runtime.CreateCommandCatalog());
        Assert.True(bind.Success, FormatDiagnostics(bind));
        Assert.NotNull(bind.Document);
        return Assert.Single(bind.Document.Scenarios);
    }

    private static string FormatDiagnostics(BddParseResult result)
    {
        return string.Join(Environment.NewLine, result.Diagnostics.Select(x => $"{x.Code} {x.Message}"));
    }

    private static string FormatDiagnostics(BddBindResult result)
    {
        return string.Join(Environment.NewLine, result.Diagnostics.Select(x => $"{x.Code} {x.Message}"));
    }

    private static string FormatResults(BddScenarioRunResult result)
    {
        return string.Join(
            Environment.NewLine,
            result.Steps.Select(x => $"{x.Status}: {x.Invocation.Step.Text} {x.Message}"));
    }

    private sealed class Fixture
    {
        public MainPage MainPage { get; } = new();

        public UserFormPage UserFormPage { get; } = new();

        public string RememberedTrainedPlayer { get; private set; } = string.Empty;

        public int OtherPlayerChecks { get; private set; }

        public void NavigateToMain()
        {
            MainPage.Navigated = true;
        }

        public void NavigateToUserForm()
        {
            UserFormPage.Navigated = true;
        }

        [BddGiven("I remember {name} as a trained player")]
        public void RememberTrainedPlayer(string name)
        {
            RememberedTrainedPlayer = name;
        }

        [BddThen("remembered trained player should be {name}")]
        public void AssertRememberedTrainedPlayer(string name)
        {
            Assert.Equal(name, RememberedTrainedPlayer);
        }

        [BddThen("no other player should be remembered than {name}")]
        public void AssertNoOtherPlayer(string name)
        {
            OtherPlayerChecks++;
            Assert.Equal(name, RememberedTrainedPlayer);
        }
    }

    private sealed class CommandIdFixture
    {
        [BddGiven("I remember something", CommandId = "Remember")]
        public void Remember()
        {
        }
    }

    private sealed class SettingsFixture
    {
        public string TypedHost { get; private set; } = string.Empty;

        public string RawHost { get; private set; } = string.Empty;

        [BddGiven("the A9 camera settings are available")]
        public void UseTypedSettings(ExampleA9CameraSettings camera)
        {
            TypedHost = camera.Host;
            Assert.Equal("admin", camera.Username);
            Assert.Equal("secret", camera.Password);
        }

        [BddThen("the raw A9 host setting should match")]
        public void UseRawSettings(TestSettings settings)
        {
            RawHost = settings.GetRequired<string>("hardware.a9Camera.host");
        }
    }

    [TestSettingsSection("hardware.a9Camera")]
    private sealed class ExampleA9CameraSettings
    {
        public string Host { get; init; } = string.Empty;

        public string Username { get; init; } = string.Empty;

        public string Password { get; init; } = string.Empty;
    }

    private sealed class TestSettingsProviderStub : ITestSettingsProvider
    {
        public TestSettings Resolve(TestSettingsRequest request)
        {
            var directory = Path.Combine(Path.GetTempPath(), $"bruinet-settings-injection-{Guid.NewGuid():N}");
            try
            {
                var settingsDirectory = Path.Combine(directory, "TestSettings");
                Directory.CreateDirectory(settingsDirectory);
                File.WriteAllText(Path.Combine(settingsDirectory, "testsettings.json"), """
                    {
                      "settings": {
                        "hardware": {
                          "a9Camera": {
                            "host": "192.168.168.1",
                            "username": "admin",
                            "password": "secret"
                          }
                        }
                      }
                    }
                    """);

                return new JsonTestSettingsProvider().Resolve(request with { ProjectDirectory = directory });
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }
    }

    private sealed class MainPage
    {
        public bool Navigated { get; set; }

        public TextEntry NameEntry { get; } = new();

        public GreetingButton GreetButton { get; } = new();

        public TextLabel GreetingLabel { get; } = new();

        public bool WaitReady(int? timeoutMs = null)
        {
            GreetButton.Clicked = () =>
            {
                GreetingLabel.Text = string.IsNullOrWhiteSpace(NameEntry.Text)
                    ? "Please enter your name"
                    : $"Hello, {NameEntry.Text}!";
            };
            return true;
        }
    }

    private sealed class UserFormPage
    {
        public bool Navigated { get; set; }

        public TextEntry FirstNameEntry { get; } = new();

        public ToggleControl TermsCheckBox { get; } = new();

        public PickerControl CountryPicker { get; } = new();

        public bool WaitReady(int? timeoutMs = null)
        {
            return true;
        }
    }

    private sealed class TextEntry
    {
        public string Text { get; private set; } = string.Empty;

        public void Enter(string? text, int? timeoutMs = null)
        {
            Text += text;
        }

        public void Clear(int? timeoutMs = null)
        {
            Text = string.Empty;
        }

        public void AssertTextContains(string? expected, string? message = null, int? timeoutMs = null)
        {
            Assert.Contains(expected ?? string.Empty, Text);
        }
    }

    private sealed class GreetingButton
    {
        public Action? Clicked { get; set; }

        public void Click(int? timeoutMs = null)
        {
            Clicked?.Invoke();
        }
    }

    private sealed class TextLabel
    {
        public string Text { get; set; } = string.Empty;

        public string GetText(int? timeoutMs = null)
        {
            return Text;
        }

        public void AssertTextContains(string? expected, string? message = null, int? timeoutMs = null)
        {
            Assert.Contains(expected ?? string.Empty, Text);
        }
    }

    private sealed class ToggleControl
    {
        public bool Checked { get; private set; }

        public void Check(int? timeoutMs = null)
        {
            Checked = true;
        }

        public void AssertChecked(bool? expected, string? message = null, int? timeoutMs = null)
        {
            Assert.Equal(expected, Checked);
        }
    }

    private sealed class PickerControl
    {
        public string SelectedText { get; private set; } = string.Empty;

        public void SelectByText(string? text, int? timeoutMs = null)
        {
            SelectedText = text ?? string.Empty;
        }

        public void AssertSelectedText(string? expected, string? message = null, int? timeoutMs = null)
        {
            Assert.Equal(expected, SelectedText);
        }
    }
}
