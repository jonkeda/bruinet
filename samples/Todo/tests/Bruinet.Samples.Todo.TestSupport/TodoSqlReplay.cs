using System.Data.Common;
using System.Reflection;
using System.Text.RegularExpressions;
using Bruinet.Mocking.Data;
using Bruinet.Mocking.Data.Replay;
using Bruinet.Samples.Todo.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace Bruinet.Samples.Todo.TestSupport;

/// <summary>
/// SQL replay for one app that outlives its tests (.my/sql-replay/plan.md, step 8): the app's <see cref="TodoDatabase.Wrap"/>
/// reaches the session of the test running now, begun in its class's constructor and ended in its <c>Dispose</c>. Owned by
/// the fixture or device that owns the app, never static: tests in a collection run one after another, collections each
/// have their own. Only for a head that composes the app in the test's process (bUnit, MauiHeadless); <c>BRUINET_SQL_REPLAY</c>
/// says <c>Record</c> or <c>Replay</c>.
/// </summary>
public sealed class TodoSqlReplay
{
    private static readonly Regex Unsafe = new("[^A-Za-z0-9._-]+", RegexOptions.Compiled);
    private static readonly object Gate = new();
    private readonly string _folder;
    private readonly string _head;
    private readonly string? _device;
    private readonly Schema? _schema;
    private ReplaySession? _session;
    private string? _test;
    private bool _startedUp;

    /// <param name="project">The suite's project folder name; its recordings are <c>Recordings/&lt;head&gt;/&lt;test&gt;.json</c> beside it.</param>
    /// <param name="head">The head the app runs on (<c>bunit</c>, <c>headless</c>); null when it isn't one that can replay.</param>
    /// <param name="device">The app's name, when a test has more than one; null for the only one.</param>
    public TodoSqlReplay(string project, string? head, string? device = null)
    {
        Mode = head is null ? ReplayMode.Off : ModeOfRun;
        _folder = RecordingsOf(project);
        _head = head ?? "none";
        _device = device;
        if (Mode != ReplayMode.Off)
        {
            _schema = SchemaOf(_folder, Mode);
        }
    }

    /// <summary>The run's mode: <c>BRUINET_SQL_REPLAY</c>, else off.</summary>
    public static ReplayMode ModeOfRun
        => Enum.TryParse<ReplayMode>(Environment.GetEnvironmentVariable(ReplayRegistry.ModeVariable), ignoreCase: true, out var mode) ? mode : ReplayMode.Off;

    /// <summary>This app's mode: off on a head that runs the app in a process of its own.</summary>
    public ReplayMode Mode { get; }

    /// <summary>The app's connections through the test's session; as they are between tests and with replay off.</summary>
    public DbConnection Wrap(DbConnection connection)
        => _session is { } session ? new ReplayingDbConnection(connection, session) : connection;

    /// <summary>
    /// Begins the session of what the app does before the first test (its first list load, a first sync), named after
    /// <paramref name="fixture"/>: an app started for a collection works before any test has begun. Once per app; the
    /// first test's <see cref="Begin"/> ends it.
    /// </summary>
    public void BeginStartup(string fixture)
    {
        if (Mode == ReplayMode.Off || _startedUp)
        {
            return;
        }

        _startedUp = true;
        Open(fixture + ".startup");
    }

    /// <summary>
    /// Begins <paramref name="test"/>'s session, once: a test that starts over keeps the one it has. A startup session
    /// still open is ended first, its misses thrown into this test, the first.
    /// </summary>
    public void Begin(string? test)
    {
        if (Mode == ReplayMode.Off || test is null || _test == test)
        {
            return;
        }

        End();
        Open(test);
    }

    private void Open(string test)
    {
        _test = test;
        var file = Unsafe.Replace(test, "_") + (_device is null ? string.Empty : "." + Unsafe.Replace(_device, "_")) + ".json";
        _session = new ReplaySession(Mode, Path.Combine(_folder, _head, file), _schema);
    }

    /// <summary>Ends the test's session: its recording saved, or its misses thrown, so they fail the test that made them.</summary>
    public void End()
    {
        var session = _session;
        _session = null;
        _test = null;
        if (session is null)
        {
            return;
        }

        session.Finish();
        if (session.Misses.Count > 0)
        {
            throw new ReplayMissException(string.Join("\n\n", session.Misses));
        }
    }

    /// <summary>
    /// The test's name, <c>Class.Method</c> and a theory's arguments (each case its own recording), from xUnit's output
    /// helper (its private <c>test</c>, an <c>ITest</c>).
    /// </summary>
    public static string TestName(object output)
    {
        var test = output.GetType().GetField("test", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(output)
            ?? throw new InvalidOperationException("The output helper names no test.");
        var name = (string)test.GetType().GetProperty("DisplayName")!.GetValue(test)!;
        var open = name.IndexOf('(');
        var plain = (open < 0 ? name : name.Substring(0, open)).Split('.');
        var method = plain.Length >= 2 ? plain[^2] + "." + plain[^1] : plain[^1];
        return open < 0 ? method : method + name.Substring(open);
    }

    /// <summary>The suite's recordings folder, beside its project so they are kept in git.</summary>
    public static string RecordingsOf(string project)
    {
        // Above the test's output (the suite's own project), or beside it: another suite's fixture (the UAT project's) reads this project's recordings.
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, project + ".csproj")))
            {
                return Path.Combine(folder.FullName, "Recordings");
            }

            if (File.Exists(Path.Combine(folder.FullName, project, project + ".csproj")))
            {
                return Path.Combine(folder.FullName, project, "Recordings");
            }
        }

        throw new DirectoryNotFoundException(project);
    }

    // Recording writes the snapshot from a file the app's migrations made; replay reads it. Read once per app.
    private static Schema SchemaOf(string folder, ReplayMode mode)
    {
        var path = Path.Combine(folder, "schema.json");
        if (mode == ReplayMode.Record)
        {
            var database = new TempDatabase("schema");
            try
            {
                new TodoDatabase(database.Path).MigrateAsync().GetAwaiter().GetResult();
                using var connection = new SqliteConnection($"Data Source={database.Path};Pooling=False");
                connection.Open();
                var snapshot = SchemaSnapshot.ReadAsync(connection).GetAwaiter().GetResult();
                lock (Gate)
                {
                    snapshot.Save(path);
                }

                return snapshot.ToSchema();
            }
            finally
            {
                database.Dispose();
            }
        }

        return File.Exists(path)
            ? SchemaSnapshot.Load(path).ToSchema()
            : throw new ReplayMissException($"No schema snapshot at {path}: record first ({ReplayRegistry.ModeVariable}=Record).");
    }
}
