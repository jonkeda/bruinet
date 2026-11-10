using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Display;
using Bruinet.Html.Controls.Text;

namespace Bruinet.Html.UITests.PageObjects;

public sealed class LoginPage : PageObjectBase<LoginPage>
{
    public LoginPage(IHtmlTestContext context)
        : base(context)
    {
    }

    public TextInputControl<LoginPage> UsernameInput => new(this, "[data-testid='username-input']");

    public TextInputControl<LoginPage> EmailInput => new(this, "[data-testid='email-input']");

    public TextInputControl<LoginPage> PasswordInput => new(this, "[data-testid='password-input']");

    public ButtonControl<LoginPage> LoginButton => new(this, "[data-testid='login-btn']");

    public LabelControl<LoginPage> ErrorMessage => new(this, "[data-testid='error-message']");

    public LabelControl<LoginPage> SuccessMessage => new(this, "[data-testid='success-message']");

    /// <summary>The first field validation message on the form.</summary>
    public LabelControl<LoginPage> ValidationMessage => new(this, "[data-testid='login-form'] .text-danger");
}
