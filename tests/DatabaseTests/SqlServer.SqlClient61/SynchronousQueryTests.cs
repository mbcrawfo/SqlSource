using System.Data;
using Microsoft.Data.SqlClient;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.SqlClient61;

// The synchronous method of each shape on SqlClient 6.1, the oldest driver that is supported, whose synchronous reads
// are its own code: the end-to-end project's SynchronousMethodTests prove the methods on SqlClient 7, and this class
// holds that each shape reads its rows, or runs, through SqlClient 6.1's synchronous calls, by the copies under
// method-type=sync of Queries/Methods.sql.  A test that writes runs in a transaction that is rolled back, or touches no
// row.
[Collection(UsesApp.Name)]
public sealed class SynchronousQueryTests(AppContainer app)
{
    [Fact]
    public void TouchAccountSync_OneRowInATransaction_Returns1()
    {
        using var connection = new SqlConnection(app.ConnectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var count = connection.TouchAccountSync(new TouchAccountSyncArgs(1), transaction);

        count.ShouldBe(1);
        connection.State.ShouldBe(ConnectionState.Open);
        transaction.Rollback();
    }

    // No account has the id, so the update writes nothing, with no transaction to roll back.
    [Fact]
    public void SetAccountStatusSync_NoRowOnAClosedConnection_IsClosedAgain()
    {
        using var connection = new SqlConnection(app.ConnectionString);

        connection.SetAccountStatusSync(new SetAccountStatusSyncArgs("suspended", 99));

        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public void GetAccountInRangeSync_OneRow_ReadsIt()
    {
        using var connection = new SqlConnection(app.ConnectionString);

        var account = connection.GetAccountInRangeSync(new GetAccountInRangeSyncArgs(2, 2));

        account.ShouldBe(new GetAccountInRangeSyncDto(2, "bob"));
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public void FindAccountInRangeSync_OneRow_ReadsIt()
    {
        using var connection = new SqlConnection(app.ConnectionString);
        connection.Open();

        var account = connection.FindAccountInRangeSync(new FindAccountInRangeSyncArgs(3, 4));

        account.ShouldBe(new FindAccountInRangeSyncDto(3, "carol"));
        connection.State.ShouldBe(ConnectionState.Open);
    }

    [Fact]
    public void ListAccountsWhereSync_TheDefault_ReadsItsRows()
    {
        using var connection = new SqlConnection(app.ConnectionString);

        var accounts = connection.ListAccountsWhereSync(new ListAccountsWhereSyncArgs(2), "WHERE id <= @lastId");

        accounts.ShouldBe([new ListAccountsWhereSyncDto(1, "alice"), new ListAccountsWhereSyncDto(2, "bob")]);
        connection.State.ShouldBe(ConnectionState.Closed);
    }
}
