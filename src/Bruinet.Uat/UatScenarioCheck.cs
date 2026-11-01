using System.Runtime.CompilerServices;
using System.Text;
using Bruinet.Core.Composition;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Bruinet.Uat;

/// <summary>
/// Checks scenario files without running them: the same parser, binder and value rules as the
/// xUnit gate, against a catalog built from the fixture's type. Used by the <c>bruinet data</c> tool.
/// </summary>
public static partial class UatScenarioCheck
{
    /// <summary>The catalogs a suite's steps bind against, from its fixture type and config.</summary>
    /// <param name="fixtureType">The fixture the config's <c>Fixture</c> names.</param>
    /// <param name="config">The suite's config, for the <c>Apps</c> table and the head.</param>
    /// <remarks>
    /// An <see cref="IUatAppsHost"/> names each head's modules (<see cref="IUatAppsHost.ModulesFor"/>);
    /// they are asked of an uninitialized fixture, so a head whose modules need a started fixture
    /// binds against the suite's catalog.
    /// </remarks>
    public static BddCatalogSet CatalogsFor(Type fixtureType, BddConfig config)
    {
        ArgumentNullException.ThrowIfNull(fixtureType);
        ArgumentNullException.ThrowIfNull(config);

        var runtime = UatReflectionRuntime.ForChecking(fixtureType);
        var catalog = runtime.CreateCommandCatalog();
        if (typeof(IBddSessionHost).IsAssignableFrom(fixtureType))
        {
            UatSessionSteps.Register(catalog, host: null, config);
        }

        var suiteHead = config.Runtime.TryGetValue(BddConfigFields.Head, out var head) && head.Trim() is { Length: > 0 } trimmed ? trimmed : null;
        List<UatReflectionRuntime> runtimes = [runtime];
        Dictionary<string, BddCommandCatalog> byHead = new(StringComparer.OrdinalIgnoreCase);
        if (typeof(IUatAppsHost).IsAssignableFrom(fixtureType))
        {
            var host = (IUatAppsHost)RuntimeHelpers.GetUninitializedObject(fixtureType);
            foreach (var app in config.Apps)
            {
                if ((app.Head ?? suiteHead) is not { } appHead || byHead.ContainsKey(appHead) || TryModulesFor(host, app) is not { } modules)
                {
                    continue;
                }

                var headRuntime = UatReflectionRuntime.ForChecking(fixtureType, TestCompositionCatalog.Discover(modules));
                var headCatalog = headRuntime.CreateCommandCatalog();
                UatSessionSteps.Register(headCatalog, host: null, config);
                byHead[appHead] = headCatalog;
                runtimes.Add(headRuntime);
            }
        }

        var reserved = runtimes.SelectMany(r => r.Pages).SelectMany(page => page.Controls.Select(control => control.Name).Append(page.Name));
        return new BddCatalogSet(catalog, config.Apps, suiteHead, byHead, reserved) { Usings = config.Usings };
    }

    private static TestCompositionDiscoveryOptions? TryModulesFor(IUatAppsHost host, BddAppConfig app)
    {
        try
        {
            return host.ModulesFor(new TestAppDefinition(app.Name, app.User, app.Head, app.Values));
        }
        catch (Exception exception) when (exception is NotSupportedException or NullReferenceException or InvalidOperationException)
        {
            // A head whose modules need a started fixture: its steps bind against the suite's catalog.
            return null;
        }
    }

