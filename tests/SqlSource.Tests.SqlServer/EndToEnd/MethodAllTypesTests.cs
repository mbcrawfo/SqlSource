using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlTypes;
using Shouldly;
using SqlSource.Testing.EndToEnd;
using Xunit;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// Every type of the type map written through a parameter of a generated method and read back through a column of one,
// by the methods of Queries/AllTypes.sql and Queries/NewTypes.sql, which bind each parameter as its described type,
// with its facets.  The rows are row 1 and row 2 of the seed, as SqlServerAllTypesRows has them, written again as the
// row InsertedId, and each write runs in a transaction that is rolled back.  The facets are proved at their edges: a
// value longer than its column, more digits than a datetime2(3) or a decimal(12,4) holds, and a decimal outside its
// precision.  The methods of NewTypes.sql need SQL Server 2025, and skip below it.
[Collection(UsesSqlServer.Name)]
public sealed class MethodAllTypesTests(SqlServerContainer sqlServer)
{
    private const int InsertedId = 3;

    // One longer than varchar_value and varbinary_value, varchar(20) and varbinary(8).
    private static readonly string TwentyOneCharacters = new('a', 21);

    private static readonly byte[] NineBytes = [1, 2, 3, 4, 5, 6, 7, 8, 9];

    // One longer than varbinary_value by a zero byte at its end.
    private static readonly byte[] EightBytesAndAZero = [1, 2, 3, 4, 5, 6, 7, 8, 0];

    [Fact]
    public async Task ListAllTypes_TheSeed_ReadsBothRows()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await connection.ListAllTypes(cancellationToken: TestContext.Current.CancellationToken);

