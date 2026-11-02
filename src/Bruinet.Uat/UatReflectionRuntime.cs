using System.Reflection;
using Bruinet.Core.Composition;
using Bruinet.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Bruinet.Uat;

public sealed class UatReflectionRuntime
{
    private const string CurrentPageItemKey = "Uat.Reflection.CurrentPage";
    private readonly Dictionary<string, UatRuntimePage> _pages;
    private readonly BddStepDefinitionRuntime _steps;

    private UatReflectionRuntime(
        object? root,
        IEnumerable<UatRuntimePage> pages,
        IEnumerable<BddPhraseClassBinding>? phraseClasses = null,
        BddArgumentTypes? argumentTypes = null,
        Type? rootType = null)
    {
        // A runtime built for checking has a fixture type and no fixture; its handlers never run.
        _steps = new BddStepDefinitionRuntime(root, rootType ?? root!.GetType(), phraseClasses, argumentTypes ?? BddArgumentTypes.Empty);
        _pages = new Dictionary<string, UatRuntimePage>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in pages)
        {
            _pages[page.Key] = page;
        }
    }

    public IReadOnlyCollection<UatRuntimePage> Pages => _pages.Values;

    public IReadOnlyList<BddPhraseClassBinding> PhraseClasses => _steps.PhraseClasses;

    public IReadOnlyList<string> DescribeDiscovery()
    {
        List<string> lines = ["Discovered UAT pages:"];
        foreach (var page in Pages.OrderBy(page => page.Key, StringComparer.OrdinalIgnoreCase))
        {
            var controls = page.Controls.Count == 0
                ? "(no controls)"
                : string.Join(", ", page.Controls.Select(control => control.Name).Order(StringComparer.OrdinalIgnoreCase));
            lines.Add($"- {page.Key}: {controls}");
        }

        return lines;
    }

    public static UatReflectionRuntime FromRoot(object root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var rootType = root.GetType();
        List<UatRuntimePage> pages = [];

        foreach (var property in rootType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length > 0 ||
                property.GetCustomAttribute<BddIgnoreAttribute>() is not null ||
                !IsRuntimePageProperty(property))
            {
                continue;
            }

            var page = property.GetValue(root);
            if (page is null)
            {
                continue;
            }

            var name = ResolveRuntimeName(property);
            var navigation = FindNavigationMethod(rootType, name);
            pages.Add(new UatRuntimePage(
                name,
                page.GetType(),
                page,
                DiscoverControls(page),
                navigation is null ? null : () => BddInvoker.InvokeDefinition(root, navigation))
            {
                Namespace = BddNamespaces.Normalize(BddNamespaceAttribute.Of(page.GetType())),
            });
        }

        return new UatReflectionRuntime(root, pages, argumentTypes: BddArgumentTypes.Discover(BddStepDefinitionRuntime.LoadableTypes(rootType.Assembly)));
    }

    public static UatReflectionRuntime FromComposition(object root, TestComposition composition)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(composition);

        var rootType = root.GetType();
        var pages = composition.Catalog.Pages
            .Select(page =>
            {
                var navigation = FindNavigationMethod(rootType, page.Name);
                return new UatRuntimePage(
                    page.Name,
                    page.PageType,
                    Instance: null,
                    DiscoverControls(page.PageType),
                    navigation is null ? null : () => BddInvoker.InvokeDefinition(root, navigation))
                {
                    Namespace = BddNamespaces.Normalize(page.Namespace),
                };
            })
            .ToArray();
        var argumentTypes = BddArgumentTypes.Discover(composition.Catalog.ScannedTypes.Concat(BddStepDefinitionRuntime.LoadableTypes(rootType.Assembly)));
        var phraseClasses = BddStepDefinitionRuntime.DiscoverPhraseClasses(composition.Catalog.ScannedTypes, argumentTypes);

        return new UatReflectionRuntime(root, pages, phraseClasses, argumentTypes);
    }

    /// <summary>
    /// The phrases of <paramref name="fixtureType"/> without creating the fixture: for checking
    /// scenario files (the <c>bruinet data</c> tool), not for running them.
    /// </summary>
    /// <param name="fixtureType">The suite's fixture, as the config's <c>Fixture</c> names it.</param>
    /// <param name="modules">A head's modules, for a fixture with several heads; the fixture's own scan when null.</param>
    public static UatReflectionRuntime ForChecking(Type fixtureType, TestCompositionCatalog? modules = null)
    {
        ArgumentNullException.ThrowIfNull(fixtureType);

        var usesComposition = fixtureType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Any(property => property.Name is "Composition" or "TestComposition" && typeof(TestComposition).IsAssignableFrom(property.PropertyType));
        if (!usesComposition && modules is null)
        {
            var rootPages = fixtureType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.GetIndexParameters().Length == 0 &&
                                   property.GetCustomAttribute<BddIgnoreAttribute>() is null &&
                                   IsRuntimePageProperty(property))
                .Select(property => new UatRuntimePage(ResolveRuntimeName(property), property.PropertyType, Instance: null, DiscoverControls(property.PropertyType), Navigate: null)
                {
                    Namespace = BddNamespaces.Normalize(BddNamespaceAttribute.Of(property.PropertyType)),
                });
            return new UatReflectionRuntime(null, rootPages, argumentTypes: BddArgumentTypes.Discover(BddStepDefinitionRuntime.LoadableTypes(fixtureType.Assembly)), rootType: fixtureType);
        }

        var catalog = modules ?? TestCompositionCatalog.Discover(TestCompositionDiscoveryOptions.FromFixture(fixtureType));
        var pages = catalog.Pages.Select(page => new UatRuntimePage(page.Name, page.PageType, Instance: null, DiscoverControls(page.PageType), Navigate: null)
        {
            Namespace = BddNamespaces.Normalize(page.Namespace),
        });
        var argumentTypes = BddArgumentTypes.Discover(catalog.ScannedTypes.Concat(BddStepDefinitionRuntime.LoadableTypes(fixtureType.Assembly)));
        return new UatReflectionRuntime(null, pages, BddStepDefinitionRuntime.DiscoverPhraseClasses(catalog.ScannedTypes, argumentTypes), argumentTypes, fixtureType);
    }

    public static void SetServiceProvider(BddExecutionContext context, IServiceProvider serviceProvider)
        => BddStepDefinitionRuntime.SetServiceProvider(context, serviceProvider);

    public static void RegisterRootPhrases(BddCommandCatalog catalog, Type rootType)
    {
        BddStepDefinitionRuntime.RegisterRootPhrases(catalog, rootType, handlerFactory: null);
    }

    public static void RegisterCompositionPhrases(BddCommandCatalog catalog, Type rootType)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(rootType);

        var options = TestCompositionDiscoveryOptions.FromFixture(rootType);
        var compositionCatalog = TestCompositionCatalog.Discover(options);
        BddStepDefinitionRuntime.RegisterPhraseClassPhrases(
            catalog,
            BddStepDefinitionRuntime.DiscoverPhraseClasses(compositionCatalog.ScannedTypes, BddArgumentTypes.Discover(compositionCatalog.ScannedTypes)),
            handlerFactory: null);
    }

    /// <summary>The built-in step that does nothing: a Step-through paused before it has run every step before it.</summary>
    public const string PausePhrase = "the run pauses here";

    public BddCommandCatalog CreateCommandCatalog()
    {
        var catalog = new BddCommandCatalog { ArgumentTypes = _steps.ArgumentTypes, Scope = new UatPageSet(PageModels()) };

        // The engine's three intrinsic page-graph verbs — defined once, here, each with its named
        // form for a scenario with several users (AD-015).
        foreach (var phrase in new[] { "I am on the {page} page", "{user} is on the {page} page" })
        {
            catalog.Register(BddEffectiveStepKeyword.Given, phrase, "Builtin.Page.Open", handler: OpenPageAsync, namesPage: true);
        }

        foreach (var phrase in new[] { "I should be on the {page} page", "{user} should be on the {page} page" })
        {
            catalog.Register(BddEffectiveStepKeyword.Then, phrase, "Builtin.Page.AssertOpen", handler: AssertPageAsync, namesPage: true);
        }

        foreach (var phrase in new[] { "I should see {text}", "{user} should see {text}" })
        {
            catalog.Register(
                BddEffectiveStepKeyword.Then,
                phrase,
                "Builtin.Page.AssertTextVisible",
                allowsTable: false,
                handler: AssertAnyControlTextContainsAsync);
        }

        // A step that does nothing: where a Step-through stays paused after the steps before it (set_state, AI10).
        catalog.Register(
            BddEffectiveStepKeyword.Then,
            PausePhrase,
            "Builtin.Pause",
            allowsTable: false,
            handler: (_, invocation, _) => Task.FromResult(BddStepResult.Passed(invocation)));

        // Everything control-shaped is discovered from [BddWhen]/[BddThen] on Bruinet.Core interfaces
        // and any decorated app control types found during page discovery.
        UatCatalogBuilder.RegisterControlVerbs(
            catalog,
            ControlStepTypes(),
            "Builtin.",
            binding => (context, invocation, _) => InvokeControlMethodAsync(context, invocation, binding));

        _steps.RegisterPhraseClasses(catalog);
        RegisterPagePhrases(catalog);
        _steps.RegisterRootPhrases(catalog);
        return catalog;
    }

    // The pages as the binder follows them: each control with its type and where tapping it goes.
    private Dictionary<string, UatPageModel> PageModels()
    {
        var nameOf = _pages.Values
            .GroupBy(page => page.PageType)
            .ToDictionary(group => group.Key, group => group.First().Key);
        return _pages.Values.ToDictionary(
            page => page.Key,
            page => new UatPageModel(
                page.Name,
                page.PageType,
                page.Controls
                    .Select(control => new UatControlModel(
                        control.Name,
                        control.Property.PropertyType,
                        control.Property.GetCustomAttribute<UatNavigatesToAttribute>() is { } goesTo
                            ? nameOf.GetValueOrDefault(goesTo.Page) ?? throw new InvalidOperationException(
                                $"{page.PageType.Name}.{control.MemberName} navigates to {goesTo.Page.Name}, which is not a UAT page.")
                            : null)
                    {
                        MemberName = control.MemberName,
                    })
                    .ToArray())
            {
                Namespace = page.Namespace,
            },
            StringComparer.OrdinalIgnoreCase);
    }

    // A page's own workflows: keyword attributes on page-object methods (D4). A method that returns
    // a page object says which page is current after it.
    private void RegisterPagePhrases(BddCommandCatalog catalog)
    {
        foreach (var page in _pages.Values)
        {
            foreach (var method in page.PageType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.IsSpecialName ||
                    !method.IsDefined(typeof(BddStepAttribute), inherit: false) ||
                    method.GetCustomAttribute<BddIgnoreAttribute>() is not null)
                {
                    continue;
                }

                var pageAfter = _pages.Values.FirstOrDefault(candidate => candidate.PageType == method.ReturnType);
                var (phrases, unmatchable) = BddPhraseDeclarations.Of(method, inferWithoutAttribute: false, _steps.ArgumentTypes);
                foreach (var phrase in phrases)
                {
                    catalog.Register(
                        phrase.Keyword,
                        phrase.Phrase,
                        $"{page.PageType.FullName}.{method.Name}",
                        handler: (context, invocation, cancellationToken) =>
                            InvokePagePhraseAsync(page, method, context, invocation, cancellationToken),
                        parameters: phrase.Parameters,
                        problem: phrase.Problem,
                        page: page.Key,
                        pageAfter: pageAfter?.Key);
                }

                foreach (var declaration in unmatchable)
                {
                    catalog.AddUnmatchable(declaration);
                }
            }
        }
    }

    private Task<BddStepResult> InvokePagePhraseAsync(
        UatRuntimePage page,
        MethodInfo method,
        BddExecutionContext context,
        BddStepInvocation invocation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetCurrentPage(context, invocation, out var current))
        {
            return Task.FromResult(current.Result);
        }

        if (!string.Equals(current.Page!.Key, page.Key, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(BddStepResult.Failed(
                invocation,
                $"The step '{invocation.Command.Phrase}' is a phrase of the {page.Name} page, but the current page is {current.Page.Name}. Go to the {page.Name} page first."));
        }

        var instance = GetPageInstance(context, page);
        if (instance is null)
        {
            return Task.FromResult(BddStepResult.Failed(invocation, $"UAT page '{page.Name}' requires an active DI scope."));
        }

        try
        {
            BddInvoker.InvokeDefinition(instance, method, _steps.BuildPhraseArguments(method, context, invocation, cancellationToken));
        }
        catch (Exception ex)
        {
            return Task.FromResult(BddStepResult.Failed(invocation, $"UAT page phrase '{page.Name}.{method.Name}' failed: {ex.Message}", ex));
        }

        context.Diagnostics.Add($"page-phrase:{page.Name}.{method.Name}");
        return Task.FromResult(FollowPage(context, invocation));
    }

    // The page the binder worked out for after the step becomes current, as it does after
    // "I should be on the ... page": the steps that follow were bound against it.
    private BddStepResult FollowPage(BddExecutionContext context, BddStepInvocation invocation)
    {
        if (invocation.PageAfter is not { } name ||
            string.Equals(context.CurrentPageName, name, StringComparison.OrdinalIgnoreCase) ||
            !_pages.TryGetValue(name, out var page))
        {
            return BddStepResult.Passed(invocation);
        }

        var instance = GetPageInstance(context, page);
        if (instance is null || !WaitPageReady(instance))
        {
            return BddStepResult.Failed(invocation, $"The step should have ended on the {page.Name} page, which did not become ready.");
        }

        context.CurrentPageName = page.Key;
        context.AppItems[CurrentPageItemKey] = page;
        return BddStepResult.Passed(invocation);
    }

    private IEnumerable<Type> ControlStepTypes()
    {
        // A verb declared on a control's base class (StateContainer under an app's own state
        // container) is found through the class that the page declares. A page's own phrases are
        // page phrases, even where a page exposes itself as a property (Self).
        var pageTypes = _pages.Values.Select(page => page.PageType).ToHashSet();
        var discovered = _pages.Values
            .SelectMany(page => page.Controls)
            .Where(control => !pageTypes.Contains(control.Property.PropertyType))
            .SelectMany(control => SelfAndBaseTypes(control.Property.PropertyType));
        return UatCatalogBuilder.CoreStepTypes.Concat(discovered).Distinct();
    }

    private static IEnumerable<Type> SelfAndBaseTypes(Type type)
    {
        for (Type? current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            yield return current;
        }
    }

    private static IReadOnlyList<UatRuntimeControl> DiscoverControls(object page)
    {
        return DiscoverControls(page.GetType());
    }

    private static IReadOnlyList<UatRuntimeControl> DiscoverControls(Type pageType)
    {
        List<UatRuntimeControl> controls = [];
        foreach (var property in pageType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length > 0 ||
                property.GetCustomAttribute<BddIgnoreAttribute>() is not null ||
                !IsRuntimeControlProperty(property))
            {
                continue;
            }

            controls.Add(new UatRuntimeControl(
                ResolveRuntimeName(property),
                property.Name,
                property));
        }

        return controls;
    }

    private static bool IsRuntimePageProperty(PropertyInfo property)
    {
        return property.GetCustomAttribute<UatNameAttribute>() is not null ||
               UatNameInference.HasKnownSuffix(property.Name) && property.Name.EndsWith("Page", StringComparison.Ordinal);
    }

    private static bool IsRuntimeControlProperty(PropertyInfo property)
    {
        if (property.GetCustomAttribute<UatNameAttribute>() is not null)
        {
            return true;
        }

        // A page is not a control of another page (a page's Self).
        if (property.PropertyType.IsDefined(typeof(TestPageAttribute), inherit: true))
        {
            return false;
        }

        // A type that declares its own verbs is a control whatever it is called (ContentDialog), and
        // so is one that has Core's control verbs (a native Android Spinner, RadioGroup, TextView).
        return UatNameInference.HasKnownSuffix(property.Name) ||
               UatNameInference.HasKnownSuffix(property.PropertyType.Name) ||
               IsElementObject(property.PropertyType) ||
               property.PropertyType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                   .Any(method => method.IsDefined(typeof(BddStepAttribute), inherit: false));
    }

    private static bool IsElementObject(Type type)
        => type.GetInterfaces().Any(contract => contract.IsGenericType
            && contract.GetGenericTypeDefinition() == typeof(Bruinet.Core.Interfaces.IElementObject<>));

    private static string ResolveRuntimeName(MemberInfo member)
    {
        return member.GetCustomAttribute<UatNameAttribute>()?.Name ??
               UatNameInference.FromMember(member);
    }

    private static MethodInfo? FindNavigationMethod(Type rootType, string pageName)
    {
        var expected = "NavigateTo" + pageName.Replace(" ", string.Empty, StringComparison.Ordinal);
        return rootType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(method =>
                method.Name.Equals(expected, StringComparison.Ordinal) &&
                method.GetParameters().All(parameter => parameter.IsOptional));
    }

    private Task<BddStepResult> OpenPageAsync(
        BddExecutionContext context,
        BddStepInvocation invocation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!invocation.Arguments.TryGetValue("page", out var pageName) ||
            FindPage(pageName) is not { } page)
        {
            return Task.FromResult(BddStepResult.Failed(
                invocation,
                $"UAT page '{pageName}' was not found. Available pages: {FormatPageNames()}."));
        }

        page.Navigate?.Invoke();
        var instance = GetPageInstance(context, page);
        if (instance is null)
        {
            return Task.FromResult(BddStepResult.Failed(
                invocation,
                $"UAT page '{page.Name}' requires an active DI scope."));
        }

        if (!WaitPageReady(instance))
        {
            return Task.FromResult(BddStepResult.Failed(
                invocation,
                $"UAT page '{page.Name}' did not become ready."));
        }

        context.CurrentPageName = page.Key;
        context.AppItems[CurrentPageItemKey] = page;
        context.Diagnostics.Add($"page:{page.Key}");
        return Task.FromResult(BddStepResult.Passed(invocation));
    }

    private Task<BddStepResult> AssertPageAsync(
        BddExecutionContext context,
        BddStepInvocation invocation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!invocation.Arguments.TryGetValue("page", out var pageName) ||
            FindPage(pageName) is not { } page)
        {
            return Task.FromResult(BddStepResult.Failed(
                invocation,
                $"UAT page '{pageName}' was not found. Available pages: {FormatPageNames()}."));
        }

        var instance = GetPageInstance(context, page);
        if (instance is null)
        {
            return Task.FromResult(BddStepResult.Failed(
                invocation,
                $"UAT page '{page.Name}' requires an active DI scope."));
        }

        if (!WaitPageReady(instance))
        {
            return Task.FromResult(BddStepResult.Failed(
                invocation,
                $"UAT page '{page.Name}' was not ready."));
        }

        context.CurrentPageName = page.Key;
        context.AppItems[CurrentPageItemKey] = page;
        return Task.FromResult(BddStepResult.Passed(invocation));
    }

    private Task<BddStepResult> InvokeControlMethodAsync(
        BddExecutionContext context,
        BddStepInvocation invocation,
        UatStepBinding binding)
    {
        var (methodName, valueArgumentName, valueType) = (binding.MethodName, binding.ValueArgumentName, binding.ValueType);
        object? literalArgument = binding.Literal;
        var found = binding.NamesControl
            ? TryGetCurrentControl(context, invocation, UatCatalogBuilder.ControlParameter, out var controlResult)
            : TryGetDeclaringControl(context, invocation, binding, out controlResult);
        if (!found)
        {
            return Task.FromResult(controlResult.Result);
        }

        object? value;
        try
        {
            value = valueArgumentName is null ? literalArgument
                : valueType is null || valueType == typeof(string) ? invocation.Arguments[valueArgumentName]
                : BddValues.Read(invocation.RawArguments[valueArgumentName], valueType, _steps.ValueContext(context));
        }
        catch (BddValueException ex)
        {
            return Task.FromResult(BddStepResult.Failed(invocation, $"'{valueArgumentName}' is a {BddValues.Describe(valueType!)}; {ex.Problem.Message}", ex));
        }
        object?[] arguments = [
            .. binding.ItemArgumentName is { } item ? [invocation.Arguments[item]] : Array.Empty<object?>(),
            .. valueArgumentName is null && literalArgument is null ? Array.Empty<object?>() : [value],
        ];

        try
        {
            BddInvoker.InvokeDefinition(controlResult.Control!, methodName, arguments);
            context.Diagnostics.Add($"{methodName}:{controlResult.ControlName}");
            return Task.FromResult(FollowPage(context, invocation));
        }
        catch (Exception ex)
        {
            var methodDetail = ex is MissingMethodException
                ? $" Available methods: {FormatMethodNames(controlResult.Control!)}."
                : string.Empty;
            return Task.FromResult(BddStepResult.Failed(
                invocation,
                $"Failed to run '{methodName}' on control '{controlResult.ControlName}': {ex.Message}.{methodDetail}",
                ex));
        }
    }

    private Task<BddStepResult> AssertAnyControlTextContainsAsync(
        BddExecutionContext context,
        BddStepInvocation invocation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetCurrentPage(context, invocation, out var pageResult))
        {
            return Task.FromResult(pageResult.Result);
        }

        var expected = invocation.Arguments["text"];
        var pageInstance = GetPageInstance(context, pageResult.Page!);
        if (pageInstance is null)
        {
            return Task.FromResult(BddStepResult.Failed(
                invocation,
                $"UAT page '{pageResult.Page!.Name}' requires an active DI scope."));
        }

        foreach (var controlBinding in pageResult.Page!.Controls)
        {
            var control = controlBinding.Get(pageInstance);
            if (control is null)
            {
                continue;
            }

            var text = BddInvoker.Invoke(control, "GetText");
            if (text is string value &&
                value.Contains(expected, StringComparison.Ordinal))
            {
                context.Diagnostics.Add($"see:{expected}");
                return Task.FromResult(BddStepResult.Passed(invocation));
            }
        }

        return Task.FromResult(BddStepResult.Failed(
            invocation,
            $"Text '{expected}' was not found on page '{pageResult.Page.Name}'."));
    }

    private bool TryGetCurrentControl(
        BddExecutionContext context,
        BddStepInvocation invocation,
        string controlArgumentName,
        out UatControlResolution result)
    {
        result = default;
        if (!TryGetCurrentPage(context, invocation, out var pageResult))
        {
            result = new UatControlResolution(null, string.Empty, pageResult.Result);
            return false;
        }

        var controlName = invocation.Arguments[controlArgumentName];
        var page = pageResult.Page!;
        var controlBinding = FindControl(page, controlName);
        if (controlBinding is null)
        {
            result = new UatControlResolution(
                null,
                controlName,
                BddStepResult.Failed(
                    invocation,
                    $"UAT control '{controlName}' was not found on page '{page.Name}'. Available controls: {FormatControlNames(page)}."));
            return false;
        }

        var pageInstance = GetPageInstance(context, page);
        if (pageInstance is null)
        {
            result = new UatControlResolution(
                null,
                controlName,
                BddStepResult.Failed(
                    invocation,
                    $"UAT page '{page.Name}' requires an active DI scope."));
            return false;
        }

        var control = controlBinding.Get(pageInstance);
        if (control is null)
        {
            result = new UatControlResolution(
                null,
                controlName,
                BddStepResult.Failed(
                    invocation,
                    $"UAT control '{controlName}' on page '{page.Name}' returned null."));
            return false;
        }

        result = new UatControlResolution(control, controlName, BddStepResult.Passed(invocation));
        return true;
    }

    // A verb without {control} runs on the one control of the current page whose type declares it:
    // "the todo {item} should be synced" on the list's collection.
    private bool TryGetDeclaringControl(
        BddExecutionContext context,
        BddStepInvocation invocation,
        UatStepBinding binding,
        out UatControlResolution result)
    {
        result = default;
        if (!TryGetCurrentPage(context, invocation, out var pageResult))
        {
            result = new UatControlResolution(null, string.Empty, pageResult.Result);
            return false;
        }

        var page = pageResult.Page!;
        var declaring = page.Controls
            .Where(control => SelfAndBaseTypes(control.Property.PropertyType).Any(type => Declares(type, binding)))
            .ToList();
        if (declaring.Count != 1)
        {
            result = new UatControlResolution(
                null,
                string.Empty,
                BddStepResult.Failed(
                    invocation,
                    declaring.Count == 0
                        ? $"No control on page '{page.Name}' has the step '{binding.Phrase}'. Go to the page whose control has it first."
                        : $"The step '{binding.Phrase}' is on several controls of page '{page.Name}' ({string.Join(", ", declaring.Select(control => control.Name))}), so it cannot tell which one is meant. Put {{control}} in the phrase."));
            return false;
        }

        var pageInstance = GetPageInstance(context, page);
        var control = pageInstance is null ? null : declaring[0].Get(pageInstance);
        if (control is null)
        {
            result = new UatControlResolution(
                null,
                declaring[0].Name,
                BddStepResult.Failed(invocation, $"UAT control '{declaring[0].Name}' on page '{page.Name}' could not be created."));
            return false;
        }

        result = new UatControlResolution(control, declaring[0].Name, BddStepResult.Passed(invocation));
        return true;
    }

    private static bool Declares(Type type, UatStepBinding binding)
        => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Any(method => method.Name == binding.MethodName &&
                           method.GetCustomAttributes<BddStepAttribute>(inherit: false)
                               .Any(attribute => attribute.Keyword == binding.Keyword && attribute.Phrase == binding.Phrase));

    // A step may put "the" before a control's name: "the dialog should ask ..." finds Dialog.
    private static UatRuntimeControl? FindControl(UatRuntimePage page, string name)
        => page.Controls.FirstOrDefault(control => control.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
           ?? (name.StartsWith("the ", StringComparison.OrdinalIgnoreCase)
               ? page.Controls.FirstOrDefault(control => control.Name.Equals(name[4..].TrimStart(), StringComparison.OrdinalIgnoreCase))
               : null);

    private bool TryGetCurrentPage(
        BddExecutionContext context,
        BddStepInvocation invocation,
        out UatPageResolution result)
    {
        result = default;
        if (context.AppItems.TryGetValue(CurrentPageItemKey, out var value) &&
            value is UatRuntimePage page)
        {
            result = new UatPageResolution(page, BddStepResult.Passed(invocation));
            return true;
        }

        if (context.CurrentPageName is not null &&
            _pages.TryGetValue(context.CurrentPageName, out var namedPage))
        {
            result = new UatPageResolution(namedPage, BddStepResult.Passed(invocation));
            return true;
        }

        result = new UatPageResolution(
            null,
            BddStepResult.Failed(
                invocation,
                $"No current UAT page is active. Add a 'Given I am on the ... page' step first. Available pages: {FormatPageNames()}."));
        return false;
    }

    // The binder writes a page's key into the step; a bare name finds the one page it names.
    private UatRuntimePage? FindPage(string name)
        => _pages.TryGetValue(name, out var page)
            ? page
            : _pages.Values.Where(candidate => candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList() is [var only] ? only : null;

    private string FormatPageNames()
    {
        return _pages.Count == 0
            ? "(none)"
            : string.Join(", ", _pages.Keys.Order(StringComparer.OrdinalIgnoreCase));
    }

    private static string FormatControlNames(UatRuntimePage page)
    {
        return page.Controls.Count == 0
            ? "(none)"
            : string.Join(", ", page.Controls.Select(control => control.Name).Order(StringComparer.OrdinalIgnoreCase));
    }

    private static string FormatMethodNames(object control)
    {
        var methods = control.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType != typeof(object))
            .Select(method => method.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return methods.Length == 0 ? "(none)" : string.Join(", ", methods);
    }

    /// <summary>The current page's controls and what each shows, for a paused Step-through.</summary>
    /// <param name="context">The scenario's context.</param>
    internal Bruinet.Bdd.Runner.BddRunnerPageState ReadCurrentPage(BddExecutionContext context)
    {
        var page = context.AppItems.TryGetValue(CurrentPageItemKey, out var value) && value is UatRuntimePage current ? current
            : context.CurrentPageName is { } name && _pages.TryGetValue(name, out var named) ? named
            : null;
        return UatPageState.Read(page, page is null ? null : GetPageInstance(context, page));
    }

    private static object? GetPageInstance(BddExecutionContext context, UatRuntimePage page)
    {
        if (page.Instance is not null)
        {
            return page.Instance;
        }

        return BddStepDefinitionRuntime.TryGetServiceProvider(context, out var serviceProvider)
            ? serviceProvider.GetService(page.PageType)
            : null;
    }

    private static bool WaitPageReady(object page)
    {
        if (BddInvoker.TryInvokeBool(page, "WaitReady", 10000, out var ready))
        {
            return ready;
        }

        if (BddInvoker.TryInvokeBool(page, "WaitLoaded", true, 10000, out ready))
        {
            return ready;
        }

        if (BddInvoker.TryInvokeBool(page, "IsLoaded", 10000, out ready))
        {
            return ready;
        }

        return true;
    }

    private readonly record struct UatPageResolution(UatRuntimePage? Page, BddStepResult Result);

    private readonly record struct UatControlResolution(object? Control, string ControlName, BddStepResult Result);
}

public sealed record UatRuntimePage(
    string Name,
    Type PageType,
    object? Instance,
    IReadOnlyList<UatRuntimeControl> Controls,
    Action? Navigate)
{
    /// <summary>The page's namespace (<see cref="BddNamespaces"/>); <c>""</c> when every scenario sees it.</summary>
    public string Namespace { get; init; } = string.Empty;

    /// <summary>The page as steps and the runtime know it: <c>Invoicing/Detail</c>, or <see cref="Name"/>.</summary>
    public string Key => BddNamespaces.PageKey(Namespace, Name);
}

public sealed record UatRuntimeControl(
    string Name,
    string MemberName,
    PropertyInfo Property)
{
    public object? Get(object page)
    {
        return Property.GetValue(page);
    }
}
