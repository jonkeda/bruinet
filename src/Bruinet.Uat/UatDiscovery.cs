using System.Reflection;
using Bruinet.Core.Composition;
using Bruinet.Core.Interfaces;

namespace Bruinet.Uat;

public sealed class UatDiscoveryOptions
{
    public bool RequireExplicitUatAttributes { get; init; }

    public bool AllowNameInference { get; init; } = true;
}

public static class UatDiscovery
{
    public static UatDiscoveryResult Discover(
        UatDiscoveryOptions options,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(assemblies);

        List<BddDiagnostic> diagnostics = [];
        List<UatPageBinding> pages = [];
        List<UatCommandBinding> commands = [];
        var catalog = new BddCommandCatalog();

        var types = assemblies
            .SelectMany(GetLoadableTypes)
            .Where(type => type is { IsAbstract: false, IsClass: true } && type.IsVisible)
            .Where(type => type.GetCustomAttribute<BddIgnoreAttribute>() is null)
            .ToArray();

        foreach (var type in types.Where(type => IsPageType(type, options)))
        {
            var pageName = ResolveName(type, options);
            if (pageName is null)
            {
                continue;
            }

            var controls = DiscoverControls(type, options, diagnostics);
            pages.Add(new UatPageBinding(pageName, type, controls));
        }

        // A step on a control method is a control verb, not a phrase.
        var controlTypes = pages
            .SelectMany(page => page.Controls)
            .Select(control => control.ControlType)
            .ToHashSet();

        foreach (var method in types
                     .Where(type => !controlTypes.Contains(type) && !IsControlObjectType(type))
                     .SelectMany(GetCandidateMethods))
        {
            // A broken phrase stays in the catalog and fails the step that uses it (AD-016); here it
            // is only a warning.
            var (phrases, unmatchable) = BddPhraseDeclarations.Of(method, inferWithoutAttribute: false);
            foreach (var phrase in phrases)
            {
                var command = new UatCommandBinding(
                    phrase.Phrase,
                    phrase.Keyword,
                    $"{method.DeclaringType!.FullName}.{method.Name}",
                    method);
                commands.Add(command);
                catalog.Register(phrase.Keyword, phrase.Phrase, command.CommandId, parameters: phrase.Parameters, problem: phrase.Problem);
                if (phrase.Problem is { } problem)
                {
                    diagnostics.Add(new BddDiagnostic(
                        BddDiagnosticSeverity.Warning,
                        problem.Code,
                        $"The phrase '{phrase.Phrase}' from {problem.Declaration} {problem.Message}",
                        new BddSourceLocation(null, 1)));
                }
            }

            foreach (var declaration in unmatchable)
            {
                catalog.AddUnmatchable(declaration);
                diagnostics.Add(new BddDiagnostic(
                    BddDiagnosticSeverity.Warning,
                    declaration.Problem.Code,
                    $"{declaration.Problem.Declaration} {declaration.Problem.Message}",
                    new BddSourceLocation(null, 1)));
            }
        }

        AddDuplicatePageDiagnostics(pages, diagnostics);
        AddDuplicateControlDiagnostics(pages, diagnostics);
        AddDuplicatePhraseDiagnostics(commands, diagnostics);

        return new UatDiscoveryResult(pages, commands, catalog, diagnostics);
    }

    private static IReadOnlyList<UatControlBinding> DiscoverControls(
        Type pageType,
        UatDiscoveryOptions options,
        ICollection<BddDiagnostic> diagnostics)
    {
        List<UatControlBinding> controls = [];
        foreach (var property in pageType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length > 0 ||
                property.GetCustomAttribute<BddIgnoreAttribute>() is not null ||
                !IsControlProperty(property, options))
            {
                continue;
            }

            var name = ResolveName(property, options);
            if (name is null)
            {
                continue;
            }

            var actions = property.PropertyType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .SelectMany(method => method.GetCustomAttributes<UatActionAttribute>())
                .Select(attribute => attribute.ActionName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            controls.Add(new UatControlBinding(name, property.Name, property.PropertyType, property, actions));
        }

        return controls;
    }

    private static bool IsPageType(Type type, UatDiscoveryOptions options)
    {
        if (type.GetCustomAttribute<UatNameAttribute>() is not null)
        {
            return true;
        }

        if (TestPages.IsPage(type))
        {
            return true;
        }

        return options.AllowNameInference &&
               !options.RequireExplicitUatAttributes &&
               type.Name.EndsWith("Page", StringComparison.Ordinal);
    }

