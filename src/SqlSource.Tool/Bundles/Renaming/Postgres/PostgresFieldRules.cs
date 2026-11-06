using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Postgres;

namespace SqlSource.Tool.Bundles.Renaming.Postgres;

/// <summary>
/// The rule of every string member of PostgreSQL's calls, by <c>"&lt;method&gt; &lt;path&gt;"</c> as
/// <see cref="IEngineRenaming.RuleOf" /> asks it, and of the members of the entry's PostgreSQL type records; and which
/// rows of an answer are the engine's own.
/// </summary>
/// <remarks>
/// The members are those of the records of <c>PostgresCalls.cs</c>, as <c>CaptureJson.Options</c> names them, and
/// <c>PostgresFieldRulesTests</c> fails when a string member has no rule or a rule names no member.  What the pipeline
/// reads of each is finding 1 of the phase 10 spike,
/// <c>docs/superpowers/specs/2026-11-05-query-generation-phase-10-spike.md</c>.  An error's detail and hint, and its
/// position but for <c>prepare</c>'s, are read by nothing and dropped, as is the whole error of <c>deallocate</c>,
/// <c>partitions</c> and <c>relations</c>.
/// </remarks>
internal static class PostgresFieldRules
{
    // The schemas whose rows are the engine's.
    private static readonly FrozenSet<string> EngineSchemas = FrozenSet.ToFrozenSet(
        ["pg_catalog", "information_schema"],
        StringComparer.Ordinal
    );

    // Where the entry's type records are, by the method the renaming asks the entry's members by.
    private static readonly string[] TypeRecords =
    [
        BundleFiles.Expected + " " + SidecarKeys.Parameters + "[]." + SidecarKeys.Type + ".",
        BundleFiles.Expected + " " + SidecarKeys.Columns + "[]." + SidecarKeys.Type + ".",
    ];

    // The members of a type that hold a type of their own, whose members have the same rules.
    private static readonly string[] NestedTypes =
    [
        SidecarKeys.Element + ".",
        SidecarKeys.Base + ".",
        SidecarKeys.Subtype + ".",
    ];

