namespace Bruinet.Spec.Format;

/// <summary>A line of a spec, with its 1-based number in the file.</summary>
/// <param name="Number">The line's number, from 1.</param>
/// <param name="Text">The line, without its line ending.</param>
public sealed record SpecLine(int Number, string Text);

/// <summary>A row of a Markdown table in a spec; the separator row (<c>| --- |</c>) is not one.</summary>
/// <param name="Line">The row's line number.</param>
/// <param name="Cells">The cells, trimmed, left to right.</param>
public sealed record SpecRow(int Line, IReadOnlyList<string> Cells);

/// <summary>A <c>## </c> section of a spec: its heading and every line up to the next section.</summary>
/// <param name="Line">The heading's line number.</param>
/// <param name="Name">The heading's text, such as <c>Rules</c>.</param>
/// <param name="Body">The lines under the heading.</param>
public sealed record SpecSection(int Line, string Name, IReadOnlyList<SpecLine> Body)
{
    /// <summary>The rows of the tables in the section, in their order, the header row of each included.</summary>
    public IReadOnlyList<SpecRow> Rows() => SpecReader.TableRows(Body);
}

/// <summary>A <c>### </c> heading under <c>## Examples</c>: an example, or a chapter of examples.</summary>
/// <param name="Line">The heading's line number.</param>
/// <param name="Heading">The heading's text, after the hashes.</param>
/// <param name="Body">The lines under the heading, up to the next heading of its level.</param>
public abstract record SpecBlock(int Line, string Heading, IReadOnlyList<SpecLine> Body);

/// <summary>
/// An example: <c>### R5 An error shows only after Save</c>, or <c>#### R5 …</c> in a chapter. A heading that does not
/// start with <c>R</c> and a number is still read as an example, with no rule, so a check can name it.
/// </summary>
/// <param name="Line">The heading's line number.</param>
/// <param name="Heading">The heading's text, after the hashes.</param>
/// <param name="Body">The lines under the heading.</param>
/// <param name="RuleNumber">The rule's number (<c>5</c> for <c>R5</c>); null when the heading names none.</param>
/// <param name="Name">The example's name, after the rule's number.</param>
/// <param name="ChapterName">The name of the chapter it is in; null for an example at the Examples' own level.</param>
public sealed record SpecExample(int Line, string Heading, IReadOnlyList<SpecLine> Body, int? RuleNumber, string Name, string? ChapterName)
    : SpecBlock(Line, Heading, Body);

/// <summary>
/// A chapter, <c>### Chapter: The list at a glance</c>: steps that go before each of its examples (<c>#### R1 …</c>).
/// </summary>
/// <param name="Line">The heading's line number.</param>
/// <param name="Heading">The heading's text, after the hashes.</param>
/// <param name="Body">The lines under the heading, its examples' included.</param>
/// <param name="Name">The chapter's name, after <c>Chapter:</c>.</param>
/// <param name="Preamble">The lines before its first example: the chapter's own steps.</param>
/// <param name="Examples">Its examples, in their order.</param>
public sealed record SpecChapter(int Line, string Heading, IReadOnlyList<SpecLine> Body, string Name, IReadOnlyList<SpecLine> Preamble, IReadOnlyList<SpecExample> Examples)
    : SpecBlock(Line, Heading, Body);

/// <summary>A spec read into its parts, each with the lines it is on. It says what is there; whether that follows the format is <c>Bruinet.Spec</c>'s check.</summary>
/// <param name="JourneyId">The journey id from the title (<c># Spec: TOD.02 Create a todo</c>), such as <c>TOD.02</c>; null without a title.</param>
/// <param name="TitleRest">What follows the journey id on the title line, such as <c> Create a todo</c>.</param>
/// <param name="Header">The lines between the title and the first section.</param>
/// <param name="Sections">The <c>## </c> sections, in their order.</param>
/// <param name="Blocks">The examples and chapters under <c>## Examples</c>, in their order.</param>
public sealed record SpecDocument(
    string? JourneyId,
    string TitleRest,
    IReadOnlyList<SpecLine> Header,
    IReadOnlyList<SpecSection> Sections,
    IReadOnlyList<SpecBlock> Blocks)
{
    /// <summary>The first section with this name; null when there is none.</summary>
    /// <param name="name">The section's name, such as <c>Rules</c>.</param>
    public SpecSection? Section(string name) => Sections.FirstOrDefault(section => section.Name == name);

    /// <summary>Every example, its chapters' included, in their order.</summary>
    public IReadOnlyList<SpecExample> Examples =>
        [.. Blocks.SelectMany(block => block is SpecChapter chapter ? chapter.Examples : [(SpecExample)block])];

    /// <summary>The chapters, in their order.</summary>
    public IReadOnlyList<SpecChapter> Chapters => [.. Blocks.OfType<SpecChapter>()];

    /// <summary>The ids in the rules table, in their order, such as <c>TOD.02.R1</c>.</summary>
    public IReadOnlyList<string> RuleIds => Section("Rules") is { } rules
        ? [.. rules.Rows().Skip(1).Select(row => row.Cells[0]).Where(id => id.Length > 0)]
        : [];
}
