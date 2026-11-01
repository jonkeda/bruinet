using Xunit;

namespace Bruinet.Bdd.LanguageServer.Tests;

// The MCP server's coverage: a cobertura report's lines per file, as ranges.
public sealed class CoverageReportTests : IDisposable
{
    private readonly string _report = Path.Combine(Path.GetTempPath(), $"coverage-{Guid.NewGuid():N}.cobertura.xml");

    public void Dispose() => File.Delete(_report);

    [Fact]
    public void ARun_GivesEachFilesLines_RunOrNot_ALineInTwoClassesRunWhenEitherRanIt()
    {
        File.WriteAllText(_report, """
            <coverage>
              <sources><source>C:\app\</source></sources>
              <packages><package name="App"><classes>
                <class name="TodoStore" filename="Core\TodoStore.cs"><lines>
                  <line number="42" hits="1" /><line number="43" hits="2" /><line number="51" hits="0" />
                </lines></class>
                <class name="TodoStore+Load" filename="Core\TodoStore.cs"><lines>
                  <line number="51" hits="3" /><line number="52" hits="0" />
                </lines></class>
              </classes></package></packages>
            </coverage>
            """);

        var lines = CoverageReport.Read(_report)[Path.GetFullPath(@"C:\app\Core\TodoStore.cs")];

        Assert.Equal("42-43, 51", CoverageReport.Ranges(lines.Where(pair => pair.Value).Select(pair => pair.Key)));
        Assert.Equal("52", CoverageReport.Ranges(lines.Where(pair => !pair.Value).Select(pair => pair.Key)));
    }

    [Fact]
    public void ARelativeName_IsFoundUnderTheSourceItExistsIn()
    {
        var second = Directory.CreateTempSubdirectory("coverage-source-").FullName;
        File.WriteAllText(Path.Combine(second, "Store.cs"), "class Store {}");
        try
        {
            File.WriteAllText(_report, $"""
                <coverage>
                  <sources><source>C:\nowhere\</source><source>{second}\</source></sources>
                  <packages><package name="App"><classes>
                    <class name="Store" filename="Store.cs"><lines><line number="1" hits="1" /></lines></class>
                  </classes></package></packages>
                </coverage>
                """);

            Assert.True(CoverageReport.Read(_report).ContainsKey(Path.Combine(second, "Store.cs")));
        }
        finally
        {
            Directory.Delete(second, recursive: true);
        }
    }

    [Fact]
    public void Ranges_JoinRunsOfLines_AndAreNullForNone()
    {
        Assert.Equal("1-3, 7, 9-10", CoverageReport.Ranges([10, 1, 2, 3, 7, 9]));
        Assert.Null(CoverageReport.Ranges([]));
    }

    [Theory]
    [InlineData("src/App/Pages/Todo.razor", true)]
    [InlineData("src/App/TodoStore.cs", true)]
    [InlineData("src/App/Pages/TodoPage.xaml", false)]
    [InlineData("docs/readme.md", false)]
    public void ASource_IsCodeAReportCanName(string path, bool source)
        => Assert.Equal(source, CoverageReport.IsSource(path));
}
