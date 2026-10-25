using System;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;
using static SqlSource.Tests.Parsing.ParseOf;

namespace SqlSource.Tests.Parsing;

// The parser's parameters: those of the SQL, declared variables and param markers.
public sealed class SqlFileParserParameterTests
{
    [Theory]
    [InlineData("SELECT @a, @b, @a", new[] { "a", "b" })]
    [InlineData("SELECT @Id, @ID, @id", new[] { "Id" })]
    [InlineData("SELECT '@x', @y -- @z", new[] { "y" })]
    [InlineData("SELECT @@ROWCOUNT, a @> b", new string[0])]
    public void Parse_Parameters_AreThoseOfTheSqlInOrderOfFirstAppearance(string sql, string[] expected)
    {
        var block = Blocks("-- name: Q\n" + sql + "\n").ShouldHaveSingleItem();

        block.Parameters.Select(static parameter => parameter.Name).ShouldBe(expected);
        block.Parameters.ShouldAllBe(static parameter =>
            parameter.Type == null && parameter.Nullable == null && !parameter.IsDeclared
        );
        Sql(block).ShouldBe(sql.Replace(" -- @z", string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_KeptComment_HoldsNoParameter() =>
        Blocks("-- name: Q\n-- generator: keep-comments\nSELECT @a -- @b\n")
            .ShouldHaveSingleItem()
            .Parameters.Select(static parameter => parameter.Name)
            .ShouldBe(["a"]);

    // SQL Server rejects a parameter with the name of a variable that the batch declares, so the name is never both.
    [Fact]
    public void Parse_VariableThatSqlServerSqlDeclares_IsNotAParameterWhereverItIsWritten()
    {
        const string Sql = "DECLARE @n int = @start, @m int;\nSELECT @N + @m, @end WHERE x > @n";

        var block = Blocks("-- name: Q\n" + Sql + "\n", dialect: SqlDialect.SqlServer).ShouldHaveSingleItem();

        Parameters(block).ShouldBe(["start:<none>:<unsaid>", "end:<none>:<unsaid>"]);
        ParseOf.Sql(block).ShouldBe(Sql);
    }

    // A query is one batch.
    [Fact]
    public void Parse_VariableThatAnotherQueryOfTheFileDeclares_IsAParameter()
    {
        var blocks = Blocks(
            "-- name: A\nDECLARE @n int = 1;\nSELECT @n\n-- name: B\nSELECT @n\n",
            dialect: SqlDialect.SqlServer
        );

        blocks
            .Select(Parameters)
            .ShouldBe([
                [],
                ["n:<none>:<unsaid>"],
            ]);
    }

    // The comments of a query are no part of its SQL, and a marker is a comment.
    [Fact]
    public void Parse_DeclarationWithCommentsAndAMarkerInside_IsStillRead() =>
        Blocks(
                "-- name: Q\nDECLARE /* first */ @n int, -- then\n-- summary: S.\n    @m int;\nSELECT @n, @m, @p\n",
                dialect: SqlDialect.SqlServer
            )
            .ShouldHaveSingleItem()
            .Parameters.Select(static parameter => parameter.Name)
            .ShouldBe(["p"]);

    [Theory]
    [InlineData(nameof(SqlDialect.Ansi))]
    [InlineData(nameof(SqlDialect.PostgreSql))]
    [InlineData(nameof(SqlDialect.CockroachDb))]
    [InlineData(nameof(SqlDialect.MySql))]
    [InlineData(nameof(SqlDialect.MariaDb))]
    [InlineData(nameof(SqlDialect.Sqlite))]
    [InlineData(nameof(SqlDialect.Oracle))]
    public void Parse_DeclareUnderADialectWithoutDeclaredVariables_LeavesTheNameAParameter(string dialect) =>
        Blocks("-- name: Q\nDECLARE @n int = @start;\nSELECT @n\n", dialect: Enum.Parse<SqlDialect>(dialect))
            .ShouldHaveSingleItem()
            .Parameters.Select(static parameter => parameter.Name)
            .ShouldBe(["n", "start"]);

    // A variable in a default is not a parameter that a token brings, so it needs no declaration.
    [Fact]
    public void Parse_DeclaredVariableInADefault_NeedsNoParamMarker() =>
        Blocks(
                "-- name: Q\n-- token: {{g:OFFSET @n}}\nDECLARE @n int = 1;\nSELECT 1 {{f:LIMIT @N}} {{g}}\n",
                dialect: SqlDialect.SqlServer
            )
            .ShouldHaveSingleItem()
            .Parameters.ShouldBeEmpty();

    [Fact]
    public void Parse_ParameterInADefaultBesideADeclaredVariable_MustStillBeDeclared()
    {
        const string Text = "-- name: Q\nDECLARE @n int = 1;\nSELECT 1 {{f:LIMIT @n OFFSET @b}}\n";

        Errors(Text, dialect: SqlDialect.SqlServer)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.UndeclaredParameter, TextSpans.Of(Text, "@b"), "@b")]);
    }

