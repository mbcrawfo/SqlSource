using System;
using System.Collections.Generic;
using SqlSource.Generation.Types.Drivers;
using SqlSource.Settings.TypeOverrides;
using SqlSource.Snapshot;

namespace SqlSource.Generation.Types.Maps;

/// <summary>The type override's answer for the SQL Server type of <paramref name="key" />, or null.</summary>
internal delegate TypeMapping? SqlServerOverrideOf(string key);

/// <summary>
/// The base map: the C# type SqlClient boxes for each SQL Server type it reads, under the column of a project's
/// driver.  Each row is what SqlClient 6.1.7 and 7.1.1 give for <c>GetValue</c> against SQL Server 2025, not what
/// their documentation implies.  No row differs between the two, so a row has one C# type.
/// </summary>
internal static class SqlServerTypeMap
{
    private const string StringKeyword = "string";
    private const string DateTime = "global::System.DateTime";
    private const string SqlServerTypesNamespace = "Microsoft.SqlServer.Types.";

    private static readonly Dictionary<string, Row> Rows = BuildRows();

    // Every C# type a row gives, as it writes it, and DateOnly and TimeOnly, for Gives.
    private static readonly HashSet<string> Given = BuildGiven();

    /// <summary>The keys of the map: every type name it has a row for.</summary>
    public static IEnumerable<string> Keys => Rows.Keys;

