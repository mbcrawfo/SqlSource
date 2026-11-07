using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using SqlSource.Parsing;
using SqlSource.Parsing.Quoting;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Describing.Postgres;

namespace SqlSource.Tool.Tests;

/// <summary>
/// The engine part of the renaming for <see cref="FakeDescriber" />: PostgreSQL's lexer and word rule, or SQL Server's
/// for a test of brackets and prefixes, a small vocabulary, the copies of reused parameters that a request records
/// under <c>copies</c>, as <see cref="Copies" /> writes them, and the rule of every member of
/// <c>FakeDescriberCalls.cs</c>' <c>open</c> and <c>describe</c> and of the entry's PostgreSQL types.
/// </summary>
/// <remarks>
/// A row of <c>pg_catalog</c> is the engine's.  A setting's value and a server's line are its own strings, renamed as
/// SQL.  A test changes it through its constructor: <c>rules</c> replaces the rule of a member, by
/// <c>"&lt;method&gt; &lt;path&gt;"</c>, a null taking the rule away, and <c>derive</c> is what it derives.
/// </remarks>
internal sealed class FakeEngineRenaming(
    SqlDialect dialect = SqlDialect.PostgreSql,
    IReadOnlyDictionary<string, FieldRule?>? rules = null,
    Action<RenamingCall, IRenamingScope>? derive = null
) : IEngineRenaming
{
    /// <summary>
    /// The words the fake keeps, folded: a few keywords, a function, the schemas <c>public</c> and <c>pg_catalog</c>,
    /// and the types of <see cref="FakeDescriber.TypedRows" /> that are the engine's.
    /// </summary>
    public static readonly IReadOnlySet<string> Vocabulary = new HashSet<string>(
        [
            "select",
            "from",
            "where",
            "join",
            "on",
            "as",
            "and",
            "or",
            "not",
            "null",
            "left",
            "count",
            "public",
            "pg_catalog",
            "integer",
            "int4",
            "text",
            "_text",
        ],
        StringComparer.Ordinal
    );

    // Where the type records are: the describer's answer, then the entry's.
    private static readonly string[] TypeRecords =
    [
        "describe answer.description.parameters[].type.",
        "describe answer.description.columns[].type.",
        BundleFiles.Expected + " parameters[].type.",
        BundleFiles.Expected + " columns[].type.",
    ];

    // The members of a type that hold a type of their own, whose members have the same rules.
    private static readonly string[] NestedTypes = ["element.", "base.", "subtype."];

    // The members of a type record, as FakeType and the sidecar's PostgreSQL type write them.
    private static readonly Dictionary<string, FieldRule> TypeMembers = new(StringComparer.Ordinal)
    {
        ["name"] = FieldRule.TypeSpelling,
        ["bindName"] = FieldRule.TypeSpelling,
        ["kind"] = FieldRule.Closed,
        ["schema"] = FieldRule.Name(NameClass.Schema),
        ["internalName"] = FieldRule.Name(NameClass.Type),
        ["labels[]"] = FieldRule.Label,
    };

    // The members of FakeDescriberCalls.cs' records, by "<method> <path>", but the types'.
    private static readonly Dictionary<string, FieldRule> Members = WithFailures(
        new(StringComparer.Ordinal)
        {
            ["open answer.server.version"] = FieldRule.Fixed,
            ["open answer.server.driver"] = FieldRule.Drop,
            ["open answer.server.driverVersion"] = FieldRule.Drop,
            ["open answer.server.settings[].name"] = FieldRule.Fixed,
            ["open answer.server.settings[].value"] = FieldRule.Own,
            ["describe request.name"] = FieldRule.Name(NameClass.Query),
            ["describe request.sql"] = FieldRule.Sql,
            ["describe request.parameters[].name"] = FieldRule.Parameter,
            ["describe request.parameters[].type"] = FieldRule.TypeSpelling,
            ["describe request.copies[].name"] = FieldRule.Parameter,
            ["describe request.copies[].parameter"] = FieldRule.Parameter,
            ["describe request.copies[].suffix"] = FieldRule.Fixed,
            ["describe answer.description.resultKind"] = FieldRule.Closed,
            ["describe answer.description.parameters[].name"] = FieldRule.Parameter,
            ["describe answer.description.parameters[].typeSource"] = FieldRule.Fixed,
            ["describe answer.description.columns[].name"] = FieldRule.Name(NameClass.Column),
            ["describe answer.description.columns[].nullableSource"] = FieldRule.Fixed,
            ["describe answer.description.columns[].origin.schema"] = FieldRule.Name(NameClass.Schema),
            ["describe answer.description.columns[].origin.table"] = FieldRule.Name(NameClass.Table),
            ["describe answer.description.columns[].origin.column"] = FieldRule.Name(NameClass.Column),
            ["describe answer.description.matchesTable.schema"] = FieldRule.Name(NameClass.Schema),
            ["describe answer.description.matchesTable.table"] = FieldRule.Name(NameClass.Table),
            ["describe answer.description.plan"] = FieldRule.Fixed,
            ["describe answer.description.tableMatch"] = FieldRule.Fixed,
        }
    );

    public SqlDialect Dialect => dialect;

    public bool IsWordStart(char character) =>
        dialect == SqlDialect.SqlServer
            ? QuoteReader.IsNameCharacterOfSqlServer(character) && !char.IsDigit(character)
            : PostgresIdentifiers.IsWordStart(character);

    public bool IsWordPart(char character) =>
        dialect == SqlDialect.SqlServer
            ? QuoteReader.IsNameCharacterOfSqlServer(character)
            : PostgresIdentifiers.IsWordPart(character);

    // Character by character, as PostgreSQL folds: CA1308 rejects string.ToLowerInvariant.
    public string Fold(string spelling, bool quoted) =>
        quoted ? spelling : string.Concat(spelling.Select(char.ToLowerInvariant));

    public bool IsEngineWord(string spelling, bool quoted) => Vocabulary.Contains(Fold(spelling, quoted));

    public FieldRule? RuleOf(string method, string path)
    {
        var key = method + " " + path;
        if (rules is not null && rules.TryGetValue(key, out var replaced))
        {
            return replaced;
        }

        if (Members.TryGetValue(key, out var rule))
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

    // A type of pg_catalog.
    public bool IsEngineRow(string method, string path, JsonObject row) =>
        row["schema"] is JsonValue schema
        && schema.TryGetValue<string>(out var name)
        && string.Equals(name, "pg_catalog", StringComparison.Ordinal);

    // The copies a call's request records under copies, as Copies writes them.
    public IEnumerable<ParameterCopy> CopiesIn(string method, JsonObject call) =>
        call["request"]?["copies"] is JsonArray copies
            ? copies
                .OfType<JsonObject>()
                .Select(static copy => new ParameterCopy(
                    copy["name"]!.GetValue<string>(),
                    copy["parameter"]!.GetValue<string>(),
                    copy["suffix"]!.GetValue<string>()
                ))
            : [];

    /// <summary>
    /// The copies of reused parameters as a request of <c>describe</c> records them for <see cref="CopiesIn" />, each
    /// copy's name, the parameter it copies and its suffix.
    /// </summary>
    public static JsonArray Copies(params (string Name, string Parameter, string Suffix)[] copies) =>
        [
            .. copies.Select(static copy => new JsonObject
            {
                ["name"] = copy.Name,
                ["parameter"] = copy.Parameter,
                ["suffix"] = copy.Suffix,
            }),
        ];

    public void Register(RenamingCall call, IRenamingScope scope) { }

    public string RenameOwn(RenamingField field, IRenamingScope scope) => scope.Sql(field.Value, NameClass.Alias).Text;

    // The words of a renamed value: what a comma or white space parts.
    public IEnumerable<string> WordsOf(RenamingField field) =>
        field.Value.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);

    public void Derive(RenamingCall call, IRenamingScope scope) => derive?.Invoke(call, scope);

    // The members of the calls with those of a FakeFailure in the answer of each.  The failure's argument is the
    // query's name, as FakeDescriber.Failure gives it.
    private static Dictionary<string, FieldRule> WithFailures(Dictionary<string, FieldRule> members)
    {
        var failure = new Dictionary<string, FieldRule>(StringComparer.Ordinal)
        {
            ["id"] = FieldRule.Closed,
            ["title"] = FieldRule.Fixed,
            ["messageFormat"] = FieldRule.Fixed,
            ["category"] = FieldRule.Fixed,
            ["description"] = FieldRule.Fixed,
            ["helpLink"] = FieldRule.Fixed,
            ["arguments[]"] = FieldRule.Name(NameClass.Query),
            ["step"] = FieldRule.Closed,
            ["serverLines[]"] = FieldRule.Own,
            ["help"] = FieldRule.Fixed,
        };
        foreach (var method in (string[])["open", "describe"])
        {
            foreach (var (path, rule) in failure)
            {
                members[$"{method} answer.error.{path}"] = rule;
            }
        }

        return members;
    }
}
