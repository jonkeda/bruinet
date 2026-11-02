using Microsoft.Maui;

namespace MauiHeadless.Internals;

/// <summary>A field's <see cref="TextTransform"/>, applied as MAUI's platform handlers apply it.</summary>
internal static class TextTransforms
{
    public static string? Apply(string? text, TextTransform transform) => transform switch
    {
        TextTransform.Uppercase => text?.ToUpperInvariant(),
        TextTransform.Lowercase => text?.ToLowerInvariant(),
        _ => text,
    };
}
