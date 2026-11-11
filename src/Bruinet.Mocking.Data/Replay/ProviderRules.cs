using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Globalization;

namespace Bruinet.Mocking.Data.Replay;

/// <summary>
/// What replay needs to know of a provider, by reflection so no provider is referenced: reading a real reader whole
/// (a UDT such as <c>geography</c> through its own <c>GetSqlBytes</c>), a command as an exchange, and a recorded error raised
/// again as the provider's own exception (<c>SqlException</c> of either SqlClient, <c>SqliteException</c>), so the app's
/// <c>catch (SqlException e) when (e.Number == 2627)</c> runs in replay. Built through the providers' internals: an upgrade of
/// one may move them.
/// </summary>
public static class ProviderRules
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    /// <summary>The live command as an exchange: its text (a procedure called as one as its <c>EXEC</c>) and its input parameters.</summary>
    public static Exchange Live(DbCommand command, string kind)
    {
        var exchange = new Exchange
        {
            Kind = kind,
            Text = command.CommandType == CommandType.StoredProcedure ? "EXEC " + command.CommandText : command.CommandText,
        };
        foreach (DbParameter parameter in command.Parameters)
        {
            if (parameter.Direction is ParameterDirection.Input or ParameterDirection.InputOutput)
            {
                exchange.Parameters[parameter.ParameterName] = Values.ToJson(parameter.Value);
            }
        }

        return exchange;
    }

    /// <summary>
    /// Replay mode: the recorded exchange answering <paramref name="command"/>, its outputs given to the command's
    /// parameters; a recorded error is raised as the provider's own exception.
    /// </summary>
    public static Exchange Replay(ReplaySession session, DbCommand command, string kind)
    {
        var exchange = session.Answer(Live(command, kind));
        if (exchange.Error is { } error)
        {
            throw Raise(error);
        }

        SetOutputs(command, exchange, session);
        return exchange;
    }

    /// <summary>The command's output, input-output and return-value parameters as they are now; null when it has none.</summary>
    public static Dictionary<string, JsonNode?>? Outputs(DbCommand command)
    {
        Dictionary<string, JsonNode?>? outputs = null;
        foreach (DbParameter parameter in command.Parameters)
        {
            if (parameter.Direction is ParameterDirection.Output or ParameterDirection.InputOutput or ParameterDirection.ReturnValue)
            {
                (outputs ??= new Dictionary<string, JsonNode?>(StringComparer.Ordinal))[parameter.ParameterName] = Values.ToJson(parameter.Value);
            }
        }

        return outputs;
    }

    /// <summary>Gives a recorded exchange's outputs back to the live command's parameters, through the session's bindings.</summary>
    public static void SetOutputs(DbCommand command, Exchange recorded, ReplaySession session)
    {
        if (recorded.Outputs is null)
        {
            return;
        }

        foreach (DbParameter parameter in command.Parameters)
        {
            if (parameter.Direction is ParameterDirection.Output or ParameterDirection.InputOutput or ParameterDirection.ReturnValue
                && recorded.Outputs.TryGetValue(parameter.ParameterName, out var value))
            {
                parameter.Value = Values.FromJson(session.Bound(value)) ?? DBNull.Value;
            }
        }
    }

    /// <summary>Every result set of <paramref name="reader"/>, read whole; the reader is left at its end, not closed.</summary>
    public static List<RecordedResult> ReadAll(DbDataReader reader)
    {
        var results = new List<RecordedResult>();
        do
        {
            var columns = Enumerable.Range(0, reader.FieldCount)
                .Select(i => new RecordedColumn(reader.GetName(i), FieldType(reader, i), TypeName(reader.GetDataTypeName(i))))
                .ToList();
            var rows = new List<IReadOnlyList<JsonNode?>>();
            while (reader.Read())
            {
                rows.Add([.. Enumerable.Range(0, reader.FieldCount).Select(i => Values.ToJson(Value(reader, i, columns[i])))]);
            }

            results.Add(new RecordedResult(columns, rows));
        }
        while (reader.NextResult());

        return results;
    }

    /// <summary>An error a command raised, recorded with what its type needs to be raised again; null for one that isn't the provider's.</summary>
    public static RecordedError? Recorded(Exception exception)
    {
        if (exception is not DbException error)
        {
            return null;
        }

        var type = error.GetType();
        return new RecordedError(
            type.FullName!,
            LoggingDbCommand.ErrorNumber(error),
            Convert.ToByte(type.GetProperty("State")?.GetValue(error) ?? (byte)0, CultureInfo.InvariantCulture),
            Convert.ToByte(type.GetProperty("Class")?.GetValue(error) ?? (byte)0, CultureInfo.InvariantCulture),
            error.Message,
            type.GetProperty("Server")?.GetValue(error) as string);
    }

    /// <summary>
    /// The provider's own exception for a recorded error. Throws <see cref="ReplayMissException"/> when its type can't be
    /// made: the test then needs the database, rather than catching another exception than the app does.
    /// </summary>
    public static Exception Raise(RecordedError error)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(error.Type, throwOnError: false))
            .FirstOrDefault(found => found is not null)
            ?? throw new ReplayMissException($"The recorded error {error.Type} {error.Number} can't be raised: its provider isn't loaded.");

        try
        {
            if (type.Name == "SqlException")
            {
                return SqlException(type, error);
            }

            // SqliteException(string message, int errorCode).
            if (type.GetConstructor([typeof(string), typeof(int)]) is { } plain)
            {
                return (Exception)plain.Invoke([error.Message, error.Number]);
            }
        }
        catch (Exception failure) when (failure is not ReplayMissException)
        {
            throw new ReplayMissException($"The recorded error {error.Type} {error.Number} can't be raised: {failure.Message}");
        }

        throw new ReplayMissException($"The recorded error {error.Type} {error.Number} can't be raised: no way to make one.");
    }

    // Either SqlClient's: an SqlError by its constructor's parameter names, in an SqlErrorCollection, through CreateException.
    private static Exception SqlException(Type exceptionType, RecordedError error)
    {
        var assembly = exceptionType.Assembly;
        var errorType = assembly.GetType(exceptionType.Namespace + ".SqlError")!;
        var collectionType = assembly.GetType(exceptionType.Namespace + ".SqlErrorCollection")!;
        var constructor = errorType.GetConstructors(Any).OrderByDescending(c => c.GetParameters().Length).First();
        var sqlError = constructor.Invoke([.. constructor.GetParameters().Select(parameter => parameter.Name switch
        {
            "infoNumber" or "number" => error.Number,
            "errorState" or "state" => error.State,
            "errorClass" or "class" => error.Class,
            "errorMessage" or "message" => error.Message,
            "server" => error.Server ?? string.Empty,
            "procedure" => string.Empty,
            _ => Default(parameter.ParameterType),
        })]);
        var errors = Activator.CreateInstance(collectionType, nonPublic: true)!;
        collectionType.GetMethod("Add", Any, null, [errorType], null)!.Invoke(errors, [sqlError]);
        var create = exceptionType.GetMethods(Any)
            .Where(method => method.Name == "CreateException")
            .Where(method => method.GetParameters() is { Length: >= 2 } parameters && parameters[0].ParameterType == collectionType && parameters[1].ParameterType == typeof(string))
            .OrderBy(method => method.GetParameters().Length)
            .First();
        return (Exception)create.Invoke(null, [.. create.GetParameters().Select((parameter, i) => i switch
        {
            0 => errors,
            1 => "16.0",
            _ => parameter.HasDefaultValue ? parameter.DefaultValue : Default(parameter.ParameterType),
        })])!;
    }

    // A UDT's name comes as database.schema.type (TestDb_w4.sys.geography): without the database, so a recording doesn't
    // depend on which database it was made on.
    private static string TypeName(string name) => name.Split('.') is { Length: 3 } parts ? parts[1] + "." + parts[2] : name;

    private static string FieldType(DbDataReader reader, int ordinal)
    {
        try
        {
            // A UDT (geography, hierarchyid) whose CLR type is not loaded here has no type: SqlClient answers null.
            return reader.GetFieldType(ordinal)?.FullName ?? typeof(SqlBytes).FullName!;
        }
        catch (Exception error) when (error is FileNotFoundException or FileLoadException or TypeLoadException or InvalidCastException)
        {
            // A provider that throws instead for such a type: kept as its bytes too. Any other provider error would
            // record a wrong column type, so it is thrown (review 05, finding 12).
            return typeof(SqlBytes).FullName!;
        }
    }

    private static object? Value(DbDataReader reader, int ordinal, RecordedColumn column)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var udt = column.ClrType == typeof(SqlBytes).FullName
            || column.DataTypeName.EndsWith("geography", StringComparison.OrdinalIgnoreCase)
            || column.DataTypeName.EndsWith("geometry", StringComparison.OrdinalIgnoreCase)
            || column.DataTypeName.EndsWith("hierarchyid", StringComparison.OrdinalIgnoreCase);
        if (udt && reader.GetType().GetMethod("GetSqlBytes", [typeof(int)]) is { } bytes)
        {
            return bytes.Invoke(reader, [ordinal]);
        }

        return reader.GetValue(ordinal);
    }

    private static object? Default(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;
}
