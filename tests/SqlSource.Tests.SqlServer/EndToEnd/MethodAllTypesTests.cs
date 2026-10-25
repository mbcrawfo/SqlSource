using System;
using System.Data.Common;
using System.Globalization;
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
// value longer than its column, by more than a trailing space or zero byte or by only that, more digits than a
// datetime2(3) or a decimal(12,4) holds, in a write and in a WHERE, a fraction of a decimal of scale 0, a decimal
// outside its precision, and the null of a vector where the server takes no other NULL.  The methods of NewTypes.sql
// need SQL Server 2025, and skip below it.
[Collection(UsesSqlServer.Name)]
public sealed class MethodAllTypesTests(SqlServerContainer sqlServer)
{
    private const int InsertedId = 3;

    // One longer than varchar_value and varbinary_value, varchar(20) and varbinary(8).
    private static readonly string TwentyOneCharacters = new('a', 21);

    private static readonly byte[] NineBytes = [1, 2, 3, 4, 5, 6, 7, 8, 9];

    // One longer than varchar_value by a space at its end.
    private static readonly string TwentyCharactersAndASpace = new string('a', 20) + " ";

    // One longer than varbinary_value by a zero byte at its end.
    private static readonly byte[] EightBytesAndAZero = [1, 2, 3, 4, 5, 6, 7, 8, 0];

    // The probe of MeasureVectorDistance, along the first axis.
    private static readonly float[] FirstAxis = [1f, 0f, 0f];

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
    // refuses the insert, where SqlClient would cut them to the parameter's Size and store less than was given: 2628,
    // which names the column, from SQL Server 2019 in a database of compatibility level 150 or later, as the server's
    // own default makes this one, and 8152 on 2017.  Neither ends in what the server drops without an error, a space
    // of a string or a zero byte of a byte[].
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
    // space of a string below: the over-long values that an insert takes.
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

