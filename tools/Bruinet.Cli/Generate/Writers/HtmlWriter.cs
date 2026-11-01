using System.Globalization;

namespace Bruinet.Generator.Writers;

public class HtmlWriter : CodeWriter
{
    public HtmlWriter() : base("", "", "<!--", "-->")
    {
    }

    public void Tag(string tag, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            text = "&nbsp;";
        }
        WriteLine($"<{tag}>{text}</{tag}>");
    }

    public void Tag(string text, string attribute, string value)
    {
        WriteLine($"<{text} {attribute}=\"{value}\">");
    }

    /// <summary>
    /// Opens and close a tag. &lt;TAG/>
    /// </summary>
    /// <param name="tag">The tag.</param>
    public void TagFull(string tag)
    {
        WriteLine($"<{tag}/>");
    }

    /// <summary>
    /// Opens and close a tag. &lt;TAG>
    /// </summary>
    /// <param name="tag">The tag.</param>
    public void TagOpen(string tag)
    {
        Open($"<{tag}>");
    }

    /// <summary>
    /// Close a tag. &lt;/TAG>
    /// </summary>
    /// <param name="tag">The tag.</param>
    public void TagClose(string tag)
    {
        Close($"</{tag}>");
    }

    /// <summary>
    /// Opens a tag. &lt;TAG
    /// </summary>
    /// <param name="tag">The tag.</param>
    public void TagStart(string tag)
    {
        WriteStart($"<{tag}");
    }

    /// <summary>
    /// Close the tags with an end. >
    /// </summary>
    public void TagEnd()
    {
        WriteEnd(">");
        Spaces++;
    }

    /// <summary>
    /// Close the tags with a full end. />
    /// </summary>
    public void TagFullEnd()
    {
        WriteEnd("/>");
    }

    public override void Empty()
    {
        WriteLine("<br>");
    }

    public void BodyOpen()
    {
        TagOpen("html");

        WriteLine("<head>");
        WriteLine("    <style>");
        WriteLine("        body {");
        WriteLine("            font-family: arial, sans-serif;");
        WriteLine("        }");

        WriteLine("        table {");
        WriteLine("            border-collapse: collapse;");
        WriteLine("        }");

        WriteLine("        td, th {");
        WriteLine("            border: 1px solid #dddddd;");
        WriteLine("            padding: 8px;");
        WriteLine("        }");

        WriteLine("        tr:nth-child(even) {");
        WriteLine("            background-color: #dddddd;");
        WriteLine("        }");
        WriteLine("    </style>");
        WriteLine("</head>");

        TagOpen("body");
    }

    public void BodyClose()
    {
        TagClose("body");
        TagClose("html");
    }

    public void RenderLabelValueNotNull(string label, object? value)
    {
        if (value != null)
        {
            RenderLabelValue(label, value);
        }
    }

    public void RenderLabelValue(string label, object? value)
    {
        TagOpen("tr");
        Tag("th", label);
        if (value != null)
        {
            Tag("td", value.ToString() ?? " ");
        }
        else
        {
            Tag("td", " ");
        }
        TagClose("tr");
    }

    public void Attribute(string name, string? value)
    {
        if (value == null)
        {
            return;
        }
        Write(" ");
        Write(name);
        Write("=\"");
        Write(value);
        Write("\"");
    }

    private void Attribute(string name, int value, bool showZeros = true)
    {
        if (value != 0
            || showZeros)
        {
            Attribute(name, value.ToString(CultureInfo.InvariantCulture));
        }
    }
}
