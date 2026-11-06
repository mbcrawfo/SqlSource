using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using NpgsqlTypes;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness.EndToEnd;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Npgsql8;

// The type map on Npgsql 8, the oldest driver that is supported: the models the generator gives under it, both rows of
// all_types read through ListAllTypes' method and, by the constant of its copy ListAllTypesForDapper, through Dapper
// into AllTypesDto, and every column written through InsertEveryType's method and read back, and the columns that
// InsertAllTypes writes by its method, each in a transaction that is rolled back.  The end-to-end project proves the
// same table on Npgsql 10.  The data source maps nothing: all_types reads its enum as a string.
[Collection(UsesApp.Name)]
public sealed class AllTypesQueryTests(AppContainer app)
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

        rows.Count.ShouldBe(2);
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
            AllTypesRows.ArgsFrom<InsertEveryTypeArgs>(
                seeded.Single(static row => row.Id == AllTypesRows.FullRowId),
                FullRowCopyId
            ),
            transaction,
            cancellationToken: token
        );
        var nulls = await connection.InsertEveryType(
            AllTypesRows.ArgsFrom<InsertEveryTypeArgs>(
                seeded.Single(static row => row.Id == AllTypesRows.NullRowId),
                NullRowCopyId
            ),
            transaction,
            cancellationToken: token
        );
        var read = await connection.ListAllTypes(transaction, cancellationToken: token);
        await transaction.RollbackAsync(token);

        AllTypesRows.ShouldBeTheFullRow(full.ShouldHaveSingleItem(), FullRowCopyId);
        AllTypesRows.ShouldBeTheNullRow(nulls.ShouldHaveSingleItem(), NullRowCopyId);
        read.Count.ShouldBe(4);
        AllTypesRows.ShouldBeTheFullRow(read.Single(static row => row.Id == FullRowCopyId), FullRowCopyId);
        AllTypesRows.ShouldBeTheNullRow(read.Single(static row => row.Id == NullRowCopyId), NullRowCopyId);
    }

    // The method of the query that the end-to-end project's Dapper write runs, whose parameters are not nullable, so
    // that each is bound as a value of its type and never as null.  The columns it does not write are the properties of
    // AllTypesDto that InsertAllTypesArgs lacks, found by reflection, since this project has no list of its own.
    [Fact]
    public async Task InsertAllTypes_FullRow_ReturnsTheValuesWritten()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(app.ConnectionString);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var full = (await connection.ListAllTypes(transaction, cancellationToken: token)).Single(static row =>
            row.Id == AllTypesRows.FullRowId
        );

        var rows = await connection.InsertAllTypes(
            AllTypesRows.ArgsFrom<InsertAllTypesArgs>(full, FullRowCopyId),
            transaction,
            cancellationToken: token
        );
        await transaction.RollbackAsync(token);

        var written = typeof(InsertAllTypesArgs).GetProperties().Select(static property => property.Name);
        var notWritten = typeof(AllTypesDto)
            .GetProperties()
            .Select(static property => property.Name)
            .Except(written, StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        notWritten.ShouldNotBeEmpty();
        AllTypesRows.ShouldBeTheFullRow(rows.ShouldHaveSingleItem(), FullRowCopyId, nullProperties: notWritten);
    }

    [Fact]
    public async Task QueryAsync_ListAllTypes_ReadsBothRows()
    {
        await using var source = NpgsqlDataSource.Create(app.ConnectionString);
        await using var connection = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var rows = (await connection.QueryAsync<AllTypesDto>(AllTypesQueries.ListAllTypesForDapper)).ToList();

        rows.Count.ShouldBe(2);
        AllTypesRows.ShouldBeTheFullRow(rows[0]);
        AllTypesRows.ShouldBeTheNullRow(rows[1]);
    }

    // A sequence of each kind for int4_array_value: an array, a List<int>, an ImmutableArray<int> (an IList<int>) and a
    // HashSet<int> and a lazy sequence, which are copied once.
    private static IEnumerable<int> SequenceOf(string kind) =>
        kind switch
        {
            "Array" => (int[])[1, 2, 3],
            "List" => (List<int>)[1, 2, 3],
            "ImmutableArray" => (ImmutableArray<int>)[1, 2, 3],
            "HashSet" => (HashSet<int>)[3, 1, 2],
            "Lazy" => Enumerable.Range(1, 3).Select(static x => x),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    [Theory]
    [InlineData("Array", true)]
    [InlineData("List", true)]
    [InlineData("ImmutableArray", true)]
    [InlineData("HashSet", false)]
    [InlineData("Lazy", true)]
    public async Task Int4Array_EachKindOfSequence_RoundTrips(string kind, bool ordered)
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(app.ConnectionString);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);

        var row = await InsertWith(
            connection,
            transaction,
            args => args with { Int4ArrayValue = SequenceOf(kind) },
            token
        );
        await transaction.RollbackAsync(token);

        // The set's order is the set's, so the elements are compared without it.
        row.Int4ArrayValue.ShouldNotBeNull().OrderBy(static x => x).ShouldBe([1, 2, 3]);
        if (ordered)
        {
            row.Int4ArrayValue.ShouldBe([1, 2, 3]);
        }
    }

    [Fact]
    public async Task Int4Array_LazySequence_IsEnumeratedOnce()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(app.ConnectionString);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var sequence = new CountingSequence([4, 5, 6]);

        var row = await InsertWith(connection, transaction, args => args with { Int4ArrayValue = sequence }, token);
        await transaction.RollbackAsync(token);

        sequence.Enumerations.ShouldBe(1);
        row.Int4ArrayValue.ShouldBe([4, 5, 6]);
    }

    [Fact]
    public async Task Int4Array_Null_IsNull()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(app.ConnectionString);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);

        var row = await InsertWith(connection, transaction, static args => args with { Int4ArrayValue = null }, token);
        await transaction.RollbackAsync(token);

        row.Int4ArrayValue.ShouldBeNull();
    }

    [Fact]
    public async Task TextArray_HashSet_RoundTrips()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(app.ConnectionString);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);

        var row = await InsertWith(
            connection,
            transaction,
            static args => args with { TextArrayValue = (HashSet<string>)["b c", "a", "Zoë"] },
            token
        );
        await transaction.RollbackAsync(token);

        row.TextArrayValue.ShouldNotBeNull()
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ShouldBe(["Zoë", "a", "b c"]);
    }

    // InsertEveryType with the values of row 1 and one change, on the open transaction.
    private static async Task<AllTypesDto> InsertWith(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Func<InsertEveryTypeArgs, InsertEveryTypeArgs> change,
        System.Threading.CancellationToken token
    )
    {
        var full = (await connection.ListAllTypes(transaction, cancellationToken: token)).Single(static row =>
            row.Id == AllTypesRows.FullRowId
        );
        var rows = await connection.InsertEveryType(
            change(AllTypesRows.ArgsFrom<InsertEveryTypeArgs>(full, FullRowCopyId)),
            transaction,
            cancellationToken: token
        );
        return rows.ShouldHaveSingleItem();
    }

    private sealed class CountingSequence(int[] items) : IEnumerable<int>
    {
        public int Enumerations { get; private set; }

        public IEnumerator<int> GetEnumerator()
        {
            Enumerations++;
            return ((IEnumerable<int>)items).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
