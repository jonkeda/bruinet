using Microsoft.CodeAnalysis.CSharp.Syntax;
using Bruinet.Generator.Models;

namespace Bruinet.Generator.Generators;

/// <summary>
/// Recognizes and emits a family of public ControlObject members from a base
/// Core method. Member generators are registered with
/// <see cref="ControlObjectGenerator"/> and run over the analyzed source.
/// </summary>
public interface IMemberGenerator
{
    /// <summary>
    /// Determines whether this generator handles the given Core method.
    /// </summary>
    /// <param name="method">The method syntax to evaluate.</param>
    /// <returns>True if this generator should process the method; otherwise, false.</returns>
    bool Matches(MethodDeclarationSyntax method);

    /// <summary>
    /// Extracts portable metadata from a matched Core method.
    /// </summary>
    /// <param name="method">The method syntax to extract from.</param>
    /// <returns>Portable method metadata for code generation.</returns>
    MethodInfo Extract(MethodDeclarationSyntax method);

    /// <summary>
    /// The names of the public members this generator will emit for one Core method.
    /// </summary>
    /// <remarks>
    /// Used to detect real collisions between Core methods. It is the emitted names that can
    /// clash, not the stem they share: <c>OpenCore</c> emits <c>Open</c> while
    /// <c>IsOpenCore</c> emits <c>IsOpen</c>, <c>WaitOpen</c> and <c>AssertOpen</c>, so the two
    /// coexist even though both are "about Open".
    /// </remarks>
    /// <param name="coreMethod">The extracted core method metadata.</param>
    /// <returns>Every member name that <see cref="Generate"/> will declare.</returns>
    IReadOnlyList<string> EmittedMemberNames(MethodInfo coreMethod);

    /// <summary>
    /// Emits the public member(s) for one Core method as formatted C# code.
    /// </summary>
    /// <param name="coreMethod">The extracted core method metadata.</param>
    /// <param name="context">The analyzed ControlObject context.</param>
    /// <returns>The generated member(s) as a formatted C# code string.</returns>
    string Generate(MethodInfo coreMethod, ControlObjectContext context);
}

/// <summary>
/// The marker on every member the generator writes. Not on the class: a control's class is partial, and its
/// hand-written part (<c>.tpl.cs</c>) is not generated. Coverage leaves members with it out
/// (<c>coverage.runsettings</c>), as the generator is tested on its own.
/// </summary>
internal static class GeneratedMember
{
    /// <summary>The attribute line, as it is written above a member.</summary>
    public const string Attribute = "[global::System.CodeDom.Compiler.GeneratedCode(\"Bruinet.Generator\", \"1.0\")]";
}
