# Container

A region whose content the app chooses. It scopes the controls inside it, and may read or
act on its own root. Base: `ContainerObjectBase<TParent, TSelf>` in
`src/Bruinet.Maui/Containers/ContainerObjectBase.cs`.

## Shape (framework container)

Two classes in one file: an open generic for subclassing, and a sealed concrete one for
callers that need no subclass. Popup declares a Core method, so it is a `.tpl.cs` and both
classes are `partial`.

```csharp
namespace Bruinet.Maui.CommunityToolkit.Controls.Views;

/// <summary>CommunityToolkit.Maui <c>Popup</c>: a view shown modally over the page.</summary>
/// <remarks>...route, root lookup, app-side requirements...</remarks>
/// <typeparam name="TParent">The scope that raises the popup, usually the page.</typeparam>
/// <typeparam name="TSelf">The popup type itself (self-referencing for fluent returns).</typeparam>
public partial class Popup<TParent, TSelf> : ContainerObjectBase<TParent, TSelf>
    where TParent : IMauiScope<TParent>
    where TSelf : Popup<TParent, TSelf>
{
    public Popup(IMauiScope<TParent> parentScope, Locator locator) : base(parentScope, locator) { }
    public Popup(IMauiScope<TParent> parentScope, string locatorValue) : base(parentScope, locatorValue) { }

    #region Core Methods (Element-Aware, No Logging)

    /// <summary>Whether the popup is on screen.</summary>
    [AbsenceTolerant]
    protected virtual bool? IsOpenCore(IMauiElement? element) => element != null;

    #endregion
}

/// <summary>A <see cref="Popup{TParent, TSelf}"/> for use where no popup-specific subclass is needed.</summary>
public sealed partial class Popup<TParent> : Popup<TParent, Popup<TParent>>
    where TParent : IMauiScope<TParent>
{
    public Popup(IMauiScope<TParent> parentScope, Locator locator) : base(parentScope, locator) { }
    public Popup(IMauiScope<TParent> parentScope, string locatorValue) : base(parentScope, locatorValue) { }
}
```

A page declares it like a control: `public Popup<MyPage> TestPopup => new(this, "TestPopup");`.
A container with no Core methods and no shortcuts is a plain `.cs` (R5), and not `partial`.
Some older behaviour-less containers (`Border`, `Grid`, `ContentView`) are still `.tpl.cs`
files with an empty `.gen.cs`; they predate R5 and are not a model.

## What the base already gives

Do not re-declare these:

- `Exists`, `IsVisible`, `WaitExists`, `WaitVisible`, `AssertExists`, `AssertVisible`,
  `GetAttribute`, `IsReady`, `WaitReady`;
- typed children scoped to the root: `Child<TControl>(id)`, `Label(id)`, `Button(id)`,
  `Entry(id)`, `CheckBox(id)`;
- `Parent`, to leave the container in a chain;
- `Confirm(read, done, timeoutMs)` for waiting for an action's effect inside Core methods.

## Rules

- **Core methods act on the root.** Same contract as a control:
  `protected virtual *Core(IMauiElement element, ...)`, where `element` is the container root.
  Actions and asserts return `TSelf`, so the chain stays inside; generated setters return the
  parent. The caller leaves with `.Parent`.
- **Absence**: a read that must answer for a missing container carries `[AbsenceTolerant]` and
  takes `IMauiElement?` (`Popup.IsOpenCore`, `StateContainer.IsShowingCore(element, viewAutomationId)`).
- **Capabilities**: implement the interface on the container
  (`RefreshView<TParent, TSelf> : ContainerObjectBase<TParent, TSelf>, IRefreshableControlObject<TSelf>`)
  with Core methods for whatever the base does not supply (`IsEnabledCore`, `PullToRefreshCore`).
- **Scoping is strict.** Children resolve only under the root, never in the parent. That is the
  point of a container; do not add a fallback.
