using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Snapshot.Reading;

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

    // The keys of a parameter's own type, here and for each engine below: a type has a bind name only there.  Each
    // reader checks its keys against the array it read them by, so the place of the bind name in it is free.
    private static readonly string[] PostgresParameterKeys = [.. PostgresKeys, SidecarKeys.BindName];

    private static readonly string[] SqlServerKeys =
    [
        SidecarKeys.Name,
        SidecarKeys.MaxLength,
        SidecarKeys.Precision,
        SidecarKeys.Scale,
        SidecarKeys.UserType,
    ];

    private static readonly string[] SqlServerParameterKeys = [.. SqlServerKeys, SidecarKeys.BindName];

    private static readonly string[] UserTypeKeys =
    [
        SidecarKeys.Schema,
        SidecarKeys.Name,
        SidecarKeys.AssemblyQualifiedName,
    ];

    private static readonly string[] OtherKeys = [SidecarKeys.Name];

    private static readonly string[] OtherParameterKeys = [.. OtherKeys, SidecarKeys.BindName];

    /// <summary>
    /// Reads the value of a <c>type</c> key under <paramref name="engine" />.  Null for <c>null</c> where
    /// <paramref name="orNull" /> allows it, and after an error.  <paramref name="isParameter" /> says that the type
    /// is a parameter's own, the only place where <c>bindName</c> is a key: anywhere else it is an unknown key, which
    /// is skipped with its value.
    /// </summary>
    public static SidecarType? Read(SidecarCursor cursor, string engine, bool orNull, bool isParameter)
    {
        if (!cursor.BeginObject(SidecarKeys.Type, orNull, out var brace))
        {
            return null;
        }

        return engine switch
        {
            SidecarValues.Engine.Postgres => ReadPostgres(cursor, brace, isParameter),
            SidecarValues.Engine.SqlServer => ReadSqlServer(cursor, brace, isParameter),
            _ => ReadOther(cursor, brace, isParameter),
        };
    }

    // A type inside a type is as deep as the text nests, which is SidecarTokenizer.MaxDepth levels at most.
    private static PostgresType? ReadPostgres(SidecarCursor cursor, TextSpan brace, bool isParameter)
    {
        var keys = isParameter ? PostgresParameterKeys : PostgresKeys;
        var seen = 0;
        string? name = null;
        string? bindName = null;
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
        for (var key = cursor.NextKey(keys, ref seen); key is not null; key = cursor.NextKey(keys, ref seen))
        {
            switch (key)
            {
                case SidecarKeys.Name:
                    name = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.BindName:
                    bindName = cursor.ReadString(key, orNull: false);
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
                case SidecarKeys.Subtype:
                    subtype = ReadNested(cursor, key);
                    break;
                default:
                    cursor.SkipValue();
                    break;
            }
        }

        cursor.Require(keys, seen, brace, SidecarKeys.Name);
        cursor.Require(keys, seen, brace, SidecarKeys.Kind);
        cursor.Require(keys, seen, brace, SidecarKeys.Schema);
        cursor.Require(keys, seen, brace, SidecarKeys.InternalName);

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
        )
        {
            BindName = bindName,
        };
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
        cursor.BeginObject(key, orNull: true, out var brace) ? ReadPostgres(cursor, brace, isParameter: false) : null;

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

    private static SqlServerType? ReadSqlServer(SidecarCursor cursor, TextSpan brace, bool isParameter)
    {
        var keys = isParameter ? SqlServerParameterKeys : SqlServerKeys;
        var seen = 0;
        string? name = null;
        string? bindName = null;
        int? maxLength = null;
        int? precision = null;
        int? scale = null;
        SqlServerUserType? userType = null;
        for (var key = cursor.NextKey(keys, ref seen); key is not null; key = cursor.NextKey(keys, ref seen))
        {
            switch (key)
            {
                case SidecarKeys.Name:
                    name = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.BindName:
                    bindName = cursor.ReadString(key, orNull: false);
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
                case SidecarKeys.UserType:
                    userType = ReadUserType(cursor);
                    break;
                default:
                    cursor.SkipValue();
                    break;
            }
        }

        cursor.Require(keys, seen, brace, SidecarKeys.Name);
        cursor.Require(keys, seen, brace, SidecarKeys.MaxLength);
        cursor.Require(keys, seen, brace, SidecarKeys.Precision);
        cursor.Require(keys, seen, brace, SidecarKeys.Scale);
        return cursor.Error is null && name is not null
            ? new SqlServerType(
                name,
                maxLength.GetValueOrDefault(),
                precision.GetValueOrDefault(),
                scale.GetValueOrDefault(),
                userType
            )
            {
                BindName = bindName,
            }
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
                case SidecarKeys.AssemblyQualifiedName:
                    assemblyQualifiedName = cursor.ReadString(key, orNull: true);
                    break;
                default:
                    cursor.SkipValue();
                    break;
            }
        }

        return new SqlServerUserType(schema, name, assemblyQualifiedName);
    }

    private static OtherEngineType? ReadOther(SidecarCursor cursor, TextSpan brace, bool isParameter)
    {
        var keys = isParameter ? OtherParameterKeys : OtherKeys;
        var seen = 0;
        string? name = null;
        string? bindName = null;
        for (var key = cursor.NextKey(keys, ref seen); key is not null; key = cursor.NextKey(keys, ref seen))
        {
            switch (key)
            {
                case SidecarKeys.Name:
                    name = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.BindName:
                    bindName = cursor.ReadString(key, orNull: false);
                    break;
                default:
                    cursor.SkipValue();
                    break;
            }
        }

        cursor.Require(keys, seen, brace, SidecarKeys.Name);
        return cursor.Error is null && name is not null ? new OtherEngineType(name) { BindName = bindName } : null;
    }
}
