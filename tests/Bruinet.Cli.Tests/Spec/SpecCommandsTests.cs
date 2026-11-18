namespace Bruinet.Cli.Tests.Spec;

/// <summary>
/// <c>bruinet spec check</c> and <c>bruinet spec trace</c> as a person types them: paths relative to the folder they
/// run in, a folder searched for specs, and several result files after one <c>--results</c>.
/// </summary>
public sealed class SpecCommandsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "bruinet-spec-tests", Guid.NewGuid().ToString("N"));

    private const string Spec = """
        # Spec: ABC.01 Do a thing

        ## Rules

        | Id | Rule |
        | --- | --- |
        | ABC.01.R1 | One. |
        | ABC.01.R2 | Two. |
        """;

    private const string Code = """
        public sealed class ThingTests
        {
            [Fact]
            [Trait("Rule", "ABC.01.R1")]
            public void One_Holds()
            {
            }

            [Fact]
            [Trait("Rule", "ABC.01.R2")]
            public void Two_Holds()
            {
            }
        }
        """;

    private static string Trx(string test, string outcome) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <Results>
            <UnitTestResult testName="Ns.ThingTests.{test}" outcome="{outcome}" />
          </Results>
        </TestRun>
        """;

    public SpecCommandsTests()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "specs"));
        Directory.CreateDirectory(Path.Combine(_folder, "tests"));
        File.WriteAllText(Path.Combine(_folder, "specs", "ABC.01.spec.md"), Spec);
        File.WriteAllText(Path.Combine(_folder, "tests", "ThingTests.cs"), Code);
        File.WriteAllText(Path.Combine(_folder, "one.trx"), Trx("One_Holds", "Passed"));
        File.WriteAllText(Path.Combine(_folder, "two.trx"), Trx("Two_Holds", "Passed"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    private async Task<(int Exit, string Output, string Errors)> Run(params string[] args)
    {
        var output = new StringWriter();
        var errors = new StringWriter();
        var exit = await BruinetCommand.RunAsync(args, new CliIo(TextReader.Null, output, errors, _folder, Interactive: false));
        return (exit, output.ToString(), errors.ToString());
    }

    [Fact]
    public async Task Check_AFolder_ShowsEachSpecAsFoundUnderIt()
    {
        var (exit, output, _) = await Run("spec", "check", "specs");

        Assert.Equal(1, exit);
        Assert.StartsWith($"{Path.Join("specs", "ABC.01.spec.md")}: ", output, StringComparison.Ordinal);
        Assert.Contains("finding(s)", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_AFolderWithNoSpec_CouldNotRun()
    {
        var (exit, _, errors) = await Run("spec", "check", "tests");

        Assert.Equal(2, exit);
        Assert.Contains("No *.spec.md found.", errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Trace_ReachesEveryRule()
    {
        var (exit, output, _) = await Run("spec", "trace", Path.Join("specs", "ABC.01.spec.md"), "tests");

        Assert.Equal(0, exit);
        Assert.Contains("2 of 2 rules reached by a test, 0 finding(s)", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Trace_TakesSeveralResultsAfterOneOption()
    {
        var (exit, output, _) = await Run("spec", "trace", Path.Join("specs", "ABC.01.spec.md"), "tests", "--results", "one.trx", "two.trx");

        Assert.Equal(0, exit);
        Assert.Contains("ABC.01.R1: 1 test(s) [Passing]", output, StringComparison.Ordinal);
        Assert.Contains("2 of 2 rules reached by a test, 2 passing, 0 finding(s)", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Trace_AMissingPath_CouldNotRun()
    {
        var (exit, _, errors) = await Run("spec", "trace", "nothing.spec.md", "tests");

        Assert.Equal(2, exit);
        Assert.Contains("nothing.spec.md: not found", errors, StringComparison.Ordinal);
    }
}
