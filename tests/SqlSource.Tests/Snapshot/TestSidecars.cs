using System;
using System.Collections.Immutable;
using SqlSource.Snapshot;

namespace SqlSource.Tests.Snapshot;

// Builds the records of a sidecar with the values a test does not care about filled in.
internal static class TestSidecars
{
    public static EquatableArray<T> Of<T>(params T[] items)
        where T : IEquatable<T> => new(ImmutableArray.Create(items));

    public static PostgresType Postgres(
        string name = "integer",
        string kind = SidecarValues.Kind.Base,
        string schema = "pg_catalog",
        string internalName = "int4",
        int? length = null,
        int? precision = null,
        int? scale = null,
        PostgresType? element = null,
        PostgresType? baseType = null,
        string[]? labels = null,
        PostgresType? subtype = null
    ) =>
        new(
            name,
            kind,
            schema,
            internalName,
            length,
            precision,
            scale,
            element,
            baseType,
            labels is null ? null : Of(labels),
            subtype
        );

    public static SqlServerType SqlServer(
        string name = "int",
        int maxLength = 4,
        int precision = 10,
        int scale = 0,
        SqlServerUserType? userType = null
    ) => new(name, maxLength, precision, scale, userType);

    public static SidecarParameter Parameter(
        int ordinal,
        string name,
        SidecarType? type,
        bool? nullable = null,
        string? typeSource = SidecarValues.TypeSource.Inferred
    ) => new(name, ordinal, type, nullable, type is null ? null : typeSource);

    public static SidecarColumn Column(
        int ordinal,
        string name,
        SidecarType type,
        bool? nullable = false,
        string? nullableSource = SidecarValues.NullableSource.Catalog,
        SidecarOrigin? origin = null,
        bool? identity = null,
        bool? computed = null
    ) => new(ordinal, name, type, nullable, nullableSource, origin, identity, computed);

    // An entry with rows has one column unless it is given some; an entry without rows has no columns, no table
    // and no provenance.
    public static SidecarEntry Entry(
        string name = "Q",
        string hash = "h",
        string engine = SidecarValues.Engine.Postgres,
        string? database = "app",
        string? serverVersion = "16.4",
        SidecarResultKind resultKind = SidecarResultKind.Rows,
        SidecarTable? matchesTable = null,
        string? plan = SidecarValues.Plan.NotNeeded,
        string? tableMatch = SidecarValues.TableMatch.NoOrigin,
        SidecarParameter[]? parameters = null,
        SidecarColumn[]? columns = null
    )
    {
        var rows = resultKind == SidecarResultKind.Rows;
        SidecarType type = engine == SidecarValues.Engine.SqlServer ? SqlServer() : Postgres();
        return new SidecarEntry(
            name,
            default,
            hash,
            engine,
            database,
            serverVersion,
            resultKind,
            rows ? matchesTable : null,
            rows ? plan : null,
            rows ? tableMatch : null,
            Of(parameters ?? []),
            rows ? Of(columns ?? [Column(0, "id", type)]) : null
        );
    }

    public static Sidecar SidecarOf(params SidecarEntry[] entries) => new(SidecarFormat.Version, "1.2.3", Of(entries));
}
