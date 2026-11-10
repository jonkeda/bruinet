using Bruinet.Bdd;
using System.Text;
using Bruinet.Core.Testing;
using Bruinet.Scraper.Code;
using Bruinet.Scraper.Projects;
using Microsoft.CodeAnalysis.CSharp;

namespace Bruinet.Scraper.Testing;

/// <summary>
/// Drafts a resolved test recording by rules (R4), with no model: an xUnit UI test that calls page objects,
/// a <c>.feature.md</c> scenario of catalog phrases, or both, from the same steps. Every resolved step is
/// written and every gap marked where it falls. A draft is a <c>partial</c> class whose other part is the
/// person's and never touched; a file a person made is never drafted over.
/// </summary>
public static class TestDrafter
{
    /// <summary>Where xUnit drafts go, in the project.</summary>
    public const string TestsFolder = "Tests/Recorded";

    /// <summary>Where scenario drafts go, in the project.</summary>
    public const string FeaturesFolder = "Features/Recorded";

    /// <summary>A drafted xUnit part's extension; the person's part is the plain <c>.cs</c> beside it.</summary>
    public const string Extension = ".recorded.cs";

    /// <summary>Drafts the files of a resolved recording, without writing them.</summary>
    /// <param name="project">The scraped project.</param>
    /// <param name="test">The resolved recording.</param>
    /// <param name="header">What the draft says it was made from.</param>
    /// <param name="target">What to draft.</param>
    /// <param name="name">The test's name; the session's name when null.</param>
    public static TestDraft Draft(ScrapeProject project, ResolvedTest test, RecordedHeader header, TestTarget target, string? name = null)
    {
        name = string.IsNullOrWhiteSpace(name) ? test.Name : name.Trim();
        var className = Pascal(name) + "Tests";
        var files = new List<DraftFile>();
        var problems = new List<string>();
        if (target is TestTarget.Xunit or TestTarget.Both)
        {
            if (project.Config.Tests.Base is not { } baseClass)
            {
                problems.Add($"An xUnit draft derives from your UI tests' base, which gives Context and NavigateToPage(path): set ## Tests → Base in {ScraperConfig.FileName}.");
            }
            else
            {
                var ns = Namespace(project.Name) + ".Tests.Recorded";
                files.Add(new DraftFile($"{TestsFolder}/{className}{Extension}", XunitPart(project, test, header, ns, className, Method(name)), DraftFileKind.Draft));
                files.Add(new DraftFile($"{TestsFolder}/{className}.cs", PersonsPart(ns, className, baseClass), DraftFileKind.PersonsPart));
            }
        }
        if (target is TestTarget.Scenario or TestTarget.Both)
            files.Add(new DraftFile($"{FeaturesFolder}/{Slug.Of(name, "recorded")}.feature.md", Scenario(test, header, name), DraftFileKind.Draft));
        return new TestDraft(files, problems);
    }

    /// <summary>
    /// Writes a draft into the project: a drafted file over its earlier draft (one with a <c>recorded</c>
    /// header), never over a file a person made; the person's part only when it is not there yet.
    /// </summary>
    public static IReadOnlyList<DraftWrite> Write(ScrapeProject project, TestDraft draft)
    {
        var written = new List<DraftWrite>();
        foreach (var file in draft.Files)
        {
            var full = Path.Combine(project.Folder, file.Path.Replace('/', Path.DirectorySeparatorChar));
            var exists = File.Exists(full);
            if (exists && file.Kind == DraftFileKind.PersonsPart)
            {
                written.Add(new DraftWrite(file.Path, false, "Yours: kept as it is."));
                continue;
            }
            if (exists && RecordedHeader.Find(File.ReadAllText(full)) is null)
            {
                written.Add(new DraftWrite(file.Path, false, "A file without a recorded header is there: it is a person's, and is not drafted over. Draft under another name."));
                continue;
            }
            if (file.Path.EndsWith(".cs", StringComparison.Ordinal))
            {
                var result = CodeGate.Write(project, file.Path, file.Text);
                written.Add(new DraftWrite(file.Path, result.Written, result.Written ? null : result.Reason ?? string.Join("; ", result.Problems.Select(p => p.Message))));
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, file.Text);
            written.Add(new DraftWrite(file.Path, true, null));
        }
        return written;
    }

