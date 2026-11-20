# Draft templates: a plain site

How a suite with no component library of its own writes what the Scraper's factory drafts: Bruinet's own controls,
each found by what a person reads (a field by its label, a button by its text). `scrape-loop.ps1 -Factory` copies
this file beside a new project's `scraper.config.md`; a suite with form rows and dialogs of its own writes its own.

A field no sign names a control for is `*`: the kind the page gives it decides. A field is found by the label that
points at it, by the label around it, or by its own `aria-label`, placeholder or name.

## Field members

| Control | Kind | Member |
| --- | --- | --- |
| * | checkbox | `public CheckBoxControl<{Self}> {Name} => new(this, Locator.ByXPath("//input[@type='checkbox'][@id=//label[normalize-space()='{Label}']/@for or ancestor::label[starts-with(normalize-space(), '{Label}')] or @aria-label='{Label}' or @name='{Label}']"));` |
| * | select | `public SelectControl<{Self}> {Name} => new(this, Locator.ByXPath("//select[@id=//label[normalize-space()='{Label}']/@for or ancestor::label[starts-with(normalize-space(), '{Label}')] or @aria-label='{Label}' or @name='{Label}']"));` |
| * | textarea | `public TextAreaControl<{Self}> {Name} => new(this, Locator.ByXPath("//textarea[@id=//label[normalize-space()='{Label}']/@for or ancestor::label[starts-with(normalize-space(), '{Label}')] or @aria-label='{Label}' or @placeholder='{Label}' or @name='{Label}']"));` |
| * | | `public TextInputControl<{Self}> {Name} => new(this, Locator.ByXPath("//input[@id=//label[normalize-space()='{Label}']/@for or ancestor::label[starts-with(normalize-space(), '{Label}')] or @aria-label='{Label}' or @placeholder='{Label}' or @name='{Label}']"));` |

## Button member

```csharp
public ButtonControl<{Self}> {Name} => new(this, Locator.ByXPath("//*[self::button or self::a or @role='button' or self::input[@type='button' or @type='submit']][normalize-space()='{Label}' or @value='{Label}' or @aria-label='{Label}']"));
```

## Tab member

A tab is found by how its text starts: one that ends in a count ("Open (3)") is given without it.

```csharp
public ButtonControl<{Self}> {Name} => new(this, Locator.ByXPath("//*[@role='tab'][starts-with(normalize-space(), '{Label}')]"));
```

## Form page

```csharp
{Header}
using Bruinet.Core.Locators;
using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Selection;
using Bruinet.Html.Controls.Text;
using Bruinet.Html.Controls.Toggle;
using Bruinet.Html.Interfaces;
using Bruinet.Html.Pages;

namespace {Namespace};

/// <summary>{Summary}</summary>
public sealed class {Self}(IHtmlTestContext context) : PageObjectBase<{Self}>(context, "{Address}")
{
{Members}
}
```

## Dialog

```csharp
{Header}
using Bruinet.Core.Locators;
using Bruinet.Html.Controls;
using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Selection;
using Bruinet.Html.Controls.Text;
using Bruinet.Html.Controls.Toggle;
using Bruinet.Html.Interfaces;

namespace {Namespace};

/// <summary>{Summary}</summary>
public sealed class {Self}({Page} page) : ContainerBase<{Page}, {Self}>(page, Locator.ByCss("dialog[open], [role='dialog']"))
{
    /// <inheritdoc />
    public override {Self} Self => this;

{Members}
}
```

## Form page test

```csharp
{Header}
using Bruinet.Html.Interfaces;
using {PagesNamespace};

namespace {Namespace};

/// <summary>The page at {Address} shows what its class declares. Read only: nothing is typed and nothing pressed.</summary>
public sealed class {Self} : IAsyncLifetime
{
    private IHtmlTestContext _context = null!;

    public async Task InitializeAsync() => _context = await SiteContext.OpenAsync();

    public Task DisposeAsync() => SiteContext.CloseAsync(_context);

    [Fact]
    public void ShowsWhatItsClassDeclares()
    {
        var page = new {Form}(_context);
        page.Open();
        page.AssertLoaded();
{FieldChecks}
{Checks}
    }
}
```

## Form test

```csharp
{Header}
using Bruinet.Html.Interfaces;
using {PagesNamespace};

namespace {Namespace};

/// <summary>The form "{Opener}" opens shows what its class declares. Read only: it is opened, and nothing is typed or saved.</summary>
public sealed class {Self} : IAsyncLifetime
{
    private IHtmlTestContext _context = null!;

    public async Task InitializeAsync() => _context = await SiteContext.OpenAsync();

    public Task DisposeAsync() => SiteContext.CloseAsync(_context);

    [Fact]
    public void ShowsWhatItsClassDeclares()
    {
        var page = new {Page}(_context);
        page.Open();
        page.AssertLoaded();
        page.{OpenerMember}.Click();
        var form = new {Form}(page);
        form.AssertVisible();
{FieldChecks}
{Checks}
    }
}
```
