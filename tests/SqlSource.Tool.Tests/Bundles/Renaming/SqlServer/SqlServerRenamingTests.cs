using System;
using System.Linq;
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
    public void Spelled_IsTheSpellingAsWritten()
    {
        Engine.Spelled("EMAIL", false).ShouldBe("EMAIL");
        Engine.Spelled("EMAIL", true).ShouldBe("EMAIL");
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

    // A column of sys read through a linked server is the user's: the linked server's name is the user's, which the
    // renaming would keep with the row's other names.
    [Theory]
    [InlineData("columns")]
    [InlineData("operands")]
    [InlineData("table")]
    public void IsEngineRow_AColumnOfTheServersThroughALinkedServer_IsFalse(string method)
    {
        var row = new JsonObject
        {
            ["name"] = "database_id",
            ["sourceServer"] = "acme_link",
            ["sourceDatabase"] = "mssqlsystemresource",
            ["sourceSchema"] = "sys",
            ["sourceTable"] = "databases",
            ["sourceColumn"] = "database_id",
        };
        Engine.IsEngineRow(method, "answer.rows[]", row).ShouldBeFalse();
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

    // x__1 is a copy of x_ when the query reuses both x and x_, which no spelling tells, so it is read from reused.
    [Fact]
    public void CopiesIn_ACopyOfTwoParametersOfTheQuery_IsItsOwnParameter() =>
        Engine
            .CopiesIn("parameters", ParametersCall(("x", ["x_1", "x_2"]), ("x_", ["x__1", "x__2"])))
            .ShouldBe([
                new ParameterCopy("x_1", "x", "_1"),
                new ParameterCopy("x_2", "x", "_2"),
                new ParameterCopy("x__1", "x_", "_1"),
                new ParameterCopy("x__2", "x_", "_2"),
            ]);

    // ParameterCopies cuts a copy to fit 127 characters, as its own tests hold, so its parameter is no prefix of it.
    [Fact]
    public void CopiesIn_ACopyCutToFit_IsItsParameter()
    {
        var name = new string('a', ParameterCopies.MaximumCopyLength);
        var cut = new string('a', ParameterCopies.MaximumCopyLength - 2) + "_1";

        Engine
            .CopiesIn("parameters", ParametersCall((name, [cut])))
            .ShouldHaveSingleItem()
            .ShouldBe(new ParameterCopy(cut, name, "_1"));
    }

    // ParameterCopies passes over a number whose copy meets a name the text holds, which the renamed query's never do.
    [Fact]
    public void CopiesIn_ANumberPassedOver_IsSuffixedByPosition() =>
        Engine
            .CopiesIn("parameters", ParametersCall(("before", ["before_2", "before_3"])))
            .ShouldBe([new ParameterCopy("before_2", "before", "_1"), new ParameterCopy("before_3", "before", "_2")]);

    [Fact]
    public void CopiesIn_AnotherMethodOrNoReused_IsEmpty()
    {
        var call = ParametersCall(("before", ["before_1", "before_2"]));
        Engine.CopiesIn("probe", call).ShouldBeEmpty();
        Engine.CopiesIn("columns", call).ShouldBeEmpty();

        var request = new JsonObject { ["sql"] = "SELECT @before", ["params"] = null };
        var absent = new JsonObject { ["request"] = request };
        Engine.CopiesIn("parameters", absent).ShouldBeEmpty();
        request["reused"] = null;
        Engine.CopiesIn("parameters", absent).ShouldBeEmpty();
        Engine.CopiesIn("parameters", new JsonObject { ["request"] = new JsonArray() }).ShouldBeEmpty();
    }

    // An entry with a member of another JSON kind records no copy, and the others still do.
    [Fact]
    public void CopiesIn_AnEntryOfAnotherKind_RecordsNone()
    {
        var reused = new JsonArray(
            "before",
            new JsonObject { ["name"] = 1, ["copies"] = new JsonArray("x_1") },
            new JsonObject { ["name"] = "y", ["copies"] = "y_1" },
            new JsonObject { ["name"] = "z", ["copies"] = new JsonArray("z_1", 2) },
            new JsonObject { ["name"] = "before", ["copies"] = new JsonArray("before_1") }
        );
        var call = new JsonObject { ["request"] = new JsonObject { ["reused"] = reused } };

        Engine.CopiesIn("parameters", call).ShouldBe([new ParameterCopy("before_1", "before", "_1")]);
    }

    [Theory]
    [InlineData("1", 0, true)]
    [InlineData("$12", 0, true)]
    [InlineData("$", 0, true)]
    [InlineData("$ 1", 0, true)]
    [InlineData("$-1", 0, true)]
    [InlineData("$.5", 0, true)]
    [InlineData("$a", 0, false)]
    [InlineData("$_", 0, false)]
    [InlineData("$@", 0, false)]
    [InlineData("$#", 0, false)]
    [InlineData("$$", 0, false)]
    [InlineData("a", 0, false)]
    public void IsNumberStart_IsADigitOrTheSignOfAMoneyLiteral(string text, int at, bool expected) =>
        Engine.IsNumberStart(text, at, text.Length).ShouldBe(expected);

    // The server reads each of these as money, so the renaming keeps it as it keeps 12.
    [Theory]
    [InlineData("SELECT $12 AS price")]
    [InlineData("SELECT $12.50 AS price")]
    [InlineData("SELECT $12. AS price")]
    [InlineData("SELECT $ AS price")]
    [InlineData("SELECT $ 12 AS price")]
    [InlineData("SELECT $-12 AS price")]
    [InlineData("SELECT $+12 AS price")]
    [InlineData("SELECT $.5 AS price")]
    [InlineData("SELECT $-.5 AS price")]
    public void Sql_AMoneyLiteral_IsKept(string sql)
    {
        var (renamed, mapping, refusal) = RenameQuery(sql);

        refusal.ShouldBeNull();
        renamed.ShouldBe(
            sql.Replace("price", mapping.Name("price", quoted: false, NameClass.Alias), StringComparison.Ordinal)
        );
    }

    // The server reads $1e5 as $1 AS e5, as it reads 12abc as 12 AS abc, so the run is refused whole as 12abc is.
    [Theory]
    [InlineData("SELECT $1e5", "$1e5")]
    [InlineData("SELECT $1_000", "$1_000")]
    [InlineData("SELECT $12abc", "$12abc")]
    [InlineData("SELECT $1.2.3", "$1.2.3")]
    public void Sql_AMoneyLiteralWithMore_IsRefused(string sql, string token)
    {
        var (_, _, refusal) = RenameQuery(sql);

        refusal.ShouldBe(new RenamingRefusal(RefusalStep.Token, "query.sql", token));
    }

    [Fact]
    public void Sql_ADollarThatAWordCharacterFollows_IsAWord()
    {
        var (renamed, mapping, refusal) = RenameQuery("SELECT $action, $secret, x$1 FROM t");

        refusal.ShouldBeNull();
        var secret = mapping.Name("$secret", quoted: false, NameClass.Alias);
        var x = mapping.Name("x$1", quoted: false, NameClass.Alias);
        var table = mapping.Name("t", quoted: false, NameClass.Alias);
        renamed.ShouldBe($"SELECT $action, {secret}, {x} FROM {table}");
    }

    [Theory]
    [InlineData("SELECT $")]
    [InlineData("SELECT 1 /*+ $*/")]
    public void Sql_ADollarAtTheEnd_IsKept(string sql)
    {
        var (renamed, _, refusal) = RenameQuery(sql);

        refusal.ShouldBeNull();
        renamed.ShouldBe(sql);
    }

    [Fact]
    public void Check_AMoneyLiteral_PassesAsANumber()
    {
        var mapping = new NameMapping(RenamingBundles.Key(1), Engine.Fold, Engine.Spelled);
        var query = mapping.Name("GetPrice", quoted: false, NameClass.Query);
        var price = mapping.Name("price", quoted: false, NameClass.Alias);

        TokenCheck
            .Check(Engine, mapping, [], $"-- name: {query}\nSELECT $12.50 AS {price}, $-1", expected: null)
            .ShouldBeNull();
        TokenCheck
            .Check(Engine, mapping, [], $"-- name: {query}\nSELECT $1e5", expected: null)
            .ShouldBe(new RenamingRefusal(RefusalStep.Token, "query.sql", "$1e5"));
    }

    [Fact]
    public void RenameOwn_AMessage_IsItsParametersRenamed()
    {
        var mapping = new NameMapping(RenamingBundles.Key(1), Engine.Fold, Engine.Spelled);
        var call = ParametersCall(("label", ["label_1", "label_2"]), ("before", ["before_1", "before_2"]));
        var copies = RenamingCopies.Of(
            Engine,
            [new RenamingCall(1, "parameters", call, call)],
            static found => found.Original
        );
        var run = RenamingRun.For(Engine, mapping, "SELECT @label, @label, @before, @before", copies);
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

    // A call of parameters whose request records the reused parameters given, each with its copies.
    private static JsonObject ParametersCall(params (string Name, string[] Copies)[] reused) =>
        new()
        {
            ["request"] = new JsonObject
            {
                ["sql"] = "SELECT 1",
                ["params"] = null,
                ["reused"] = new JsonArray([
                    .. reused.Select(static entry => new JsonObject
                    {
                        ["name"] = entry.Name,
                        ["copies"] = new JsonArray([.. entry.Copies.Select(static copy => (JsonNode)copy)]),
                    }),
                ]),
            },
            ["answer"] = new JsonObject(),
        };

    // The SQL renamed as query.sql's body, its mapping and the run's refusal.
    private static (string Renamed, NameMapping Mapping, RenamingRefusal? Refusal) RenameQuery(string sql)
    {
        var mapping = new NameMapping(RenamingBundles.Key(1), Engine.Fold, Engine.Spelled);
        var run = RenamingRun.For(Engine, mapping, sql, RenamingCopies.None);
        var renamed = run.At("query.sql").Sql(sql, NameClass.Alias).Text;
        return (renamed, mapping, run.Refusal);
    }
}
