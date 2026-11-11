using System.Data.Common;
using System.Data.SqlTypes;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace Bruinet.Mocking.Data.EntityFrameworkCore.SqlServer;

#pragma warning disable EF1001 // SQL Server's spatial mapping is internal API: the hook a provider-only reader method needs.
/// <summary>
/// How EF reads spatial columns from a <see cref="Bruinet.Mocking.Data.Replay.ReplayDataReader"/>. SQL Server's geometry mapping reads with
/// <c>SqlDataReader.GetSqlBytes</c>, a cast EF compiles into the query's shaper, so any other reader fails. This swaps
/// the mapping for one that reads with <see cref="DbDataReader.GetFieldValue{T}"/> of <see cref="SqlBytes"/>; the
/// value converter after it is SQL Server's own.
/// </summary>
public static class SpatialReplay
{
    public static DbContextOptionsBuilder UseSpatialReplay(this DbContextOptionsBuilder options)
        => options.ReplaceService<IRelationalTypeMappingSourcePlugin, SqlServerNetTopologySuiteTypeMappingSourcePlugin, ReplayGeometryPlugin>();
}

/// <summary>SQL Server's spatial plugin, each geometry mapping it finds wrapped in <see cref="ReplayGeometryMapping{T}"/>.</summary>
public sealed class ReplayGeometryPlugin(NtsGeometryServices geometryServices) : SqlServerNetTopologySuiteTypeMappingSourcePlugin(geometryServices)
{
    public override RelationalTypeMapping? FindMapping(in RelationalTypeMappingInfo mappingInfo)
    {
        var mapping = base.FindMapping(mappingInfo);
        if (mapping?.GetType() is not { IsGenericType: true } type || type.GetGenericTypeDefinition() != typeof(SqlServerGeometryTypeMapping<>))
        {
            return mapping;
        }

        var geometry = type.GetGenericArguments()[0];
        return (RelationalTypeMapping)typeof(ReplayGeometryMapping<>).MakeGenericType(geometry)
            .GetMethod(nameof(ReplayGeometryMapping<Geometry>.From))!
            .Invoke(null, [mapping])!;
    }
}

/// <summary>SQL Server's geometry mapping, reading through <see cref="DbDataReader"/> instead of <c>SqlDataReader</c>.</summary>
public sealed class ReplayGeometryMapping<TGeometry> : SqlServerGeometryTypeMapping<TGeometry>
    where TGeometry : Geometry
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private readonly ValueConverter<TGeometry, SqlBytes> _spatial;

    private ReplayGeometryMapping(RelationalTypeMappingParameters parameters, ValueConverter<TGeometry, SqlBytes> spatial)
        : base(parameters, spatial)
        => _spatial = spatial;

    /// <summary>The same mapping as <paramref name="mapping"/>: its parameters and spatial converter, read by reflection.</summary>
    public static ReplayGeometryMapping<TGeometry> From(SqlServerGeometryTypeMapping<TGeometry> mapping)
    {
        var parameters = (RelationalTypeMappingParameters)typeof(RelationalTypeMapping).GetProperty("Parameters", Hidden | BindingFlags.DeclaredOnly)!.GetValue(mapping)!;
        var spatial = Members(mapping.GetType())
            .Select(member => member switch
            {
                FieldInfo field => field.GetValue(mapping),
                PropertyInfo property when property.GetIndexParameters().Length == 0 => property.GetValue(mapping),
                _ => null,
            })
            .OfType<ValueConverter<TGeometry, SqlBytes>>()
            .First();
        return new ReplayGeometryMapping<TGeometry>(parameters, spatial);
    }

    public override MethodInfo GetDataReaderMethod()
        => typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetFieldValue))!.MakeGenericMethod(typeof(SqlBytes));

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters) => new ReplayGeometryMapping<TGeometry>(parameters, _spatial);

    private static IEnumerable<MemberInfo> Members(Type? type)
    {
        for (; type is not null; type = type.BaseType)
        {
            foreach (var member in type.GetFields(Hidden | BindingFlags.Public | BindingFlags.DeclaredOnly).Cast<MemberInfo>()
                .Concat(type.GetProperties(Hidden | BindingFlags.Public | BindingFlags.DeclaredOnly)))
            {
                yield return member;
            }
        }
    }
}
#pragma warning restore EF1001