    private static bool IsControlProperty(PropertyInfo property, UatDiscoveryOptions options)
    {
        if (property.GetCustomAttribute<UatNameAttribute>() is not null)
        {
            return true;
        }

        if (IsControlObjectType(property.PropertyType))
        {
            return true;
        }

        if (property.PropertyType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Any(method => method.GetCustomAttributes<UatActionAttribute>().Any() ||
                           method.GetCustomAttributes<BddStepAttribute>().Any()))
        {
            return true;
        }

        return options.AllowNameInference &&
               !options.RequireExplicitUatAttributes &&
               (UatNameInference.HasKnownSuffix(property.Name) ||
                UatNameInference.HasKnownSuffix(property.PropertyType.Name));
    }

    private static bool IsControlObjectType(Type type)
    {
        return type.GetInterfaces().Any(IsControlObjectInterface);
    }

    private static bool IsControlObjectInterface(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IControlObject<>);
    }

    private static string? ResolveName(MemberInfo member, UatDiscoveryOptions options)
    {
        var attribute = member.GetCustomAttribute<UatNameAttribute>();
        if (attribute is not null)
        {
            return attribute.Name;
        }

        return options.AllowNameInference && !options.RequireExplicitUatAttributes
            ? member switch
            {
                Type type => UatNameInference.FromType(type),
                _ => UatNameInference.FromMember(member)
            }
            : null;
    }

    private static IEnumerable<MethodInfo> GetCandidateMethods(Type type)
    {
        return type
            .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public)
            .Where(method => method.GetCustomAttribute<BddIgnoreAttribute>() is null)
            .Where(method => method.GetCustomAttributes<BddStepAttribute>().Any());
    }

    private static IReadOnlyList<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>().ToArray();
        }
    }

    private static void AddDuplicatePageDiagnostics(
        IReadOnlyList<UatPageBinding> pages,
        ICollection<BddDiagnostic> diagnostics)
    {
        foreach (var duplicate in pages.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
        {
            diagnostics.Add(new BddDiagnostic(
                BddDiagnosticSeverity.Error,
                "UATD001",
                $"Duplicate UAT page name '{duplicate.Key}'.",
                new BddSourceLocation(null, 1)));
        }
    }

    private static void AddDuplicateControlDiagnostics(
        IReadOnlyList<UatPageBinding> pages,
        ICollection<BddDiagnostic> diagnostics)
    {
        foreach (var page in pages)
        {
            foreach (var duplicate in page.Controls.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
            {
                diagnostics.Add(new BddDiagnostic(
                    BddDiagnosticSeverity.Error,
                    "UATD002",
                    $"Duplicate UAT control name '{duplicate.Key}' on page '{page.Name}'.",
                    new BddSourceLocation(null, 1)));
            }
        }
    }

    private static void AddDuplicatePhraseDiagnostics(
        IReadOnlyList<UatCommandBinding> commands,
        ICollection<BddDiagnostic> diagnostics)
    {
        foreach (var duplicate in commands.GroupBy(x => $"{x.Keyword}:{x.Phrase}", StringComparer.Ordinal).Where(x => x.Count() > 1))
        {
            diagnostics.Add(new BddDiagnostic(
                BddDiagnosticSeverity.Error,
                "UATD003",
                $"Duplicate UAT command phrase '{duplicate.First().Phrase}' for keyword '{duplicate.First().Keyword}'.",
                new BddSourceLocation(null, 1)));
        }
    }
}

public sealed record UatDiscoveryResult(
    IReadOnlyList<UatPageBinding> Pages,
    IReadOnlyList<UatCommandBinding> Commands,
    BddCommandCatalog Catalog,
    IReadOnlyList<BddDiagnostic> Diagnostics)
{
    public bool Success => Diagnostics.All(x => x.Severity != BddDiagnosticSeverity.Error);
}

public sealed record UatPageBinding(
    string Name,
    Type PageType,
    IReadOnlyList<UatControlBinding> Controls);

public sealed record UatControlBinding(
    string Name,
    string MemberName,
    Type ControlType,
    PropertyInfo Property,
    IReadOnlyList<string> Actions);

public sealed record UatCommandBinding(
    string Phrase,
    BddEffectiveStepKeyword Keyword,
    string CommandId,
    MethodInfo Method);
