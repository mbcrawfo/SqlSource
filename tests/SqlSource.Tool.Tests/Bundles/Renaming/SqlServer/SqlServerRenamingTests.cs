using System.Collections.Generic;
using System.Text.Json.Nodes;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Bundles.Renaming.SqlServer;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming.SqlServer;

public sealed class SqlServerRenamingTests
{
    private const string HierarchyIdOfVersion10 =
        "Microsoft.SqlServer.Types.SqlHierarchyId, Microsoft.SqlServer.Types, Version=10.0.0.0, Culture=neutral, "
        + "PublicKeyToken=89845dcd8080cc91";

    private static readonly SqlServerRenaming Engine = SqlServerRenaming.Instance;

    // The query's parameters, compared as the renaming compares them.
    private static readonly IReadOnlySet<string> Before = new HashSet<string>(["before"], SqlParameterName.Comparer);

    [Fact]
    public void Dialect_IsSqlServers() => Engine.Dialect.ShouldBe(SqlDialect.SqlServer);

    [Theory]
    [InlineData('a', true)]
    [InlineData('é', true)]
    [InlineData('_', true)]
    [InlineData('@', true)]
    [InlineData('#', true)]
    [InlineData('$', true)]
    [InlineData('.', false)]
    [InlineData(' ', false)]
    [InlineData('[', false)]
    public void IsWordStartAndPart_AreTheServersNameCharacters(char character, bool expected)
    {
        Engine.IsWordStart(character).ShouldBe(expected);
        Engine.IsWordPart(character).ShouldBe(expected);
    }

    [Fact]
    public void Fold_IgnoresCaseAndQuoting()
    {
        Engine.Fold("Users", false).ShouldBe(Engine.Fold("USERS", true));
        Engine.Fold("Café", true).ShouldBe("CAFÉ");
    }

    [Theory]
    [InlineData("select", false)]
    [InlineData("SeLeCt", true)]
    [InlineData("msdb", false)]
    [InlineData("1", true)]
    [InlineData("42", false)]
    [InlineData(HierarchyIdOfVersion10, true)]
    [InlineData(
        "MICROSOFT.SQLSERVER.TYPES.SQLGEOGRAPHY, MICROSOFT.SQLSERVER.TYPES, VERSION=16.0.0.0, CULTURE=NEUTRAL, "
            + "PUBLICKEYTOKEN=89845DCD8080CC91",
        true
    )]
    public void IsEngineWord_TheServersOrTheToolsWord_IsTrue(string spelling, bool quoted) =>
        Engine.IsEngineWord(spelling, quoted).ShouldBeTrue(spelling);

    [Theory]
    [InlineData("users", false)]
    [InlineData("1a", true)]
    [InlineData("2 x", true)]
    [InlineData("", true)]
    [InlineData("١", true)]
    [InlineData("MyCompany.Money, MyCompany, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", true)]
    [InlineData(
        "Microsoft.SqlServer.Types.SqlHierarchyId, Microsoft.SqlServer.Types, Version=, Culture=neutral, "
            + "PublicKeyToken=89845dcd8080cc91",
        true
    )]
    [InlineData(
        "Microsoft.SqlServer.Types.SqlHierarchyId, Microsoft.SqlServer.Types, Version=10.0.0.0, Culture=neutral, "
            + "PublicKeyToken=0000000000000000",
        true
    )]
    [InlineData(
        "Microsoft.SqlServer.Types.SqlMoney, Microsoft.SqlServer.Types, Version=10.0.0.0, Culture=neutral, "
            + "PublicKeyToken=89845dcd8080cc91",
        true
    )]
    public void IsEngineWord_AnotherSpelling_IsFalse(string spelling, bool quoted) =>
        Engine.IsEngineWord(spelling, quoted).ShouldBeFalse(spelling);

    [Fact]
    public void RuleOf_IsTheFieldsTables()
    {
        Engine.RuleOf("open", "answer.database").ShouldBe(FieldRule.Name(NameClass.Database));
        Engine.RuleOf("columns", "answer.rows[].unknown").ShouldBeNull();
    }