        rows.Count.ShouldBe(2);
        SqlServerAllTypesRows.ShouldBeTheFullRow(rows[0]);
        SqlServerAllTypesRows.ShouldBeTheNullRow(rows[1]);
    }

    // The insert that Dapper runs too, of every column but datetime2_value and rowversion_value, through its method.
    [Fact]
    public async Task InsertAllTypes_TheValuesOfRow1_ReturnsThemWritten()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        await EndToEndSources.RolledBackAsync(
            connection,
            async transaction =>
            {
                var full = await FullRowAsync(connection, transaction);

                var rows = await connection.InsertAllTypes(
                    SqlServerAllTypesRows.ArgsOf<InsertAllTypesArgs>(InsertedId, full),
                    transaction: transaction,
                    cancellationToken: token
                );

                SqlServerAllTypesRows.ShouldBeTheFullRow(
                    rows.ShouldHaveSingleItem(),
                    InsertedId,
                    nullProperties: new[] { nameof(AllTypesDto.Datetime2Value) }.ToHashSet(StringComparer.Ordinal)
                );
            },
            token
        );
    }

    // Every column but the rowversion, which the server fills, with the values of row 1: among them a datetime2 of
    // seven digits, whose last a datetime would lose.  The row is read back by the OUTPUT and again by ListAllTypes.
    [Fact]
    public async Task WriteAllTypes_TheValuesOfRow1_WritesThemWhole()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        await EndToEndSources.RolledBackAsync(
            connection,
            async transaction =>
            {
                var full = await FullRowAsync(connection, transaction);

                var written = await connection.WriteAllTypes(
                    SqlServerAllTypesRows.ArgsOf<WriteAllTypesArgs>(InsertedId, full),
                    transaction: transaction,
                    cancellationToken: token
                );

                SqlServerAllTypesRows.ShouldBeTheFullRow(written, InsertedId);
                var rows = await connection.ListAllTypes(transaction: transaction, cancellationToken: token);
                SqlServerAllTypesRows.ShouldBeTheFullRow(rows.Single(static row => row.Id == InsertedId), InsertedId);
            },
            token
        );
    }

    // A null of every type is sent as a NULL of the type, a sql_variant's too.
    [Fact]
    public async Task WriteAllTypes_EveryColumnNull_WritesTheNullRow()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        await EndToEndSources.RolledBackAsync(
            connection,
            async transaction =>
            {
                var written = await connection.WriteAllTypes(
                    SqlServerAllTypesRows.ArgsOf<WriteAllTypesArgs>(InsertedId, row: null),
                    transaction: transaction,
                    cancellationToken: token
                );

                SqlServerAllTypesRows.ShouldBeTheNullRow(written, InsertedId);
            },
            token
        );
    }

    // No insert can write a rowversion, so it is proved as a parameter of a WHERE, bound as a timestamp.
    [Fact]
    public async Task FindByRowversion_TheRowversionOfRow1_FindsRow1()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        var full = (await connection.ListAllTypes(cancellationToken: token))[0];

        var found = await connection.FindByRowversion(
            new FindByRowversionArgs(full.RowversionValue.ShouldNotBeNull()),
            cancellationToken: token
        );

        found.ShouldBe([new FindByRowversionDto(SqlServerAllTypesRows.FullRowId)]);
    }

    // The value is bound as a datetime2 of scale 7, so it is compared whole: one tick more matches nothing, where a
    // datetime, to 1/300 of a second, would match neither.
    [Fact]
    public async Task FindByDatetime2_TheValueOfRow1_FindsRow1AndNotATickLater()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        var value = (await connection.ListAllTypes(cancellationToken: token))[0].Datetime2Value.ShouldNotBeNull();

        var found = await connection.FindByDatetime2(new FindByDatetime2Args(value), cancellationToken: token);
        var later = await connection.FindByDatetime2(
            new FindByDatetime2Args(value.AddTicks(1)),
            cancellationToken: token
        );

        found.ShouldBe([new FindByDatetime2Dto(SqlServerAllTypesRows.FullRowId)]);
        later.ShouldBeEmpty();
    }

    // A string longer than its varchar(20) and a byte[] longer than its varbinary(8) are sent whole, so the server
    // refuses the insert, where SqlClient would cut them to the parameter's Size and store less than was given: 2628
    // from SQL Server 2019, which names the column, and 8152 before it.  Neither ends in what the server drops without
    // an error, a space of a string or a zero byte of a byte[].
    [Theory]
    [InlineData(nameof(WriteAllTypesArgs.VarcharValue))]
    [InlineData(nameof(WriteAllTypesArgs.VarbinaryValue))]
    public async Task WriteAllTypes_ValueLongerThanItsColumn_IsRefusedByTheServer(string property)
    {
        var token = TestContext.Current.CancellationToken;
        var args = SqlServerAllTypesRows.ArgsOf<WriteAllTypesArgs>(InsertedId, row: null) with
        {
            VarcharValue = property == nameof(WriteAllTypesArgs.VarcharValue) ? TwentyOneCharacters : null,
            VarbinaryValue = property == nameof(WriteAllTypesArgs.VarbinaryValue) ? NineBytes : null,
        };
        await using var connection = EndToEndSources.App(sqlServer);

        var thrown = await Should.ThrowAsync<SqlException>(() =>
            EndToEndSources.RolledBackAsync(
                connection,
                transaction => connection.WriteAllTypes(args, transaction: transaction, cancellationToken: token),
                token
            )
        );

        thrown.Number.ShouldBe(sqlServer.Major >= 15 ? 2628 : 8152, thrown.Message);
    }

    // A byte[] longer than its varbinary(8) only by a trailing zero byte is sent whole, bound at varbinary(max) as the
    // refusal above proves, and the server stores it without that byte and with no error, as it drops the trailing
    // spaces of a string: the one over-long value that an insert takes.
    [Fact]
    public async Task WriteAllTypes_ByteArrayLongerOnlyByATrailingZero_IsStoredWithoutIt()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        await EndToEndSources.RolledBackAsync(
            connection,
            async transaction =>
            {
                var written = await connection.WriteAllTypes(
                    SqlServerAllTypesRows.ArgsOf<WriteAllTypesArgs>(InsertedId, row: null) with
                    {
                        VarbinaryValue = EightBytesAndAZero,
                    },
                    transaction: transaction,
                    cancellationToken: token
                );

                written.VarbinaryValue.ShouldBe(EightBytesAndAZero[..8]);
            },
            token
        );
    }

    // A value longer than the char(5) is compared whole, and matches no row that holds its first five characters,
    // where SqlClient would cut it to them and match.
    [Fact]
    public async Task FindByChar_ValueLongerThanItsColumn_MatchesNoRowOfItsStart()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        await EndToEndSources.RolledBackAsync(
            connection,
            async transaction =>
            {
                _ = await connection.WriteAllTypes(
                    SqlServerAllTypesRows.ArgsOf<WriteAllTypesArgs>(InsertedId, row: null) with
                    {
                        CharValue = "abcde",
                    },
                    transaction: transaction,
                    cancellationToken: token
                );

                var whole = await connection.FindByChar(
                    new FindByCharArgs("abcde"),
                    transaction: transaction,
                    cancellationToken: token
                );
                var longer = await connection.FindByChar(
                    new FindByCharArgs("abcdef"),
                    transaction: transaction,
                    cancellationToken: token
                );

                whole.ShouldBe([new FindByCharDto(InsertedId)]);
                longer.ShouldBeEmpty();
            },
            token
        );
    }

    // The parameter is bound at scale 7 whatever the column's, so the server rounds the value as it assigns it, and
    // SqlClient, which would truncate it at scale 3, does not: .1235678 reads back .1240000.
    [Theory]
    [InlineData(1234567, 1230000)]
    [InlineData(1235678, 1240000)]
    public async Task WriteDatetime2Of3_SevenDigits_ReadsBackAsTheServerRoundsThem(long ticks, long expectedTicks)
    {
        var second = new DateTime(2024, 3, 15, 13, 45, 30, DateTimeKind.Unspecified);
        await using var connection = EndToEndSources.App(sqlServer);

        var written = await connection.WriteDatetime2Of3(
            new WriteDatetime2Of3Args(second.AddTicks(ticks)),
            cancellationToken: TestContext.Current.CancellationToken
        );

        written.ShouldBe(new WriteDatetime2Of3Dto(second.AddTicks(expectedTicks)));
    }

    // A decimal with more scale than its decimal(12,4) is rounded to it by SqlClient, half away from zero, as the
    // server rounds it on an insert, so the row holds 1.2346 and a WHERE given 1.23456 compares 1.2346 and matches it.
    // Bound by its value, 1.23456 would be a decimal(6,5), which the WHERE compares unrounded, and matches nothing.
    [Fact]
    public async Task WriteAllTypes_DecimalWithMoreScale_IsRoundedAndMatchedRounded()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        await EndToEndSources.RolledBackAsync(
            connection,
            async transaction =>
            {
                var written = await connection.WriteAllTypes(
                    SqlServerAllTypesRows.ArgsOf<WriteAllTypesArgs>(InsertedId, row: null) with
                    {
                        DecimalValue = 1.23456m,
                    },
                    transaction: transaction,
                    cancellationToken: token
                );

                var found = await connection.FindByDecimal(
                    new FindByDecimalArgs(1.23456m),
                    transaction: transaction,
                    cancellationToken: token
                );

                written.DecimalValue.ShouldBe(1.2346m);
                found.ShouldBe([new FindByDecimalDto(InsertedId)]);
            },
            token
        );
    }

    // A decimal with more digits before the point than its decimal(12,4) holds is refused by SqlClient before it is
    // sent, where by value the server would report the overflow.
    [Fact]
    public async Task WriteAllTypes_DecimalOutsideItsPrecision_ThrowsBeforeSending()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var thrown = await Should.ThrowAsync<ArgumentException>(() =>
            EndToEndSources.RolledBackAsync(
                connection,
                transaction =>
                    connection.WriteAllTypes(
                        SqlServerAllTypesRows.ArgsOf<WriteAllTypesArgs>(InsertedId, row: null) with
                        {
                            DecimalValue = 123456789.1m,
                        },
                        transaction: transaction,
                        cancellationToken: token
                    ),
                token
            )
        );

        thrown.Message.ShouldContain("out of range");
    }

    // A C# enum over byte, which the query's type override gives tinyint, is sent as its number and read back from the
    // tinyint, the one type SqlClient reads it from.
    [Fact]
    public async Task ListTinyintLevels_High_ReadsItAsALevel()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var levels = await connection.ListTinyintLevels(
            new ListTinyintLevelsArgs(Level.High),
            cancellationToken: TestContext.Current.CancellationToken
        );

        levels.ShouldBe([new ListTinyintLevelsDto(Level.High)]);
    }

    [Fact]
    public async Task ListNewTypes_TheSeed_ReadsBothRows()
    {
        sqlServer.SkipBelow(17, "SQL Server before 2025 has no json or vector.");
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await connection.ListNewTypes(cancellationToken: TestContext.Current.CancellationToken);

        rows.Count.ShouldBe(2);
        SqlServerAllTypesRows.ShouldBeTheFullNewTypesRow(rows[0]);
        SqlServerAllTypesRows.ShouldBeTheNullNewTypesRow(rows[1]);
    }

    // The json is sent as an nvarchar(max), and the vector by its value, whose length gives its dimension.
    [Fact]
    public async Task WriteNewTypes_TheValuesOfRow1_WritesThemWhole()
    {
        sqlServer.SkipBelow(17, "SQL Server before 2025 has no json or vector.");
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        await EndToEndSources.RolledBackAsync(
            connection,
            async transaction =>
            {
                var written = await connection.WriteNewTypes(
                    new WriteNewTypesArgs(
                        InsertedId,
                        /*lang=json,strict*/
                        """{"name":"Zoë","tags":["a","b"]}""",
                        new SqlVector<float>(new[] { 1.5f, -2.25f, 0.5f })
                    ),
                    transaction: transaction,
                    cancellationToken: token
                );

                SqlServerAllTypesRows.ShouldBeTheFullNewTypesRow(written, InsertedId);
            },
            token
        );
    }

    // A null vector is sent as the null of vector(3), which the column takes, where DBNull would be an nvarchar.
    [Fact]
    public async Task WriteNewTypes_BothColumnsNull_WritesTheNullRow()
    {
        sqlServer.SkipBelow(17, "SQL Server before 2025 has no json or vector.");
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        await EndToEndSources.RolledBackAsync(
            connection,
            async transaction =>
            {
                var written = await connection.WriteNewTypes(
                    new WriteNewTypesArgs(InsertedId, null, null),
                    transaction: transaction,
                    cancellationToken: token
                );

                SqlServerAllTypesRows.ShouldBeTheNullNewTypesRow(written, InsertedId);
            },
            token
        );
    }

    [Fact]
    public async Task GetNewType_RowWithAVector_ReadsIt()
    {
        sqlServer.SkipBelow(17, "SQL Server before 2025 has no vector.");
        await using var connection = EndToEndSources.App(sqlServer);

        var row = await connection.GetNewType(
            new GetNewTypeArgs(SqlServerAllTypesRows.FullRowId),
            cancellationToken: TestContext.Current.CancellationToken
        );

        row.Id.ShouldBe(SqlServerAllTypesRows.FullRowId);
        row.V.Memory.ToArray().ShouldBe([1.5f, -2.25f, 0.5f]);
    }

    // SqlClient reads a NULL vector as the null vector, with no throw, so the method checks a column that the marker
    // says is not null, and throws as a NULL of any other type does.
    [Fact]
    public async Task GetNewType_NullInANotNullVector_ThrowsInvalidCastException()
    {
        sqlServer.SkipBelow(17, "SQL Server before 2025 has no vector.");
        await using var connection = EndToEndSources.App(sqlServer);

        var thrown = await Should.ThrowAsync<InvalidCastException>(() =>
            connection.GetNewType(
                new GetNewTypeArgs(SqlServerAllTypesRows.NullRowId),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        thrown.Message.ShouldBe("Column 'v' is null.");
    }

    // Row 1 of the seed, read through the method in the write's transaction.
    private static async Task<AllTypesDto> FullRowAsync(SqlConnection connection, DbTransaction transaction) =>
        (
            await connection.ListAllTypes(
                transaction: transaction,
                cancellationToken: TestContext.Current.CancellationToken
            )
        ).Single(static row => row.Id == SqlServerAllTypesRows.FullRowId);
}
