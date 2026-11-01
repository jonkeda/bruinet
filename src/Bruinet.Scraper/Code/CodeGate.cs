using System.Diagnostics;
using System.Text.RegularExpressions;
using Bruinet.Scraper.Projects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Bruinet.Scraper.Code;

/// <summary>
/// What generated code must pass: a syntax check before a file is written, then the project's own
/// build with its analyzers. What a model writes is trusted only once it builds.
/// </summary>
public static partial class CodeGate
{
    /// <summary>How long a build may take before it is ended.</summary>
    public static readonly TimeSpan BuildLimit = TimeSpan.FromMinutes(10);

    /// <summary>The most problems a build result lists.</summary>
    public const int MaxProblems = 50;

    /// <summary>
    /// Writes a C# file into the project when its syntax is valid. The path is relative to the project
    /// and must stay inside it.
    /// </summary>
    public static WriteResult Write(ScrapeProject project, string path, string code)
    {
        var full = Path.GetFullPath(Path.Combine(project.Folder, path));
        if (!full.StartsWith(project.Folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return WriteResult.Refused($"'{path}' is outside {project.Name}; give a path inside the project, such as {project.Config.PagesFolder}/InvoicePage.cs.");
        if (!full.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || full.EndsWith(".gen.cs", StringComparison.OrdinalIgnoreCase))
            return WriteResult.Refused($"'{path}' is not a C# source file the Scraper writes: a .cs file, not a generated .gen.cs.");
        var relative = Path.GetRelativePath(project.Folder, full);
        if (relative.Split(Path.DirectorySeparatorChar)[0] is "bin" or "obj" or ScrapeProject.RecordingsFolderName)
            return WriteResult.Refused($"'{path}' is in bin, obj or Recordings.");

        var problems = Syntax(code);
        if (problems.Count > 0)
            return new WriteResult(false, relative, false, problems, "The code has syntax errors; nothing was written.");

        var existed = File.Exists(full);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, code.Replace("\r\n", "\n", StringComparison.Ordinal));
        return new WriteResult(true, relative, existed, [], null);
    }

    /// <summary>The syntax errors in C# code, with their lines.</summary>
    public static IReadOnlyList<CodeProblem> Syntax(string code) =>
        CSharpSyntaxTree.ParseText(code).GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => new CodeProblem(null, d.Location.GetLineSpan().StartLinePosition.Line + 1, d.Location.GetLineSpan().StartLinePosition.Character + 1, "error", d.Id, d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)))
            .ToArray();

    /// <summary>A control template's extension: the generator writes its members to a <c>.gen.cs</c> beside it.</summary>
    public const string TemplateExtension = ".tpl.cs";

    /// <summary>Builds the project and gives its errors and warnings.</summary>
    public static Task<BuildResult> BuildAsync(ScrapeProject project, CancellationToken cancellationToken = default) =>
        BuildAsync(project, null, cancellationToken);

    /// <summary>
    /// Generates the project's control templates, then builds it and gives its errors and warnings. A
    /// template that does not generate fails the build before it runs.
    /// </summary>
    /// <param name="project">The scraped project.</param>
    /// <param name="templates">Writes each <c>.tpl.cs</c> to its <c>.gen.cs</c>; null to build only.</param>
    /// <param name="cancellationToken">Ends the build.</param>
    public static async Task<BuildResult> BuildAsync(ScrapeProject project, ITemplateGenerator? templates, CancellationToken cancellationToken = default)
    {
        var generated = Generate(project, templates);
        if (generated.Any(p => p.Severity == "error"))
            return new BuildResult(false, generated.Take(MaxProblems).ToArray(), null);

        var projectFile = Directory.EnumerateFiles(project.Folder, "*.csproj").FirstOrDefault()
            ?? throw new FileNotFoundException($"{project.Folder} has no project file to build.");
        var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "build", projectFile, "-v:quiet", "-nologo", "-clp:NoSummary", "/nr:false" })
            info.ArgumentList.Add(argument);

        var lines = new List<string>();
        using var process = Process.Start(info) ?? throw new InvalidOperationException("dotnet did not start.");
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (lines) lines.Add(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (lines) lines.Add(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(BuildLimit);
        try
        {
            await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"The build of {project.Name} took longer than {BuildLimit.TotalMinutes} minutes and was ended.", exception);
        }

        List<string> output;
        lock (lines) output = [.. lines];
        var problems = generated.Concat(output.Select(Parse).OfType<CodeProblem>().Distinct()).Take(MaxProblems).ToArray();
        var succeeded = process.ExitCode == 0;
        return new BuildResult(succeeded, problems, succeeded || problems.Length > 0 ? null : string.Join('\n', output.TakeLast(20)));
    }

    // The templates under the project's Controls and Pages folders, when it has any.
    private static IReadOnlyList<CodeProblem> Generate(ScrapeProject project, ITemplateGenerator? templates)
    {
        if (templates is null)
            return [];
        var problems = new List<CodeProblem>();
        foreach (var folder in new[] { project.Config.ControlsFolder, project.Config.PagesFolder }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var full = Path.Combine(project.Folder, folder);
            if (Directory.Exists(full) && Directory.EnumerateFiles(full, "*" + TemplateExtension, SearchOption.AllDirectories).Any())
                problems.AddRange(templates.Generate(full));
        }
        return problems;
    }

    // "C:\x\Pages\Foo.cs(12,5): error CS0246: The type ... [C:\x\X.csproj]"
    private static CodeProblem? Parse(string line)
    {
        var match = MsBuildLine().Match(line);
        return match.Success
            ? new CodeProblem(match.Groups["file"].Value, int.Parse(match.Groups["line"].Value, System.Globalization.CultureInfo.InvariantCulture), int.Parse(match.Groups["column"].Value, System.Globalization.CultureInfo.InvariantCulture), match.Groups["severity"].Value, match.Groups["code"].Value, match.Groups["message"].Value.Trim())
            : null;
    }

    [GeneratedRegex(@"^\s*(?<file>.+?)\((?<line>\d+),(?<column>\d+)\):\s*(?<severity>error|warning)\s+(?<code>\w+):\s*(?<message>.*?)(\s+\[[^\]]+\])?$")]
    private static partial Regex MsBuildLine();
}

/// <summary>A compiler or analyzer message.</summary>
public sealed record CodeProblem(string? File, int Line, int Column, string Severity, string Code, string Message);

/// <summary>What <see cref="CodeGate.Write"/> did.</summary>
public sealed record WriteResult(bool Written, string? Path, bool Replaced, IReadOnlyList<CodeProblem> Problems, string? Reason)
{
    /// <summary>Not written, for a reason.</summary>
    public static WriteResult Refused(string reason) => new(false, null, false, [], reason);
}

/// <summary>A build's outcome: its errors and warnings, or its last lines when it failed without any.</summary>
public sealed record BuildResult(bool Succeeded, IReadOnlyList<CodeProblem> Problems, string? Output);
