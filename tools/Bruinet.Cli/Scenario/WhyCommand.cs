using System.CommandLine;
using System.Text.Json.Nodes;
using Bruinet.Bdd.LanguageServer;
using static Bruinet.Cli.CommonOptions;

namespace Bruinet.Cli.Scenario;

/// <summary>
/// <c>bruinet scenario why &lt;file&gt;:&lt;line&gt;</c> (AI9): where a scenario's newest failing run first parted from its last green
/// run, and the files changed since that touch the step; the MCP server's <c>diff_since_green</c> as text. Nothing to compare
/// (no failure, or no green run before it) is an answer, exit 0 (D3).
/// </summary>
internal static class WhyCommand
{
    public static Command Build(CliIo io)
    {
        var at = new Argument<string>("file:line") { Description = "The feature file and its scenario heading's line: Features/delete.feature.md:15." };
        var suite = new Option<string?>("--suite") { Description = "The suite a linked file runs in; its own when left out." };
        var (solution, json) = (Solution(), Json());
        var command = new Command("why", "Where a scenario's newest failure first parted from its last green run: the step, the kind of change, and the files changed since that touch it.") { at, suite, solution, json };
        command.Action(io, async (parsed, _) =>
        {
            var text = parsed.GetValue(at)!;
            if (Place(text, io.CurrentDirectory) is not (var path, int line))
            {
                throw new ArgumentException($"'{text}': give the feature file and the scenario's line, file:line.");
            }

            using var server = new BddMcpServer(SolutionFolder(parsed.GetValue(solution), Path.GetDirectoryName(path)!));
            var arguments = new JsonObject { ["path"] = path, ["line"] = line };
            if (parsed.GetValue(suite) is { } name)
            {
                arguments["suite"] = name;
            }

            var result = server.Handle(new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "tools/call",
                ["params"] = new JsonObject { ["name"] = "diff_since_green", ["arguments"] = arguments },
            }.ToJsonString())!["result"]!;
            var answer = (string)result["content"]![0]!["text"]!;
            if ((bool?)result["isError"] == true)
            {
                await io.Error.WriteLineAsync(answer).ConfigureAwait(false);
                return 2;
            }

            await io.Out.WriteLineAsync(parsed.GetValue(json) ? answer : Text(JsonNode.Parse(answer)!)).ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    // The answer as a reader takes it in: the step and kind first, then what differed, then the files.
    private static string Text(JsonNode why)
    {
        if ((bool?)why["compared"] == false)
        {
            return $"{(string?)why["scenario"]}: {(string?)why["note"]}";
        }

        var lines = new List<string>
        {
            $"{(string?)why["scenario"]}: {(string?)why["kind"]}" + (why["step"] is { } step ? $" at line {(int?)step["line"]}, {(string?)step["text"]}" : string.Empty),
            $"  green {(string?)why["green"]?["at"]} ({(string?)why["green"]?["commit"] ?? "no commit"}), failing {(string?)why["failing"]?["at"]} ({(string?)why["failing"]?["commit"] ?? "no commit"})",
        };
        lines.AddRange(why["differences"]?.AsArray().Select(item => $"  - {(string?)item}") ?? []);
        lines.AddRange(why["slower"]?.AsArray().Select(item => $"  slower: {(string?)item}") ?? []);
        lines.AddRange(why["files"]?["touching"]?.AsArray().Select(item => $"  touches the step: {(string?)item}") ?? []);
        lines.AddRange(why["files"]?["earlier"]?.AsArray().Select(item => $"  touches an earlier step: {(string?)item}") ?? []);
        if ((int?)why["files"]?["other"] is > 0 and var other)
        {
            lines.Add($"  {other} other files changed");
        }

        if ((string?)why["note"] is { Length: > 0 } note)
        {
            lines.Add($"  {note}");
        }
        return string.Join(Environment.NewLine, lines);
    }
}
