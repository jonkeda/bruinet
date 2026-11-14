using Bruinet.Scraper.Recordings;
using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Tests.Recordings;

/// <summary><c>scrape find</c>: a fact in a recorded page, shadow roots included, with where it is.</summary>
public sealed class RecordingSearchTests
{
    private static ScrapeSnapshot Hours() => new()
    {
        Platform = "html",
        Location = "https://start-acc.bouw7.nl/hour-log#/",
        Nodes =
        [
            new() { Number = 1, Parent = 0, Type = "html" },
            new() { Number = 2, Parent = 1, Type = "div", Attributes = new Dictionary<string, string> { ["class"] = "v-dialog form-row", ["data-cy"] = "dialog-hour-log-form" } },
            new() { Number = 3, Parent = 2, Type = "input", Attributes = new Dictionary<string, string> { ["placeholder"] = "First select a project", ["disabled"] = "disabled" } },
            new() { Number = 4, Parent = 1, Type = "b7-data-table-filter-preset-card" },
            new() { Number = 5, Parent = 4, Kind = ScrapeNodeKind.ShadowRoot },
            new() { Number = 6, Parent = 5, Type = "div", Text = "All projects", Attributes = new Dictionary<string, string> { ["class"] = "label" } },
        ],
    };

    [Fact]
    public void An_attribute_value_is_found_with_its_path()
    {
        var hit = Assert.Single(RecordingSearch.Find(Hours(), "first select", "run/002-hours.nodes.jsonl"));

        Assert.Equal("placeholder", hit.Field);
        Assert.Equal("First select a project", hit.Value);
        Assert.Equal(3, hit.Node);
        Assert.Equal("html > div.v-dialog.form-row[data-cy=dialog-hour-log-form] > input", hit.Path);
    }

    [Fact]
    public void Text_in_a_shadow_root_is_found_and_the_root_is_in_the_path()
    {
        var hit = Assert.Single(RecordingSearch.Find(Hours(), "All projects", "f"));

        Assert.Equal("text", hit.Field);
        Assert.Equal("html > b7-data-table-filter-preset-card > #shadow > div.label", hit.Path);
    }

    [Fact]
    public void A_folder_of_runs_is_searched_file_by_file_and_a_run_can_be_chosen()
    {
        var folder = Directory.CreateTempSubdirectory("recordings").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(folder, "run-1"));
            Directory.CreateDirectory(Path.Combine(folder, "run-2"));
            SnapshotFile.Write(Hours(), Path.Combine(folder, "run-1", "002-hours" + SnapshotFile.Extension));
            SnapshotFile.Write(Hours(), Path.Combine(folder, "run-2", "001-hours" + SnapshotFile.Extension));

            Assert.Equal(["run-1/002-hours.nodes.jsonl", "run-2/001-hours.nodes.jsonl"], RecordingSearch.Find(folder, "First select").Select(h => h.File));
            Assert.Single(RecordingSearch.Find(folder, "First select", run: "run-2"));
            Assert.Empty(RecordingSearch.Find(folder, "First select", run: "run-3"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
