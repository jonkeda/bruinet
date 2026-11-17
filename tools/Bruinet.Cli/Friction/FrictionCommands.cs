using System.CommandLine;

namespace Bruinet.Cli.Friction;

/// <summary><c>bruinet friction</c>: a suite's friction log, a row at a time.</summary>
internal static class FrictionCommands
{
    public static Command Build(CliIo io) => new("friction", "A suite's friction log (.testing/400-FrictionLog.md): add a row for what a stage had to work around.")
    {
        Add(io),
    };

    private static Command Add(CliIo io)
    {
        Option<string> Text(string name, string description, bool required) => new(name) { Description = description, Required = required };
        var stage = Text("--stage", "The loop's stage and the journey, such as \"3 Spec (B7.HRS.02)\".", true);
        var what = Text("--what", "What happened: the symptom as it was met, with the message or the element.", true);
        var cost = Text("--cost", "What it cost: minutes, runs, probes. Rough is fine; none is not.", true);
        var workaround = Text("--workaround", "What the stage did to go on. Default: None.", false);
        var change = Text("--change", "What would have avoided it; start with \"Done:\" when it is done, \"Open:\" when not.", true);
        var where = Text("--where", "Where the change goes: Bruinet, a control library, the suite, or the app's owner.", true);
        var log = new Option<string?>("--log") { Description = $"The friction log. Default: .testing/{FrictionLog.FileName} of the suite this folder is in." };
        var command = new Command("add", "Add a row to the friction log, numbered after its last row and dated today. Prints the row's number.")
        {
            stage, what, cost, workaround, change, where, log,
        };
        command.Action(io, parsed =>
        {
            var path = parsed.GetValue(log) is { Length: > 0 } given
                ? Path.GetFullPath(given, io.CurrentDirectory)
                : FrictionLog.Find(io.CurrentDirectory) ?? throw new ArgumentException($"No .testing/{FrictionLog.FileName} in this folder or above it: give --log.");
            if (!File.Exists(path))
                throw new ArgumentException($"The friction log {path} does not exist.");
            var (markdown, number) = FrictionLog.Add(File.ReadAllText(path), DateOnly.FromDateTime(DateTime.Now), parsed.GetValue(stage)!, parsed.GetValue(what)!, parsed.GetValue(cost)!, parsed.GetValue(workaround) ?? string.Empty, parsed.GetValue(change)!, parsed.GetValue(where)!);
            File.WriteAllText(path, markdown);
            io.Out.WriteLine(number);
            io.Error.WriteLine($"Row {number} added to {path}");
            return 0;
        });
        return command;
    }
}
