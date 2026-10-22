using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Statements;
using SqlSource.Tool.Describing.Postgres.Types;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// A server's error, or a refusal of the tool, as the failure the run reports at the query.
public sealed class PostgresErrorsTests
{
    [Fact]
    public void Open_ErrorWithASqlState_IsSqlsrc224AtNoStepWithTheServersLine()
    {
        var error = new PostgresServerError("28P01", ConnectionErrors.PasswordRefused, null, null, null);

        var failure = PostgresErrors.Open(error, "billing");

        failure.Descriptor.Id.ShouldBe("SQLSRC224");
        failure.Arguments.ShouldBe(["billing"]);
        failure.Step.ShouldBeNull();
        failure.ServerLines.ShouldBe(["28P01: the server refused the password"]);
        failure.Help.ShouldBe(PostgresErrors.OpenHelp);
    }

    [Fact]
    public void Open_ErrorWithoutASqlState_HasTheMessageAlone() =>
        PostgresErrors
            .Open(new PostgresServerError(null, ConnectionErrors.ConnectTimedOut, null, null, null), "app")
            .ServerLines.ShouldBe(["the connection to the server timed out"]);

    // The refusal is the tool's own message and no server said it: the line is that message alone.
    [Fact]
    public void Open_MultiplexingRefused_IsSqlsrc236()
    {
        var failure = PostgresErrors.Open(
            new PostgresServerError(null, PostgresConnection.MultiplexingRefused, null, null, null),
            "app"
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC236");
        failure.Arguments.ShouldBe(["app"]);
        failure.Step.ShouldBeNull();
        failure.ServerLines.ShouldBe([PostgresConnection.MultiplexingRefused]);
        failure.Help.ShouldBe(PostgresErrors.SessionNotKeptHelp);
    }

    // Each line the server gave, in the order PostgresErrors.ServerLines fixes, and a position counted from where the
    // SQL starts.
    [Fact]
    public void ServerLines_ErrorWithEveryPart_GivesEachInOrder() =>
        PostgresErrors
            .ServerLines(new PostgresServerError("42883", "operator does not exist", "a detail", "a hint", 63), 31)
            .ShouldBe(["42883: operator does not exist", "detail: a detail", "hint: a hint", "at character 32"]);

    [Theory]
    [InlineData(null)]
    [InlineData(31)]
    [InlineData(7)]
    public void ServerLines_NoPositionOrOneBeforeTheSql_HasNoPositionLine(int? position) =>
        PostgresErrors
            .ServerLines(new PostgresServerError("42601", "syntax error", null, null, position), 31)
            .ShouldBe(["42601: syntax error"]);

    private static DescribeRequest Query(params SqlQueryParameter[] parameters) => Listed("SELECT 1", parameters);

    [Fact]
    public void SeveralStatements_Query_IsSqlsrc230WithItsHelp()
    {
        var failure = PostgresErrors.SeveralStatements(Query());

        failure.Descriptor.Id.ShouldBe("SQLSRC230");
        failure.Arguments.ShouldBe(["Q"]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.SeveralStatementsHelp);
    }

    [Fact]
    public void NotDescribable_Kind_IsSqlsrc233WithItsHelp()
    {
        var failure = PostgresErrors.NotDescribable(Query(), "CALL");

        failure.Descriptor.Id.ShouldBe("SQLSRC233");
        failure.Arguments.ShouldBe(["Q", "CALL"]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.NotDescribableHelp);
    }

    // The type as the marker wrote it, and the parameter as the SQL writes one, with its prefix.
    [Fact]
    public void TypeRefused_Parameter_IsSqlsrc228ThatSaysItIsNoTypeName()
    {
        var parameter = Declared("tenant", "int) AS SELECT 1");

        var failure = PostgresErrors.TypeRefused(Query(parameter), parameter);

        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments.ShouldBe(["int) AS SELECT 1", "@tenant", "Q", "it is not written as a type name"]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.NotATypeNameHelp);
    }

    private static PostgresPrepareRequest Prepared(int number, params string[] types) =>
        new(PostgresScript.Statement(number), "SELECT 1", types);

    private static PostgresServerError Error(string state, string message, int? position = null, string? hint = null) =>
        new(state, message, null, hint, position);

    // The backstop for what the tool's own check misses: the kind is the word "utility", never a word of a statement.
    [Fact]
    public void Prepare_UtilityStatementsCannotBePrepared_IsSqlsrc233()
    {
        var failure = PostgresErrors.Prepare(
            new PostgresServerError("42P14", "utility statements cannot be prepared", null, null, null),
            Query(),
            Prepared(1),
            "app"
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC233");
        failure.Arguments.ShouldBe(["Q", "utility"]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBe(["42P14: utility statements cannot be prepared"]);
        failure.Help.ShouldBe(PostgresErrors.NotDescribableHelp);
    }

    // A statement that is not listed after PREPARE, and one that already exists: both are a backend that is not the
    // one the tool prepared on.  The first carries no SQLSTATE and the second is matched by its SQLSTATE alone, so
    // the message may be translated.
    [Theory]
    [InlineData(null, PostgresLive.StatementNotListed)]
    [InlineData("42P05", "prepared statement \"sqlsource_1\" already exists")]
    [InlineData("42P05", "la sentencia preparada \"sqlsource_1\" ya existe")]
    public void Prepare_StatementNotListedOrAlreadyExists_IsSqlsrc236(string? state, string message)
    {
        var failure = PostgresErrors.Prepare(
            new PostgresServerError(state, message, null, null, 40),
            Query(),
            Prepared(1),
            "app"
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC236");
        failure.Arguments.ShouldBe(["app"]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBe([state is null ? message : $"{state}: {message}"]);
        failure.Help.ShouldBe(PostgresErrors.SessionNotKeptHelp);
    }

    // The message of the not-listed answer is the tool's own; the same words from the server with a SQLSTATE are
    // the server rejecting the query.
    [Fact]
    public void Prepare_NotListedMessageWithASqlState_IsSqlsrc225() =>
        PostgresErrors
            .Prepare(Error("42601", PostgresLive.StatementNotListed), Query(), Prepared(1), "app")
            .Descriptor.Id.ShouldBe("SQLSRC225");

    // 26000 is matched by its SQLSTATE alone.
    [Theory]
    [InlineData("prepared statement \"sqlsource_1\" does not exist")]
    [InlineData("la sentencia preparada \"sqlsource_1\" no existe")]
    public void Columns_PreparedStatementDoesNotExist_IsSqlsrc236(string message)
    {
        var failure = PostgresErrors.Columns(Error("26000", message, position: 9), Query(), "app");

        failure.Descriptor.Id.ShouldBe("SQLSRC236");
        failure.Arguments.ShouldBe(["app"]);
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
        failure.ServerLines.ShouldBe([$"26000: {message}"]);
        failure.Help.ShouldBe(PostgresErrors.SessionNotKeptHelp);
    }

    [Fact]
    public void Columns_AnyOtherError_IsSqlsrc225AtDescribeColumns()
    {
        var failure = PostgresErrors.Columns(Error("42P01", "relation \"nope\" does not exist"), Query(), "app");

        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
    }

    // 42P18 has no position.  The server names the parameter $2, and the user wrote @b.
    [Fact]
    public void Prepare_42P18_IsSqlsrc226NamingTheParameterOfTheMessage()
    {
        var failure = PostgresErrors.Prepare(
            Error("42P18", "could not determine data type of parameter $2"),
            Query(Inferred("a"), Inferred("b")),
            Prepared(1, "unknown", "unknown"),
            "app"
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC226");
        failure.Arguments.ShouldBe(["@b", "Q"]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBe(["42P18: could not determine data type of parameter $2"]);
        failure.Help.ShouldBe(
            "cast the parameter where it is used, @b::int, or declare its type with -- param: @b int"
        );
    }

    // "PREPARE sqlsource_7 (unknown" and a line break and ") AS " are 34 characters, so 62 is the 28th of the SQL.
    [Fact]
    public void Prepare_42P08_IsSqlsrc227WithThePositionInTheUsersSql()
    {
        var failure = PostgresErrors.Prepare(
            Error("42P08", "could not determine data type of parameter $1", 62),
            Query(Inferred("name")),
            Prepared(7, "unknown"),
            "app"
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC227");
        failure.Arguments.ShouldBe(["@name", "Q"]);
        failure.ServerLines.ShouldBe(["42P08: could not determine data type of parameter $1", "at character 28"]);
        failure.Help.ShouldBe(PostgresErrors.FirstUseHelp);
    }

    [Theory]
    [InlineData("42P18", "could not determine data type of parameter $7")]
    [InlineData("42P08", "could not determine data type of a parameter")]
    public void Prepare_ParameterErrorThatNamesNoParameterOfTheList_IsSqlsrc225(string state, string message) =>
        PostgresErrors
            .Prepare(Error(state, message), Query(Inferred("a")), Prepared(1, "unknown"), "app")
            .Descriptor.Id.ShouldBe("SQLSRC225");

    // The character 32 of "PREPARE sqlsource_10 (unknown, nope_t" is the n of nope_t.
    [Fact]
    public void Prepare_42704InsideTheTypeList_IsSqlsrc228NamingTheTypeAndTheParameter()
    {
        var failure = PostgresErrors.Prepare(
            Error("42704", "type \"nope_t\" does not exist", 32),
            Query(Inferred("id"), Declared("tenant", "nope_t")),
            Prepared(10, "unknown", "nope_t"),
            "app"
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments.ShouldBe(["nope_t", "@tenant", "Q", "the database has no such type"]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBe(["42704: type \"nope_t\" does not exist"]);
        failure.Help.ShouldBe(PostgresErrors.NoSuchTypeHelp);
    }

    // The same SQLSTATE at a cast in the SQL is the server rejecting the query: 47 less the 35 before the SQL.
    [Fact]
    public void Prepare_42704OutsideTheTypeList_IsSqlsrc225WithThePosition()
    {
        var failure = PostgresErrors.Prepare(
            Error("42704", "type \"nope_t\" does not exist", 47),
            Query(Inferred("p")),
            Prepared(11, "unknown"),
            "app"
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Arguments.ShouldBe(["Q", "type \"nope_t\" does not exist"]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBe(["42704: type \"nope_t\" does not exist", "at character 12"]);
        failure.Help.ShouldBeNull();
    }

    [Fact]
    public void Prepare_42704WithNoPosition_IsSqlsrc225() =>
        PostgresErrors
            .Prepare(
                Error("42704", "type \"nope_t\" does not exist"),
                Query(Declared("p", "nope_t")),
                Prepared(1, "nope_t"),
                "app"
            )
            .Descriptor.Id.ShouldBe("SQLSRC225");

    // Any other error the server positions inside the type list is the type's too: PostgreSQL 14 has no negative
    // scale, and says so at the type.
    [Theory]
    [InlineData("22023", "NUMERIC scale -3 must be between 0 and precision 2")]
    [InlineData("42601", "syntax error at or near \"numeric\"")]
    public void Prepare_OtherErrorInsideTheTypeList_IsSqlsrc228ThatSaysItIsNoTypeName(string state, string message)
    {
        var failure = PostgresErrors.Prepare(
            Error(state, message, 23),
            Query(Declared("p", "numeric(2,-3)")),
            Prepared(35, "numeric(2,-3)"),
            "app"
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments.ShouldBe(["numeric(2,-3)", "@p", "Q", "it is not written as a type name"]);
        failure.ServerLines.ShouldBe([$"{state}: {message}"]);
        failure.Help.ShouldBe(PostgresErrors.NotATypeNameHelp);
    }

    // SQLSRC228 names a declared type, and the item at the position is the server's own "unknown".
    [Fact]
    public void Prepare_ErrorInsideTheTypeListAtAnInferredParameter_IsSqlsrc225() =>
        PostgresErrors
            .Prepare(Error("42601", "syntax error", 23), Query(Inferred("p")), Prepared(1, "unknown"), "app")
            .Descriptor.Id.ShouldBe("SQLSRC225");

    // A parameter with no declared type takes the type of its first use, which a type error may be the fault of.
    [Theory]
    [InlineData("42883", "operator does not exist: timestamp with time zone > interval")]
    [InlineData("42804", "argument of WHERE must be type boolean, not type integer")]
    [InlineData("42725", "operator is not unique: unknown || unknown")]
    public void Prepare_TypeErrorWithAParameterOfNoDeclaredType_IsSqlsrc225WithTheFirstUseHelp(
        string state,
        string message
    )
    {
        var failure = PostgresErrors.Prepare(
            Error(state, message, 47),
            Listed(
                "SELECT id FROM orders WHERE placed_at > now() - @age AND n = @n AND q = @q",
                Inferred("age"),
                Declared("n", "int"),
                Inferred("q")
            ),
            Prepared(1, "unknown", "int", "unknown"),
            "app"
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Arguments.ShouldBe(["Q", message]);
        failure.Help.ShouldBe(
            "if the error is at a use of a parameter, the server gave each of @age, @q the type of its first use, "
                + "which may not be the type meant: cast the parameter where it is used to the type meant, "
                + "@age::<type>, or declare its type with -- param: @age <type>"
        );
    }

    [Fact]
    public void Prepare_TypeErrorWithOneParameterOfNoDeclaredType_NamesItAlone() =>
        PostgresErrors
            .Prepare(
                Error("42883", "operator does not exist: integer = text", 40),
                Listed("SELECT id FROM accounts WHERE id = @x", Inferred("x")),
                Prepared(1, "unknown"),
                "app"
            )
            .Help.ShouldBe(
                "if the error is at a use of a parameter, the server gave @x the type of its first use, which may not "
                    + "be the type meant: cast the parameter where it is used to the type meant, @x::<type>, or "
                    + "declare its type with -- param: @x <type>"
            );

    // A cast at a use fixes the type there, with white space and comments before the "::", and a parameter cast at
    // every use is not named.
    [Theory]
    [InlineData("SELECT @x::int + @y")]
    [InlineData("SELECT @x::int + @X /* a */ :: int + @y")]
    [InlineData("SELECT CAST(@x AS int) + @y")]
    [InlineData("SELECT cast ( -- a\n @x as int) + @y")]
    public void Prepare_TypeErrorWithAParameterCastAtEveryUse_DoesNotNameIt(string sql) =>
        PostgresErrors
            .Prepare(
                Error("42883", "operator does not exist: integer + text"),
                Listed(sql, Inferred("x"), Inferred("y")),
                Prepared(1, "unknown", "unknown"),
                "app"
            )
            .Help.ShouldBe(PostgresErrors.InferredParameterHelp([Inferred("y")]));

    // A "::" in a string or a comment, a ": :", a CAST of more than the parameter and a cast of a parenthesis fix
    // nothing at the parameter.
    [Theory]
    [InlineData("SELECT @x::int + @x")]
    [InlineData("SELECT @x + CAST(@x AS int)")]
    [InlineData("SELECT @x '::int'")]
    [InlineData("SELECT @x -- ::int")]
    [InlineData("SELECT @x : :int")]
    [InlineData("SELECT CAST(@x + 1 AS int)")]
    [InlineData("SELECT (@x)::int")]
    public void Prepare_TypeErrorWithAParameterUncastAtOneUse_NamesIt(string sql) =>
        PostgresErrors
            .Prepare(
                Error("42883", "operator does not exist: integer + text"),
                Listed(sql, Inferred("x")),
                Prepared(1, "unknown"),
                "app"
            )
            .Help.ShouldBe(PostgresErrors.InferredParameterHelp([Inferred("x")]));

    [Fact]
    public void Prepare_TypeErrorWithEveryParameterOfNoDeclaredTypeCast_IsSqlsrc225WithNoHelp() =>
        PostgresErrors
            .Prepare(
                Error("42883", "function lowr(text) does not exist"),
                Listed("SELECT lowr(name) FROM accounts WHERE id = @id::int", Inferred("id")),
                Prepared(1, "unknown"),
                "app"
            )
            .Help.ShouldBeNull();

    [Fact]
    public void InferredParameterHelp_NoParameter_ThrowsArgumentException() =>
        Should
            .Throw<ArgumentException>(() => PostgresErrors.InferredParameterHelp([]))
            .ParamName.ShouldBe("undeclared");

    [Fact]
    public void Prepare_TypeErrorWithEveryParameterDeclared_IsSqlsrc225WithNoHelp() =>
        PostgresErrors
            .Prepare(
                Error("42883", "operator does not exist: integer = text", 40),
                Query(Declared("x", "text")),
                Prepared(1, "text"),
                "app"
            )
            .Help.ShouldBeNull();

    [Fact]
    public void Prepare_OtherErrorWithAParameterOfNoDeclaredType_IsSqlsrc225WithNoHelp() =>
        PostgresErrors
            .Prepare(
                Error("42P01", "relation \"nope\" does not exist", 40),
                Query(Inferred("x")),
                Prepared(1, "unknown"),
                "app"
            )
            .Help.ShouldBeNull();

    [Fact]
    public void Prepare_AnyOtherError_IsSqlsrc225WithEveryLineOfTheServer()
    {
        var failure = PostgresErrors.Prepare(
            Error("42883", "operator does not exist: character varying = integer", 63, "You might need to add casts."),
            Query(Declared("p", "int")),
            Prepared(12, "int"),
            "app"
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Arguments.ShouldBe(["Q", "operator does not exist: character varying = integer"]);
        failure.ServerLines.ShouldBe([
            "42883: operator does not exist: character varying = integer",
            "hint: You might need to add casts.",
            "at character 32",
        ]);
        failure.Help.ShouldBeNull();
    }

    // A connection that dropped: the describer's own text, and no SQLSTATE.
    [Fact]
    public void Prepare_ErrorWithoutASqlState_IsSqlsrc225WithTheMessageAlone()
    {
        var failure = PostgresErrors.Prepare(
            new PostgresServerError(null, PostgresConnection.LostConnection, null, null, null),
            Query(),
            Prepared(1),
            "app"
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.ServerLines.ShouldBe([PostgresConnection.LostConnection]);
    }

    // The server counts a character outside the basic plane once, and so does the tool: the fifth character of the
    // SQL, whatever the type list holds.
    [Fact]
    public void Prepare_TypeListWithACharacterOutsideTheBasicPlane_StillCountsFromTheSql()
    {
        var prepare = Prepared(1, "\"\U0001F600\"");
        var head = ParameterPlaceholders.HeadLength(prepare.Statement, prepare.Types);

        var failure = PostgresErrors.Prepare(
            Error("42601", "syntax error", head + 5),
            Query(Declared("p", "\"\U0001F600\"")),
            prepare,
            "app"
        );

        failure.ServerLines.ShouldBe(["42601: syntax error", "at character 5"]);
    }

    [Fact]
    public void Types_Error_IsSqlsrc225AtResolveType()
    {
        var failure = PostgresErrors.Types(
            new PostgresServerError(null, PostgresConnection.LostConnection, null, null, null),
            Query()
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Arguments.ShouldBe(["Q", PostgresConnection.LostConnection]);
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.ServerLines.ShouldBe([PostgresConnection.LostConnection]);
    }

    // Nothing the server said: a table, a column or a type went while the query was described.  So there is no
    // server line, and the help says to run the tool again.
    [Fact]
    public void SchemaChanged_TableThatWentAtTheCatalog_IsSqlsrc225WithNoServerLineAndTheHelp()
    {
        const string What = "the table of the column 'id' no longer exists";

        var failure = PostgresErrors.SchemaChanged(Query(), DescribeStep.Catalog, What);

        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Arguments.ShouldBe(["Q", What]);
        failure.Step.ShouldBe(DescribeStep.Catalog);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.SchemaChangedHelp);
    }

    [Fact]
    public void SchemaChanged_TypeThatWentAtResolveType_KeepsTheStepItWasGiven() =>
        PostgresErrors
            .SchemaChanged(Query(), DescribeStep.ResolveType, "the type of the column 'n' no longer exists")
            .Step.ShouldBe(DescribeStep.ResolveType);

    // An error of "types" with a position, as PostgreSQL 13 gave before the tool refused it at "open".  The position
    // is in the tool's own query of the catalog, which the user never wrote, so there is no line for it; the detail
    // and the hint of the server stay.
    [Fact]
    public void Types_ErrorWithAPosition_HasNoPositionLine() =>
        PostgresErrors
            .Types(
                new PostgresServerError("42703", "column m.rngmultitypid does not exist", "a detail", "a hint", 392),
                Query()
            )
            .ServerLines.ShouldBe(["42703: column m.rngmultitypid does not exist", "detail: a detail", "hint: a hint"]);

    // The server said nothing: it accepted the type.  So there is no server line, and the help says what it gave.
    [Fact]
    public void PseudoType_InferredParameter_IsSqlsrc226ThatNamesThePseudoType()
    {
        var parameter = Inferred("p");

        var failure = PostgresErrors.PseudoType(Query(parameter), parameter, "record");

        failure.Descriptor.Id.ShouldBe("SQLSRC226");
        failure.Arguments.ShouldBe(["@p", "Q"]);
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(
            "the server gave it the pseudo-type 'record', which no value has: cast the parameter where it is used, "
                + "or declare its type with -- param: @p <type>"
        );
    }

    [Fact]
    public void PseudoType_DeclaredParameter_IsSqlsrc228ThatSaysItIsAPseudoType()
    {
        var parameter = Declared("p", "anyelement");

        var failure = PostgresErrors.PseudoType(Query(parameter), parameter, "anyelement");

        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments.ShouldBe(["anyelement", "@p", "Q", "it is a pseudo-type"]);
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.PseudoTypeHelp);
    }

    private static readonly DescribeRequest Checked = new(
        "ListUsers",
        "SELECT id FROM users WHERE status = @status",
        new EquatableArray<SqlQueryParameter>([new SqlQueryParameter("status", null, null, false)])
    );

    private static IReadOnlyList<DescribedParameter> DescribedWith(string name, string bindName) =>
        [
            new DescribedParameter(
                "status",
                TestSidecars.Postgres(name, bindName: bindName),
                SidecarValues.TypeSource.Inferred
            ),
        ];

    private static PostgresCheckRequest AskedWith(string type) =>
        new("SELECT id FROM users WHERE status = @status", [new PostgresCheckParameter("status", type)]);

    [Fact]
    public void Check_VerdictSame_IsNoFailure() =>
        PostgresErrors
            .Check(
                Checked,
                DescribedWith("text", "text"),
                AskedWith("text"),
                new PostgresCheckAnswer([], null, null),
                new CheckVerdict(NpgsqlCheck.Same, null)
            )
            .ShouldBeNull();

    [Fact]
    public void Check_ColumnsThatDiffer_IsSqlsrc231WithWhatDiffersAndNoServerLine()
    {
        var failure = PostgresErrors
            .Check(
                Checked,
                DescribedWith("text", "text"),
                AskedWith("text"),
                new PostgresCheckAnswer([], null, null),
                new CheckVerdict(NpgsqlCheck.Differ, "the result has 0 columns for Npgsql and 1 for SqlSource")
            )
            .ShouldNotBeNull();

        failure.Descriptor.Id.ShouldBe("SQLSRC231");
        failure.Arguments.ShouldBe(["Npgsql", "ListUsers", "the result has 0 columns for Npgsql and 1 for SqlSource"]);
        failure.Step.ShouldBe(DescribeStep.CheckParameters);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.CheckHelp);
    }

    // The position is in Npgsql's own rewrite of the SQL, which the user never sees, so it has no line.
    [Fact]
    public void Check_ServerErrorOfClass42_IsSqlsrc231()
    {
        var error = new PostgresServerError("42601", "syntax error at or near \"@\"", null, "a hint", 31);

        var failure = PostgresErrors
            .Check(
                Checked,
                DescribedWith("text", "text"),
                AskedWith("text"),
                new PostgresCheckAnswer(null, error, null, null, false),
                new CheckVerdict(NpgsqlCheck.Differ, error.Message)
            )
            .ShouldNotBeNull();

        failure.Descriptor.Id.ShouldBe("SQLSRC231");
        failure.Arguments.ShouldBe(["Npgsql", "ListUsers", "syntax error at or near \"@\""]);
        failure.ServerLines.ShouldBe(["42601: syntax error at or near \"@\"", "hint: a hint"]);
    }

    // A timeout or a failing server says nothing of how Npgsql reads the text, and the verdict says so
    // (NpgsqlCheckTests holds the rule): it is the server rejecting the query at the step, with the lines of the
    // error and no help.
    [Fact]
    public void Check_VerdictErrorOutsideClass42_IsSqlsrc225AtCheckParameters()
    {
        var error = new PostgresServerError("57014", "canceling statement due to user request", null, null, 31);

        var failure = PostgresErrors
            .Check(
                Checked,
                DescribedWith("text", "text"),
                AskedWith("text"),
                new PostgresCheckAnswer(null, error, null, null, false),
                new CheckVerdict(NpgsqlCheck.Error, error.Message)
            )
            .ShouldNotBeNull();

        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Arguments.ShouldBe(["ListUsers", "canceling statement due to user request"]);
        failure.Step.ShouldBe(DescribeStep.CheckParameters);
        failure.ServerLines.ShouldBe(["57014: canceling statement due to user request"]);
        failure.Help.ShouldBeNull();
    }

    [Fact]
    public void Check_VerdictErrorOfALostConnection_IsSqlsrc225WithItsText()
    {
        var error = new PostgresServerError(null, PostgresConnection.LostConnection, null, null, null);

        var failure = PostgresErrors
            .Check(
                Checked,
                DescribedWith("text", "text"),
                AskedWith("text"),
                new PostgresCheckAnswer(null, error, null, null, false),
                new CheckVerdict(NpgsqlCheck.Error, error.Message)
            )
            .ShouldNotBeNull();

        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Arguments.ShouldBe(["ListUsers", PostgresConnection.LostConnection]);
        failure.Step.ShouldBe(DescribeStep.CheckParameters);
        failure.ServerLines.ShouldBe([PostgresConnection.LostConnection]);
    }

    [Fact]
    public void Check_TextRefusedByNpgsql_IsSqlsrc231()
    {
        var error = new PostgresServerError(null, "please remove all semicolons", null, null, null);

        var failure = PostgresErrors
            .Check(
                Checked,
                DescribedWith("text", "text"),
                AskedWith("text"),
                new PostgresCheckAnswer(null, error, null, null, true),
                new CheckVerdict(NpgsqlCheck.Differ, error.Message)
            )
            .ShouldNotBeNull();

        failure.Descriptor.Id.ShouldBe("SQLSRC231");
        failure.ServerLines.ShouldBe(["please remove all semicolons"]);
    }

    [Fact]
    public void Check_TypeDiffersAndAParameterIsAnArrayOfADomain_HelpsWithTheCast()
    {
        var domain = TestSidecars.Postgres("email_t", SidecarValues.Kind.Domain, "public", "email_t");
        var array = TestSidecars.Postgres("email_t[]", SidecarValues.Kind.Array, "public", "_email_t", element: domain);
        IReadOnlyList<DescribedParameter> parameters =
        [
            new DescribedParameter("status", array, SidecarValues.TypeSource.Inferred),
        ];

        var failure = PostgresErrors
            .Check(
                Checked,
                parameters,
                AskedWith("public.email_t[]"),
                new PostgresCheckAnswer([], null, null),
                new CheckVerdict(
                    NpgsqlCheck.Differ,
                    "column 1 is character varying[] for Npgsql and email_t[] for SqlSource",
                    true
                )
            )
            .ShouldNotBeNull();

        failure.Descriptor.Id.ShouldBe("SQLSRC231");
        failure.Help.ShouldBe(PostgresErrors.ArrayOfDomainHelp);
    }

    [Fact]
    public void Check_TypeDiffersWithoutSuchAParameter_HasTheCheckHelp()
    {
        var failure = PostgresErrors
            .Check(
                Checked,
                DescribedWith("text", "text"),
                AskedWith("text"),
                new PostgresCheckAnswer([], null, null),
                new CheckVerdict(NpgsqlCheck.Differ, "column 1 is text for Npgsql and integer for SqlSource", true)
            )
            .ShouldNotBeNull();

        failure.Help.ShouldBe(PostgresErrors.CheckHelp);
    }

    // A difference of the texts has no lines of the server: the message says where the two part.
    [Fact]
    public void Check_PlacementDiffers_IsSqlsrc231WithThePlacementHelpAndNoServerLine()
    {
        const string Differs =
            "the SQL with $n parts at character 45: '$1] IS NOT' for Npgsql and ':id] IS NO' for SqlSource";

        var failure = PostgresErrors
            .Check(
                Checked,
                DescribedWith("text", "text"),
                AskedWith("text"),
                new PostgresCheckAnswer([], null, null),
                new CheckVerdict(NpgsqlCheck.Differ, Differs, PlacementDiffers: true)
            )
            .ShouldNotBeNull();

        failure.Descriptor.Id.ShouldBe("SQLSRC231");
        failure.Arguments.ShouldBe(["Npgsql", "ListUsers", Differs]);
        failure.Step.ShouldBe(DescribeStep.CheckParameters);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.PlacementHelp);
    }

    // The help of an array of a domain is for a difference of a column's type, not of the texts.
    [Fact]
    public void Check_ArrayOfADomainButOnlyTheTextDiffers_HasThePlacementHelp()
    {
        var domain = TestSidecars.Postgres("email_t", SidecarValues.Kind.Domain, "public", "email_t");
        var array = TestSidecars.Postgres("email_t[]", SidecarValues.Kind.Array, "public", "_email_t", element: domain);
        IReadOnlyList<DescribedParameter> parameters =
        [
            new DescribedParameter("status", array, SidecarValues.TypeSource.Inferred),
        ];

        var failure = PostgresErrors
            .Check(
                Checked,
                parameters,
                AskedWith("public.email_t[]"),
                new PostgresCheckAnswer([], null, null),
                new CheckVerdict(NpgsqlCheck.Differ, "the SQL with $n parts at character 3", PlacementDiffers: true)
            )
            .ShouldNotBeNull();

        failure.Help.ShouldBe(PostgresErrors.PlacementHelp);
    }

    // The type is named by its name, as the SQL would spell it, and not by the name it was bound by.
    [Theory]
    [InlineData("public.check_pair", "public.check_pair")]
    [InlineData("\"My Schema\".\"Pair\"", "My Schema.Pair")]
    [InlineData("\"char\"", "char")]
    public void Check_DriverError_IsSqlsrc232NamingTheTypeByItsNameAndTheParameterAsWritten(
        string name,
        string bindName
    )
    {
        var failure = PostgresErrors
            .Check(
                Checked,
                DescribedWith(name, bindName),
                AskedWith(bindName),
                new PostgresCheckAnswer(null, null, new PostgresBindError("status", "Npgsql's own words")),
                new CheckVerdict(NpgsqlCheck.Unbound, "Npgsql's own words")
            )
            .ShouldNotBeNull();

        failure.Descriptor.Id.ShouldBe("SQLSRC232");
        failure.Arguments.ShouldBe([name, "@status", "ListUsers"]);
        failure.Step.ShouldBe(DescribeStep.CheckParameters);
        failure.ServerLines.ShouldBe(["Npgsql's own words"]);
        failure.Help.ShouldBe(PostgresErrors.BindHelp);
    }

    [Fact]
    public void Unnamed_ParameterOfATypeWithoutABindName_IsSqlsrc232SayingThereIsNoName()
    {
        var failure = PostgresErrors.Unnamed(Checked, 0, "public.\"x.y\"");

        failure.Descriptor.Id.ShouldBe("SQLSRC232");
        failure.Arguments.ShouldBe(["public.\"x.y\"", "@status", "ListUsers"]);
        failure.Step.ShouldBe(DescribeStep.CheckParameters);
        failure.ServerLines.ShouldBe([PostgresBindNames.NoName]);
        failure.Help.ShouldBe(PostgresErrors.UnnamedHelp);
    }
}
