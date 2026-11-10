using Bruinet.Core.Configuration;

namespace Bruinet.WinForms.Tests;

/// <summary>
/// Unit tests for WinForms context options and configuration.
/// </summary>
public class WinFormsTestContextOptionsTests
{
    [Fact]
    public void Options_CanBeCreatedWithExecutablePath()
    {
        var options = new WinFormsTestContextOptions
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
        var options = new WinFormsTestContextOptions
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
        var options = new WinFormsTestContextOptions
        {
            WindowHandle = handle
        };

        Assert.Equal(handle, options.WindowHandle!.Value);
    }

    [Fact]
    public void Options_DefaultTimeouts_AreNull()
    {
        var options = new WinFormsTestContextOptions();
        Assert.Null(options.Timeouts);
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

        var options = new WinFormsTestContextOptions
        {
            Timeouts = timeouts
        };

        Assert.Equal(10000, options.Timeouts?.DefaultWait.ToMs());
        Assert.Equal(20000, options.Timeouts?.PageLoad.ToMs());
        Assert.Equal(200, options.Timeouts?.PollingInterval.ToMs());
    }
}

/// <summary>
/// The WinForms stack's shape on the call model (AD-010): its scope, page and context derive from
/// Core's, and its element and driver are its own.
/// </summary>
public class WinFormsTypeStructureTests
{
    [Fact]
    public void IWinFormsTestContext_IsACoreTestContext_AndAScope()
    {
        Assert.True(typeof(ITestContext).IsAssignableFrom(typeof(IWinFormsTestContext)));
        Assert.True(typeof(IScope).IsAssignableFrom(typeof(IWinFormsTestContext)));
    }

    [Fact]
    public void IWinFormsPage_IsACoreTestPage()
    {
        Assert.True(typeof(ITestPage).IsAssignableFrom(typeof(IWinFormsPage)));
    }

    [Fact]
    public void IWinFormsDriver_IsADiagnosticDriver()
    {
        Assert.True(typeof(IDiagnosticDriver).IsAssignableFrom(typeof(IWinFormsDriver)));
    }

    [Theory]
    [InlineData(typeof(TextBox<>), typeof(IEditableTextControlObject<>))]
    [InlineData(typeof(RichTextBox<>), typeof(IEditableTextControlObject<>))]
    [InlineData(typeof(PasswordBox<>), typeof(IEditableTextControlObject<>))]
    [InlineData(typeof(Button<>), typeof(IClickableControlObject<>))]
    [InlineData(typeof(CheckBox<>), typeof(IToggleControlObject<>))]
    [InlineData(typeof(RadioButton<>), typeof(IToggleControlObject<>))]
    [InlineData(typeof(ComboBox<>), typeof(ISelectorControlObject<>))]
    [InlineData(typeof(ListBox<>), typeof(ISelectorControlObject<>))]
    [InlineData(typeof(TrackBar<>), typeof(IRangeControlObject<>))]
    [InlineData(typeof(NumericUpDown<>), typeof(IRangeControlObject<>))]
    [InlineData(typeof(ProgressBar<>), typeof(IRangeControlObject<>))]
    [InlineData(typeof(DateTimePicker<>), typeof(IDateControlObject<>))]
    [InlineData(typeof(Label<>), typeof(ITextControlObject<>))]
    [InlineData(typeof(TabPage<>), typeof(ITabControlObject<>))]
    [InlineData(typeof(TreeNode<>), typeof(IExpandableControlObject<>))]
    public void A_Control_ImplementsTheCoreInterface_ItsMembersPromise(Type control, Type coreInterface)
    {
        var closed = control.MakeGenericType(typeof(TestPage));
        Assert.Contains(closed.GetInterfaces(), i => i.IsGenericType && i.GetGenericTypeDefinition() == coreInterface);
    }
}
