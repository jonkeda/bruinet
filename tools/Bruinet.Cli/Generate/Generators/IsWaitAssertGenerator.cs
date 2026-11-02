using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Bruinet.Generator.Models;
using Bruinet.Generator.Writers;

namespace Bruinet.Generator.Generators;

/// <summary>
/// Emits a query/Wait/Assert trio for a protected virtual <c>Is*Core</c> or
/// <c>Get*Core</c> query. <c>Is*Core</c> (returning <c>bool?</c>) yields
/// <c>Is*</c>/<c>Wait*</c>/<c>Assert*</c> (e.g., <c>IsVisibleCore</c> →
/// <c>IsVisible</c>, <c>WaitVisible</c>, <c>AssertVisible</c>). <c>Get*Core</c>
/// (returning any value) yields <c>Get*</c>/<c>Wait*</c>/<c>Assert*</c> (e.g.,
/// <c>GetTextCore</c> → <c>GetText</c>, <c>WaitText</c>, <c>AssertText</c>).
/// </summary>
public class IsWaitAssertGenerator : IMemberGenerator
{
    private readonly MemberGeneratorOptions _options;
    private readonly string _methodPrefix = "Is";   // Prefix for Is*Core state queries
    private readonly string _getterPrefix = "Get";  // Prefix for Get*Core value queries

    public IsWaitAssertGenerator(MemberGeneratorOptions? options = null)
    {
        _options = options ?? new MemberGeneratorOptions { MethodSuffix = "Core" };
    }

