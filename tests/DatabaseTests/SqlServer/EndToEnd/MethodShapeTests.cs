using System;
using System.Data.SqlTypes;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The result shapes of Queries/Methods.sql, and its token, run through MethodQueries' methods on the seed of "app": a
// query with no rows is rowcount by default and runs as none, one and one-optional read their row and refuse the rows
// their shape does not allow, and a token is given the text of its default or another fragment.  A write runs in a
// transaction that is rolled back.  Every shape throws an error that a statement after the rows raises: none and
// rowcount read no rows, and a shape that reads them reads the results after them before it returns.  When a shape
// that reads rows throws an exception of its own, the reader's disposal reads those results and drops their error, and
// when that error rolled back the caller's transaction the method throws TransactionRolledBackException, with its own
// exception inside; a database error at a row is thrown as it is, and what that leaves is pinned too.  A transaction
// that has completed, committed or rolled back by an earlier error, is refused before anything runs.
[Collection(UsesSqlServer.Name)]
public sealed class MethodShapeTests(SqlServerDatabases sqlServer)
{
    // The number of SQL Server's error "Conversion failed when converting the varchar value 'x' to data type int.".
    private const int ConversionFailed = 245;

    // The number of SQL Server's error "Divide by zero error encountered.".
    private const int DivideByZero = 8134;

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

    // A method that reads its rows into an array reads the results after them before it returns, so the THROW after
    // the rows is thrown, as by the streaming StreamThenRaise, and the mark shows that the batch ran as far as it.
    [Fact]
    public async Task ListThenRaise_ErrorAfterTheRows_ThrowsTheError()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        await SessionMark.ClearAsync(connection, token);

        var thrown = await Should.ThrowAsync<SqlException>(() => connection.ListThenRaise(cancellationToken: token));

