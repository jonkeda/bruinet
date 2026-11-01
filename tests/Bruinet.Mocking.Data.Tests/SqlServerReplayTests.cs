using System.Data;
using System.Data.SqlTypes;
using Bruinet.Mocking.Data.Replay;
using Microsoft.Data.SqlClient;
using System.Globalization;

namespace Bruinet.Mocking.Data.Tests;

/// <summary>
/// The ADO.NET adapter over SQL Server (R2 of .my/sql-replay/plan.md): a procedure with an output parameter, a return
/// value and two result sets, a geography column, and a broken key raised again as SqlException 2627, all replayed
/// against a server that doesn't exist.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class SqlServerReplayTests : IAsyncLifetime
{
    private readonly string _database = "BruinetSqlReplayTests_" + Guid.NewGuid().ToString("N")[..8];
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "Bruinet", "ReplayTests", Guid.NewGuid().ToString("N"));

    private string Connection => $"Server={SqlServerTests.Server};Database={_database};Integrated Security=True;TrustServerCertificate=True";

    public async Task InitializeAsync()
    {
        if (new SqlServerFactAttribute().Skip is not null)
        {
            return;
        }

        await Execute(SqlServerTests.Master, $"CREATE DATABASE [{_database}]");
        await Execute(Connection, "CREATE TABLE dbo.Places (Id int NOT NULL PRIMARY KEY, Name nvarchar(100) NOT NULL, Location geography NULL)");
        await Execute(Connection, "INSERT INTO dbo.Places VALUES (1, N'Hoorn', geography::Point(52.64, 5.06, 4326)), (2, N'Enkhuizen', NULL)");
        await Execute(Connection, """
            CREATE PROCEDURE dbo.FindPlace @name nvarchar(100), @count int OUTPUT AS
            BEGIN
                SELECT Id, Name FROM dbo.Places WHERE Name = @name;
                SELECT Id, Location FROM dbo.Places ORDER BY Id;
                SET @count = (SELECT COUNT(*) FROM dbo.Places);
                RETURN 7;
            END
            """);
    }

    public async Task DisposeAsync()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }

        if (new SqlServerFactAttribute().Skip is not null)
        {
            return;
        }

        SqlConnection.ClearAllPools();
        await Execute(SqlServerTests.Master, $"ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_database}]");
    }

    [SqlServerFact]
    public async Task Replay_GivesOutputsReturnValueResultSetsGeographyAndErrors_WithNoServer()
    {
        var recording = Path.Combine(_folder, "find-place.json");
        var recorded = await Run(new ReplaySession(ReplayMode.Record, recording), Connection);

        var session = new ReplaySession(ReplayMode.Replay, recording);
        var replayed = await Run(session, "Server=nowhere.invalid;Database=Gone;Integrated Security=True;Connect Timeout=1");

        Assert.Equal(recorded, replayed);
        Assert.StartsWith("1|Hoorn;1:22,2:null;count 2;return 7;error 2627", replayed);
        Assert.Empty(session.Misses);
    }

    private static async Task<string> Run(ReplaySession session, string connectionString)
    {
        using var connection = new ReplayingDbConnection(new SqlConnection(connectionString), session);
        await connection.OpenAsync();
        var parts = new List<string>();

        using (var command = connection.CreateCommand())
        {
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "dbo.FindPlace";
            command.Parameters.Add(new SqlParameter("@name", "Hoorn"));
            var count = new SqlParameter("@count", SqlDbType.Int) { Direction = ParameterDirection.Output };
            var returned = new SqlParameter("@return", SqlDbType.Int) { Direction = ParameterDirection.ReturnValue };
            command.Parameters.Add(count);
            command.Parameters.Add(returned);
            using (var reader = await command.ExecuteReaderAsync())
            {
                Assert.True(await reader.ReadAsync());
                parts.Add($"{reader.GetInt32(0)}|{reader.GetString(1)}");
                Assert.True(await reader.NextResultAsync());
                var locations = new List<string>();
                while (await reader.ReadAsync())
                {
                    locations.Add($"{reader.GetInt32(0)}:{(reader.IsDBNull(1) ? "null" : reader.GetFieldValue<SqlBytes>(1).Length.ToString(CultureInfo.InvariantCulture))}");
                }

                parts.Add(string.Join(",", locations));
            }

            parts.Add($"count {count.Value}");
            parts.Add($"return {returned.Value}");
        }

        using (var insert = connection.CreateCommand())
        {
            insert.CommandText = "INSERT INTO dbo.Places (Id, Name) VALUES (@id, @name)";
            insert.Parameters.Add(new SqlParameter("@id", 1));
            insert.Parameters.Add(new SqlParameter("@name", "Hoorn again"));
            var error = await Assert.ThrowsAsync<SqlException>(() => insert.ExecuteNonQueryAsync());
            parts.Add($"error {error.Number} class {error.Class} {error.Message[..30]}");
        }

        session.Finish();
        return string.Join(";", parts);
    }

    private static async Task Execute(string connectionString, string sql)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
