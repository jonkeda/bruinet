using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using Bruinet.Mocking.Data.Replay;
using Xunit;

namespace Bruinet.Mocking.Data.Net48.Tests;

/// <summary>The ADO.NET wrapper in a .NET Framework process, over the framework's own SqlClient and the local SQL Server.</summary>
public sealed class FrameworkTests
{
    private static string Server => Environment.GetEnvironmentVariable("BRUINET_SQL_SERVER") is { Length: > 0 } server ? server : ".";

    [SqlServerFact]
    public void Wrapper_LogsTheFrameworksSqlClient_CommandsReadersAndProcedures()
    {
        Assert.StartsWith(".NET Framework", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
        var log = new SqlLog(null, keep: true);
        using (var connection = new LoggingDbConnection(new SqlConnection($"Server={Server};Database=master;Integrated Security=True"), log))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT name FROM sys.databases WHERE name = @name";
                var name = command.CreateParameter();
                name.ParameterName = "@name";
                name.Value = "master";
                command.Parameters.Add(name);
                using (var reader = command.ExecuteReader())
                {
                    Assert.True(reader.Read());
                    Assert.Equal("master", reader.GetString(0));
                }
            }

            using (var procedure = connection.CreateCommand())
            {
                procedure.CommandType = CommandType.StoredProcedure;
                procedure.CommandText = "sys.sp_server_info";
                using (var reader = procedure.ExecuteReader())
                {
                    while (reader.Read())
                    {
                    }
                }
            }
        }

        var entries = log.Entries;
        Assert.Equal("open", entries[0].Kind);
        var query = entries.Single(entry => entry.Text == "SELECT name FROM sys.databases WHERE name = @name");
        Assert.Equal(1, query.Rows);
        Assert.Equal(new[] { "@name" }, query.Parameters);
        Assert.Contains(entries, entry => entry.Text == "EXEC sys.sp_server_info" && entry.Rows > 0);
    }

    [SqlServerFact]
    public void Replay_TheFrameworksSqlClient_OutputsRowsAndErrors_WithNoServer()
    {
        var recording = Path.Combine(Path.GetTempPath(), "Bruinet", "ReplayTests", Guid.NewGuid().ToString("N"), "framework.json");
        try
        {
            var recorded = Run(new ReplaySession(ReplayMode.Record, recording), $"Server={Server};Database=master;Integrated Security=True");
            var session = new ReplaySession(ReplayMode.Replay, recording);
            var replayed = Run(session, "Server=nowhere.invalid;Database=Gone;Integrated Security=True;Connect Timeout=1");

            Assert.Equal(recorded, replayed);
            Assert.Equal("master|next 42|error 8134", replayed);
            Assert.Empty(session.Misses);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(recording), recursive: true);
        }
    }

    private static string Run(ReplaySession session, string connectionString)
    {
        string name;
        object next;
        SqlException error;
        using (var connection = new ReplayingDbConnection(new SqlConnection(connectionString), session))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SET @next = @value + 1; SELECT name FROM sys.databases WHERE name = @name";
                command.Parameters.Add(new SqlParameter("@name", "master"));
                command.Parameters.Add(new SqlParameter("@value", 41));
                var output = new SqlParameter("@next", SqlDbType.Int) { Direction = ParameterDirection.Output };
                command.Parameters.Add(output);
                using (var reader = command.ExecuteReader())
                {
                    Assert.True(reader.Read());
                    name = reader.GetString(0);
                }

                next = output.Value;
            }

            using (var failing = connection.CreateCommand())
            {
                failing.CommandText = "SELECT 1 / 0";
                error = Assert.Throws<SqlException>(() => failing.ExecuteScalar());
            }
        }

        session.Finish();
        return $"{name}|next {next}|error {error.Number}";
    }
}
