using System;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// What the answer of "check" says against the answer of "columns" and the tool's own rewrite: the two describes saw
// one query when Npgsql placed each parameter where the lexer did, and the columns are equal in count, names and
// types.
public class CheckVerdictTests
{
    private static readonly PostgresColumnRow[] Described =
    [
        new(0, "id", 23, 16396, 1, null, null, null),
        new(1, "name", 1043, 16396, 2, 100, null, null),
    ];

    private static readonly PostgresCheckColumn[] Seen =
    [
        new("id", 23, "integer"),
        new("name", 1043, "character varying"),
    ];

    private const string Sql = "SELECT id, name FROM users WHERE id = @id";

    private static CheckVerdict Read(PostgresCheckAnswer answer, params PostgresColumnRow[] described) =>
        Read(answer, Sql, ["id"], described);

    private static CheckVerdict Read(
        PostgresCheckAnswer answer,
        string sql,
        string[] parameters,
        params PostgresColumnRow[] described
    ) =>
        NpgsqlCheck.Read(
            answer,
            described,
            PostgresRows.Of(PostgresRows.Int4, PostgresRows.Varchar),
            sql,
            new EquatableArray<SqlQueryParameter>([
                .. Array.ConvertAll(parameters, static name => new SqlQueryParameter(name, null, null, false)),
            ])
        );

    private static PostgresCheckAnswer Columns(params PostgresCheckColumn[] columns) => new(columns, null, null);

    // The columns of Described, and what Npgsql sent.
    private static PostgresCheckAnswer Sent(string text, params string[] parameters) =>
        new(Seen, null, null, new PostgresCheckSent(text, parameters));

    [Fact]
    public void Read_ColumnsEqualInCountNamesAndTypes_IsSame() =>
        Read(Columns(new("id", 23, "integer"), new("name", 1043, "character varying")), Described)
            .ShouldBe(new CheckVerdict(NpgsqlCheck.Same, null));

    // A statement without rows: both describes found none.
    [Fact]
    public void Read_NoColumnsOnEitherSide_IsSame() =>
        Read(Columns()).ShouldBe(new CheckVerdict(NpgsqlCheck.Same, null));

    [Fact]
    public void Read_AnotherNumberOfColumns_DiffersWithBothCounts() =>
        Read(Columns(new PostgresCheckColumn("id", 23, "integer")), Described)
            .ShouldBe(new CheckVerdict(NpgsqlCheck.Differ, "the result has 1 columns for Npgsql and 2 for SqlSource"));

    // Names are compared as they are: the server folded both.
    [Fact]
    public void Read_ColumnWithAnotherName_DiffersNamingBoth() =>
        Read(Columns(new("id", 23, "integer"), new("Name", 1043, "character varying")), Described)
            .ShouldBe(
                new CheckVerdict(NpgsqlCheck.Differ, "column 2 is named 'Name' for Npgsql and 'name' for SqlSource")
            );

    [Fact]
    public void Read_ColumnWithAnotherType_DiffersNamingBothTypes() =>
        Read(Columns(new("id", 25, "text"), new("name", 1043, "character varying")), Described)
            .ShouldBe(
                new CheckVerdict(NpgsqlCheck.Differ, "column 1 is text for Npgsql and integer for SqlSource", true)
            );

    // The first difference is the one reported.
    [Fact]
    public void Read_SeveralDifferences_ReportsTheFirst() =>
        Read(Columns(new("x", 25, "text"), new("y", 25, "text")), Described)
            .Differs.ShouldBe("column 1 is named 'x' for Npgsql and 'id' for SqlSource");

    [Fact]
    public void Read_Error_DiffersWithItsMessage() =>
        Read(
                new PostgresCheckAnswer(
                    null,
                    new PostgresServerError("42601", "syntax error at or near \"@\"", null, null, 8),
                    null
                ),
                Described
            )
            .ShouldBe(new CheckVerdict(NpgsqlCheck.Differ, "syntax error at or near \"@\""));

    // What a misread @ gives is a disagreement: an error of class 42, or Npgsql's refusal of the text, which has no
    // SQLSTATE; an answer recorded before textRefused existed has no SQLSTATE only where Npgsql refused the text.
    // Anything else, a cancel or a lost connection, says nothing of how Npgsql reads the SQL, whatever the answer
    // says of the text.
    [Theory]
    [InlineData("42601", false, NpgsqlCheck.Differ)]
    [InlineData("42P01", null, NpgsqlCheck.Differ)]
    [InlineData(null, true, NpgsqlCheck.Differ)]
    [InlineData(null, null, NpgsqlCheck.Differ)]
    [InlineData("57014", false, NpgsqlCheck.Error)]
    [InlineData("57014", null, NpgsqlCheck.Error)]
    [InlineData(null, false, NpgsqlCheck.Error)]
    public void Read_ErrorOfTheServer_IsADisagreementOnlyWhereAMisreadAtCouldGiveIt(
        string? sqlState,
        bool? textRefused,
        string result
    ) =>
        Read(
                new PostgresCheckAnswer(
                    null,
                    new PostgresServerError(sqlState, "the server's words", null, null, null),
                    null,
                    null,
                    textRefused
                ),
                Described
            )
            .ShouldBe(new CheckVerdict(result, "the server's words"));

    [Fact]
    public void Read_DriverError_IsUnboundWithItsMessage() =>
        Read(new PostgresCheckAnswer(null, null, new PostgresBindError("p", "no such type")), Described)
            .ShouldBe(new CheckVerdict(NpgsqlCheck.Unbound, "no such type"));

