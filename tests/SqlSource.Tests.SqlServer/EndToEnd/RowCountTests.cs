using System;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// The counts that rowcount returns on SQL Server, through MethodQueries' methods on the seed of "app": the rows of an
// OUTPUT counted, -1 for a SELECT and under SET NOCOUNT ON, which outlives a command of no parameters on its
// connection, and the first result set of a batch of two statements.  A write runs in a transaction that is rolled
// back.
[Collection(UsesSqlServer.Name)]
public sealed class RowCountTests(SqlServerContainer sqlServer)
{
    [Fact]
    public async Task AddTwoNotes_InsertWithOutput_CountsItsRows()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        var token = TestContext.Current.CancellationToken;

        await EndToEndSources.RolledBackAsync(
            connection,
            async transaction =>
            {
                var count = await connection.AddTwoNotes(
                    new AddTwoNotesArgs(
                        3,
                        "call",
                        "Asked to reopen.",
                        new DateTime(2024, 10, 1, 9, 0, 0, DateTimeKind.Unspecified)
                    ),
                    transaction: transaction,
                    cancellationToken: token
                );

                count.ShouldBe(2);
                var bodies = await connection.QueryAsync<string>(
                    "SELECT body FROM dbo.notes WHERE account_id = 3",
                    transaction: transaction
                );
                bodies.ShouldBe(["Asked to reopen.", "Asked to reopen."]);
            },
            token
        );
    }

    [Fact]
    public async Task CountReadAccounts_Select_IsMinusOne()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var count = await connection.CountReadAccounts(cancellationToken: TestContext.Current.CancellationToken);

        count.ShouldBe(-1);
    }

    // SET NOCOUNT ON in a query with no parameters, which SqlClient sends as a batch, is the session's: the count of
    // every later command on the open connection is -1 too.  The connection string names a pool of this test alone, so
    // that the second connection is given the session of the first, as its @@SPID shows, which SqlClient resets with
    // sp_reset_connection when it hands it out again: that turns NOCOUNT off, and the count is back.
    [Fact]
    public async Task TouchAccountsQuietly_NoCountInABatch_IsMinusOneUntilThePoolResetsTheSession()
    {
        var token = TestContext.Current.CancellationToken;
        var pool = new SqlConnectionStringBuilder(sqlServer.ConnectionStringOf(SqlServerContainer.AppDatabase))
        {
            ApplicationName = nameof(TouchAccountsQuietly_NoCountInABatch_IsMinusOneUntilThePoolResetsTheSession),
        }.ConnectionString;
        short session;

        await using (var connection = new SqlConnection(pool))
        {
            await EndToEndSources.RolledBackAsync(
                connection,
                async transaction =>
                {
                    var quiet = await connection.TouchAccountsQuietly(
                        transaction: transaction,
                        cancellationToken: token
                    );
                    var after = await connection.TouchAccount(
                        new TouchAccountArgs(1),
                        transaction: transaction,
                        cancellationToken: token
                    );

                    quiet.ShouldBe(-1);
                    after.ShouldBe(-1);
                },
                token
            );
            session = await connection.ExecuteScalarAsync<short>("SELECT @@SPID");
        }

        await using var reset = new SqlConnection(pool);
        await EndToEndSources.RolledBackAsync(
            reset,
            async transaction =>
            {
                var count = await reset.TouchAccount(
                    new TouchAccountArgs(1),
                    transaction: transaction,
                    cancellationToken: token
                );

                (await reset.ExecuteScalarAsync<short>("SELECT @@SPID", transaction: transaction)).ShouldBe(session);
                count.ShouldBe(1);
            },
            token
        );
    }

    // A query with parameters runs in sp_executesql, and a SET inside it ends with it: the count of its own command is
    // -1, and that of the next command on the open connection is not.
    [Fact]
    public async Task TouchAccountQuietly_NoCountWithAParameter_EndsWithItsCommand()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        var token = TestContext.Current.CancellationToken;

        await EndToEndSources.RolledBackAsync(
            connection,
            async transaction =>
            {
                var quiet = await connection.TouchAccountQuietly(
                    new TouchAccountQuietlyArgs(1),
                    transaction: transaction,
                    cancellationToken: token
                );
                var after = await connection.TouchAccount(
                    new TouchAccountArgs(1),
                    transaction: transaction,
                    cancellationToken: token
                );

                quiet.ShouldBe(-1);
                after.ShouldBe(1);
            },
            token
        );
    }

    [Fact]
    public async Task TouchAndCountAccounts_UpdateThenSelect_CountsTheUpdate()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        var token = TestContext.Current.CancellationToken;

        await EndToEndSources.RolledBackAsync(
            connection,
            async transaction =>
            {
                var count = await connection.TouchAndCountAccounts(
                    new TouchAndCountAccountsArgs(2, 3),
                    transaction: transaction,
                    cancellationToken: token
                );

                count.ShouldBe(2);
            },
            token
        );
    }

    [Fact]
    public async Task TouchAndGetAccount_UpdateThenSelect_ReadsTheFirstResultSet()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        var token = TestContext.Current.CancellationToken;

        await EndToEndSources.RolledBackAsync(
            connection,
            async transaction =>
            {
                var account = await connection.TouchAndGetAccount(
                    new TouchAndGetAccountArgs(2, 3),
                    transaction: transaction,
                    cancellationToken: token
                );

                account.ShouldBe(new TouchAndGetAccountDto(3, "carol"));
            },
            token
        );
    }

    [Fact]
    public async Task MergeAccountStatuses_MergeWithOutput_CountsItsRows()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        var token = TestContext.Current.CancellationToken;

        await EndToEndSources.RolledBackAsync(
            connection,
            async transaction =>
            {
                var count = await connection.MergeAccountStatuses(
                    new MergeAccountStatusesArgs(2, "suspended"),
                    transaction: transaction,
                    cancellationToken: token
                );

                count.ShouldBe(2);
                var statuses = await connection.QueryAsync<string>(
                    "SELECT status FROM dbo.accounts ORDER BY id",
                    transaction: transaction
                );
                statuses.ShouldBe(["suspended", "suspended", "suspended"]);
            },
            token
        );
    }
}
