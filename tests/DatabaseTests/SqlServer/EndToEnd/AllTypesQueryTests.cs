using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness.EndToEnd;
using Xunit;
using static SqlSource.DatabaseTests.TestHarness.EndToEnd.ModelReflection;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The type map through Dapper, which runs the SQL of AllTypesDapperQueries, the claim of Queries/AllTypes.sql at the
// output models.  The read side: both rows of all_types into AllTypesDto, every column of each.  The write side: a row
// inserted with InsertAllTypesArgs, built from the values of row 1, and read back by OUTPUT, for every type that Dapper
// sends exactly.  Each insert runs in a transaction that is rolled back.
[Collection(UsesSqlServer.Name)]
public sealed class AllTypesQueryTests(SqlServerContainer sqlServer)
{
    private const int InsertedId = 3;

    // The columns that InsertAllTypes does not write, by the property of AllTypesDto, each with what Dapper, SqlClient
    // or the server did when its parameter was sent.  Their values are proved by the read side.
    internal static IReadOnlyDictionary<string, string> WriteSideLeftOut { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Datetime2Value"] =
                "Dapper sends a DateTime as datetime, which holds 1/300 of a second, so the column loses its digits.",
            ["RowversionValue"] = "The server fills a rowversion, and refuses a value for it.",
        };

    public static TheoryData<string> LeftOutColumns => [.. WriteSideLeftOut.Keys];

    // The error number of the server's refusal, by the left-out column that the server refuses: 273 is "Cannot insert
    // an explicit value into a timestamp column".  The value of any other left-out column is written, and differs.
    private static readonly Dictionary<string, int> Refusals = new(StringComparer.Ordinal)
    {
        ["RowversionValue"] = 273,
    };

    [Fact]
    public async Task QueryAsync_ListAllTypes_ReadsBothRows()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var rows = (await connection.QueryAsync<AllTypesDto>(AllTypesDapperQueries.ListAllTypes)).ToList();

        rows.Count.ShouldBe(2);
        SqlServerAllTypesRows.ShouldBeTheFullRow(rows[0]);
        SqlServerAllTypesRows.ShouldBeTheNullRow(rows[1]);
    }

    [Fact]
    public async Task QuerySingleAsync_InsertAllTypes_ReturnsTheValuesWritten()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        try
        {
            var full = (
                await connection.QueryAsync<AllTypesDto>(AllTypesDapperQueries.ListAllTypes, transaction: transaction)
            ).Single(static row => row.Id == SqlServerAllTypesRows.FullRowId);

            var row = await connection.QuerySingleAsync<AllTypesDto>(
                AllTypesDapperQueries.InsertAllTypes,
                ArgsFrom(full),
                transaction
            );

            // The server fills the rowversion of the new row, which is compared by its length.
            SqlServerAllTypesRows.ShouldBeTheFullRow(
                row,
                InsertedId,
                nullProperties: WriteSideLeftOut
                    .Keys.Where(static property => !Refusals.ContainsKey(property))
                    .ToHashSet()
            );
        }
        finally
        {
            await transaction.RollbackAsync(token);
        }
    }

    // Each column that InsertAllTypes leaves out still fails alone, with its value of row 1 as the parameter, typed as
    // the input model would type it: the server refuses it, or the value it writes is not row 1's.  A type that comes
    // to be written exactly fails here, and goes back into the query.
    [Theory]
    [MemberData(nameof(LeftOutColumns))]
    public async Task ExecuteAsync_LeftOutColumnAlone_FailsToWriteItsValue(string property)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        var full = (await connection.QueryAsync<AllTypesDto>(AllTypesDapperQueries.ListAllTypes)).Single(static row =>
            row.Id == SqlServerAllTypesRows.FullRowId
        );
        var column = typeof(AllTypesDto).GetProperty(property).ShouldNotBeNull(property);
        var type = Nullable.GetUnderlyingType(column.PropertyType) ?? column.PropertyType;
        var value = column.GetValue(full).ShouldNotBeNull(property);
        var arguments = Activator.CreateInstance(typeof(ColumnArgs<>).MakeGenericType(type), InsertedId, value);
        var name = ColumnOf(property);
        var sql = $"INSERT INTO dbo.all_types (id, {name}) VALUES (@id, @value)";

        await using var transaction = await connection.BeginTransactionAsync(token);
        try
        {
            if (Refusals.TryGetValue(property, out var number))
            {
                var error = await Should.ThrowAsync<SqlException>(() =>
                    connection.ExecuteAsync(sql, arguments, transaction)
                );
                error.Number.ShouldBe(number, error.Message);
            }
            else
            {
                (await connection.ExecuteAsync(sql, arguments, transaction)).ShouldBe(1);
                var written = await connection.ExecuteScalarAsync<object>(
                    $"SELECT {name} FROM dbo.all_types WHERE id = @id",
                    new { id = InsertedId },
                    transaction
                );
                written.ShouldNotBeNull(property).ShouldNotBe(value, property);
            }
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

    // The input model with the values of the full row, property by property by name, and the id of the new row.
    private static InsertAllTypesArgs ArgsFrom(AllTypesDto full)
    {
        var constructor = typeof(InsertAllTypesArgs).GetConstructors().ShouldHaveSingleItem();
        var arguments = constructor
            .GetParameters()
            .Select(parameter =>
                parameter.Name == nameof(InsertAllTypesArgs.Id)
                    ? InsertedId
                    : typeof(AllTypesDto).GetProperty(parameter.Name!).ShouldNotBeNull(parameter.Name).GetValue(full)
            )
            .ToArray();
        return (InsertAllTypesArgs)constructor.Invoke(arguments);
    }

    // The parameters of one column, as the input model has them, and the id of the row.
    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "The theory makes it by reflection."
    )]
    private sealed record ColumnArgs<T>(int Id, T Value);
}
