using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Bruinet.Generator.Models;

namespace Bruinet.Generator.Analysis;

/// <summary>
/// Analyzes a C# file using Roslyn to locate the target ControlObject class and
/// build the <see cref="ControlObjectContext"/> used for generation.
/// </summary>
public class ControlObjectAnalyzer
{
    /// <summary>
    /// Parses source and finds the target class.
    /// </summary>
    /// <param name="sourceCode">The C# source code to analyze.</param>
    /// <param name="targetClassName">Optional target class name; if not specified, uses the first class.</param>
    /// <returns>Tuple of (ClassDeclarationSyntax?, CompilationUnitSyntax).</returns>
    public (ClassDeclarationSyntax? ClassDecl, CompilationUnitSyntax Root)
        FindTarget(string sourceCode, string? targetClassName = null)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = (CompilationUnitSyntax)tree.GetRoot();
        var classDecl = FindClass(root, targetClassName);
        return (classDecl, root);
    }

    /// <summary>
    /// Returns the Core methods (protected members ending in "Core") of a class.
    /// </summary>
    public IEnumerable<MethodDeclarationSyntax> CoreMethods(ClassDeclarationSyntax classDecl)
    {
        return classDecl.Members.OfType<MethodDeclarationSyntax>();
    }

    /// <summary>
    /// Reports methods that look like generation candidates but would be skipped silently.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A method named <c>*Core</c> taking the platform element first is, by every visible
    /// signal, meant to generate. If it lacks <c>virtual</c> or <c>protected</c> the
    /// generator simply passes over it — no warning, no output — and the missing public API
    /// surfaces much later, if at all. Two such methods sat in <c>SelectorControlBase</c>
    /// for exactly that reason.
    /// </para>
    /// <para>
    /// Deliberate exclusions are declared with <c>[SkipGeneration("reason")]</c>, which keeps
    /// the method <c>virtual</c> and overridable. With intent declarable, silence is no
    /// longer an acceptable default: anything else that misses is reported.
    /// </para>
    /// <para>
    /// <c>Ensure*</c> guards are exempt — <c>ActionGenerator</c> excludes them by name, and
    /// they are internal checks rather than API. Overrides are exempt because the base class
    /// already generated the member.
    /// </para>
    /// </remarks>
    /// <param name="classDecl">The class being generated.</param>
    /// <returns>One diagnostic message per near-miss; empty when all are well-formed.</returns>
    public IReadOnlyList<string> FindSilentlySkippedCoreMethods(ClassDeclarationSyntax classDecl)
    {
        var problems = new List<string>();

        foreach (var method in CoreMethods(classDecl))
        {
            var name = method.Identifier.Text;

            if (!name.EndsWith("Core", StringComparison.Ordinal)) continue;
            if (name.StartsWith("Ensure", StringComparison.Ordinal)) continue;
            if (HasSkipGenerationAttribute(method)) continue;

            var modifiers = method.Modifiers;
            if (modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword))) continue;

            // Only methods taking the platform element first are candidates at all; anything
            // else is a private helper that happens to end in "Core".
            var firstParam = method.ParameterList.Parameters.FirstOrDefault();
            if (firstParam?.Type?.ToString().Contains("Element") != true) continue;

            var isProtected = modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword));
            var isVirtual = modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword));
            if (isProtected && isVirtual) continue;

            var missing = (isProtected, isVirtual) switch
            {
                (false, false) => "'protected' and 'virtual'",
                (true, false) => "'virtual'",
                _ => "'protected'"
            };

            problems.Add(
                $"'{name}' looks like a generation candidate but is missing {missing}, so it " +
                "would be skipped without warning. Add the missing modifier to generate a " +
                "public member, or [SkipGeneration(\"reason\")] if that is deliberate.");
        }

        return problems;
    }

    /// <summary>
    /// The unit-of-work helpers a Core method must not call: the generated wrapper around the
    /// Core method is already one of them.
    /// </summary>
    private static readonly string[] RunHelperPrefixes = ["RunWait", "RunDo", "RunGet", "RunAssert", "RunSet"];

    /// <summary>
    /// The child factories a container inherits; each returns a control scoped to it.
    /// </summary>
    private static readonly HashSet<string> ChildFactories =
        ["Button", "Label", "Entry", "CheckBox", "ActivityIndicator", "Child"];

    /// <summary>
    /// Reports Core methods that start a second unit of work inside the one their generated
    /// wrapper already runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two shapes, both found from syntax alone:
    /// </para>
    /// <list type="bullet">
    /// <item>A call to a <c>Run*</c> helper (<c>RunWait</c>, <c>RunDo</c>, <c>RunGet*</c>,
    /// <c>RunAssert*</c>, <c>RunSet*</c>, <c>Run</c>): a second poll with its own timeout and log
    /// entry. Wait for an action's effect with <c>Confirm</c>.</item>
    /// <item>A call on a part: a property or method of this class built with <c>new(this, ...)</c>,
    /// or a container's child factory (<c>Button(id)</c>, <c>Label(id)</c>, ...). The part's public
    /// member is a complete unit of work of its own.</item>
    /// </list>
    /// <para>
    /// Warnings, not errors: generation goes on, and the CLI prints them. Hand-written members are
    /// not Core methods and may call parts in sequence.
    /// </para>
    /// </remarks>
    /// <param name="classDecl">The class being generated.</param>
    /// <returns>One message per nested call; empty when there are none.</returns>
    public IReadOnlyList<string> FindNestedUnitsOfWork(ClassDeclarationSyntax classDecl)
    {
        var parts = PartMembers(classDecl);
        var warnings = new List<string>();

        foreach (var method in CoreMethods(classDecl))
        {
            var name = method.Identifier.Text;
            if (!name.EndsWith("Core", StringComparison.Ordinal)) continue;

            SyntaxNode? body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
            if (body == null) continue;

            foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (IsRunHelperCall(invocation, out var helper))
                {
                    warnings.Add(
                        $"'{name}' calls '{helper}' inside a Core method: a second poll with its own " +
                        "timeout and log entry, inside the one the generated wrapper runs. Wait with " +
                        "Confirm instead.");
                }
                else if (IsPartCall(invocation, parts, out var call))
                {
                    warnings.Add(
                        $"'{name}' calls '{call}', a public member of another control: a nested unit " +
                        "of work (a second readiness check, poll and log entry). Move the behaviour to " +
                        "the part and add a shortcut, or hand-write the member as calls in sequence.");
                }
            }
        }

        return warnings;
    }

    /// <summary>
    /// Names of the class's properties and methods that return a control scoped to the class:
    /// their body creates an object and passes <c>this</c>.
    /// </summary>
    private static HashSet<string> PartMembers(ClassDeclarationSyntax classDecl)
    {
        static bool CreatesScopedToThis(ExpressionSyntax? expression)
            => expression is BaseObjectCreationExpressionSyntax { ArgumentList: { } arguments }
               && arguments.Arguments.Any(a => a.Expression.DescendantNodesAndSelf().OfType<ThisExpressionSyntax>().Any());

        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in classDecl.Members.OfType<PropertyDeclarationSyntax>())
        {
            var expression = property.ExpressionBody?.Expression
                ?? property.AccessorList?.Accessors
                    .FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration))?.ExpressionBody?.Expression;
            if (CreatesScopedToThis(expression))
                names.Add(property.Identifier.Text);
        }

        foreach (var method in classDecl.Members.OfType<MethodDeclarationSyntax>())
        {
            if (CreatesScopedToThis(method.ExpressionBody?.Expression))
                names.Add(method.Identifier.Text);
        }

        return names;
    }

    private static bool IsRunHelperCall(InvocationExpressionSyntax invocation, out string helper)
    {
        helper = invocation.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            GenericNameSyntax generic => generic.Identifier.Text,
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax or BaseExpressionSyntax } access
                => access.Name.Identifier.Text,
            _ => ""
        };

        var name = helper;
        return name == "Run" || RunHelperPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static bool IsPartCall(InvocationExpressionSyntax invocation, HashSet<string> parts, out string call)
    {
        call = invocation.Expression.ToString();

        if (invocation.Expression is not MemberAccessExpressionSyntax access)
            return false;

        return access.Expression switch
        {
            // Part.Member(...)
            IdentifierNameSyntax target => parts.Contains(target.Identifier.Text),
            // Button(id).Member(...), Child<T>(id).Member(...), PartFactory(x).Member(...)
            InvocationExpressionSyntax { Expression: IdentifierNameSyntax factory }
                => ChildFactories.Contains(factory.Identifier.Text) || parts.Contains(factory.Identifier.Text),
            InvocationExpressionSyntax { Expression: GenericNameSyntax factory }
                => ChildFactories.Contains(factory.Identifier.Text),
            _ => false
        };
    }

    /// <summary>
    /// Reports methods named as shortcuts that cannot be one.
    /// </summary>
    /// <remarks>
    /// A method ending in <c>Shortcut</c> is a shortcut by intent, so one that breaks the rules
    /// (not protected, virtual, or not a single call on a part) is an error rather than a
    /// silent skip, as for Core methods.
    /// </remarks>
    /// <param name="classDecl">The class being generated.</param>
    /// <returns>One message per malformed shortcut; empty when all are well-formed.</returns>
    public IReadOnlyList<string> FindMalformedShortcuts(ClassDeclarationSyntax classDecl)
    {
        var problems = new List<string>();

        foreach (var method in CoreMethods(classDecl))
        {
            if (!ShortcutMethod.IsCandidate(method) || HasSkipGenerationAttribute(method)) continue;

            if (!ShortcutMethod.TryParse(method, out _, out var error))
                problems.Add(error!);
        }

        return problems;
    }

    /// <summary>
    /// Whether a Core method opts out of generation with <c>[SkipGeneration]</c>.
    /// </summary>
    public static bool IsGenerationSkipped(MethodDeclarationSyntax method)
        => HasSkipGenerationAttribute(method);

    /// <summary>
    /// Reads [SkipGeneration] from a Core method.
    /// </summary>
    private static bool HasSkipGenerationAttribute(MethodDeclarationSyntax method)
    {
        return method.AttributeLists
            .SelectMany(list => list.Attributes)
            .Any(a =>
            {
                var name = a.Name.ToString();
                var simpleName = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
                return simpleName is "SkipGeneration" or "SkipGenerationAttribute";
            });
    }

    /// <summary>
    /// Builds the <see cref="ControlObjectContext"/> from the target class and root.
    /// </summary>
    public ControlObjectContext BuildContext(ClassDeclarationSyntax classDecl, CompilationUnitSyntax root)
    {
        return new ControlObjectContext
        {
            ContainingTypeName = classDecl.Identifier.Text,
            TypeParameters = GetTypeParameters(classDecl),
            FluentReturnType = ResolveFluentReturnType(classDecl),
            ElementType = DetectElementType(classDecl),
            Namespace = GetNamespace(root),
            Usings = GetUsingStatements(root),
            ClassDeclaration = classDecl
        };
    }

    /// <summary>
    /// Finds a class in the compilation unit.
    /// </summary>
    private ClassDeclarationSyntax? FindClass(CompilationUnitSyntax root, string? className = null)
    {
        var classDecls = new List<ClassDeclarationSyntax>();

        // Search at root level
        classDecls.AddRange(root.Members.OfType<ClassDeclarationSyntax>());

        // Search inside FileScopedNamespaceDeclaration
        var fileScoped = root.Members.OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
        if (fileScoped != null)
        {
            classDecls.AddRange(fileScoped.Members.OfType<ClassDeclarationSyntax>());
        }

        // Search inside regular NamespaceDeclaration
        var regularNs = root.Members.OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        if (regularNs != null)
        {
            classDecls.AddRange(regularNs.Members.OfType<ClassDeclarationSyntax>());
        }

        if (!classDecls.Any())
            return null;

        if (string.IsNullOrEmpty(className))
            return classDecls.First();

        return classDecls.FirstOrDefault(c => c.Identifier.Text == className);
    }

    /// <summary>
    /// Detects the platform element type from the first Core method's first parameter.
    /// Defaults to "IMauiElement" when none is found.
    /// </summary>
    private string DetectElementType(ClassDeclarationSyntax classDecl)
    {
        foreach (var method in classDecl.Members.OfType<MethodDeclarationSyntax>())
        {
            if (!method.Identifier.Text.EndsWith("Core", StringComparison.Ordinal))
                continue;
            if (method.ParameterList.Parameters.Count == 0)
                continue;

            var paramType = method.ParameterList.Parameters[0].Type?.ToString();
            if (string.IsNullOrEmpty(paramType))
                continue;

            return paramType.TrimEnd('?');
        }

        return "IMauiElement";
    }

    /// <summary>
    /// Gets the generic type parameters from a class declaration (e.g., "&lt;TScope&gt;").
    /// </summary>
    public string GetTypeParameters(ClassDeclarationSyntax classDecl)
    {
        if (classDecl.TypeParameterList == null || classDecl.TypeParameterList.Parameters.Count == 0)
            return "";

        var parameters = string.Join(", ", classDecl.TypeParameterList.Parameters.Select(p => p.Identifier.Text));
        return $"<{parameters}>";
    }

    /// <summary>
    /// Resolves the type parameter that public members return for fluent chaining.
    /// </summary>
    /// <remarks>
    /// Rules, in order:
    /// <list type="number">
    /// <item><c>[FluentReturn("T")]</c> on the class, when present.</item>
    /// <item><c>TSelf</c>, when the class declares it — containers and collections
    /// return themselves so a chain stays inside the scope.</item>
    /// <item>The class itself, when it passes itself to its base as a type argument:
    /// <c>Toolbar&lt;TParent&gt; : CollectionObjectBase&lt;TParent, Toolbar&lt;TParent&gt;,
    /// ToolbarItem&lt;TParent&gt;&gt;</c> is the same self-reference as <c>TSelf</c>, closed
    /// by a concrete class instead of passed on.</item>
    /// <item>The single type parameter — the control case, which returns its
    /// containing scope.</item>
    /// <item>Empty, when the class has no type parameters; actions then return void.</item>
    /// </list>
    /// </remarks>
    /// <param name="classDecl">The class to resolve against.</param>
    /// <returns>The type parameter name, or an empty string when there is none.</returns>
    public string ResolveFluentReturnType(ClassDeclarationSyntax classDecl)
    {
        var declared = classDecl.TypeParameterList?.Parameters
            .Select(p => p.Identifier.Text)
            .ToList() ?? [];

        var explicitReturn = GetFluentReturnAttributeValue(classDecl);
        if (!string.IsNullOrEmpty(explicitReturn))
        {
            if (declared.Count > 0 && !declared.Contains(explicitReturn))
            {
                throw new InvalidOperationException(
                    $"[FluentReturn(\"{explicitReturn}\")] on '{classDecl.Identifier.Text}' names a type " +
                    $"parameter the class does not declare. Declared: {string.Join(", ", declared)}.");
            }

            return explicitReturn;
        }

        if (declared.Contains("TSelf"))
            return "TSelf";

        var closedSelf = ResolveClosedSelfType(classDecl);
        if (closedSelf != null)
            return closedSelf;

        if (declared.Count == 1)
            return declared[0];

        if (declared.Count == 0)
            return "";

        throw new InvalidOperationException(
            $"Cannot infer the fluent return type for '{classDecl.Identifier.Text}': it declares " +
            $"{declared.Count} type parameters ({string.Join(", ", declared)}) and none is named 'TSelf'. " +
            $"Add [FluentReturn(\"<name>\")] to the class to say which one public members return.");
    }

    /// <summary>
    /// The class's own type, when the class supplies itself as a type argument to its base.
    /// </summary>
    /// <returns>The self type (for example <c>Toolbar&lt;TParent&gt;</c>), or null.</returns>
    private static string? ResolveClosedSelfType(ClassDeclarationSyntax classDecl)
    {
        if (classDecl.TypeParameterList == null || classDecl.BaseList == null)
            return null;

        var selfType = classDecl.Identifier.Text + "<" +
            string.Join(", ", classDecl.TypeParameterList.Parameters.Select(p => p.Identifier.Text)) + ">";
        var normalizedSelf = WithoutWhitespace(selfType);

        var arguments = classDecl.BaseList.Types
            .SelectMany(baseType => baseType.Type.DescendantNodesAndSelf().OfType<GenericNameSyntax>())
            .SelectMany(generic => generic.TypeArgumentList.Arguments);

        return arguments.Any(argument => WithoutWhitespace(argument.ToString()) == normalizedSelf)
            ? selfType
            : null;
    }

    private static string WithoutWhitespace(string value)
        => new(value.Where(c => !char.IsWhiteSpace(c)).ToArray());

    /// <summary>
    /// Reads the single string argument of a <c>[FluentReturn(...)]</c> attribute, if present.
    /// </summary>
    private static string? GetFluentReturnAttributeValue(ClassDeclarationSyntax classDecl)
    {
        var attribute = classDecl.AttributeLists
            .SelectMany(list => list.Attributes)
            .FirstOrDefault(a =>
            {
                var name = a.Name.ToString();
                return name is "FluentReturn" or "FluentReturnAttribute"
                    || name.EndsWith(".FluentReturn", StringComparison.Ordinal)
                    || name.EndsWith(".FluentReturnAttribute", StringComparison.Ordinal);
            });

        var argument = attribute?.ArgumentList?.Arguments.FirstOrDefault()?.Expression;

        return argument switch
        {
            LiteralExpressionSyntax literal => literal.Token.ValueText,
            // nameof(TSelf) - take the operand's text
            InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" } } invocation
                => invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression.ToString(),
            _ => null
        };
    }

    /// <summary>
    /// Extracts the namespace from a compilation unit.
    /// </summary>
    public string? GetNamespace(CompilationUnitSyntax root)
    {
        var namespaceDecl = root.Members
            .OfType<NamespaceDeclarationSyntax>()
            .FirstOrDefault();

        if (namespaceDecl != null)
            return namespaceDecl.Name.ToString();

        var fileScoped = root.Members
            .OfType<FileScopedNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        return fileScoped?.Name.ToString();
    }

    /// <summary>
    /// Extracts using namespace names from a compilation unit (without the
    /// "using" keyword or trailing semicolon).
    /// </summary>
    public List<string> GetUsingStatements(CompilationUnitSyntax root)
    {
        return root.Usings
            .Select(u => u.Name?.ToString() ?? "")
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();
    }
}
