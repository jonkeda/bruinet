using System.Text.Json.Nodes;
using Bruinet.Mocking.Data.Replay;
using Microsoft.Data.Sqlite;

namespace Bruinet.Mocking.Data.Tests;

/// <summary>
/// What a SQL recording never keeps (review 09, D4, R2-T19): a secret-named parameter is written *** and replays
/// against any live value; a column the suite names is written ***; a value that looks like a key stops the save.
/// </summary>
public sealed class SqlRedactionTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "Bruinet", "RedactionTests", Guid.NewGuid().ToString("N"));

    public SqlRedactionTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void A_SecretNamedParameter_IsSavedAsStars_AndReplaysAgainstAnyValue()
    {
        var database = Path.Combine(_folder, "users.db");
        var recording = Path.Combine(_folder, "users.json");
        Run(new ReplaySession(ReplayMode.Record, recording), database, "hunter2-the-real-one");
        var saved = File.ReadAllText(recording);
        SqliteConnection.ClearAllPools();
        File.Delete(database);

        var session = new ReplaySession(ReplayMode.Replay, recording);
        var replayed = Run(session, database, "another-password");

        Assert.DoesNotContain("hunter2", saved, StringComparison.Ordinal);
        Assert.Contains("*** @password #0", saved, StringComparison.Ordinal);
        Assert.Equal("mia@example.test", replayed);
        Assert.Empty(session.Misses);
    }

    [Fact]
    public void A_ValueThatLooksLikeAKey_StopsTheSave_NamingWhereItIs()
    {
        var recording = new Recording
        {
            Exchanges =
            [
                new Exchange
                {
                    Index = 0,
                    Kind = "reader",
                    Text = "SELECT Note FROM Users",
                    Results = [new RecordedResult([new RecordedColumn("Note", "System.String", "TEXT")], [[Values.ToJson("token AKIAABCDEFGHIJKLMNOP in a note")]])],
                },
            ],
        };

        var error = Assert.Throws<SqlRecordingSecretException>(() => SqlRedaction.Apply(recording, "users.json"));

        Assert.Contains("column Note", error.Message, StringComparison.Ordinal);
        Assert.Contains("an AWS access key", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SecretNames_AreKnownByTheirName()
    {
        Assert.True(SqlRedaction.IsSecretName("@PasswordHash"));
        Assert.True(SqlRedaction.IsSecretName("api_key"));
        Assert.True(SqlRedaction.IsSecretName("SecurityStamp"));
        Assert.False(SqlRedaction.IsSecretName("@Email"));
    }

    // Seeds a user (outside the session), then signs in through the session: a read with the password as a parameter.
    private static string Run(ReplaySession session, string database, string password)
    {
        if (session.Mode == ReplayMode.Record)
        {
            using var seed = new SqliteConnection($"Data Source={database}");
            seed.Open();
            using var create = seed.CreateCommand();
            create.CommandText = "CREATE TABLE Users (Email TEXT, Password TEXT); INSERT INTO Users VALUES ('mia@example.test', 'hunter2-the-real-one');";
            create.ExecuteNonQuery();
        }

        string? email;
        using (var connection = new ReplayingDbConnection(new SqliteConnection($"Data Source={database}"), session))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Email FROM Users WHERE Password = @password OR @password = 'another-password'";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@password";
            parameter.Value = password;
            command.Parameters.Add(parameter);
            email = command.ExecuteScalar() as string;
        }

        session.Finish();
        return email ?? "(none)";
    }
}
