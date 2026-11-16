using Bruinet.Uat;
using Xunit;

namespace Bruinet.Uat.Tests;

/// <summary>Scenarios run file by file, each file's as they are written.</summary>
public sealed class BddDocumentOrderTests
{
    private static readonly Dictionary<string, string[]> Files = new()
    {
        ["a.spec.md"] = ["A.R1 first", "A.R2 second", "A.R3 third"],
        ["b.spec.md"] = ["B.R1 first", "B.R2 second"],
    };

    private static IReadOnlyList<string> Names(string file) => Files[file];

    [Fact]
    public void Cases_go_by_file_then_by_their_place_in_it()
    {
        object?[][] cases =
        [
            ["b.spec.md", "B.R2 second"],
            ["a.spec.md", "A.R3 third"],
            ["a.spec.md", "A.R1 first"],
            ["b.spec.md", "B.R1 first"],
            ["a.spec.md", "A.R2 second"],
        ];

        var ordered = BddDocumentOrder.InDocumentOrder(cases, each => each, Names);

        Assert.Equal(["A.R1 first", "A.R2 second", "A.R3 third", "B.R1 first", "B.R2 second"], ordered.Select(each => (string)each[1]!));
    }

    [Fact]
    public void A_case_that_names_no_scenario_keeps_its_place_after_the_others()
    {
        object?[][] cases =
        [
            [42],
            ["a.spec.md", "A.R2 second"],
            [],
            ["a.spec.md", "A.R1 first"],
        ];

        var ordered = BddDocumentOrder.InDocumentOrder(cases, each => each, Names);

        Assert.Equal(["A.R1 first", "A.R2 second"], ordered.Take(2).Select(each => (string)each[1]!));
        Assert.Equal(42, ordered[2][0]);
        Assert.Empty(ordered[3]);
    }

    [Fact]
    public void A_scenario_the_file_does_not_have_goes_last_in_its_file()
    {
        object?[][] cases =
        [
            ["a.spec.md", "A.R9 gone"],
            ["a.spec.md", "A.R1 first"],
        ];

        var ordered = BddDocumentOrder.InDocumentOrder(cases, each => each, Names);

        Assert.Equal(["A.R1 first", "A.R9 gone"], ordered.Select(each => (string)each[1]!));
    }
}
