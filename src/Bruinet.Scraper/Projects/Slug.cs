using System.Text;

namespace Bruinet.Scraper.Projects;

/// <summary>A name made fit for a file name: lower case letters and digits joined by dashes.</summary>
public static class Slug
{
    /// <summary>The slug of <paramref name="text"/>, at most 40 characters; <paramref name="fallback"/> when nothing is left.</summary>
    public static string Of(string? text, string fallback)
    {
        var builder = new StringBuilder();
        foreach (var c in (text ?? "").Normalize(NormalizationForm.FormD))
        {
            if (char.IsAsciiLetterOrDigit(c))
                builder.Append(char.ToLowerInvariant(c));
            else if (char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark)
                continue;
            else if (builder.Length > 0 && builder[^1] != '-')
                builder.Append('-');
            if (builder.Length >= 40)
                break;
        }
        var slug = builder.ToString().Trim('-');
        return slug.Length > 0 ? slug : fallback;
    }
}
