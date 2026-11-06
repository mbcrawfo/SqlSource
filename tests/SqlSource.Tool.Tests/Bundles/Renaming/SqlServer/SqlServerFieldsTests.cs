using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Shouldly;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Bundles.Renaming.SqlServer;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming.SqlServer;

// Every string member of SqlServerCalls.cs' records and of the entry's SQL Server type has its rule.
public sealed class SqlServerFieldsTests
{
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
