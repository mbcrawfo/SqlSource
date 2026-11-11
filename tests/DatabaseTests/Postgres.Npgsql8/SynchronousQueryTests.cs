using System.Data;
using Npgsql;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Npgsql8;

// The synchronous method of each shape on Npgsql 8, the oldest driver that is supported, whose synchronous reads are
// its own code: the end-to-end project's SynchronousMethodTests prove the methods on Npgsql 10, and this class holds
// that each shape reads its rows, or runs, through Npgsql 8's synchronous calls, by the copies under method-type=sync
// of Queries/Methods.sql.  A test that writes runs in a transaction that is rolled back.
[Collection(UsesApp.Name)]
public sealed class SynchronousQueryTests(AppContainer app)
{
    [Fact]
    public void TouchAccountSync_OneRowInATransaction_Returns1()
    {
        using var source = NpgsqlDataSource.Create(app.ConnectionString);
        using var connection = source.OpenConnection();
        using var transaction = connection.BeginTransaction();

        var count = connection.TouchAccountSync(new TouchAccountSyncArgs(1), transaction);

        count.ShouldBe(1);
        connection.State.ShouldBe(ConnectionState.Open);
        transaction.Rollback();
    }

    [Fact]
    public void TouchAccountQuietlySync_NoRowOnAClosedConnection_IsClosedAgain()
    {
        using var source = NpgsqlDataSource.Create(app.ConnectionString);
        using var connection = source.CreateConnection();

        connection.TouchAccountQuietlySync(new TouchAccountQuietlySyncArgs(99));

        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public void GetAccountInRangeSync_OneRow_ReadsIt()
    {
        using var source = NpgsqlDataSource.Create(app.ConnectionString);
        using var connection = source.CreateConnection();

        var account = connection.GetAccountInRangeSync(new GetAccountInRangeSyncArgs(2, 2));

        account.ShouldBe(new GetAccountInRangeSyncDto(2, "bob"));
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public void FindAccountInRangeSync_OneRow_ReadsIt()
    {
        using var source = NpgsqlDataSource.Create(app.ConnectionString);
        using var connection = source.OpenConnection();

        var account = connection.FindAccountInRangeSync(new FindAccountInRangeSyncArgs(3, 5));

        account.ShouldBe(new FindAccountInRangeSyncDto(3, "carol"));
        connection.State.ShouldBe(ConnectionState.Open);
    }

    [Fact]
    public void ListAccountsWhereSync_AFilter_ReadsItsRows()
    {
        using var source = NpgsqlDataSource.Create(app.ConnectionString);
        using var connection = source.CreateConnection();

        var accounts = connection.ListAccountsWhereSync(new ListAccountsWhereSyncArgs(2), "status <> 'active'");

        accounts.ShouldBe([new ListAccountsWhereSyncDto(2, "bob"), new ListAccountsWhereSyncDto(3, "carol")]);
        connection.State.ShouldBe(ConnectionState.Closed);
    }
}
