using System.Text.Json;
using System.Text.Json.Serialization;
using Bruinet.Api.Mocking;
using Bruinet.Samples.Todo.Contracts;
using Bruinet.Samples.Todo.Core.Models;
using Bruinet.Samples.Todo.Infrastructure.Api;
using Bruinet.Samples.Todo.Infrastructure.Data;
using Bruinet.TestData;

namespace Bruinet.Samples.Todo.TestSupport;

/// <summary>
/// A named starting state: what the server has, what the device has, and what time it is.
/// </summary>
/// <remarks>
/// <para>
/// Test sets are data (<c>TestData/*.json</c>), so the integration fixture and the UI fixture
/// mean the same thing by "three-todos". <see cref="SeedDatabaseAsync"/> writes the local half
/// with the app's own migrations and repository; <see cref="StubServer"/> serves the server half
/// from WireMock.
/// </para>
/// <para>
/// Files are read strictly: an unknown field, a missing required field or a <c>null</c> where the
/// type has none is an error naming the file, not a todo with a missing title.
/// </para>
/// <para>
/// <see cref="Now"/> is pinned in every tier that uses the test set (the UI tiers pass it as
/// <c>TODO_NOW</c>), so due dates written as plain dates are overdue or not on every run alike.
/// </para>
/// </remarks>
public sealed class TodoTestSet
{
    private const string ResourcePrefix = "TestData.";

    /// <summary>The serializer settings test-set files are read with.</summary>
    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    private TodoTestSet(string name, TodoTestSetFile file)
    {
        Name = name;
        Now = file.Now;
        Server = file.Server;
        Local = file.Local.Select(local => local.ToItem(file.Server, name)).ToList();
        ServerUsers = file.Users ?? [];
        LocalUsers = (file.LocalUsers ?? ServerUsers).Select(UserMapping.ToUser).ToList();
    }

    /// <summary>The test set's name, its file name without extension.</summary>
    public string Name { get; }

    /// <summary>The instant the test set is set at.</summary>
    public DateTimeOffset Now { get; }

    /// <summary>What the server has.</summary>
    public IReadOnlyList<TodoDto> Server { get; }

    /// <summary>What the device has, with the "same as the server" shortcut filled in.</summary>
    public IReadOnlyList<TodoItem> Local { get; }

    /// <summary>The users the server has.</summary>
    public IReadOnlyList<UserDto> ServerUsers { get; }

    /// <summary>The users the device has cached: the server's, unless the file says otherwise.</summary>
    public IReadOnlyList<User> LocalUsers { get; }

