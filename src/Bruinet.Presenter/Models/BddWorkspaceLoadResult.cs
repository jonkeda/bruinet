namespace Bruinet.Presenter.Models;

public sealed record BddWorkspaceLoadResult(
    string FolderPath,
    string WorkspaceName,
    BddWorkspaceConfigLoadResult Config,
    IReadOnlyList<BddFileLoadResult> Files,
    IReadOnlyList<BddScenarioLoadResult> Scenarios,
    IReadOnlyList<string> Diagnostics,
    string DiscoveryReport,
    string CommandCatalogReport,
    BddCatalogSet? Catalogs = null)
{
    public int ErrorCount => Diagnostics.Count(line => line.Contains("error", StringComparison.OrdinalIgnoreCase));
}

public sealed record BddWorkspaceConfigLoadResult(
    bool ConfigExists,
    string ConfigPath,
    string Target,
    string Fixture,
    string AppPath,
    string ResolvedAppPath,
    bool AppPathExists,
    string WorkingDirectory,
    string ResolvedWorkingDirectory,
    bool WorkingDirectoryExists,
    IReadOnlyList<BddAssemblyLoadResult> Assemblies,
    IReadOnlyList<string> Diagnostics,
    BddProjectResolution? Project = null,
    IReadOnlyList<BddConfigProblem>? Problems = null)
{
    /// <summary>The diagnostics, each attached to the field that caused it.</summary>
    /// <remarks>
    /// <see cref="Diagnostics" /> is a projection of these, so the Diagnostics tab and the
    /// editor's per-field lines never disagree.
    /// </remarks>
    public IReadOnlyList<BddConfigProblem> FieldProblems => Problems ?? [];

    /// <summary>The problems attached to one field.</summary>
    /// <param name="section">The section, such as <c>Runtime</c>.</param>
    /// <param name="field">The field name.</param>
    /// <returns>Its problems, in order.</returns>
    public IReadOnlyList<BddConfigProblem> ProblemsFor(string section, string field) =>
        [.. FieldProblems.Where(problem =>
            problem.Section.Equals(section, StringComparison.OrdinalIgnoreCase) &&
            problem.Field.Equals(field, StringComparison.OrdinalIgnoreCase))];

    public bool HasErrors => Diagnostics.Any(line => line.StartsWith("Error:", StringComparison.OrdinalIgnoreCase));

    public string Summary
    {
        get
        {
            if (!ConfigExists)
            {
                return "Config missing";
            }

            var target = string.IsNullOrWhiteSpace(Target) ? "Target missing" : Target;
            var fixture = string.IsNullOrWhiteSpace(Fixture) ? "Fixture missing" : Fixture;

            // No AppPath is the normal case, not a fault: the fixture resolves the app.
            var app = string.IsNullOrWhiteSpace(AppPath)
                ? "App from fixture"
                : AppPathExists ? "App ok" : "App missing";
            return $"{target}  {fixture}  {app}";
        }
    }
}

public sealed record BddAssemblyLoadResult(
    string Kind,
    string Assembly,
    string ResolvedPath,
    bool Exists);

/// <summary>One diagnostic, attached to the config field that caused it.</summary>
/// <param name="Section">The section, such as <c>Runtime</c>, or <c>Assemblies</c>.</param>
/// <param name="Field">The field or row the problem belongs to.</param>
/// <param name="Message">The message, without the <c>Error:</c> prefix.</param>
/// <param name="IsError">Whether this blocks a run.</param>
public sealed record BddConfigProblem(string Section, string Field, string Message, bool IsError = true)
{
    /// <summary>The line as the Diagnostics tab prints it.</summary>
    public string Line => IsError ? $"Error: {Message}" : Message;
}

/// <summary>Sections a <see cref="BddConfigProblem" /> can belong to.</summary>
public static class BddConfigSections
{
    /// <summary>The <c>Runtime</c> table.</summary>
    public const string Runtime = "Runtime";

    /// <summary>The <c>Assemblies</c> table.</summary>
    public const string Assemblies = "Assemblies";

    /// <summary>The config file itself, rather than one of its fields.</summary>
    public const string File = "File";
}

/// <summary>What the <c>Runtime Project</c> row resolved to.</summary>
/// <param name="Configured">The row's value, as written.</param>
/// <param name="ResolvedPath">The project file it points at.</param>
/// <param name="PagesAssemblyPath">Where the build puts the Pages assembly, built or not.</param>
/// <param name="PagesAssemblyExists">Whether that assembly has been built.</param>
/// <param name="TargetFrameworks">Every framework the project declares, for the framework picker.</param>
public sealed record BddProjectResolution(
    string Configured,
    string ResolvedPath,
    string PagesAssemblyPath,
    bool PagesAssemblyExists,
    IReadOnlyList<string> TargetFrameworks)
{
    /// <summary>Whether the project resolved but its output has not been built yet.</summary>
    public bool NeedsBuild => PagesAssemblyPath.Length > 0 && !PagesAssemblyExists;
}

public sealed record BddFileLoadResult(
    string FilePath,
    string Name,
    bool ParseSucceeded,
    bool BindSucceeded,
    IReadOnlyList<string> Diagnostics);

/// <param name="Name">The scenario's name.</param>
/// <param name="SuiteName">The suite it belongs to.</param>
/// <param name="FilePath">The feature file it is in.</param>
/// <param name="Tags">Its tags.</param>
/// <param name="Steps">Its steps.</param>
/// <param name="LineNumber">The scenario's heading line in its file; 0 when not known.</param>
public sealed record BddScenarioLoadResult(
    string Name,
    string SuiteName,
    string FilePath,
    IReadOnlyList<string> Tags,
    IReadOnlyList<BddStepLoadResult> Steps,
    int LineNumber = 0);

/// <param name="Status">The step's status.</param>
/// <param name="Text">The step's text; a callable phrase's steps are indented under their call.</param>
/// <param name="CommandId">What it binds to.</param>
/// <param name="LineNumber">Its line in the scenario file; for a callable phrase's step, the call's.</param>
/// <param name="CallText">For a callable phrase's step, the call as written in the scenario; null otherwise.</param>
public sealed record BddStepLoadResult(
    string Status,
    string Text,
    string CommandId,
    int LineNumber,
    string? CallText = null);
