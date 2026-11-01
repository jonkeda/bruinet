using System.Collections;
using System.Text;
using System.Globalization;

namespace Bruinet.Generator.Writers;

public class MarkdownWriter
{
    private readonly StringBuilder _sb = new StringBuilder();

    public void Clear()
    {
        _sb.Clear();
    }

    public void Title(string? text)
    {
        Title(text, 1);
    }

    public void Title(string? text, int level)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        for (int i = 0; i < level; i++)
        {
            _sb.Append("#");
        }

        _sb.Append(" ");
        _sb.AppendLine(text);
    }

    public void Subtitle(string? text)
    {
        Title(text, 2);
    }

    public void CSharp(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _sb.AppendLine("```csharp");
        _sb.AppendLine(text);
        _sb.AppendLine("```");
        _sb.AppendLine();
    }

    public void Line(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _sb.Append(text);
        _sb.AppendLine();
    }

    public void Paragraph(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _sb.Append(text);
        _sb.AppendLine("  ");
    }

    public void Numbered(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _sb.Append("1. ");
        _sb.Append(text);
        _sb.AppendLine("  ");
    }

    public void Items(params IEnumerable<string?>? texts)
    {
        if (texts == null)
        {
            return;
        }
        foreach (var text in texts)
        {
            Item(text);
        }
        TableHeader();
    }

    public void Item(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _sb.Append(CultureInfo.InvariantCulture, $"- {text}");
        _sb.AppendLine("  ");
    }

    public void Item(string? text, DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _sb.Append(CultureInfo.InvariantCulture, $"- {text}");
        _sb.AppendLine("  ");
    }

    public override string ToString()
    {
        return _sb.ToString();
    }

    public void Row(params IEnumerable<string?>? texts)
    {
        if (texts == null)
        {
            return;
        }
        foreach (var text in texts)
        {
            Cell(text);
        }
        RowEnd();
    }

    public void Cell(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _sb.Append(CultureInfo.InvariantCulture, $"| {text} ");
    }

    public void RowEnd()
    {
        _sb.Append('|');
        _sb.AppendLine();
    }

    public void TableHeader(params IEnumerable<string?>? texts)
    {
        if (texts == null)
        {
            return;
        }
        int i = 0;
        foreach (var text in texts)
        {
            i++;
            Cell(text);
        }
        RowEnd();
        for (int j = 0; j < i; j++)
        {
            Cell("  ");

        }
        RowEnd();
    }

    public void Table(IEnumerable rows)
    {
        var first = true;
        Type rowType = null!;
        foreach (var row in rows)
        {
            if (first)
            {
                rowType = row.GetType();
                foreach (var info in rowType.GetProperties())
                {
                    Cell(info.Name);
                }
                RowEnd();
                rowType = row.GetType();
                foreach (var info in rowType.GetProperties())
                {
                    Cell("--");
                }
                RowEnd();
                first = false;
            }
            foreach (var info in rowType.GetProperties())
            {
                Cell(info.GetValue(row)?.ToString());
            }
            RowEnd();
        }
    }

    public void Write(IMarkdownWriter? mdWriter)
    {
        mdWriter?.WriteMarkdown(this);
    }

    public void Item(string name, string? value)
    {
        if (value == null)
        {
            return;
        }
        _sb.Append(CultureInfo.InvariantCulture, $"**{name}** : ");
        _sb.Append(value);
        _sb.AppendLine("  ");
    }

    public void Item(string name, params IEnumerable<string>? values)
    {
        if (values == null
            || !values.Any())
        {
            return;
        }
        Item(name, string.Join(",", values));
    }

    public void Item(string name, double? value)
    {
        Item(name, value?.ToString(CultureInfo.InvariantCulture));
    }

    public void Item(string name, int? value)
    {
        Item(name, value?.ToString(CultureInfo.InvariantCulture));
    }

    public void Item(string name, bool? value)
    {
        Item(name, value?.ToString());
    }

    public void Item(string name, DateTime? value)
    {
        Item(name, value?.ToShortDateString());
    }

    public void Item(string name, Enum? value)
    {
        Item(name, value?.ToString());
    }

    public void List(string title, int level, IEnumerable<IMarkdownWriter>? items)
    {
        if (items == null)
        {
            return;
        }
        var markdownWriters = items.ToList();
        if (!markdownWriters.Any())
        {
            return;
        }
        Title(title, level);
        foreach (var item in markdownWriters)
        {
            Write(item);
        }
    }

    public void List(IEnumerable<IMarkdownWriter>? items)
    {
        if (items == null)
        {
            return;
        }
        foreach (var item in items)
        {
            Write(item);
        }
    }

}
