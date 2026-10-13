using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// A server's error, or a refusal of the tool, as the failure the run reports at the query.
public class PostgresErrorsTests
{
    [Fact]
    public void Open_ErrorWithASqlState_IsSqlsrc224AtNoStepWithTheServersLine()
    {
        var error = new PostgresServerError(
            "28P01",
            "password authentication failed for user \"app\"",
            null,
            null,
            null
        );

        var failure = PostgresErrors.Open(error, "billing");

        failure.Descriptor.Id.ShouldBe("SQLSRC224");
        failure.Arguments.ShouldBe(["billing"]);
        failure.Step.ShouldBeNull();
        failure.ServerLines.ShouldBe(["28P01: password authentication failed for user \"app\""]);
        failure.Help.ShouldBe(PostgresErrors.OpenHelp);
    }

    [Fact]
    public void Open_ErrorWithoutASqlState_HasTheMessageAlone() =>
        PostgresErrors
            .Open(new PostgresServerError(null, "Failed to connect to 127.0.0.1:1", null, null, null), "app")
            .ServerLines.ShouldBe(["Failed to connect to 127.0.0.1:1"]);

    // Each line the server gave, in the order the epic fixes, and a position counted from where the SQL starts.
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

    private static DescribeRequest Query(params SqlQueryParameter[] parameters) =>
        new("Q", "SELECT 1", new EquatableArray<SqlQueryParameter>([.. parameters]));

    private static SqlQueryParameter Inferred(string name) => new(name, null, null, false);

    private static SqlQueryParameter Declared(string name, string type) => new(name, type, null, true);

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

    private static PostgresPrepareRequest Prepared(string statement, params string[] types) =>
        new(statement, "SELECT 1", types);

    private static PostgresServerError Error(string state, string message, int? position = null, string? hint = null) =>
        new(state, message, null, hint, position);

    // 42P18 has no position.  The server names the parameter $2, and the user wrote @b.
    [Fact]
    public void Prepare_42P18_IsSqlsrc226NamingTheParameterOfTheMessage()
    {
        var failure = PostgresErrors.Prepare(
            Error("42P18", "could not determine data type of parameter $2"),
            Query(Inferred("a"), Inferred("b")),
            Prepared("sqlsource_1", "unknown", "unknown")
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
            Prepared("sqlsource_7", "unknown")
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
            .Prepare(Error(state, message), Query(Inferred("a")), Prepared("sqlsource_1", "unknown"))
            .Descriptor.Id.ShouldBe("SQLSRC225");

    // The character 32 of "PREPARE sqlsource_10 (unknown, nope_t" is the n of nope_t.
    [Fact]
    public void Prepare_42704InsideTheTypeList_IsSqlsrc228NamingTheTypeAndTheParameter()
    {
        var failure = PostgresErrors.Prepare(
            Error("42704", "type \"nope_t\" does not exist", 32),
            Query(Inferred("id"), Declared("tenant", "nope_t")),
            Prepared("sqlsource_10", "unknown", "nope_t")
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
            Prepared("sqlsource_11", "unknown")
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
                Prepared("sqlsource_1", "nope_t")
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
            Prepared("sqlsource_35", "numeric(2,-3)")
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
            .Prepare(Error("42601", "syntax error", 23), Query(Inferred("p")), Prepared("sqlsource_1", "unknown"))
            .Descriptor.Id.ShouldBe("SQLSRC225");

    [Fact]
    public void Prepare_AnyOtherError_IsSqlsrc225WithEveryLineOfTheServer()
    {
        var failure = PostgresErrors.Prepare(
            Error("42883", "operator does not exist: character varying = integer", 63, "You might need to add casts."),
            Query(Declared("p", "int")),
            Prepared("sqlsource_12", "int")
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

    // A connection that dropped: the driver's message, and no SQLSTATE.
    [Fact]
    public void Prepare_ErrorWithoutASqlState_IsSqlsrc225WithTheMessageAlone()
    {
        var failure = PostgresErrors.Prepare(
            new PostgresServerError(null, "Exception while reading from stream", null, null, null),
            Query(),
            Prepared("sqlsource_1")
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.ServerLines.ShouldBe(["Exception while reading from stream"]);
    }

    // The server counts a character outside the basic plane once, and so does the tool: the fifth character of the
    // SQL, whatever the type list holds.
    [Fact]
    public void Prepare_TypeListWithACharacterOutsideTheBasicPlane_StillCountsFromTheSql()
    {
        var prepare = Prepared("sqlsource_1", "\"\U0001F600\"");
        var head = ParameterPlaceholders.HeadLength(prepare.Statement, prepare.Types);

        var failure = PostgresErrors.Prepare(
            Error("42601", "syntax error", head + 5),
            Query(Declared("p", "\"\U0001F600\"")),
            prepare
        );

        failure.ServerLines.ShouldBe(["42601: syntax error", "at character 5"]);
    }
}
