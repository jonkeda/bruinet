using Xunit;

namespace Bruinet.Bdd.Tests;

public sealed class BddConfigWriterTests
{
    private const string SampleConfig = """
        # UAT Config

        ## Runtime

        | Field | Value |
        | --- | --- |
        | Target | MAUI |
        | Fixture | TodoUatFixture |

        ## Assemblies

        | Kind | Assembly |
        | --- | --- |
        | Pages | Bruinet.Samples.Todo.Uat.dll |
        | Controls | Bruinet.Maui.dll |

        ## Discovery

        | Field | Value |
        | --- | --- |
        | AllowNameInference | true |
        """;

    [Fact]
    public void Apply_EmptyEdit_ReturnsInputUnchanged()
    {
        Assert.Equal(SampleConfig, BddConfigWriter.Apply(SampleConfig, BddConfigEdit.Empty));
    }

    [Fact]
    public void Apply_ChangedField_RewritesOnlyThatLine()
    {
        var edit = BddConfigEdit.Empty.WithField("Runtime", "Fixture", "TodoAppFixture");

        var updated = BddConfigWriter.Apply(SampleConfig, edit);

        Assert.Equal("TodoAppFixture", BddConfigParser.Parse(updated).Runtime["Fixture"]);
        Assert.Equal(
            ["| Fixture | TodoUatFixture |"],
            ChangedLines(SampleConfig, updated).Removed);
        Assert.Equal(
            ["| Fixture | TodoAppFixture |"],
            ChangedLines(SampleConfig, updated).Added);
    }

    [Fact]
    public void Apply_SameValue_ReturnsInputUnchanged()
    {
        var edit = BddConfigEdit.Empty.WithField("Runtime", "Target", "MAUI");

        Assert.Equal(SampleConfig, BddConfigWriter.Apply(SampleConfig, edit));
    }

    [Fact]
    public void Apply_NullValue_RemovesTheRowAndLeavesNoBlankLine()
    {
        var edit = BddConfigEdit.Empty.WithField("Runtime", "Target", null);

        var updated = BddConfigWriter.Apply(SampleConfig, edit);

        Assert.False(BddConfigParser.Parse(updated).Runtime.ContainsKey("Target"));
        Assert.DoesNotContain("\n\n\n", updated, StringComparison.Ordinal);
        Assert.Equal(["| Target | MAUI |"], ChangedLines(SampleConfig, updated).Removed);
        Assert.Empty(ChangedLines(SampleConfig, updated).Added);
    }

    [Fact]
    public void Apply_NewField_AppendsToItsSection()
    {
        var edit = BddConfigEdit.Empty.WithField("Runtime", "Project", "./Todo.Uat.csproj");

        var updated = BddConfigWriter.Apply(SampleConfig, edit);

        Assert.Equal("./Todo.Uat.csproj", BddConfigParser.Parse(updated).Runtime["Project"]);
        Assert.Equal("MAUI", BddConfigParser.Parse(updated).Runtime["Target"]);
        Assert.Equal(["| Project | ./Todo.Uat.csproj |"], ChangedLines(SampleConfig, updated).Added);
    }

    [Fact]
    public void Apply_MissingSection_CreatesItInCanonicalOrder()
    {
        var edit = BddConfigEdit.Empty.WithField("Settings", "Root", "TestSettings");

        var updated = BddConfigWriter.Apply(SampleConfig, edit);

        Assert.Equal("TestSettings", BddConfigParser.Parse(updated).Settings.Root);
        Assert.True(
            updated.IndexOf("## Settings", StringComparison.Ordinal) >
            updated.IndexOf("## Discovery", StringComparison.Ordinal),
            "Settings should follow Discovery.");
    }

    [Fact]
    public void Apply_Rows_ReplacesTheTableContents()
    {
        var edit = BddConfigEdit.Empty.WithRows(
            "Assemblies",
            ["Kind", "Assembly"],
            [Cells(("Kind", "Pages"), ("Assembly", "Other.dll"))]);

        var config = BddConfigParser.Parse(BddConfigWriter.Apply(SampleConfig, edit));

        Assert.Equal("Other.dll", Assert.Single(config.Assemblies).Assembly);
    }

