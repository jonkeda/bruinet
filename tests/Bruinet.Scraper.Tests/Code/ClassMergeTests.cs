using Bruinet.Scraper.Code;

namespace Bruinet.Scraper.Tests.Code;

// A draft merged into the class that is there: the page's members are the draft's, what only the class has stays.
public sealed class ClassMergeTests
{
    private const string Existing = """
        // <scraped page="/contact › + Contact" variant="form" from="walk/001.nodes.jsonl" hash="aaaa1111" />
        using Shop.Components;

        namespace Shop.Pages;

        /// <summary>The contact form.</summary>
        public sealed class ContactDialog(ContactsPage page) : Dialog<ContactsPage, ContactDialog>(page)
        {
            /// <summary>The name, as a person wrote it down.</summary>
            public TextRow<ContactDialog> Name => new(this, "Naam");

            public SelectRow<ContactDialog> Country => new(this, "Country");

            /// <summary>Fills in what a new contact needs.</summary>
            public ContactDialog FillIn(string name)
            {
                Name.SetText(name);
                return this;
            }
        }

        public sealed class Other
        {
            public int Name => 1;
        }
        """;

    private const string Draft = """
        // <scraped page="/contact › + Contact" variant="form" from="walk/007.nodes.jsonl" hash="bbbb2222" />
        using Shop.Components;
        using Shop.Controls;

        namespace Shop.Pages;

        /// <summary>Drafted.</summary>
        public sealed partial class ContactDialog(ContactsPage page) : Dialog<ContactsPage, ContactDialog>(page)
        {
            public TextRow<ContactDialog> Name => new(this, "Name");

            public SelectRow<ContactDialog> Country => new(this, "Country");

            public DateRow<ContactDialog> Since => new(this, "Customer since");
        }
        """;

    [Fact]
    public void A_member_of_the_draft_replaces_the_class_s_own_or_is_added_and_what_only_the_class_has_stays()
    {
        var merged = ClassMerge.Merge(Existing, Draft, "ContactDialog");

        Assert.Equal(["Since"], merged.Added);
        Assert.Equal(["Name"], merged.Replaced);
        Assert.Equal(["FillIn"], merged.Kept);
        Assert.Contains("public TextRow<ContactDialog> Name => new(this, \"Name\");", merged.Code, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Naam\"", merged.Code, StringComparison.Ordinal);
        Assert.Contains("public DateRow<ContactDialog> Since => new(this, \"Customer since\");", merged.Code, StringComparison.Ordinal);
        Assert.Contains("public ContactDialog FillIn(string name)", merged.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void The_class_keeps_its_own_declaration_and_what_a_person_wrote_above_a_member()
    {
        var merged = ClassMerge.Merge(Existing, Draft, "ContactDialog");

        // The class is one file's: it does not become a part because the draft is written as one.
        Assert.Contains("public sealed class ContactDialog(ContactsPage page)", merged.Code, StringComparison.Ordinal);
        Assert.DoesNotContain("partial", merged.Code, StringComparison.Ordinal);
        Assert.Contains("/// <summary>The contact form.</summary>", merged.Code, StringComparison.Ordinal);
        Assert.Contains("/// <summary>The name, as a person wrote it down.</summary>", merged.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void The_first_line_and_the_usings_are_those_of_now()
    {
        var merged = ClassMerge.Merge(Existing, Draft, "ContactDialog");

        Assert.StartsWith("// <scraped page=\"/contact › + Contact\" variant=\"form\" from=\"walk/007.nodes.jsonl\" hash=\"bbbb2222\" />", merged.Code, StringComparison.Ordinal);
        Assert.DoesNotContain("aaaa1111", merged.Code, StringComparison.Ordinal);
        Assert.Contains("using Shop.Controls;", merged.Code, StringComparison.Ordinal);
        Assert.Single(merged.Code.Split('\n'), line => line.Trim() == "using Shop.Components;");
    }

    [Fact]
    public void Another_class_of_the_file_is_not_touched_and_the_same_draft_merged_twice_changes_nothing()
    {
        var once = ClassMerge.Merge(Existing, Draft, "ContactDialog");
        var twice = ClassMerge.Merge(once.Code, Draft, "ContactDialog");

        Assert.Contains("public int Name => 1;", once.Code, StringComparison.Ordinal);
        Assert.True(twice.Same);
        Assert.Equal(once.Code, twice.Code);
    }

    [Fact]
    public void A_class_written_by_hand_gets_the_first_line_of_the_draft_it_was_merged_with()
    {
        var hand = "namespace Shop.Pages;\n\npublic sealed class ContactDialog\n{\n}\n";

        var merged = ClassMerge.Merge(hand, Draft, "ContactDialog");

        Assert.StartsWith("// <scraped page=", merged.Code, StringComparison.Ordinal);
        Assert.Equal(["Name", "Country", "Since"], merged.Added);
        Assert.Contains("namespace Shop.Pages;", merged.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void A_person_s_class_is_only_added_to_and_keeps_its_first_line()
    {
        var hand = Existing[(Existing.IndexOf('\n', StringComparison.Ordinal) + 1)..];

        var merged = ClassMerge.Merge(hand, Draft, "ContactDialog", addOnly: true);

        Assert.Equal(["Since"], merged.Added);
        Assert.Empty(merged.Replaced);
        // The person's own member stays as it was written, and the file does not become a draft.
        Assert.Contains("public TextRow<ContactDialog> Name => new(this, \"Naam\");", merged.Code, StringComparison.Ordinal);
        Assert.DoesNotContain("<scraped", merged.Code, StringComparison.Ordinal);
        Assert.Contains("public DateRow<ContactDialog> Since => new(this, \"Customer since\");", merged.Code, StringComparison.Ordinal);
        Assert.Contains("using Shop.Controls;", merged.Code, StringComparison.Ordinal);
        Assert.Equal(merged.Code, ClassMerge.Merge(merged.Code, Draft, "ContactDialog", addOnly: true).Code);
    }

    [Fact]
    public void Only_the_members_that_are_asked_for_are_taken_from_the_draft()
    {
        var merged = ClassMerge.Merge(Existing, Draft, "ContactDialog", addOnly: true, only: ["Country", "Nobody"]);

        Assert.Empty(merged.Added);
        Assert.Equal(Existing, merged.Code);
    }

    [Fact]
    public void A_file_that_does_not_declare_the_class_is_said()
    {
        Assert.Contains("does not declare Nobody", Assert.Throws<ArgumentException>(() => ClassMerge.Merge(Existing, Draft, "Nobody")).Message, StringComparison.Ordinal);
    }
}
