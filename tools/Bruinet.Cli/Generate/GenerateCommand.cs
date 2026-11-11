using System.CommandLine;
using Bruinet.Generator;
using Bruinet.Generator.Generators;
using Bruinet.Generator.Models;

namespace Bruinet.Cli.Generate;

/// <summary><c>bruinet controls generate</c>: each <c>.tpl.cs</c> template to its <c>.gen.cs</c>. Exit 0 all written, 1 a file failed, 2 bad input.</summary>
internal static class GenerateCommand
{
    private const string TemplateExtension = ".tpl.cs";
    private const string GeneratedExtension = ".gen.cs";

    public static Command Build(CliIo io)
    {
        var input = new Option<string>("--input", "-i") { Description = $"Files and folders, separated by ';'. Folders are searched for {TemplateExtension} files; each is written next to its input as <name>{GeneratedExtension}.", Required = true };
        var members = new Option<string>("--members") { Description = "Which members to generate.", DefaultValueFactory = _ => "all" };
        members.AcceptOnlyFromAmong("all", "actions", "state");
        var generate = new Command("generate", $"Generate ControlObject members: each {TemplateExtension} to its {GeneratedExtension}.") { input, members };
        generate.Action(io, parsed => Run(parsed.GetValue(input)!, parsed.GetValue(members)!, io.Out, io.Error));
        return new Command("controls", "ControlObjects.") { generate };
    }

    public static int Run(string input, string members, TextWriter output, TextWriter errors)
    {
        var inputFiles = new List<string>();
        foreach (var entry in input.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Directory.Exists(entry))
            {
                inputFiles.AddRange(Directory.EnumerateFiles(entry, "*" + TemplateExtension, SearchOption.AllDirectories).Where(IsTemplateFile));
            }
            else if (File.Exists(entry))
            {
                if (!IsTemplateFile(entry))
                {
                    errors.WriteLine($"Skipping (not a {TemplateExtension} file): {entry}");
                    continue;
                }

                inputFiles.Add(entry);
            }
            else
            {
                errors.WriteLine($"Error: Input not found: {entry}");
                return 2;
            }
        }

        inputFiles = inputFiles
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (inputFiles.Count == 0)
        {
            errors.WriteLine($"Error: No {TemplateExtension} files found for --input '{input}'");
            return 2;
        }

        var generator = BuildGenerator(members);
        var failed = 0;
        var warnings = 0;
        var written = 0;

        foreach (var inputFile in inputFiles)
        {
            var outputFile = GetOutputPath(inputFile);

            try
            {

                var options = new GeneratorOptions
                {
                    InputFilePath = inputFile,
                    OutputFilePath = outputFile,
                    IncludeGeneratedHeader = true,
                };

                // Progress and warnings on stderr: stdout is the answer (D3), the files written.
                if (generator.GenerateToFile(inputFile, outputFile, options))
                {
                    written++;
                    output.WriteLine(outputFile);
                }

                foreach (var warning in generator.Warnings)
                {
                    warnings++;
                    errors.WriteLine($"Warning: {inputFile}: {warning}");
                }
            }
            catch (InvalidOperationException ex)
            {
                // Validation failures (name collisions, no target class) are expected input
                // errors — report the message without a stack trace.
                failed++;
                errors.WriteLine($"Error processing {inputFile}: {ex.Message}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                failed++;
                errors.WriteLine($"Error processing {inputFile}: {ex.Message}");
            }
        }

        errors.WriteLine($"{written} of {inputFiles.Count} file(s) changed.");
        if (warnings > 0)
        {
            errors.WriteLine($"{warnings} warning(s). Generation continued; see the lines starting 'Warning:'.");
        }

        if (failed > 0)
        {
            errors.WriteLine($"{failed} of {inputFiles.Count} file(s) failed.");
            return 1;
        }

        return 0;
    }

    private static bool IsTemplateFile(string path)
        => path.EndsWith(TemplateExtension, StringComparison.OrdinalIgnoreCase);

    private static string GetOutputPath(string inputFile)
        => string.Concat(inputFile.AsSpan(0, inputFile.Length - TemplateExtension.Length), GeneratedExtension);

    private static ControlObjectGenerator BuildGenerator(string members)
    {
        if (members == "all")
            return ControlObjectGenerator.CreateDefault();

        var generator = new ControlObjectGenerator();
        if (members == "state")
            generator.Register(new IsWaitAssertGenerator());
        if (members == "actions")
            generator.Register(new ActionGenerator());
        return generator;
    }
}
