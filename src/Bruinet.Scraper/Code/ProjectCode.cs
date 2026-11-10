using System.Xml.Linq;
using Bruinet.Scraper.Projects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bruinet.Scraper.Code;

/// <summary>
/// The page objects and controls a scraped project has, read from its source (syntax only, nothing is
/// built), and from the scraped projects it references: a shared project's controls come with it.
/// </summary>
public static class ProjectCode
{
    private static readonly string[] PageBases = ["PageObjectBase", "BlazorPageObjectBase"];
    private static readonly string[] ContainerBases = ["ContainerBase", "FrameScope"];
    private static readonly string[] CollectionBases = ["CollectionObjectBase"];
    private static readonly string[] ItemBases = ["ItemObjectBase"];

    /// <summary>The classes of a project and the scraped projects it references, the project's own first.</summary>
    public static IReadOnlyList<CodeClass> Read(ScrapeProject project)
    {
        var classes = new List<CodeClass>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>([project.Folder]);
        while (pending.Count > 0)
        {
            var folder = pending.Dequeue();
            if (!seen.Add(folder))
                continue;
            var name = ProjectName(folder);
            classes.AddRange(ReadFolder(folder, name));
            foreach (var referenced in References(folder))
                if (File.Exists(Path.Combine(referenced, ScraperConfig.FileName)))
                    pending.Enqueue(referenced);
        }
        return classes;
    }

    /// <summary>The classes in one file's text.</summary>
    public static IEnumerable<CodeClass> ReadText(string text, string file, string project)
    {
        var root = CSharpSyntaxTree.ParseText(text).GetCompilationUnitRoot();
        var header = ScrapedHeader.Find(text);
        foreach (var type in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            var baseName = type.BaseList?.Types.Select(t => BaseName(t.Type)).FirstOrDefault(n => n is not null);
            if (Kind(baseName) is not { } kind)
                continue;
            var members = Members(type);
            yield return new CodeClass(
                type.Identifier.Text + (type.TypeParameterList is { } tp ? tp.ToString() : ""),
                Namespace(type),
                kind,
                baseName,
                file,
                project,
                header,
                members);
            header = null; // the header is the file's first page object's or control's
        }
    }

    private static IEnumerable<CodeClass> ReadFolder(string folder, string project)
    {
        var classes = new List<CodeClass>();
        var parts = new List<(string Namespace, string Name, CodeMember[] Members)>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(folder, file);
            var first = relative.Split(Path.DirectorySeparatorChar)[0];
            if (first is "bin" or "obj" or ScrapeProject.RecordingsFolderName || file.EndsWith(".gen.cs", StringComparison.OrdinalIgnoreCase))
                continue;
            var text = File.ReadAllText(file);
            classes.AddRange(ReadText(text, file, project));
            parts.AddRange(Parts(text));
        }
        // A partial class's other parts (a person's beside a draft) add their members to it.
        return classes.Select(c =>
        {
            var more = parts.Where(p => p.Namespace == c.Namespace && p.Name == c.Name).SelectMany(p => p.Members).Where(m => c.Members.All(x => x.Name != m.Name)).ToArray();
            return more.Length == 0 ? c : c with { Members = [.. c.Members, .. more] };
        }).ToArray();
    }

    // The parts of partial classes that name no base: their members belong to the part that does.
    private static IEnumerable<(string Namespace, string Name, CodeMember[] Members)> Parts(string text)
    {
        var root = CSharpSyntaxTree.ParseText(text).GetCompilationUnitRoot();
        foreach (var type in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            if (!type.Modifiers.Any(SyntaxKind.PartialKeyword) || type.BaseList is not null)
                continue;
            yield return (Namespace(type), type.Identifier.Text + (type.TypeParameterList?.ToString() ?? ""), Members(type));
        }
    }

    private static CodeMember[] Members(ClassDeclarationSyntax type) =>
        type.Members.OfType<PropertyDeclarationSyntax>()
            .Where(p => p.Modifiers.Any(SyntaxKind.PublicKeyword))
            .Select(p => new CodeMember(p.Identifier.Text, p.Type.ToString(), Locator(p)) { Scope = Scope(p) })
            .ToArray();

    private static CodeKind? Kind(string? baseName) =>
        baseName is null ? null
        : PageBases.Contains(baseName) ? CodeKind.Page
        : ContainerBases.Contains(baseName) ? CodeKind.Container
        : CollectionBases.Contains(baseName) ? CodeKind.Collection
        : ItemBases.Contains(baseName) ? CodeKind.Item
        : baseName == "ViewBase" || baseName.EndsWith("ControlBase", StringComparison.Ordinal) ? CodeKind.Control
        : null;

    private static string? BaseName(TypeSyntax type) => type switch
    {
        GenericNameSyntax generic => generic.Identifier.Text,
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        QualifiedNameSyntax qualified => BaseName(qualified.Right),
        _ => null,
    };

    // The locator a control property is made with: its second constructor argument, as written.
    private static string? Locator(PropertyDeclarationSyntax property)
    {
        var creation = (property.ExpressionBody?.Expression ?? property.Initializer?.Value) as BaseObjectCreationExpressionSyntax;
        return creation?.ArgumentList?.Arguments.Skip(1).FirstOrDefault()?.ToString();
    }

    // What a control property is made on: its first constructor argument (this, or a frame's member).
    private static string? Scope(PropertyDeclarationSyntax property)
    {
        var creation = (property.ExpressionBody?.Expression ?? property.Initializer?.Value) as BaseObjectCreationExpressionSyntax;
        return creation?.ArgumentList?.Arguments.FirstOrDefault()?.ToString();
    }

    private static string Namespace(SyntaxNode node) =>
        node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? "";

    private static string ProjectName(string folder) =>
        Directory.EnumerateFiles(folder, "*.csproj").Select(Path.GetFileNameWithoutExtension).FirstOrDefault() ?? Path.GetFileName(folder);

    private static IEnumerable<string> References(string folder)
    {
        foreach (var projectFile in Directory.EnumerateFiles(folder, "*.csproj"))
        {
            XDocument document;
            try
            {
                document = XDocument.Load(projectFile);
            }
            catch (System.Xml.XmlException)
            {
                continue; // a project file that does not load builds nothing either; the build says why
            }
            foreach (var include in document.Descendants().Where(e => e.Name.LocalName == "ProjectReference").Select(e => (string?)e.Attribute("Include")).OfType<string>())
                yield return Path.GetDirectoryName(Path.GetFullPath(Path.Combine(folder, include.Replace('\\', Path.DirectorySeparatorChar))))!;
        }
    }
}

/// <summary>What a class is to the Scraper.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<CodeKind>))]
public enum CodeKind
{
    /// <summary>A page object.</summary>
    Page,

    /// <summary>A container or component with named parts.</summary>
    Container,

    /// <summary>A collection of rows.</summary>
    Collection,

    /// <summary>A collection's row.</summary>
    Item,

    /// <summary>A simple control.</summary>
    Control,
}

/// <summary>A page object or control in a project's source.</summary>
public sealed record CodeClass(string Name, string Namespace, CodeKind Kind, string? Base, string File, string Project, ScrapedHeader? Header, IReadOnlyList<CodeMember> Members);

/// <summary>A public property: a control on a page or container, with the locator it is made with.</summary>
public sealed record CodeMember(string Name, string Type, string? Locator)
{
    /// <summary>What it is made on, as written: <c>this</c>, or the member of the frame it is in.</summary>
    public string? Scope { get; init; }
}
