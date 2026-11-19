using Bruinet.Scraper.Factory;
using Bruinet.Scraper.Places;
using Bruinet.Scraper.Projects;

namespace Bruinet.Scraper.Tests.Service;

// A person's answers before the factory runs, what a kept change may touch, and the app cut into modules.
public sealed class FactorySetupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Without_a_file_every_answer_is_the_careful_one()
    {
        var setup = FactorySetup.Read(_root);

        Assert.Equal((WritesAllowed.ReadOnly, ImproveScope.Data, AskWhen.Batch, CommitWhen.No, SpecAcceptor.Person, 1), (setup.Writes, setup.Improve, setup.Ask, setup.Commit, setup.Specs, setup.Walkers));
        Assert.Empty(setup.Run);
        Assert.False(setup.RunsAll);
    }

    [Fact]
    public void A_setup_writes_its_answers_and_reads_them_back()
    {
        var setup = new FactorySetup().With("Writes", "own-records").With("Run", "Projects, Contacts").With("Improve", "suite").With("Ask", "blocked").With("Commit", "group").With("Specs", "agent").With("Walkers", "4");

        var read = FactorySetup.Parse(setup.ToMarkdown());

        Assert.Equal((WritesAllowed.OwnRecords, ImproveScope.Suite, AskWhen.Blocked, CommitWhen.Group, SpecAcceptor.Agent, 4), (read.Writes, read.Improve, read.Ask, read.Commit, read.Specs, read.Walkers));
        Assert.Equal(["Projects", "Contacts"], read.Run);
        Assert.True(new FactorySetup().With("run", "all").RunsAll);
    }

    [Theory]
    [InlineData("Writes", "anything", "Writes is one of read-only, own-records, free")]
    [InlineData("Walkers", "0", "Walkers is a number from 1 to 16")]
    [InlineData("Colour", "blue", "'Colour' is not a question of the setup")]
    public void An_answer_that_is_none_of_its_question_s_is_said(string question, string answer, string says) =>
        Assert.Contains(says, Assert.Throws<FormatException>(() => new FactorySetup().With(question, answer)).Message, StringComparison.Ordinal);

    [Fact]
    public void What_a_kept_change_may_touch_follows_the_setup()
    {
        var project = MakeApp();
        var config = Path.Combine(project.Home, ScraperConfig.FileName);
        var signs = Path.Combine(_root, "Shop.Controls", "control-signs.md");
        var control = Path.Combine(_root, "Shop.Controls", "Select.cs");
        var page = Path.Combine(project.Folder, "Pages", "ContactsPage.cs");
        var capture = Path.Combine(project.RecordingsFolder, "walk", "001.nodes.jsonl");
        var engine = Path.Combine(_root, "bruinet", "src", "Walker.cs");

        string[] all = [config, signs, control, page, capture, engine];
        HashSet<string> Refused(ImproveScope scope) => [.. ChangeScope.Refused(scope, project, all).Select(file => Path.GetFileName(file))];

        Assert.Equal(6, Refused(ImproveScope.Off).Count);
        // The app's data: its config and a library's signs, and never a capture.
        Assert.Equal(["001.nodes.jsonl", "ContactsPage.cs", "Select.cs", "Walker.cs"], Refused(ImproveScope.Data).Order(StringComparer.Ordinal));
        // The suite's and a control library's code too; a capture is in the Scraper's own place.
        Assert.Equal(["Walker.cs"], Refused(ImproveScope.Suite));
        Assert.Empty(Refused(ImproveScope.Bruinet));
    }

    [Fact]
    public void An_app_is_cut_into_modules_by_its_config_and_plain_ones_come_first()
    {
        var project = MakeApp();
        var list = Shop();

        var modules = ModuleCut.Of(list, project.Config, Owed.Of(list, new SuiteSource("", new HashSet<string>(StringComparer.Ordinal))));

        Assert.Equal(["About", "Sales", "Contacts"], modules.Select(module => module.Name));
        var sales = modules[1];
        Assert.Equal(["/invoice", "/orders"], sales.Groups);
        Assert.Equal((2, 2), (sales.Pages, sales.Order));
        // A page of one record asks most: its module comes last.
        Assert.Contains(PlaceGroupKind.RecordPage, modules[2].Kinds);
        Assert.Equal(2, modules[2].Owed);
    }

    [Fact]
    public void What_a_person_wrote_in_the_modules_file_is_kept_when_the_cut_is_made_again()
    {
        var project = MakeApp();
        var list = Shop();
        var fresh = ModuleCut.Of(list, project.Config, Owed.Of(list, new SuiteSource("", new HashSet<string>(StringComparer.Ordinal))));
        var written = ModuleCut.ToMarkdown(fresh)
            .Replace("| 3 | Contacts | yes |", "| 0 | Contacts | yes |", StringComparison.Ordinal)
            .Replace("| 1 | About | yes |", "| 1 | About | no |", StringComparison.Ordinal);

        var kept = ModuleCut.Keep(fresh, ModuleCut.Parse(written));

        Assert.Equal(["Contacts", "About", "Sales"], kept.Select(module => module.Name));
        Assert.Equal([1, 2, 3], kept.Select(module => module.Order));
        Assert.Equal(ModuleDo.No, kept[1].Do);
        Assert.Equal(["Contacts", "Sales"], ModuleCut.ToRun(kept).Select(module => module.Name));
        // What the tool counts is written again, not read from the file.
        Assert.Equal(2, kept[0].Pages);
    }

    [Fact]
    public void A_module_that_says_later_runs_after_those_that_say_yes()
    {
        var modules = ModuleCut.Parse("## Modules\n\n| Order | Module | Do |\n| --- | --- | --- |\n| 1 | A | later |\n| 2 | B | yes |\n| 3 | C | no |\n");

        Assert.Equal(["B", "A"], ModuleCut.ToRun(modules).Select(module => module.Name));
        Assert.Contains("Do is yes, later or no", Assert.Throws<FormatException>(() => ModuleCut.Parse("## Modules\n\n| Order | Module | Do |\n| --- | --- | --- |\n| 1 | A | maybe |\n")).Message, StringComparison.Ordinal);
    }

    // The Scraper's own project beside its suite, with a control library that has a signs file.
    private ScrapeProject MakeApp()
    {
        var suite = Directory.CreateDirectory(Path.Combine(_root, "Shop.UITests")).FullName;
        var scraper = Directory.CreateDirectory(Path.Combine(_root, "Shop.Scraper")).FullName;
        Directory.CreateDirectory(Path.Combine(_root, "Shop.Controls"));
        File.WriteAllText(Path.Combine(suite, "Shop.UITests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(_root, "Shop.Controls", "control-signs.md"), "## Signs\n\n| Control | Sign | State |\n| --- | --- | --- |\n| Select | .select | made |\n");
        File.WriteAllText(Path.Combine(scraper, ScraperConfig.FileName), """
            # Scraper Config

            ## App

            | Field | Value |
            | --- | --- |
            | Platform | html |
            | StartUrl | https://shop.example/ |
            | Suite | ../Shop.UITests |

            ## Modules

            | Path | Pages | Namespace |
            | --- | --- | --- |
            | /invoice | Modules/Sales/Pages | Shop.Modules.Sales.Pages |
            | /orders | Modules/Sales/Pages | Shop.Modules.Sales.Pages |
            | /contact | Modules/Contacts/Pages | Shop.Modules.Contacts.Pages |

            ## Control libraries

            | Class prefix | Library | Signs |
            | --- | --- | --- |
            | s- | Shop.Controls | ../Shop.Controls/control-signs.md |
            """);
        return ScrapeProject.Open(scraper);
    }

    private static PlacesList Shop()
    {
        var list = new PlacesList();
        foreach (var place in new[]
        {
            new Place { Key = "/about", Kind = PlaceKind.Page, State = PlaceState.Known, Address = "/about" },
            new Place { Key = "/invoice", Kind = PlaceKind.Page, State = PlaceState.Seen, Address = "/invoice", Columns = ["Number"] },
            new Place { Key = "/orders", Kind = PlaceKind.Page, State = PlaceState.Seen, Address = "/orders", Columns = ["Number"] },
            new Place { Key = "/contact", Kind = PlaceKind.Page, State = PlaceState.Seen, Address = "/contact", Columns = ["Name"] },
            new Place { Key = "/contact#/view/{id}", Kind = PlaceKind.Page, State = PlaceState.Seen, Address = "/contact#/view/7" },
        })
        {
            list.Add(place);
        }

        return list;
    }
}