    /// <summary>The test sets that exist.</summary>
    public static IReadOnlyList<string> Names { get; } = typeof(TodoTestSet).Assembly
        .GetManifestResourceNames()
        .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal))
        .Select(name => name[ResourcePrefix.Length..^".json".Length])
        .Order(StringComparer.Ordinal)
        .ToList();

    /// <summary>Loads a test set by name, e.g. <c>three-todos</c>.</summary>
    /// <exception cref="ArgumentException">There is no such test set; the message lists those there are.</exception>
    /// <exception cref="InvalidDataException">The file does not match the types; the message names the file, line and field.</exception>
    public static TodoTestSet Load(string name)
    {
        using var stream = typeof(TodoTestSet).Assembly.GetManifestResourceStream($"{ResourcePrefix}{name}.json")
            ?? throw new ArgumentException(
                $"There is no test set '{name}'. There are: {string.Join(", ", Names)}.", nameof(name));

        TodoTestSetFile? file;
        try
        {
            file = JsonSerializer.Deserialize<TodoTestSetFile>(stream, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"TestData/{name}.json:{exception.LineNumber + 1}: {exception.Message}", exception);
        }

        return new TodoTestSet(name, file ?? throw new InvalidDataException($"TestData/{name}.json is empty."));
    }

    /// <summary>Creates the database at <paramref name="path"/> and stores the local half.</summary>
    /// <remarks>
    /// In a replayed run (<see cref="TodoSqlReplay"/>) the file is made empty: the app's reads are answered from the
    /// recording, and one that reached the file would find nothing.
    /// </remarks>
    public async Task SeedDatabaseAsync(string path, CancellationToken cancellationToken = default)
    {
        var database = new TodoDatabase(path);
        await database.MigrateAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (TodoSqlReplay.ModeOfRun == Bruinet.Mocking.Data.Replay.ReplayMode.Replay)
        {
            return;
        }

        var repository = new SqliteTodoRepository(database);
        foreach (var item in Local)
        {
            await repository.SaveAsync(item, cancellationToken).ConfigureAwait(false);
        }

        await new SqliteUserRepository(database).ReplaceAllAsync(LocalUsers, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Puts a database back to this test set's local half, in place: its todos and users replaced,
    /// the file kept, so an app that has it open sees the test set on its next read (no restart).
    /// </summary>
    /// <param name="path">The app's database file.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    public async Task RestoreDatabaseAsync(string path, CancellationToken cancellationToken = default)
    {
        var database = new TodoDatabase(path);
        await using (var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM todos;";
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await SeedDatabaseAsync(path, cancellationToken).ConfigureAwait(false);

        // This process's pooled handles go, so the app alone has the file.
        TodoDatabase.ReleaseFileHandles();
    }

    /// <summary>Serves the server half from <paramref name="mock"/>, as a fake that remembers writes.</summary>
    public TodoServerStub StubServer(MockApiServer mock, string apiKey = TodoApi.DevelopmentApiKey)
        => TodoServerStub.Install(mock, Server, apiKey, ServerUsers);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = TestDataJson.Strict(TodoApi.JsonOptions);
        options.Converters.Add(new JsonStringEnumConverter<SyncState>());
        return options;
    }
}

/// <summary>The shape of a <c>TestData/*.json</c> file.</summary>
/// <param name="Now">The instant the test set is set at.</param>
/// <param name="Server">What the server has, as the API sends it.</param>
/// <param name="Local">What the device has.</param>
/// <param name="Users">The users the server has; none when left out.</param>
/// <param name="LocalUsers">
/// The users the device has cached. Left out, the device has synced the server's users; <c>[]</c>
/// means it never fetched them.
/// </param>
/// <param name="Schema">The <c>$schema</c> reference editors use; ignored when loading.</param>
public sealed record TodoTestSetFile(
    DateTimeOffset Now,
    IReadOnlyList<TodoDto> Server,
    IReadOnlyList<LocalTodoData> Local,
    IReadOnlyList<UserDto>? Users = null,
    IReadOnlyList<UserDto>? LocalUsers = null,
    [property: JsonPropertyName("$schema")] string? Schema = null);

/// <summary>
/// A todo on the device, as a test set writes it: either complete, or only <see cref="Id"/> and
/// <see cref="SyncState"/> to mean "the same as the server's todo with this id".
/// </summary>
/// <param name="Id">The todo's id.</param>
/// <param name="SyncState">Whether the server has this version.</param>
/// <param name="Title">The title; leave out, with every other content field, to copy the server's todo.</param>
/// <param name="Notes">Free text, or <c>null</c>.</param>
/// <param name="DueDate">The due date, or <c>null</c>.</param>
/// <param name="Status">The status; required when <paramref name="Title"/> is given.</param>
/// <param name="CreatedAt">When it was created; required when <paramref name="Title"/> is given.</param>
/// <param name="UpdatedAt">When it last changed; required when <paramref name="Title"/> is given.</param>
/// <param name="IsDeleted">A tombstone waiting for the server to be told.</param>
public sealed record LocalTodoData(
    Guid Id,
    SyncState SyncState,
    string? Title = null,
    string? Notes = null,
    DateOnly? DueDate = null,
    TodoStatusDto? Status = null,
    DateTimeOffset? CreatedAt = null,
    DateTimeOffset? UpdatedAt = null,
    bool IsDeleted = false)
{
    /// <summary>Whether this entry is the "same as the server" shortcut.</summary>
    [JsonIgnore]
    public bool IsSameAsServer => Title is null;

    /// <summary>The device's todo, with the shortcut resolved against <paramref name="server"/>.</summary>
    /// <exception cref="InvalidDataException">The shortcut names no server todo, or a complete entry misses a field.</exception>
    public TodoItem ToItem(IReadOnlyList<TodoDto> server, string testSetName)
    {
        if (FindProblem(server) is { } problem)
        {
            throw new InvalidDataException($"TestData/{testSetName}.json: local todo {Id}: {problem}");
        }

        var dto = IsSameAsServer
            ? server.First(todo => todo.Id == Id)
            : new TodoDto(Id, Title!, Notes, DueDate, Status!.Value, CreatedAt!.Value, UpdatedAt!.Value);
        return TodoMapping.ToItem(dto) with { SyncState = SyncState, IsDeleted = IsDeleted };
    }

    /// <summary>Why this entry cannot become a todo, or <c>null</c> when it can.</summary>
    public string? FindProblem(IReadOnlyList<TodoDto> server)
    {
        if (IsSameAsServer)
        {
            if (Notes is not null || DueDate is not null || Status is not null || CreatedAt is not null || UpdatedAt is not null)
            {
                return "gives content fields but no title. Give all of them, or only id and syncState to copy the server's todo.";
            }

            return server.Any(todo => todo.Id == Id)
                ? null
                : "copies the server's todo, but the server has no todo with this id.";
        }

        return Status is null ? "has a title but no status."
            : CreatedAt is null ? "has a title but no createdAt."
            : UpdatedAt is null ? "has a title but no updatedAt."
            : null;
    }
}
