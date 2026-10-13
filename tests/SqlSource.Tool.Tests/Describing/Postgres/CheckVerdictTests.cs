using System;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// What the answer of "check" says against the answer of "columns": the two describes saw one query when the
// columns are equal in count, names and types.
public class CheckVerdictTests
{
    private static readonly PostgresColumnRow[] Described =
    [
        new(0, "id", 23, 16396, 1, null, null, null),
        new(1, "name", 1043, 16396, 2, 100, null, null),
    ];

    private static CheckVerdict Read(PostgresCheckAnswer answer, params PostgresColumnRow[] described) =>
        NpgsqlCheck.Read(answer, described, PostgresRows.Of(PostgresRows.Int4, PostgresRows.Varchar));

    private static PostgresCheckAnswer Columns(params PostgresCheckColumn[] columns) => new(columns, null, null);

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
            .ShouldBe(new CheckVerdict(NpgsqlCheck.Differ, "column 1 is text for Npgsql and integer for SqlSource"));

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

    [Fact]
    public void Read_DriverError_IsUnboundWithItsMessage() =>
        Read(new PostgresCheckAnswer(null, null, new PostgresBindError("p", "no such type")), Described)
            .ShouldBe(new CheckVerdict(NpgsqlCheck.Unbound, "no such type"));

    [Fact]
    public void Read_AnswerWithNothing_Throws() =>
        Should.Throw<InvalidOperationException>(() => Read(new PostgresCheckAnswer(null, null, null), Described));
}
