using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// The result shapes of Queries/Methods.sql, and its token, run through MethodQueries' methods on the seed of "app": a
// query with no rows is rowcount by default and runs as none, one and one-optional read their row and refuse the rows
// their shape does not allow, and a token is given the text of its default or another fragment.  A write runs in a
// transaction that is rolled back.
[Collection(UsesSqlServer.Name)]
public sealed class MethodShapeTests(SqlServerContainer sqlServer)
{
    [Fact]
    public async Task TouchAccount_NoRows_IsRowCountByDefault()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        var token = TestContext.Current.CancellationToken;

        await EndToEndSources.RolledBackAsync(
            connection,
            async transaction =>
            {
                var count = await connection.TouchAccount(
                    new TouchAccountArgs(1),
                    transaction: transaction,
                    cancellationToken: token
                );

                count.ShouldBe(1);
            },
            token
        );
    }

    [Fact]
    public async Task SetAccountStatus_None_RunsTheUpdate()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        var token = TestContext.Current.CancellationToken;

        await EndToEndSources.RolledBackAsync(
            connection,
            async transaction =>
            {
                await connection.SetAccountStatus(
                    new SetAccountStatusArgs("active", 2),
                    transaction: transaction,
                    cancellationToken: token
                );

                typeof(MethodQueriesExtensions)
                    .GetMethod(nameof(MethodQueriesExtensions.SetAccountStatus))!
                    .ReturnType.ShouldBe(typeof(Task));
                var status = await connection.ExecuteScalarAsync<string>(
                    "SELECT status FROM dbo.accounts WHERE id = 2",
                    transaction: transaction
                );
                status.ShouldBe("active");
            },
            token
        );
    }

    [Fact]
    public async Task GetAccountInRange_OneRow_ReadsIt()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var account = await connection.GetAccountInRange(
            new GetAccountInRangeArgs(2, 2),
            cancellationToken: TestContext.Current.CancellationToken
        );

        account.ShouldBe(new GetAccountInRangeDto(2, "bob"));
    }

    [Fact]
    public async Task GetAccountInRange_NoRow_Throws()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var thrown = await Should.ThrowAsync<UnexpectedRowCountException>(() =>
            connection.GetAccountInRange(
                new GetAccountInRangeArgs(4, 5),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        thrown.QueryName.ShouldBe("GetAccountInRange");
        thrown.Message.ShouldBe("The query 'GetAccountInRange' returned no rows.");
    }

    [Fact]
    public async Task GetAccountInRange_TwoRows_Throws()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var thrown = await Should.ThrowAsync<UnexpectedRowCountException>(() =>
            connection.GetAccountInRange(
                new GetAccountInRangeArgs(1, 2),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        thrown.QueryName.ShouldBe("GetAccountInRange");
        thrown.Message.ShouldBe("The query 'GetAccountInRange' returned more than one row.");
    }

    [Fact]
    public async Task FindAccountInRange_OneRow_ReadsIt()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var account = await connection.FindAccountInRange(
            new FindAccountInRangeArgs(3, 4),
            cancellationToken: TestContext.Current.CancellationToken
        );

        account.ShouldBe(new FindAccountInRangeDto(3, "carol"));
    }

    [Fact]
    public async Task FindAccountInRange_NoRow_ReturnsNull()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var account = await connection.FindAccountInRange(
            new FindAccountInRangeArgs(4, 5),
            cancellationToken: TestContext.Current.CancellationToken
        );

        account.ShouldBeNull();
    }

    [Fact]
    public async Task FindAccountInRange_TwoRows_Throws()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var thrown = await Should.ThrowAsync<UnexpectedRowCountException>(() =>
            connection.FindAccountInRange(
                new FindAccountInRangeArgs(2, 3),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        thrown.QueryName.ShouldBe("FindAccountInRange");
        thrown.Message.ShouldBe("The query 'FindAccountInRange' returned more than one row.");
    }

    [Fact]
    public async Task ListAccountsWhere_TheDefault_UsesTheParameter()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListAccountsWhere(
            new ListAccountsWhereArgs(2),
            "WHERE id <= @lastId",
            cancellationToken: TestContext.Current.CancellationToken
        );

        accounts.ShouldBe([new ListAccountsWhereDto(1, "alice"), new ListAccountsWhereDto(2, "bob")]);
    }

    // The declared @lastId is sent all the same, and SQL Server accepts a parameter that the text does not use.
    [Fact]
    public async Task ListAccountsWhere_AnotherFragment_LeavesTheParameterUnused()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListAccountsWhere(
            new ListAccountsWhereArgs(1),
            "WHERE status = 'suspended'",
            cancellationToken: TestContext.Current.CancellationToken
        );

        accounts.ShouldBe([new ListAccountsWhereDto(3, "carol")]);
    }

    // The error after the rows is lost by a method that reads them into an array, as by the streaming StreamThenRaise:
    // the rows are returned and nothing is thrown, though the batch ran as far as the THROW, which its mark shows.  A
    // fix that reports the error flips this test.
    [Fact]
    public async Task ListThenRaise_ErrorAfterTheRows_ReturnsTheRowsAndIsNotThrown()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        await SessionMark.ClearAsync(connection, token);

        var accounts = await connection.ListThenRaise(cancellationToken: token);

        accounts.ShouldBe([
            new CollectedAccount(1, "alice"),
            new CollectedAccount(2, "bob"),
            new CollectedAccount(3, "carol"),
        ]);
        (await SessionMark.ReadAsync(connection, token)).ShouldBe(3);
    }

    // Under SET XACT_ABORT ON the THROW rolls back the transaction that the method ran in, and the method still returns
    // the rows and throws nothing, as the streaming StreamThenRaise does: the caller learns of the rollback only at the
    // commit.
    [Fact]
    public async Task ListThenRaise_ErrorAfterTheRowsInATransactionUnderXactAbort_RollsItBackUnreported()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await XactAbortTransaction.BeginAsync(sqlServer, token);

        var accounts = await session.Connection.ListThenRaise(
            transaction: session.Transaction,
            cancellationToken: token
        );

        accounts.ShouldBe([
            new CollectedAccount(1, "alice"),
            new CollectedAccount(2, "bob"),
            new CollectedAccount(3, "carol"),
        ]);
        await session.ShouldHaveRolledBackUnreportedAsync(token);
    }

    // The error after the row is lost by one as by many: the row is returned and nothing is thrown, though the batch
    // ran as far as the THROW.  A fix that reports the error flips this test.
    [Fact]
    public async Task GetThenRaise_ErrorAfterTheRow_ReturnsTheRowAndIsNotThrown()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        await SessionMark.ClearAsync(connection, token);

        var account = await connection.GetThenRaise(cancellationToken: token);

        account.ShouldBe(new CollectedAccount(1, "alice"));
        (await SessionMark.ReadAsync(connection, token)).ShouldBe(5);
    }

    // The same under one-optional.  A fix that reports the error flips this test.
    [Fact]
    public async Task FindThenRaise_ErrorAfterTheRow_ReturnsTheRowAndIsNotThrown()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        await SessionMark.ClearAsync(connection, token);

        var account = await connection.FindThenRaise(cancellationToken: token);

        account.ShouldBe(new CollectedAccount(1, "alice"));
        (await SessionMark.ReadAsync(connection, token)).ShouldBe(6);
    }

    // The batch of ListThenRaise under none, which reads no rows, reports the error of the THROW.
    [Fact]
    public async Task RunThenRaise_ErrorAfterTheRows_ThrowsTheError()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        await SessionMark.ClearAsync(connection, token);

        var thrown = await Should.ThrowAsync<SqlException>(() => connection.RunThenRaise(cancellationToken: token));

        thrown.Number.ShouldBe(50000);
        thrown.Message.ShouldBe("after the rows");
        (await SessionMark.ReadAsync(connection, token)).ShouldBe(4);
    }

    // The same batch under rowcount, which reads no rows either, reports the error too.
    [Fact]
    public async Task CountThenRaise_ErrorAfterTheRows_ThrowsTheError()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        await SessionMark.ClearAsync(connection, token);

        var thrown = await Should.ThrowAsync<SqlException>(() => connection.CountThenRaise(cancellationToken: token));

        thrown.Number.ShouldBe(50000);
        thrown.Message.ShouldBe("after the rows");
        (await SessionMark.ReadAsync(connection, token)).ShouldBe(7);
    }
}