        thrown.Number.ShouldBe(50000);
        thrown.Message.ShouldBe("after the rows");
        (await SessionMark.ReadAsync(connection, token)).ShouldBe(3);
    }

    // Under SET XACT_ABORT ON the THROW rolls back the transaction that the method ran in, and the method throws the
    // error, as the streaming StreamThenRaise does, so the caller learns of the rollback from the method.
    [Fact]
    public async Task ListThenRaise_ErrorAfterTheRowsInATransactionUnderXactAbort_ThrowsAndRollsItBack()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: true, token);

        var thrown = await Should.ThrowAsync<SqlException>(() =>
            session.Connection.ListThenRaise(transaction: session.Transaction, cancellationToken: token)
        );

        thrown.Number.ShouldBe(50000);
        await session.ShouldHaveRolledBackAsync(token);
    }

    // With XACT_ABORT off, as SqlClient leaves it, the failed conversion after the rows rolls back the transaction that
    // the method ran in, and the method throws the error, as the streaming StreamThenFailConversion does.  The
    // conversion is a SELECT, whose result set the method moves to before its error is raised.
    [Fact]
    public async Task ListThenFailConversion_ErrorAfterTheRowsInATransaction_ThrowsAndRollsItBack()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: false, token);

        var thrown = await Should.ThrowAsync<SqlException>(() =>
            session.Connection.ListThenFailConversion(transaction: session.Transaction, cancellationToken: token)
        );

        thrown.Number.ShouldBe(ConversionFailed);
        await session.ShouldHaveRolledBackAsync(token);
    }

    // The second row makes one throw its refusal, and the reader's disposal reads the failed conversion after the
    // rows, which with XACT_ABORT off rolls back the transaction and is dropped: the method throws the rollback, with
    // the refusal inside it, so the caller learns of the rollback from the method.
    [Fact]
    public async Task GetTwoThenFailConversion_SecondRowInATransaction_ThrowsTheRollbackWithTheRowCountInside()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: false, token);

        var thrown = await Should.ThrowAsync<TransactionRolledBackException>(() =>
            session.Connection.GetTwoThenFailConversion(transaction: session.Transaction, cancellationToken: token)
        );

        thrown.QueryName.ShouldBe("GetTwoThenFailConversion");
        thrown.Message.ShouldBe(
            "The transaction was rolled back by an error that the query 'GetTwoThenFailConversion' raised after the "
                + "inner exception was thrown."
        );
        var inner = thrown.InnerException.ShouldBeOfType<UnexpectedRowCountException>();
        inner.QueryName.ShouldBe("GetTwoThenFailConversion");
        inner.Message.ShouldBe("The query 'GetTwoThenFailConversion' returned more than one row.");
        await session.ShouldHaveRolledBackAsync(token);
    }

    // With no transaction there is none to check, and the method throws its refusal as it is.
    [Fact]
    public async Task GetTwoThenFailConversion_SecondRowWithoutATransaction_ThrowsTheRowCount()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var thrown = await Should.ThrowAsync<UnexpectedRowCountException>(() =>
            connection.GetTwoThenFailConversion(cancellationToken: TestContext.Current.CancellationToken)
        );

        thrown.QueryName.ShouldBe("GetTwoThenFailConversion");
        thrown.Message.ShouldBe("The query 'GetTwoThenFailConversion' returned more than one row.");
    }

    // With XACT_ABORT off the THROW after the rows leaves the transaction open, so the method throws its refusal as
    // it is, and the commit after it commits what the transaction did.
    [Fact]
    public async Task GetTwoThenRaise_SecondRowInATransaction_ThrowsTheRowCountAndCommits()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: false, token);

        var thrown = await Should.ThrowAsync<UnexpectedRowCountException>(() =>
            session.Connection.GetTwoThenRaise(transaction: session.Transaction, cancellationToken: token)
        );

        thrown.QueryName.ShouldBe("GetTwoThenRaise");
        await session.ShouldCommitAsync(token);
    }

    // Under SET XACT_ABORT ON the same THROW rolls the transaction back, and the method throws the rollback, with its
    // refusal inside it.
    [Fact]
    public async Task GetTwoThenRaise_SecondRowInATransactionUnderXactAbort_ThrowsTheRollback()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: true, token);

        var thrown = await Should.ThrowAsync<TransactionRolledBackException>(() =>
            session.Connection.GetTwoThenRaise(transaction: session.Transaction, cancellationToken: token)
        );

        thrown.QueryName.ShouldBe("GetTwoThenRaise");
        _ = thrown.InnerException.ShouldBeOfType<UnexpectedRowCountException>();
        await session.ShouldHaveRolledBackAsync(token);
    }

    // The caller catches an error that rolled back its transaction and goes on to give that transaction to a method,
    // which SqlClient would run outside any transaction: the method refuses it before it runs the query, so its
    // refusal of the second row is not reported as a rollback that the query did not cause.  The transaction is as the
    // error left it.
    [Fact]
    public async Task GetTwoThenRaise_TransactionRolledBackByAnEarlierError_IsRefused()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: false, token);
        await using (var fail = new SqlCommand("SELECT CAST('x' AS int);", session.Connection, session.Transaction))
        {
            var error = await Should.ThrowAsync<SqlException>(() => fail.ExecuteScalarAsync(token));
            error.Number.ShouldBe(ConversionFailed);
        }

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
            session.Connection.GetTwoThenRaise(transaction: session.Transaction, cancellationToken: token)
        );

        thrown.GetType().ShouldBe(typeof(InvalidOperationException));
        thrown.Message.ShouldBe(
            "The transaction given to the query 'GetTwoThenRaise' has completed; it is no longer usable."
        );
        await session.ShouldHaveRolledBackAsync(token);
    }

    // A none method given a transaction that has committed is refused before anything runs, where SqlClient would run
    // its batch outside any transaction and commit it on its own: the mark that its batch sets on the session is not
    // there, and the error after it is not raised.
    [Fact]
    public async Task RunThenRaise_CommittedTransaction_IsRefusedAndRunsNothing()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: false, token);
        await session.ShouldCommitAsync(token);
        await SessionMark.ClearAsync(session.Connection, token);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
            session.Connection.RunThenRaise(transaction: session.Transaction, cancellationToken: token)
        );

        thrown.GetType().ShouldBe(typeof(InvalidOperationException));
        thrown.Message.ShouldBe(
            "The transaction given to the query 'RunThenRaise' has completed; it is no longer usable."
        );
        (await SessionMark.ReadAsync(session.Connection, token)).ShouldBe(0);
    }

    // A rowcount method given a transaction that an error rolled back is refused before anything runs: the mark that
    // its batch sets on the session is not there, and the error after it is not raised.
    [Fact]
    public async Task CountThenRaise_TransactionRolledBackByAnEarlierError_IsRefusedAndRunsNothing()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: false, token);
        await using (var fail = new SqlCommand("SELECT CAST('x' AS int);", session.Connection, session.Transaction))
        {
            _ = await Should.ThrowAsync<SqlException>(() => fail.ExecuteScalarAsync(token));
        }

        await SessionMark.ClearAsync(session.Connection, token);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
            session.Connection.CountThenRaise(transaction: session.Transaction, cancellationToken: token)
        );

        thrown.GetType().ShouldBe(typeof(InvalidOperationException));
        thrown.Message.ShouldBe(
            "The transaction given to the query 'CountThenRaise' has completed; it is no longer usable."
        );
        (await SessionMark.ReadAsync(session.Connection, token)).ShouldBe(0);
    }

    // A method that reads its rows into a collection throws at the NULL of the third row, and the failed conversion
    // after the rows rolls back the transaction: the method throws the rollback, with the NULL's exception inside it.
    [Fact]
    public async Task ListNullAtThirdThenFailConversion_InATransaction_ThrowsTheRollbackWithTheNullInside()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: false, token);

        var thrown = await Should.ThrowAsync<TransactionRolledBackException>(() =>
            session.Connection.ListNullAtThirdThenFailConversion(
                transaction: session.Transaction,
                cancellationToken: token
            )
        );

        thrown.QueryName.ShouldBe("ListNullAtThirdThenFailConversion");
        _ = thrown.InnerException.ShouldBeOfType<SqlNullValueException>();
        await session.ShouldHaveRolledBackAsync(token);
    }

    // What is observed, on 2025 and on 2017: the server sends the first row, then raises the divide by zero at the
    // second, error 8134, which SqlClient's read throws.  That is the database's error, which the method throws as it
    // is, and the reader's disposal reads the failed conversion after the rows and drops it.  With XACT_ABORT off the
    // divide by zero alone leaves the transaction open, but the conversion rolls it back, so the caller gets the
    // divide by zero, the transaction's Connection is null and the commit throws: nothing reports the rollback
    // (TD-0090).
    [Fact]
    public async Task ListFailingAtSecondThenFailConversion_InATransaction_ThrowsTheDivideByZeroAndRollsBack()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = await UnpooledTransaction.BeginAsync(sqlServer, xactAbort: false, token);

        var thrown = await Should.ThrowAsync<SqlException>(() =>
            session.Connection.ListFailingAtSecondThenFailConversion(
                transaction: session.Transaction,
                cancellationToken: token
            )
        );

        thrown.Number.ShouldBe(DivideByZero);
        await session.ShouldHaveRolledBackAsync(token);
    }

    // A method of one reads the results after its second read, so the THROW after the row is thrown, as by many.
    [Fact]
    public async Task GetThenRaise_ErrorAfterTheRow_ThrowsTheError()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        await SessionMark.ClearAsync(connection, token);

        var thrown = await Should.ThrowAsync<SqlException>(() => connection.GetThenRaise(cancellationToken: token));

        thrown.Number.ShouldBe(50000);
        thrown.Message.ShouldBe("after the rows");
        (await SessionMark.ReadAsync(connection, token)).ShouldBe(5);
    }

    // The same under one-optional.
    [Fact]
    public async Task FindThenRaise_ErrorAfterTheRow_ThrowsTheError()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        await SessionMark.ClearAsync(connection, token);

        var thrown = await Should.ThrowAsync<SqlException>(() => connection.FindThenRaise(cancellationToken: token));

        thrown.Number.ShouldBe(50000);
        thrown.Message.ShouldBe("after the rows");
        (await SessionMark.ReadAsync(connection, token)).ShouldBe(6);
    }

    // A method of one-optional that finds no row reads the results after it too, so the error is thrown rather than
    // hidden behind the null.
    [Fact]
    public async Task FindNoneThenRaise_NoRowAndAnErrorAfter_ThrowsTheError()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        await SessionMark.ClearAsync(connection, token);

        var thrown = await Should.ThrowAsync<SqlException>(() =>
            connection.FindNoneThenRaise(cancellationToken: token)
        );

        thrown.Number.ShouldBe(50000);
        thrown.Message.ShouldBe("after the rows");
        (await SessionMark.ReadAsync(connection, token)).ShouldBe(8);
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
