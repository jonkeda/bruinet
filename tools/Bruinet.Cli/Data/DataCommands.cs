using System.CommandLine;
using Bruinet.DataValidator;

namespace Bruinet.Cli.Data;

/// <summary>
/// <c>bruinet data</c>: an app's test data and scenario files (docs/guides/test-data.md), from a UAT project
/// folder with a uat.config.md, or with --assembly. Exit 0 fine, 1 problems found, 2 could not run.
/// </summary>
internal static class DataCommands
{
    public static Command Build(CliIo io) => new("data", "Check an app's test data and scenario files (docs/guides/test-data.md).")
    {
        Make(io, "validate", "Check every test set and scenario, or one file.", Argument("file", "A test set or scenario file. Default: all of them."), fix: true, json: true),
        Make(io, "phrases", "The phrases steps can use, with argument types; with a scenario file, only those it sees.", Argument("file", "A scenario file."), keyword: true, json: true),
        Make(io, "references", "The config and files that use a namespace.", Argument("namespace", "The namespace.", required: true), json: true),
        Make(io, "snippet", "A method for a step that binds to nothing.", Argument("step", "file:line of the step, e.g. Features/TOD.01.feature.md:14.", required: true)),
        Make(io, "describe", "The fields of a test-data type.", Argument("type", "The type, e.g. TodoDto.", required: true), json: true),
        Make(io, "list", "The test sets and what uses them.", json: true),
        Make(io, "schema", "Write (or check) testdata.schema.json.", check: true),
    };

    private static Argument<string?> Argument(string name, string description, bool required = false) =>
        new(name) { Description = description, Arity = required ? ArgumentArity.ExactlyOne : ArgumentArity.ZeroOrOne };

    private static Command Make(CliIo io, string name, string description, Argument<string?>? argument = null, bool fix = false, bool keyword = false, bool json = false, bool check = false)
    {
        var assembly = new Option<string[]>("--assembly") { Description = "The assembly with the TestDataDefinition (repeatable).", AllowMultipleArgumentsPerToken = true };
        var config = new Option<string?>("--config") { Description = "The config to read. Default: ./uat.config.md or ./bdd.config.md." };
        var command = new Command(name, description) { assembly, config };
        if (argument is not null)
            command.Add(argument);
        var jsonOption = json ? CommonOptions.Json() : null;
        var fixOption = fix ? new Option<bool>("--fix") { Description = "Rewrite values that have one right answer." } : null;
        var keywordOption = keyword ? new Option<string?>("--keyword") { Description = "Only phrases for Given, When or Then." } : null;
        var checkOption = check ? new Option<bool>("--check") { Description = "Only check that the schema is up to date." } : null;
        foreach (var option in new Option?[] { jsonOption, fixOption, keywordOption, checkOption })
        {
            if (option is not null)
                command.Add(option);
        }

        command.Action(io, parsed => DataValidatorApp.Run(
            ToolOptions.Create(
                name,
                argument is null ? null : parsed.GetValue(argument),
                parsed.GetValue(assembly) ?? [],
                parsed.GetValue(config),
                jsonOption is not null && parsed.GetValue(jsonOption),
                checkOption is not null && parsed.GetValue(checkOption),
                fixOption is not null && parsed.GetValue(fixOption),
                keywordOption is null ? null : parsed.GetValue(keywordOption),
                io.CurrentDirectory),
            io.Out,
            io.Error));
        return command;
    }
}
