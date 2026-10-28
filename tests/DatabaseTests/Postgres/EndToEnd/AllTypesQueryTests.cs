using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using Shouldly;
using SqlSource.Testing.EndToEnd;
using Xunit;
using static SqlSource.Testing.EndToEnd.ModelReflection;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The type map through the generated methods and through Dapper.  The read side: both rows of all_types into
// AllTypesDto, every column of each, by AllTypesQueries' method and by AllTypesSql's constant through Dapper.  The
// write side of InsertAllTypes: a row inserted with InsertAllTypesArgs, built from the values of row 1, and read back
// by RETURNING, for every type that Dapper can send, by its method and by its constant through Dapper.  Each insert
// runs in a transaction that is rolled back.  EveryTypeTests writes every column through InsertEveryType's method.
[Collection(UsesPostgres.Name)]
public sealed class AllTypesQueryTests(PostgresContainer postgres)
{
    private const int InsertedId = 3;

    // The columns that InsertAllTypes does not write, by the property of AllTypesDto, each with what Dapper or Npgsql
    // said when its parameter was sent.  Their values are proved by the read side.
    internal static IReadOnlyDictionary<string, string> WriteSideLeftOut { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TimetzValue"] =
                "Dapper and Npgsql send a DateTimeOffset as timestamptz, which takes offset 0 alone, never as timetz.",
            ["JsonValue"] = "Dapper sends a string as text, which the server refuses for a column of json.",
            ["JsonbValue"] = "Dapper sends a string as text, which the server refuses for a column of jsonb.",
            ["JsonpathValue"] = "Dapper sends a string as text, which the server refuses for a column of jsonpath.",
            ["XmlValue"] = "Dapper sends a string as text, which the server refuses for a column of xml.",
            ["InetValue"] = "Dapper has no DbType for an IPAddress and refuses it as a parameter.",
            ["CidrValue"] = "Dapper has no DbType for an IPNetwork and refuses it as a parameter.",
            ["MacaddrValue"] = "Dapper has no DbType for a PhysicalAddress and refuses it as a parameter.",
            ["Macaddr8Value"] = "Dapper has no DbType for a PhysicalAddress and refuses it as a parameter.",
            ["PointValue"] = "Dapper has no DbType for an NpgsqlPoint and refuses it as a parameter.",
            ["LineValue"] = "Dapper has no DbType for an NpgsqlLine and refuses it as a parameter.",
            ["LsegValue"] = "Dapper has no DbType for an NpgsqlLSeg and refuses it as a parameter.",
            ["BoxValue"] = "Dapper has no DbType for an NpgsqlBox and refuses it as a parameter.",
            ["CircleValue"] = "Dapper has no DbType for an NpgsqlCircle and refuses it as a parameter.",
            ["TsqueryValue"] = "Dapper has no DbType for an NpgsqlTsQuery and refuses it as a parameter.",
            ["OidValue"] = "Dapper sends a uint as DbType.UInt32, which Npgsql refuses.",
            ["XidValue"] = "Dapper sends a uint as DbType.UInt32, which Npgsql refuses.",
            ["CidValue"] = "Dapper sends a uint as DbType.UInt32, which Npgsql refuses.",
            ["RegtypeValue"] = "Dapper sends a uint as DbType.UInt32, which Npgsql refuses.",
            ["RegconfigValue"] = "Dapper sends a uint as DbType.UInt32, which Npgsql refuses.",
            ["Xid8Value"] = "Dapper sends a ulong as DbType.UInt64, which Npgsql refuses.",
            ["TidValue"] = "Dapper has no DbType for an NpgsqlTid and refuses it as a parameter.",
            ["PgLsnValue"] = "Dapper has no DbType for an NpgsqlLogSequenceNumber and refuses it as a parameter.",
            ["Int2vectorValue"] =
                "Npgsql sends a short[] as smallint[], which the server refuses for a column of int2vector.",
            ["OidvectorValue"] =
                "Npgsql writes a uint[] only for a parameter that names its NpgsqlDbType or DataTypeName.",
            ["RefcursorValue"] = "Dapper sends a string as text, which the server refuses for a column of refcursor.",
            ["LtreeValue"] = "Dapper sends a string as text, which the server refuses for a column of ltree.",
            ["LqueryValue"] = "Dapper sends a string as text, which the server refuses for a column of lquery.",
            ["LtxtqueryValue"] = "Dapper sends a string as text, which the server refuses for a column of ltxtquery.",
            ["Int4rangeValue"] = "Dapper has no DbType for an NpgsqlRange<int> and refuses it as a parameter.",
            ["Int8rangeValue"] = "Dapper has no DbType for an NpgsqlRange<long> and refuses it as a parameter.",
            ["NumrangeValue"] = "Dapper has no DbType for an NpgsqlRange<decimal> and refuses it as a parameter.",
            ["TsrangeValue"] = "Dapper has no DbType for an NpgsqlRange<DateTime> and refuses it as a parameter.",
            ["TstzrangeValue"] = "Dapper has no DbType for an NpgsqlRange<DateTime> and refuses it as a parameter.",
            ["DaterangeValue"] = "Dapper has no DbType for an NpgsqlRange<DateOnly> and refuses it as a parameter.",
            ["StatusArrayValue"] =
                "Dapper sends a string[] as text[], which the server refuses for a column of account_status[].",
            ["StatusValue"] = "Dapper sends a string as text, which the server refuses for a column of account_status.",
        };

