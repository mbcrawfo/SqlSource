using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Snapshot;

/// <summary>
/// Reads a type object.  The entry's engine picks the shape, and each shape says which keys it cannot do without.
/// </summary>
internal static class SidecarTypeReader
{
    private static readonly string[] PostgresKeys =
    [
        SidecarKeys.Name,
        SidecarKeys.Kind,
        SidecarKeys.Schema,
        SidecarKeys.InternalName,
        SidecarKeys.Length,
        SidecarKeys.Precision,
        SidecarKeys.Scale,
        SidecarKeys.Element,
        SidecarKeys.Base,
        SidecarKeys.Labels,
        SidecarKeys.Subtype,
    ];

    private static readonly string[] SqlServerKeys =
    [
        SidecarKeys.Name,
        SidecarKeys.MaxLength,
        SidecarKeys.Precision,
        SidecarKeys.Scale,
        SidecarKeys.UserType,
    ];

    private static readonly string[] UserTypeKeys =
    [
        SidecarKeys.Schema,
        SidecarKeys.Name,
        SidecarKeys.AssemblyQualifiedName,
    ];

    private static readonly string[] OtherKeys = [SidecarKeys.Name];

    /// <summary>
    /// Reads the value of a <c>type</c> key under <paramref name="engine" />.  Null for <c>null</c> where
    /// <paramref name="orNull" /> allows it, and after an error.
    /// </summary>
    public static SidecarType? Read(SidecarCursor cursor, string engine, bool orNull)
    {
        if (!cursor.BeginObject(SidecarKeys.Type, orNull, out var brace))
        {
            return null;
        }

        return engine switch
        {
            SidecarValues.Engine.Postgres => ReadPostgres(cursor, brace),
            SidecarValues.Engine.SqlServer => ReadSqlServer(cursor, brace),
            _ => ReadOther(cursor, brace),
        };
    }

    // A type inside a type is as deep as the text nests, and the text nests 64 levels at most.
    private static PostgresType? ReadPostgres(SidecarCursor cursor, TextSpan brace)
    {
        var seen = 0;
        string? name = null;
        string? kind = null;
        string? schema = null;
        string? internalName = null;
        int? length = null;
        int? precision = null;
        int? scale = null;
        PostgresType? element = null;
        PostgresType? baseType = null;
        EquatableArray<string>? labels = null;
        PostgresType? subtype = null;
        for (
            var key = cursor.NextKey(PostgresKeys, ref seen);
            key is not null;
            key = cursor.NextKey(PostgresKeys, ref seen)
        )
        {
            switch (key)
            {
                case SidecarKeys.Name:
                    name = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.Kind:
                    kind = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.Schema:
                    schema = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.InternalName:
                    internalName = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.Length:
                    length = cursor.ReadInt32(key, orNull: true);
                    break;
                case SidecarKeys.Precision:
                    precision = cursor.ReadInt32(key, orNull: true);
                    break;
                case SidecarKeys.Scale:
                    scale = cursor.ReadInt32(key, orNull: true);
                    break;
                case SidecarKeys.Element:
                    element = ReadNested(cursor, key);
                    break;
                case SidecarKeys.Base:
                    baseType = ReadNested(cursor, key);
                    break;
                case SidecarKeys.Labels:
                    labels = ReadLabels(cursor);
                    break;
                default:
                    subtype = ReadNested(cursor, key);
                    break;
            }
        }

        cursor.Require(PostgresKeys, seen, brace, SidecarKeys.Name);
        cursor.Require(PostgresKeys, seen, brace, SidecarKeys.Kind);
        cursor.Require(PostgresKeys, seen, brace, SidecarKeys.Schema);
        cursor.Require(PostgresKeys, seen, brace, SidecarKeys.InternalName);

        if (MissingNestedKey(kind, element, baseType, labels, subtype) is { } missing)
        {
            cursor.Fail(SidecarErrorKind.MissingKey, brace, missing);
        }

        if (cursor.Error is not null || name is null || kind is null || schema is null || internalName is null)
        {
            return null;
        }

        return new PostgresType(
            name,
            kind,
            schema,
            internalName,
            length,
            precision,
            scale,
            element,
            baseType,
            labels,
            subtype
        );
    }

