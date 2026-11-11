namespace Bruinet.Generator.Writers;

public class TemplateWriter
{
    private string template = string.Empty;

    public TemplateWriter()
    {
    }

    public TemplateWriter(Type aClass, string name)
    {
        // A missing template is a fault in the build of the tool, not an empty file to write (review 05, finding 14).
        var assembly = aClass.Assembly;
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"No embedded template '{name}' in {assembly.GetName().Name}.");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        template = reader.ReadToEnd();
    }

    public void Replace(string variable, string value)
    {
        if (value == null)
            return;
        template = template.Replace("#" + variable + "#", value);
    }

    public override string ToString()
    {
        return template;
    }
}
