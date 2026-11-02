using Microsoft.CodeAnalysis.CSharp.Syntax;
using Bruinet.Generator.Analysis;
using Bruinet.Generator.Models;
using Bruinet.Generator.Writers;

namespace Bruinet.Generator.Generators;

/// <summary>
/// Emits a component's public members from its shortcuts: one-line forwards to a part.
/// </summary>
/// <remarks>
/// <para>
/// Every emitted member is a single call on the part, with no <c>Run*</c> wrapper. The part's
/// member is already a complete unit of work - readiness, lookup, poll, log - so wrapping it
/// again would repeat all of that inside a second poll with its own timeout.
/// </para>
/// <para>
/// <c>IsPlayingShortcut() => PlayPauseButton.IsPlaying()</c> emits <c>IsPlaying</c>,
/// <c>WaitPlaying</c> and <c>AssertPlaying</c>, forwarding to the part's members of the same
/// names. <c>GetProgressShortcut() => ProgressSlider.GetValue()</c> emits <c>GetProgress</c>,
/// <c>WaitProgress</c> and <c>AssertProgress</c>, forwarding to <c>GetValue</c>,
/// <c>WaitValue</c> and <c>AssertValue</c>, plus any <c>[GenerateComparisons]</c> variants.
/// Any other shortcut is emitted as itself, made public under its new name.
/// </para>
/// </remarks>
public class ShortcutGenerator : IMemberGenerator
{
    /// <inheritdoc />
    /// <remarks>
    /// Malformed shortcuts are not matched; <see cref="ControlObjectAnalyzer.FindMalformedShortcuts"/>
    /// reports them before generation starts.
    /// </remarks>
    public bool Matches(MethodDeclarationSyntax method)
        => ShortcutMethod.IsCandidate(method) && ShortcutMethod.TryParse(method, out _, out _);

