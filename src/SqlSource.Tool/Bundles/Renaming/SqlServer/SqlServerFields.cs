using System;
using System.Collections.Frozen;
using System.Collections.Generic;
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

    // The methods whose rows are the columns of a result set, with their sources.
    private static readonly string[] ColumnMethods =
    [
        SqlServerCallNames.Columns,
        SqlServerCallNames.Operands,
        SqlServerCallNames.Table,
    ];

    // The type records of the entry, whose members are asked with their path from the entry.
    private static readonly string[] EntryTypes = ["parameters[].type.", "columns[].type."];

    private static readonly FrozenDictionary<string, FieldRule> Rules = Build();

    /// <summary>
    /// The rule of a member, by the call's method and its path from the call, or by <see cref="BundleFiles.Expected" />
    /// and its path from the entry; or null for a member that no record holds.
    /// </summary>
    public static FieldRule? RuleOf(string method, string path) =>
        Rules.TryGetValue(method + " " + path, out var rule) ? rule : null;

    private static FrozenDictionary<string, FieldRule> Build()
    {
        var rules = new Dictionary<string, FieldRule>(StringComparer.Ordinal)
        {
            ["open answer.productVersion"] = FieldRule.Fixed,
            ["open answer.driverVersion"] = FieldRule.Fixed,
            ["open answer.settings[].name"] = FieldRule.Fixed,
            ["open answer.settings[].value"] = FieldRule.Fixed,
            ["open answer.database"] = FieldRule.Name(NameClass.Database),
            ["table request.schema"] = FieldRule.Name(NameClass.Schema),
            ["table request.table"] = FieldRule.Name(NameClass.Table),
            ["parameters request.reused[].name"] = FieldRule.Parameter,
            ["parameters request.reused[].copies[]"] = FieldRule.Parameter,
            ["parameters answer.rows[].name"] = FieldRule.Parameter,
            ["parameters answer.rows[].suggestedSystemTypeName"] = FieldRule.TypeSpelling,
            ["parameters answer.rows[].suggestedUserTypeSchema"] = FieldRule.Name(NameClass.Schema),
            ["parameters answer.rows[].suggestedUserTypeName"] = FieldRule.Name(NameClass.Type),
            ["parameters answer.rows[].suggestedAssemblyQualifiedTypeName"] = FieldRule.WholeName(NameClass.Type),
        };
        foreach (var method in Methods)
        {
            rules[method + " answer.error.errors[].message"] = FieldRule.Own;
        }

        foreach (var method in SqlMethods)
        {
            rules[method + " request.sql"] = FieldRule.Sql;
            rules[method + " request.params"] = FieldRule.TypeSpelling;
        }

        foreach (var method in DescribeMethods)
        {
            var row = method + " answer.rows[].";
            rules[method + " answer.errors[].message"] = FieldRule.Own;
            rules[row + "systemTypeName"] = FieldRule.TypeSpelling;
            rules[row + "userTypeSchema"] = FieldRule.Name(NameClass.Schema);
            rules[row + "userTypeName"] = FieldRule.Name(NameClass.Type);
            rules[row + "assemblyQualifiedTypeName"] = FieldRule.WholeName(NameClass.Type);
        }

        foreach (var method in ColumnMethods)
        {
            var row = method + " answer.rows[].";
            rules[row + "name"] = FieldRule.Name(NameClass.Column);
            rules[row + "sourceServer"] = FieldRule.Name(NameClass.Database);
            rules[row + "sourceDatabase"] = FieldRule.Name(NameClass.Database);
            rules[row + "sourceSchema"] = FieldRule.Name(NameClass.Schema);
            rules[row + "sourceTable"] = FieldRule.Name(NameClass.Table);
            rules[row + "sourceColumn"] = FieldRule.Name(NameClass.Column);
            rules[row + "sourceType"] = FieldRule.Closed;
        }

        foreach (var type in EntryTypes)
        {
            var record = BundleFiles.Expected + " " + type;
            rules[record + "name"] = FieldRule.TypeSpelling;
            rules[record + "bindName"] = FieldRule.TypeSpelling;
            rules[record + "userType.schema"] = FieldRule.Name(NameClass.Schema);
            rules[record + "userType.name"] = FieldRule.Name(NameClass.Type);
            rules[record + "userType.assemblyQualifiedName"] = FieldRule.WholeName(NameClass.Type);
        }

        return rules.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
