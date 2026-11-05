using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;

namespace SqlSource.Tool.Tests.Spike10;

// PostgreSQL's part of the crude renamer: the rule of each field of its calls, its own names, the plan (step 9), the
// settings of open and its errors (step 10).
internal sealed partial class CrudeRenamer
{
    private static readonly HashSet<string> PostgresEngineNames =
    [
        with(StringComparer.Ordinal),
        "pg_catalog",
        "public",
        "information_schema",
        "pg_toast",
        "$user",
    ];

    // The keys of a plan whose strings are one raw name, and the class of a new one.
    private static readonly Dictionary<string, char> PlanNameKeys = new(StringComparer.Ordinal)
    {
        ["Alias"] = 'a',
        ["Relation Name"] = 't',
        ["Schema"] = 's',
        ["CTE Name"] = 'a',
        ["Index Name"] = 'i',
        ["Function Name"] = 'f',
    };

    // The keys of a plan whose strings are the planner's fixed words.
    private static readonly HashSet<string> PlanFixedKeys =
    [
        with(StringComparer.Ordinal),
        "Node Type",
        "Join Type",
        "Parent Relationship",
        "Command",
        "Strategy",
        "Partial Mode",
        "Scan Direction",
        "Operation",
    ];

    private static readonly Dictionary<string, Rule> PostgresFields = new(StringComparer.Ordinal)
    {
        ["open.answer.serverVersion"] = Rule.Keep,
        ["open.answer.driverVersion"] = Rule.Keep,
        ["open.answer.settings[].name"] = Rule.Keep,
        ["open.answer.settings[].value"] = Rule.Setting,
        ["prepare.request.statement"] = Rule.Keep,
        ["prepare.request.sql"] = Rule.Sql,
        ["prepare.request.types[]"] = Rule.TypeSpelling,
        ["columns.request.statement"] = Rule.Keep,
        ["columns.answer.columns[].name"] = Rule.Column,
        ["tables.answer.tables[].schema"] = Rule.Schema,
        ["tables.answer.tables[].name"] = Rule.Table,
        ["tables.answer.tables[].relkind"] = Rule.Keep,
        ["tables.answer.tables[].columns[].name"] = Rule.Column,
        ["tables.answer.tables[].columns[].identity"] = Rule.Keep,
        ["tables.answer.tables[].columns[].generated"] = Rule.Keep,
        ["relations.request.relations[].schema"] = Rule.Schema,
        ["relations.request.relations[].name"] = Rule.Table,
        ["relations.answer.tables[].schema"] = Rule.Schema,
        ["relations.answer.tables[].name"] = Rule.Table,
        ["relations.answer.tables[].relkind"] = Rule.Keep,
        ["relations.answer.tables[].columns[].name"] = Rule.Column,
        ["relations.answer.tables[].columns[].identity"] = Rule.Keep,
        ["relations.answer.tables[].columns[].generated"] = Rule.Keep,
        ["partitions.request.relations[].schema"] = Rule.Schema,
        ["partitions.request.relations[].name"] = Rule.Table,
        ["partitions.answer.rows[].schema"] = Rule.Schema,
        ["partitions.answer.rows[].name"] = Rule.Table,
        ["types.answer.rows[].typname"] = Rule.TypeName,
        ["types.answer.rows[].nspname"] = Rule.Schema,
        ["types.answer.rows[].formatType"] = Rule.TypeSpelling,
        ["types.answer.rows[].enumLabels[]"] = Rule.Label,
        ["types.answer.rows[].typtype"] = Rule.Keep,
        ["types.answer.rows[].typcategory"] = Rule.Keep,
        ["spellings.answer.rows[].formatType"] = Rule.TypeSpelling,
        ["check.request.sql"] = Rule.Sql,
        ["check.request.parameters[].name"] = Rule.Parameter,
        ["check.request.parameters[].dataTypeName"] = Rule.TypeSpelling,
        ["check.answer.columns[].name"] = Rule.Column,
        ["check.answer.columns[].dataTypeName"] = Rule.TypeSpelling,
        ["check.answer.sent.text"] = Rule.Sql,
        ["check.answer.sent.parameters[]"] = Rule.Parameter,
        ["explain.request.statement"] = Rule.Keep,
        ["explain.request.settings[]"] = Rule.Keep,
        ["explain.answer.plan"] = Rule.Plan,
        ["overloads.request.functions[]"] = Rule.Function,
        ["overloads.request.operators[]"] = Rule.Keep,
        ["overloads.answer.functions[]"] = Rule.Function,
        ["overloads.answer.operators[]"] = Rule.Keep,
        ["deallocate.request.statement"] = Rule.Keep,
    };

    // The names the planner makes (step 2), by form, and the words it prints of its own.
    private static readonly Regex[] PlannerForms =
    [
        PlannerSelect(),
        PlannerValues(),
        PlannerColumn(),
        PlannerSubPlan(),
        PlannerWords(),
    ];