- **Fixed part ids.** For an app component with several parts, the page sets one id on the
  instance and the component gives its parts fixed ids (`Value`, `Clear`, `Title`). The
  container finds them under the instance root. Never compose a part id from the instance id
  (`Name_Value`) or ask the app for a bindable property that only names a part. Ids that carry
  data identity (`Action_{id}`, row roots) are fine.
- **A member that needs a child and the root** is hand-written as plain calls in sequence, with
  a remark naming both (R4):

  ```csharp
  /// <remarks>Across two parts: the button, and the popup root that should disappear.</remarks>
  public TSelf CloseWith(string buttonAutomationId, int? timeoutMs = null)
  {
      Button(buttonAutomationId).Click(timeoutMs);

      if (!WaitOpen(false, timeoutMs))
      {
          throw new TimeoutException(
              $"Popup '{Locator.Value}' was still open after pressing '{buttonAutomationId}'.");
      }

      return Self;
  }
  ```

## Root lookup overrides

Each with a `<remarks>` giving the reason.

| Situation | Override |
| --- | --- |
| The app rebuilds or replaces the root during a test (Popup opens and closes; StateContainer swaps its children) | `protected override bool CacheContainerRoot => false;` |
| The root is not under the parent (the toolkit shows a Popup as a modal page) | `FindContainerRootElement()` searching `Context.AppElement`; throw `ElementNotFoundException` naming the locator |
| The container is shown over its parent, which may be busy or out of the tree while it shows (a Popup, a dialog) | `protected override bool AsksParent => false;` - it answers for itself, and `Page` still names the page for the log |
| Content loads asynchronously | `protected override ScopeReadiness ProbeContentReadiness(IMauiElement root)`: one check of concrete state (spinner gone, count non-zero), answering `ContentReady()` or `ContentNotReady("what it waits for")`. Never a sleep and never a loop: every call on a child asks it on each attempt |
| The container scrolls its content | `public override IMauiElement? ScrollingRoot => TryGetContainerRoot();` (ScrollView). Otherwise inherit the parent's |

## Windows

Layout views with no automation peer (`ContentView`, `Border`, and the layouts a toolkit
container hosts) are invisible to UI Automation until the app registers the Bruinet automation
handlers from `samples/Bruinet.Maui.AppSupport`. Say so in the class remarks and check the
sample app registers it. `Border` needs its own registration.

## App-specific containers (test projects)

In the test project's `Containers/` folder, a plain `.cs`:

```csharp
public class ProductFormContainer : ContainerObjectBase<GridCollectionDemoPage, ProductFormContainer>
{
    public ProductFormContainer(IMauiScope<GridCollectionDemoPage> parentScope, string automationId)
        : base(parentScope, automationId) { }

    public Entry<ProductFormContainer> NameEntry => new(this, "ProductNameEntry");
    public ProductOptionsContainer Options => new(this, "ProductOptionsContainer");   // nested

    /// <summary>Fills the form and returns this container so the caller stays in scope.</summary>
    public ProductFormContainer FillProduct(string name, string price, bool inStock)
    {
        NameEntry.SetText(name);
        Options.InStockCheckBox.SetChecked(inStock);
        return Self;
    }
}
```

- Derive from `ContainerObjectBase` or from a framework container (`Border<TParent, TSelf>`).
- Children are named properties, created fresh on each access.
- Domain helpers compose those children and return `Self`.

## Examples to read

`src/Bruinet.Maui/Controls/Container/Border.tpl.cs` (no behaviour, handler note), `ScrollView.tpl.cs`
(`ScrollingRoot`, Core methods on the root), `RefreshView.tpl.cs` (capability interface),
`src/Bruinet.Maui.CommunityToolkit/Controls/Views/Popup.tpl.cs` (root outside the page, across-parts member),
`src/Bruinet.Maui.CommunityToolkit/Controls/Layouts/StateContainer.tpl.cs` (uncached root, parameterised read).
