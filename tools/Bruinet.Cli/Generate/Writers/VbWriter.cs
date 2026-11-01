namespace Bruinet.Generator.Writers;

public class VbWriter : CodeWriter
{
    public VbImportsDictionary Imports { get; } = new VbImportsDictionary();

    public override string ToString()
    {
        if (Imports.Count == 0)
            return base.ToString();
        return $"{Imports}\n{base.ToString()}";
    }

    public VbWriter() : base("", "", "", "")
    {
    }

    public VbWriter(int spaces) : this()
    {
        Spaces = spaces;
    }

    public void AddImport(string usingText)
    {
        Imports.Add(usingText);
    }
}