    // The key of the nested type that a kind names and the object lacks.  What a kind names is known only once the
    // whole object is read, so one given as null is missing.  A kind the reader does not know names nothing.
    private static string? MissingNestedKey(
        string? kind,
        PostgresType? element,
        PostgresType? baseType,
        EquatableArray<string>? labels,
        PostgresType? subtype
    ) =>
        kind switch
        {
            SidecarValues.Kind.Array when element is null => SidecarKeys.Element,
            SidecarValues.Kind.Domain when baseType is null => SidecarKeys.Base,
            SidecarValues.Kind.Enum when labels is null => SidecarKeys.Labels,
            SidecarValues.Kind.Range or SidecarValues.Kind.Multirange when subtype is null => SidecarKeys.Subtype,
            _ => null,
        };

    private static PostgresType? ReadNested(SidecarCursor cursor, string key) =>
        cursor.BeginObject(key, orNull: true, out var brace) ? ReadPostgres(cursor, brace) : null;

    private static EquatableArray<string>? ReadLabels(SidecarCursor cursor)
    {
        if (!cursor.BeginArray(SidecarKeys.Labels, orNull: true))
        {
            return null;
        }

        var labels = ImmutableArray.CreateBuilder<string>();
        while (cursor.NextElement(out var first))
        {
            if (first.Kind == SidecarTokenKind.String)
            {
                labels.Add(cursor.GetString(first));
            }
            else
            {
                cursor.WrongType(first, SidecarKeys.Labels);
            }
        }

        return new EquatableArray<string>(labels.ToImmutable());
    }

    private static SqlServerType? ReadSqlServer(SidecarCursor cursor, TextSpan brace)
    {
        var seen = 0;
        string? name = null;
        int? maxLength = null;
        int? precision = null;
        int? scale = null;
        SqlServerUserType? userType = null;
        for (
            var key = cursor.NextKey(SqlServerKeys, ref seen);
            key is not null;
            key = cursor.NextKey(SqlServerKeys, ref seen)
        )
        {
            switch (key)
            {
                case SidecarKeys.Name:
                    name = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.MaxLength:
                    maxLength = cursor.ReadInt32(key, orNull: false);
                    break;
                case SidecarKeys.Precision:
                    precision = cursor.ReadInt32(key, orNull: false);
                    break;
                case SidecarKeys.Scale:
                    scale = cursor.ReadInt32(key, orNull: false);
                    break;
                default:
                    userType = ReadUserType(cursor);
                    break;
            }
        }

        cursor.Require(SqlServerKeys, seen, brace, SidecarKeys.Name);
        cursor.Require(SqlServerKeys, seen, brace, SidecarKeys.MaxLength);
        cursor.Require(SqlServerKeys, seen, brace, SidecarKeys.Precision);
        cursor.Require(SqlServerKeys, seen, brace, SidecarKeys.Scale);
        return cursor.Error is null && name is not null
            ? new SqlServerType(
                name,
                maxLength.GetValueOrDefault(),
                precision.GetValueOrDefault(),
                scale.GetValueOrDefault(),
                userType
            )
            : null;
    }

    private static SqlServerUserType? ReadUserType(SidecarCursor cursor)
    {
        if (!cursor.BeginObject(SidecarKeys.UserType, orNull: true, out _))
        {
            return null;
        }

        var seen = 0;
        string? schema = null;
        string? name = null;
        string? assemblyQualifiedName = null;
        for (
            var key = cursor.NextKey(UserTypeKeys, ref seen);
            key is not null;
            key = cursor.NextKey(UserTypeKeys, ref seen)
        )
        {
            switch (key)
            {
                case SidecarKeys.Schema:
                    schema = cursor.ReadString(key, orNull: true);
                    break;
                case SidecarKeys.Name:
                    name = cursor.ReadString(key, orNull: true);
                    break;
                default:
                    assemblyQualifiedName = cursor.ReadString(key, orNull: true);
                    break;
            }
        }

        return new SqlServerUserType(schema, name, assemblyQualifiedName);
    }

    private static OtherEngineType? ReadOther(SidecarCursor cursor, TextSpan brace)
    {
        var seen = 0;
        string? name = null;
        while (cursor.NextKey(OtherKeys, ref seen) is not null)
        {
            name = cursor.ReadString(SidecarKeys.Name, orNull: false);
        }

        cursor.Require(OtherKeys, seen, brace, SidecarKeys.Name);
        return cursor.Error is null && name is not null ? new OtherEngineType(name) : null;
    }
}
