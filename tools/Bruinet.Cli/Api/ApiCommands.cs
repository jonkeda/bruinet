using System.CommandLine;
using Bruinet.Api.OpenApi;
using Bruinet.Cli.Sql;
using Bruinet.Core.Api;

namespace Bruinet.Cli.Api;

/// <summary>
/// <c>bruinet api</c>: a description of an API from the calls Bruinet saw, what changed between two descriptions, the
/// operations no run called, and a run's answers held against a description.
/// </summary>
internal static class ApiCommands
{
    public static Command Build(CliIo io) => new("api", "An API's description: merged from the calls a run made, compared with an earlier one, and held against what the service answered.")
    {
        DescribeCommand(io),
        DiffCommand(io),
        UncalledCommand(io),
        CheckCommand(io),
    };

    private static Option<string[]> Exchanges() => new("--exchanges")
    {
        Description = "A file of exchanges (one JSON object per line, as BRUINET_API_EXCHANGES writes), or a folder of them (*.jsonl). May be given more than once.",
        Required = true,
        AllowMultipleArgumentsPerToken = true,
    };

    private static Option<string> Description() => new("--description") { Description = "The description: an OpenAPI 3 document in JSON.", Required = true };

    private static Command DescribeCommand(CliIo io)
    {
        var exchanges = Exchanges();
        var output = new Option<string>("--out") { Description = "The description to write (OpenAPI 3, JSON).", Required = true };
        var title = new Option<string>("--title") { Description = "The API's name.", DefaultValueFactory = _ => "API" };
        var hosts = new Option<string[]>("--host") { Description = "Only the calls to this host; may be given more than once. Every host when left out.", AllowMultipleArgumentsPerToken = true };
        var command = new Command("describe", "Write the description of an API as the given exchanges show it: each address with its parameters, the shape of each body, how many calls stand behind it. Shapes only, no values.") { exchanges, output, title, hosts };
        command.Action(io, parsed =>
        {
            var read = Read(io, parsed.GetValue(exchanges)!);
            var description = ObservedDescription.Merge(read, parsed.GetValue(title)!, parsed.GetValue(hosts));
            var path = Path.GetFullPath(parsed.GetValue(output)!, io.CurrentDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, description.ToJson());
            io.Error.WriteLine($"{description.Operations.Count} operations from {description.Operations.Sum(operation => operation.Observed)} of {read.Count} exchanges: wrote {path}");
            return 0;
        });
        return command;
    }

    private static Command DiffCommand(CliIo io)
    {
        var first = new Argument<string>("description") { Description = "The description now; with a second one, the earlier of the two." };
        var second = new Argument<string?>("later") { Description = "The later description. When left out, the first is compared with its own text at --base.", Arity = ArgumentArity.ZeroOrOne };
        var since = new Option<string>("--base") { Description = "The git revision to compare a single description with.", DefaultValueFactory = _ => "HEAD" };
        var command = new Command("diff", "What two descriptions of an API differ in: operations added and gone, statuses, and the fields of each body. Exit 1 when they differ.") { first, second, since };
        command.Action(io, parsed =>
        {
            var one = Path.GetFullPath(parsed.GetValue(first)!, io.CurrentDirectory);
            ApiDescription before;
            ApiDescription now;
            if (parsed.GetValue(second) is { } later)
            {
                before = ApiDescription.Read(File.ReadAllText(one));
                now = ApiDescription.Read(File.ReadAllText(Path.GetFullPath(later, io.CurrentDirectory)));
            }
            else
            {
                var folder = Path.GetDirectoryName(one)!;
                var root = SqlCommands.GitText(folder, "rev-parse", "--show-toplevel").Trim();
                before = ApiDescription.Read(SqlCommands.GitText(folder, "show", $"{parsed.GetValue(since)}:{Path.GetRelativePath(root, one).Replace('\\', '/')}", "--"));
                now = ApiDescription.Read(File.ReadAllText(one));
            }

            var lines = DescriptionDiff.Compare(before, now);
            foreach (var line in lines)
            {
                io.Out.WriteLine(line);
            }

            io.Error.WriteLine(lines.Count == 0 ? "the descriptions say the same" : $"{lines.Count} differences");
            return lines.Count == 0 ? 0 : 1;
        });
        return command;
    }

    private static Command UncalledCommand(CliIo io)
    {
        var description = Description();
        var exchanges = Exchanges();
        var command = new Command("uncalled", "The operations of a description that none of the given exchanges called: what no test and no walk reaches. Exit 1 when any.") { description, exchanges };
        command.Action(io, parsed =>
        {
            var read = ApiDescription.Read(File.ReadAllText(Path.GetFullPath(parsed.GetValue(description)!, io.CurrentDirectory)));
            var uncalled = DescriptionCheck.Uncalled(read, Read(io, parsed.GetValue(exchanges)!));
            foreach (var operation in uncalled)
            {
                io.Out.WriteLine(operation.Name);
            }

            io.Error.WriteLine($"{uncalled.Count} of {read.Operations.Count} operations not called");
            return uncalled.Count == 0 ? 0 : 1;
        });
        return command;
    }

    private static Command CheckCommand(CliIo io)
    {
        var description = Description();
        var exchanges = Exchanges();
        var command = new Command("check", "Each answer of the given exchanges against the schema the description has for it: a value of another type, a missing field, a field the description does not have. Exit 1 when an answer does not fit.") { description, exchanges };
        command.Action(io, parsed =>
        {
            var read = ApiDescription.Read(File.ReadAllText(Path.GetFullPath(parsed.GetValue(description)!, io.CurrentDirectory)));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var (checkedCalls, failed) = (0, 0);
            foreach (var exchange in Read(io, parsed.GetValue(exchanges)!))
            {
                var result = DescriptionCheck.Check(read, exchange);
                checkedCalls += result.ItemCount;
                failed += result.Failures.Any() ? 1 : 0;
                foreach (var issue in result.Issues)
                {
                    // One line per kind of finding, however many calls show it.
                    var line = $"{issue.Severity}: {(read.Find(exchange.Method, exchange.Path)?.Name ?? result.Endpoint)} {exchange.StatusCode} {issue.Path}: {issue.Message}";
                    if (seen.Add(line))
                    {
                        io.Out.WriteLine(line);
                    }
                }
            }

            io.Error.WriteLine($"{checkedCalls} answers checked, {failed} do not fit the description");
            return failed == 0 ? 0 : 1;
        });
        return command;
    }

    private static List<ApiExchange> Read(CliIo io, string[] paths)
    {
        var exchanges = new List<ApiExchange>();
        foreach (var given in paths)
        {
            var path = Path.GetFullPath(given, io.CurrentDirectory);
            IEnumerable<string> files = Directory.Exists(path) ? Directory.GetFiles(path, "*.jsonl", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
                : File.Exists(path) ? [path]
                : throw new FileNotFoundException($"There is no file or folder of exchanges at {path}.");
            foreach (var file in files)
            {
                exchanges.AddRange(ApiExchangeLog.Read(file));
            }
        }

        return exchanges;
    }
}
