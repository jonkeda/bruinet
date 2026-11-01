namespace Bruinet.Generator.Writers;

public class YamlWriter : CodeWriter
{
    public YamlWriter() : base("", "", "#", "")
    {
    }

    public void Add(string name, string value)
    {
        if (value == null)
            return;
        LineStart();

        Write(name);

        Write(": ");

        Write("\"");
        Write(value);
        WriteEnd("\"");
    }

    public YamlWriter(int spaces) : this()
    {
        Spaces = spaces;
    }

    public new void Open(string open)
    {
        LineStart();
        Write(open);
        WriteEnd(":");
        Spaces++;
    }

    public new void Close(string close)
    {
        if (Spaces > 0)
            Spaces--;
    }
}