    /// <summary>
    /// The C# type of <paramref name="type" /> under <paramref name="column" />.  At each level,
    /// <paramref name="overrideOf" /> is asked first, then <paramref name="libraries" />, the rows of the libraries
    /// that are on, or none when it is null, then the map's row.  A type with a user type is asked by the user type's
    /// key, and an alias type then maps as its base; a CLR type is asked of <paramref name="libraries" /> after the
    /// override, which has rows for the spatial types of <c>sys</c> alone, then a CLR type of <c>sys</c> is a row by
    /// its name, and any other is unreadable.  Never throws.
    /// </summary>
    public static TypeMapping Map(
        SqlServerType type,
        SqlClientColumn column,
        SqlServerOverrideOf? overrideOf = null,
        LibraryRows? libraries = null
    )
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
                return Map(type with { UserType = null }, column, overrideOf, libraries);
            }

            if (libraries?.Find(type) is { } clrFromLibrary)
            {
                return clrFromLibrary;
            }

            // Only the CLR types are rows by their name: an assembly-qualified sys.int is not int.
            return IsSystemSchema(userType) && Rows.TryGetValue(userType.Name!, out var clrRow) && clrRow.IsClr
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

        if (libraries?.Find(type) is { } fromLibrary)
        {
            return fromLibrary;
        }

        return Rows.TryGetValue(BaseName(type.Name), out var row)
            ? row.Under(column)
            : TypeMapping.Failed(TypeMappingKind.Unreadable, key);
    }

    /// <summary>
    /// The key of <paramref name="type" />, written as the type override reads it: the bare name of a user type of
    /// <c>sys</c>, <c>Schema.Name</c> for any other user type, and the base name of the type otherwise, each part in
    /// quotes when it needs them, as <see cref="TypeKeyText.WritePart" /> writes it: <c>dbo."Email"</c>.
    /// </summary>
    public static string KeyOf(SqlServerType type)
    {
        if (UserTypeOf(type) is not { } userType)
        {
            // A system type's name is case-insensitive: a row is keyed by its own name, so the key asked of the
            // override and the key of the mapping agree.
            var baseName = BaseName(type.Name);
            return Rows.TryGetValue(baseName, out var row) ? row.Key : TypeKeyText.WritePart(baseName);
        }

        return IsSystemSchema(userType)
            ? TypeKeyText.WritePart(userType.Name!)
            : TypeKeyText.WritePart(userType.Schema ?? string.Empty) + "." + TypeKeyText.WritePart(userType.Name!);
    }

    /// <summary>
    /// Whether SqlClient reads <paramref name="text" /> for some type: a row's C# type exactly,
    /// <c>System.DateOnly</c> or <c>System.TimeOnly</c>, or an array <c>X[]</c> of a type <c>X</c> it gives.  No row
    /// gives those two, since <c>GetValue</c> boxes a <c>date</c> or <c>time</c> as a <c>DateTime</c> or a
    /// <c>TimeSpan</c>, but <c>GetFieldValue&lt;T&gt;</c> reads them as either when asked, as the override
    /// <c>date=System.DateOnly</c> asks, so a converter between one and another type must not make them foreign.  A
    /// type it never gives, as <c>MyApp.Email</c> or a type of the package Microsoft.SqlServer.Types, is foreign,
    /// which a converter is needed for.
    /// </summary>
    public static bool Gives(string text) =>
        Given.Contains(text)
        || (text.EndsWith("[]", StringComparison.Ordinal) && Gives(text.Substring(0, text.Length - 2)));

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

    private static bool IsSystemSchema(SqlServerUserType userType) => SystemSchemas.IsSqlServer(userType.Schema);

    private static HashSet<string> BuildGiven()
    {
        var given = new HashSet<string>(StringComparer.Ordinal)
        {
            "global::System.DateOnly",
            "global::System.TimeOnly",
        };
        foreach (var row in Rows.Values)
        {
            if (row.Text is { } text)
            {
                _ = given.Add(text);
            }
        }

        return given;
    }

    private static Dictionary<string, Row> BuildRows()
    {
        var rows = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);

        void Add(Row row, params string[] keys)
        {
            foreach (var key in keys)
            {
                rows.Add(key, row with { Key = key });
            }
        }

        Add(Row.Same("long", true), "bigint");
        Add(Row.Same("int", true), "int");
        Add(Row.Same("short", true), "smallint");
        Add(Row.Same("byte", true), "tinyint");
        Add(Row.Same("bool", true), "bit");
        Add(Row.Same("decimal", true), "decimal", "numeric", "money", "smallmoney");
        Add(Row.Same("double", true), "float");
        Add(Row.Same("float", true), "real");
        Add(Row.Same(DateTime, true), "date", "datetime", "datetime2", "smalldatetime");
        Add(Row.Same("global::System.TimeSpan", true), "time");
        Add(Row.Same("global::System.DateTimeOffset", true), "datetimeoffset");
        Add(
            Row.Same(StringKeyword, false),
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
        Add(Row.Same("byte[]", false), "binary", "varbinary", "image", "timestamp");
        Add(Row.Same("global::System.Guid", true), "uniqueidentifier");
        Add(Row.Same("object", false), "sql_variant");
        Add(
            Row.OfSqlClient("global::Microsoft.Data.SqlTypes.SqlVector<float>", true, SqlClientColumn.SqlClient61),
            "vector"
        );
        Add(Row.NeedsTypes("SqlHierarchyId"), SqlServerNames.HierarchyId);
        Add(Row.NeedsTypes("SqlGeography"), SqlServerNames.Geography);
        Add(Row.NeedsTypes("SqlGeometry"), SqlServerNames.Geometry);
        return rows;
    }

    /// <summary>
    /// Whether a project whose SqlClient chooses <paramref name="column" /> lacks a type that SqlClient first has in
    /// the version of <paramref name="since" />: its column is of an older version, or there is no SqlClient.
    /// </summary>
    internal static bool Lacks(SqlClientColumn column, SqlClientColumn since) => Order(column) < Order(since);

    // The columns in the order of their versions, oldest first, no SqlClient before any.
    private static int Order(SqlClientColumn column) =>
        column switch
        {
            SqlClientColumn.None => 0,
            SqlClientColumn.OlderThan61 => 1,
            SqlClientColumn.SqlClient61 => 2,
            SqlClientColumn.SqlClient7 => 3,
            _ => 0,
        };

    /// <summary>A row of the map.</summary>
    /// <param name="Key">The row's name, set when it is added.</param>
    /// <param name="Text">The C# type, or null when SqlClient cannot read the type without a package.</param>
    /// <param name="IsValueType">Whether the C# type is a value type.</param>
    /// <param name="SqlClientSince">
    /// The column of the first version of SqlClient that has the C# type, when it is SqlClient's own, or null.  A
    /// project without SqlClient, or with an older one, has no such type, as <see cref="Lacks" /> decides.
    /// </param>
    /// <param name="Package">The package's C# type for a type that SqlClient reads only with it, or null.</param>
    private sealed record Row(
        string Key,
        string? Text,
        bool IsValueType,
        SqlClientColumn? SqlClientSince,
        string? Package
    )
    {
        // Whether SqlClient reads the type only with the package, which makes it a CLR type.
        public bool IsClr => Package is not null;

        public static Row Same(string text, bool isValueType) => new(string.Empty, text, isValueType, null, null);

        public static Row OfSqlClient(string text, bool isValueType, SqlClientColumn since) =>
            new(string.Empty, text, isValueType, since, null);

        public static Row NeedsTypes(string package) =>
            new(string.Empty, null, false, null, SqlServerTypesNamespace + package);

        public TypeMapping Under(SqlClientColumn column)
        {
            if (Package is not null)
            {
                return TypeMapping.Failed(TypeMappingKind.NeedsSqlServerTypes, Key, Package);
            }

            if (SqlClientSince is { } since && Lacks(column, since))
            {
                return column == SqlClientColumn.None
                    ? TypeMapping.Failed(TypeMappingKind.NeedsDriver, Key)
                    : TypeMapping.Failed(TypeMappingKind.NeedsNewerSqlClient, Key, VersionOf(since));
            }

            return TypeMapping.Mapped(Key, Text!, IsValueType);
        }

        // The version of a column's first release, as a message names it.
        private static string VersionOf(SqlClientColumn column) =>
            column switch
            {
                SqlClientColumn.SqlClient61 => "6.1",
                SqlClientColumn.SqlClient7 => "7.0",
                SqlClientColumn.OlderThan61 or SqlClientColumn.None or _ => string.Empty,
            };
    }
}
