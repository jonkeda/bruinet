using Bruinet.Cli;
using System.Text.Json;
using Xunit;

namespace Bruinet.DataValidator.Tests;

public sealed class DataValidatorAppTests : IDisposable
{
    private static readonly string Assembly = typeof(ShelfTestDataDefinition).Assembly.Location;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "Bruinet", "DataValidatorTests", Guid.NewGuid().ToString("N"));

    public DataValidatorAppTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Validate_OnGoodData_ExitsZero()
    {
        var (exit, output, _) = Run("validate", "--assembly", Assembly);

        Assert.Equal(0, exit);
        Assert.Contains("No problems in 1 test set(s).", output);
    }

    [Fact]
    public void Validate_OnABrokenFile_PrintsFileLineAndCode()
    {
        var file = Write("broken.json", """
            {
              "books": [
                { "id": 1, "titel": "First", "genre": "Novel" }
              ]
            }
            """);

        var (exit, output, _) = Run("validate", file, "--assembly", Assembly);

        Assert.Equal(1, exit);
        Assert.Contains("broken.json:3: DATA001", output);
        Assert.Contains("Did you mean 'title'?", output);
    }

    [Fact]
    public void Validate_AsJson_GivesOneObjectPerProblem()
    {
        var file = Write("broken.json", """{ "books": [ { "id": 1, "title": "A", "genre": "Drama" } ] }""");

        var (exit, output, _) = Run("validate", file, "--assembly", Assembly, "--json");

        Assert.Equal(1, exit);
        var problem = Assert.Single(JsonDocument.Parse(output).RootElement.EnumerateArray());
        Assert.Equal("DATA003", problem.GetProperty("code").GetString());
        Assert.Contains("Novel, Poetry", problem.GetProperty("message").GetString());
    }

    [Fact]
    public void Validate_WithAConfig_ChecksTheTestSetsTheScenariosName()
    {
        WriteConfig(defaultTestData: "good");
        Write(Path.Combine("Features", "shelf.feature.md"), """
            # Feature: Shelf

            ## Metadata

            | Field | Value |
            | --- | --- |
            | TestData | god |

            ## Scenario: Nothing

            ```gherkin
            Given I am on the Shelf page
            ```
            """);

        var (exit, output, _) = Run("validate");

        Assert.Equal(1, exit);
        Assert.Contains("Features/shelf.feature.md:7: DATA020", output);
        Assert.Contains("Did you mean 'good'?", output);
        Assert.DoesNotContain("uat.config.md", output);
    }

    [Fact]
    public void List_ShowsCountsAndWhatUsesEachTestSet()
    {
        WriteConfig(defaultTestData: "good");

        var (exit, output, _) = Run("list");

        Assert.Equal(0, exit);
        Assert.Matches(@"good\s+books 2\s+used by: uat\.config\.md", output);
    }

    [Fact]
    public void Describe_ShowsTheFields()
    {
        var (exit, output, _) = Run("describe", "book", "--assembly", Assembly);

        Assert.Equal(0, exit);
        Assert.Matches(@"title\s+string\s+required", output);
        Assert.Matches(@"genre\s+Genre\s+required, Novel \| Poetry", output);
    }

    [Fact]
    public void Describe_AnUnknownType_ListsTheKnownOnes()
    {
        var (exit, _, errors) = Run("describe", "Magazine", "--assembly", Assembly);

        Assert.Equal(2, exit);
        Assert.Contains("Book", errors);
    }

    [Fact]
    public void Schema_Check_PassesForTheCommittedSchema()
    {
        var (exit, output, _) = Run("schema", "--check", "--assembly", Assembly);

        Assert.True(exit == 0, output);
    }

    [Fact]
    public void AMissingAssembly_IsReportedWithoutAStackTrace()
    {
        var (exit, _, errors) = Run("validate", "--assembly", Path.Combine(_folder, "Nope.dll"));

        Assert.Equal(2, exit);
        Assert.StartsWith("error: ", errors);
        Assert.DoesNotContain(" at ", errors);
    }

    [Fact]
    public void NoConfigAndNoAssembly_SaysWhatToDo()
    {
        var (exit, _, errors) = Run("validate");

        Assert.Equal(2, exit);
        Assert.Contains("--assembly", errors);
    }

    private (int Exit, string Output, string Errors) Run(params string[] args)
    {
        var output = new StringWriter();
        var errors = new StringWriter();
        var exit = BruinetCommand.RunAsync(["data", .. args], new CliIo(TextReader.Null, output, errors, _folder, Interactive: false)).GetAwaiter().GetResult();
        return (exit, output.ToString(), errors.ToString());
    }

    private string Write(string relativePath, string content)
    {
        var path = Path.Combine(_folder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private void WriteConfig(string defaultTestData) => Write("uat.config.md", $"""
        # UAT Config

        ## Runtime

        | Field | Value |
        | --- | --- |
        | DefaultTestData | {defaultTestData} |

        ## Assemblies

        | Kind | Assembly |
        | --- | --- |
        | TestData | {Assembly} |
        """);
}
