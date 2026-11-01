using System.Text;

namespace Bruinet.Generator.Writers;

public abstract class CodeWriter
{
    protected string BlockOpen { get; }
    protected string BlockClose { get; }

    public string CommentOpen { get; }
    public string CommentClose { get; }
    public string SpaceString { get; }

    protected CodeWriter(string blockOpen, string blockClose, string commentOpen, string commentClose)
    {
        BlockOpen = blockOpen;
        BlockClose = blockClose;
        CommentOpen = commentOpen;
        CommentClose = commentClose;
        SpaceString = "   ";
    }

    protected CodeWriter(string blockOpen, string blockClose, string commentOpen, string commentClose, string spaceString)
    {
        BlockOpen = blockOpen;
        BlockClose = blockClose;
        CommentOpen = commentOpen;
        CommentClose = commentClose;
        SpaceString = spaceString;
    }

    private readonly StringBuilder _sb = new StringBuilder();
    public int Spaces { get; set; }

    public void IncreaseSpace(int spaces)
    {
        Spaces += spaces;
    }
    public void DecreaseSpace(int spaces)
    {
        Spaces -= spaces;
        if (Spaces < 0)
        {
            Spaces = 0;
        }
    }

    public void Write(string? text)
    {
        if (text != null)
        {
            _sb.Append(text);
        }
    }

    public void WriteStart(string? text)
    {
        if (text != null)
        {
            LineStart();
            _sb.Append(text);
        }
    }

    public void WriteEnd()
    {
        _sb.Append('\n');
    }

    public void WriteEnd(string? text)
    {
        if (text != null)
        {
            _sb.Append(text);
        }
        _sb.Append('\n');
    }

    public void WriteLine(string? text)
    {
        if (text != null)
        {
            LineStart();
            _sb.Append(text);
            _sb.Append('\n');
        }
    }

    public void WriteLines(string? text)
    {
        if (text != null)
        {
            string[] lines = text.Split('\n');
            foreach (string line in lines)
            {
                WriteLine(line);
            }
        }
    }

    public void WriteException(Exception ex)
    {
        _sb.Append(ex.Message);
        _sb.Append('\n');
    }

    public void WriteLine()
    {
        _sb.Append('\n');
    }

    public virtual void Empty()
    {
        _sb.Append('\n');
    }

    public void Open()
    {
        Open(BlockOpen);
    }

    public void Open(string open)
    {
        if (!string.IsNullOrEmpty(open))
        {
            LineStart();
            _sb.Append(open);
            _sb.Append('\n');
        }
        Spaces++;
    }

    public void LineStart()
    {
        for (int i = 0; i < Spaces; i++)
        {
            _sb.Append(SpaceString);
        }
    }

    public void Close()
    {
        Close(BlockClose);
    }

    public void Close(string close)
    {
        if (Spaces > 0)
            Spaces--;
        if (!string.IsNullOrEmpty(close))
        {
            LineStart();
            _sb.Append(close);
            _sb.Append('\n');
        }
    }

    public override string ToString()
    {
        return _sb.ToString();
    }

    public void Generated()
    {
        Comment("Generated");
    }

    public void Comment(string comment)
    {
        WriteLine($"{CommentOpen} {comment} {CommentClose}");
    }

    public void OpenComment(string? comment)
    {
        if (comment == null)
        {
            return;
        }
        WriteLine($"{CommentOpen} {comment} ");
    }

    public void CloseComment(string? comment)
    {
        if (comment == null)
        {
            return;
        }
        WriteLine($"{CommentClose}");
    }

}