    private static string XunitPart(ScrapeProject project, ResolvedTest test, RecordedHeader header, string ns, string className, string method)
    {
        var classes = ProjectCode.Read(project).Where(c => c.Kind == CodeKind.Page).ToArray();
        var usings = test.Steps.Select(s => s.PageObject).Append(test.StartPageObject).OfType<string>()
            .Select(p => classes.FirstOrDefault(c => c.Name == p)?.Namespace)
            .OfType<string>().Where(n => n.Length > 0 && n != ns).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        var body = new List<string>();
        var pages = new Dictionary<string, string>(StringComparer.Ordinal);
        string PageVar(string pageObject)
        {
            if (pages.TryGetValue(pageObject, out var name))
                return name;
            name = Camel(StepNames.FromIdentifier(pageObject));
            while (pages.ContainsValue(name))
                name += "2";
            pages[pageObject] = name;
            body.Add($"var {name} = new {pageObject}(Context);");
            return name;
        }

        if (test.StartPage is { } start)
            body.Add($"NavigateToPage({Literal(start)});");
        if (test.StartPageObject is { } first)
            PageVar(first);
        else
            body.Add($"// gap: the start page '{test.StartPage}' has no page object: draft it, then draft this test again.");
        body.Add("");
        foreach (var step in test.Steps)
        {
            if (step.Gap is { } gap)
            {
                body.Add("// gap: " + OneLine(gap.Message));
                continue;
            }
            var page = PageVar(step.PageObject!);
            if (step.Verb == "On")
            {
                body.Add($"{page}.AssertLoaded(true);");
                continue;
            }
            var target = $"{page}.{step.Member}";
            if (step.Placeholder is { } placeholder)
            {
                var variable = Environment(placeholder);
                body.Add($"// the secret <{placeholder}>: set {variable}, or map it to a setting or test data");
                body.Add($"{target}.{step.Verb}(System.Environment.GetEnvironmentVariable({Literal(variable)}) ?? \"\");");
                continue;
            }
            body.Add(step.Verb switch
            {
                "Enter" or "SelectByText" or "AssertText" => $"{target}.{step.Verb}({Literal(step.Value ?? "")});",
                "AssertVisible" => $"{target}.AssertVisible(true);",
                "AssertChecked" => $"{target}.AssertChecked({(step.Checked == false ? "false" : "true")});",
                _ => $"{target}.{step.Verb}();",
            });
        }

        var code = new StringBuilder();
        code.Append(header).Append('\n');
        foreach (var u in usings)
            code.Append("using ").Append(u).Append(";\n");
        code.Append('\n').Append("namespace ").Append(ns).Append(";\n\n");
        code.Append("// Drafted from a recording by rules: drafted again over this file, so change the page objects or your part instead.\n");
        code.Append("public sealed partial class ").Append(className).Append("\n{\n");
        code.Append("    [Fact]\n    public void ").Append(method).Append("()\n    {\n");
        foreach (var line in TrimBlank(body))
            code.Append(line.Length == 0 ? "\n" : "        " + line + "\n");
        code.Append("    }\n}\n");
        return code.ToString();
    }

    private static string PersonsPart(string ns, string className, string baseClass) =>
        $"namespace {ns};\n\n" +
        "// Your part of a recorded test: the Scraper never changes it. The base gives Context and NavigateToPage(path).\n" +
        $"public sealed partial class {className} : {baseClass}\n{{\n}}\n";

