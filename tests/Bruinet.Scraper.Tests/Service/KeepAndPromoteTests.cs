using Bruinet.Scraper.Factory;

namespace Bruinet.Scraper.Tests.Service;

// What a change may not make worse, and a sign that moves from an app's config up to its library.
public sealed class KeepAndPromoteTests
{
    private const string Config = """
        # Scraper Config

        ## Control signs

        How the app's own elements are recognized.

        | Control | Sign | State |
        | --- | --- | --- |
        | RichTextEditor | .ck-editor | made |
        | DatePicker | a-date-picker | to make |
        | DatePicker | .b7-date | to make |

        ## Field holders

        | Holder | Sign |
        | --- | --- |
        | FormRow | .form-row-content |
        """;

    private const string Library = """
        # Control signs: Vuetify 2

        ## Signs

        | Control | Sign | State |
        | --- | --- | --- |
        | `VSelect` | `.v-select` | made |

        ## Place signs

        | What | Sign |
        | --- | --- |
        | tab | `.v-tab` |
        """;

    [Fact]
    public void A_control_s_signs_move_from_the_config_to_the_top_of_the_library_s_table()
    {
        var (config, library, moved) = SignMove.Move(Config, Library, "DatePicker");

        Assert.Equal(["| `DatePicker` | `a-date-picker` | to make |", "| `DatePicker` | `.b7-date` | to make |"], moved);
        Assert.DoesNotContain("DatePicker", config, StringComparison.Ordinal);
        Assert.Contains("| RichTextEditor | .ck-editor | made |", config, StringComparison.Ordinal);
        Assert.Contains("| FormRow | .form-row-content |", config, StringComparison.Ordinal);
        // The first row that fits decides: what an app found stands before what the library had.
        Assert.Contains("| --- | --- | --- |\n| `DatePicker` | `a-date-picker` | to make |\n| `DatePicker` | `.b7-date` | to make |\n| `VSelect` | `.v-select` | made |", library, StringComparison.Ordinal);
        Assert.Contains("| tab | `.v-tab` |", library, StringComparison.Ordinal);
    }

    [Fact]
    public void A_control_the_config_has_no_sign_for_and_a_file_without_a_table_are_said()
    {
        Assert.Contains("no sign for 'Nobody'", Assert.Throws<FormatException>(() => SignMove.Move(Config, Library, "Nobody")).Message, StringComparison.Ordinal);
        Assert.Contains("## Signs", Assert.Throws<FormatException>(() => SignMove.Move(Config, "# Nothing\n", "DatePicker")).Message, StringComparison.Ordinal);
        // A holder is no control sign: the table under another heading is not looked in.
        Assert.Throws<FormatException>(() => SignMove.Move(Config, Library, "FormRow"));
    }

    [Fact]
    public void A_change_is_worse_when_more_is_owed_or_fewer_fields_have_a_control()
    {
        var mark = new OwedCounts(PagesWithoutClass: 14, DialogFormsNotDrafted: 6, RecordPages: 6, FieldsWithControl: 959);

        Assert.Empty(mark.WorseThan(mark));
        Assert.Empty(new OwedCounts(12, 6, 5, 970).WorseThan(mark));
        Assert.Equal(["dialog forms not drafted: 7, was 6", "fields with a made control: 950, was 959"], new OwedCounts(14, 7, 6, 950).WorseThan(mark));
    }
}
