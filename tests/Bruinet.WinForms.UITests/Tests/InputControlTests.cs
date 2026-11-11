using Bruinet.WinForms.UITests.Fixtures;
using Bruinet.WinForms.UITests.Pages;

namespace Bruinet.WinForms.UITests.Tests;

/// <summary>
/// Tests for individual input controls (TextBox, PasswordBox, NumericUpDown, RichTextBox, ComboBox, CheckBox).
/// </summary>
[Collection("WinForms UITests")]
public class InputControlTests
{
    private readonly WinFormsSampleFixture _fixture;

    public InputControlTests(WinFormsSampleFixture fixture) => _fixture = fixture;

    private LoginPage GetPage()
    {
        var page = new LoginPage(_fixture.Context);
        page.ClickClear();
        page.WaitForFormCleared(timeoutMs: 2000);
        return page;
    }

    [Fact]
    public void TextBox_Enter_SetsTextCorrectly()
    {
        var page = GetPage();
        page.EnterUsername("testuser");
        Assert.Equal("testuser", page.GetUsername());
    }

    [Fact]
    public void TextBox_Clear_RemovesAllText()
    {
        var page = GetPage();
        page.EnterUsername("testuser");
        page.ClickClear();
        page.WaitForFormCleared();
        Assert.Equal(string.Empty, page.GetUsername());
    }

    [Fact]
    public void PasswordBox_Enter_AcceptsPassword()
    {
        // A password field reads as nothing through UI Automation; the app says what it was given.
        var page = GetPage();
        page.EnterPassword("secret123");
        page.EnterUsername("testuser");
        page.ClickLogin();
        Assert.Contains("with a 9-character password", page.GetStatusMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void NumericUpDown_SetValue_SetsNumberCorrectly()
    {
        var page = GetPage();
        page.SetPort(9090);
        Assert.Equal(9090, page.GetPort());
    }

    [Fact]
    public void NumericUpDown_SetValue_HandlesMinimumValue()
    {
        var page = GetPage();
        page.SetPort(1);
        Assert.Equal(1, page.GetPort());
    }

    [Fact]
    public void NumericUpDown_SetValue_HandlesMaximumValue()
    {
        var page = GetPage();
        page.SetPort(65535);
        Assert.Equal(65535, page.GetPort());
    }

    [Fact]
    public void RichTextBox_SetContent_SetsTextCorrectly()
    {
        var page = GetPage();
        page.SetNotes("Important notes for testing");
        Assert.Contains("Important notes", page.GetNotes());
    }

    [Fact]
    public void RichTextBox_Clear_RemovesAllText()
    {
        var page = GetPage();
        page.SetNotes("Some notes");
        page.ClickClear();
        page.WaitForFormCleared();
        Assert.Equal(string.Empty, page.GetNotes());
    }

    [Fact]
    public void Form_Login_WithAllInputs()
    {
        var page = GetPage();
        page.EnterUsername("admin");
        page.EnterPassword("admin123");
        page.SetPort(8080);
        page.SetNotes("Admin test user");
        page.SelectRole("Admin");
        page.SetRememberMe(true);

        Assert.Equal("admin", page.GetUsername());
        Assert.Equal(8080, page.GetPort());
        Assert.Contains("Admin test user", page.GetNotes());
        Assert.Equal("Admin", page.GetSelectedRole());
        Assert.True(page.IsRememberMeChecked());
    }

    [Fact]
    public void Form_Clear_ResetsAllFields()
    {
        var page = GetPage();
        page.EnterUsername("testuser");
        page.EnterPassword("password");
        page.SetPort(9000);
        page.SetRememberMe(true);

        page.ClickClear();
        page.WaitForFormCleared();

        Assert.Equal(string.Empty, page.GetUsername());
        Assert.Equal(8080, page.GetPort()); // Default port
        Assert.False(page.IsRememberMeChecked());
    }

    [Fact]
    public void Form_Login_UpdatesStatusMessage()
    {
        var page = GetPage();
        page.EnterUsername("testuser");
        page.SelectRole("User");
        page.ClickLogin();
        page.WaitForLoginComplete();

        var status = page.GetStatusMessage();
        Assert.Contains("testuser", status);
        Assert.Contains("User", status);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("User")]
    [InlineData("Guest")]
    public void ComboBox_SelectRole_VariousRoles(string expectedRole)
    {
        var page = GetPage();
        page.SelectRole(expectedRole);
        Assert.Equal(expectedRole, page.GetSelectedRole());
    }

    [Fact]
    public void CheckBox_Remember_TogglesCorrectly()
    {
        var page = GetPage();

        page.SetRememberMe(false);
        Assert.False(page.IsRememberMeChecked());

        page.SetRememberMe(true);
        Assert.True(page.IsRememberMeChecked());

        page.SetRememberMe(false);
        Assert.False(page.IsRememberMeChecked());
    }

    [Fact]
    public void TextBox_WithSpecialCharacters()
    {
        var page = GetPage();
        page.EnterUsername("user@example.com");
        Assert.Equal("user@example.com", page.GetUsername());
    }

    [Fact]
    public void NumericUpDown_CommonPorts()
    {
        var page = GetPage();
        var commonPorts = new[] { 80.0, 443.0, 3306.0, 5432.0, 8080.0 };

        foreach (var port in commonPorts)
        {
            page.SetPort(port);
            Assert.Equal(port, page.GetPort());
        }
    }
}