    private static string Scenario(ResolvedTest test, RecordedHeader header, string name)
    {
        var md = new StringBuilder();
        md.Append("# Feature: ").Append(name).Append("\n\n## Metadata\n\n| Field | Value |\n| --- | --- |\n");
        md.Append("| ").Append(RecordedHeader.MetadataField).Append(" | ").Append(header.MetadataValue).Append(" |\n\n");
        md.Append("## Scenario: ").Append(name).Append("\n\n");
        var block = new List<string>();
        string? group = null;
        void Step(string keyword, string phrase)
        {
            block.Add((group == keyword ? "And" : keyword) + " " + phrase);
            group = keyword;
        }
        void Flush()
        {
            if (block.Count == 0)
                return;
            md.Append("```gherkin\n");
            foreach (var line in block)
                md.Append(line).Append('\n');
            md.Append("```\n\n");
            block.Clear();
            group = null; // a new block starts with its keyword
        }
        void Gap(int? number, string message)
        {
            Flush();
            md.Append(number is { } n ? $"Gap (event {n}): " : "Gap: ").Append(OneLine(message)).Append("\n\n");
        }

        if (test.StartPageObject is { } first)
            Step("Given", $"I am on the {StepNames.FromIdentifier(first)} page");
        else
            Gap(null, $"the start page '{test.StartPage}' has no page object: draft it, then draft this test again.");
        foreach (var step in test.Steps)
        {
            if (step.Gap is { } gap)
                Gap(step.Event, gap.Message);
            else if (step.Placeholder is { } placeholder)
                Gap(step.Event, $"the secret <{placeholder}> goes here, mapped to a setting or test data: {step.Phrase}");
            else
                Step(step.Verb is "On" || step.Kind == RecordingEvent.AssertKind ? "Then" : "When", step.Phrase!);
        }
        Flush();
        return md.ToString().TrimEnd('\n') + "\n";
    }

    private static IEnumerable<string> TrimBlank(List<string> lines)
    {
        while (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, quote: true);

    private static string OneLine(string text) => text.ReplaceLineEndings(" ");

    private static string[] Words(string text) =>
        text.Split((char[])[.. text.Where(c => !char.IsLetterOrDigit(c)).Distinct()], StringSplitOptions.RemoveEmptyEntries);

    private static string Pascal(string text)
    {
        var name = string.Concat(Words(text).Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
        return name.Length == 0 || char.IsDigit(name[0]) ? "Recorded" + name : name;
    }

    private static string Camel(string text)
    {
        var pascal = Pascal(text);
        return char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }

    private static string Method(string text)
    {
        var words = Words(text);
        if (words.Length == 0)
            return "Recorded";
        var name = string.Join('_', words.Select((w, i) => i == 0 ? char.ToUpperInvariant(w[0]) + w[1..] : w.ToLowerInvariant()));
        return char.IsDigit(name[0]) ? "Recorded_" + name : name;
    }

    // A project name as a namespace: each part an identifier.
    private static string Namespace(string project) =>
        string.Join('.', project.Split('.').Select(part =>
        {
            var name = new string(part.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
            return name.Length == 0 || char.IsDigit(name[0]) ? "_" + name : name;
        }));

    // A placeholder's environment variable: apiKey is API_KEY.
    private static string Environment(string placeholder) =>
        string.Concat(placeholder.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + c : c.ToString())).ToUpperInvariant();
}

/// <summary>The files of a draft, and why a target could not be drafted.</summary>
public sealed record TestDraft(IReadOnlyList<DraftFile> Files, IReadOnlyList<string> Problems);

/// <summary>A drafted file: its path in the project, its text, and whose it is.</summary>
public sealed record DraftFile(string Path, string Text, DraftFileKind Kind);

/// <summary>Whose a drafted file is.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DraftFileKind>))]
public enum DraftFileKind
{
    /// <summary>The Scraper's: drafted again over itself.</summary>
    Draft,

    /// <summary>The person's: written once, then never touched.</summary>
    PersonsPart,
}

/// <summary>What became of a drafted file: written, or why not.</summary>
public sealed record DraftWrite(string Path, bool Written, string? Note);
