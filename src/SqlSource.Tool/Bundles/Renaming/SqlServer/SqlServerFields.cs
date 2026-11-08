using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.SqlServer;

namespace SqlSource.Tool.Bundles.Renaming.SqlServer;

/// <summary>
/// The rule of every string member of the records of <c>Describing/SqlServer/SqlServerCalls.cs</c>, by the call's
/// method and the member's path as a capture writes it, and of every string member of the entry's SQL Server type.
/// </summary>
/// <remarks>
/// The pipeline reads every member, so none is dropped.  A member added to a record has no rule until it is given one
/// here, and a bundle that holds it is refused at its place.
/// </remarks>
internal static class SqlServerFields
{
    // Every method has an answer's error, whose messages are the engine's own.
    private static readonly string[] Methods =
    [
        SqlServerCallNames.Open,
        SqlServerCallNames.Parameters,
        SqlServerCallNames.Probe,
        SqlServerCallNames.Declared,
        SqlServerCallNames.Columns,
        SqlServerCallNames.Operands,
        SqlServerCallNames.Table,
    ];

    // The methods whose request is SQL under declarations.
    private static readonly string[] SqlMethods =
    [
        SqlServerCallNames.Parameters,
        SqlServerCallNames.Probe,
        SqlServerCallNames.Declared,
        SqlServerCallNames.Columns,
        SqlServerCallNames.Operands,
    ];

    // The methods that ask sys.dm_exec_describe_first_result_set, whose rows give a type and whose error rows a
    // message.
    private static readonly string[] DescribeMethods =
    [
        SqlServerCallNames.Declared,
        SqlServerCallNames.Columns,
        SqlServerCallNames.Operands,
        SqlServerCallNames.Table,
    ];

    /// <summary>The methods whose rows are the columns of a result set, with their sources.</summary>
    public static IReadOnlyList<string> ColumnMethods { get; } =
    [SqlServerCallNames.Columns, SqlServerCallNames.Operands, SqlServerCallNames.Table];

    /// <summary>The path of a row of an answer.</summary>
    public const string Rows = "answer.rows[]";

    /// <summary>The member of a column, a source or a type that holds a name.</summary>
    public const string Name = SidecarKeys.Name;

    /// <summary>The member of a column row that holds the linked server it is read through.</summary>
    public const string SourceServer = "sourceServer";

    /// <summary>The member of a column row that holds its source database.</summary>
    public const string SourceDatabase = "sourceDatabase";

    /// <summary>The member of a column row that holds its source schema.</summary>
    public const string SourceSchema = "sourceSchema";

    /// <summary>The member of a column row that holds its source column.</summary>
    public const string SourceColumn = "sourceColumn";

    /// <summary>The member of the <c>table</c> request that holds the schema.</summary>
    public const string Schema = SidecarKeys.Schema;

    /// <summary>The member of the <c>parameters</c> request that lists the reused parameters.</summary>
    public const string Reused = "reused";

    /// <summary>The member of a reused parameter that lists its copies.</summary>
    public const string Copies = "copies";

    private static readonly FrozenDictionary<string, FieldRule> Rules = Build();

    /// <summary>
    /// The rule of a member, by the call's method and its path from the call, or by <see cref="BundleFiles.Expected" />
    /// and its path from the entry; or null for a member that no record holds.
    /// </summary>
    public static FieldRule? RuleOf(string method, string path) =>
        Rules.TryGetValue(method + " " + path, out var rule) ? rule : null;

    /// <summary>
    /// The members of the calls that the table has a rule for, as <c>"&lt;method&gt; &lt;path&gt;"</c>; the entry's
    /// are left out.
    /// </summary>
    public static IEnumerable<string> Members =>
        Rules.Keys.Where(static key => !key.StartsWith(BundleFiles.Expected + " ", StringComparison.Ordinal));

