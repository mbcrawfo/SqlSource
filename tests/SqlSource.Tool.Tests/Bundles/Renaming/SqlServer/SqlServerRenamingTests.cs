using System.Collections.Generic;
using System.Text.Json.Nodes;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Bundles.Renaming.SqlServer;
using SqlSource.Tool.Describing.SqlServer.Statements;
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
        "Microsoft.SqlServer.Types.SqlHierarchyId, Microsoft.SqlServer.Types, Version=abc, Culture=neutral, "
            + "PublicKeyToken=89845dcd8080cc91",
        true
    )]
    [InlineData(
        "Microsoft.SqlServer.Types.SqlHierarchyId, Microsoft.SqlServer.Types, Version=1..0, Culture=neutral, "
            + "PublicKeyToken=89845dcd8080cc91",
        true
    )]
    [InlineData(
        "Microsoft.SqlServer.Types.SqlHierarchyId, Microsoft.SqlServer.Types, Version=.1, Culture=neutral, "
            + "PublicKeyToken=89845dcd8080cc91",
        true
    )]
    [InlineData(
        "Microsoft.SqlServer.Types.SqlHierarchyId, Microsoft.SqlServer.Types, Version=1., Culture=neutral, "
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
    [InlineData("columns", "master", "sys")]
    [InlineData("columns", "fixtures", "Sys")]
    [InlineData("operands", "fixtures", "INFORMATION_SCHEMA")]
    [InlineData("table", "msdb", "information_schema")]
    [InlineData("columns", "mssqlsystemresource", "sys")]
    [InlineData("operands", "MSSQLSystemResource", "dbo")]
    [InlineData("table", "mssqlsystemresource", "sales")]
    [InlineData("columns", "mssqlsystemresource", null)]
    public void IsEngineRow_ARowOfTheServers_IsTrue(string method, string database, string? schema)
    {
        var row = new JsonObject { ["sourceDatabase"] = database, ["sourceSchema"] = schema };
        Engine.IsEngineRow(method, "answer.rows[]", row).ShouldBeTrue();
    }

    // A column of the server's under its own name, folded, with or without a nullability mark, under the tool's AS [n],
    // or under no name.
    [Theory]
    [InlineData("database_id", "database_id")]
    [InlineData("DATABASE_ID", "database_id")]
    [InlineData("database_id!", "database_id")]
    [InlineData("database_id?", "database_id")]
    [InlineData("1", "database_id")]
    [InlineData("12", "database_id")]
    [InlineData(null, "database_id")]
    public void IsEngineRow_AColumnOfTheServersUnderItsOwnName_IsTrue(string? name, string column)
    {
        var row = new JsonObject
        {
            ["name"] = name,
            ["sourceDatabase"] = "mssqlsystemresource",
            ["sourceSchema"] = "sys",
            ["sourceTable"] = "databases",
            ["sourceColumn"] = column,
        };
        Engine.IsEngineRow("columns", "answer.rows[]", row).ShouldBeTrue();
        Engine.IsEngineRow("operands", "answer.rows[]", row).ShouldBeTrue();
    }

    // A column of the server's under an alias is the user's: the alias is the user's name, which the renaming would
    // keep with the row's other names.
    [Theory]
    [InlineData("acme_payroll_db", "database_id")]
    [InlineData("Acme Payroll", "database_id")]
    [InlineData("acme_payroll_db!", "database_id")]
    [InlineData("database_id_", "database_id")]
    [InlineData("database_id!!", "database_id")]
    [InlineData("!", "database_id")]
    [InlineData("1a", "database_id")]
    [InlineData("١", "database_id")]
    [InlineData("database_id", null)]
    [InlineData("secret_alias", "COLUMN_NAME")]
    public void IsEngineRow_AColumnOfTheServersUnderAnAlias_IsFalse(string name, string? column)
    {
        var row = new JsonObject
        {
            ["name"] = name,
            ["sourceDatabase"] = "mssqlsystemresource",
            ["sourceSchema"] = column == "COLUMN_NAME" ? "INFORMATION_SCHEMA" : "sys",
            ["sourceColumn"] = column,
        };
        Engine.IsEngineRow("columns", "answer.rows[]", row).ShouldBeFalse(name);
    }

    // A user's tables can be in a system database, as they are when a connection string names master, so a row's
    // database alone does not make it the engine's.
    [Theory]
    [InlineData("columns", "master")]
    [InlineData("operands", "msdb")]
    [InlineData("table", "model")]
    [InlineData("columns", "tempdb")]
    [InlineData("columns", "MASTER")]
    public void IsEngineRow_ADboRowOfASystemDatabase_IsFalse(string method, string database)
    {
        var row = new JsonObject
        {
            ["sourceDatabase"] = database,
            ["sourceSchema"] = "dbo",
            ["sourceTable"] = "users",
        };
        Engine.IsEngineRow(method, "answer.rows[]", row).ShouldBeFalse();
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

    // A copy that ParameterCopies cut to fit 127 characters, as its own tests hold, has no parameter before its
    // separator, so it is not read back and the proof refuses the bundle.  This is TD-0104.
    [Fact]
    public void CopyOf_ACopyCutToFit_IsNull()
    {
        var name = new string('a', ParameterCopies.MaximumCopyLength);
        var cut = new string('a', ParameterCopies.MaximumCopyLength - 2) + "_1";

        Engine.CopyOf(cut, new HashSet<string>([name], SqlParameterName.Comparer)).ShouldBeNull();
    }

    // A money literal starts with $, which starts a word by the engine's word rule, so it is renamed to a name.  This
    // is TD-0103.
    [Fact]
    public void Sql_AMoneyLiteral_IsRenamedToAName()
    {
        const string Sql = "SELECT $12 AS price";
        var mapping = new NameMapping(RenamingBundles.Key(1), Engine.Fold);
        var run = RenamingRun.For(Engine, mapping, Sql);

        var renamed = run.At("query.sql").Sql(Sql, NameClass.Alias).Text;

        run.Refusal.ShouldBeNull();
        mapping.TryGet("$12", quoted: false, out var money).ShouldBeTrue();
        money.ShouldMatch("^a_[0-9a-f]{8}$");
        renamed.ShouldBe($"SELECT {money} AS {mapping.Name("price", quoted: false, NameClass.Alias)}");
    }

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