    /// <inheritdoc />
    public MethodInfo Extract(MethodDeclarationSyntax method)
    {
        if (!ShortcutMethod.TryParse(method, out var shortcut, out var error))
            throw new InvalidOperationException(error);

        var info = new MethodInfo
        {
            MethodName = shortcut!.MethodName,
            PublicMethodName = shortcut.PublicName,
            ReturnType = shortcut.ReturnType,
            Shortcut = shortcut
        };

        if (shortcut.Kind == ShortcutKind.Getter)
        {
            info.Comparisons.AddRange(IsWaitAssertGenerator.ExtractComparisons(method));
        }

        return info;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> EmittedMemberNames(MethodInfo coreMethod)
    {
        var shortcut = Shortcut(coreMethod);

        return shortcut.Kind switch
        {
            ShortcutKind.State => ["Is" + shortcut.Stem, "Wait" + shortcut.Stem, "Assert" + shortcut.Stem],
            ShortcutKind.Getter => ["Get" + shortcut.Stem, "Wait" + shortcut.Stem, "Assert" + shortcut.Stem],
            _ => [shortcut.PublicName]
        };
    }

    /// <inheritdoc />
    public string Generate(MethodInfo coreMethod, ControlObjectContext context)
    {
        var shortcut = Shortcut(coreMethod);
        var fluentReturnType = string.IsNullOrEmpty(context.FluentReturnType) ? "void" : context.FluentReturnType;
        var writer = new CsWriter(0);

        writer.WriteLine($"#region {shortcut.PublicName} (shortcut to {shortcut.Part}.{shortcut.PartMember})");
        writer.WriteLine();

        switch (shortcut.Kind)
        {
            case ShortcutKind.State:
                GenerateState(writer, shortcut, fluentReturnType);
                break;
            case ShortcutKind.Getter:
                GenerateGetter(writer, shortcut, coreMethod.Comparisons, fluentReturnType);
                break;
            default:
                writer.WriteLine(GeneratedMember.Attribute);
                writer.WriteLine($"public {shortcut.ReturnType} {shortcut.PublicName}{shortcut.ParameterListText}");
                writer.IncreaseSpace(1);
                writer.WriteLine($"=> {shortcut.Invocation};");
                writer.DecreaseSpace(1);
                writer.WriteLine();
                break;
        }

        writer.WriteLine("#endregion");
        return writer.ToString();
    }

    private static void GenerateState(CsWriter writer, ShortcutMethod shortcut, string fluentReturnType)
    {
        var part = shortcut.Part;
        var stem = shortcut.Stem;
        var partStem = shortcut.PartStem;

        // As for a getter, the shortcut's parameters and arguments lead each signature and call,
        // where the part's generated trio puts its Core method's extra parameters.
        var parameters = string.Concat(shortcut.Parameters.Select(p => $"{p.TypeName} {p.ParameterName}, "));
        var arguments = string.Concat(shortcut.Arguments.Select(a => a + ", "));

        Forward(writer, $"public {shortcut.ReturnType} Is{stem}({parameters.TrimEnd(',', ' ')})",
            $"{part}.Is{partStem}({arguments.TrimEnd(',', ' ')})");
        Forward(writer, $"public bool Wait{stem}({parameters}bool? expected = true, int? timeoutMs = null)",
            $"{part}.Wait{partStem}({arguments}expected, timeoutMs)");
        Forward(writer, $"public {fluentReturnType} Assert{stem}({parameters}bool? expected = true, string? message = null, int? timeoutMs = null)",
            $"{part}.Assert{partStem}({arguments}expected, message, timeoutMs)");
    }

    private static void GenerateGetter(CsWriter writer, ShortcutMethod shortcut,
        IReadOnlyList<string> comparisons, string fluentReturnType)
    {
        var part = shortcut.Part;
        var stem = shortcut.Stem;
        var partStem = shortcut.PartStem;
        var valueType = shortcut.ReturnType.EndsWith('?') ? shortcut.ReturnType : shortcut.ReturnType + "?";

        // The shortcut's own parameters and arguments lead every signature and call, in the
        // same position the part's generated members give its Core method's extra parameters.
        var parameters = string.Concat(shortcut.Parameters.Select(p => $"{p.TypeName} {p.ParameterName}, "));
        var arguments = string.Concat(shortcut.Arguments.Select(a => a + ", "));

        Forward(writer, $"public {valueType} Get{stem}({parameters}int? timeoutMs = null)",
            $"{part}.Get{partStem}({arguments}timeoutMs)");

        // SequenceEquals replaces equality under the same names, on the part as here.
        if (comparisons.Contains("Equals") || comparisons.Contains("SequenceEquals"))
        {
            ForwardPair(writer, part, fluentReturnType, stem, partStem, "",
                parameters, arguments, $"{valueType} expected", "expected");
        }

        foreach (var comparison in comparisons.Where(c => c is not ("Equals" or "SequenceEquals")))
        {
            var (declaration, argument) = comparison switch
            {
                "Empty" => ("bool? expected = true", "expected"),
                "HasItem" => ($"{ItemTypeOf(valueType)} item", "item"),
                "Count" => ("int? expected", "expected"),
                _ => ($"{valueType} expected", "expected")
            };

            ForwardPair(writer, part, fluentReturnType, stem, partStem, comparison,
                parameters, arguments, declaration, argument);
        }
    }

    /// <summary>
    /// Emits the Wait/Assert pair for one comparison, forwarding to the part's pair.
    /// </summary>
    private static void ForwardPair(CsWriter writer, string part, string fluentReturnType,
        string stem, string partStem, string comparison, string parameters, string arguments,
        string expectedDeclaration, string expectedArgument)
    {
        Forward(writer,
            $"public bool Wait{stem}{comparison}({parameters}{expectedDeclaration}, int? timeoutMs = null)",
            $"{part}.Wait{partStem}{comparison}({arguments}{expectedArgument}, timeoutMs)");
        Forward(writer,
            $"public {fluentReturnType} Assert{stem}{comparison}({parameters}{expectedDeclaration}, string? message = null, int? timeoutMs = null)",
            $"{part}.Assert{partStem}{comparison}({arguments}{expectedArgument}, message, timeoutMs)");
    }

    private static void Forward(CsWriter writer, string signature, string call)
    {
        writer.WriteLine(signature);
        writer.IncreaseSpace(1);
        writer.WriteLine($"=> {call};");
        writer.DecreaseSpace(1);
        writer.WriteLine();
    }

    /// <summary>
    /// The item type of a collection, for HasItem; the same syntactic reading the part's
    /// generator uses, so the two signatures agree.
    /// </summary>
    private static string ItemTypeOf(string collectionType)
    {
        var open = collectionType.IndexOf('<');
        var close = collectionType.LastIndexOf('>');
        if (open < 0 || close < open)
            return collectionType;

        var inner = collectionType[(open + 1)..close].Trim();
        return inner.Contains(',') ? collectionType : inner;
    }

    private static ShortcutMethod Shortcut(MethodInfo coreMethod)
        => coreMethod.Shortcut
           ?? throw new InvalidOperationException(
               $"'{coreMethod.MethodName}' was not extracted as a shortcut.");
}
