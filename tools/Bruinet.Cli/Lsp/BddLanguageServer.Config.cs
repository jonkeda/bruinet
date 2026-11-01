using System.Diagnostics.CodeAnalysis;
using Bruinet.TestData;
using Bruinet.Bdd.Workspace;

namespace Bruinet.Bdd.LanguageServer;

// The suite configs, uat.config.md and bdd.config.md (phase 4): diagnostics for what the engine
// would ignore or reject, completion and hover of their fields and values, and the test-data
// schema kept in step with the definition. And test sets offered for a feature file's TestData.
public sealed partial class BddLanguageServer
{
    private const string SchemaStale = "UATG009";

    private static readonly Dictionary<string, Dictionary<string, string>> ConfigFields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Runtime"] = new(StringComparer.OrdinalIgnoreCase)
        {
            [BddConfigFields.Fixture] = "The fixture type the suite runs with: its name, or its full name.",
            [BddConfigFields.Head] = "The head this suite drives: maui, web, mixed or service. A scenario tagged with heads runs only on theirs.",
            [BddConfigFields.Target] = "What the fixture drives (MAUI, BLAZOR, SERVICE); derived from the fixture when left out.",
            [BddConfigFields.AppPath] = "The app to start, relative to the config; for an app the fixture cannot find itself.",
            [BddConfigFields.WorkingDirectory] = "The working directory of a run, relative to the config.",
            [BddConfigFields.DefaultTestData] = "The test set a feature file runs on when its metadata names none.",
            [BddConfigFields.AppsFile] = "A markdown file with the suite's Apps table, relative to the config (apps.md, shared with the UI tests).",
            [BddConfigFields.Project] = "The UAT test project, from which the Pages assembly is resolved.",
        },
        ["Discovery"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["RequireExplicitUatAttributes"] = "true: only members marked for UAT are steps and controls.",
            ["AllowNameInference"] = "true: a control's UAT name may be inferred from its property name.",
        },
        ["Reporting"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["OutputDirectory"] = "Where the reports go, under the run's folder ($(BruinetTestResults)).",
            ["ScreenshotOnFailure"] = "true: a failed step takes a screenshot.",
            ["IncludeRuntimeTrace"] = "true: each report has the catalog and discovery trace too.",
        },
        ["Settings"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Root"] = "The settings folder, relative to the config.",
            ["DefaultFile"] = "The settings every scenario reads.",
            ["LocalFile"] = "A per-machine settings file over the default, not committed.",
            ["ScenarioConvention"] = "A scenario's own settings file: scenarios/{ScenarioId}.json.",
        },
    };

    private static readonly Dictionary<string, string> AssemblyKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Pages"] = "The page objects, and the fixture of a UI suite.",
        ["Steps"] = "The step definitions, and the fixture of a service suite.",
        ["Controls"] = "The control library whose control verbs the steps use.",
        ["Commands"] = "The built-in commands (Bruinet.Uat).",
        [BddConfigFields.TestDataAssemblyKind] = "The test-data definition: the test sets and their schema.",
    };

    private static readonly string[] ConfigSections = ["Runtime", "Assemblies", "Discovery", "Reporting", "Settings", "Skip Rules", "Apps", "Using"];

    private static readonly HashSet<string> BooleanFields = new(StringComparer.OrdinalIgnoreCase) { "RequireExplicitUatAttributes", "AllowNameInference", "ScreenshotOnFailure", "IncludeRuntimeTrace" };

    internal static bool IsConfig(string path) => BddFileFormat.IsConfigFileName(Path.GetFileName(path));

    // A config's tables: each ## section with its first table's columns and rows (1-based lines).
    private static List<(BddHeading Section, List<string> Columns, List<(int Line, List<string> Cells)> Rows)> ConfigTables(BddServerFile file)
    {
        List<(BddHeading, List<string>, List<(int, List<string>)>)> tables = [];
        foreach (var section in Flatten(file.Structure.Headings).Where(heading => heading.Level == 2))
        {
            if (file.Structure.Blocks.FirstOrDefault(block => block.Kind == BddBlockKind.Table && block.Start > section.Line && block.Start <= section.End) is not { } table)
            {
                tables.Add((section, [], []));
                continue;
            }

            var columns = Cells(file.Line(table.Start));
            var rows = Enumerable.Range(table.Start + 2, Math.Max(0, table.End - table.Start - 1))
                .Where(line => file.Line(line).TrimStart().StartsWith('|'))
                .Select(line => (line, Cells(file.Line(line))))
                .ToList();
            tables.Add((section, columns, rows));
        }

        return tables;
    }

    private static List<string> Cells(string row)
    {
        var trimmed = row.Trim();
        if (trimmed.StartsWith('|'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.EndsWith('|'))
        {
            trimmed = trimmed[..^1];
        }

        return trimmed.Split('|').Select(cell => cell.Trim()).ToList();
    }

    [SuppressMessage("Maintainability", "CA1502", Justification = "Known debt (AD-033): split when it next changes.")]
    internal IReadOnlyList<Diagnostic> ConfigDiagnostics(BddServerFile file)
    {
        List<Diagnostic> diagnostics = [];
        var tables = ConfigTables(file);
        var directory = Path.GetDirectoryName(file.Path)!;
        var catalogs = file.Suite?.TryGetCatalogs(out _);
        var definitions = catalogs is null ? [] : file.Suite!.TestDataDefinitions();

        foreach (var (section, columns, rows) in tables)
        {
            var name = section.Text.Trim();
            if (!ConfigSections.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                Add(section.Line, 2, "UATG001", $"'## {name}' is not a config section, so the suite ignores it. The sections are {string.Join(", ", ConfigSections)}.");
                continue;
            }

            foreach (var (line, cells) in rows)
            {
                var row = columns.Zip(cells).ToDictionary(pair => pair.First, pair => pair.Second, StringComparer.OrdinalIgnoreCase);
                if (ConfigFields.TryGetValue(name, out var fields) && row.TryGetValue("Field", out var field) && field.Length > 0)
                {
                    var value = row.GetValueOrDefault("Value") ?? string.Empty;
                    if (!fields.ContainsKey(field))
                    {
                        Add(line, 2, "UATG002", $"'{field}' is not a {name} field, so the suite ignores it. The fields are {string.Join(", ", fields.Keys)}.");
                    }
                    else if (BooleanFields.Contains(field) && !bool.TryParse(value, out _))
                    {
                        Add(line, 1, "UATG004", $"{field} is true or false, not '{value}'.");
                    }
                    else if (field.Equals(BddConfigFields.Head, StringComparison.OrdinalIgnoreCase) && !BddTagConventions.Heads.Contains(value, StringComparer.OrdinalIgnoreCase))
                    {
                        Add(line, 1, "UATG004", $"'{value}' is not a head. The heads are {string.Join(", ", BddTagConventions.Heads)}.");
                    }
                    else if (field.Equals(BddConfigFields.DefaultTestData, StringComparison.OrdinalIgnoreCase) && definitions.Count > 0 &&
                             !definitions.Any(definition => definition.TestSetNames.Contains(value, StringComparer.OrdinalIgnoreCase)))
                    {
                        Add(line, 1, "UATG004", $"'{value}' is not a test set. The test sets are {string.Join(", ", definitions.SelectMany(definition => definition.TestSetNames))}.");
                    }
                    else if (field.Equals(BddConfigFields.AppsFile, StringComparison.OrdinalIgnoreCase) && file.Suite is { } suite && ReadConfigProblem(suite) is { } problem)
                    {
                        Add(line, 1, "UATG006", problem);
                    }
                    else if (field.Equals(BddConfigFields.Fixture, StringComparison.OrdinalIgnoreCase) && file.Suite is { } bound && catalogs is null &&
                             bound.TryGetCatalogs(out var reason) is null && reason is not null && !reason.Contains("Build the UAT project first", StringComparison.Ordinal))
                    {
                        Add(line, 2, "UATG008", $"The suite cannot be bound, so its feature files are only parsed: {reason}");
                    }
                }
                else if (name.Equals("Assemblies", StringComparison.OrdinalIgnoreCase) && row.GetValueOrDefault("Kind") is { Length: > 0 } kind)
                {
                    if (!AssemblyKinds.ContainsKey(kind))
                    {
                        Add(line, 2, "UATG002", $"'{kind}' is not an assembly kind. The kinds are {string.Join(", ", AssemblyKinds.Keys)}.");
                    }

                    if (row.GetValueOrDefault("Assembly") is { Length: > 0 } assembly && !File.Exists(Path.GetFullPath(Path.Combine(directory, assembly))))
                    {
                        Add(line, 2, "UATG005", $"'{assembly}' is not built yet. Build it, and the suite binds.");
                    }

                    if (kind.Equals(BddConfigFields.TestDataAssemblyKind, StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var definition in definitions.Where(definition => SchemaIsStale(definition)))
                        {
                            Add(line, 2, SchemaStale, $"{TestDataSchema.FileName} of {definition.Name} is out of date with its definition. Run 'bruinet data schema', or use the quick fix.");
                        }
                    }
                }
                else if (name.Equals("Apps", StringComparison.OrdinalIgnoreCase))
                {
                    if (row.GetValueOrDefault("Name") is not { Length: > 0 })
                    {
                        Add(line, 1, "UATG004", "An app needs a Name; the suite skips a row without one.");
                    }
                    else if (row.GetValueOrDefault("Head") is { Length: > 0 } head && !BddTagConventions.Heads.Contains(head, StringComparer.OrdinalIgnoreCase))
                    {
                        Add(line, 1, "UATG004", $"'{head}' is not a head. The heads are {string.Join(", ", BddTagConventions.Heads)}.");
                    }
                }
                else if (name.Equals("Using", StringComparison.OrdinalIgnoreCase) && catalogs is not null &&
                         row.GetValueOrDefault("Namespace") is { Length: > 0 } space && !KnownNamespaces(catalogs).Contains(BddNamespaces.Normalize(space)))
                {
                    Add(line, 2, "UATG007", $"No step or page is in the namespace '{space}', so using it gives nothing.");
                }
            }
        }

        if (!tables.Any(table => table.Section.Text.Trim().Equals("Runtime", StringComparison.OrdinalIgnoreCase) &&
                                 table.Rows.Any(row => row.Cells.FirstOrDefault()?.Equals(BddConfigFields.Fixture, StringComparison.OrdinalIgnoreCase) == true)))
        {
            var at = tables.FirstOrDefault(table => table.Section.Text.Trim().Equals("Runtime", StringComparison.OrdinalIgnoreCase)).Section?.Line ?? 1;
            Add(at, 1, "UATG003", "The Runtime table names no Fixture, so the scenarios cannot be bound or run.");
        }

        if (catalogs is not null && file.Suite!.VersionMismatches() is { Count: > 0 } mismatches)
        {
            Add(1, 2, "UATG010", $"The extension may bind differently from the suite's own run: {string.Join("; ", mismatches)}. Update the extension, or the suite's Bruinet packages.");
        }

        return diagnostics;

        void Add(int line, int severity, string code, string message)
            => diagnostics.Add(new Diagnostic(LineRange(file, line), severity, code, DiagnosticSource, message));
    }

    private static string? ReadConfigProblem(BddSuite suite)
    {
        try
        {
            suite.ReadConfig();
            return null;
        }
        catch (BddWorkspaceException exception)
        {
            return exception.Message;
        }
    }

    private static HashSet<string> KnownNamespaces(BddCatalogSet catalogs)
        => catalogs.Default.Patterns.Select(pattern => pattern.Namespace)
            .Concat(UatPageSet.Of(catalogs.Default).Values.Select(page => page.Namespace))
            .Concat(catalogs.Default.Scope?.Namespaces ?? [])
            .Where(name => name.Length > 0)
            .Select(BddNamespaces.Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool SchemaIsStale(ITestDataDefinition definition)
    {
        var path = TestDataSchema.PathOf(definition);
        return !File.Exists(path) || File.ReadAllText(path).ReplaceLineEndings("\n") != TestDataSchema.Export(definition);
    }

    // The schema written again, as bruinet data schema writes it.
    private void RegenerateSchema(BddServerFile file, List<CodeAction> actions)
    {
        if (file.Suite is not { } suite || suite.TryGetCatalogs(out _) is null)
        {
            return;
        }

        foreach (var definition in suite.TestDataDefinitions().Where(SchemaIsStale))
        {
            var path = TestDataSchema.PathOf(definition);
            if (!File.Exists(path))
            {
                continue;
            }

            var schema = Context(Path.GetFullPath(path));
            actions.Add(new CodeAction($"Regenerate {TestDataSchema.FileName} of {definition.Name}", "quickfix", Edit(new Uri(schema.Path).AbsoluteUri, new TextEdit(Whole(schema), TestDataSchema.Export(definition))))
            {
                IsPreferred = true,
            });
        }
    }

    // In a config table: field names, then the field's values; assembly kinds; namespaces.
    private CompletionList ConfigCompletion(BddServerFile file, int line, int column)
    {
        var table = ConfigTables(file).FirstOrDefault(table => table.Rows.Any(row => row.Line == line));
        var text = file.Line(line);
        if (table.Section is null || column > text.Length)
        {
            return new CompletionList(false, []);
        }

        var cell = text[..column].Count(character => character == '|') - 1;
        var start = text.LastIndexOf('|', Math.Max(0, column - 1)) + 1;
        var end = text.IndexOf('|', column) is var next and >= 0 ? next : text.Length;
        var range = new Range(new Position(line - 1, start), new Position(line - 1, end));
        var section = table.Section.Text.Trim();
        var cells = Cells(text);
        IEnumerable<(string Value, string Detail)> values = [];
        if (ConfigFields.TryGetValue(section, out var fields))
        {
            values = cell == 0
                ? fields.Select(field => (field.Key, field.Value))
                : ConfigValues(file, cells.FirstOrDefault() ?? string.Empty);
        }
        else if (section.Equals("Assemblies", StringComparison.OrdinalIgnoreCase) && cell == 0)
        {
            values = AssemblyKinds.Select(kind => (kind.Key, kind.Value));
        }
        else if (section.Equals("Using", StringComparison.OrdinalIgnoreCase) && file.Catalogs is { } catalogs)
        {
            values = KnownNamespaces(catalogs).Order(StringComparer.OrdinalIgnoreCase).Select(name => (name, "namespace"));
        }
        else if (section.Equals("Apps", StringComparison.OrdinalIgnoreCase) && table.Columns.ElementAtOrDefault(cell)?.Equals("Head", StringComparison.OrdinalIgnoreCase) == true)
        {
            values = BddTagConventions.Heads.Select(head => (head, "head"));
        }

        return new CompletionList(false, values
            .Select(value => new CompletionItem(value.Value, 12) { Detail = value.Detail, TextEdit = new TextEdit(range, $" {value.Value} ") })
            .ToList());
    }

    private IEnumerable<(string Value, string Detail)> ConfigValues(BddServerFile file, string field)
    {
        if (BooleanFields.Contains(field))
        {
            return [("true", field), ("false", field)];
        }

        if (field.Equals(BddConfigFields.Head, StringComparison.OrdinalIgnoreCase))
        {
            return BddTagConventions.Heads.Select(head => (head, "head"));
        }

        if (field.Equals(BddConfigFields.DefaultTestData, StringComparison.OrdinalIgnoreCase))
        {
            return TestSets(file).Select(set => (set, "test set"));
        }

        return [];
    }

    // The test sets of the file's suite, once it is built.
    private static IEnumerable<string> TestSets(BddServerFile file)
        => file.Suite is { } suite && file.Catalogs is not null
            ? suite.TestDataDefinitions().SelectMany(definition => definition.TestSetNames).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)
            : [];

    // A feature file's Metadata: the TestData value's test sets.
    private static CompletionList? MetadataCompletion(BddServerFile file, int line, int column)
    {
        var text = file.Line(line);
        var cells = Cells(text);
        if (!text.TrimStart().StartsWith('|') || cells.Count < 2 || !cells[0].Equals(BddMetadataFields.TestData, StringComparison.OrdinalIgnoreCase) ||
            text[..Math.Min(column, text.Length)].Count(character => character == '|') != 2)
        {
            return null;
        }

        var start = text.IndexOf('|', text.IndexOf('|') + 1) + 1;
        var end = text.IndexOf('|', start) is var next and >= 0 ? next : text.Length;
        var range = new Range(new Position(line - 1, start), new Position(line - 1, end));
        return new CompletionList(false, TestSets(file)
            .Select(set => new CompletionItem(set, 12) { Detail = "test set", TextEdit = new TextEdit(range, $" {set} ") })
            .ToList());
    }

    // A config field's or an assembly kind's meaning.
    private static Hover? ConfigHover(BddServerFile file, int line)
    {
        var table = ConfigTables(file).FirstOrDefault(table => table.Rows.Any(row => row.Line == line));
        if (table.Section is null || Cells(file.Line(line)).FirstOrDefault() is not { Length: > 0 } first)
        {
            return null;
        }

        var meaning = ConfigFields.TryGetValue(table.Section.Text.Trim(), out var fields) && fields.TryGetValue(first, out var field)
            ? field
            : table.Section.Text.Trim().Equals("Assemblies", StringComparison.OrdinalIgnoreCase) && AssemblyKinds.TryGetValue(first, out var kind) ? kind : null;
        return meaning is null ? null : new Hover(new MarkupContent("markdown", $"**{first}**: {meaning}"), LineRange(file, line));
    }
}