    /// <summary>
    /// Matches protected virtual query methods ending with "Core":
    /// <c>Is*Core</c> returning <c>bool?</c>, or <c>Get*Core</c> returning any value.
    /// The first parameter must be the platform element.
    /// </summary>
    public bool Matches(MethodDeclarationSyntax method)
    {
        var methodName = method.Identifier.Text;

        // Must end with the Core suffix.
        if (!methodName.EndsWith(_options.MethodSuffix, StringComparison.Ordinal))
            return false;

        var isState = methodName.StartsWith(_methodPrefix, StringComparison.Ordinal);   // Is*Core
        var isGetter = methodName.StartsWith(_getterPrefix, StringComparison.Ordinal);  // Get*Core
        if (!isState && !isGetter)
            return false;

        var returnType = method.ReturnType.ToString();

        // Is*Core must return bool or bool?; Get*Core must return a value (non-void).
        if (isState && returnType != "bool?" && returnType != "bool")
            return false;
        if (isGetter && returnType == "void")
            return false;

        var modifiers = method.Modifiers;
        if (_options.RequireProtected && !modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword)))
            return false;
        if (_options.RequireVirtual && !modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword)))
            return false;

        // Exclude overrides — the base class already generated the public trio.
        if (modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword)))
            return false;

        // Ensure first parameter is the platform element (nullable or not)
        if (method.ParameterList.Parameters.Count == 0)
            return false;

        var firstParam = method.ParameterList.Parameters[0];
        var paramType = firstParam.Type?.ToString() ?? "";
        if (!paramType.Contains("Element"))
            return false;

        return true;
    }

    /// <summary>
    /// Extracts method metadata. Property name is derived by stripping the prefix
    /// ("Is" or "Get") and "Core" suffix (e.g., IsExistsCore → Exists,
    /// GetTextCore → Text). For getters, the extra parameters after the element
    /// are captured for the generated signatures.
    /// </summary>
    public MethodInfo Extract(MethodDeclarationSyntax method)
    {
        var methodName = method.Identifier.Text;
        var isGetter = methodName.StartsWith(_getterPrefix, StringComparison.Ordinal);
        var prefix = isGetter ? _getterPrefix : _methodPrefix;

        // Extract property name: "IsExistsCore" → "Exists", "GetTextCore" → "Text"
        var prefixLength = prefix.Length;
        var suffixLength = _options.MethodSuffix.Length;
        var propertyName = methodName.Substring(prefixLength, methodName.Length - prefixLength - suffixLength);

        var info = new MethodInfo
        {
            MethodName = methodName,
            PublicMethodName = propertyName,  // Used as base for Is*/Get*, Wait*, Assert*
            ReturnType = method.ReturnType.ToString(),
            XmlDocumentation = ExtractXmlDocumentation(method),
            IsAbsenceTolerant = HasAbsenceTolerantAttribute(method)
        };

        // Capture extra parameters (after the element): they lead every generated signature,
        // for state queries (IsShowingCore(element, id) -> IsShowing(id)) as for getters.
        var parameters = method.ParameterList.Parameters;
        for (int i = 1; i < parameters.Count; i++)
        {
            var param = parameters[i];
            var typeName = param.Type?.ToString() ?? "object";
            var paramName = param.Identifier.Text;
            info.Parameters.Add((typeName, paramName, null));
        }

        if (isGetter)
        {
            foreach (var comparison in ExtractComparisons(method))
            {
                info.Comparisons.Add(comparison);
            }
        }

        return info;
    }

    /// <summary>
    /// The helper a getter's Get member runs through. An absence-tolerant getter reads once,
    /// with the element resolved optionally, so it answers null for a missing element instead
    /// of waiting for it and throwing.
    /// </summary>
    private static string GetHelper(MethodInfo coreMethod)
        => coreMethod.IsAbsenceTolerant ? "RunGetWithOptionalElement" : "RunGetWithElement";

    /// <summary>The helper a Wait member polls through; see <see cref="GetHelper"/>.</summary>
    private static string WaitHelper(MethodInfo coreMethod)
        => coreMethod.IsAbsenceTolerant ? "RunWaitWithOptionalElement" : "RunWaitWithElement";

    /// <summary>The helper an Assert member polls through; see <see cref="GetHelper"/>.</summary>
    private static string AssertHelper(MethodInfo coreMethod)
        => coreMethod.IsAbsenceTolerant ? "RunAssertWithOptionalElement" : "RunAssertWithElement";

    /// <summary>
    /// Reads [AbsenceTolerant] from a Core method. Matched syntactically (no semantic
    /// model), so the attribute may appear with or without the "Attribute" suffix and
    /// with any qualification - the same approach ExtractComparisons uses.
    /// </summary>
    private static bool HasAbsenceTolerantAttribute(MethodDeclarationSyntax method)
    {
        return method.AttributeLists
            .SelectMany(list => list.Attributes)
            .Any(a =>
            {
                var name = a.Name.ToString();
                var simpleName = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
                return simpleName is "AbsenceTolerant" or "AbsenceTolerantAttribute";
            });
    }

    /// <summary>
    /// The operator and wording for each ordered comparison variant.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, (string Operator, string Words)> OrderedComparisons =
        new Dictionary<string, (string, string)>
        {
            ["GreaterThan"] = (">", "greater than"),
            ["AtLeast"] = (">=", "at least"),
            ["LessThan"] = ("<", "less than"),
            ["AtMost"] = ("<=", "at most"),
        };

    /// <summary>
    /// Reads the comparison variants declared by [GenerateComparisons]. Shared with
    /// <see cref="ShortcutGenerator"/>, which forwards the same variants to a part.
    /// </summary>
    internal static List<string> ExtractComparisons(MethodDeclarationSyntax method)
    {
        var comparisons = new List<string> { "Equals" };

        var attribute = method.AttributeLists
            .SelectMany(list => list.Attributes)
            .FirstOrDefault(a =>
            {
                var name = a.Name.ToString();
                var simpleName = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
                return simpleName is "GenerateComparisons" or "GenerateComparisonsAttribute";
            });

        if (attribute?.ArgumentList == null)
            return comparisons;

        // Flags are written as `Comparison.Contains | Comparison.StartsWith`; pull the
        // member names out of the expression text rather than evaluating it.
        var argumentText = attribute.ArgumentList.Arguments.ToString();

        // Matched as `Comparison.<Name>` rather than by bare substring: "Equals" occurs
        // inside "SequenceEquals", so a substring test would silently add the wrong variant.
        foreach (var variant in new[]
                 {
                     "Contains", "StartsWith", "EndsWith", "Empty",
                     "SequenceEquals", "HasItem", "Count",
                     "GreaterThan", "AtLeast", "LessThan", "AtMost"
                 })
        {
            if (argumentText.Contains($".{variant}") && !comparisons.Contains(variant))
                comparisons.Add(variant);
        }

        // A collection getter has no meaningful reference equality, so SequenceEquals
        // replaces the default rather than joining it.
        if (comparisons.Contains("SequenceEquals"))
            comparisons.Remove("Equals");

        return comparisons;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> EmittedMemberNames(MethodInfo coreMethod)
    {
        var propertyName = coreMethod.PublicMethodName;
        var queryPrefix = coreMethod.MethodName.StartsWith(_getterPrefix, StringComparison.Ordinal) ? _getterPrefix : _methodPrefix;

        // The comparison variants (Wait{P}Contains, Assert{P}Empty, ...) all carry the same
        // stem, so anything that would clash with one already clashes with these three.
        return [queryPrefix + propertyName, "Wait" + propertyName, "Assert" + propertyName];
    }

    /// <summary>
    /// Generates the query/Wait/Assert trio from a single Is*Core or Get*Core method.
    /// </summary>
    public string Generate(MethodInfo coreMethod, ControlObjectContext context)
    {
        // Assert* members return the scope for fluent chaining. Which type parameter
        // that is depends on the class: TScope for a control, TSelf for a container.
        var fluentReturnType = context.FluentReturnType;

        return coreMethod.MethodName.StartsWith(_getterPrefix, StringComparison.Ordinal)
            ? GenerateGetter(coreMethod, fluentReturnType)
            : GenerateState(coreMethod, fluentReturnType);
    }

    private string GenerateState(MethodInfo coreMethod, string fluentReturnType)
    {
        var propertyName = coreMethod.PublicMethodName;
        var writer = new CsWriter(0);

        // Write region start
        writer.WriteLine($"#region {propertyName} (Is{propertyName} / Wait{propertyName} / Assert{propertyName})");
        writer.WriteLine();

        // Generate Is*() method
        GenerateIsMethod(writer, coreMethod, propertyName);
        writer.WriteLine();

        // Generate Wait*(bool? expected, int? timeoutMs = null) method
        GenerateWaitMethod(writer, coreMethod, propertyName);
        writer.WriteLine();

        // Generate Assert*(bool? expected, string? message = null, int? timeoutMs = null) method
        GenerateAssertMethod(writer, coreMethod, propertyName, fluentReturnType);
        writer.WriteLine();

        // Write region end
        writer.WriteLine("#endregion");

        return writer.ToString();
    }

    /// <summary>
    /// Generates the Get*/Wait*/Assert* trio from a single Get*Core method.
    /// </summary>
    private string GenerateGetter(MethodInfo coreMethod, string fluentReturnType)
    {
        var propertyName = coreMethod.PublicMethodName;
        var returnType = coreMethod.ReturnType;
        var nullableReturnType = returnType.EndsWith("?", StringComparison.Ordinal) ? returnType : returnType + "?";
        var paramPrefix = BuildParameterListPrefix(coreMethod);
        var lambdaArgs = BuildLambdaArguments(coreMethod);
        var writer = new CsWriter(0);

        // Write region start
        writer.WriteLine($"#region {propertyName} (Get{propertyName} / Wait{propertyName} / Assert{propertyName})");
        writer.WriteLine();

        // Get{PropertyName}(...) getter
        writer.WriteLine(GeneratedMember.Attribute);
        writer.WriteLine($"public {nullableReturnType} Get{propertyName}({paramPrefix}int? timeoutMs = null)");
        writer.Open();
        writer.WriteLine($"return {GetHelper(coreMethod)}(element => {coreMethod.MethodName}(element{lambdaArgs}), timeoutMs);");
        writer.Close();
        writer.WriteLine();

        // SequenceEquals supplies Wait/Assert{PropertyName} with element-wise semantics, so
        // the reference-comparing default is suppressed rather than emitted and shadowed —
        // two members of the same name would not compile.
        var hasSequenceEquality = coreMethod.Comparisons.Contains("SequenceEquals");

        if (!hasSequenceEquality)
        {
            // Wait{PropertyName}(...) waiter
            writer.WriteLine(GeneratedMember.Attribute);
            writer.WriteLine($"public bool Wait{propertyName}({paramPrefix}{nullableReturnType} expected, int? timeoutMs = null)");
            writer.Open();
            writer.WriteLine($"return {WaitHelper(coreMethod)}(expected,");
            writer.IncreaseSpace(1);
            writer.WriteLine($"element => {coreMethod.MethodName}(element{lambdaArgs}) == expected,");
            writer.WriteLine("timeoutMs);");
            writer.DecreaseSpace(1);
            writer.Close();
            writer.WriteLine();

            // Assert{PropertyName}(...) assertion
            writer.WriteLine(GeneratedMember.Attribute);
            writer.WriteLine($"public {fluentReturnType} Assert{propertyName}({paramPrefix}{nullableReturnType} expected, string? message = null, int? timeoutMs = null)");
            writer.Open();
            writer.WriteLine($"return {AssertHelper(coreMethod)}(expected,");
            writer.IncreaseSpace(1);
            writer.WriteLine($"element => {coreMethod.MethodName}(element{lambdaArgs}), (actual, expected1) => (actual == expected1),");
            writer.WriteLine($"{BuildAssertMessage(propertyName)}, timeoutMs);");
            writer.DecreaseSpace(1);
            writer.Close();
            writer.WriteLine();
        }

        // Additional comparison variants declared via [GenerateComparisons].
        foreach (var comparison in coreMethod.Comparisons.Where(c => c != "Equals"))
        {
            GenerateComparisonVariant(writer, coreMethod, propertyName, comparison,
                paramPrefix, nullableReturnType, lambdaArgs, fluentReturnType);
            writer.WriteLine();
        }

        // Write region end
        writer.WriteLine("#endregion");

        return writer.ToString();
    }

    /// <summary>
    /// Emits a Wait/Assert pair for one non-equality comparison variant, e.g.
    /// <c>WaitTextContains</c> / <c>AssertTextContains</c>.
    /// </summary>
    private void GenerateComparisonVariant(CsWriter writer, MethodInfo coreMethod,
        string propertyName, string comparison, string paramPrefix,
        string nullableReturnType, string lambdaArgs, string fluentReturnType)
    {
        var memberName = $"{propertyName}{comparison}";

        if (comparison == "Empty")
        {
            // Empty is a bool? predicate over the value, not a value comparison.
            writer.WriteLine(GeneratedMember.Attribute);
            writer.WriteLine($"public bool Wait{memberName}({paramPrefix}bool? expected = true, int? timeoutMs = null)");
            writer.Open();
            writer.WriteLine($"return {WaitHelper(coreMethod)}(expected,");
            writer.IncreaseSpace(1);
            writer.WriteLine($"element => string.IsNullOrEmpty({coreMethod.MethodName}(element{lambdaArgs})) == expected,");
            writer.WriteLine("timeoutMs);");
            writer.DecreaseSpace(1);
            writer.Close();
            writer.WriteLine();

            writer.WriteLine(GeneratedMember.Attribute);
            writer.WriteLine($"public {fluentReturnType} Assert{memberName}({paramPrefix}bool? expected = true, string? message = null, int? timeoutMs = null)");
            writer.Open();
            writer.WriteLine($"return {AssertHelper(coreMethod)}(expected,");
            writer.IncreaseSpace(1);
            writer.WriteLine($"element => (bool?)string.IsNullOrEmpty({coreMethod.MethodName}(element{lambdaArgs})), (actual, expected1) => (actual == expected1),");
            writer.WriteLine($"{BuildAssertMessage(memberName)}, timeoutMs);");
            writer.DecreaseSpace(1);
            writer.Close();
            return;
        }

        if (comparison is "SequenceEquals" or "HasItem" or "Count")
        {
            GenerateCollectionVariant(writer, coreMethod, propertyName, comparison,
                paramPrefix, nullableReturnType, lambdaArgs, fluentReturnType);
            return;
        }

        if (OrderedComparisons.TryGetValue(comparison, out var ordered))
        {
            // Lifted operators: a null actual compares false, so an unreadable value never passes.
            writer.WriteLine(GeneratedMember.Attribute);
            writer.WriteLine($"public bool Wait{memberName}({paramPrefix}{nullableReturnType} expected, int? timeoutMs = null)");
            writer.Open();
            writer.WriteLine($"return {WaitHelper(coreMethod)}(expected,");
            writer.IncreaseSpace(1);
            writer.WriteLine($"element => {coreMethod.MethodName}(element{lambdaArgs}) {ordered.Operator} expected,");
            writer.WriteLine("timeoutMs);");
            writer.DecreaseSpace(1);
            writer.Close();
            writer.WriteLine();

            writer.WriteLine(GeneratedMember.Attribute);
            writer.WriteLine($"public {fluentReturnType} Assert{memberName}({paramPrefix}{nullableReturnType} expected, string? message = null, int? timeoutMs = null)");
            writer.Open();
            writer.WriteLine($"return {AssertHelper(coreMethod)}(expected,");
            writer.IncreaseSpace(1);
            writer.WriteLine($"element => {coreMethod.MethodName}(element{lambdaArgs}), (actual, expected1) => (actual {ordered.Operator} expected1),");
            writer.WriteLine($"message ?? $\"Expected {propertyName} to be {ordered.Words} '{{expected}}'. Locator: {{Locator}}\", timeoutMs);");
            writer.DecreaseSpace(1);
            writer.Close();
            return;
        }

        // StartsWith and EndsWith compare ordinally, as Contains and == do.
        var ordinal = comparison is "StartsWith" or "EndsWith" ? ", StringComparison.Ordinal" : string.Empty;
        var predicate = $"actual?.{comparison}(expected1!{ordinal}) == true";

        writer.WriteLine(GeneratedMember.Attribute);
        writer.WriteLine($"public bool Wait{memberName}({paramPrefix}{nullableReturnType} expected, int? timeoutMs = null)");
        writer.Open();
        writer.WriteLine($"return {WaitHelper(coreMethod)}(expected,");
        writer.IncreaseSpace(1);
        writer.WriteLine($"element => {coreMethod.MethodName}(element{lambdaArgs})?.{comparison}(expected!{ordinal}) == true,");
        writer.WriteLine("timeoutMs);");
        writer.DecreaseSpace(1);
        writer.Close();
        writer.WriteLine();

        writer.WriteLine(GeneratedMember.Attribute);
        writer.WriteLine($"public {fluentReturnType} Assert{memberName}({paramPrefix}{nullableReturnType} expected, string? message = null, int? timeoutMs = null)");
        writer.Open();
        writer.WriteLine($"return {AssertHelper(coreMethod)}(expected,");
        writer.IncreaseSpace(1);
        writer.WriteLine($"element => {coreMethod.MethodName}(element{lambdaArgs}), (actual, expected1) => ({predicate}),");
        writer.WriteLine($"{BuildAssertMessage(memberName)}, timeoutMs);");
        writer.DecreaseSpace(1);
        writer.Close();
    }

    /// <summary>
    /// Emits a Wait/Assert pair for a <c>Get*Core</c> that returns a collection.
    /// </summary>
    private void GenerateCollectionVariant(CsWriter writer, MethodInfo coreMethod,
        string propertyName, string comparison, string paramPrefix,
        string nullableReturnType, string lambdaArgs, string fluentReturnType)
    {
        var memberName = comparison == "SequenceEquals" ? propertyName : $"{propertyName}{comparison}";
        var call = $"{coreMethod.MethodName}(element{lambdaArgs})";

        // HasItem asks a yes/no question about the collection, so — like the Empty variant —
        // both the parameter and the compared value are bool?. Emitting it as
        // "expected: an item, actual: the collection" would not type-check:
        // RunAssertWithElement<T> needs one T for both sides.
        if (comparison == "HasItem")
        {
            var itemType = ElementTypeOf(nullableReturnType);

            writer.WriteLine(GeneratedMember.Attribute);
            writer.WriteLine($"public bool Wait{memberName}({paramPrefix}{itemType} item, int? timeoutMs = null)");
            writer.Open();
            writer.WriteLine($"return {WaitHelper(coreMethod)}(item,");
            writer.IncreaseSpace(1);
            writer.WriteLine($"element => {call}?.Contains(item!) == true,");
            writer.WriteLine("timeoutMs);");
            writer.DecreaseSpace(1);
            writer.Close();
            writer.WriteLine();

            writer.WriteLine(GeneratedMember.Attribute);
            writer.WriteLine($"public {fluentReturnType} Assert{memberName}({paramPrefix}{itemType} item, string? message = null, int? timeoutMs = null)");
            writer.Open();
            writer.WriteLine($"return {AssertHelper(coreMethod)}((bool?)true,");
            writer.IncreaseSpace(1);
            writer.WriteLine($"element => (bool?)({call}?.Contains(item!) == true), (actual, expected1) => (actual == expected1),");
            writer.WriteLine($"message ?? $\"Expected {memberName} to contain '{{item}}'. Locator: {{Locator}}\", timeoutMs);");
            writer.DecreaseSpace(1);
            writer.Close();
            return;
        }

        var (expectedType, waitPredicate, assertActual, assertPredicate) = comparison switch
        {
            "SequenceEquals" => (
                nullableReturnType,
                $"{call}?.SequenceEqual(expected!) == true",
                call,
                "actual?.SequenceEqual(expected1!) == true"),

            // Count compares an int against an int; the actual value is projected inside the
            // lambda so both sides of RunAssertWithElement<T> agree on T.
            _ => (
                "int?",
                $"{call}?.Count() == expected",
                $"(int?)({call}?.Count())",
                "actual == expected1"),
        };

        writer.WriteLine(GeneratedMember.Attribute);
        writer.WriteLine($"public bool Wait{memberName}({paramPrefix}{expectedType} expected, int? timeoutMs = null)");
        writer.Open();
        writer.WriteLine($"return {WaitHelper(coreMethod)}(expected,");
        writer.IncreaseSpace(1);
        writer.WriteLine($"element => {waitPredicate},");
        writer.WriteLine("timeoutMs);");
        writer.DecreaseSpace(1);
        writer.Close();
        writer.WriteLine();

        writer.WriteLine(GeneratedMember.Attribute);
        writer.WriteLine($"public {fluentReturnType} Assert{memberName}({paramPrefix}{expectedType} expected, string? message = null, int? timeoutMs = null)");
        writer.Open();
        writer.WriteLine($"return {AssertHelper(coreMethod)}(expected,");
        writer.IncreaseSpace(1);
        writer.WriteLine($"element => {assertActual}, (actual, expected1) => ({assertPredicate}),");
        writer.WriteLine($"{BuildAssertMessage(memberName)}, timeoutMs);");
        writer.DecreaseSpace(1);
        writer.Close();
    }

    /// <summary>
    /// The item type of a collection return type, for variants that take one item.
    /// </summary>
    private static string ElementTypeOf(string collectionType)
    {
        var open = collectionType.IndexOf('<');
        var close = collectionType.LastIndexOf('>');
        if (open < 0 || close < open)
            return collectionType;

        var inner = collectionType[(open + 1)..close].Trim();

        // A nested generic argument (Dictionary<K,V>) is not a single-item collection.
        return inner.Contains(',') ? collectionType : inner;
    }

    /// <summary>
    /// Builds the message argument for a generated Assert. The caller's
    /// <c>message</c> wins; otherwise a diagnostic naming the property, the expected
    /// value, and the locator is synthesized so failures stay readable.
    /// </summary>
    private static string BuildAssertMessage(string propertyName)
    {
        return $"message ?? $\"Expected {propertyName} to be '{{expected}}'. Locator: {{Locator}}\"";
    }

    private static string BuildParameterListPrefix(MethodInfo coreMethod)
    {
        if (coreMethod.Parameters.Count == 0)
            return "";

        var parts = coreMethod.Parameters.Select(p => $"{p.TypeName} {p.ParameterName}");
        return string.Join(", ", parts) + ", ";
    }

    private static string BuildLambdaArguments(MethodInfo coreMethod)
    {
        if (coreMethod.Parameters.Count == 0)
            return "";

        return ", " + string.Join(", ", coreMethod.Parameters.Select(p => p.ParameterName));
    }

    #region IsExists / WaitExists / AssertExists

    /// <summary>
    /// Generates public bool Is{PropertyName}() method.
    /// </summary>
    private void GenerateIsMethod(CsWriter writer, MethodInfo coreMethod, string propertyName)
    {
        var parameters = string.Join(", ", coreMethod.Parameters.Select(p => $"{p.TypeName} {p.ParameterName}"));

        writer.WriteLine(GeneratedMember.Attribute);
        writer.WriteLine($"public {coreMethod.ReturnType} Is{propertyName}({parameters})");
        writer.Open();
        writer.WriteLine($"return {coreMethod.MethodName}(TryFindElement(){BuildLambdaArguments(coreMethod)}) == true;");
        writer.Close();
    }

    /// <summary>
    /// Generates public Wait{PropertyName}(bool? expected, int? timeoutMs = null) method.
    /// The return type mirrors the Is*Core method (bool or bool?).
    /// </summary>
    private void GenerateWaitMethod(CsWriter writer, MethodInfo coreMethod, string propertyName)
    {
        // An absence-tolerant query resolves the element optionally, so asking for the
        // absent state reports it instead of raising ElementNotFoundException.
        var helper = coreMethod.IsAbsenceTolerant
            ? "RunWaitWithOptionalElement"
            : "RunWaitWithElement";

        writer.WriteLine(GeneratedMember.Attribute);
        writer.WriteLine($"public bool Wait{propertyName}({BuildParameterListPrefix(coreMethod)}bool? expected = true, int? timeoutMs = null)");
        writer.Open();
        writer.WriteLine($"return {helper}(expected,");
        writer.IncreaseSpace(1);
        writer.WriteLine($"element => {coreMethod.MethodName}(element{BuildLambdaArguments(coreMethod)}) == expected!.Value,");
        writer.WriteLine("timeoutMs);");
        writer.DecreaseSpace(1);
        writer.Close();
    }

    /// <summary>
    /// Generates the public Assert{PropertyName}(bool? expected, string? message = null, int? timeoutMs = null)
    /// method, returning the resolved fluent type for chaining.
    /// </summary>
    private void GenerateAssertMethod(CsWriter writer, MethodInfo coreMethod, string propertyName,
        string fluentReturnType)
    {
        var helper = coreMethod.IsAbsenceTolerant
            ? "RunAssertWithOptionalElement"
            : "RunAssertWithElement";

        // A method group when the Core method takes only the element, so the output is unchanged
        // for every existing state query; a lambda when it takes more.
        var actual = coreMethod.Parameters.Count == 0
            ? coreMethod.MethodName
            : $"element => {coreMethod.MethodName}(element{BuildLambdaArguments(coreMethod)})";

        writer.WriteLine(GeneratedMember.Attribute);
        writer.WriteLine($"public {fluentReturnType} Assert{propertyName}({BuildParameterListPrefix(coreMethod)}bool? expected = true, string? message = null, int? timeoutMs = null)");
        writer.Open();
        writer.WriteLine($"return {helper}(expected,");
        writer.IncreaseSpace(1);
        writer.WriteLine($"{actual}, (actual, expected1) => (actual == expected1),");
        writer.WriteLine($"{BuildAssertMessage(propertyName)}, timeoutMs);");
        writer.DecreaseSpace(1);
        writer.Close();
    }

    #endregion

    private string? ExtractXmlDocumentation(MethodDeclarationSyntax method)
    {
        var trivia = method.GetLeadingTrivia();
        var xmlTrivia = trivia
            .FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                                 t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

        return xmlTrivia.IsKind(SyntaxKind.None) ? null : xmlTrivia.ToString();
    }
}
