using System.Globalization;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// What the server said of a query, or what the tool refuses before it asks, as the failure the run reports at the
// query: each row of the errors of the describer, from errors the test makes.
public sealed class SqlServerErrorsTests
{
    private const string CastOrDeclareHelp =
        "add a CAST around a use of the parameter, or declare its type with -- param:";

    private static readonly SqlServerType Int = new("int", 4, 10, 0, null);
    private static readonly SqlServerType Name100 = new("nvarchar(100)", 200, 0, 0, null);

    // The four errors of a parameter whose type the server cannot deduce, each with a message that names it.
    [Theory]
    [InlineData(11502, "The type for parameter '@p' cannot be deduced in this context.")]
    [InlineData(
        11503,
        "The parameter type cannot be deduced because a single expression contains two untyped parameters, '@p' and "
            + "'@q'."
    )]
    [InlineData(
        11506,
        "The parameter type for '@p' cannot be uniquely deduced; two possibilities are 'sql_variant' and 'xml'."
    )]
    [InlineData(11507, "The parameter type for '@p' cannot be deduced because no type would make the query valid.")]
    public void Parameters_TypeCannotBeDeduced_IsSqlsrc226NamingTheParameter(int number, string message)
    {
        var query = Of(SqlDialectRules.SqlServer, "SELECT @p + @q AS x");

        var failure = SqlServerErrors.Parameters(query, ErrorOf((number, message)), Copied(query));

        failure.Descriptor.Id.ShouldBe("SQLSRC226");
        failure.Arguments.ShouldBe(["@p", Query]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBe([$"Msg {number}, Level 16, State 1: {message}"]);
        failure.Help.ShouldBe(CastOrDeclareHelp);
    }

    // A copy is named in the message by its own name, and the error names its parameter.
    [Fact]
    public void Parameters_MessageNamesACopy_NamesItsParameter()
    {
        var query = Of(SqlDialectRules.SqlServer, "SELECT @a AS a WHERE @p = 1 OR COALESCE(@p, @a) = 1");

        var failure = SqlServerErrors.Parameters(
            query,
            ErrorOf((11502, "The type for parameter '@p_2' cannot be deduced in this context.")),
            Copied(query)
        );

        failure.Arguments.ShouldBe(["@p", Query]);
    }

    // "@pp" holds "@p" and is not it: a name counts only as a whole token.
    [Fact]
    public void Parameters_MessageNamesNoParameterOfTheList_NamesTheFirstUndeclared()
    {
        var query = Of(SqlDialectRules.SqlServer, "SELECT @p AS p, @q AS q", ("p", "int"));

        var failure = SqlServerErrors.Parameters(
            query,
            ErrorOf((11502, "The type for parameter '@pp' cannot be deduced in this context.")),
            Copied(query)
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC226");
        failure.Arguments.ShouldBe(["@q", Query]);
    }

    [Fact]
    public void Parameters_NameInAnotherCase_NamesTheParameterAsTheListSpellsIt()
    {
        var query = Of(SqlDialectRules.SqlServer, "SELECT @a AS a, @Name AS n");

        var failure = SqlServerErrors.Parameters(
            query,
            ErrorOf((11502, "The type for parameter '@NAME' cannot be deduced in this context.")),
            Copied(query)
        );

        failure.Arguments.ShouldBe(["@Name", Query]);
    }

    // The rename prevents 11508, so one is a bug of the describer: reported with the server's line, to be seen.
    [Fact]
    public void Parameters_UsedMoreThanOnce_IsSqlsrc225WithTheServersLine()
    {
        var query = Of(SqlDialectRules.SqlServer, "SELECT @p AS p");
        const string Message = "The undeclared parameter '@p' is used more than once in the batch being analyzed.";

        var failure = SqlServerErrors.Parameters(query, ErrorOf((11508, Message)), Copied(query));

        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Arguments.ShouldBe([Query, Message]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBe([$"Msg 11508, Level 16, State 1: {Message}"]);
        failure.Help.ShouldBeNull();
    }

    // The cause is the first error and 11501 says only that there was one: the message is the cause's, and every
    // error is a line, in the server's order.
    [Fact]
    public void Parameters_CompileErrorThen11501_IsSqlsrc225WithTheCauseFirst()
    {
        var query = Of(SqlDialectRules.SqlServer, "SELECT nope FROM dbo.users WHERE id = @p");

        var failure = SqlServerErrors.Parameters(
            query,
            ErrorOf(
                (207, "Invalid column name 'nope'."),
                (11501, "The batch could not be analyzed because of compile errors.")
            ),
            Copied(query)
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Arguments.ShouldBe([Query, "Invalid column name 'nope'."]);
        failure.ServerLines.ShouldBe([
            "Msg 207, Level 16, State 1: Invalid column name 'nope'.",
            "Msg 11501, Level 16, State 1: The batch could not be analyzed because of compile errors.",
        ]);
    }

    // A discovered type that is not valid, and a type the query's own text names that the database lacks: neither is
    // a declaration, which "declared" resolved before.
    [Theory]
    [InlineData(11504)]
    [InlineData(11505)]
    [InlineData(2715)]
    public void Parameters_OtherError_IsSqlsrc225(int number)
    {
        var query = Of(SqlDialectRules.SqlServer, "SELECT CAST(@p AS dbo.Nope) AS p");

        var failure = SqlServerErrors.Parameters(
            query,
            ErrorOf(
                (number, "The server's message."),
                (11501, "The batch could not be analyzed because of compile errors.")
            ),
            Copied(query)
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Arguments.ShouldBe([Query, "The server's message."]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
    }

    [Theory]
    [InlineData(2715, "Column, parameter, or variable #1: Cannot find data type dbo.Nope.")]
    [InlineData(2724, "Parameter or variable '@p' has an invalid data type.")]
    public void Declared_TypeTheDatabaseLacks_IsSqlsrc228AtResolveType(int number, string message)
    {
        var query = Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.users WHERE email = @p", ("p", "dbo.Nope"));

        var failure = SqlServerErrors.Declared(
            query,
            query.Parameters[0],
            DescribeErrors((number, message), (11501, "The batch could not be analyzed because of compile errors."))
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments.ShouldBe(["dbo.Nope", "@p", Query, "the database has no such type"]);
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.ServerLines.ShouldBe([
            $"Msg {number}: {message}",
            "Msg 11501: The batch could not be analyzed because of compile errors.",
        ]);
        failure.Help.ShouldBe(SqlServerErrors.NoSuchTypeHelp);
    }

    [Fact]
    public void Declared_SyntaxError_IsSqlsrc228ItIsNotAType()
    {
        var query = Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.users WHERE id = @p", ("p", "int"));

        var failure = SqlServerErrors.Declared(
            query,
            query.Parameters[0],
            DescribeErrors((102, "Incorrect syntax near ';'."))
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments.ShouldBe(["int", "@p", Query, "it is not a type"]);
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.ServerLines.ShouldBe(["Msg 102: Incorrect syntax near ';'."]);
        failure.Help.ShouldBe(SqlServerErrors.NotATypeHelp);
    }

    // A declared parameter's name too long for an identifier is refused as the same name undeclared is, by the error
    // that parameters raises for it: SQLSRC225.
    [Fact]
    public void Declared_NameTooLong_IsSqlsrc225AtResolveType()
    {
        var name = new string('p', 128);
        var query = Of(SqlDialectRules.SqlServer, $"SELECT id FROM dbo.users WHERE id = @{name}", (name, "int"));
        var tooLong = $"The identifier that starts with '@{name[..127]}' is too long. Maximum length is 128.";

        var failure = SqlServerErrors.Declared(
            query,
            query.Parameters[0],
            DescribeErrors(
                (103, tooLong),
                (103, tooLong),
                (11501, "The batch could not be analyzed because of compile errors.")
            )
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Arguments.ShouldBe([Query, tooLong]);
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.ServerLines.ShouldBe([
            $"Msg 103: {tooLong}",
            $"Msg 103: {tooLong}",
            "Msg 11501: The batch could not be analyzed because of compile errors.",
        ]);
        failure.Help.ShouldBeNull();
    }

    // The same error for a name that fits is of the type, such as a name of a type too long.
    [Fact]
    public void Declared_IdentifierTooLongForANameThatFits_IsSqlsrc228ItIsNotAType()
    {
        var type = "dbo." + new string('t', 200);
        var query = Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.users WHERE id = @p", ("p", type));

        var failure = SqlServerErrors.Declared(
            query,
            query.Parameters[0],
            DescribeErrors((103, "The identifier that starts with 'ttt' is too long. Maximum length is 128."))
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments.ShouldBe([type, "@p", Query, "it is not a type"]);
    }

    // The grammar's refusal is the tool's own, before anything is sent: no server line.
    [Fact]
    public void TypeRefused_Reason_IsSqlsrc228WithTheReasonAndNoServerLine()
    {
        var query = Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.users WHERE id = @p", ("p", "int; DROP"));
        const string Reason = "it is not written as a type name: a ; is not part of a type name";

        var failure = SqlServerErrors.TypeRefused(query, query.Parameters[0], Reason);

        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments.ShouldBe(["int; DROP", "@p", Query, Reason]);
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(SqlServerErrors.NotATypeHelp);
    }

    [Fact]
    public void NoType_Parameter_IsSqlsrc226WithNoServerLine()
    {
        var failure = SqlServerErrors.NoType(Of(SqlDialectRules.SqlServer, "SELECT @p AS p"), "p");

        failure.Descriptor.Id.ShouldBe("SQLSRC226");
        failure.Arguments.ShouldBe(["@p", Query]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(CastOrDeclareHelp);
    }

    [Fact]
    public void CopiesDisagree_TwoTypes_IsSqlsrc262NamingBoth()
    {
        var failure = SqlServerErrors.CopiesDisagree(
            Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.users WHERE id = @p AND name = @p"),
            "p",
            Int,
            Name100
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC262");
        failure.Arguments.ShouldBe(["@p", Query, "int", "nvarchar(100)"]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe("declare the parameter's type with -- param:");
        MessageOf(failure)
            .ShouldBe("The copies of the parameter '@p' of the query 'Q' have the types 'int' and 'nvarchar(100)'");
    }

    // A user type is named by its schema and name, as a declaration would write it.
    [Fact]
    public void CopiesDisagree_UserType_NamesItBySchemaAndName() =>
        SqlServerErrors
            .CopiesDisagree(
                Of(SqlDialectRules.SqlServer, "SELECT @p AS p"),
                "p",
                new SqlServerType("nvarchar(320)", 640, 0, 0, new SqlServerUserType("dbo", "Email", null)),
                Name100
            )
            .Arguments.ShouldBe(["@p", Query, "dbo.Email", "nvarchar(100)"]);

    [Fact]
    public void ListsDiffer_BothWays_IsSqlsrc231SayingWhichSideLacksEach()
    {
        var failure = SqlServerErrors.ListsDiffer(
            Of(SqlDialectRules.SqlServer, "SELECT @id AS id"),
            [new ListDifference("id", true), new ListDifference("x", false)]
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC231");
        failure.Arguments.ShouldBe([
            "SQL Server",
            Query,
            "SqlSource reads '@id' as a parameter and SQL Server does not; SQL Server reads '@x' as a parameter and "
                + "SqlSource does not",
        ]);
        failure.Step.ShouldBe(DescribeStep.CheckParameters);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(
            "rewrite the query so that SqlSource and SQL Server read the same parameters, or give a parameter that "
                + "SqlSource reads as a variable a -- param: marker with its type"
        );
        MessageOf(failure)
            .ShouldBe(
                "SQL Server reads the parameters of the query 'Q' differently from SqlSource: SqlSource reads '@id' "
                    + "as a parameter and SQL Server does not; SQL Server reads '@x' as a parameter and SqlSource does "
                    + "not"
            );
    }

    // A connection lost at a later call: the fixed line, and the message is the line.
    [Fact]
    public void Failed_ConnectionLostAtProbe_IsSqlsrc225WithTheFixedLine()
    {
        var failure = SqlServerErrors.Failed(
            Of(SqlDialectRules.SqlServer, "SELECT @p AS p"),
            new SqlServerServerError([new SqlServerErrorRow(0, 0, 0, ConnectionErrors.LostConnection)]),
            DescribeStep.DescribeParameters
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Arguments.ShouldBe([Query, "the connection to the server was lost"]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBe(["the connection to the server was lost"]);
        failure.Help.ShouldBeNull();
    }

    [Fact]
    public void Failed_ErrorAtDeclared_IsAtResolveType() =>
        SqlServerErrors
            .Failed(
                Of(SqlDialectRules.SqlServer, "SELECT @p AS p"),
                new SqlServerServerError([new SqlServerErrorRow(-2, 11, 0, ConnectionErrors.NoAnswerInTime)]),
                DescribeStep.ResolveType
            )
            .Step.ShouldBe(DescribeStep.ResolveType);

    [Fact]
    public void Columns_ConflictingResults_Is263()
    {
        const string Message =
            "The metadata could not be determined because the statement 'SELECT id FROM dbo.users' is not "
            + "compatible with the statement 'SELECT name FROM dbo.users'.";

        var failure = SqlServerErrors.Columns(
            Of(SqlDialectRules.SqlServer, "IF @a = 1 SELECT id FROM dbo.users ELSE SELECT name FROM dbo.users"),
            [new SqlServerDescribeError(11509, 3, Message)],
            DescribeStep.DescribeColumns
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC263");
        failure.Arguments.ShouldBe([Query]);
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
        failure.ServerLines.ShouldBe([$"Msg 11509: {Message}"]);
        failure.Help.ShouldBeNull();
        MessageOf(failure).ShouldBe("The query 'Q' returns rows of different shapes on different paths");
    }

    [Fact]
    public void Columns_DynamicSql_Is264()
    {
        const string Message =
            "The metadata could not be determined because statement 'EXEC(@s)' contains dynamic SQL.  Consider "
            + "using the WITH RESULT SETS clause to explicitly describe the result set.";

        var failure = SqlServerErrors.Columns(
            Of(SqlDialectRules.SqlServer, "DECLARE @s nvarchar(max) = N'SELECT 1 AS a'; EXEC(@s)"),
            [new SqlServerDescribeError(11513, 4, Message)],
            DescribeStep.DescribeColumns
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC264");
        failure.Arguments.ShouldBe([Query]);
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
        failure.ServerLines.ShouldBe([$"Msg 11513: {Message}"]);
        failure.Help.ShouldBeNull();
        MessageOf(failure).ShouldBe("The query 'Q' runs SQL that the server cannot read until it runs");
    }

    // The cause is the row of its type, wherever it stands, and every row is still a line.
    [Fact]
    public void Columns_TemporaryTable_Is265WithHelp()
    {
        const string Message =
            "The metadata could not be determined because statement 'SELECT a FROM #y' uses a temp table.  Metadata "
            + "discovery only supports temp tables when analyzing a single-statement batch.";

        var failure = SqlServerErrors.Columns(
            Of(SqlDialectRules.SqlServer, "CREATE TABLE #y (a int NOT NULL); SELECT a FROM #y"),
            [
                new SqlServerDescribeError(208, 2, "Invalid object name '#y'."),
                new SqlServerDescribeError(11525, 10, Message),
            ],
            DescribeStep.DescribeColumns
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC265");
        failure.Arguments.ShouldBe([Query]);
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
        failure.ServerLines.ShouldBe(["Msg 208: Invalid object name '#y'.", $"Msg 11525: {Message}"]);
        failure.Help.ShouldBe("make the query one statement, or use a table variable in place of the temporary table");
        MessageOf(failure).ShouldBe("The query 'Q' uses a temporary table in a batch of several statements");
    }

    // Every parameter is declared, so this cannot happen: it is reported as the server rejecting the query, to be
    // seen, and the message is the row of its type.
    [Fact]
    public void Columns_UndeclaredParameter_Is225()
    {
        const string Message =
            "The metadata could not be determined because statement 'SELECT @p AS p' uses an undeclared parameter "
            + "in a context that affects its metadata.";

        var failure = SqlServerErrors.Columns(
            Of(SqlDialectRules.SqlServer, "SELECT @p AS p"),
            [
                new SqlServerDescribeError(102, 2, "Incorrect syntax near 'x'."),
                new SqlServerDescribeError(11521, 8, Message),
            ],
            DescribeStep.DescribeColumns
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Arguments.ShouldBe([Query, Message]);
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
        failure.ServerLines.ShouldBe(["Msg 102: Incorrect syntax near 'x'.", $"Msg 11521: {Message}"]);
        failure.Help.ShouldBeNull();
    }

    // An OUTPUT without INTO on a table with a trigger, an unknown column, a GO inside the query and a word read as
    // a procedure: the cause first and the row that says the query did not compile last, each of the type the server
    // gives it.
    [Theory]
    [InlineData(
        334,
        1,
        "The target table 'dbo.trig' of the DML statement cannot have any enabled triggers if the statement contains "
            + "an OUTPUT clause without INTO clause.",
        11501,
        2,
        "The batch could not be analyzed because of compile errors."
    )]
    [InlineData(
        207,
        2,
        "Invalid column name 'nope'.",
        11501,
        2,
        "The batch could not be analyzed because of compile errors."
    )]
    [InlineData(
        102,
        2,
        "Incorrect syntax near 'GO'.",
        11501,
        2,
        "The batch could not be analyzed because of compile errors."
    )]
    [InlineData(
        2812,
        1,
        "Could not find stored procedure 'SELEC'.",
        11529,
        1,
        "The metadata could not be determined because every code path results in an error; see previous errors for "
            + "some of these."
    )]
    public void Columns_OtherType_Is225WithEveryLineInOrder(
        int cause,
        int causeType,
        string causeText,
        int last,
        int lastType,
        string lastText
    )
    {
        var failure = SqlServerErrors.Columns(
            Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.users"),
            [
                new SqlServerDescribeError(cause, causeType, causeText),
                new SqlServerDescribeError(last, lastType, lastText),
            ],
            DescribeStep.DescribeColumns
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Arguments.ShouldBe([Query, causeText]);
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
        failure.ServerLines.ShouldBe([$"Msg {cause}: {causeText}", $"Msg {last}: {lastText}"]);
        failure.Help.ShouldBeNull();
    }

    [Fact]
    public void Columns_TableStep_IsTableMatch() =>
        SqlServerErrors
            .Columns(
                Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.users"),
                [new SqlServerDescribeError(208, 2, "Invalid object name 'dbo.users'.")],
                DescribeStep.TableMatch
            )
            .Step.ShouldBe(DescribeStep.TableMatch);

    // The parameters' procedure reports a temporary table of a batch as a missing table, where the columns' gives its
    // cause.
    [Fact]
    public void AfterRefusal_TemporaryTable_Is265()
    {
        var query = Of(SqlDialectRules.SqlServer, "CREATE TABLE #y (a int NOT NULL); SELECT a FROM #y WHERE a = @p");
        const string Message =
            "The metadata could not be determined because statement 'SELECT a FROM #y WHERE a = @p' uses a temp "
            + "table.  Metadata discovery only supports temp tables when analyzing a single-statement batch.";

        var failure = SqlServerErrors.AfterRefusal(
            query,
            Refused(),
            Copied(query),
            new SqlServerColumnsAnswer(null, [new SqlServerDescribeError(11525, 10, Message)], null)
        );

        failure.Descriptor.Id.ShouldBe("SQLSRC265");
        failure.Arguments.ShouldBe([Query]);
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
        failure.ServerLines.ShouldBe([$"Msg 11525: {Message}"]);
        failure.Help.ShouldBe(SqlServerErrors.TemporaryTableHelp);
    }

    // Rows of no shape's cause leave the parameters' failure, with its lines.
    [Fact]
    public void AfterRefusal_OtherRows_IsParametersFailure()
    {
        var query = Of(SqlDialectRules.SqlServer, "SELECT a FROM #y WHERE a = @p");

        var failure = SqlServerErrors.AfterRefusal(
            query,
            Refused(),
            Copied(query),
            new SqlServerColumnsAnswer(
                null,
                [
                    new SqlServerDescribeError(208, 2, "Invalid object name '#y'."),
                    new SqlServerDescribeError(
                        11529,
                        1,
                        "The metadata could not be determined because every code path results in an error; see "
                            + "previous errors for some of these."
                    ),
                ],
                null
            )
        );

        failure.ShouldBe(SqlServerErrors.Parameters(query, Refused(), Copied(query)));
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBe([
            "Msg 208, Level 16, State 1: Invalid object name '#y'.",
            "Msg 11501, Level 16, State 1: The batch could not be analyzed because of compile errors.",
        ]);
    }

    [Fact]
    public void AfterRefusal_ColumnsAnswered_IsParametersFailure()
    {
        var query = Of(SqlDialectRules.SqlServer, "SELECT a FROM #y WHERE a = @p");
        var row = new SqlServerColumnRow(
            false,
            1,
            "a",
            false,
            "int",
            4,
            10,
            0,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            false
        );

        SqlServerErrors
            .AfterRefusal(query, Refused(), Copied(query), new SqlServerColumnsAnswer([row], null, null))
            .ShouldBe(SqlServerErrors.Parameters(query, Refused(), Copied(query)));
    }

    [Fact]
    public void NoColumnName_Is266()
    {
        var failure = SqlServerErrors.NoColumnName(Of(SqlDialectRules.SqlServer, "SELECT id, 1 FROM dbo.users"), 2);

        failure.Descriptor.Id.ShouldBe("SQLSRC266");
        failure.Arguments.ShouldBe(["2", Query]);
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe("give the column a name with AS");
        MessageOf(failure).ShouldBe("Column 2 of the query 'Q' has no name");
    }

    private static CopiedSql Copied(DescribeRequest query) => ParameterCopies.Rename(query.Sql, query.Parameters);

    private static SqlServerServerError ErrorOf(params (int Number, string Message)[] errors) =>
        new([.. errors.Select(error => new SqlServerErrorRow(error.Number, 16, 1, error.Message))]);

    // What parameters says of a temporary table that a batch creates: the table is missing, and the batch did not
    // compile.
    private static SqlServerServerError Refused() =>
        ErrorOf(
            (208, "Invalid object name '#y'."),
            (SqlServerErrors.RefusedBatch, "The batch could not be analyzed because of compile errors.")
        );

    private static SqlServerDeclaredAnswer DescribeErrors(params (int Number, string Message)[] errors) =>
        new(null, [.. errors.Select(error => new SqlServerDescribeError(error.Number, 1, error.Message))], null);

    private static string MessageOf(DescribeFailure failure) =>
        string.Format(
            CultureInfo.InvariantCulture,
            failure.Descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture),
            [.. failure.Arguments]
        );
}
