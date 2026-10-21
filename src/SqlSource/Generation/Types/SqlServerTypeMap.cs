using System;
using System.Collections.Generic;
using SqlSource.Settings;
using SqlSource.Snapshot;

namespace SqlSource.Generation.Types;

/// <summary>The type override's answer for the SQL Server type of <paramref name="key" />, or null.</summary>
internal delegate TypeMapping? SqlServerOverrideOf(string key);

/// <summary>
/// The base map: the C# type SqlClient boxes for each SQL Server type it reads, under the column of a project's
/// driver.  Each row is what SqlClient 6.1.7 and 7.1.1 give for <c>GetValue</c> against SQL Server 2025, not what
/// their documentation implies.  No row differs between the two, so a row has one C# type.
/// </summary>
internal static class SqlServerTypeMap
{
    private const string SystemSchema = "sys";
    private const string StringKeyword = "string";
    private const string DateTime = "global::System.DateTime";
    private const string SqlServerTypesNamespace = "Microsoft.SqlServer.Types.";

    private static readonly Dictionary<string, Row> Rows = BuildRows();

    /// <summary>The keys of the map: every type name it has a row for.</summary>
    public static IEnumerable<string> Keys => Rows.Keys;

    /// <summary>
    /// The C# type of <paramref name="type" /> under <paramref name="column" />.  At each level,
    /// <paramref name="overrideOf" /> is asked first.  A type with a user type is asked by the user type's key, and an
    /// alias type then maps as its base; a CLR type of <c>sys</c> is a row by its name, and any other is unreadable.
    /// Never throws.
    /// </summary>
    public static TypeMapping Map(SqlServerType type, SqlClientColumn column, SqlServerOverrideOf? overrideOf = null)
    {
        if (UserTypeOf(type) is { } userType)
        {
            var userKey = KeyOf(type);
            if (overrideOf?.Invoke(userKey) is { } overriddenUser)
            {
                return overriddenUser;
            }

            if (string.IsNullOrEmpty(userType.AssemblyQualifiedName))
            {
                return Map(type with { UserType = null }, column, overrideOf);
            }

            return IsSystemSchema(userType) && Rows.TryGetValue(userType.Name!, out var clrRow)
                ? clrRow.Under(column)
                : TypeMapping.Failed(TypeMappingKind.Unreadable, userKey);
        }

        var key = KeyOf(type);
        if (overrideOf?.Invoke(key) is { } overridden)
        {
            return overridden;
        }

        if (string.IsNullOrEmpty(type.Name))
        {
            return TypeMapping.Failed(TypeMappingKind.NotWrittenByTheTool, key);
        }

        return Rows.TryGetValue(BaseName(type.Name), out var row)
            ? row.Under(column)
            : TypeMapping.Failed(TypeMappingKind.Unreadable, key);
    }

    /// <summary>
    /// The key of <paramref name="type" />, written as the type override reads it: the bare name of a user type of
    /// <c>sys</c>, <c>Schema.Name</c> for any other user type, and the base name of the type otherwise, each part in
    /// quotes when it needs them, as <see cref="TypeKeys.WritePart" /> writes it: <c>dbo."Email"</c>.
    /// </summary>
    public static string KeyOf(SqlServerType type)
    {
        if (UserTypeOf(type) is not { } userType)
        {
            return TypeKeys.WritePart(BaseName(type.Name));
        }

        return IsSystemSchema(userType)
            ? TypeKeys.WritePart(userType.Name!)
            : TypeKeys.WritePart(userType.Schema ?? string.Empty) + "." + TypeKeys.WritePart(userType.Name!);
    }

    /// <summary>
    /// <paramref name="name" /> cut before its first <c>(</c>, which drops its facets: <c>nvarchar(50)</c> is
    /// <c>nvarchar</c>.
    /// </summary>
    public static string BaseName(string name)
    {
        var parenthesis = name.IndexOf('(');
        return parenthesis < 0 ? name : name.Substring(0, parenthesis);
    }

    // A sidecar edited by hand may leave the name out; such a user type is no user type.
    private static SqlServerUserType? UserTypeOf(SqlServerType type) =>
        string.IsNullOrEmpty(type.UserType?.Name) ? null : type.UserType;

    private static bool IsSystemSchema(SqlServerUserType userType) =>
        string.Equals(userType.Schema, SystemSchema, StringComparison.Ordinal);

    private static Dictionary<string, Row> BuildRows()
    {
        var rows = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);

        void Add(Func<string, Row> row, params string[] keys)
        {
            foreach (var key in keys)
            {
                rows.Add(key, row(key));
            }
        }

        Add(key => Row.Of(key, "long", true), "bigint");
        Add(key => Row.Of(key, "int", true), "int");
        Add(key => Row.Of(key, "short", true), "smallint");
        Add(key => Row.Of(key, "byte", true), "tinyint");
        Add(key => Row.Of(key, "bool", true), "bit");
        Add(key => Row.Of(key, "decimal", true), "decimal", "numeric", "money", "smallmoney");
        Add(key => Row.Of(key, "double", true), "float");
        Add(key => Row.Of(key, "float", true), "real");
        Add(key => Row.Of(key, DateTime, true), "date", "datetime", "datetime2", "smalldatetime");
        Add(key => Row.Of(key, "global::System.TimeSpan", true), "time");
        Add(key => Row.Of(key, "global::System.DateTimeOffset", true), "datetimeoffset");
        Add(
            key => Row.Of(key, StringKeyword, false),
            "char",
            "varchar",
            "text",
            "nchar",
            "nvarchar",
            "ntext",
            "xml",
            "json",
            "sysname"
        );
        Add(key => Row.Of(key, "byte[]", false), "binary", "varbinary", "image", "timestamp");
        Add(key => Row.Of(key, "global::System.Guid", true), "uniqueidentifier");
        Add(key => Row.Of(key, "object", false), "sql_variant");
        Add(key => Row.OfSqlClient(key, "global::Microsoft.Data.SqlTypes.SqlVector<float>", true), "vector");
        Add(key => Row.NeedsTypes(key, "SqlHierarchyId"), "hierarchyid");
        Add(key => Row.NeedsTypes(key, "SqlGeography"), "geography");
        Add(key => Row.NeedsTypes(key, "SqlGeometry"), "geometry");
        return rows;
    }

    /// <summary>A row of the map.</summary>
    /// <param name="Key">The row's name.</param>
    /// <param name="Text">The C# type, or null when SqlClient cannot read the type without a package.</param>
    /// <param name="IsValueType">Whether the C# type is a value type.</param>
    /// <param name="IsOfSqlClient">Whether the C# type is SqlClient's own, so a project without it has none.</param>
    /// <param name="Package">The package's C# type for a type that SqlClient reads only with it, or null.</param>
    private sealed record Row(string Key, string? Text, bool IsValueType, bool IsOfSqlClient, string? Package)
    {
        public static Row Of(string key, string text, bool isValueType) => new(key, text, isValueType, false, null);

        public static Row OfSqlClient(string key, string text, bool isValueType) =>
            new(key, text, isValueType, true, null);

        public static Row NeedsTypes(string key, string package) =>
            new(key, null, false, false, SqlServerTypesNamespace + package);

        public TypeMapping Under(SqlClientColumn column)
        {
            if (Package is not null)
            {
                return TypeMapping.Failed(TypeMappingKind.NeedsSqlServerTypes, Key, Package);
            }

            return column == SqlClientColumn.None && IsOfSqlClient
                ? TypeMapping.Failed(TypeMappingKind.NeedsDriver, Key)
                : TypeMapping.Mapped(Key, Text!, IsValueType);
        }
    }
}
