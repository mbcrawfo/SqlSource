using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dapper;
using Shouldly;
using SqlSource.Testing.EndToEnd;
using Xunit;

namespace SqlSource.Tests.Postgres.EndToEnd;

// The type map through Dapper.  The read side: both rows of all_types into AllTypesDto, every column of each.  The
// write side: a row inserted with InsertAllTypesArgs, built from the values of row 1, and read back by RETURNING, for
// every type that Dapper can send.  The insert runs in a transaction that is rolled back.
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
                "Dapper sends a DateTimeOffset by its own DbType, not the handler, as timestamptz, at offset 0 alone.",
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

    [Fact]
    public async Task Query_ListAllTypes_ReadsBothRows()
    {
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var rows = (await connection.QueryAsync<AllTypesDto>(AllTypesQueries.ListAllTypes)).ToList();

        rows.Count.ShouldBe(2);
        AllTypesRows.ShouldBeTheFullRow(rows[0]);
        AllTypesRows.ShouldBeTheNullRow(rows[1]);
    }

    [Fact]
    public async Task Query_InsertAllTypes_ReturnsTheValuesWritten()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        try
        {
            var full = (
                await connection.QueryAsync<AllTypesDto>(AllTypesQueries.ListAllTypes, transaction: transaction)
            ).Single(static row => row.Id == AllTypesRows.FullRowId);

            var row = await connection.QuerySingleAsync<AllTypesDto>(
                AllTypesQueries.InsertAllTypes,
                ArgsFrom(full),
                transaction
            );

            AllTypesRows.ShouldBeTheFullRow(row, InsertedId, nullProperties: WriteSideLeftOut.Keys.ToHashSet());
        }
        finally
        {
            await transaction.RollbackAsync(token);
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

    // Npgsql gives the type of an array column as System.Array, and Dapper looks for a constructor of that exact type,
    // so it cannot construct a positional record with an array.  This is why AllTypesDto is a class.
    [Fact]
    public async Task Query_RecordWithAnArray_DapperCannotConstructIt()
    {
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var error = await Should.ThrowAsync<InvalidOperationException>(() =>
            connection.QueryAsync<Int4Array>("SELECT int4_array_value FROM all_types")
        );

        error.Message.ShouldContain("(System.Array int4_array_value)");
    }

    // The input model with the values of the full row, property by property by name, and the id of the new row.  A
    // parameter of bit(1) has no length, so it is a BitArray where the column is a bool.
    private static InsertAllTypesArgs ArgsFrom(AllTypesDto full)
    {
        var constructor = typeof(InsertAllTypesArgs).GetConstructors().ShouldHaveSingleItem();
        var arguments = constructor
            .GetParameters()
            .Select(parameter =>
                parameter.Name switch
                {
                    nameof(InsertAllTypesArgs.Id) => InsertedId,
                    "Bit1Value" => new BitArray([full.Bit1Value.ShouldNotBeNull()]),
                    _ => typeof(AllTypesDto)
                        .GetProperty(parameter.Name!)
                        .ShouldNotBeNull(parameter.Name)
                        .GetValue(full),
                }
            )
            .ToArray();
        return (InsertAllTypesArgs)constructor.Invoke(arguments);
    }

    private static string[] NamesOf(Type model) =>
        [.. model.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(static property => property.Name)];

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Dapper would make it, and the test proves that it cannot."
    )]
    private sealed record Int4Array(int[]? Int4ArrayValue);
}
