using Bruinet.Generator;
using Bruinet.Generator.Models;

namespace Bruinet.Cli.Tests.Generate;

/// <summary>
/// The approval test of the generator's output (AD-033): every committed <c>.gen.cs</c> is what the
/// generator writes from its <c>.tpl.cs</c> today. It fails when a generated file was edited by hand
/// (the invariant "the generator output is not hand-edited"), or when a generator change alters files
/// nobody regenerated. Regenerate with <c>bruinet controls generate</c> and read the diff.
/// </summary>
public sealed class GeneratedFilesTests
{
    private static readonly string[] Skipped = [".git", ".my", "bin", "obj", "TestResults", "node_modules"];

    [Fact]
    public void EveryGeneratedFile_IsWhatTheGeneratorWritesFromItsTemplate()
    {
        var root = RepositoryRoot();
        var generator = ControlObjectGenerator.CreateDefault();
        var templates = Templates(root).ToList();

        var stale = templates
            .Select(template => (template, generated: template[..^".tpl.cs".Length] + ".gen.cs"))
            .Where(pair => File.Exists(pair.generated))
            .Where(pair =>
            {
                var expected = generator.Generate(File.ReadAllText(pair.template), new GeneratorOptions
                {
                    InputFilePath = pair.template,
                    OutputFilePath = pair.generated,
                    IncludeGeneratedHeader = true,
                });
                return Normalized(expected) != Normalized(File.ReadAllText(pair.generated));
            })
            .Select(pair => Path.GetRelativePath(root, pair.generated).Replace('\\', '/'))
            .ToList();

        Assert.True(templates.Count >= 100, $"Only {templates.Count} templates found under {root}.");
        Assert.True(stale.Count == 0,
            $"These .gen.cs files differ from what the generator writes: regenerate them (bruinet controls generate), never edit them: {string.Join(", ", stale)}");
    }

    private static string Normalized(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

    private static IEnumerable<string> Templates(string directory)
    {
        foreach (var template in Directory.EnumerateFiles(directory, "*.tpl.cs"))
        {
            yield return template;
        }

        foreach (var child in Directory.EnumerateDirectories(directory)
                     .Where(child => !Skipped.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase)))
        {
            foreach (var template in Templates(child))
            {
                yield return template;
            }
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bruinet.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
