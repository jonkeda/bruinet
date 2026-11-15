using StreamJsonRpc;

namespace Bruinet.Bdd.LanguageServer;

// CodeLens from C# (phase 4): the steps across every suite that use a step definition, a page
// phrase, or a page object's control.
public sealed partial class BddLanguageServer
{
    /// <summary><c>bruinet/usages</c>: the steps that bind to a C# method, or that use a page's control.</summary>
    [JsonRpcMethod("bruinet/usages")]
    public UsagesResult Usages(UsagesParams request)
    {
        var symbol = request.Symbol.Split('(')[0].Trim();
        var dot = symbol.LastIndexOf('.');
        if (dot <= 0)
        {
            return new UsagesResult(0, 0, []);
        }

        var typeName = symbol[..dot];
        var member = symbol[(dot + 1)..];
        List<string> files;
        lock (_gate)
        {
            files = (_workspace?.Suites ?? [])
                .SelectMany(suite => suite.FeatureFiles.Concat(suite.LinkedFiles))
                .Where(file => BddFileFormat.IsScenarioFile(file))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        Dictionary<(string File, int Line), Usage> usages = [];
        HashSet<(string File, int Line)> scenarios = [];
        foreach (var path in files)
        {
            var file = Context(path);
            if (file.Catalogs is not { } catalogs)
            {
                continue;
            }

            // A control of a page: its UAT name, or its property's name (SyncButton for Sync).
            var page = UatPageSet.Of(catalogs.Default).Values.FirstOrDefault(model => Dotted(model.PageType.FullName) == typeName);
            var control = page?.Controls.FirstOrDefault(model => SameName(model.Name, member));
            foreach (var scenario in Analyze(file).Scenarios)
            {
                foreach (var invocation in scenario.Invocations)
                {
                    var uses = control is not null
                        ? invocation.Command.ControlVerb is not null &&
                          string.Equals(invocation.Arguments.GetValueOrDefault("control"), control.Name, StringComparison.Ordinal) &&
                          PageAt(file, WrittenLine(invocation)) is { } on && (on == page!.Key || PageName(on) == page.Name)
                        : Dotted(invocation.CommandId) == symbol;
                    if (!uses || invocation.Step.Source.FilePath is not { } written)
                    {
                        continue;
                    }

                    var at = (Path.GetFullPath(written), invocation.Step.Source.LineNumber);
                    var text = string.Equals(at.Item1, file.Path, StringComparison.OrdinalIgnoreCase) ? file.Line(at.Item2) : Context(at.Item1).Line(at.Item2);
                    usages.TryAdd(at, new Usage(new Uri(at.Item1).AbsoluteUri, at.Item2 - 1, text.Trim(), scenario.Source.Name));
                    scenarios.Add((file.Path, scenario.Source.Source.LineNumber));
                }
            }
        }

        return new UsagesResult(usages.Count, scenarios.Count, usages.Values.OrderBy(usage => usage.Uri, StringComparer.OrdinalIgnoreCase).ThenBy(usage => usage.Line).ToList());
    }

    // CodeLens writes a nested type with a dot; reflection with a plus.
    private static string Dotted(string? name) => (name ?? string.Empty).Replace('+', '.');

    private static bool SameName(string control, string member)
    {
        var name = control.Replace(" ", string.Empty, StringComparison.Ordinal);
        return string.Equals(name, member, StringComparison.OrdinalIgnoreCase) ||
               (member.StartsWith(name, StringComparison.OrdinalIgnoreCase) && member.Length > name.Length && char.IsUpper(member[name.Length]));
    }
}
