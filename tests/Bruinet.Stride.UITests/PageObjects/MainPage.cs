namespace Bruinet.Stride.UITests.PageObjects;

/// <summary>
/// Page object for the main sample app page (legacy counter/greeting UI).
/// </summary>
public class MainPage : PageObjectBase<MainPage>
{
    public override string Name => "Main Page";
    protected override Locator ReadyLocator => Locator.ByAutomationId("MainPanel");

    public MainPage(IStrideTestContext context) : base(context) { }

    // Controls
    public TextBlock<MainPage> Title => TextBlock("Title");
    public TextBlock<MainPage> CounterDisplay => TextBlock("CounterDisplay");
    public Button<MainPage> IncrementButton => Button("IncrementButton");
    public Button<MainPage> DecrementButton => Button("DecrementButton");
    public Button<MainPage> ResetButton => Button("ResetButton");
    public EditText<MainPage> NameInput => EditText("NameInput");
    public Button<MainPage> GreetButton => Button("GreetButton");
    public TextBlock<MainPage> GreetingDisplay => TextBlock("GreetingDisplay");
    public ToggleButton<MainPage> DarkModeToggle => ToggleButton("DarkModeToggle");
    public Slider<MainPage> VolumeSlider => Slider("VolumeSlider");
    public TextBlock<MainPage> VolumeDisplay => TextBlock("VolumeDisplay");

    // Actions
    public int GetCounterValue() => Count(CounterDisplay.GetText());

    /// <summary>The number in the counter's text ("Count: 3"); 0 when it shows none.</summary>
    private static int Count(string? counterText)
    {
        var text = counterText ?? "";
        var valueStr = text.Replace("Count: ", "");
        return int.TryParse(valueStr, out var value) ? value : 0;
    }

    public MainPage IncrementCounter()
    {
        var before = GetCounterValue();
        IncrementButton.Click();
        CounterDisplay.WaitText(text => Count(text) == before + 1);
        return this;
    }

    public MainPage DecrementCounter()
    {
        var before = GetCounterValue();
        DecrementButton.Click();
        CounterDisplay.WaitText(text => Count(text) == before - 1);
        return this;
    }

    public MainPage ResetCounter()
    {
        ResetButton.Click();
        CounterDisplay.WaitText(text => Count(text) == 0);
        return this;
    }

    public MainPage Greet(string name)
    {
        NameInput.SetText(name);
        GreetButton.Click();
        GreetingDisplay.WaitText(text => !string.IsNullOrEmpty(text));
        return this;
    }

    public MainPage SetVolume(double value)
    {
        VolumeSlider.SetValue(value);
        return this;
    }
}
