// docs/getting-started/quick-start-stride.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
#region docs:page
using Bruinet.Core.Locators;
using Bruinet.Stride.Controls.Buttons;
using Bruinet.Stride.Controls.Display;
using Bruinet.Stride.Interfaces;
using Bruinet.Stride.Pages;

namespace MyGame.UITests;

public sealed class MainPage(IStrideTestContext context) : PageObjectBase<MainPage>(context)
{
    public TextBlock<MainPage> CounterDisplay => TextBlock("CounterDisplay");
    public Button<MainPage> IncrementButton => Button("IncrementButton");

    protected override Locator ReadyLocator => Locator.ByAutomationId("MainPanel");
}
#endregion
