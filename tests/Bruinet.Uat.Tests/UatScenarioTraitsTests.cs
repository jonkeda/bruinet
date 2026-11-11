using Bruinet.Traits;
using Xunit;

namespace Bruinet.Uat.Tests;

/// <summary>The traits a scenario's feature file gives its case, and the folder-wide format check.</summary>
public sealed class UatScenarioTraitsTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("bruinet-traits-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Of_GivesTheModuleFromArea_TheJourneysFromTheScenariosTags_AndATierFromPriority()
    {
        var traits = UatScenarioTraits.Of(
            new Dictionary<string, string> { ["Area"] = "Contacts", ["Journey"] = "CTS.06", ["Priority"] = "UI" },
            ["@CTS.06.1", "@CTS.06.5", "@maui", "@contacts"]);

        Assert.Equal(
            [
                new(TraitKeys.Module, "Contacts"),
                new(TraitKeys.Journey, "CTS.06.1"),
                new(TraitKeys.Journey, "CTS.06.5"),
                new(TraitKeys.Pyramid, TierNames.Ui),
                new(TraitKeys.Pyramid, TierNames.HermeticUi),
            ],
            traits);
    }

    [Fact]
    public void Of_FallsBackToTheFilesJourneys_AndAPriorityThatNamesNoTierGivesNone()
    {
        var traits = UatScenarioTraits.Of(
            new Dictionary<string, string> { ["Journey"] = "CTS.01, CTS.02", ["Priority"] = "Smoke" },
            ["@maui"]);

        Assert.Equal([new(TraitKeys.Journey, "CTS.01"), new(TraitKeys.Journey, "CTS.02")], traits);
    }

    [Fact]
    public void AddTo_LeavesWhatTheRunnersAttributesWrote()
    {
        Dictionary<string, List<string>> existing = new(StringComparer.OrdinalIgnoreCase) { [TraitKeys.Pyramid] = [TierNames.Live] };

        UatScenarioTraits.AddTo(existing, [new(TraitKeys.Pyramid, TierNames.Ui), new(TraitKeys.Module, "Contacts")]);

        Assert.Equal([TierNames.Live], existing[TraitKeys.Pyramid]);
        Assert.Equal(["Contacts"], existing[TraitKeys.Module]);
    }

    [Fact]
    public void Of_AFile_ReadsTheNamedScenario()
    {
        var file = Write("CTS.06-create.feature.md", Feature("Priority | Video", "@CTS.06.2"));

        Assert.Equal(
            [new(TraitKeys.Module, "Contacts"), new(TraitKeys.Journey, "CTS.06.2"), new(TraitKeys.Pyramid, TierNames.Video), new(TraitKeys.Category, TierNames.Video)],
            UatScenarioTraits.Of(file, "Create"));
        Assert.Empty(UatScenarioTraits.Of(file, "Missing"));
    }

    [Fact]
    public void GetFolderFiles_FindsANewFile_WithoutAList()
    {
        Write("a.feature.md", Feature("Priority | Smoke", "@A.1"));
        Write(Path.Combine("sub", "b.feature.md"), Feature("Priority | Smoke", "@B.1"));

        var files = FormatCheck.Files(_folder).Select(row => Path.GetFileName((string)row[0])).ToList();

        Assert.Equal(["a.feature.md", "b.feature.md"], files);
    }

    [Fact]
    public void OptOutReason_IsTheReasonField_AndATagWithoutOneFails()
    {
        var checkedFile = Write("checked.feature.md", Feature("Priority | Smoke", "@A.1"));
        var draft = Write("draft.feature.md", Feature("Tags | contacts, no-format-check |\n| FormatCheck | Draft: the steps are not written yet", "@A.1"));
        var unexplained = Write("unexplained.feature.md", Feature("Tags | @no-format-check", "@A.1"));

        Assert.Null(UatSpecFormatTestBase.OptOutReason(checkedFile));
        Assert.Equal("Draft: the steps are not written yet", UatSpecFormatTestBase.OptOutReason(draft));
        var failure = Assert.Throws<InvalidOperationException>(() => UatSpecFormatTestBase.OptOutReason(unexplained));
        Assert.Contains("FormatCheck", failure.Message, StringComparison.Ordinal);
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_folder, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static string Feature(string extraMetadataRow, string tag) => $"""
        # Feature: Create

        ## Metadata

        | Field | Value |
        |---|---|
        | Area | Contacts |
        | {extraMetadataRow} |

        {tag}
        ## Scenario: Create

        ```gherkin
        When I work
        ```
        """;

    private sealed class FormatCheck : UatSpecFormatTestBase
    {
        public static IEnumerable<object[]> Files(string folder) => GetFolderFiles(folder);
    }
}
