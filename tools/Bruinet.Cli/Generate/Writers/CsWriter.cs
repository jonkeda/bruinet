namespace Bruinet.Generator.Writers;

public class CsWriter : CodeWriter
{
    public UsingDictionary Usings { get; } = new UsingDictionary();

    public override string ToString()
    {
        if (Usings.Count == 0)
            return base.ToString();
        return $"{Usings}\n{base.ToString()}";
    }

    public CsWriter() : base("{", "}", "/*", "*/")
    {
    }

    public CsWriter(int spaces) : this()
    {
        Spaces = spaces;
    }

    public void AddUsing(string usingText)
    {
        Usings.Add(usingText);
    }
}
