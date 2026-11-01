using System.Globalization;
using System.Net;
using System.Text;

namespace Bruinet.Bdd.Preview;

// The narrator in the preview: a Record button, and the videos already recorded, to watch.
internal static class BddPreviewRecord
{
    /// <summary>A Record button; empty when the host offers no recording.</summary>
    public static string Button(BddPreviewRunState? record, string attribute, string label, string title)
    {
        if (record is null)
        {
            return string.Empty;
        }

        var state = record.CanRun
            ? $"title=\"{Encode(title)}\""
            : $"disabled title=\"{Encode(record.Reason ?? "This cannot be recorded from here.")}\"";
        return $" <button type=\"button\" class=\"uat-run uat-record\" {attribute} {state}>⏺ {label}</button>";
    }

    /// <summary>The recorded videos, newest first; empty when there are none.</summary>
    public static string List(IReadOnlyList<BddPreviewRecording> recordings)
    {
        if (recordings.Count == 0)
        {
            return string.Empty;
        }

        var html = new StringBuilder("<details class=\"uat-recordings\"><summary>Recordings (")
            .Append(recordings.Count.ToString(CultureInfo.InvariantCulture))
            .Append(")</summary><ul>");
        foreach (var recording in recordings)
        {
            html.Append(CultureInfo.InvariantCulture, $"<li><a href=\"#\" data-watch=\"{Encode(recording.Path)}\" title=\"Watch {Encode(recording.Path)}\">{Encode(recording.Name)}</a> <span class=\"uat-diag-where\">{recording.Written.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}</span></li>");
        }

        return html.Append("</ul></details>\n").ToString();
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
