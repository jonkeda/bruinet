namespace Bruinet.Generator.Writers;

public class JavaWriter : CodeWriter
{
    public string? Package { get; set; }
    public ImportDictionary Imports { get; } = new ImportDictionary();

    public override string ToString()
    {
        var packageName = Package ?? string.Empty;
        if (string.IsNullOrEmpty(Package))
            return $"{Imports.ToString(packageName)}\n{base.ToString()}";
        return $"using {Package};\n\n{Imports.ToString(packageName)}\n{base.ToString()}";
    }

    public JavaWriter() : base("{", "}", "/*", "*/")
    {
    }

    public JavaWriter(int spaces) : this()
    {
        Spaces = spaces;
    }

    public void AddImport(string importText)
    {
        Imports.Add(importText);
    }
}
