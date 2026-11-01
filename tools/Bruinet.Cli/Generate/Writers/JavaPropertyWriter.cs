namespace Bruinet.Generator.Writers;

public class JavaPropertyWriter : CodeWriter
{
    public JavaPropertyWriter() : base("", "", "#", "")
    {
    }

    public void Add(string name, string value)
    {
        if (value == null)
        {
            return;
        }
        LineStart();

        Write(name);

        Write("=");

        WriteEnd(value);
    }

    public JavaPropertyWriter(int spaces) : this()
    {
        Spaces = spaces;
    }
}
