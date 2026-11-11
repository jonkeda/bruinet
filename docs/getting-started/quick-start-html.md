# Quick start: a web app in a browser

A first UI test of a web app, in Chromium through Playwright, in five steps. The code below is compiled with
Bruinet's build (`tests/Bruinet.Docs.Samples`), so it is today's API.

## 1. The feed

Bruinet's packages are on the organisation's GitHub Packages feed, not nuget.org. Add it once, with a token that can
read packages (see [VERSIONING](../../VERSIONING.md)).

## 2. A test project

```bash
dotnet new xunit -n MyApp.UITests
cd MyApp.UITests
```

## 3. The one package

```bash
dotnet add package Bruinet.Html.Playwright
```

It brings `Bruinet.Html` and `Bruinet.Core`. Build once, then install the browser with the script the build writes:

```bash
dotnet build
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

## 4. A page object and a test

A page object names the page's controls; a test uses only those.

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

<!-- sample: tests/Bruinet.Docs.Samples/Html/CounterTests.cs#test -->
```csharp
using Bruinet.Html.Context;
using Bruinet.Html.Interfaces;
using Bruinet.Html.Playwright;
using Xunit;

namespace MyApp.UITests;

public sealed class CounterTests : IAsyncLifetime
{
    private const string App = "https://localhost:5001";
    private IHtmlTestContext _context = null!;

    public async Task InitializeAsync()
        => _context = await PlaywrightTestContext.CreateAsync(new HtmlTestContextOptions { BaseUrl = App });

    public async Task DisposeAsync() => await ((IAsyncDisposable)_context).DisposeAsync();

    [Fact]
    public void Increment_CountsUp()
    {
        _context.NavigateTo($"{App}/counter");
        var page = new CounterPage(_context);

        page.Increment.Click();

        page.Count.AssertText("Current count: 1");
    }
}
```

## 5. Run it

Start the app, then:

```bash
dotnet test
```

A failing assert says what it expected and what it read, and the control's locator. Next: the
[Playwright guide](../platform-guides/playwright.md), and [how Bruinet is put together](../architecture/overview.md).
