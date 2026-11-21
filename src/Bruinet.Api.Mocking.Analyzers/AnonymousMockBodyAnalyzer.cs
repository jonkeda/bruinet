using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Bruinet.Api.Mocking.Analyzers;

/// <summary>
/// BRUINET001: a mock response body is an anonymous type instead of the API's contract type.
/// The rule is AD-012.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AnonymousMockBodyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic id.</summary>
    public const string DiagnosticId = "BRUINET001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Mock response bodies use the API's contract types",
        messageFormat: "The mock response body is an anonymous type; use the API's DTO, or ReturnsSample with a recorded response",
        category: "Bruinet.Api.Mocking",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An anonymous body is not checked against the contract: a misspelled or missing field still compiles, and the app under test reads a body the real API never sends.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (!IsBodyMethod(invocation.TargetMethod))
        {
            return;
        }

        var body = invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == "body");
        if (body is null)
        {
            return;
        }

        var value = body.Value;
        while (value is IConversionOperation { IsImplicit: true } conversion)
        {
            value = conversion.Operand;
        }

        if (ContainsAnonymousType(value.Type))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, body.Syntax.GetLocation()));
        }
    }

    private static bool IsBodyMethod(IMethodSymbol method)
    {
        var type = method.ContainingType?.ToDisplayString();
        return (method.Name == "ReturnsJson" && type == "Bruinet.Api.Mocking.ApiStubBuilder")
            || (method.Name == "Json" && type == "Bruinet.Api.Mocking.MockResponse");
    }

    private static bool ContainsAnonymousType(ITypeSymbol? type) => type switch
    {
        null => false,
        { IsAnonymousType: true } => true,
        IArrayTypeSymbol array => ContainsAnonymousType(array.ElementType),
        INamedTypeSymbol named => named.TypeArguments.Any(ContainsAnonymousType),
        _ => false,
    };
}
