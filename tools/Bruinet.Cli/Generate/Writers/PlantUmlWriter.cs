namespace Bruinet.Generator.Writers;

public class D2Writer : CodeWriter
{
    public string? Package { get; set; }
    public ImportDictionary Imports { get; set; } = new ImportDictionary();

    public D2Writer() : base("{", "}", "/*", "*/")
    {
    }

    public void Start()
    {
        WriteLine("@startuml");
        WriteLine();
        WriteLine("' hide the spot");
        WriteLine("hide circle");
        WriteLine();
        WriteLine("' either show or hide members");
        WriteLine("' hide members");
        WriteLine("' show members");
        WriteLine();
        WriteLine("' avoid problems with angled crows feet");
        WriteLine("skinparam linetype ortho");
        WriteLine();
    }

    public void End()
    {
        WriteLine("@enduml");
    }

    public D2Writer(int spaces) : this()
    {
        Spaces = spaces;
    }

    public void AddImport(string importText)
    {
        Imports.Add(importText);
    }
}
