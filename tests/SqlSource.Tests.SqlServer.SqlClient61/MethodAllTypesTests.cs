using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlTypes;
using Shouldly;
using SqlSource.Testing.EndToEnd;
using Xunit;

namespace SqlSource.Tests.SqlServer.SqlClient61;

// Every type of the type map written through a parameter of a generated method and read back through a column of one
// on SqlClient 6.1, by the methods of AllTypesMethodQueries and NewTypesMethodQueries, as the end-to-end project proves
// them on SqlClient 7: the seed's rows written again as the row InsertedId, each in a transaction that is rolled back,
// a rowversion in a WHERE, a C# enum over byte, json and vector, and the null of a vector that its column says cannot
// be null.  The container is SQL Server 2025, which has json and vector.
[Collection(UsesApp.Name)]
public sealed class MethodAllTypesTests(AppContainer app)
{
    private const int InsertedId = 3;

    [Fact]
    public async Task ListAllTypes_TheSeed_ReadsBothRows()
    {
        await using var connection = new SqlConnection(app.ConnectionString);

        var rows = await connection.ListAllTypes(cancellationToken: TestContext.Current.CancellationToken);

        rows.Length.ShouldBe(2);
        SqlServerAllTypesRows.ShouldBeTheFullRow(rows[0]);
        SqlServerAllTypesRows.ShouldBeTheNullRow(rows[1]);
    }

    [Fact]
    public async Task WriteAllTypes_TheValuesOfRow1_WritesThemWhole() =>
        await RolledBackAsync(
            async (connection, transaction) =>
            {
                var token = TestContext.Current.CancellationToken;
                var full = (await connection.ListAllTypes(transaction: transaction, cancellationToken: token)).Single(
                    static row => row.Id == SqlServerAllTypesRows.FullRowId
                );

                var written = await connection.WriteAllTypes(
                    SqlServerAllTypesRows.ArgsOf<WriteAllTypesParams>(InsertedId, full),
                    transaction: transaction,
                    cancellationToken: token
                );

                SqlServerAllTypesRows.ShouldBeTheFullRow(written, InsertedId);
                var rows = await connection.ListAllTypes(transaction: transaction, cancellationToken: token);
                SqlServerAllTypesRows.ShouldBeTheFullRow(rows.Single(static row => row.Id == InsertedId), InsertedId);
            }
        );

    [Fact]
    public async Task WriteAllTypes_EveryColumnNull_WritesTheNullRow() =>
        await RolledBackAsync(
            async (connection, transaction) =>
            {
                var written = await connection.WriteAllTypes(
                    SqlServerAllTypesRows.ArgsOf<WriteAllTypesParams>(InsertedId, row: null),
                    transaction: transaction,
                    cancellationToken: TestContext.Current.CancellationToken
                );

                SqlServerAllTypesRows.ShouldBeTheNullRow(written, InsertedId);
            }
        );

    [Fact]
    public async Task FindByRowversion_TheRowversionOfRow1_FindsRow1()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(app.ConnectionString);
        var full = (await connection.ListAllTypes(cancellationToken: token))[0];

        var found = await connection.FindByRowversion(
            new FindByRowversionParams(full.RowversionValue.ShouldNotBeNull()),
            cancellationToken: token
        );

        found.ShouldBe([new FindByRowversionDto(SqlServerAllTypesRows.FullRowId)]);
    }

    [Fact]
    public async Task ListTinyintLevels_High_ReadsItAsALevel()
    {
        await using var connection = new SqlConnection(app.ConnectionString);

        var levels = await connection.ListTinyintLevels(
            new ListTinyintLevelsParams(Level.High),
            cancellationToken: TestContext.Current.CancellationToken
        );

        levels.ShouldBe([new ListTinyintLevelsDto(Level.High)]);
    }

    [Fact]
    public async Task ListNewTypes_TheSeed_ReadsBothRows()
    {
        await using var connection = new SqlConnection(app.ConnectionString);

        var rows = await connection.ListNewTypes(cancellationToken: TestContext.Current.CancellationToken);

        rows.Length.ShouldBe(2);
        SqlServerAllTypesRows.ShouldBeTheFullNewTypesRow(rows[0]);
        SqlServerAllTypesRows.ShouldBeTheNullNewTypesRow(rows[1]);
    }

    [Fact]
    public async Task WriteNewTypes_TheValuesOfRow1_WritesThemWhole() =>
        await RolledBackAsync(
            async (connection, transaction) =>
            {
                var written = await connection.WriteNewTypes(
                    new WriteNewTypesParams(
                        InsertedId,
                        /*lang=json,strict*/
                        """{"name":"Zoë","tags":["a","b"]}""",
                        new SqlVector<float>(new[] { 1.5f, -2.25f, 0.5f })
                    ),
                    transaction: transaction,
                    cancellationToken: TestContext.Current.CancellationToken
                );

                SqlServerAllTypesRows.ShouldBeTheFullNewTypesRow(written, InsertedId);
            }
        );

    [Fact]
    public async Task WriteNewTypes_BothColumnsNull_WritesTheNullRow() =>
        await RolledBackAsync(
            async (connection, transaction) =>
            {
                var written = await connection.WriteNewTypes(
                    new WriteNewTypesParams(InsertedId, null, null),
                    transaction: transaction,
                    cancellationToken: TestContext.Current.CancellationToken
                );

                SqlServerAllTypesRows.ShouldBeTheNullNewTypesRow(written, InsertedId);
            }
        );

    [Fact]
    public async Task GetNewType_RowWithAVector_ReadsIt()
    {
        await using var connection = new SqlConnection(app.ConnectionString);

        var row = await connection.GetNewType(
            new GetNewTypeParams(SqlServerAllTypesRows.FullRowId),
            cancellationToken: TestContext.Current.CancellationToken
        );

        row.Id.ShouldBe(SqlServerAllTypesRows.FullRowId);
        row.V.Memory.ToArray().ShouldBe([1.5f, -2.25f, 0.5f]);
    }

    [Fact]
    public async Task GetNewType_NullInANotNullVector_ThrowsInvalidCastException()
    {
        await using var connection = new SqlConnection(app.ConnectionString);

        var thrown = await Should.ThrowAsync<InvalidCastException>(() =>
            connection.GetNewType(
                new GetNewTypeParams(SqlServerAllTypesRows.NullRowId),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        thrown.Message.ShouldBe("Column 'v' is null.");
    }

    // Runs the write in a transaction that is rolled back when it is disposed, and that the write gives to every call,
    // since SqlClient refuses a command on a connection whose transaction it is not given.
    private async Task RolledBackAsync(Func<SqlConnection, DbTransaction, Task> write)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(app.ConnectionString);
        await connection.OpenAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await write(connection, transaction);
    }
}
