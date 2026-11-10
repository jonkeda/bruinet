using System.Reflection;
using Bruinet.Core.Composition;
using Microsoft.Extensions.DependencyInjection;

namespace Bruinet.Bdd;

/// <summary>
/// The step definitions of a suite that are not about a screen: the fixture's own methods and the
/// step-definition classes of its composition, found by reflection, with their arguments read from
/// the step and their calls made.
/// </summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed class BddStepDefinitionRuntime
{
    private const string ServiceProviderItemKey = "Uat.Reflection.ServiceProvider";
    private readonly object _root;
    private readonly Type _rootType;
    private readonly IReadOnlyList<BddPhraseClassBinding> _phraseClasses;
    private readonly BddArgumentTypes _argumentTypes;

    /// <summary>Creates the runtime over a fixture, or over its type only for checking.</summary>
    /// <param name="root">The fixture; null when built for checking, whose handlers never run.</param>
    /// <param name="rootType">The fixture's type.</param>
    /// <param name="phraseClasses">The step-definition classes of its composition.</param>
    /// <param name="argumentTypes">The argument types its steps can read.</param>
    public BddStepDefinitionRuntime(object? root, Type rootType, IEnumerable<BddPhraseClassBinding>? phraseClasses, BddArgumentTypes argumentTypes)
    {
        ArgumentNullException.ThrowIfNull(rootType);
        ArgumentNullException.ThrowIfNull(argumentTypes);
        _root = root!;
        _rootType = rootType;
        _phraseClasses = phraseClasses?.ToArray() ?? [];
        _argumentTypes = argumentTypes;
    }

    /// <summary>The step-definition classes and their methods.</summary>
    public IReadOnlyList<BddPhraseClassBinding> PhraseClasses => _phraseClasses;

    /// <summary>The argument types its steps can read.</summary>
    public BddArgumentTypes ArgumentTypes => _argumentTypes;

    /// <summary>The scope the step-definition classes are resolved from, for one scenario.</summary>
    /// <param name="context">The scenario's execution context.</param>
    /// <param name="serviceProvider">The scenario's scope.</param>
    public static void SetServiceProvider(BddExecutionContext context, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        context.AppItems[ServiceProviderItemKey] = serviceProvider;
    }

    /// <summary>The runtime over a fixture and the step-definition classes given, without a composition.</summary>
    /// <param name="root">The fixture, whose own attributed methods are steps too.</param>
    /// <param name="stepDefinitionTypes">The step-definition classes; resolved from the scope set with <see cref="SetServiceProvider"/>.</param>
    public static BddStepDefinitionRuntime FromTypes(object root, params Type[] stepDefinitionTypes)
    {
        ArgumentNullException.ThrowIfNull(root);
        var types = stepDefinitionTypes.Concat(LoadableTypes(root.GetType().Assembly)).ToArray();
        var argumentTypes = BddArgumentTypes.Discover(types);
        return new BddStepDefinitionRuntime(root, root.GetType(), DiscoverPhraseClasses(stepDefinitionTypes, argumentTypes), argumentTypes);
    }

    /// <summary>A catalog of the step-definition classes' steps and hooks and the fixture's steps.</summary>
    public BddCommandCatalog CreateCommandCatalog()
    {
        var catalog = new BddCommandCatalog { ArgumentTypes = _argumentTypes };
        RegisterPhraseClasses(catalog);
        RegisterRootPhrases(catalog);
        return catalog;
    }

    internal void RegisterPhraseClasses(BddCommandCatalog catalog)
    {
        RegisterPhraseClassPhrases(
            catalog,
            _phraseClasses,
            (phraseClassType, method) => (context, invocation, cancellationToken) =>
                InvokePhraseClassMethodAsync(
                    phraseClassType,
                    method,
                    context,
                    invocation,
                    cancellationToken));

        foreach (var phraseClass in _phraseClasses)
        {
            foreach (var hook in phraseClass.Hooks)
            {
                catalog.AddHook(new BddHook(
                    hook.Kind,
                    $"{phraseClass.Type.Name}.{hook.Method.Name}",
                    hook.Tags,
                    (context, result, cancellationToken) => InvokeHookAsync(phraseClass.Type, hook.Method, context, result, cancellationToken)));
            }
        }
    }

    // A hook takes what it names of the context, the step's result and the cancellation token.
    private static async Task InvokeHookAsync(Type type, MethodInfo method, BddExecutionContext context, BddStepResult? result, CancellationToken cancellationToken)
    {
        object? instance = null;
        if (!method.IsStatic)
        {
            if (!TryGetServiceProvider(context, out var serviceProvider))
            {
                throw new InvalidOperationException($"The hook {type.Name}.{method.Name} requires an active DI scope.");
            }

            instance = serviceProvider.GetService(type) ?? ActivatorUtilities.CreateInstance(serviceProvider, type);
        }

        var arguments = method.GetParameters().Select(parameter =>
            parameter.ParameterType == typeof(BddExecutionContext) ? context
            : parameter.ParameterType == typeof(BddStepResult) ? result
            : parameter.ParameterType == typeof(CancellationToken) ? (object?)cancellationToken
            : throw new InvalidOperationException($"The hook {type.Name}.{method.Name} takes '{parameter.Name}', which no hook gets: it may take a BddExecutionContext, a BddStepResult and a CancellationToken.")).ToArray();

        if (BddInvoker.InvokeDefinition(instance!, method, arguments) is Task task)
        {
            await task.ConfigureAwait(false);
        }
    }

    internal static void RegisterPhraseClassPhrases(
        BddCommandCatalog catalog,
        IEnumerable<BddPhraseClassBinding> phraseClasses,
        Func<Type, MethodInfo, BddCommandHandler?>? handlerFactory)
    {
        foreach (var phraseClass in phraseClasses)
        {
            foreach (var method in phraseClass.Methods)
            {
                foreach (var phrase in method.Phrases)
                {
                    catalog.Register(
                        phrase.Keyword,
                        phrase.Phrase,
                        $"{method.Method.DeclaringType!.FullName}.{method.Method.Name}",
                        handler: handlerFactory?.Invoke(phraseClass.Type, method.Method),
                        parameters: phrase.Parameters,
                        problem: phrase.Problem,
                        phraseNamespace: phraseClass.Namespace,
                        isState: phrase.IsState);
                }

                foreach (var unmatchable in method.Unmatchable)
                {
                    catalog.AddUnmatchable(unmatchable);
                }
            }
        }
    }

    internal void RegisterRootPhrases(BddCommandCatalog catalog)
    {
        RegisterRootPhrases(
            catalog,
            _rootType,
            method => (context, invocation, cancellationToken) =>
                InvokeRootPhraseAsync(method, context, invocation, cancellationToken));
    }

    internal static void RegisterRootPhrases(
        BddCommandCatalog catalog,
        Type rootType,
        Func<MethodInfo, BddCommandHandler?>? handlerFactory)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(rootType);

        foreach (var method in rootType
                     .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                     .Where(method => method.GetCustomAttribute<BddIgnoreAttribute>() is null))
        {
            // On the fixture only an attribute makes a phrase: a public method that happens to
            // start with When stays a method.
            var (phrases, unmatchable) = BddPhraseDeclarations.Of(method, inferWithoutAttribute: false, catalog.ArgumentTypes);
            var commandId = $"{method.DeclaringType!.FullName}.{method.Name}";
            foreach (var phrase in phrases)
            {
                // A global phrase class's phrase takes precedence over the fixture's. A page phrase
                // or a namespace's phrase of another method does not: the same text there and here
                // is ambiguous where both are seen (D5).
                if (catalog.Patterns.Any(pattern =>
                        ((!pattern.IsPageScoped && pattern.Namespace.Length == 0) || pattern.CommandId == commandId) &&
                        pattern.Keyword == phrase.Keyword &&
                        pattern.Phrase.Equals(phrase.Phrase, StringComparison.Ordinal)))
                {
                    continue;
                }

                catalog.Register(
                    phrase.Keyword,
                    phrase.Phrase,
                    commandId,
                    handler: handlerFactory?.Invoke(method),
                    parameters: phrase.Parameters,
                    problem: phrase.Problem,
                    isState: phrase.IsState);
            }

            foreach (var declaration in unmatchable)
            {
                catalog.AddUnmatchable(declaration);
            }
        }
    }

    private async Task<BddStepResult> InvokeRootPhraseAsync(
        MethodInfo method,
        BddExecutionContext context,
        BddStepInvocation invocation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var result = BddInvoker.InvokeDefinition(_root, method, BuildPhraseArguments(method, context, invocation, cancellationToken));
            if (result is Task<BddStepResult> resultTask)
            {
                return await resultTask.ConfigureAwait(false);
            }

            if (result is Task task)
            {
                await task.ConfigureAwait(false);
                context.Diagnostics.Add($"custom:{method.Name}");
                return BddStepResult.Passed(invocation);
            }

            if (result is BddStepResult stepResult)
            {
                return stepResult;
            }

            if (result is bool boolean && !boolean)
            {
                return BddStepResult.Failed(invocation, $"Custom BDD step '{method.Name}' returned false.");
            }

            context.Diagnostics.Add($"custom:{method.Name}");
            return BddStepResult.Passed(invocation, result as string);
        }
        catch (Exception ex)
        {
            return BddStepResult.Failed(invocation, $"Custom BDD step '{method.Name}' failed: {ex.Message}", ex);
        }
    }

    private async Task<BddStepResult> InvokePhraseClassMethodAsync(
        Type phraseClassType,
        MethodInfo method,
        BddExecutionContext context,
        BddStepInvocation invocation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetServiceProvider(context, out var serviceProvider))
        {
            return BddStepResult.Failed(
                invocation,
                $"UAT phrase class '{phraseClassType.FullName}' requires an active DI scope.");
        }

        try
        {
            var instance = serviceProvider.GetService(phraseClassType) ??
                           ActivatorUtilities.CreateInstance(serviceProvider, phraseClassType);
            var result = BddInvoker.InvokeDefinition(instance, method, BuildPhraseArguments(method, context, invocation, cancellationToken));
            if (result is Task<BddStepResult> resultTask)
            {
                return await resultTask.ConfigureAwait(false);
            }

            if (result is Task task)
            {
                await task.ConfigureAwait(false);
                context.Diagnostics.Add($"phrase:{method.Name}");
                return BddStepResult.Passed(invocation);
            }

            if (result is BddStepResult stepResult)
            {
                return stepResult;
            }

            if (result is bool boolean && !boolean)
            {
                return BddStepResult.Failed(invocation, $"UAT phrase '{method.Name}' returned false.");
            }

            context.Diagnostics.Add($"phrase:{method.Name}");
            return BddStepResult.Passed(invocation, result as string);
        }
        catch (Exception ex)
        {
            return BddStepResult.Failed(invocation, $"UAT phrase '{method.Name}' failed: {ex.Message}", ex);
        }
    }

    internal object?[] BuildPhraseArguments(
        MethodInfo method,
        BddExecutionContext context,
        BddStepInvocation invocation,
        CancellationToken cancellationToken)
    {
        var parameters = method.GetParameters();
        var arguments = new object?[parameters.Length];
        var values = ValueContext(context);
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (parameter.ParameterType == typeof(BddExecutionContext))
            {
                arguments[i] = context;
                continue;
            }

            if (parameter.ParameterType == typeof(BddStepInvocation))
            {
                arguments[i] = invocation;
                continue;
            }

            if (parameter.ParameterType == typeof(CancellationToken))
            {
                arguments[i] = cancellationToken;
                continue;
            }

            if (parameter.ParameterType == typeof(BddTable))
            {
                arguments[i] = invocation.Step.Table
                    ?? (parameter.IsOptional
                        ? null
                        : throw new InvalidOperationException(
                            $"Custom BDD step '{method.Name}' needs a table under the step ({invocation.Step.Source})."));
                continue;
            }

            if (BddPhraseDeclarations.IsSettingsType(parameter.ParameterType, method.DeclaringType?.Assembly))
            {
                arguments[i] = context.GetSettings(parameter.ParameterType);
                continue;
            }

            if (invocation.Command.Parameters.FirstOrDefault(p => p.Name == parameter.Name) is { } typed)
            {
                arguments[i] = BddArguments.Read(typed, invocation, values);
                continue;
            }

            // A pattern registered without types (by hand, or by an older catalog): text as before.
            if (parameter.Name is not null &&
                invocation.RawArguments.TryGetValue(parameter.Name, out var value))
            {
                arguments[i] = BddValues.Read(value, parameter.ParameterType, values);
                continue;
            }

            arguments[i] = parameter.IsOptional
                ? Type.Missing
                : throw new InvalidOperationException(
                    $"Custom BDD step '{method.Name}' requires parameter '{parameter.Name}' but no matching phrase argument was found.");
        }

        return arguments;
    }

    internal BddValueContext ValueContext(BddExecutionContext context)
        => new(
            context.Now ?? context.Clock?.Now ?? DateTimeOffset.UtcNow,
            context.Apps,
            _argumentTypes,
            TryGetServiceProvider(context, out var services) ? services : null);

    internal static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }

    internal static IReadOnlyList<BddPhraseClassBinding> DiscoverPhraseClasses(IReadOnlyList<Type> types, BddArgumentTypes argumentTypes)
    {
        List<BddPhraseClassBinding> phraseClasses = [];

        foreach (var type in types.Where(IsPhraseClassType))
        {
            List<BddPhraseClassMethodBinding> methods = [];
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName ||
                    method.GetCustomAttribute<BddIgnoreAttribute>() is not null)
                {
                    continue;
                }

                var (phrases, unmatchable) = BddPhraseDeclarations.Of(method, inferWithoutAttribute: true, argumentTypes);
                if (phrases.Count == 0 && unmatchable.Count == 0)
                {
                    continue;
                }

                methods.Add(new BddPhraseClassMethodBinding(
                    method,
                    phrases.Select(phrase => new BddPhraseMethodPhrase(phrase.Phrase, phrase.Keyword)
                    {
                        Parameters = phrase.Parameters,
                        Problem = phrase.Problem,
                        IsState = phrase.IsState,
                    }).ToArray())
                {
                    Unmatchable = unmatchable,
                });
            }

            var hooks = BddHooks.Discover(type);
            if (methods.Count > 0 || hooks.Count > 0)
            {
                phraseClasses.Add(new BddPhraseClassBinding(type, methods) { Namespace = NamespaceOf(type), Hooks = hooks });
            }
        }

        return phraseClasses;
    }

    // The phrase class's own namespace, else its [BddNamespace] or its assembly's; the root without any.
    private static string NamespaceOf(Type type)
        => BddNamespaces.Normalize(
            type.GetCustomAttribute<BddStepDefinitionsAttribute>()?.Namespace ??
            BddNamespaceAttribute.Of(type));

    private static bool IsPhraseClassType(Type type)
    {
        return type.GetCustomAttribute<BddStepDefinitionsAttribute>() is not null ||
               typeof(BddStepDefinitionsBase).IsAssignableFrom(type);
    }

    internal static bool TryGetServiceProvider(BddExecutionContext context, out IServiceProvider serviceProvider)
    {
        if (context.AppItems.TryGetValue(ServiceProviderItemKey, out var value) &&
            value is IServiceProvider found)
        {
            serviceProvider = found;
            return true;
        }

        serviceProvider = null!;
        return false;
    }
}

