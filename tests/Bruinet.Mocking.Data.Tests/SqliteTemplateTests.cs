using Microsoft.Data.Sqlite;

namespace Bruinet.Mocking.Data.Tests;

/// <summary>A SQLite dependency's copy mode: the template made once per stamp, each test on its own copy.</summary>
public sealed class SqliteTemplateTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "Bruinet.Mocking.Data.Tests", Guid.NewGuid().ToString("N"));

    private string Template => Path.Combine(_folder, "seed.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task The_template_is_made_once_per_stamp()
    {
        var made = 0;
        Task Seed(string path)
        {
            made++;
            return SeedAsync(path);
        }

        await SqliteTemplate.EnsureAsync(Template, "build-1", Seed);
        await SqliteTemplate.EnsureAsync(Template, "build-1", Seed);
        Assert.Equal(1, made);

        await SqliteTemplate.EnsureAsync(Template, "build-2", Seed);
        Assert.Equal(2, made);
    }

    [Fact]
    public async Task Each_test_gets_its_own_copy_and_disposing_it_deletes_it()
    {
        await SqliteTemplate.EnsureAsync(Template, "build-1", SeedAsync);
        using var first = SqliteTemplate.Copy(Template, "First test", _folder);
        string secondPath;
        using (var second = SqliteTemplate.Copy(Template, "Second: test", _folder))
        {
            await ExecuteAsync(first.ConnectionString, "INSERT INTO todos (title) VALUES ('Bread')");

            Assert.Equal(2L, await CountAsync(first.ConnectionString));
            Assert.Equal(1L, await CountAsync(second.ConnectionString));
            secondPath = second.Path;
            SqliteConnection.ClearAllPools();
        }

        Assert.False(File.Exists(secondPath));
        Assert.Equal(1L, await CountAsync("Data Source=" + Template));
    }

    private static async Task SeedAsync(string path)
    {
        await ExecuteAsync("Data Source=" + path, "CREATE TABLE todos (id INTEGER PRIMARY KEY, title TEXT NOT NULL); INSERT INTO todos (title) VALUES ('Milk');");
        SqliteConnection.ClearAllPools();
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM todos";
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
