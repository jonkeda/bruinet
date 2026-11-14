using Bruinet.Core.Logging;
using Bruinet.Html.Controls.Text;

namespace Bruinet.Html.Tests;

/// <summary>A secret typed with <c>SetSecret</c> reaches the field and nothing else: not the call log, not a message.</summary>
public sealed class SecretInputTests
{
    private const string Secret = "hunter2-Secret";

    private readonly Mock<IHtmlTestContext> _context = new();
    private readonly Mock<ITestLogger> _logger = new();
    private readonly Mock<IHtmlElement> _field = new();
    private readonly List<LogEvent> _logged = [];
    private string _value = string.Empty;

    public SecretInputTests()
    {
        _context.Setup(c => c.Timeouts).Returns(new TimeoutSettings { DefaultWait = TimeSpan.FromMilliseconds(300), PageLoad = TimeSpan.FromMilliseconds(300), PollingInterval = TimeSpan.FromMilliseconds(1) });
        _context.Setup(c => c.Context).Returns(() => _context.Object);
        _context.Setup(c => c.Logger).Returns(_logger.Object);
        _context.Setup(c => c.IsIdle()).Returns(true);
        _context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Password"))).Returns(_field.Object);
        _logger.Setup(l => l.Write(It.IsAny<LogEvent>())).Callback<LogEvent>(_logged.Add);
        _field.Setup(e => e.Visible).Returns(true);
        _field.Setup(e => e.Enabled).Returns(true);
        _field.Setup(e => e.InputValue).Returns(() => _value);
    }

    private sealed class Page(IHtmlTestContext context) : PageObjectBase<Page>(context)
    {
        public override bool IsLoaded() => true;

        public TextInputControl<Page> Password => new(this, Locator.ByAutomationId("Password"));
    }

    [Fact]
    public void SetSecret_FillsTheField_AndLogsOnlyStars()
    {
        _field.Setup(e => e.Fill(It.IsAny<string>())).Callback<string>(text => _value = text);

        new Page(_context.Object).Password.SetSecret(Secret);

        Assert.Equal(Secret, _value);
        Assert.NotEmpty(_logged);
        Assert.DoesNotContain(_logged, entry => entry.ToString().Contains(Secret, StringComparison.Ordinal));
    }

    [Fact]
    public void SetSecret_ThatDoesNotTake_FailsWithoutTheSecretInTheMessage()
    {
        _field.Setup(e => e.Fill(It.IsAny<string>())).Callback<string>(_ => _value = "something else");

        var error = Assert.ThrowsAny<Exception>(() => new Page(_context.Object).Password.SetSecret(Secret));

        Assert.DoesNotContain(Secret, error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(_logged, entry => entry.ToString().Contains(Secret, StringComparison.Ordinal));
    }

    [Fact]
    public void SetSecret_Empty_IsRefused()
        => Assert.Throws<ArgumentException>(() => new Page(_context.Object).Password.SetSecret(string.Empty));
}