    private static bool PlannerName(string spelling) => PlannerForms.Any(form => form.IsMatch(spelling));

    // A type row of pg_catalog or information_schema, or a relation of one, is the engine's.
    private static bool IsPostgresEngineRow(JsonObject row) =>
        (row["nspname"] ?? row["schema"]) is JsonValue schema
        && schema.GetValueKind() == JsonValueKind.String
        && schema.GetValue<string>() is "pg_catalog" or "information_schema";

    // search_path's schemas, other than $user, public and pg_catalog, are renamed; any other setting is kept.
    private string Setting(JsonObject? row, string value)
    {
        if (row?["name"]?.GetValue<string>() != "search_path")
        {
            return value;
        }

        var parts = value.Split(',').Select(static part => part.Trim()).ToList();
        for (var index = 0; index < parts.Count; index++)
        {
            var part = parts[index];
            var quoted = part.Length > 1 && part[0] == '"';
            var name = quoted ? PostgresIdentifiers.Unquote(part) : part;
            if (!Keeps(name))
            {
                var renamed = NameOf(name, quoted, 's', "open.search_path");
                parts[index] = quoted ? "\"" + renamed + "\"" : renamed;
            }
        }

        return string.Join(", ", parts);
    }

    // The names of a plan, by key, before the query is read (step 2).
    private void RegisterPlanNames(JsonObject call)
    {
        if (call["answer"]?["plan"] is not JsonValue text || JsonNode.Parse(text.GetValue<string>()) is not { } plan)
        {
            return;
        }

        foreach (var (key, value) in PlanStrings(plan))
        {
            if (
                PlanNameKeys.TryGetValue(key, out var cls)
                && !CrudeLists.Instance.PlanKeysNotRead.Contains(key)
                && !(
                    key == "Alias"
                    && (_rules & CrudeRules.ChildAliasesThroughTheirAlias) != 0
                    && ChildAlias().IsMatch(value)
                )
            )
            {
                _ = NameOf(value, quoted: true, cls, "plan." + key);
            }
        }
    }