    private static FrozenDictionary<string, FieldRule> Build()
    {
        var rules = new Dictionary<string, FieldRule>(StringComparer.Ordinal);
        void Add(string method, string path, FieldRule rule) => rules.Add(method + " " + path, rule);
        static string Row(string member) => JsonStrings.MemberOf(Rows, member);

        var setting = JsonStrings.ElementOf("answer.settings");
        Add(SqlServerCallNames.Open, "answer.productVersion", FieldRule.Fixed);
        Add(SqlServerCallNames.Open, "answer.driverVersion", FieldRule.Fixed);
        Add(SqlServerCallNames.Open, JsonStrings.MemberOf(setting, Name), FieldRule.Fixed);
        Add(SqlServerCallNames.Open, JsonStrings.MemberOf(setting, "value"), FieldRule.Fixed);
        Add(SqlServerCallNames.Open, "answer.database", FieldRule.Name(NameClass.Database));
        Add(
            SqlServerCallNames.Table,
            JsonStrings.MemberOf(CaptureJson.RequestKey, Schema),
            FieldRule.Name(NameClass.Schema)
        );
        Add(SqlServerCallNames.Table, "request.table", FieldRule.Name(NameClass.Table));

        var reused = JsonStrings.ElementOf(JsonStrings.MemberOf(CaptureJson.RequestKey, Reused));
        Add(SqlServerCallNames.Parameters, JsonStrings.MemberOf(reused, Name), FieldRule.Parameter);
        Add(
            SqlServerCallNames.Parameters,
            JsonStrings.ElementOf(JsonStrings.MemberOf(reused, Copies)),
            FieldRule.Parameter
        );
        Add(SqlServerCallNames.Parameters, Row(Name), FieldRule.Parameter);
        Add(SqlServerCallNames.Parameters, Row("suggestedSystemTypeName"), FieldRule.TypeSpelling);
        Add(SqlServerCallNames.Parameters, Row("suggestedUserTypeSchema"), FieldRule.Name(NameClass.Schema));
        Add(SqlServerCallNames.Parameters, Row("suggestedUserTypeName"), FieldRule.Name(NameClass.Type));
        Add(
            SqlServerCallNames.Parameters,
            Row("suggestedAssemblyQualifiedTypeName"),
            FieldRule.WholeName(NameClass.Type)
        );

        foreach (var method in Methods)
        {
            Add(method, "answer.error.errors[].message", FieldRule.Own);
        }

        foreach (var method in SqlMethods)
        {
            Add(method, "request.sql", FieldRule.Sql);
            Add(method, "request.params", FieldRule.TypeSpelling);
        }

        foreach (var method in DescribeMethods)
        {
            Add(method, "answer.errors[].message", FieldRule.Own);
            Add(method, Row("systemTypeName"), FieldRule.TypeSpelling);
            Add(method, Row("userTypeSchema"), FieldRule.Name(NameClass.Schema));
            Add(method, Row("userTypeName"), FieldRule.Name(NameClass.Type));
            Add(method, Row("assemblyQualifiedTypeName"), FieldRule.WholeName(NameClass.Type));
        }

        foreach (var method in ColumnMethods)
        {
            Add(method, Row(Name), FieldRule.Name(NameClass.Column));
            Add(method, Row(SourceServer), FieldRule.Name(NameClass.Database));
            Add(method, Row(SourceDatabase), FieldRule.Name(NameClass.Database));
            Add(method, Row(SourceSchema), FieldRule.Name(NameClass.Schema));
            Add(method, Row("sourceTable"), FieldRule.Name(NameClass.Table));
            Add(method, Row(SourceColumn), FieldRule.Name(NameClass.Column));
            Add(method, Row("sourceType"), FieldRule.Closed);
        }

        foreach (var type in EntryRenamer.TypeRecords)
        {
            var record = type[..^1];
            var userType = JsonStrings.MemberOf(record, SidecarKeys.UserType);
            Add(BundleFiles.Expected, JsonStrings.MemberOf(record, Name), FieldRule.TypeSpelling);
            Add(BundleFiles.Expected, JsonStrings.MemberOf(record, SidecarKeys.BindName), FieldRule.TypeSpelling);
            Add(BundleFiles.Expected, JsonStrings.MemberOf(userType, Schema), FieldRule.Name(NameClass.Schema));
            Add(BundleFiles.Expected, JsonStrings.MemberOf(userType, Name), FieldRule.Name(NameClass.Type));
            Add(
                BundleFiles.Expected,
                JsonStrings.MemberOf(userType, SidecarKeys.AssemblyQualifiedName),
                FieldRule.WholeName(NameClass.Type)
            );
        }

        return rules.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
