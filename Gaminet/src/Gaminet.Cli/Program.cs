// gaminet: the migrator. It has no commands yet; the first comes with the first step that needs one.
// gaminet, gaminet --help: what it is, exit 0. Anything else: not a command, exit 2.
return args switch
{
    [] or ["--help" or "-h" or "help"] => Help(Console.Out, 0),
    _ => Unknown(args[0]),
};

static int Help(TextWriter to, int exit)
{
    to.WriteLine("gaminet: converts a product to another architecture, a unit at a time, with evidence.");
    to.WriteLine();
    to.WriteLine("Usage: gaminet --help");
    to.WriteLine();
    to.WriteLine("No commands yet. Planned: the inventory, the transform rules and the ledger of a migration.");
    return exit;
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"gaminet: '{command}' is not a command.");
    return Help(Console.Error, 2);
}
