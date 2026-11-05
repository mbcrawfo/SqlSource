using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

#pragma warning disable CA1308 // PostgreSQL folds a name to lower case, and the lists are lower case.

namespace SqlSource.Tool.Tests.Spike10;

// SQL Server's part of the crude renamer: the rule of each field of its calls, its own names and its errors (step 10).
internal sealed partial class CrudeRenamer
{
    private static readonly HashSet<string> SqlServerEngineNames =
    [
        with(StringComparer.Ordinal),
        "dbo",
        "sys",
        "information_schema",
        // Engine words the spike found in no list of task 4: the pseudo-columns, MERGE's BY SOURCE and BY TARGET, the
        // system databases, and the name of browse mode 2's hidden row.
        "$action",
        "$identity",
        "$rowguid",
        "$node_id",
        "$edge_id",
        "$from_id",
        "$to_id",
        "source",
        "target",
        "master",
        "msdb",
        "model",
        "tempdb",
        "mssqlsystemresource",
        "rowstat\u0000",
    ];

    private static readonly Dictionary<string, Rule> SqlServerFields = SqlServerFieldTable();

    private static Dictionary<string, Rule> SqlServerFieldTable()
    {
        var fields = new Dictionary<string, Rule>(StringComparer.Ordinal)
        {
            ["open.answer.database"] = Rule.Database,
            ["open.answer.productVersion"] = Rule.Keep,
            ["open.answer.driverVersion"] = Rule.Keep,
            ["open.answer.settings[].name"] = Rule.Keep,
            ["open.answer.settings[].value"] = Rule.Keep,
            ["parameters.answer.rows[].name"] = Rule.Parameter,
            ["parameters.answer.rows[].suggestedSystemTypeName"] = Rule.TypeSpelling,
            ["parameters.answer.rows[].suggestedUserTypeName"] = Rule.TypeName,
            ["parameters.answer.rows[].suggestedUserTypeSchema"] = Rule.Schema,
            ["parameters.answer.rows[].suggestedAssemblyQualifiedTypeName"] = Rule.Keep,
            ["table.request.schema"] = Rule.Schema,
            ["table.request.table"] = Rule.Table,
        };
        foreach (var method in new[] { "parameters", "declared", "columns", "operands", "probe" })
        {
            fields[method + ".request.sql"] = Rule.Sql;
            fields[method + ".request.params"] = Rule.TypeSpelling;
        }

        // The rows of sys.dm_exec_describe_first_result_set, which columns, operands, table and declared answer.
        foreach (var method in new[] { "columns", "operands", "table", "declared" })
        {
            var row = method + ".answer.rows[].";
            fields[row + "name"] = Rule.Column;
            fields[row + "systemTypeName"] = Rule.TypeSpelling;
            fields[row + "userTypeName"] = Rule.TypeName;
            fields[row + "userTypeSchema"] = Rule.Schema;
            fields[row + "assemblyQualifiedTypeName"] = Rule.Keep;
            fields[row + "sourceServer"] = Rule.Database;
            fields[row + "sourceDatabase"] = Rule.Database;
            fields[row + "sourceSchema"] = Rule.Schema;
            fields[row + "sourceTable"] = Rule.Table;
            fields[row + "sourceColumn"] = Rule.Column;
            fields[row + "sourceType"] = Rule.Keep;
        }

        return fields;
    }

    // A row whose origin is in sys or INFORMATION_SCHEMA, or in a system database, is the engine's.
    private static bool IsSqlServerEngineRow(JsonObject row) =>
        (
            row["sourceSchema"] is JsonValue schema
            && schema.GetValueKind() == JsonValueKind.String
            && schema.GetValue<string>().ToLowerInvariant() is "sys" or "information_schema"
        )
        || (
            row["sourceDatabase"] is JsonValue database
            && database.GetValueKind() == JsonValueKind.String
            && database.GetValue<string>().ToLowerInvariant() is "master" or "msdb" or "model" or "tempdb"
        );

    // Step 10: the numbers stay, and a message becomes the @names it named, renamed.  No case holds an error.
    private void SqlServerError(JsonObject error)
    {
        Note("an error was renamed");
        foreach (var row in (error["errors"] as JsonArray ?? []).OfType<JsonObject>().Append(error))
        {
            if (row["message"] is JsonValue message && message.GetValueKind() == JsonValueKind.String)
            {
                row["message"] = string.Join(
                    " ",
                    AtName()
                        .Matches(message.GetValue<string>())
                        .Select(match => "@" + ParameterName(match.Value[1..], "error"))
                );
            }
        }
    }

    [GeneratedRegex(@"@\w+")]
    private static partial Regex AtName();
}
