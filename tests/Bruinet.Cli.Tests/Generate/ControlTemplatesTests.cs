using Bruinet.Cli.Generate;

namespace Bruinet.Cli.Tests.Generate;

public sealed class ControlTemplatesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "bruinet-templates-tests", Guid.NewGuid().ToString("N"));

    public ControlTemplatesTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void A_scraped_projects_template_gets_its_generated_members_and_a_broken_one_an_error()
    {
        File.WriteAllText(Path.Combine(_folder, "Status.tpl.cs"), """
            using Bruinet.Html.Controls;
            using Bruinet.Html.Interfaces;

            namespace Shop.UITests.Controls;

            public partial class Status<TScope> : ViewBase<TScope> where TScope : IHtmlScope<TScope>
            {
                public Status(IHtmlScope<TScope> scope, string selectorOrId) : base(scope, selectorOrId) { }

                protected virtual string GetLabelCore(IHtmlElement element) => element.Text;
            }
            """);
        File.WriteAllText(Path.Combine(_folder, "Broken.tpl.cs"), "namespace Shop.UITests.Controls;\n");

        var problems = new ControlTemplates().Generate(_folder);

        var generated = File.ReadAllText(Path.Combine(_folder, "Status.gen.cs"));
        Assert.Contains("GetLabel(", generated);
        var problem = Assert.Single(problems, p => p.Severity == "error");
        Assert.EndsWith("Broken.tpl.cs", problem.File);
        Assert.False(File.Exists(Path.Combine(_folder, "Broken.gen.cs")));
    }
}
