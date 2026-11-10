using Bruinet.Scraper.Snapshots;
using Bruinet.Scraper.Projects;

namespace Bruinet.Scraper.Tests.Testing;

// The proposal's User Form example (.my/recording/proposal.md, section 2): a project with its page object
// and a session with the page's capture, for the actions a test adds.
internal static class UserForm
{
    public static (ScrapeProject Project, RecordingSession Session) Make(string root, string config = "")
    {
        var folder = Path.Combine(root, "Sample.UITests");
        Directory.CreateDirectory(Path.Combine(folder, "PageObjects"));
        File.WriteAllText(Path.Combine(folder, "Sample.UITests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(folder, ScraperConfig.FileName), "## App\n\n| Field | Value |\n| --- | --- |\n| StartUrl | http://localhost:5180/ |\n" + config);
        File.WriteAllText(Path.Combine(folder, "PageObjects", "UserFormPage.cs"), """
            // <scraped page="/userform" from="x/001.nodes.jsonl" hash="1" />
            namespace Sample.UITests.PageObjects;

            public sealed class UserFormPage(IHtmlTestContext context) : BlazorPageObjectBase<UserFormPage>(context)
            {
                public TextInputControl<UserFormPage> FirstName => new(this, Locator.ByCss("[data-automation-id='FirstNameInput']"));

                public TextInputControl<UserFormPage> Password => new(this, Locator.ByCss("[data-automation-id='PasswordInput']"));

                public CheckBoxControl<UserFormPage> Terms => new(this, Locator.ByCss("[data-automation-id='TermsCheckbox']"));

                public SelectControl<UserFormPage> Country => new(this, Locator.ByCss("[data-automation-id='CountrySelect']"));

                public ButtonControl<UserFormPage> Submit => new(this, Locator.ByCss("[data-automation-id='SubmitButton']"));

                public FiltersPanel<UserFormPage> Filters => new(this, Locator.ByCss("[data-testid='filters']"));

                public MysteryWidget<UserFormPage> Mystery => new(this, Locator.ByCss("[data-testid='mystery']"));
            }

            public sealed class FiltersPanel<TParent>(TParent parent, Locator locator) : ContainerBase<TParent, FiltersPanel<TParent>>(parent, locator)
            {
                public SelectControl<FiltersPanel<TParent>> Status => new(this, Locator.ByCss("[data-testid='status']"));
            }
            """);
        var project = ScrapeProject.Open(folder);
        var session = project.StartSession("User form", DateTimeOffset.UnixEpoch);
        session.AddPage(new ScrapeSnapshot
        {
            Platform = "html",
            Location = "http://localhost:5180/userform",
            Title = "User form",
            CapturedAt = DateTimeOffset.UnixEpoch,
            Nodes =
            [
                Node(1, 0, "html"),
                Node(2, 1, "body"),
                Node(3, 2, "form"),
                Node(4, 3, "input", ("data-automation-id", "FirstNameInput")),
                Node(5, 3, "input", ("data-automation-id", "PasswordInput"), ("type", "password")),
                Node(6, 3, "label"),
                Node(7, 6, "input", ("data-automation-id", "TermsCheckbox"), ("type", "checkbox")),
                Node(8, 3, "select", ("data-automation-id", "CountrySelect")),
                Node(9, 8, "option"),
                Node(10, 3, "button", ("data-automation-id", "SubmitButton")),
                Node(11, 10, "span"),
                Node(12, 2, "p"),
                Node(13, 2, "div", ("data-testid", "filters")),
                Node(14, 13, "select", ("data-testid", "status")),
                Node(15, 2, "div", ("data-testid", "mystery")),
            ],
        });
        return (project, session);
    }

    private static ScrapeNode Node(int number, int parent, string type, params (string Name, string Value)[] attributes) => new()
    {
        Number = number,
        Parent = parent,
        Type = type,
        Attributes = attributes.ToDictionary(a => a.Name, a => a.Value),
    };
}