    // A string longer than its varchar(20) only by a trailing space is sent whole, and the server stores it without the
    // space and with no error.
    [Fact]
    public async Task WriteAllTypes_StringLongerOnlyByATrailingSpace_IsStoredWithoutIt()
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
                        VarcharValue = TwentyCharactersAndASpace,
                    },
                    transaction: transaction,
                    cancellationToken: token
                );

                written.VarcharValue.ShouldBe(TwentyCharactersAndASpace[..20]);
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

    // A value longer than the char(5) only by a trailing space matches the row of its first five characters, since the
    // server pads the shorter side with spaces to compare two strings.
    [Fact]
    public async Task FindByChar_ValueLongerOnlyByASpace_MatchesTheRow()
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

                var found = await connection.FindByChar(
                    new FindByCharArgs("abcde "),
                    transaction: transaction,
                    cancellationToken: token
                );

                found.ShouldBe([new FindByCharDto(InsertedId)]);
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

    // The parameter is bound at scale 7, so a WHERE compares a datetime2(3) with all seven digits of the value: the
    // column's .124 equals .1240000, and not .1235678, which the server would round to .124 on an insert.
    [Theory]
    [InlineData(1240000, true)]
    [InlineData(1235678, false)]
    public async Task FindDatetime2Of3_SevenDigits_ComparesThemAll(long ticks, bool isFound)
    {
        var second = new DateTime(2024, 3, 15, 13, 45, 30, DateTimeKind.Unspecified);
        await using var connection = EndToEndSources.App(sqlServer);

        var found = await connection.FindDatetime2Of3(
            new FindDatetime2Of3Args(second.AddTicks(ticks)),
            cancellationToken: TestContext.Current.CancellationToken
        );

        found.Count.ShouldBe(isFound ? 1 : 0);
    }

    // A decimal with more scale than its decimal(12,4) is rounded to it by SqlClient, half away from zero, as the
    // server rounds it on an insert, so the row holds 1.2346 and a WHERE given 1.23456 compares 1.2346 and matches it.
    // Bound by its value, 1.23456 would be a decimal(6,5), which the WHERE compares unrounded, and matches nothing.
    // 1.23465 is rounded to 1.2347, and -1.23465 to -1.2347, where rounding half to even would give 1.2346 and -1.2346.
    [Theory]
    [InlineData("1.23456", "1.2346")]
    [InlineData("1.23465", "1.2347")]
    [InlineData("-1.23465", "-1.2347")]
    public async Task WriteAllTypes_DecimalWithMoreScale_IsRoundedAndMatchedRounded(string given, string rounded)
    {
        var value = decimal.Parse(given, CultureInfo.InvariantCulture);
        var expected = decimal.Parse(rounded, CultureInfo.InvariantCulture);
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        await EndToEndSources.RolledBackAsync(
            connection,
            async transaction =>
            {
                var written = await connection.WriteAllTypes(
                    SqlServerAllTypesRows.ArgsOf<WriteAllTypesArgs>(InsertedId, row: null) with
                    {
                        DecimalValue = value,
                    },
                    transaction: transaction,
                    cancellationToken: token
                );

                var found = await connection.FindByDecimal(
                    new FindByDecimalArgs(value),
                    transaction: transaction,
                    cancellationToken: token
                );

                written.DecimalValue.ShouldBe(expected);
                found.ShouldBe([new FindByDecimalDto(InsertedId)]);
            },
            token
        );
    }

    // A decimal for a type of scale 0 is rounded to a whole number, half away from zero, before it is sent, as the
    // server rounds it when it stores it.  SqlClient takes a Scale of 0 as not set and takes the value's, so unrounded
    // 12345.00 would be a decimal(5,2) for the numeric(5,0), which it holds, and be refused as out of range.
    [Theory]
    [InlineData("12345.00", "12345")]
    [InlineData("-12345.00", "-12345")]
    [InlineData("1234.5", "1235")]
    [InlineData("-1234.5", "-1235")]
    public async Task WriteDecimalsOfScale0_ValueWithAFraction_IsStoredRounded(string given, string rounded)
    {
        var value = decimal.Parse(given, CultureInfo.InvariantCulture);
        var expected = decimal.Parse(rounded, CultureInfo.InvariantCulture);
        await using var connection = EndToEndSources.App(sqlServer);

        var written = await connection.WriteDecimalsOfScale0(
            new WriteDecimalsOfScale0Args(value, value),
            cancellationToken: TestContext.Current.CancellationToken
        );

        written.ShouldBe(new WriteDecimalsOfScale0Dto(expected, expected));
    }

    // A WHERE compares the decimal(12,0) with the value rounded to scale 0, as the server would convert it: 1.5 finds
    // the row that holds 2.  Unrounded, 1.5 would be a decimal(12,1), which the WHERE compares as it is, and finds
    // nothing.
    [Theory]
    [InlineData("1.5", "2")]
    [InlineData("-1.5", "-2")]
    [InlineData("2.4", "2")]
    [InlineData("-0.4", "0")]
    public async Task FindDecimalOfScale0_ValueWithAFraction_FindsTheRowItRoundsTo(string given, string rounded)
    {
        var value = decimal.Parse(given, CultureInfo.InvariantCulture);
        var expected = decimal.Parse(rounded, CultureInfo.InvariantCulture);
        await using var connection = EndToEndSources.App(sqlServer);

        var found = await connection.FindDecimalOfScale0(
            new FindDecimalOfScale0Args(value),
            cancellationToken: TestContext.Current.CancellationToken
        );

        found.ShouldBe([new FindDecimalOfScale0Dto(expected)]);
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

    // The column takes a NULL of any type, the nvarchar that DBNull would be sent as too, so this proves the write
    // and not the type of the null: MeasureVectorDistance_NullProbe_IsANullDistance proves that.
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

    // A null probe is sent as the null of vector(3), so VECTOR_DISTANCE gives a NULL, where DBNull.Value, which
    // SqlClient sends as an nvarchar(4000), would be refused by the server: it takes a NULL of no type but vector.
    [Fact]
    public async Task MeasureVectorDistance_NullProbe_IsANullDistance()
    {
        sqlServer.SkipBelow(17, "SQL Server before 2025 has no vector.");
        await using var connection = EndToEndSources.App(sqlServer);

        var measured = await connection.MeasureVectorDistance(
            new MeasureVectorDistanceArgs(null, SqlServerAllTypesRows.FullRowId),
            cancellationToken: TestContext.Current.CancellationToken
        );

        measured.ShouldBe(new MeasureVectorDistanceDto(null));
    }

    // The cosine distance from row 1's [1.5, -2.25, 0.5], of length 2.75, to [1, 0, 0] is 1 - 1.5 / 2.75, to the
    // precision of a float, which the server computes in.
    [Fact]
    public async Task MeasureVectorDistance_Probe_IsTheDistance()
    {
        sqlServer.SkipBelow(17, "SQL Server before 2025 has no vector.");
        await using var connection = EndToEndSources.App(sqlServer);

        var measured = await connection.MeasureVectorDistance(
            new MeasureVectorDistanceArgs(new SqlVector<float>(FirstAxis), SqlServerAllTypesRows.FullRowId),
            cancellationToken: TestContext.Current.CancellationToken
        );

        measured.Distance.ShouldNotBeNull().ShouldBe(1 - (1.5 / 2.75), tolerance: 1e-6);
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