    [Fact]
    public void Read_AnswerWithNothing_Throws() =>
        Should.Throw<InvalidOperationException>(() => Read(new PostgresCheckAnswer(null, null, null), Described));

    [Fact]
    public void Read_SameTextsAndColumns_IsSame() =>
        Read(Sent("SELECT id, name FROM users WHERE id = $1", "id"), Described)
            .ShouldBe(new CheckVerdict(NpgsqlCheck.Same, null));

    // A slice without a lower bound that reads a column of the parameter's name: Npgsql places the parameter there
    // too, the server reads a subscript, and in a predicate the columns are the same.
    [Fact]
    public void Read_NpgsqlPlacedAParameterSqlSourceDidNot_IsDiffer() =>
        Read(
                Sent("SELECT id, name FROM users WHERE id = $1 AND tags[$1] IS NOT NULL", "id"),
                "SELECT id, name FROM users WHERE id = @id AND tags[:id] IS NOT NULL",
                ["id"],
                Described
            )
            .ShouldBe(
                new CheckVerdict(
                    NpgsqlCheck.Differ,
                    "the SQL with $n parts at character 51: '$1] IS NOT' for Npgsql and ':id] IS NO' for SqlSource",
                    PlacementDiffers: true
                )
            );

    // Each text places both parameters, and the two put them the other way round.
    [Fact]
    public void Read_ParametersInEachOthersPlace_DiffersQuotingFromTheDollar() =>
        Read(
                Sent("SELECT id, name FROM users WHERE id = $2 AND name = $1", "id", "name"),
                "SELECT id, name FROM users WHERE id = @id AND name = @name",
                ["id", "name"],
                Described
            )
            .ShouldBe(
                new CheckVerdict(
                    NpgsqlCheck.Differ,
                    "the SQL with $n parts at character 39: '$2 AND nam' for Npgsql and '$1 AND nam' for SqlSource",
                    PlacementDiffers: true
                )
            );

    // The message says where, as for any other difference of the texts: the parameter the query lacks keeps the
    // number Npgsql gave it, so that the parameters after it keep theirs.
    [Theory]
    [InlineData("SELECT id, name FROM users WHERE id = $1 AND tags[$2] IS NOT NULL", "id", "x")]
    [InlineData("SELECT id, name FROM users WHERE id = $2 AND tags[$1] IS NOT NULL", "x", "id")]
    public void Read_NpgsqlNamesAParameterTheQueryLacks_DiffersSayingWhereItPlacedIt(
        string text,
        string first,
        string second
    ) =>
        Read(
                new PostgresCheckAnswer(Seen, null, null, new PostgresCheckSent(text, [first, second])),
                "SELECT id, name FROM users WHERE id = @id AND tags[:x] IS NOT NULL",
                ["id"],
                Described
            )
            .ShouldBe(
                new CheckVerdict(
                    NpgsqlCheck.Differ,
                    "the SQL with $n parts at character 51: Npgsql placed @x there, which SqlSource does not read as a "
                        + "parameter",
                    PlacementDiffers: true
                )
            );

    // Npgsql numbers by the order of the command's parameters, and the lexer by the order of the query's list; a
    // parameter of the list that the SQL does not reference is in neither text.
    [Fact]
    public void Read_NpgsqlNumbersInAnotherOrder_IsSame() =>
        Read(
                Sent("SELECT id, name FROM users WHERE name = $1 AND id = $2", "name", "id"),
                "SELECT id, name FROM users WHERE name = @name AND id = @id",
                ["tenant", "id", "name"],
                Described
            )
            .ShouldBe(new CheckVerdict(NpgsqlCheck.Same, null));

    // Both texts hold the string as it was written.
    [Fact]
    public void Read_LiteralDollarOneInAString_IsSame() =>
        Read(
                Sent("SELECT '$1' AS a, name FROM users WHERE id = $1", "id"),
                "SELECT '$1' AS a, name FROM users WHERE id = @id",
                ["id"],
                Described
            )
            .ShouldBe(new CheckVerdict(NpgsqlCheck.Same, null));

    // Npgsql finds a parameter ignoring case, and so does the lexer: one parameter, placed twice.
    [Fact]
    public void Read_NamesDifferInCaseOnly_IsSame() =>
        Read(
                Sent("SELECT id, name FROM users WHERE id = $1 OR id = $1", "ID"),
                "SELECT id, name FROM users WHERE id = @ID OR id = @id",
                ["id"],
                Described
            )
            .ShouldBe(new CheckVerdict(NpgsqlCheck.Same, null));

    // Texts that agree leave the columns to decide.
    [Fact]
    public void Read_SameTextsButOtherColumns_DiffersByTheColumns() =>
        Read(
                new PostgresCheckAnswer(
                    [new PostgresCheckColumn("id", 23, "integer")],
                    null,
                    null,
                    new PostgresCheckSent("SELECT id, name FROM users WHERE id = $1", ["id"])
                ),
                Described
            )
            .ShouldBe(new CheckVerdict(NpgsqlCheck.Differ, "the result has 1 columns for Npgsql and 2 for SqlSource"));

    // An answer recorded before Npgsql's text was: whatever the text, the columns decide.
    [Fact]
    public void Read_AnswerWithoutSent_ComparesColumnsAlone() =>
        Read(Columns(Seen), "SELECT id, name FROM users WHERE id = @id AND tags[:id] IS NOT NULL", ["id"], Described)
            .ShouldBe(new CheckVerdict(NpgsqlCheck.Same, null));
}