    [Theory]
    [InlineData("columns", "sourceDatabase", "msdb")]
    [InlineData("operands", "sourceDatabase", "MSSQLSystemResource")]
    [InlineData("table", "sourceSchema", "INFORMATION_SCHEMA")]
    [InlineData("columns", "sourceSchema", "Sys")]
    public void IsEngineRow_ARowOfTheServers_IsTrue(string method, string member, string value)
    {
        var row = new JsonObject { ["sourceDatabase"] = "fixtures", ["sourceSchema"] = "dbo" };
        row[member] = value;
        Engine.IsEngineRow(method, "answer.rows[]", row).ShouldBeTrue();
    }

    [Theory]
    [InlineData("sys")]
    [InlineData("INFORMATION_SCHEMA")]
    public void IsEngineRow_ATableRequestOfTheServersSchema_IsTrue(string schema) =>
        Engine
            .IsEngineRow("table", "request", new JsonObject { ["schema"] = schema, ["table"] = "objects" })
            .ShouldBeTrue();

    [Fact]
    public void IsEngineRow_AnyOtherRow_IsFalse()
    {
        var users = new JsonObject
        {
            ["sourceDatabase"] = "fixtures",
            ["sourceSchema"] = "dbo",
            ["sourceTable"] = "users",
        };
        Engine.IsEngineRow("columns", "answer.rows[]", users).ShouldBeFalse();
        Engine.IsEngineRow("columns", "answer.rows[]", new JsonObject { ["sourceSchema"] = null }).ShouldBeFalse();

        var system = new JsonObject { ["sourceDatabase"] = "msdb", ["sourceSchema"] = "sys" };
        Engine.IsEngineRow("parameters", "answer.rows[]", system).ShouldBeFalse();
        Engine.IsEngineRow("columns", "answer.errors[]", system).ShouldBeFalse();
        Engine
            .IsEngineRow("table", "request", new JsonObject { ["schema"] = "dbo", ["table"] = "users" })
            .ShouldBeFalse();
        Engine.IsEngineRow("columns", "request", new JsonObject { ["schema"] = "sys" }).ShouldBeFalse();
    }

    [Fact]
    public void CopyOf_ACopyOfAQueryParameter_IsTheParameterAndOneSeparator()
    {
        Engine.CopyOf("before_1", Before).ShouldBe(("before", "_1"));
        Engine.CopyOf("Before__12", Before).ShouldBe(("Before", "_12"));
    }

    [Fact]
    public void CopyOf_AParameterWhoseNameEndsInASeparator_IsThatParameter() =>
        Engine
            .CopyOf("before___3", new HashSet<string>(["before_"], SqlParameterName.Comparer))
            .ShouldBe(("before_", "_3"));

    [Theory]
    [InlineData("before_x")]
    [InlineData("other_1")]
    [InlineData("before_")]
    [InlineData("before1")]
    [InlineData("_1")]
    [InlineData("before_1x")]
    [InlineData("before_١")]
    public void CopyOf_AnyOtherName_IsNull(string name) => Engine.CopyOf(name, Before).ShouldBeNull(name);

    [Fact]
    public void RenameOwn_AMessage_IsItsParametersRenamed()
    {
        var mapping = new NameMapping(RenamingBundles.Key(1), Engine.Fold);
        var run = RenamingRun.For(Engine, mapping, "SELECT @label, @label");
        run.RegisterParameters();
        var field = new RenamingField(
            0,
            "parameters",
            "answer.error.errors[].message",
            "'@Label_1' and @@ERROR, and @before_2",
            null
        );

        var renamed = Engine.RenameOwn(field, run.At("message"));

        // BEFORE is a keyword of the server's, kept as the user's name spelled as one is.
        var label = mapping.Name("label", quoted: false, NameClass.Parameter);
        renamed.ShouldBe($"@{label} @before");
        Engine.WordsOf(field with { Value = renamed }).ShouldBe(["@", label, "@", "before"]);
    }
}