    private static IEnumerable<(string Key, string Value)> PlanStrings(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, value) in obj)
                {
                    if (value is JsonValue leaf && leaf.GetValueKind() == JsonValueKind.String)
                    {
                        yield return (name, leaf.GetValue<string>());
                    }
                    else if (value is JsonArray array && array.All(static e => e is JsonValue))
                    {
                        foreach (var item in array.OfType<JsonValue>())
                        {
                            if (item.GetValueKind() == JsonValueKind.String)
                            {
                                yield return (name, item.GetValue<string>());
                            }
                        }
                    }
                    else
                    {
                        foreach (var inner in PlanStrings(value))
                        {
                            yield return inner;
                        }
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    foreach (var inner in PlanStrings(item))
                    {
                        yield return inner;
                    }
                }

                break;
            default:
                break;
        }
    }

    // Step 9: the plan's keys the walk does not read are dropped, a name key is one name, a fixed key is kept, and any
    // other string is renamed through PlanTokens.
    private string Plan(string text)
    {
        var plan = JsonNode.Parse(text)!;
        PlanNodeRename(plan);
        return plan.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private void PlanNodeRename(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, value) in obj.ToArray())
                {
                    if (CrudeLists.Instance.PlanKeysNotRead.Contains(name))
                    {
                        _ = obj.Remove(name);
                    }
                    else if (value is JsonValue leaf && leaf.GetValueKind() == JsonValueKind.String)
                    {
                        obj[name] = PlanString(name, leaf.GetValue<string>());
                    }
                    else if (value is JsonArray array && array.Count > 0 && array.All(static e => e is JsonValue))
                    {
                        for (var index = 0; index < array.Count; index++)
                        {
                            if (array[index]!.GetValueKind() == JsonValueKind.String)
                            {
                                array[index] = PlanString(name, array[index]!.GetValue<string>());
                            }
                        }
                    }
                    else
                    {
                        PlanNodeRename(value);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    PlanNodeRename(item);
                }

                break;
            default:
                break;
        }
    }

    private string PlanString(string key, string value)
    {
        if (PlanFixedKeys.Contains(key))
        {
            return value;
        }

        if (key == "Alias" && DerivedAlias(value) is { } derived)
        {
            return derived;
        }

        if (PlanNameKeys.TryGetValue(key, out var cls))
        {
            return NameOf(value, quoted: true, cls, "plan." + key);
        }

        if (
            key == "Subplan Name"
            && (_rules & CrudeRules.CteSubplanName) != 0
            && value.StartsWith("CTE ", StringComparison.Ordinal)
        )
        {
            return "CTE " + NameOf(value[4..], quoted: true, 'a', "plan." + key);
        }

        if (
            key
            is not (
                "Output"
                or "Filter"
                or "Group Key"
                or "Subplan Name"
                or "Sort Key"
                or "Hash Cond"
                or "Join Filter"
                or "Index Cond"
                or "Recheck Cond"
                or "Merge Cond"
                or "One-Time Filter"
                or "Run Condition"
                or "Function Call"
                or "Window"
                or "Hash Keys"
                or "Group Keys"
            )
        )
        {
            Note($"no rule for plan key {key}: renamed through PlanTokens");
        }

        return PlanExpression(key, value);
    }

    private string PlanExpression(string key, string value)
    {
        var tokens = PlanTokens.Read(value);
        if (tokens is null)
        {
            Note($"refused: a plan string of {key} does not read through PlanTokens");
            return value;
        }

        var into = new StringBuilder(value.Length);
        var at = 0;
        foreach (var token in tokens)
        {
            _ = into.Append(value, at, token.Start - at);
            var text = value[token.Start..token.End];
            _ = into.Append(
                token.Kind switch
                {
                    PlanTokens.Kind.Word => DerivedAlias(text) ?? NameOf(text, quoted: false, 'a', "plan." + key),
                    PlanTokens.Kind.QuotedName => PlanQuotedName(text, key),
                    PlanTokens.Kind.String => "'"
                        + _mapping.Literal(text[1..^1].Replace("''", "'", StringComparison.Ordinal))
                        + "'",
                    PlanTokens.Kind.Number
                    or PlanTokens.Kind.Open
                    or PlanTokens.Kind.Close
                    or PlanTokens.Kind.OpenBracket
                    or PlanTokens.Kind.CloseBracket
                    or PlanTokens.Kind.Comma
                    or PlanTokens.Kind.Dot
                    or PlanTokens.Kind.Cast
                    or PlanTokens.Kind.Other => text,
                    _ => text,
                }
            );
            at = token.End;
        }

        _ = into.Append(value, at, value.Length - at);
        return into.ToString();
    }

    private string PlanQuotedName(string quoted, string key)
    {
        var name = PostgresIdentifiers.Unquote(quoted);
        return Keeps(name) ? quoted : "\"" + NameOf(name, quoted: true, 'a', "plan." + key) + "\"";
    }

    // With ChildAliasesThroughTheirAlias, an alias <alias>_<n> that no answer or the query holds, of an alias that one
    // does, renamed through that alias.
    private string? DerivedAlias(string name)
    {
        if (
            (_rules & CrudeRules.ChildAliasesThroughTheirAlias) == 0
            || _mapping.TryGet(name, out _)
            || ChildAlias().Match(name) is not { Success: true } child
        )
        {
            return null;
        }

        var parent = child.Groups["alias"].Value;
        if (Keeps(parent))
        {
            return name;
        }

        return _mapping.TryGet(parent, out var renamed)
            ? renamed.Replacement + "_" + child.Groups["number"].Value
            : null;
    }

    // Step 10: the SQLSTATE stays, a 42P18 or 42P08 keeps "parameter $n", the tool's own texts stay, and any other
    // text is emptied; detail and hint go.  No case holds an error.
    private void PostgresError(JsonObject error, string path)
    {
        Note("an error was renamed");
        if (path.EndsWith(".driverError", StringComparison.Ordinal))
        {
            if (error["parameter"] is JsonValue parameter)
            {
                error["parameter"] = ParameterName(parameter.GetValue<string>(), "driverError");
            }

            error["message"] = "";
            return;
        }

        var state = error["sqlState"]?.GetValue<string>();
        var message = error["message"]?.GetValue<string>() ?? "";
        error["message"] = state switch
        {
            "42P18" or "42P08" when DollarParameter().Match(message) is { Success: true } named => "parameter "
                + named.Value,
            _ when message
                    is ConnectionErrors.MultiplexingRefused
                        or ConnectionErrors.UnreadableTypes
                        or PostgresLive.StatementNotListed
                        or PostgresLive.TransactionRefused => message,
            _ => "",
        };
        error["detail"] = null;
        error["hint"] = null;
    }

    [GeneratedRegex(@"^\*SELECT\* \d+$")]
    private static partial Regex PlannerSelect();

    [GeneratedRegex(@"^\*VALUES\*$")]
    private static partial Regex PlannerValues();

    [GeneratedRegex(@"^col(umn)?\d+$")]
    private static partial Regex PlannerColumn();

    [GeneratedRegex(@"^(InitPlan|SubPlan)( \d+)?$")]
    private static partial Regex PlannerSubPlan();

    [GeneratedRegex(
        @"^(unnamed_subquery|ANY_subquery|excluded|worktable|hashed|\?column\?|w\d+)$",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex PlannerWords();

    [GeneratedRegex(@"^(?<alias>.+)_(?<number>\d+)$")]
    private static partial Regex ChildAlias();

    [GeneratedRegex(@"\$\d+")]
    private static partial Regex DollarParameter();
}