    public static TheoryData<string> LeftOutColumns => [.. WriteSideLeftOut.Keys];

    // What Dapper and Npgsql throw before the server sees a left-out column, on the default image and on the floor
    // alike: Dapper refuses a type it has no DbType for, and Npgsql a DbType it does not support
    // (NotSupportedException), a timestamptz at another offset (ArgumentException) and a uint[] it has no type for
    // (InvalidCastException).  Any other error, even a subclass of one of these, is not a failure to bind.
    private static readonly Type[] BindingErrors =
    [
        typeof(NotSupportedException),
        typeof(ArgumentException),
        typeof(InvalidCastException),
    ];

    [Fact]
    public async Task ListAllTypes_BothRows_ReadsEveryColumn()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var rows = await connection.ListAllTypes(cancellationToken: token);

        rows.Count.ShouldBe(2);
        AllTypesRows.ShouldBeTheFullRow(rows[0]);
        AllTypesRows.ShouldBeTheNullRow(rows[1]);
        (rows[0] with { Id = 9 }).Int4ArrayValue.ShouldBeSameAs(rows[0].Int4ArrayValue);
        (rows[0] with { }).ShouldBe(rows[0]);
    }

    // The method of the query that Dapper's write runs, with the same values: it writes what Dapper can send, and the
    // columns of WriteSideLeftOut are null.
    [Fact]
    public async Task InsertAllTypes_FullRow_ReturnsTheValuesWritten()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);

        var full = (await connection.ListAllTypes(transaction, cancellationToken: token)).Single(static row =>
            row.Id == AllTypesRows.FullRowId
        );
        var rows = await connection.InsertAllTypes(
            AllTypesRows.ArgsFrom<InsertAllTypesArgs>(full, InsertedId),
            transaction,
            cancellationToken: token
        );

        AllTypesRows.ShouldBeTheFullRow(
            rows.ShouldHaveSingleItem(),
            InsertedId,
            nullProperties: WriteSideLeftOut.Keys.ToHashSet()
        );
        await transaction.RollbackAsync(token);
    }

    [Fact]
    public async Task QueryAsync_ListAllTypes_ReadsBothRows()
    {
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var rows = (await connection.QueryAsync<AllTypesDto>(AllTypesSql.ListAllTypes)).ToList();

        // Npgsql gives the type of an array column as System.Array, and Dapper looks for a constructor of that exact
        // type, so the positional constructor of the record never matches the row.  Dapper reads it through the
        // constructor without parameters that the generator gives a record with an array, and sets each property.
        rows.Count.ShouldBe(2);
        AllTypesRows.ShouldBeTheFullRow(rows[0]);
        AllTypesRows.ShouldBeTheNullRow(rows[1]);
        (rows[0] with { Id = 9 }).Int4ArrayValue.ShouldBeSameAs(rows[0].Int4ArrayValue);
        (rows[0] with { }).ShouldBe(rows[0]);
    }

    [Fact]
    public async Task QuerySingleAsync_InsertAllTypes_ReturnsTheValuesWritten()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        try
        {
            var full = (
                await connection.QueryAsync<AllTypesDto>(AllTypesSql.ListAllTypes, transaction: transaction)
            ).Single(static row => row.Id == AllTypesRows.FullRowId);

            var row = await connection.QuerySingleAsync<AllTypesDto>(
                AllTypesSql.InsertAllTypes,
                AllTypesRows.ArgsFrom<InsertAllTypesArgs>(full, InsertedId),
                transaction
            );

            AllTypesRows.ShouldBeTheFullRow(row, InsertedId, nullProperties: WriteSideLeftOut.Keys.ToHashSet());
        }
        finally
        {
            // A parameter that Npgsql fails to write breaks the connection and disposes the transaction, whose
            // rollback, and even its Connection, would then throw over that error.
            if (connection.State == ConnectionState.Open)
            {
                await transaction.RollbackAsync(token);
            }
        }
    }

    // Each column that InsertAllTypes leaves out still fails alone, with its value of row 1 as the parameter, typed as
    // the input model would type it: a type that comes to bind fails here, and goes back into the query.
    [Theory]
    [MemberData(nameof(LeftOutColumns))]
    public async Task ExecuteAsync_LeftOutColumnAlone_FailsToBind(string property)
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        var full = (await connection.QueryAsync<AllTypesDto>(AllTypesSql.ListAllTypes)).Single(static row =>
            row.Id == AllTypesRows.FullRowId
        );
        var column = typeof(AllTypesDto).GetProperty(property).ShouldNotBeNull(property);
        var type = Nullable.GetUnderlyingType(column.PropertyType) ?? column.PropertyType;
        var arguments = Activator.CreateInstance(
            typeof(ColumnArgs<>).MakeGenericType(type),
            InsertedId,
            column.GetValue(full).ShouldNotBeNull(property)
        );
        var sql = $"INSERT INTO all_types (id, {ColumnOf(property)}) VALUES (@id, @value)";

        await using var transaction = await connection.BeginTransactionAsync(token);
        try
        {
            var error = await Should.ThrowAsync<Exception>(() => connection.ExecuteAsync(sql, arguments, transaction));

            // The server's refusal is a mismatch of types, and not an error of the statement.
            if (error is PostgresException refusal)
            {
                refusal.SqlState.ShouldBe(PostgresErrorCodes.DatatypeMismatch, refusal.MessageText);
            }
            else
            {
                error.GetType().ShouldBeOneOf(BindingErrors, error.ToString());
            }
        }
        finally
        {
            if (connection.State == ConnectionState.Open)
            {
                await transaction.RollbackAsync(token);
            }
        }
    }

    [Fact]
    public void InsertAllTypes_WriteSide_LeavesOutOnlyTheListedTypes()
    {
        var written = NamesOf(typeof(InsertAllTypesArgs));
        var columns = NamesOf(typeof(AllTypesDto));

        written.Intersect(WriteSideLeftOut.Keys).ShouldBeEmpty();
        written
            .Concat(WriteSideLeftOut.Keys)
            .Order(StringComparer.Ordinal)
            .ShouldBe(columns.Order(StringComparer.Ordinal));
    }

    // The parameters of one column, as the input model has them, and the id of the row.
    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The theory makes it by reflection."
    )]
    private sealed record ColumnArgs<T>(int Id, T Value);
}