[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed record BddPhraseClassBinding(
    Type Type,
    IReadOnlyList<BddPhraseClassMethodBinding> Methods)
{
    /// <summary>The namespace its phrases are seen in (<see cref="BddNamespaces"/>); <c>""</c> for every scenario.</summary>
    public string Namespace { get; init; } = string.Empty;

    internal IReadOnlyList<BddHookMethod> Hooks { get; init; } = [];
}

[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed record BddPhraseClassMethodBinding(
    MethodInfo Method,
    IReadOnlyList<BddPhraseMethodPhrase> Phrases)
{
    /// <summary>The method's broken declarations that have no phrase to match.</summary>
    public IReadOnlyList<BddUnmatchableDeclaration> Unmatchable { get; init; } = [];
}

[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed record BddPhraseMethodPhrase(
    string Phrase,
    BddEffectiveStepKeyword Keyword)
{
    /// <summary>What a step supplies, with types.</summary>
    public IReadOnlyList<BddPhraseParameter> Parameters { get; init; } = [];

    /// <summary>Why the phrase cannot run; null when it is fine.</summary>
    public BddPhraseProblem? Problem { get; init; }

    /// <summary>Whether it is a declared state (<c>[BddState]</c>).</summary>
    public bool IsState { get; init; }
}