    [Fact]
    public void Apply_EmptyRows_ClearsTheTableButKeepsTheSection()
    {
        var edit = BddConfigEdit.Empty.WithRows("Assemblies", ["Kind", "Assembly"], []);

        var updated = BddConfigWriter.Apply(SampleConfig, edit);

        Assert.Empty(BddConfigParser.Parse(updated).Assemblies);
        Assert.Contains("## Assemblies", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_UnknownSectionsAndProse_Survive()
    {
        var markdown = """
            # UAT Config

            Some prose a person wrote, which must survive.

            ## Runtime

            | Field | Value |
            | --- | --- |
            | Target | WPF |

            ## Notes

            A section no parser knows about.
            """;

        var updated = BddConfigWriter.Apply(
            markdown,
            BddConfigEdit.Empty.WithField("Runtime", "Target", "WINFORMS"));

        Assert.Contains("Some prose a person wrote, which must survive.", updated, StringComparison.Ordinal);
        Assert.Contains("## Notes", updated, StringComparison.Ordinal);
        Assert.Contains("A section no parser knows about.", updated, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\r\n", true)]
    [InlineData("\r\n", false)]
    [InlineData("\n", true)]
    [InlineData("\n", false)]
    public void Apply_KeepsLineEndingsAndTrailingNewline(string newLine, bool trailingNewLine)
    {
        var markdown = string.Join(
            newLine,
            "## Runtime",
            string.Empty,
            "| Field | Value |",
            "| --- | --- |",
            "| Target | WPF |") + (trailingNewLine ? newLine : string.Empty);

        var updated = BddConfigWriter.Apply(
            markdown,
            BddConfigEdit.Empty.WithField("Runtime", "Target", "HTML"));

        Assert.Equal(trailingNewLine, updated.EndsWith(newLine, StringComparison.Ordinal));
        Assert.Equal(newLine == "\r\n", updated.Contains("\r\n", StringComparison.Ordinal));
        Assert.Equal("HTML", BddConfigParser.Parse(updated).Runtime["Target"]);
    }

    [Fact]
    public void Apply_PaddedTable_KeepsItsColumnWidths()
    {
        var markdown = """
            ## Runtime

            | Field    | Value          |
            | -------- | -------------- |
            | Target   | MAUI           |
            | Fixture  | TodoUatFixture |
            """;

        var updated = BddConfigWriter.Apply(
            markdown,
            BddConfigEdit.Empty.WithField("Runtime", "Target", "WPF"));

        Assert.Contains("| Target   | WPF            |", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_WritesMinimalConfigThatParses()
    {
        var markdown = BddConfigWriter.Create(new BddConfigTemplate("./Todo.Uat.csproj", "TodoUatFixture"));

        var config = BddConfigParser.Parse(markdown);

        Assert.Equal("./Todo.Uat.csproj", config.Runtime["Project"]);
        Assert.Equal("TodoUatFixture", config.Runtime["Fixture"]);
        Assert.False(config.Runtime.ContainsKey("Target"));
        Assert.Empty(config.Assemblies);
    }

    [Fact]
    public void Create_WithoutFixture_LeavesItOut()
    {
        var config = BddConfigParser.Parse(BddConfigWriter.Create(new BddConfigTemplate("./Todo.Uat.csproj")));

        Assert.False(config.Runtime.ContainsKey("Fixture"));
    }

    /// <summary>
    /// The property that keeps hand-written configs hand-editable: a no-op edit is byte-identical,
    /// and a real edit changes one line, on every config actually checked in.
    /// </summary>
    [Theory]
    [MemberData(nameof(RepositoryConfigs))]
    public void Apply_RealConfig_IsByteIdenticalForANoOpAndOneLineForAnEdit(string configPath)
    {
        var markdown = File.ReadAllText(configPath);

        Assert.Equal(markdown, BddConfigWriter.Apply(markdown, BddConfigEdit.Empty));

        var updated = BddConfigWriter.Apply(
            markdown,
            BddConfigEdit.Empty.WithField("Runtime", "Fixture", "AReplacementFixture"));

        Assert.Equal("AReplacementFixture", BddConfigParser.Parse(updated).Runtime["Fixture"]);

        var changes = ChangedLines(markdown, updated);
        Assert.Single(changes.Added);
        Assert.True(changes.Removed.Count <= 1, "An edit should touch at most one existing line.");
    }

    public static TheoryData<string> RepositoryConfigs()
    {
        TheoryData<string> data = [];
        var root = FindRepositoryRoot();
        if (root is null)
        {
            return data;
        }

        foreach (var path in Directory.EnumerateFiles(root, "bdd.config.md", SearchOption.AllDirectories))
        {
            if (!path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                data.Add(path);
            }
        }

        return data;
    }

    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bruinet.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName;
    }

    private static Dictionary<string, string> Cells(params (string Column, string Value)[] cells)
    {
        Dictionary<string, string> row = new(StringComparer.OrdinalIgnoreCase);
        foreach (var (column, value) in cells)
        {
            row[column] = value;
        }

        return row;
    }

    private static (IReadOnlyList<string> Added, IReadOnlyList<string> Removed) ChangedLines(
        string before,
        string after)
    {
        var beforeLines = Lines(before);
        var afterLines = Lines(after);

        return (
            [.. afterLines.Where(line => !beforeLines.Remove(line))],
            [.. Lines(before).Where(line => !Lines(after).Remove(line))]);

        static List<string> Lines(string text) =>
            [.. text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(line => line.Trim())];
    }
}
