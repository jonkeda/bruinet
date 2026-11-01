namespace Bruinet.Generator.Writers;

public class TemplateWriter
{
    private string template = string.Empty;

    public TemplateWriter()
    {
    }

    public TemplateWriter(Type aClass, string name)
    {
        try
        {
            var assembly = aClass.Assembly;
            using (var stream = assembly.GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream ?? throw new InvalidOperationException(), System.Text.Encoding.UTF8))
            {
                template = reader.ReadToEnd();
            }
        }
        catch (Exception)
        {
            // Handle the exception as needed.
        }
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
