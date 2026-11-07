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

    // A money literal starts with $, which starts a word by the engine's word rule, so it is renamed to a name.  This
    // is TD-0103.
    [Fact]
    public void Sql_AMoneyLiteral_IsRenamedToAName()
    {
        const string Sql = "SELECT $12 AS price";
        var mapping = new NameMapping(RenamingBundles.Key(1), Engine.Fold);
        var run = RenamingRun.For(Engine, mapping, Sql, RenamingCopies.None);

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
}
