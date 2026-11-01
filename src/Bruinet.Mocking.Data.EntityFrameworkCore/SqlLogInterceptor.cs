using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bruinet.Mocking.Data.EntityFrameworkCore;

/// <summary>
/// Logs every command and connection open an EF Core context makes to a <see cref="SqlLog"/>. It reads EF's own events
/// (a reader's rows and time when it is disposed) and never wraps the reader, so what EF reads is unchanged: SQL Server's
/// spatial mapping, which casts the reader to <c>SqlDataReader</c>, keeps working.
/// </summary>
public sealed class SqlLogInterceptor(SqlLog log) : DbCommandInterceptor
{
    /// <summary>Logs connection opens; added beside the command interceptor (<see cref="SqlLogExtensions.UseSqlLog"/>).</summary>
    public IInterceptor Connections { get; } = new ConnectionLog(log);

    // When each reader's command started, on this interceptor's own clock: EF's DataReaderDisposing duration doesn't run
    // from the execution to the disposal, so a reader's time, rows read included, is measured here.
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<DbCommand, Started> _started = new();

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        _started.AddOrUpdate(command, new Started(DateTimeOffset.UtcNow, System.Diagnostics.Stopwatch.GetTimestamp()));
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(ReaderExecuting(command, eventData, result));

    public override InterceptionResult DataReaderDisposing(DbCommand command, DataReaderDisposingEventData eventData, InterceptionResult result)
    {
        var entry = _started.TryGetValue(command, out var started)
            ? Entry("reader", command, started.At, System.Diagnostics.Stopwatch.GetElapsedTime(started.Timestamp))
            : Entry("reader", command, eventData.StartTime, eventData.Duration);
        // EF counts read calls: one more than the rows when the reader was read to its end, as EF reads.
        entry.Rows = Math.Max(0, eventData.ReadCount - 1);
        var affected = eventData.DataReader.RecordsAffected;
        entry.Affected = affected >= 0 ? affected : null;
        log.Write(entry);
        return result;
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        var entry = Entry("non-query", command, eventData.StartTime, eventData.Duration);
        entry.Affected = result;
        log.Write(entry);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(NonQueryExecuted(command, eventData, result));

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        log.Write(Entry("scalar", command, eventData.StartTime, eventData.Duration));
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(ScalarExecuted(command, eventData, result));

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        var entry = Entry("error", command, eventData.StartTime, eventData.Duration);
        entry.Error = eventData.Exception is DbException error ? LoggingDbCommand.ErrorNumber(error) : null;
        log.Write(entry);
    }

    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        CommandFailed(command, eventData);
        return Task.CompletedTask;
    }

    private static SqlLogEntry Entry(string kind, DbCommand command, DateTimeOffset start, TimeSpan duration)
        => SqlLog.Entry(
            kind,
            command.CommandType == CommandType.StoredProcedure ? "EXEC " + command.CommandText : command.CommandText,
            command.Parameters.Cast<DbParameter>().Select(parameter => parameter.ParameterName),
            start.UtcDateTime,
            duration);

    private sealed record Started(DateTimeOffset At, long Timestamp);

    private sealed class ConnectionLog(SqlLog log) : DbConnectionInterceptor
    {
        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
            => log.Write(SqlLog.Entry("open", null, null, eventData.StartTime.UtcDateTime, eventData.Duration));

        public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            ConnectionOpened(connection, eventData);
            return Task.CompletedTask;
        }
    }
}

/// <summary>Adds the SQL log to a context's options.</summary>
public static class SqlLogExtensions
{
    /// <summary>Logs every command and connection open of contexts made with these options to <paramref name="log"/>.</summary>
    public static DbContextOptionsBuilder UseSqlLog(this DbContextOptionsBuilder options, SqlLog log)
    {
        var interceptor = new SqlLogInterceptor(log);
        return options.AddInterceptors(interceptor, interceptor.Connections);
    }

    /// <inheritdoc cref="UseSqlLog(DbContextOptionsBuilder, SqlLog)"/>
    public static DbContextOptionsBuilder<TContext> UseSqlLog<TContext>(this DbContextOptionsBuilder<TContext> options, SqlLog log)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseSqlLog((DbContextOptionsBuilder)options, log);
}
