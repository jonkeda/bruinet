using System.Collections;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Globalization;

namespace Bruinet.Mocking.Data.Replay;

/// <summary>
/// A reader over recorded result sets: what the app reads, in record mode as in replay, so both read the same way. Code
/// typed to a provider's own reader (<c>SqlDataReader</c>) can't take it; EF's SQL Server spatial mapping is the one such
/// case handled, by its own package.
/// </summary>
public sealed class ReplayDataReader(IReadOnlyList<RecordedResult> results, int recordsAffected) : DbDataReader
{
    private int _set;
    private int _row = -1;
    private bool _closed;

    private RecordedResult Current => results[_set];

    public override int FieldCount => _set < results.Count ? Current.Columns.Count : 0;

    public override int RecordsAffected => recordsAffected;

    public override bool HasRows => _set < results.Count && Current.Rows.Count > 0;

    public override bool IsClosed => _closed;

    public override int Depth => 0;

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override bool Read() => _set < results.Count && ++_row < Current.Rows.Count;

    public override bool NextResult()
    {
        _set++;
        _row = -1;
        return _set < results.Count;
    }

    public override void Close() => _closed = true;

    public override string GetName(int ordinal) => Current.Columns[ordinal].Name;

    public override int GetOrdinal(string name)
    {
        for (var i = 0; i < Current.Columns.Count; i++)
        {
            if (string.Equals(Current.Columns[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        // DbDataReader.GetOrdinal's contract: an unknown name throws IndexOutOfRangeException.
#pragma warning disable CA2201, MA0012
        throw new IndexOutOfRangeException(name);
#pragma warning restore CA2201, MA0012
    }

    public override string GetDataTypeName(int ordinal) => Current.Columns[ordinal].DataTypeName;

    public override Type GetFieldType(int ordinal) => Resolve(Current.Columns[ordinal].ClrType);

    public override object GetValue(int ordinal) => Values.FromJson(Current.Rows[_row][ordinal]) ?? DBNull.Value;

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }

        return count;
    }

    public override bool IsDBNull(int ordinal) => Current.Rows[_row][ordinal] is null;

    public override T GetFieldValue<T>(int ordinal)
    {
        var value = GetValue(ordinal);
        if (value is T typed)
        {
            return typed;
        }

        if (value is byte[] bytes && typeof(T) == typeof(SqlBytes))
        {
            return (T)(object)new SqlBytes(bytes);
        }

        if (value is SqlBytes sqlBytes && typeof(T) == typeof(byte[]))
        {
            return (T)(object)sqlBytes.Value;
        }

        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (target == typeof(Guid) && value is string guid)
        {
            return (T)(object)Guid.Parse(guid);
        }

        if (target == typeof(DateTimeOffset) && value is DateTime moment)
        {
            return (T)(object)new DateTimeOffset(moment);
        }

        return (T)Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
    }

    public override bool GetBoolean(int ordinal) => GetFieldValue<bool>(ordinal);

    public override byte GetByte(int ordinal) => GetFieldValue<byte>(ordinal);

    public override char GetChar(int ordinal) => GetFieldValue<string>(ordinal)[0];

    public override DateTime GetDateTime(int ordinal) => GetFieldValue<DateTime>(ordinal);

    public override decimal GetDecimal(int ordinal) => GetFieldValue<decimal>(ordinal);

    public override double GetDouble(int ordinal) => GetFieldValue<double>(ordinal);

    public override float GetFloat(int ordinal) => GetFieldValue<float>(ordinal);

    public override Guid GetGuid(int ordinal) => GetFieldValue<Guid>(ordinal);

    public override short GetInt16(int ordinal) => GetFieldValue<short>(ordinal);

    public override int GetInt32(int ordinal) => GetFieldValue<int>(ordinal);

    public override long GetInt64(int ordinal) => GetFieldValue<long>(ordinal);

    public override string GetString(int ordinal) => GetFieldValue<string>(ordinal);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
    {
        var bytes = GetFieldValue<byte[]>(ordinal);
        if (buffer is null)
        {
            return bytes.Length;
        }

        var count = (int)Math.Min(length, bytes.Length - dataOffset);
        Array.Copy(bytes, dataOffset, buffer, bufferOffset, count);
        return count;
    }

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
    {
        var text = GetString(ordinal);
        if (buffer is null)
        {
            return text.Length;
        }

        var count = (int)Math.Min(length, text.Length - dataOffset);
        text.CopyTo((int)dataOffset, buffer, bufferOffset, count);
        return count;
    }

    /// <summary>A spatial or other UDT value's bytes, as SqlClient's own reader gives them.</summary>
    public SqlBytes GetSqlBytes(int ordinal) => GetFieldValue<SqlBytes>(ordinal);

    public override IEnumerator GetEnumerator() => new DbEnumerator(this);

    private static Type Resolve(string name)
        => Type.GetType(name) ?? typeof(SqlBytes).Assembly.GetType(name) ?? typeof(object);
}
