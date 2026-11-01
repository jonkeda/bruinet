namespace Bruinet.Generator.Writers;

public class MermaidWriter : CodeWriter
{
    public MermaidWriter() : base("", "", "---", "---")
    {
    }

    public void Start()
    {
        WriteLine("<html>");
        WriteLine("<head>");
        WriteLine(@"<meta charset=""UTF-8"">");
        WriteLine(@"<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">");
        WriteLine(@"<script src=""https://cdnjs.cloudflare.com/ajax/libs/mermaid/11.4.0/mermaid.min.js""></script>");

        WriteLine(@"<style>
					body {
                        overflow: auto !important;
                        width: max-content !important;
                    }
                    html {
                        overflow: auto !important;
                    }
					</style>
					");

        WriteLine("</head>");

        WriteLine("<body>");
    }

    public void End()
    {
        WriteLine(@"<script> mermaid.initialize({ startOnLoad: true });</script>");

        WriteLine("</body>");
        WriteLine("</html>");
    }

    public MermaidWriter(int spaces) : this()
    {
        Spaces = spaces;
    }

    public void StartDiagram()
    {
        WriteLine(@"<pre class=""mermaid"">");
    }

    public void EndDiagram()
    {
        WriteLine("</pre>");
    }
}
