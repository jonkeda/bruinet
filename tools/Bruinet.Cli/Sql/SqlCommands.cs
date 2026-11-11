using System.CommandLine;
using System.Diagnostics;
using Bruinet.Cli.Selection;
using Bruinet.Mocking.Data;
using Bruinet.Mocking.Data.Replay;
using Microsoft.Data.SqlClient;

namespace Bruinet.Cli.Sql;

/// <summary>
/// <c>bruinet sql</c>: the SQL log of a suite's run (.my/sql-replay/plan.md, step 1). The schema snapshot, an Extended
/// Events capture for an app left unchanged, and the report: the database's share of each test and how far each table,
/// view and procedure reaches. Read-only on the database, but for the capture's own session.
/// </summary>
internal static class SqlCommands
{
    /// <summary>The capture's state, between start and read, when no other file is named.</summary>
    public const string StateFile = "sql-capture.json";

    public static Command Build(CliIo io) => new("sql", "The SQL log: the schema snapshot, an Extended Events capture, and the report of what each test spent in the database.")
    {
        SnapshotCommand(io),
        StaleCommand(io),
        DiffCommand(io),
        CheckCommand(io),
        AcceptCommand(io),
        new Command("capture", "An Extended Events session on the test database: the app's commands, logged by SQL Server, the app unchanged.")
        {
            StartCommand(io),
            StopCommand(io),
            ReadCommand(io),
        },
        ReportCommand(io),
    };

