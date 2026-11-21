using System.Collections.Immutable;
using Bruinet.Api.Mocking;
using Bruinet.Api.Mocking.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Bruinet.Api.Mocking.Tests.Analyzers;

public sealed class AnonymousMockBodyAnalyzerTests
{
    [Fact]
    public async Task AnAnonymousBody_IsReported()
    {
        var diagnostics = await AnalyzeAsync("""server.Get("/a").ReturnsJson(new { mode = "x" });""");

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(AnonymousMockBodyAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public async Task AnArrayOfAnonymousItems_IsReported()
        => Assert.Single(await AnalyzeAsync("""server.Get("/a").ReturnsJson(new[] { new { id = 1 } });"""));

    [Fact]
    public async Task AListOfAnonymousItems_IsReported()
        => Assert.Single(await AnalyzeAsync("""server.Get("/a").ReturnsJson(new[] { new { id = 1 } }.ToList());"""));

    [Fact]
    public async Task AnAnonymousBodyAfterOtherSetup_IsReported()
        => Assert.Single(await AnalyzeAsync("""server.Get("/a").AtPriority(1).FailsFirst(1, 503).ReturnsJson(new { id = 1 }, 201);"""));

    [Fact]
    public async Task AnAnonymousBodyInACallback_IsReported()
        => Assert.Single(await AnalyzeAsync("""server.Get("/a").RespondsWith(_ => MockResponse.Json(new { id = 1 }));"""));

    [Fact]
    public async Task ADtoBody_IsFine()
        => Assert.Empty(await AnalyzeAsync("""
            server.Get("/a").ReturnsJson(new Dto(1, "x"));
            server.Get("/b").ReturnsJson(new[] { new Dto(1, "x") });
            server.Get("/c").RespondsWith(_ => MockResponse.Json(new List<Dto>()));
            """));

    [Fact]
    public async Task AnotherMethodTakingAnAnonymousObject_IsFine()
        => Assert.Empty(await AnalyzeAsync("""System.Console.WriteLine(new { id = 1 });"""));

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string statements)
    {
        var source = $$"""
            using System.Collections.Generic;
            using System.Linq;
            using Bruinet.Api.Mocking;

            public sealed record Dto(int Id, string Name);

            public static class Snippet
            {
                public static void Run(MockApiServer server)
                {
                    {{statements}}
                }
            }
            """;

        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Append(typeof(MockApiServer).Assembly.Location)
            .Select(path => MetadataReference.CreateFromFile(path));

        var compilation = CSharpCompilation.Create(
            "Snippet",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, "The snippet does not compile: " + string.Join("; ", errors));

        return await compilation
            .WithAnalyzers([new AnonymousMockBodyAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();
    }
}
