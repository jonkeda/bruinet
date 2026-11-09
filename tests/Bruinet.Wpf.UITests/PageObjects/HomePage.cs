namespace Bruinet.Wpf.UITests.PageObjects;

/// <summary>
/// Page object for the Home page. It is loaded when its root, AutomationId <c>HomePage</c>, is shown.
/// </summary>
public class HomePage : PageObjectBase<HomePage>
{
    public Label<HomePage> WelcomeText => Label("WelcomeText");
    public Label<HomePage> DescriptionText => Label("DescriptionText");
    public Label<HomePage> FeaturesHeader => Label("FeaturesHeader");

    public HomePage(IWpfTestContext context) : base(context) { }
}
