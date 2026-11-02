using System;
using System.Data.SqlClient;
using Xunit;

namespace Bruinet.Mocking.Data.Net48.Tests;

/// <summary>A test that needs the local SQL Server (<c>BRUINET_SQL_SERVER</c>, else <c>.</c>); skipped when none answers.</summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    private static readonly Lazy<string?> Missing = new(() =>
    {
        var server = Environment.GetEnvironmentVariable("BRUINET_SQL_SERVER") is { Length: > 0 } name ? name : ".";
        try
        {
            using var connection = new SqlConnection($"Server={server};Database=master;Integrated Security=True;Connect Timeout=3");
            connection.Open();
            return null;
        }
        catch (Exception error)
        {
            return $"No SQL Server at {server}: {error.Message}";
        }
    });

    public SqlServerFactAttribute()
    {
        if (Missing.Value is { } reason)
        {
            Skip = reason;
        }
    }
}
