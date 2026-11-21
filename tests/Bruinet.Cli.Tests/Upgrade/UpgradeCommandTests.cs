using Bruinet.Cli;

namespace Bruinet.Cli.Tests.Upgrade;

// bruinet upgrade: the rename rows of the changes list applied to a project that uses Bruinet.
public sealed class UpgradeCommandTests : IDisposable
{
    private const string Changes = """
        # Bruinet API changes

        ## Renames

        | Commit | Kind | Old | New |
        | --- | --- | --- | --- |
        | Later | type | `OldGate` | `NewGate` |
        | ApiFamily | package | `Bruinet.Mocking.Http` | `Bruinet.Api.Replay` |
        | ApiFamily | namespace | `Bruinet.Mocking` | `Bruinet.Api.Mocking` |
        | ApiFamily | keep | `Bruinet.Mocking.Data` | |
        | Earlier | namespace | `Bruinet.Old` | `Bruinet.New` |

        ## Made

        | Commit | Change | Find | Write instead |
        | --- | --- | --- | --- |
        | ApiFamily | prose | x | y |
        """;

    private readonly string _folder = Directory.CreateTempSubdirectory("bruinet-upgrade-").FullName;

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
        File.Delete(_folder + "-changes.md");
    }

    [Fact]
    public async Task Upgrade_RewritesWholeNames_LongestFirst_AndLeavesAKeptOne()
    {
        var code = Write("Tests/StubTests.cs", """
            using Bruinet.Mocking;
            using Bruinet.Mocking.Http;
            using Bruinet.Mocking.Steps;
            using Bruinet.Mocking.Data;
            using NotBruinet.Mocking;

            // MyBruinet.Mocking and Bruinet.MockingBird stay.
            var gate = new OldGate(); var other = new OldGateway();
            var options = new Bruinet.Mocking.Http.HttpReplayOptions();
            """);
        var project = Write("Tests/Tests.csproj", """
            <Project>
              <PackageReference Include="Bruinet.Mocking" />
              <ProjectReference Include="..\..\src\Bruinet.Mocking.Http\Bruinet.Mocking.Http.csproj" />
              <PackageReference Include="Bruinet.Mocking.Data" />
            </Project>
            """);
        var config = Write("Tests/bdd.config.md", "| Using | Bruinet.Mocking.Steps |\n");
        Write("Tests/bin/Debug/Old.cs", "using Bruinet.Mocking;");
        Write("bruinet/.git", "gitdir: elsewhere");
        Write("bruinet/src/Stub.cs", "namespace Bruinet.Mocking;");

        var run = await Run("upgrade", _folder, "--changes", ChangesFile(), "--no-build");

        Assert.Equal(0, run.Exit);
        Assert.Equal("""
            using Bruinet.Api.Mocking;
            using Bruinet.Api.Replay;
            using Bruinet.Api.Mocking.Steps;
            using Bruinet.Mocking.Data;
            using NotBruinet.Mocking;

            // MyBruinet.Mocking and Bruinet.MockingBird stay.
            var gate = new NewGate(); var other = new OldGateway();
            var options = new Bruinet.Api.Replay.HttpReplayOptions();
            """, File.ReadAllText(code));
        Assert.Equal("""
            <Project>
              <PackageReference Include="Bruinet.Api.Mocking" />
              <ProjectReference Include="..\..\src\Bruinet.Api.Replay\Bruinet.Api.Replay.csproj" />
              <PackageReference Include="Bruinet.Mocking.Data" />
            </Project>
            """, File.ReadAllText(project));
        Assert.Equal("| Using | Bruinet.Api.Mocking.Steps |\n", File.ReadAllText(config));
        Assert.Equal("using Bruinet.Mocking;", File.ReadAllText(Path.Combine(_folder, "Tests/bin/Debug/Old.cs")));
        Assert.Equal("namespace Bruinet.Mocking;", File.ReadAllText(Path.Combine(_folder, "bruinet/src/Stub.cs")));
        Assert.Contains("Tests/StubTests.cs: Bruinet.Mocking.Http -> Bruinet.Api.Replay (2)", run.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_SaysWhatWouldChange_ChangesNothing_AndExitsOne()
    {
        var code = Write("A.cs", "using Bruinet.Mocking;");

        var check = await Run("upgrade", _folder, "--changes", ChangesFile(), "--check");
        var again = await Run("upgrade", _folder, "--changes", ChangesFile(), "--no-build");
        var done = await Run("upgrade", _folder, "--changes", ChangesFile(), "--check");

        Assert.Equal(1, check.Exit);
        Assert.Contains("A.cs: Bruinet.Mocking -> Bruinet.Api.Mocking", check.Output, StringComparison.Ordinal);
        Assert.Equal(0, again.Exit);
        Assert.Equal("using Bruinet.Api.Mocking;", File.ReadAllText(code));
        Assert.Equal(0, done.Exit);
        Assert.Equal(string.Empty, done.Output.Trim());
    }

    [Fact]
    public async Task From_AppliesOnlyTheRenamesMadeAfterAChange()
    {
        var code = Write("A.cs", "using Bruinet.Mocking; using Bruinet.Old; OldGate gate;");

        var run = await Run("upgrade", _folder, "--changes", ChangesFile(), "--from", "ApiFamily", "--no-build");
        var unknown = await Run("upgrade", _folder, "--changes", ChangesFile(), "--from", "Nowhere", "--no-build");

        Assert.Equal(0, run.Exit);
        Assert.Equal("using Bruinet.Mocking; using Bruinet.Old; NewGate gate;", File.ReadAllText(code));
        Assert.Equal(2, unknown.Exit);
    }

    [Fact]
    public async Task AFileKeepsItsByteOrderMark()
    {
        var path = Path.Combine(_folder, "Marked.cs");
        File.WriteAllText(path, "using Bruinet.Mocking;", new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        await Run("upgrade", _folder, "--changes", ChangesFile(), "--no-build");

        Assert.Equal([0xEF, 0xBB, 0xBF], File.ReadAllBytes(path)[..3]);
        Assert.Equal("using Bruinet.Api.Mocking;", File.ReadAllText(path));
    }

    [Fact]
    public async Task TheToolCarriesTheChangesListItWasBuiltWith()
    {
        Write("A.cs", "class A;");

        var run = await Run("upgrade", _folder, "--check");

        Assert.Equal(0, run.Exit);
    }

    private string ChangesFile() => Write("../" + Path.GetFileName(_folder) + "-changes.md", Changes);

    private string Write(string relative, string text)
    {
        var path = Path.GetFullPath(Path.Combine(_folder, relative));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private async Task<(int Exit, string Output)> Run(params string[] args)
    {
        var output = new StringWriter();
        var exit = await BruinetCommand.RunAsync(args, new CliIo(TextReader.Null, output, TextWriter.Null, _folder, Interactive: false));
        return (exit, output.ToString());
    }
}
