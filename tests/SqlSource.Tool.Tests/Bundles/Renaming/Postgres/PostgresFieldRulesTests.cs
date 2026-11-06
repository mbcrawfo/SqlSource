using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shouldly;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Bundles.Renaming.Postgres;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming.Postgres;

public sealed class PostgresFieldRulesTests
{
    // The records of each call, as PostgresSession asks it; relations is answered as tables is.
    private static readonly Dictionary<string, (Type Request, Type Answer)> Calls = new(StringComparer.Ordinal)
    {
        [PostgresCallNames.Open] = (typeof(PostgresOpenRequest), typeof(PostgresOpenAnswer)),
        [PostgresCallNames.Prepare] = (typeof(PostgresPrepareRequest), typeof(PostgresPrepareAnswer)),
        [PostgresCallNames.Columns] = (typeof(PostgresColumnsRequest), typeof(PostgresColumnsAnswer)),
        [PostgresCallNames.Tables] = (typeof(PostgresTablesRequest), typeof(PostgresTablesAnswer)),
        [PostgresCallNames.Types] = (typeof(PostgresTypesRequest), typeof(PostgresTypesAnswer)),
        [PostgresCallNames.Spellings] = (typeof(PostgresSpellingsRequest), typeof(PostgresSpellingsAnswer)),
        [PostgresCallNames.Check] = (typeof(PostgresCheckRequest), typeof(PostgresCheckAnswer)),
        [PostgresCallNames.Explain] = (typeof(PostgresExplainRequest), typeof(PostgresExplainAnswer)),
        [PostgresCallNames.Partitions] = (typeof(PostgresPartitionsRequest), typeof(PostgresPartitionsAnswer)),
        [PostgresCallNames.Relations] = (typeof(PostgresRelationsRequest), typeof(PostgresTablesAnswer)),
        [PostgresCallNames.Overloads] = (typeof(PostgresOverloadsRequest), typeof(PostgresOverloadsAnswer)),
        [PostgresCallNames.Deallocate] = (typeof(PostgresDeallocateRequest), typeof(PostgresDeallocateAnswer)),
    };

    private static readonly Type[] Scalars = [typeof(string), typeof(int), typeof(long), typeof(bool)];

    public static TheoryData<string> Methods => [with(Calls.Keys.Order(StringComparer.Ordinal))];

    // A call that is added fails here until its records are mapped above, and so are walked below.
    [Fact]
    public void Calls_AreEveryCallOfTheDescriber() =>
        Calls
            .Keys.Order(StringComparer.Ordinal)
            .ShouldBe(
                typeof(PostgresCallNames)
                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(static field => field.IsLiteral)
                    .Select(static field => (string)field.GetRawConstantValue()!)
                    .Order(StringComparer.Ordinal)
            );

    // Below a member that is dropped nothing is renamed, so nothing there needs a rule.
    [Fact]
    public void RuleOf_EveryStringMemberOfEveryCall_HasARule()
    {
        List<string> missing = [];
        foreach (var (method, path, isString) in AllMembers())
        {
            if (isString && !IsUnderADrop(method, path) && PostgresFieldRules.RuleOf(method, path) is null)
            {
                missing.Add(method + " " + path);
            }
        }

        missing.ShouldBeEmpty();
    }

