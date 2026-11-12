// docs/guides/test-writing.md quotes these regions: change them here, then run tools/Scripts/update-doc-samples.ps1.
using Bruinet.Maui.Controls.Buttons;
using Bruinet.Maui.Controls.Display;
using Bruinet.Maui.Controls.Text;
using Bruinet.Maui.Interfaces;
using Bruinet.Maui.Pages;
using Xunit;

namespace Bruinet.Docs.Samples.Guides;

public sealed class FormTests(FormFixture fixture) : IClassFixture<FormFixture>
{
    private readonly FormFixture _fixture = fixture;

    #region docs:shape
    [Fact]
    public void SavingValidForm_ShowsSuccess()
    {
        var page = _fixture.OpenFormPage();

        page.FillRequiredFields("Example");
        page.Save();

        Assert.True(page.SuccessMessage.IsVisible());
    }
    #endregion

    [Fact]
    public void SavedForm_SaysSo()
    {
        var page = _fixture.OpenFormPage();

        #region docs:assert
        Assert.True(page.SubmitButton.IsEnabled());
        Assert.Equal("Saved", page.Status.GetText());
        #endregion
    }
}

public sealed class FormFixture
{
    public IMauiTestContext Context { get; } = null!;

    public FormPage OpenFormPage() => new(Context);
}

public sealed class FormPage(IMauiTestContext context) : PageObjectBase<FormPage>(context)
{
    public Entry<FormPage> Title => Entry("TitleEntry");
    public Button<FormPage> SubmitButton => Button("SubmitButton");
    public Label<FormPage> Status => Label("StatusLabel");
    public Label<FormPage> SuccessMessage => Label("SuccessMessage");

    public FormPage FillRequiredFields(string name)
    {
        Title.SetText(name);
        return this;
    }

    public FormPage Save()
    {
        SubmitButton.Click();
        return this;
    }
}