    private static Command SnapshotCommand(CliIo io)
    {
        var connection = Connection();
        var connectionFrom = ConnectionFrom();
        var output = new Option<string>("--out") { Description = "The snapshot's file.", DefaultValueFactory = _ => "schema.json" };
        var references = new Option<string?>("--references-file") { Description = "A JSON file of references kept by hand, for dynamic SQL: { \"dbo.search\": [\"dbo.places\"] }." };
        var data = new Option<string[]>("--data") { Description = "A table of reference data whose rows are hashed with it (repeatable).", AllowMultipleArgumentsPerToken = true };
        var defaultSchema = new Option<string>("--default-schema") { Description = "The app's user's default schema: a command's unqualified name is taken in it, then dbo. SQLite's is always main.", DefaultValueFactory = _ => "dbo" };
        var provider = new Option<string>("--provider") { Description = "sqlserver, or sqlite for a SQLite file (\"Data Source=<file>\"): the connection string can't tell them apart.", DefaultValueFactory = _ => "sqlserver" };
        provider.AcceptOnlyFromAmong("sqlserver", "sqlite");
        var json = CommonOptions.Json();
        var command = new Command("snapshot", "Write the database's schema snapshot: tables, views, procedures and functions, their hashes and references.") { connection, connectionFrom, output, references, data, defaultSchema, provider, json };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            var sqlite = parsed.GetValue(provider) == "sqlite";
            await using System.Data.Common.DbConnection sql = sqlite
                ? new Microsoft.Data.Sqlite.SqliteConnection(ConnectionString(parsed, connection, connectionFrom))
                : new SqlConnection(ConnectionString(parsed, connection, connectionFrom));
            await sql.OpenAsync(cancellationToken);
            var options = new SnapshotOptions
            {
                Provider = sqlite ? SnapshotProvider.Sqlite : SnapshotProvider.SqlServer,
                Data = [.. parsed.GetValue(data) ?? []],
                DefaultSchema = parsed.GetValue(defaultSchema)!,
            };
            var snapshot = await SchemaSnapshot.ReadAsync(sql, options, cancellationToken);
            if (parsed.GetValue(references) is { } file)
            {
                foreach (var unknown in snapshot.MergeReferences(Path.GetFullPath(file, io.CurrentDirectory)))
                {
                    io.Error.WriteLine($"warning: {unknown} in {file} is not in the database");
                }
            }

            var to = Path.GetFullPath(parsed.GetValue(output)!, io.CurrentDirectory);
            snapshot.Save(to);
            var dynamic = snapshot.Objects.Where(pair => pair.Value.Dynamic && !pair.Value.Listed).Select(pair => pair.Key).ToList();
            io.Error.WriteLine($"{snapshot.Objects.Count} objects of {sql.Database}; wrote {to}");
            if (dynamic.Count > 0)
            {
                io.Error.WriteLine($"{dynamic.Count} with dynamic SQL and no references listed (--references-file): {string.Join(", ", dynamic)}");
            }

            if (parsed.GetValue(json))
            {
                io.Out.WriteLine(CommonOptions.ToJson(new { file = to, database = sql.Database, objects = snapshot.Objects.Count, dynamic }));
            }

            return 0;
        });
        return command;
    }

    private static Option<string> Recordings() => new("--recordings") { Description = "The folder of recordings (<head>/<test>.json).", Required = true };

    private static Command DiffCommand(CliIo io)
    {
        var recordings = Recordings();
        var since = new Option<string>("--base") { Description = "The git revision to compare with.", DefaultValueFactory = _ => "HEAD" };
        var command = new Command("diff", "What each re-recorded test changed against git: commands added and removed, rows, columns, values, errors, the stamp. bruinet sql accept marks them read.") { recordings, since };
        command.Action(io, (parsed, cancellationToken) =>
        {
            var folder = Path.GetFullPath(parsed.GetValue(recordings)!, io.CurrentDirectory);
            var revision = parsed.GetValue(since)!;
            var root = GitText(folder, "rev-parse", "--show-toplevel").Trim();
            var prefix = Path.GetRelativePath(root, folder).Replace('\\', '/').TrimEnd('/') + "/";
            var before = GitText(folder, "ls-tree", "-r", "--name-only", "--full-name", revision, "--", ":/" + prefix)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .Select(path => path[prefix.Length..])
                .ToList();
            // Tests that share an app read each other's rows: a value volatile in any recording of the suite is volatile in all.
            var olds = before.ToDictionary(path => path, path => GitText(folder, "show", $"{revision}:{prefix}{path}"), StringComparer.Ordinal);
            var volatileBefore = RecordingReview.VolatileValues(olds.Values.Where(RecordingReview.IsRecording).Select(json => Mocking.Data.Replay.Recording.Parse(json)!));
            var volatileAfter = RecordingReview.VolatileValues(folder);
            var changed = 0;
            foreach (var path in RecordingReview.Recordings(folder).Union(before, StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal))
            {
                var now = Path.Combine(folder, path);
                var nowJson = File.Exists(now) ? File.ReadAllText(now) : null;
                var oldJson = olds.TryGetValue(path, out var old) ? old : null;
                if (nowJson is not null && oldJson is not null && RecordingReview.IsRecording(nowJson) && RecordingReview.IsRecording(oldJson)
                    && RecordingReview.Hash(nowJson, volatileAfter) == RecordingReview.Hash(oldJson, volatileBefore))
                {
                    continue;
                }

                if (nowJson is not null && !RecordingReview.IsRecording(nowJson) || oldJson is not null && !RecordingReview.IsRecording(oldJson))
                {
                    continue;
                }

                var lines = RecordingDiff.Compare(oldJson is null ? null : Mocking.Data.Replay.Recording.Parse(oldJson), nowJson is null ? null : Mocking.Data.Replay.Recording.Parse(nowJson), volatileBefore, volatileAfter);
                changed++;
                io.Out.WriteLine(path);
                foreach (var line in lines.DefaultIfEmpty("rewritten, nothing a test reads changed"))
                {
                    io.Out.WriteLine("  " + line);
                }
            }

            io.Error.WriteLine($"{changed} recordings changed since {revision}");
            return Task.FromResult(0);
        });
        return command;
    }

    private static Command AcceptCommand(CliIo io)
    {
        var recordings = Recordings();
        var command = new Command("accept", "Mark every recording read as it is now (reviewed.json), after reading bruinet sql diff.") { recordings };
        command.Action(io, parsed =>
        {
            var folder = Path.GetFullPath(parsed.GetValue(recordings)!, io.CurrentDirectory);
            RecordingReview.Accept(folder);
            io.Error.WriteLine($"accepted: wrote {Path.Combine(folder, RecordingReview.FileName)}");
            return 0;
        });
        return command;
    }

    private static Command CheckCommand(CliIo io)
    {
        var recordings = Recordings();
        var json = CommonOptions.Json();
        var command = new Command("check", "The gate: recordings that went in unread (changed since bruinet sql accept). Exit 1 when any.") { recordings, json };
        command.Action(io, parsed =>
        {
            var unread = RecordingReview.Unreviewed(Path.GetFullPath(parsed.GetValue(recordings)!, io.CurrentDirectory));
            Lines(io, unread, parsed.GetValue(json));
            io.Error.WriteLine(unread.Count == 0 ? "every recording reviewed" : $"{unread.Count} recordings unread: read bruinet sql diff --recordings <folder>, then bruinet sql accept");
            return unread.Count == 0 ? 0 : 1;
        });
        return command;
    }

    /// <summary>A gate's list on stdout: a line each, or one JSON array (<c>[]</c> when empty).</summary>
    internal static void Lines(CliIo io, IEnumerable<string> lines, bool json)
    {
        if (json)
        {
            io.Out.WriteLine(CommonOptions.ToJson(lines.ToList()));
            return;
        }

        foreach (var line in lines)
        {
            io.Out.WriteLine(line);
        }
    }

    internal static string GitText(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var git = Process.Start(start) ?? throw new InvalidOperationException("git did not start.");
        var output = git.StandardOutput.ReadToEndAsync();
        var error = git.StandardError.ReadToEnd();
        git.WaitForExit();
        return git.ExitCode == 0 ? output.Result : throw new InvalidOperationException($"git {string.Join(' ', arguments)}: {error.Trim()}");
    }

    private static Command StaleCommand(CliIo io)
    {
        var schema = new Option<string>("--schema") { Description = "The schema snapshot now.", Required = true };
        var recordings = Recordings();
        var versions = new Option<string[]>("--stamp") { Description = "A version that shapes the SQL, name=value (ef=10.0.12); compared with the recordings' (repeatable).", AllowMultipleArgumentsPerToken = true };
        var json = CommonOptions.Json();
        var command = new Command("stale", "The recordings a schema change made stale, and what changed, from their stamps without replaying: the tests to re-record. Exit 1 when any is stale.") { schema, recordings, versions, json };
        command.Action(io, (parsed, cancellationToken) =>
        {
            var snapshot = SchemaSnapshot.Load(Path.GetFullPath(parsed.GetValue(schema)!, io.CurrentDirectory));
            var given = (parsed.GetValue(versions) ?? []).Select(item => item.Split(['='], 2)).Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
            var folder = Path.GetFullPath(parsed.GetValue(recordings)!, io.CurrentDirectory);
            var states = RecordingCheck.Check(folder, snapshot.ToSchema(given));
            if (parsed.GetValue(json))
            {
                io.Out.WriteLine(CommonOptions.ToJson(states.Where(state => state.IsStale)
                    .Select(state => new { recording = Path.GetRelativePath(folder, state.Path).Replace('\\', '/'), differences = state.Differences })
                    .ToList()));
            }
            else
            {
                foreach (var state in states.Where(state => state.IsStale))
                {
                    io.Out.WriteLine(Path.GetRelativePath(folder, state.Path));
                    foreach (var difference in state.Differences)
                    {
                        io.Out.WriteLine("  " + difference);
                    }
                }
            }

            var unlisted = states.SelectMany(state => state.Objects).Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(name => snapshot.Objects.TryGetValue(name, out var item) && item.Dynamic && !item.Listed)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
            if (unlisted.Count > 0)
            {
                io.Error.WriteLine($"warning: recordings touch dynamic SQL with no references listed, so a change to what it reads is missed: {string.Join(", ", unlisted)}");
            }

            var stale = states.Count(state => state.IsStale);
            io.Error.WriteLine($"{stale} of {states.Count} recordings stale");
            return Task.FromResult(stale > 0 ? 1 : 0);
        });
        return command;
    }

    private static Command StartCommand(CliIo io)
    {
        var connection = Connection();
        var connectionFrom = ConnectionFrom();
        var folder = new Option<string?>("--server-folder") { Description = "A folder on the server for the session's files. Default: the server's log folder." };
        var state = State();
        var like = new Option<string?>("--database-like") { Description = "Capture every database whose name is LIKE this (TestDb_%), for a suite that makes one per run; default: the connection's." };
        var command = new Command("start", "Start the session on the connection's database and send the marker that lines the clocks up. Needs ALTER ANY EVENT SESSION.") { connection, connectionFrom, folder, state, like };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var sql = await OpenAsync(ConnectionString(parsed, connection, connectionFrom), cancellationToken);
            var where = parsed.GetValue(folder) ?? await DefaultFolderAsync(sql, cancellationToken);
            var started = await ExtendedEvents.StartAsync(sql, where, cancellationToken, parsed.GetValue(like));
            var to = Path.GetFullPath(parsed.GetValue(state)!, io.CurrentDirectory);
            started.Save(to);
            io.Error.WriteLine($"Session {started.Session} on {started.Database}, its files {started.Files}; state in {to}");
            return 0;
        });
        return command;
    }

    private static Command StopCommand(CliIo io)
    {
        var connection = Connection();
        var connectionFrom = ConnectionFrom();
        var state = State();
        var command = new Command("stop", "Stop and drop the session; its files stay for read.") { connection, connectionFrom, state };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            var started = CaptureState.Load(Path.GetFullPath(parsed.GetValue(state)!, io.CurrentDirectory));
            await using var sql = await OpenAsync(ConnectionString(parsed, connection, connectionFrom), cancellationToken);
            await ExtendedEvents.StopAsync(sql, started.Database, cancellationToken);
            io.Error.WriteLine($"Session {started.Session} stopped.");
            return 0;
        });
        return command;
    }

    private static Command ReadCommand(CliIo io)
    {
        var connection = Connection();
        var connectionFrom = ConnectionFrom();
        var state = State();
        var output = new Option<string>("--out") { Description = "The log folder to write to.", DefaultValueFactory = _ => "sql-log" };
        var command = new Command("read", "Read the session's files through the server into the log, on this machine's clock.") { connection, connectionFrom, state, output };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            var started = CaptureState.Load(Path.GetFullPath(parsed.GetValue(state)!, io.CurrentDirectory));
            await using var sql = await OpenAsync(ConnectionString(parsed, connection, connectionFrom), cancellationToken);
            var entries = await ExtendedEvents.ReadAsync(sql, started, cancellationToken);
            var folder = Path.GetFullPath(parsed.GetValue(output)!, io.CurrentDirectory);
            var log = new SqlLog(folder);
            foreach (var entry in entries)
            {
                log.Write(entry);
            }

            io.Error.WriteLine($"{entries.Count} commands into {folder}");
            return 0;
        });
        return command;
    }

    private static Command ReportCommand(CliIo io)
    {
        var logs = new Option<string[]>("--log") { Description = "Log folders or files (*.jsonl).", Required = true, AllowMultipleArgumentsPerToken = true };
        var timings = new Option<string[]>("--timings") { Description = "The run's timings (BRUINET_TIMINGS): their test spans join the commands to the tests.", AllowMultipleArgumentsPerToken = true };
        var schema = new Option<string?>("--schema") { Description = "The schema snapshot: a procedure's or view's tables count for the tests that use it." };
        var output = new Option<string>("--out") { Description = "The folder for report.md, report.json and sql-touch-map.json.", DefaultValueFactory = _ => "sql-report" };
        var command = new Command("report", "The database's share of each test, how far each object reaches, and the commands repeated within a test.") { logs, timings, schema, output };
        command.Action(io, parsed =>
        {
            var entries = parsed.GetValue(logs)!.SelectMany(path => SqlLog.Read(Path.GetFullPath(path, io.CurrentDirectory))).ToList();
            var csv = (parsed.GetValue(timings) ?? []).SelectMany(path => TouchMap.CsvFiles(Path.GetFullPath(path, io.CurrentDirectory))).ToList();
            var snapshot = parsed.GetValue(schema) is { } file ? SchemaSnapshot.Load(Path.GetFullPath(file, io.CurrentDirectory)) : null;
            var report = SqlReport.Of(entries, SqlReport.Windows(csv), snapshot);
            var folder = Path.GetFullPath(parsed.GetValue(output)!, io.CurrentDirectory);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "report.md"), report.ToMarkdown());
            File.WriteAllText(Path.Combine(folder, "report.json"), report.ToJson());
            File.WriteAllText(Path.Combine(folder, "sql-touch-map.json"), CommonOptions.ToJson(report.TouchMap()));
            io.Error.WriteLine($"{report.Tests.Count} tests, {entries.Count} commands, {report.Objects.Count} objects; wrote {folder}");
            return 0;
        });
        return command;
    }

    private static Option<string?> Connection() => new("--connection") { Description = "A connection string to the test database. Prefer --connection-from: this one lands in shell history and the process list." };

    private static Option<string?> ConnectionFrom() => new("--connection-from") { Description = "The environment variable that holds the connection string, so a password stays off the command line." };

    // The one connection string the command was given: by value or by variable, not both and not neither.
    private static string ConnectionString(ParseResult parsed, Option<string?> connection, Option<string?> from)
    {
        var given = parsed.GetValue(connection);
        var variable = parsed.GetValue(from);
        if (given is { Length: > 0 } == variable is { Length: > 0 })
        {
            throw new ArgumentException("Give the test database as --connection-from <variable> or --connection <string>, one of them.");
        }

        return given is { Length: > 0 }
            ? given
            : Environment.GetEnvironmentVariable(variable!) is { Length: > 0 } value
                ? value
                : throw new ArgumentException($"The environment variable {variable} is not set: it holds the test database's connection string.");
    }

    private static Option<string> State() => new("--state") { Description = "The capture's state file.", DefaultValueFactory = _ => StateFile };

    private static async Task<SqlConnection> OpenAsync(string connectionString, CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<string> DefaultFolderAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000))", connection);
        return (string)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