    [Fact]
    public void RuleOf_EveryRuleOfTheTable_IsOfAMemberThatExists()
    {
        var members = AllMembers().Select(static found => found.Method + " " + found.Path).ToHashSet();

        PostgresFieldRules.Members.Where(member => !members.Contains(member)).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(Methods))]
    public void RuleOf_ErrorDetail_IsDrop(string method) =>
        PostgresFieldRules.RuleOf(method, "answer.error.detail").ShouldBe(FieldRule.Drop);

    [Theory]
    [MemberData(nameof(Methods))]
    public void RuleOf_ErrorHint_IsDrop(string method) =>
        PostgresFieldRules.RuleOf(method, "answer.error.hint").ShouldBe(FieldRule.Drop);

    [Fact]
    public void RuleOf_PrepareErrorPosition_IsNotDrop() =>
        PostgresFieldRules.RuleOf(PostgresCallNames.Prepare, "answer.error.position").ShouldNotBe(FieldRule.Drop);

    [Fact]
    public void RuleOf_CheckErrorPosition_IsDrop() =>
        PostgresFieldRules.RuleOf(PostgresCallNames.Check, "answer.error.position").ShouldBe(FieldRule.Drop);

    [Theory]
    [InlineData(PostgresCallNames.Deallocate)]
    [InlineData(PostgresCallNames.Partitions)]
    [InlineData(PostgresCallNames.Relations)]
    public void RuleOf_ErrorOfACallWhoseErrorIsReadByNothing_IsDrop(string method) =>
        PostgresFieldRules.RuleOf(method, "answer.error").ShouldBe(FieldRule.Drop);

    [Theory]
    [InlineData(PostgresCallNames.Prepare, "request.types[]", nameof(FieldRuleKind.TypeSpelling))]
    [InlineData(PostgresCallNames.Check, "answer.sent.parameters[]", nameof(FieldRuleKind.Parameter))]
    [InlineData(PostgresCallNames.Relations, "answer.tables[].columns[].name", nameof(FieldRuleKind.Name))]
    [InlineData(PostgresCallNames.Types, "answer.rows[].enumLabels[]", nameof(FieldRuleKind.Label))]
    [InlineData(PostgresCallNames.Explain, "answer.plan", nameof(FieldRuleKind.Own))]
    public void RuleOf_MemberOfACall_IsTheTables(string method, string path, string kind) =>
        (PostgresFieldRules.RuleOf(method, path)?.Kind.ToString()).ShouldBe(kind);

    [Fact]
    public void RuleOf_EntryTypeAtAnyDepth_IsTheTypesMembers() =>
        PostgresFieldRules
            .RuleOf(BundleFiles.Expected, "columns[].type.element.base.internalName")
            .ShouldBe(FieldRule.Name(NameClass.Type));

    [Theory]
    [InlineData("parameters[].type.name", nameof(FieldRuleKind.TypeSpelling))]
    [InlineData("parameters[].type.bindName", nameof(FieldRuleKind.TypeSpelling))]
    [InlineData("columns[].type.kind", nameof(FieldRuleKind.Closed))]
    [InlineData("columns[].type.subtype.schema", nameof(FieldRuleKind.Name))]
    [InlineData("columns[].type.element.labels[]", nameof(FieldRuleKind.Label))]
    public void RuleOf_EntryTypeMember_IsTheTypesRule(string path, string kind) =>
        (PostgresFieldRules.RuleOf(BundleFiles.Expected, path)?.Kind.ToString()).ShouldBe(kind);

    [Theory]
    [InlineData(PostgresCallNames.Open, "answer.unknown")]
    [InlineData(PostgresCallNames.Prepare, "answer.parameterTypes[]")]
    [InlineData("unknown", "request.sql")]
    [InlineData(BundleFiles.Expected, "columns[].type.unknown")]
    [InlineData(BundleFiles.Expected, "columns[].name")]
    public void RuleOf_UnknownMember_IsNull(string method, string path) =>
        PostgresFieldRules.RuleOf(method, path).ShouldBeNull();

    [Theory]
    [InlineData( /*lang=json,strict*/
        """{"nspname":"pg_catalog"}""",
        true
    )]
    [InlineData( /*lang=json,strict*/
        """{"schema":"information_schema"}""",
        true
    )]
    [InlineData( /*lang=json,strict*/
        """{"nspname":"public","schema":"pg_catalog"}""",
        false
    )]
    [InlineData( /*lang=json,strict*/
        """{"schema":"public"}""",
        false
    )]
    [InlineData( /*lang=json,strict*/
        """{"schema":null}""",
        false
    )]
    [InlineData( /*lang=json,strict*/
        """{"name":"pg_catalog"}""",
        false
    )]
    public void IsEngineRow_BySchema(string row, bool expected) =>
        PostgresFieldRules.IsEngineRow(JsonNode.Parse(row)!.AsObject()).ShouldBe(expected);

    // Every member of every call, at every depth, as CaptureJson.Options names it: an object and an array are members
    // too, and an element of an array is [] in the path.
    private static IEnumerable<(string Method, string Path, bool IsString)> AllMembers() =>
        Calls.SelectMany(static call =>
            MembersOf(call.Value.Request, "request")
                .Concat(MembersOf(call.Value.Answer, "answer"))
                .Select(found => (call.Key, found.Path, found.IsString))
        );

    private static IEnumerable<(string Path, bool IsString)> MembersOf(Type record, string path)
    {
        foreach (var property in record.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var at = path + "." + JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            foreach (var found in ValuesOf(property.PropertyType, at))
            {
                yield return found;
            }
        }
    }

    private static IEnumerable<(string Path, bool IsString)> ValuesOf(Type type, string path)
    {
        var plain = Nullable.GetUnderlyingType(type) ?? type;
        if (Scalars.Contains(plain))
        {
            yield return (path, plain == typeof(string));
            yield break;
        }

        yield return (path, false);
        var inner =
            plain.IsGenericType && plain.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
                ? ValuesOf(plain.GetGenericArguments()[0], path + "[]")
                : MembersOf(plain, path);
        foreach (var found in inner)
        {
            yield return found;
        }
    }

    // Whether the member or one that holds it is dropped, as CaptureRenamer.Drop removes it.
    private static bool IsUnderADrop(string method, string path)
    {
        for (var at = path; at.Length > 0; at = Parent(at))
        {
            if (PostgresFieldRules.RuleOf(method, at) is { Kind: FieldRuleKind.Drop })
            {
                return true;
            }
        }

        return false;
    }

    private static string Parent(string path)
    {
        if (path.EndsWith("[]", StringComparison.Ordinal))
        {
            return path[..^2];
        }

        var dot = path.LastIndexOf('.');
        return dot > 0 ? path[..dot] : string.Empty;
    }
}
