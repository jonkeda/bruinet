using Bruinet.Core.Configuration;

namespace Bruinet.Wpf.Tests;

/// <summary>
/// Unit tests for WPF context options and configuration.
/// </summary>
public class WpfTestContextOptionsTests
{
    [Fact]
    public void Options_CanBeCreatedWithExecutablePath()
    {
        var options = new WpfTestContextOptions
        {
            ExecutablePath = @"C:\test\app.exe"
        };

        Assert.Equal(@"C:\test\app.exe", options.ExecutablePath);
        Assert.Null(options.ProcessId);
        Assert.Null(options.WindowHandle);
    }

    [Fact]
    public void Options_CanBeCreatedWithProcessId()
    {
        var options = new WpfTestContextOptions
        {
            ProcessId = 12345
        };

        Assert.Equal(12345, options.ProcessId);
        Assert.Null(options.ExecutablePath);
    }

    [Fact]
    public void Options_CanBeCreatedWithWindowHandle()
    {
        var handle = new IntPtr(0x12345);
        var options = new WpfTestContextOptions
        {
            WindowHandle = handle
        };

        Assert.Equal(handle, options.WindowHandle!.Value);
    }

    [Fact]
    public void Options_DefaultTimeouts_AreTheSharedDefaults()
    {
        var options = new WpfTestContextOptions();
        Assert.Equal(Bruinet.Core.Configuration.TimeoutSettings.Default.DefaultWait, options.Timeouts.DefaultWait);
    }

    [Fact]
    public void Options_CanSetCustomTimeouts()
    {
        var timeouts = new TimeoutSettings
        {
            DefaultWait = TimeSpan.FromMilliseconds(10000),
            PageLoad = TimeSpan.FromMilliseconds(20000),
            PollingInterval = TimeSpan.FromMilliseconds(200)
        };

        var options = new WpfTestContextOptions
        {
            Timeouts = timeouts
        };

        Assert.Equal(10000, options.Timeouts?.DefaultWait.ToMs());
        Assert.Equal(20000, options.Timeouts?.PageLoad.ToMs());
        Assert.Equal(200, options.Timeouts?.PollingInterval.ToMs());
    }
}

/// <summary>
/// The WPF stack's shape on the call model (AD-010): its scope, page and context derive from
/// Core's, and its element and driver are its own.
/// </summary>
public class WpfTypeStructureTests
{
    [Fact]
    public void IWpfTestContext_IsACoreTestContext_AndAScope()
    {
        Assert.True(typeof(ITestContext).IsAssignableFrom(typeof(IWpfTestContext)));
        Assert.True(typeof(IScope).IsAssignableFrom(typeof(IWpfTestContext)));
    }

    [Fact]
    public void IWpfPage_IsACoreTestPage()
    {
        Assert.True(typeof(ITestPage).IsAssignableFrom(typeof(IWpfPage)));
    }

    [Fact]
    public void IWpfDriver_IsADiagnosticDriver()
    {
        Assert.True(typeof(IDiagnosticDriver).IsAssignableFrom(typeof(IWpfDriver)));
    }

    [Theory]
    [InlineData(typeof(TextBox<>), typeof(IEditableTextControlObject<>))]
    [InlineData(typeof(PasswordBox<>), typeof(IEditableTextControlObject<>))]
    [InlineData(typeof(Button<>), typeof(IClickableControlObject<>))]
    [InlineData(typeof(CheckBox<>), typeof(IToggleControlObject<>))]
    [InlineData(typeof(RadioButton<>), typeof(IToggleControlObject<>))]
    [InlineData(typeof(ComboBox<>), typeof(ISelectorControlObject<>))]
    [InlineData(typeof(ListBox<>), typeof(ISelectorControlObject<>))]
    [InlineData(typeof(Slider<>), typeof(IRangeControlObject<>))]
    [InlineData(typeof(Label<>), typeof(ITextControlObject<>))]
    [InlineData(typeof(TabItem<>), typeof(ITabControlObject<>))]
    [InlineData(typeof(Expander<>), typeof(IExpandableControlObject<>))]
    [InlineData(typeof(TextBox<>), typeof(IFocusableControlObject<>))]
    public void A_Control_ImplementsTheCoreInterface_ItsMembersPromise(Type control, Type coreInterface)
    {
        var closed = control.MakeGenericType(typeof(TestPage));
        Assert.Contains(closed.GetInterfaces(), i => i.IsGenericType && i.GetGenericTypeDefinition() == coreInterface);
    }

    [Fact]
    public void Locator_ByAutomationId_CreatesCorrectLocator()
    {
        var locator = Locator.ByAutomationId("MyControl");
        Assert.Equal(LocatorStrategy.AutomationId, locator.Strategy);
        Assert.Equal("MyControl", locator.Value);
    }
}
