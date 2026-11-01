using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Bruinet.Generator.Models;
using Bruinet.Generator.Writers;

namespace Bruinet.Generator.Generators;

/// <summary>
/// Emits the public value-writing wrapper for a protected virtual <c>Set*Core</c>
/// method (e.g., <c>SetTextCore</c> → <c>SetText</c>). The wrapper delegates via
/// <c>RunSetWithElement</c>, which already implements the nullable skip pattern —
/// a null value returns the containing scope without touching the element.
/// </summary>
public class SetGenerator : IMemberGenerator
{
    private readonly MemberGeneratorOptions _options;
    private const string SetterPrefix = "Set";

    /// <summary>
    /// Plumbing parameter shared by every wrapper, never the value being written.
    /// </summary>
    private const string TimeoutParameterName = "timeoutMs";

    public SetGenerator(MemberGeneratorOptions? options = null)
    {
        _options = options ?? new MemberGeneratorOptions();
    }

    /// <summary>
    /// Determines whether a method is a <c>Set*Core</c> value writer, independent of
    /// its modifiers. Used by <see cref="ActionGenerator"/> to yield these methods to
    /// this generator regardless of registration order.
    /// </summary>
    public static bool IsSetCoreMethod(MethodDeclarationSyntax method, string methodSuffix)
    {
        var methodName = method.Identifier.Text;

        if (!methodName.EndsWith(methodSuffix, StringComparison.Ordinal))
            return false;
        if (!methodName.StartsWith(SetterPrefix, StringComparison.Ordinal))
            return false;

        // A setter takes the element plus at least one value to write. `timeoutMs` is
        // plumbing rather than a value, so a method whose only extra parameter is the
        // timeout is an action, not a setter — `SetToMinimumCore(element, timeoutMs)`
        // writes nothing the caller supplied. Claiming it here would emit
        // `RunSetWithElement(timeoutMs, ...)`, whose nullable-skip would make the public
        // wrapper silently do nothing whenever the caller omitted the timeout.
        return method.ParameterList.Parameters
            .Skip(1)
            .Any(p => p.Identifier.Text != TimeoutParameterName);
    }

    /// <summary>
    /// Matches protected virtual <c>Set*Core</c> methods whose first parameter is the
    /// platform element and which take at least one value parameter.
    /// </summary>
    public bool Matches(MethodDeclarationSyntax method)
    {
        if (!IsSetCoreMethod(method, _options.MethodSuffix))
            return false;

        var modifiers = method.Modifiers;
        if (_options.RequireProtected && !modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword)))
            return false;
        if (_options.RequireVirtual && !modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword)))
            return false;

        // Exclude overrides — the base class already generated the public wrapper.
        if (modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword)))
            return false;

        var firstParam = method.ParameterList.Parameters[0];
        var paramType = firstParam.Type?.ToString() ?? "";
        return paramType.Contains("Element");
    }

    /// <summary>
    /// Extracts method metadata, stripping the Core suffix and skipping the element
    /// parameter (e.g., <c>SetTextCore</c> → <c>SetText</c>).
    /// </summary>
    public MethodInfo Extract(MethodDeclarationSyntax method)
    {
        var methodName = method.Identifier.Text;
        var suffixLength = _options.MethodSuffix.Length;
        var publicName = methodName.Substring(0, methodName.Length - suffixLength);

        var info = new MethodInfo
        {
            MethodName = methodName,
            PublicMethodName = publicName,
            ReturnType = method.ReturnType.ToString(),
            XmlDocumentation = ExtractXmlDocumentation(method)
        };

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
    /// Generates the public setter wrapping <c>RunSetWithElement</c>. The first value
    /// parameter drives the nullable skip; a declared <c>timeoutMs</c> is forwarded.
    /// </summary>
    /// <inheritdoc />
    public IReadOnlyList<string> EmittedMemberNames(MethodInfo coreMethod)
        => [coreMethod.PublicMethodName];

        public string Generate(MethodInfo coreMethod, ControlObjectContext context)
    {
        var writer = new CsWriter(0);
        var publicMethodName = coreMethod.PublicMethodName;

        var returnTypeStr = context.TypeParameters.TrimStart('<').TrimEnd('>');
        if (string.IsNullOrEmpty(returnTypeStr))
            returnTypeStr = "void";

        writer.WriteLine($"#region {publicMethodName}");
        writer.WriteLine();

        var paramList = BuildParameterList(coreMethod);
        var lambdaArgs = BuildLambdaArguments(coreMethod);

        // The value being written is the first parameter after the element.
        var valueParameterName = coreMethod.Parameters[0].ParameterName;
        var hasTimeoutMs = coreMethod.Parameters.Any(p => p.ParameterName == "timeoutMs");

        writer.WriteLine($"public {returnTypeStr} {publicMethodName}({paramList})");
        writer.Open();

        writer.WriteStart($"return RunSetWithElement({valueParameterName}, element => {{ ");
        writer.Write($"{coreMethod.MethodName}(element{lambdaArgs}); ");
        writer.Write("}");

        if (hasTimeoutMs)
        {
            writer.Write(", timeoutMs");
        }

        writer.WriteEnd(");");
        writer.Close();
        writer.WriteLine();

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
