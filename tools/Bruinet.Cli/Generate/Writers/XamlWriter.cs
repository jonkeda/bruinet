namespace Bruinet.Generator.Writers;

public class XamlWriter : CodeWriter
{
    public XamlWriter() : base("", "", "<!--", "-->", "\t")
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

    public void TagFull(string tag)
    {
        WriteLine($"<{tag}/>");
    }

    public void TagOpen(string tag)
    {
        Open($"<{tag}>");
    }

    public void TagClose(string tag)
    {
        Close($"</{tag}>");
    }

    public void TagStart(string tag)
    {
        WriteStart($"<{tag}");
    }

    public void TagEnd()
    {
        WriteEnd(">");
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
}
