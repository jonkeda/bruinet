using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Bruinet.Generator.Models;
using Bruinet.Generator.Writers;

namespace Bruinet.Generator.Generators;

/// <summary>
/// Emits the public action wrapper for a protected virtual <c>*Core</c> method
/// (e.g., <c>ClickCore</c> → <c>Click</c>). The wrapper delegates straight to the
/// Core method via <c>RunDoWithElement</c>; no guard is injected, so any
/// <c>Ensure*Core</c> check belongs inside the Core method body.
/// </summary>
public class ActionGenerator : IMemberGenerator
{
    /// <summary>
    /// Reserved prefix for guard helpers (e.g., <c>EnsureClickableCore</c>). Guards are
    /// internal checks, not actions, so they never get a public wrapper.
    /// </summary>
    private const string GuardPrefix = "Ensure";

    private readonly MemberGeneratorOptions _options;

    public ActionGenerator(MemberGeneratorOptions? options = null)
    {
        _options = options ?? new MemberGeneratorOptions();
    }

    /// <summary>
    /// Matches methods ending with "Core", declared as protected and virtual,
    /// excluding <c>Is*Core</c>/<c>Get*Core</c> queries (handled by the Is/Wait/Assert
    /// family), <c>Set*Core</c> writers (handled by <see cref="SetGenerator"/>),
    /// <c>Ensure*</c> guards, and <c>override</c>s (the base already generated the wrapper).
    /// </summary>
    public bool Matches(MethodDeclarationSyntax method)
    {
        var methodName = method.Identifier.Text;

        // Check if name ends with suffix
        if (!methodName.EndsWith(_options.MethodSuffix, StringComparison.Ordinal))
            return false;

        // Exclude Is*Core state queries — those belong to IsWaitAssertGenerator.
        if (methodName.StartsWith("Is", StringComparison.Ordinal) && method.ReturnType.ToString() == "bool?")
            return false;

        // Exclude Get*Core value queries — those belong to IsWaitAssertGenerator.
        if (methodName.StartsWith("Get", StringComparison.Ordinal))
            return false;

        // Exclude Set*Core value writers — those belong to SetGenerator.
        if (SetGenerator.IsSetCoreMethod(method, _options.MethodSuffix))
            return false;

        // Exclude Ensure* guards — internal checks, not actions.
        if (methodName.StartsWith(GuardPrefix, StringComparison.Ordinal))
            return false;

        // Exclude anything that returns a value. The wrapper is RunDoWithElement, which has
        // nowhere to put a result, so a query claimed here becomes a public method that does the
        // lookup and throws the answer away. That is how FindChildCore became a public
        // FindChild(id) on every control that found a child and returned the scope instead
        // (step 101b).
        if (method.ReturnType.ToString() != "void")
            return false;

        var modifiers = method.Modifiers;

        // Exclude overrides — the base class already generated the public wrapper.
        if (modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword)))
            return false;

        // Check for required modifiers
        if (_options.RequireProtected && !modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword)))
            return false;

        if (_options.RequireVirtual && !modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword)))
            return false;

        // Ensure it has at least the element parameter
        if (method.ParameterList.Parameters.Count == 0)
            return false;

        return true;
    }

    /// <summary>
    /// Extracts method metadata, stripping the Core suffix and preparing parameters.
    /// </summary>
    public MethodInfo Extract(MethodDeclarationSyntax method)
    {
        var methodName = method.Identifier.Text;
        var suffixLength = _options.MethodSuffix.Length;
        var publicName = methodName.Substring(0, methodName.Length - suffixLength);

        var info = new MethodInfo
        {
            MethodName = methodName,
            PublicMethodName = _options.PublicMethodPrefix + publicName,
            ReturnType = method.ReturnType.ToString(),
            XmlDocumentation = ExtractXmlDocumentation(method)
        };

        // Extract parameters, skipping the first one (element)
        var parameters = method.ParameterList.Parameters;
        for (int i = 1; i < parameters.Count; i++)
        {
            var param = parameters[i];
            var typeName = param.Type?.ToString() ?? "object";
            var paramName = param.Identifier.Text;
            var defaultValue = param.Default?.Value?.ToString();

            info.Parameters.Add((typeName, paramName, defaultValue));
        }

        return info;
    }

    /// <summary>
    /// Generates the public action wrapper using <c>RunDoWithElement</c>. The emitted
    /// body calls the Core method directly and adds no guard of its own.
    /// </summary>
    /// <inheritdoc />
    public IReadOnlyList<string> EmittedMemberNames(MethodInfo coreMethod)
        => [coreMethod.PublicMethodName];

    public string Generate(MethodInfo coreMethod, ControlObjectContext context)
    {
        var writer = new CsWriter(0); // Start with no indentation - let the file-level writer handle it
        var publicMethodName = coreMethod.PublicMethodName;

        // Determine return type - the resolved fluent type parameter, not the whole
        // type parameter list (which would emit "TParent, TSelf" for a container).
        var returnTypeStr = context.FluentReturnType;
        if (string.IsNullOrEmpty(returnTypeStr))
            returnTypeStr = "void";

        // Write region start
        writer.WriteLine($"#region {publicMethodName}");
        writer.WriteLine();

        // Build parameter list
        var paramList = BuildParameterList(coreMethod);

        // Write method signature
        writer.WriteLine($"public {returnTypeStr} {publicMethodName}({paramList})");
        writer.Open();

        // Build lambda arguments (additional parameters after element)
        var lambdaArgs = BuildLambdaArguments(coreMethod);
        var hasTimeoutMs = coreMethod.Parameters.Any(p => p.ParameterName == "timeoutMs");

        // Build method body with RunDoWithElement call. A void wrapper must not
        // return the helper's result.
        writer.WriteStart($"{(returnTypeStr == "void" ? "" : "return ")}RunDoWithElement(element => {{ ");
        writer.Write($"{coreMethod.MethodName}(element{lambdaArgs}); ");
        writer.Write("}");

        if (hasTimeoutMs)
        {
            writer.Write(", timeoutMs");
        }

        writer.WriteEnd(");");
        writer.Close();
        writer.WriteLine();

        // Write region end
        writer.WriteLine("#endregion");

        return writer.ToString();
    }

    private string BuildParameterList(MethodInfo coreMethod)
    {
        if (coreMethod.Parameters.Count == 0)
            return "";

        var paramParts = new List<string>();
        foreach (var (typeName, paramName, defaultValue) in coreMethod.Parameters)
        {
            if (string.IsNullOrEmpty(defaultValue))
                paramParts.Add($"{typeName} {paramName}");
            else
                paramParts.Add($"{typeName} {paramName} = {defaultValue}");
        }

        return string.Join(", ", paramParts);
    }

    private string BuildLambdaArguments(MethodInfo coreMethod)
    {
        if (coreMethod.Parameters.Count == 0)
            return "";

        var paramNames = coreMethod.Parameters.Select(p => p.ParameterName);
        return ", " + string.Join(", ", paramNames);
    }

    private string? ExtractXmlDocumentation(MethodDeclarationSyntax method)
    {
        var trivia = method.GetLeadingTrivia();
        var xmlTrivia = trivia
            .FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                                 t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

        return xmlTrivia.IsKind(SyntaxKind.None) ? null : xmlTrivia.ToString();
    }
}