    [Fact]
    public void Parse_InputModelOnAQueryWhoseOnlyNamesAreDeclaredVariables_IsAnError()
    {
        const string Text = "-- name: Q\n-- input-model: FindArgs\nDECLARE @n int = 1;\nSELECT @n\n";

        Errors(Text, dialect: SqlDialect.SqlServer)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.InputModelWithoutParameters,
                    TextSpans.Of(Text, "-- input-model: FindArgs")
                ),
            ]);
    }

    // The way back for a name that is read as a variable and is not one.
    [Fact]
    public void Parse_ParamMarkerWithATypeForADeclaredVariable_MakesItAParameter() =>
        Parameters(
                Blocks("-- name: Q\n-- param: @n int\nDECLARE @n int;\nSELECT @n, @a\n", dialect: SqlDialect.SqlServer)
                    .ShouldHaveSingleItem()
            )
            .ShouldBe(["a:<none>:<unsaid>", "n:int:<unsaid>:declared"]);

    private static string[] Parameters(SqlBlock block) =>
        [
            .. block.Parameters.Select(static parameter =>
                parameter.Name
                + ":"
                + (parameter.Type ?? "<none>")
                + ":"
                + Nullability(parameter.Nullable)
                + (parameter.IsDeclared ? ":declared" : string.Empty)
            ),
        ];

    private static string Nullability(bool? nullable)
    {
        if (nullable is not { } value)
        {
            return "<unsaid>";
        }

        return value ? "null" : "not null";
    }

    [Theory]
    [InlineData("@a", "a:<none>:<unsaid>:declared")]
    [InlineData("@a int", "a:int:<unsaid>:declared")]
    [InlineData("@a null", "a:<none>:null:declared")]
    [InlineData("@a not null", "a:<none>:not null:declared")]
    [InlineData("@a timestamptz null", "a:timestamptz:null:declared")]
    [InlineData("@a decimal(18, 2) not null", "a:decimal(18, 2):not null:declared")]
    [InlineData("@a double precision", "a:double precision:<unsaid>:declared")]
    // As people type it.
    [InlineData("@a\tint\tNULL", "a:int:null:declared")]
    [InlineData("@a int NOT  NULL", "a:int:not null:declared")]
    [InlineData("@A int", "a:int:<unsaid>:declared")]
    // A type that only ends in the letters of the word.
    [InlineData("@a mynull", "a:mynull:<unsaid>:declared")]
    [InlineData("@a knot null", "a:knot:null:declared")]
    public void Parse_ParamMarker_GivesItsParameterATypeAndNullability(string value, string expected) =>
        Parameters(Blocks("-- name: Q\n-- param: " + value + "\nSELECT @a\n").ShouldHaveSingleItem())
            .ShouldBe([expected]);

    [Fact]
    public void Parse_ParamMarkerWithWindowsLineEndings_IsRead() =>
        Parameters(Blocks("-- name: Q\r\n-- param: @a int null  \r\nSELECT @a\r\n").ShouldHaveSingleItem())
            .ShouldBe(["a:int:null:declared"]);

    [Fact]
    public void Parse_ParameterList_IsTheSqlsParametersThenTheDeclaredOnesInMarkerOrder()
    {
        const string Text =
            "-- name: Q\n-- param: @z int\n-- param: @b text null\n-- param: @y int\nSELECT @a, @b {{f}}\n";

        Parameters(Blocks(Text).ShouldHaveSingleItem())
            .ShouldBe([
                "a:<none>:<unsaid>",
                "b:text:null:declared",
                "z:int:<unsaid>:declared",
                "y:int:<unsaid>:declared",
            ]);
    }

    [Fact]
    public void Parse_TheSameDeclarationTwice_IsNotAConflict() =>
        Parameters(
                Blocks("-- name: Q\n-- param: @a int null\n-- param: @A int null\nSELECT @a\n").ShouldHaveSingleItem()
            )
            .ShouldBe(["a:int:null:declared"]);

    [Theory]
    [InlineData("@a text")]
    [InlineData("@a int null")]
    [InlineData("@a INT")]
    [InlineData("@a")]
    public void Parse_TwoDeclarationsOfOneParameterThatDiffer_Conflict(string second)
    {
        var text = "-- name: Q\n-- param: @a int\n-- param: " + second + "\nSELECT @a\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.ConflictingSettings,
                    new TextSpan(
                        text.IndexOf("-- param: " + second + "\n", StringComparison.Ordinal) + 10,
                        second.Length
                    ),
                    "param: " + second
                ),
            ]);
    }

    [Theory]
    [InlineData("-- param: @a -> UserId", null, null, "UserId")]
    [InlineData("-- param: @a null -> UserId", null, true, "UserId")]
    [InlineData("-- param: @a int4 not null -> App.UserId", "int4", false, "global::App.UserId")]
    [InlineData(
        "-- param: @a double precision -> System.Collections.Generic.List<UserId>",
        "double precision",
        null,
        "global::System.Collections.Generic.List<UserId>"
    )]
    // As people type it.
    [InlineData("-- param: @a\t->\tUserId  ", null, null, "UserId")]
    [InlineData("-- param: @a int4 ->UserId", "int4", null, "UserId")]
    [InlineData("-- param: @a NOT NULL -> global::UserId", null, false, "global::UserId")]
    // A -> inside quotes, or that does not stand after white space, is part of the type.
    [InlineData("-- param: @a \"odd -> name\" -> UserId", "\"odd -> name\"", null, "UserId")]
    [InlineData("-- param: @a my->type", "my->type", null, null)]
    public void Parse_ParamMarkerWithACSharpType_GivesItsParameterThatType(
        string marker,
        string? type,
        bool? nullable,
        string? clrType
    )
    {
        var parameter = Blocks("-- name: Q\n" + marker + "\nSELECT @a\n")
            .ShouldHaveSingleItem()
            .Parameters.ShouldHaveSingleItem();

        parameter.ShouldBe(new SqlQueryParameter("a", type, nullable, true, clrType));
    }

    // A parameter that only the marker declares keeps its C# type too.
    [Fact]
    public void Parse_DeclaredOnlyParameterWithACSharpType_KeepsIt() =>
        Blocks("-- name: Q\n-- param: @page int4 -> PageNumber\nSELECT 1 {{tail:LIMIT @page}}\n")
            .ShouldHaveSingleItem()
            .Parameters.ShouldBe([new SqlQueryParameter("page", "int4", null, true, "PageNumber")]);

    [Theory]
    [InlineData("-- param: @a -> default")]
    [InlineData("-- param: @a -> Default")]
    [InlineData("-- param: @a -> int?")]
    [InlineData("-- param: @a ->")]
    [InlineData("-- param: @a int4 -> UserId more")]
    [InlineData("-- param: @a -> -> UserId")]
    public void Parse_ParamMarker_BadCSharpType_IsSQLSRC111(string marker)
    {
        var text = "-- name: Q\n" + marker + "\nSELECT @a\n";
        var value = marker["-- param: ".Length..];

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.InvalidMarkerValue,
                    TextSpans.Of(text, value),
                    "param: " + value,
                    ""
                ),
            ]);
    }

    [Theory]
    [InlineData("@a -> OtherId")]
    [InlineData("@a -> App.UserId")]
    [InlineData("@a")]
    public void Parse_TwoParamMarkers_DifferentCSharpTypes_IsSQLSRC112(string second)
    {
        var text = "-- name: Q\n-- param: @a -> UserId\n-- param: " + second + "\nSELECT @a\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.ConflictingSettings,
                    new TextSpan(
                        text.IndexOf("-- param: " + second + "\n", StringComparison.Ordinal) + 10,
                        second.Length
                    ),
                    "param: " + second
                ),
            ]);
    }

    [Fact]
    public void Parse_TwoParamMarkers_TheSameCSharpType_IsNotAConflict() =>
        Blocks("-- name: Q\n-- param: @a -> UserId\n-- param: @A  ->  UserId\nSELECT @a\n")
            .ShouldHaveSingleItem()
            .Parameters.ShouldBe([new SqlQueryParameter("a", null, null, true, "UserId")]);

    [Theory]
    [InlineData("a int")]
    [InlineData("@")]
    [InlineData("@ a")]
    [InlineData("@a-b int")]
    [InlineData("@a,@b")]
    [InlineData(":a int")]
    public void Parse_ParamMarkerWithoutAPrefixedNameOnItsOwn_IsInvalid(string value)
    {
        var text = "-- name: Q\n-- param: " + value + "\nSELECT 1\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.InvalidMarkerValue,
                    TextSpans.Of(text, value),
                    "param: " + value,
                    ""
                ),
            ]);
    }

    [Theory]
    [InlineData("@page")]
    [InlineData("@page null")]
    [InlineData("@page not null")]
    [InlineData("@page -> PageNumber")]
    public void Parse_DeclaredOnlyParameterWithoutAType_IsAnError(string value)
    {
        var text = "-- name: Q\n-- param: " + value + "\nSELECT 1 {{tail:LIMIT @page}}\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.MissingParameterType, TextSpans.Of(text, value), "@page"),
            ]);
    }

    [Fact]
    public void Parse_ParameterOnlyInAnInlineDefault_MustBeDeclared()
    {
        const string Text = "-- name: Q\nSELECT @a {{f:AND x = @b AND y = @a}}\n";

        Errors(Text)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.UndeclaredParameter, TextSpans.Of(Text, "@b"), "@b")]);
    }

    [Fact]
    public void Parse_ParameterOnlyInAMarkersDefault_MustBeDeclared()
    {
        const string Text = "-- name: Q\n-- token: {{f:AND x = @b AND y = '@c'}}\nSELECT 1 {{f}}\n";

        Errors(Text)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.UndeclaredParameter, TextSpans.Of(Text, "@b"), "@b")]);
    }

    // The default is compared without its comment, and the parameter is still found in the default as written.
    [Fact]
    public void Parse_ParameterInAMarkerDefaultAfterAComment_IsReportedWhereItIsWritten()
    {
        const string Text = "-- name: Q\n-- token: {{f:/* c */ @p}}\nSELECT {{f}}\n";

        Errors(Text)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.UndeclaredParameter, TextSpans.Of(Text, "@p"), "@p")]);
    }

    [Fact]
    public void Parse_ParameterInADefaultThatIsDeclaredWithAType_IsADeclaredOnlyParameter() =>
        Parameters(
                Blocks("-- name: Q\n-- param: @b int\n-- token: {{g:OFFSET @b}}\nSELECT @a {{f:LIMIT @b}} {{g}}\n")
                    .ShouldHaveSingleItem()
            )
            .ShouldBe(["a:<none>:<unsaid>", "b:int:<unsaid>:declared"]);

    // The query does not hold the token, so its marker's default is not the query's.
    [Fact]
    public void Parse_ParameterInTheDefaultOfATokenTheQueryLacks_IsNotChecked() =>
        Blocks("-- name: Q\n-- token: {{other:@x}}\nSELECT 1\n").ShouldHaveSingleItem().Parameters.ShouldBeEmpty();

    // A fragment passed at run time may use it.
    [Fact]
    public void Parse_DeclaredParameterThatNothingHolds_IsKept() =>
        Parameters(Blocks("-- name: Q\n-- param: @later int\nSELECT 1 {{f}}\n").ShouldHaveSingleItem())
            .ShouldBe(["later:int:<unsaid>:declared"]);

    [Fact]
    public void Parse_ParamMarkerInAFileWithoutNameMarker_IsAllowed() =>
        Parameters(Blocks("-- param: @a int\nSELECT @a\n").ShouldHaveSingleItem())
            .ShouldBe(["a:int:<unsaid>:declared"]);

    [Fact]
    public void Parse_ParameterInsideAToken_IsNotAParameterOfTheSql()
    {
        var block = Blocks("-- name: Q\n-- param: @b int\nSELECT @a {{f:AND x = @b}} {{t}}@c\n").ShouldHaveSingleItem();

        block.Parameters.Select(static parameter => parameter.Name).ShouldBe(["a", "c", "b"]);
    }

    [Fact]
    public void Parse_ParameterInsideAnIgnoredToken_IsAParameterOfTheSql() =>
        Blocks("-- name: Q\n-- token-ignore: f\nSELECT {{f:@b}}\n")
            .ShouldHaveSingleItem()
            .Parameters.Select(static parameter => parameter.Name)
            .ShouldBe(["b"]);
}