    // The members of the sidecar's PostgreSQL type that are strings.
    private static readonly FrozenDictionary<string, FieldRule> TypeMembers = new Dictionary<string, FieldRule>(
        StringComparer.Ordinal
    )
    {
        [SidecarKeys.Name] = FieldRule.TypeSpelling,
        [SidecarKeys.BindName] = FieldRule.TypeSpelling,
        [SidecarKeys.Kind] = FieldRule.Closed,
        [SidecarKeys.Schema] = FieldRule.Name(NameClass.Schema),
        [SidecarKeys.InternalName] = FieldRule.Name(NameClass.Type),
        [SidecarKeys.Labels + "[]"] = FieldRule.Label,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // Every call, each of which can answer an error.
    private static readonly string[] Methods =
    [
        PostgresCallNames.Open,
        PostgresCallNames.Prepare,
        PostgresCallNames.Columns,
        PostgresCallNames.Tables,
        PostgresCallNames.Types,
        PostgresCallNames.Spellings,
        PostgresCallNames.Check,
        PostgresCallNames.Explain,
        PostgresCallNames.Partitions,
        PostgresCallNames.Relations,
        PostgresCallNames.Overloads,
        PostgresCallNames.Deallocate,
    ];

    // Built after the calls, which it reads.
    private static readonly FrozenDictionary<string, FieldRule> Rules = Build();

    /// <summary>
    /// The members of the calls that the table has a rule for, as <c>"&lt;method&gt; &lt;path&gt;"</c>.
    /// </summary>
    public static IEnumerable<string> Members => Rules.Keys;

    /// <summary>
    /// The rule of a member of a call by its method and its path from the call, or of a member of an entry's type
    /// record at any depth by <see cref="BundleFiles.Expected" /> and its path from the entry; null for any other.
    /// </summary>
    public static FieldRule? RuleOf(string method, string path)
    {
        var key = method + " " + path;
        if (Rules.TryGetValue(key, out var rule))
        {
            return rule;
        }

        var record = TypeRecords.FirstOrDefault(found => key.StartsWith(found, StringComparison.Ordinal));
        if (record is null)
        {
            return null;
        }

        var member = key[record.Length..];
        while (NestedTypes.FirstOrDefault(nested => member.StartsWith(nested, StringComparison.Ordinal)) is { } nested)
        {
            member = member[nested.Length..];
        }

        return TypeMembers.TryGetValue(member, out var typeRule) ? typeRule : null;
    }

    /// <summary>
    /// Whether a row is the engine's: its <c>nspname</c>, or its <c>schema</c> when it has none, is
    /// <c>pg_catalog</c> or <c>information_schema</c>.  It decides by the row alone, whatever call or path holds it.
    /// </summary>
    public static bool IsEngineRow(JsonObject row) =>
        (row["nspname"] ?? row["schema"]) is JsonValue schema
        && schema.TryGetValue<string>(out var name)
        && EngineSchemas.Contains(name);

    private static FrozenDictionary<string, FieldRule> Build()
    {
        var rules = new Dictionary<string, FieldRule>(StringComparer.Ordinal);
        void Add(string method, string path, FieldRule rule) => rules.Add(method + " " + path, rule);

        foreach (var method in (string[])[PostgresCallNames.Tables, PostgresCallNames.Relations])
        {
            Add(method, "answer.tables[].relkind", FieldRule.Closed);
            Add(method, "answer.tables[].schema", FieldRule.Name(NameClass.Schema));
            Add(method, "answer.tables[].name", FieldRule.Name(NameClass.Table));
            Add(method, "answer.tables[].columns[].name", FieldRule.Name(NameClass.Column));
            Add(method, "answer.tables[].columns[].identity", FieldRule.Closed);
            Add(method, "answer.tables[].columns[].generated", FieldRule.Closed);
        }

        foreach (var method in (string[])[PostgresCallNames.Relations, PostgresCallNames.Partitions])
        {
            Add(method, "request.relations[].schema", FieldRule.Name(NameClass.Schema));
            Add(method, "request.relations[].name", FieldRule.Name(NameClass.Table));
        }

        Add(PostgresCallNames.Open, "answer.serverVersion", FieldRule.Fixed);
        Add(PostgresCallNames.Open, "answer.driverVersion", FieldRule.Fixed);
        Add(PostgresCallNames.Open, "answer.settings[].name", FieldRule.Fixed);
        Add(PostgresCallNames.Open, "answer.settings[].value", FieldRule.Own);

        Add(PostgresCallNames.Prepare, "request.statement", FieldRule.Fixed);
        Add(PostgresCallNames.Prepare, "request.sql", FieldRule.Sql);
        Add(PostgresCallNames.Prepare, "request.types[]", FieldRule.TypeSpelling);

        Add(PostgresCallNames.Deallocate, "request.statement", FieldRule.Fixed);

        Add(PostgresCallNames.Columns, "request.statement", FieldRule.Fixed);
        Add(PostgresCallNames.Columns, "answer.columns[].name", FieldRule.Name(NameClass.Column));

        Add(PostgresCallNames.Types, "answer.rows[].typname", FieldRule.Name(NameClass.Type));
        Add(PostgresCallNames.Types, "answer.rows[].nspname", FieldRule.Name(NameClass.Schema));
        Add(PostgresCallNames.Types, "answer.rows[].typtype", FieldRule.Closed);
        Add(PostgresCallNames.Types, "answer.rows[].typcategory", FieldRule.Closed);
        Add(PostgresCallNames.Types, "answer.rows[].formatType", FieldRule.TypeSpelling);
        Add(PostgresCallNames.Types, "answer.rows[].enumLabels[]", FieldRule.Label);

        Add(PostgresCallNames.Spellings, "answer.rows[].formatType", FieldRule.TypeSpelling);

        Add(PostgresCallNames.Check, "request.sql", FieldRule.Sql);
        Add(PostgresCallNames.Check, "request.parameters[].name", FieldRule.Parameter);
        Add(PostgresCallNames.Check, "request.parameters[].dataTypeName", FieldRule.TypeSpelling);
        Add(PostgresCallNames.Check, "answer.columns[].name", FieldRule.Name(NameClass.Column));
        Add(PostgresCallNames.Check, "answer.columns[].dataTypeName", FieldRule.TypeSpelling);
        Add(PostgresCallNames.Check, "answer.driverError.parameter", FieldRule.Parameter);
        Add(PostgresCallNames.Check, "answer.driverError.message", FieldRule.Own);
        Add(PostgresCallNames.Check, "answer.sent.text", FieldRule.Sql);
        Add(PostgresCallNames.Check, "answer.sent.parameters[]", FieldRule.Parameter);

        Add(PostgresCallNames.Explain, "request.statement", FieldRule.Fixed);
        Add(PostgresCallNames.Explain, "request.settings[]", FieldRule.Fixed);
        Add(PostgresCallNames.Explain, "answer.plan", FieldRule.Own);

        Add(PostgresCallNames.Partitions, "answer.rows[].schema", FieldRule.Name(NameClass.Schema));
        Add(PostgresCallNames.Partitions, "answer.rows[].name", FieldRule.Name(NameClass.Table));

        Add(PostgresCallNames.Overloads, "request.functions[]", FieldRule.Name(NameClass.Function));
        Add(PostgresCallNames.Overloads, "request.operators[]", FieldRule.Sql);
        Add(PostgresCallNames.Overloads, "answer.functions[]", FieldRule.Name(NameClass.Function));
        Add(PostgresCallNames.Overloads, "answer.operators[]", FieldRule.Sql);

        // The error of every call.  Prepare's position, a number into the query's SQL, is kept.
        foreach (var method in Methods)
        {
            Add(method, "answer.error.sqlState", FieldRule.Closed);
            Add(method, "answer.error.message", FieldRule.Own);
            Add(method, "answer.error.detail", FieldRule.Drop);
            Add(method, "answer.error.hint", FieldRule.Drop);
            if (method != PostgresCallNames.Prepare)
            {
                Add(method, "answer.error.position", FieldRule.Drop);
            }
        }

        foreach (
            var method in (string[])
                [PostgresCallNames.Deallocate, PostgresCallNames.Partitions, PostgresCallNames.Relations]
        )
        {
            Add(method, "answer.error", FieldRule.Drop);
        }

        return rules.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
