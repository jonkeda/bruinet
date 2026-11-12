# Quick start: scenarios in Markdown

A first UAT scenario, in Markdown, over the page object of the [web quick start](quick-start-html.md), in five steps.
The code below is compiled with Bruinet's build (`tests/Bruinet.Docs.Samples`), so it is today's API.

## 1. The web quick start

Start from its test project, `MyApp.UITests`, and its page object:

<!-- sample: tests/Bruinet.Docs.Samples/Html/CounterPage.cs#page -->
```csharp
using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Display;
using Bruinet.Html.Interfaces;
using Bruinet.Html.Pages;

namespace MyApp.UITests;

public sealed class CounterPage(IHtmlTestContext context) : PageObjectBase<CounterPage>(context)
{
    public LabelControl<CounterPage> Count => new(this, "[data-testid='count-display']");

    public ButtonControl<CounterPage> Increment => new(this, "[data-testid='increment-btn']");
}
```

## 2. The package

```bash
dotnet add package Bruinet.Uat
```

It brings `Bruinet.Bdd`, the engine that reads the scenarios.

## 3. The config and a scenario

`uat.config.md`, beside the project file, says what the suite drives and where its page objects are:

```markdown
# UAT Config

## Runtime

| Field | Value |
| --- | --- |
| Target | HTML |
| Fixture | CounterUatFixture |

## Assemblies

| Kind | Assembly |
| --- | --- |
| Pages | MyApp.UITests.dll |

## Discovery

| Field | Value |
| --- | --- |
| AllowNameInference | true |
```

`Features/counter.feature.md` is the scenario. `Counter`, `Increment` and `Count` are the page object's names:

````markdown
# Feature: Counter

## Scenario: The button counts up

```gherkin
Given I am on the Counter page
When I tap Increment
Then Count should equal "Current count: 1"
```
````

Both go to the output folder, where the runner reads them:

```xml
<ItemGroup>
  <None Include="uat.config.md" CopyToOutputDirectory="PreserveNewest" />
  <None Include="Features\**\*.feature.md" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

## 4. A fixture and the scenario tests

The fixture opens the browser and tells the runner how to make the page objects; its `NavigateToCounter` is
`Given I am on the Counter page`:

<!-- sample: tests/Bruinet.Docs.Samples/Html/CounterUatFixture.cs#fixture -->
```csharp
using Bruinet.Core.Composition;
using Bruinet.Html.Context;
using Bruinet.Html.Interfaces;
using Bruinet.Html.Playwright;
using Microsoft.Extensions.DependencyInjection;

namespace MyApp.UITests;

// The pages the scenarios name, and how they are made: from this namespace, with the browser's context.
[TestModuleScan(typeof(CounterPage), NamespacePrefix = "MyApp.UITests")]
public sealed class CounterUatFixture : IDisposable
{
    private const string App = "https://localhost:5001";
    private readonly IHtmlTestContext _context;

    public CounterUatFixture()
    {
        _context = PlaywrightTestContext.CreateAsync(new HtmlTestContextOptions { BaseUrl = App }).GetAwaiter().GetResult();
        Composition = TestComposition.ForFixture(this, services => services.AddSingleton(_context));
    }

    public TestComposition Composition { get; }

    // "Given I am on the Counter page"
    public void NavigateToCounter() => _context.NavigateTo($"{App}/counter");

    public void Dispose() => ((IAsyncDisposable)_context).DisposeAsync().AsTask().GetAwaiter().GetResult();
}
```

The test class makes a test case of each scenario:

<!-- sample: tests/Bruinet.Docs.Samples/Html/CounterUatScenarioTests.cs#scenarios -->
```csharp
using Bruinet.Uat;
using Xunit;

namespace MyApp.UITests;

// One test case per scenario of every Features/*.feature.md.
[Trait("Category", "UAT")]
public sealed class CounterUatScenarioTests(CounterUatFixture fixture)
    : UatScenarioTestBase<CounterUatFixture>(fixture), IClassFixture<CounterUatFixture>
{
    public static IEnumerable<object[]> Scenarios => GetScenarioTheoryData();

    protected override UatRuntimeValidationOptions RuntimeValidation { get; } =
        new(Target: "HTML", Fixture: "CounterUatFixture");

    [UatScenarioTheory(Timeout = 120000)]
    [MemberData(nameof(Scenarios))]
    public Task Scenario_Passes(string filePath, string scenarioName) => RunUatScenarioAsync(filePath, scenarioName);
}
```

## 5. Run it

Start the app, then:

```bash
dotnet test --filter "Category=UAT"
```

A step that binds to nothing fails before the browser opens, naming the phrases the page has. Next: the
[UAT template guide](../guides/uat-template-guide.md) and [UAT step definitions](../guides/uat-step-definitions.md).
