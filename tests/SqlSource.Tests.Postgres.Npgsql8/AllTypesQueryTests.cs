using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using NpgsqlTypes;
using Shouldly;
using SqlSource.Testing.EndToEnd;
using Xunit;

namespace SqlSource.Tests.Postgres.Npgsql8;

// The type map on Npgsql 8, the oldest driver that is supported: the models the generator gives under it, both rows of
// all_types read through ListAllTypes' method and through Dapper into AllTypesDto, and every column written through
// InsertEveryType's method and read back, in a transaction that is rolled back.  The end-to-end project proves the same
// table on Npgsql 10.  The data source maps nothing: all_types reads its enum as a string.
public sealed class AllTypesQueryTests(AppContainer app) : IClassFixture<AppContainer>
{
    private const int FullRowCopyId = 3;

    private const int NullRowCopyId = 4;

    // Dapper's settings are the process's, so the snake_case columns are matched from before the first test.
    static AllTypesQueryTests()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    [Fact]
    public void Npgsql_Loaded_IsVersion8() => typeof(NpgsqlConnection).Assembly.GetName().Version!.Major.ShouldBe(8);

    // The types that differ from Npgsql 10's: date and time are a DateTime and a TimeSpan, cidr is an NpgsqlCidr, and a
    // range of dates is a range of DateTime.
    [Fact]
    public void Build_AllTypes_HasNpgsql8sTypes()
    {
        var properties = ModelReflection.TypedPropertiesOf(typeof(AllTypesDto));

        properties.ShouldContain(("DateValue", typeof(DateTime?), true));
        properties.ShouldContain(("TimeValue", typeof(TimeSpan?), true));
        properties.ShouldContain(("CidrValue", typeof(NpgsqlCidr?), true));
        properties.ShouldContain(("DaterangeValue", typeof(NpgsqlRange<DateTime>?), true));
        properties.ShouldContain(("DatemultirangeValue", typeof(NpgsqlRange<DateTime>[]), true));
    }

    [Fact]
    public async Task ListAllTypes_BothRows_ReadsEveryColumn()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(app.ConnectionString);
        await using var connection = source.CreateConnection();

        var rows = await connection.ListAllTypes(cancellationToken: token);

        rows.Length.ShouldBe(2);
        AllTypesRows.ShouldBeTheFullRow(rows[0]);
        AllTypesRows.ShouldBeTheNullRow(rows[1]);
    }

    [Fact]
    public async Task InsertEveryType_FullRowAndNullRow_ReadEveryValueBack()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(app.ConnectionString);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var seeded = await connection.ListAllTypes(transaction, cancellationToken: token);

        var full = await connection.InsertEveryType(
            ArgsFrom(seeded.Single(static row => row.Id == AllTypesRows.FullRowId), FullRowCopyId),
            transaction,
            cancellationToken: token
        );
        var nulls = await connection.InsertEveryType(
            ArgsFrom(seeded.Single(static row => row.Id == AllTypesRows.NullRowId), NullRowCopyId),
            transaction,
            cancellationToken: token
        );
        var read = await connection.ListAllTypes(transaction, cancellationToken: token);
        await transaction.RollbackAsync(token);

        AllTypesRows.ShouldBeTheFullRow(full.ShouldHaveSingleItem(), FullRowCopyId);
        AllTypesRows.ShouldBeTheNullRow(nulls.ShouldHaveSingleItem(), NullRowCopyId);
        read.Length.ShouldBe(4);
        AllTypesRows.ShouldBeTheFullRow(read.Single(static row => row.Id == FullRowCopyId), FullRowCopyId);
        AllTypesRows.ShouldBeTheNullRow(read.Single(static row => row.Id == NullRowCopyId), NullRowCopyId);
    }

    [Fact]
    public async Task QueryAsync_ListAllTypes_ReadsBothRows()
    {
        await using var source = NpgsqlDataSource.Create(app.ConnectionString);
        await using var connection = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var rows = (await connection.QueryAsync<AllTypesDto>(AllTypesSql.ListAllTypes)).ToList();

        rows.Count.ShouldBe(2);
        AllTypesRows.ShouldBeTheFullRow(rows[0]);
        AllTypesRows.ShouldBeTheNullRow(rows[1]);
    }

    // The input model with the values of a row, property by property by name, and the id of the new row.  A parameter
    // of bit(1) has no length, so it is a BitArray where the column is a bool.
    private static InsertEveryTypeParams ArgsFrom(AllTypesDto row, int id)
    {
        var constructor = typeof(InsertEveryTypeParams).GetConstructors().ShouldHaveSingleItem();
        var arguments = constructor
            .GetParameters()
            .Select(parameter =>
                parameter.Name switch
                {
                    nameof(AllTypesDto.Id) => id,
                    nameof(AllTypesDto.Bit1Value) => row.Bit1Value is { } bit ? new BitArray([bit]) : null,
                    _ => typeof(AllTypesDto).GetProperty(parameter.Name!).ShouldNotBeNull(parameter.Name).GetValue(row),
                }
            )
            .ToArray();
        return (InsertEveryTypeParams)constructor.Invoke(arguments);
    }
}