    /// <summary>Every problem in a scenario file: parse (<c>UAT0xx</c>), bind (<c>UATB0xx</c>) and values (<c>UATA0xx</c>).</summary>
    public static IReadOnlyList<BddDiagnostic> CheckFile(string filePath, BddCatalogSet catalogs)
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        return BddScenarioFiles.Load(filePath).Check(catalogs).Diagnostics;
    }

    /// <summary>
    /// The config's <c>## Using</c> names that nothing provides (<c>UATB011</c>): checked once per
    /// suite, as the binder checks a file's <c>Using</c> row.
    /// </summary>
    /// <param name="catalogs">The suite's catalogs, with <see cref="BddCatalogSet.Usings"/>.</param>
    /// <param name="phraseFiles">Every phrase file of the suite, whose folders are namespaces too.</param>
    /// <param name="configPath">The config, for the diagnostics' location.</param>
    public static IReadOnlyList<BddDiagnostic> CheckConfigUsings(BddCatalogSet catalogs, IEnumerable<string> phraseFiles, string configPath)
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        ArgumentNullException.ThrowIfNull(phraseFiles);
        var callables = new BddCallableSet(phraseFiles.SelectMany(file => BddScenarioFiles.ParsePhraseFile(file).Document?.Callables ?? []));
        var provided = catalogs.ProvidedNamespaces(callables).ToList();
        return catalogs.Usings
            .Where(name => !provided.Any(owner => BddNamespaces.IsVisible(owner, name)))
            .Select(name => new BddDiagnostic(
                BddDiagnosticSeverity.Error,
                "UATB011",
                $"## Using names '{name}', which no phrase class, page or phrase folder provides. The namespaces are {(provided.Count == 0 ? "(none)" : string.Join(", ", provided.Order(StringComparer.OrdinalIgnoreCase)))}.",
                new BddSourceLocation(configPath, 1)))
            .ToList();
    }

    /// <summary>
    /// A phrase file's problems: parse errors, and each broken callable as a warning at its heading.
    /// A call to a broken callable is the error, in the scenario that makes it.
    /// </summary>
    public static IReadOnlyList<BddDiagnostic> CheckPhraseFile(string filePath, BddCatalogSet catalogs)
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        return BddScenarioFiles.CheckPhrases(BddScenarioFiles.ParsePhraseFile(filePath), catalogs);
    }

    /// <summary>The callable phrases as the phrase list shows them: types, where each is declared, and problems.</summary>
    public static IReadOnlyList<string> DescribeCallables(IEnumerable<BddPreparedCallable> callables)
    {
        ArgumentNullException.ThrowIfNull(callables);
        return callables
            .OrderBy(callable => callable.Callable.Phrase, StringComparer.Ordinal)
            .Select(callable => $"Callable {callable}  <- {callable.Callable.Source}"
                + (callable.Callable.Namespace.Length > 0 ? $"  in {callable.Callable.Namespace}" : string.Empty)
                + (callable.Problem is { } problem ? $"  warning {problem.Code}: {problem.Message}" : string.Empty))
            .ToList();
    }

    /// <summary>
    /// The phrases a scenario file can use, grouped by where they come from: every scenario, its
    /// folders, the namespaces it uses, its own callables, and each page's own phrases. What an
    /// editor offers at a step in that file.
    /// </summary>
    public static IReadOnlyList<string> DescribeVisible(string filePath, BddCatalogSet catalogs, BddEffectiveStepKeyword? keyword = null)
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        var file = BddScenarioFiles.Load(filePath);
        if (file.Document is null)
        {
            return [.. file.Diagnostics.Select(d => $"{d.Location}: {d.Code} {d.Message}")];
        }

        var scope = BddScope.Of(file.Document, catalogs);
        string Group(string? owner) => BddNamespaces.Normalize(owner) is { Length: > 0 } name
            ? scope.UsingsFor(name).Any() ? $"{name} (Using)" : name
            : "Every scenario";

        var catalog = catalogs.Default;
        var phrases = catalog.Patterns
            .Where(pattern => (keyword is null || pattern.Keyword == keyword) && scope.Sees(pattern.Namespace))
            .Where(pattern => pattern.Page is null || UatPageSet.Of(catalog).TryGetValue(pattern.Page, out var page) && scope.Sees(page.Namespace))
            .Select(pattern => (Group: pattern.Page is { } page ? $"Page {page}" : pattern.ControlVerb is not null ? "Controls on the current page" : Group(pattern.Namespace), Line: Describe(pattern)));
        var prepared = file.Callables.Prepare(catalogs);
        var callables = file.Callables.Callables
            .Where(scope.Sees)
            .Select(callable => (Group: callable.LocalTo is not null ? "This file" : Group(callable.Namespace), Line: $"Callable {prepared.Get(callable)}  <- {callable.Source}"));

        return phrases.Concat(callables)
            .GroupBy(entry => entry.Group, StringComparer.Ordinal)
            .OrderBy(group => GroupOrder(group.Key))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group.Select(entry => entry.Line).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Prepend($"## {group.Key}"))
            .ToList();

        static int GroupOrder(string group) => group switch
        {
            "This file" => 0,
            "Every scenario" => 3,
            "Controls on the current page" => 4,
            _ when group.StartsWith("Page ", StringComparison.Ordinal) => 5,
            _ when group.EndsWith("(Using)", StringComparison.Ordinal) => 2,
            _ => 1,
        };
    }


    /// <summary>The text of a file with every diagnostic's <see cref="BddDiagnostic.Fix"/> applied on its line.</summary>
    /// <returns>The new text, and how many values it rewrote.</returns>
    public static (string Text, int Fixed) ApplyFixes(string text, IEnumerable<BddDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
        var count = 0;
        foreach (var diagnostic in diagnostics.Where(d => d.Fix is not null))
        {
            var index = diagnostic.Location.LineNumber - 1;
            var fix = diagnostic.Fix!;
            if (index < 0 || index >= lines.Length || lines[index].IndexOf(fix.Original, StringComparison.Ordinal) is var at && at < 0)
            {
                continue;
            }

            lines[index] = lines[index][..at] + fix.Replacement + lines[index][(at + fix.Original.Length)..];
            count++;
        }

        return (string.Join(newline, lines), count);
    }

    /// <summary>The phrases of a catalog as the phrase list shows them, with types and problems.</summary>
    public static IReadOnlyList<string> DescribePhrases(BddCommandCatalog catalog, BddEffectiveStepKeyword? keyword = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var lines = catalog.Patterns
            .Where(pattern => keyword is null || pattern.Keyword == keyword)
            .OrderBy(pattern => pattern.Keyword)
            .ThenBy(pattern => pattern.Phrase, StringComparer.Ordinal)
            .Select(Describe)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        lines.AddRange(catalog.Unmatchable
            .Where(declaration => keyword is null || declaration.Keyword == keyword)
            .Select(declaration => $"{declaration.Keyword} (no phrase)  <- {declaration.Problem.Declaration}  warning {declaration.Problem.Code}: {declaration.Problem.Message}"));
        return lines;
    }

    /// <summary>A method for a step that binds to nothing: its phrase in the method name, the values as parameters.</summary>
    public static string Snippet(BddStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        List<string> words = [];
        List<string> phrase = [];
        List<string> parameters = [];
        var encodable = true;
        foreach (Match token in TokenRegex().Matches(step.Text))
        {
            if (token.Groups["word"].Success)
            {
                var word = token.Value;
                encodable &= word.All(char.IsLetterOrDigit);
                words.Add(word);
                phrase.Add(word);
                continue;
            }

            var name = $"p{parameters.Count + 1}";
            parameters.Add($"{TypeOf(token)} {name}");
            words.Add(name.ToUpperInvariant());
            phrase.Add($"{{{name}}}");
        }

        if (step.Table is not null)
        {
            parameters.Add("BddTable table");
        }
        else if (step.DocString is not null)
        {
            parameters.Add("string text");
        }

        var attribute = $"Bdd{step.EffectiveKeyword}";
        var signature = string.Join(", ", parameters);
        var builder = new StringBuilder();
        if (encodable)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"[{attribute}]");
            builder.AppendLine(CultureInfo.InvariantCulture, $"public void {step.EffectiveKeyword}_{string.Join('_', words)}({signature})");
        }
        else
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"[{attribute}(\"{string.Join(' ', phrase).Replace("\"", "\\\"", StringComparison.Ordinal)}\")]");
            builder.AppendLine(CultureInfo.InvariantCulture, $"public void {string.Concat(words.Where(word => !word.StartsWith('P') || !word.Skip(1).All(char.IsDigit)).Select(Pascal))}({signature})");
        }

        builder.AppendLine("{");
        builder.Append('}');
        return builder.ToString();
    }

    private static string Describe(BddCommandPattern pattern)
    {
        var text = pattern.Phrase;
        foreach (var parameter in pattern.Parameters.Where(p => p.Source == BddParameterSource.Phrase))
        {
            text = text.Replace($"{{{parameter.Name}}}", parameter.ToString(), StringComparison.Ordinal);
        }

        var below = pattern.Parameters.FirstOrDefault(p => p.Source != BddParameterSource.Phrase) is { } block
            ? block.Source == BddParameterSource.DocString ? $"  + doc string '{block.Name}'" : $"  + table '{block.Name}': {BddValues.Describe(block.Type)}"
            : string.Empty;
        var problem = pattern.Problem is { } broken ? $"  warning {broken.Code}: {broken.Message}" : string.Empty;
        var scope = pattern.Namespace.Length > 0 ? $"  in {pattern.Namespace}" : string.Empty;
        return $"{pattern.Keyword} {text}{below}  <- {pattern.CommandId}{scope}{problem}";
    }

    private static string TypeOf(Match token)
    {
        var text = token.Value;
        return token.Groups["quoted"].Success ? "string"
            : token.Groups["date"].Success ? "DateOnly"
            : token.Groups["number"].Success ? text.Contains('.', StringComparison.Ordinal) ? "decimal" : "int"
            : "string";
    }

    private static string Pascal(string word)
        => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + new string(word[1..].Where(char.IsLetterOrDigit).ToArray());

    [GeneratedRegex(@"(?<quoted>""(?:[^""\\]|\\.)*"")|(?<date>\b\d{1,2}-[A-Za-z]{3,}-\d{4}\b|\b\d{4}-\d{2}-\d{2}\b)|(?<number>(?<![\w-])-?\d[\d,]*(?:\.\d+)?\b)|(?<word>[^\s""]+)")]
    private static partial Regex TokenRegex();
}
