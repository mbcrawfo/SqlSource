using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using Shouldly;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Bundles.Renaming.SqlServer;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming.SqlServer;

// The rule of each member of SqlServerCalls.cs' records and of the entry's SQL Server type that is written out here,
// a rule for every string that the SQL Server fixtures' captures hold, and, by walking the records, a rule for every
// string member of every call and a call member for every rule of the table.
public sealed class SqlServerFieldsTests
{
    // The records of each call, as SqlServerSession asks it.
    private static readonly Dictionary<string, (Type Request, Type Answer)> Calls = new(StringComparer.Ordinal)
    {
        [SqlServerCallNames.Open] = (typeof(SqlServerOpenRequest), typeof(SqlServerOpenAnswer)),
        [SqlServerCallNames.Parameters] = (typeof(SqlServerParametersRequest), typeof(SqlServerParametersAnswer)),
        [SqlServerCallNames.Probe] = (typeof(SqlServerProbeRequest), typeof(SqlServerProbeAnswer)),
        [SqlServerCallNames.Declared] = (typeof(SqlServerDeclaredRequest), typeof(SqlServerDeclaredAnswer)),
        [SqlServerCallNames.Columns] = (typeof(SqlServerColumnsRequest), typeof(SqlServerColumnsAnswer)),
        [SqlServerCallNames.Operands] = (typeof(SqlServerColumnsRequest), typeof(SqlServerColumnsAnswer)),
        [SqlServerCallNames.Table] = (typeof(SqlServerTableRequest), typeof(SqlServerColumnsAnswer)),
    };

    private static readonly string[] Methods =
    [
        "open",
        "parameters",
        "probe",
        "declared",
        "columns",
        "operands",
        "table",
    ];

    private static readonly string[] SqlMethods = ["parameters", "probe", "declared", "columns", "operands"];

    private static readonly string[] DescribeMethods = ["declared", "columns", "operands", "table"];

    private static readonly string[] ColumnMethods = ["columns", "operands", "table"];

    // The rule of each member, by "<method> <path>".
    private static readonly Dictionary<string, FieldRule> Table = Build();

    public static TheoryData<string> Members => [.. Table.Keys];

    [Theory]
    [MemberData(nameof(Members))]
    public void RuleOf_AMemberOfTheRecords_IsItsRule(string member)
    {
        var space = member.IndexOf(' ', StringComparison.Ordinal);
        SqlServerFields.RuleOf(member[..space], member[(space + 1)..]).ShouldBe(Table[member], member);
    }

    // A call that is added fails here until its records are mapped above, and so are walked below.
    [Fact]
    public void Calls_AreEveryCallOfTheDescriber() =>
        Calls
            .Keys.Order(StringComparer.Ordinal)
            .ShouldBe(
                typeof(SqlServerCallNames)
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
        foreach (var (method, path, isString) in CallMembers.Of(Calls))
        {
            if (
                isString
                && !CallMembers.IsUnderADrop(SqlServerFields.RuleOf, method, path)
                && SqlServerFields.RuleOf(method, path) is null
            )
            {
                missing.Add(method + " " + path);
            }
        }

        missing.ShouldBeEmpty();
    }

    [Fact]
    public void RuleOf_EveryRuleOfTheTable_IsOfAMemberThatExists()
    {
        var members = CallMembers.Of(Calls).Select(static found => found.Method + " " + found.Path).ToHashSet();

        SqlServerFields.Members.Where(member => !members.Contains(member)).ShouldBeEmpty();
    }

    [Fact]
    public void RuleOf_EveryStringOfTheFixtures_HasARule()
    {
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var fixture in FixtureCases.Of("mssql"))
        {
            foreach (var call in JsonNode.Parse(fixture.Capture)!["calls"]!.AsArray())
            {
                var method = call!["method"]!.GetValue<string>();
                foreach (var part in (string[])["request", "answer"])
                {
                    JsonStrings.Visit(
                        call[part],
                        part,
                        found =>
                        {
                            if (SqlServerFields.RuleOf(method, found.Path) is null)
                            {
                                _ = missing.Add($"{method} {found.Path}");
                            }

                            return null;
                        }
                    );
                }
            }
        }

        missing.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("columns", "answer.rows[].unknown")]
    [InlineData("table", "request.sql")]
    [InlineData("probe", "answer.rows[].name")]
    [InlineData("open", "answer.errors[].message")]
    [InlineData("parameters", "answer.rows[].systemTypeName")]
    [InlineData("columns", "answer.rows[].suggestedSystemTypeName")]
    [InlineData("declared", "answer.rows[].sourceSchema")]
    [InlineData("other", "request.sql")]
    [InlineData(BundleFiles.Expected, "parameters[].type.element.name")]
    public void RuleOf_AnyOtherMember_IsNull(string method, string path) =>
        SqlServerFields.RuleOf(method, path).ShouldBeNull($"{method} {path}");

    // Every member written out, apart from the table SqlServerFields builds.
    private static Dictionary<string, FieldRule> Build()
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
            rules[$"{method} answer.error.errors[].message"] = FieldRule.Own;
        }

        foreach (var method in SqlMethods)
        {
            rules[$"{method} request.sql"] = FieldRule.Sql;
            rules[$"{method} request.params"] = FieldRule.TypeSpelling;
        }

        foreach (var method in DescribeMethods)
        {
            rules[$"{method} answer.errors[].message"] = FieldRule.Own;
            rules[$"{method} answer.rows[].systemTypeName"] = FieldRule.TypeSpelling;
            rules[$"{method} answer.rows[].userTypeSchema"] = FieldRule.Name(NameClass.Schema);
            rules[$"{method} answer.rows[].userTypeName"] = FieldRule.Name(NameClass.Type);
            rules[$"{method} answer.rows[].assemblyQualifiedTypeName"] = FieldRule.WholeName(NameClass.Type);
        }

        foreach (var method in ColumnMethods)
        {
            rules[$"{method} answer.rows[].sourceServer"] = FieldRule.Name(NameClass.Database);
            rules[$"{method} answer.rows[].sourceDatabase"] = FieldRule.Name(NameClass.Database);
            rules[$"{method} answer.rows[].name"] = FieldRule.Name(NameClass.Column);
            rules[$"{method} answer.rows[].sourceSchema"] = FieldRule.Name(NameClass.Schema);
            rules[$"{method} answer.rows[].sourceTable"] = FieldRule.Name(NameClass.Table);
            rules[$"{method} answer.rows[].sourceColumn"] = FieldRule.Name(NameClass.Column);
            rules[$"{method} answer.rows[].sourceType"] = FieldRule.Closed;
        }

        foreach (var record in (string[])["parameters[].type.", "columns[].type."])
        {
            rules[$"{BundleFiles.Expected} {record}name"] = FieldRule.TypeSpelling;
            rules[$"{BundleFiles.Expected} {record}bindName"] = FieldRule.TypeSpelling;
            rules[$"{BundleFiles.Expected} {record}userType.schema"] = FieldRule.Name(NameClass.Schema);
            rules[$"{BundleFiles.Expected} {record}userType.name"] = FieldRule.Name(NameClass.Type);
            rules[$"{BundleFiles.Expected} {record}userType.assemblyQualifiedName"] = FieldRule.WholeName(
                NameClass.Type
            );
        }

        return rules;
    }
}
