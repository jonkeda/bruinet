using Bruinet.Generator.Analysis;
using Bruinet.Generator.Generation;
using Bruinet.Generator.Generators;
using Bruinet.Generator.Models;

namespace Bruinet.Generator;

/// <summary>
/// Top-level coordinator for generating Bruinet ControlObjects. Registered
/// member generators (e.g., <see cref="ActionGenerator"/> and
/// <see cref="IsWaitAssertGenerator"/>) are run over the analyzed source to emit
/// public members from base Core method definitions.
/// </summary>
public sealed class ControlObjectGenerator
{
    private readonly List<IMemberGenerator> _generators = new();
    private readonly ControlObjectAnalyzer _analyzer = new();
    private readonly ControlObjectBuilder _builder = new();
    private readonly CodeFormatter _formatter = new();

    /// <summary>
    /// Warnings from the last <see cref="Generate"/>: code that generates but breaks a rule the
    /// generator cannot enforce by failing, such as a Core method that nests a unit of work.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; private set; } = [];

    /// <summary>
    /// Registers a member generator. Registration order matters: the first
    /// generator whose <c>Matches</c> returns true handles the Core method.
    /// </summary>
    public ControlObjectGenerator Register(IMemberGenerator generator)
    {
        _generators.Add(generator);
        return this;
    }

    /// <summary>
    /// Generates ControlObject members from source and returns the formatted code.
    /// </summary>
    /// <param name="sourceCode">The C# source code to analyze.</param>
    /// <param name="options">Generation options.</param>
    /// <returns>The formatted generated code as a string.</returns>
    public string Generate(string sourceCode, GeneratorOptions options)
    {
        if (string.IsNullOrEmpty(sourceCode))
            throw new ArgumentException("Source code cannot be empty.", nameof(sourceCode));

        if (_generators.Count == 0)
            throw new InvalidOperationException("At least one member generator must be registered.");

        Warnings = [];

        var (classDecl, root) = _analyzer.FindTarget(sourceCode, options.TargetClassName);

        if (classDecl == null)
            throw new InvalidOperationException($"Could not find class {options.TargetClassName ?? "(any)"} in source code.");

        // A near-miss Core method used to vanish silently; with [SkipGeneration] available to
        // declare a deliberate exclusion, an undeclared one is an error rather than a maybe.
        var skipped = _analyzer.FindSilentlySkippedCoreMethods(classDecl)
            .Concat(_analyzer.FindMalformedShortcuts(classDecl))
            .ToList();
        if (skipped.Count > 0)
        {
            throw new InvalidOperationException(
                $"In {classDecl.Identifier.Text}: " + string.Join(" ", skipped));
        }

        Warnings = _analyzer.FindNestedUnitsOfWork(classDecl)
            .Select(warning => $"In {classDecl.Identifier.Text}: {warning}")
            .ToList();

        var context = _analyzer.BuildContext(classDecl, root);

        var members = new List<string>();

        // Tracks which Core method claimed each generated member name, so colliding
        // names fail with a clear message instead of emitting uncompilable code. Keyed on the
        // names actually emitted, not on the stem they share.
        var claimedNames = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var method in _analyzer.CoreMethods(classDecl))
        {
            // Filtered once here rather than in each generator's Matches: opting out is a
            // property of the method, not of which family would have claimed it.
            if (ControlObjectAnalyzer.IsGenerationSkipped(method))
                continue;

            foreach (var generator in _generators)
            {
                if (!generator.Matches(method))
                    continue;

                var info = generator.Extract(method);

                foreach (var emittedName in generator.EmittedMemberNames(info))
                {
                    if (claimedNames.TryGetValue(emittedName, out var previousCoreMethod))
                    {
                        throw new InvalidOperationException(
                            $"Generated member name '{emittedName}' is claimed by both " +
                            $"'{previousCoreMethod}' and '{info.MethodName}' in {context.ContainingTypeName}. " +
                            "Rename one of the Core methods — overloads that differ only by parameters " +
                            "collide on the generated name.");
                    }

                    claimedNames.Add(emittedName, info.MethodName);
                }

                members.Add(generator.Generate(info, context));
                break; // first matching generator wins
            }
        }

        // A template with no Core methods still gets a (member-less) partial: echoing the
        // source back would duplicate the class declaration in the .gen.cs file.
        var code = _builder.BuildCompilationUnit(context, members, options.IncludeGeneratedHeader);
        return _formatter.Format(code);
    }

    /// <summary>
    /// Generates ControlObject members from a file and writes to the output file.
    /// </summary>
    /// <param name="inputFilePath">Path to the source file.</param>
    /// <param name="outputFilePath">Path to write the generated file.</param>
    /// <param name="options">Generation options.</param>
    /// <summary>
    /// Writes the generated file only when its content changed, compared without line endings, so a run over every
    /// template leaves the untouched files untouched (review 11, proposal 11). True when it wrote.
    /// </summary>
    public bool GenerateToFile(string inputFilePath, string outputFilePath, GeneratorOptions options)
    {
        if (!File.Exists(inputFilePath))
            throw new FileNotFoundException($"Input file not found: {inputFilePath}");

        var sourceCode = File.ReadAllText(inputFilePath);
        var generatedCode = Generate(sourceCode, options);

        if (File.Exists(outputFilePath)
            && string.Equals(File.ReadAllText(outputFilePath).ReplaceLineEndings("\n"), generatedCode.ReplaceLineEndings("\n"), StringComparison.Ordinal))
        {
            return false;
        }

        File.WriteAllText(outputFilePath, generatedCode);
        return true;
    }

    /// <summary>
    /// Creates a generator wired with the default member generators. Order matters:
    /// the Is/Wait/Assert family and the Set family are registered before the broader
    /// action family so <c>Is*Core</c>, <c>Get*Core</c>, and <c>Set*Core</c> are not
    /// captured as plain actions. Shortcuts end in <c>Shortcut</c>, not <c>Core</c>, so no
    /// other generator claims them; they come first only to make that obvious.
    /// </summary>
    public static ControlObjectGenerator CreateDefault() =>
        new ControlObjectGenerator()
            .Register(new ShortcutGenerator())
            .Register(new IsWaitAssertGenerator())
            .Register(new SetGenerator())
            .Register(new ActionGenerator());
}
