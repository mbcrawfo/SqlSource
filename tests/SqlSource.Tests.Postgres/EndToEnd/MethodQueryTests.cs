using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Postgres.EndToEnd;

// The queries of Queries/Methods.sql, run on the seed of "app" through the methods that MethodQueries gets: each result
// shape with the rows it allows and those it refuses, what rowcount counts, a token given its sample and another
// fragment, and a method named with Async after it.  A test that writes runs in a transaction that is rolled back, and
// gives it to the method.  The others give the method an open connection or a closed one, which the method opens.
// What the methods declare is read by MethodShapeTests.
[Collection(UsesPostgres.Name)]
public sealed class MethodQueryTests(PostgresContainer postgres)
{
    [Fact]
    public async Task TouchAccount_OneRow_Returns1()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);

        var count = await connection.TouchAccount(new TouchAccountArgs(1), transaction, cancellationToken: token);

        count.ShouldBe(1);
        (await UpdatedInThisTransactionAsync(connection, transaction, 1, token)).ShouldBeTrue();
        await transaction.RollbackAsync(token);
    }

    [Fact]
    public async Task TouchAccount_NoRow_Returns0()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);

        var count = await connection.TouchAccount(new TouchAccountArgs(99), transaction, cancellationToken: token);

        count.ShouldBe(0);
        await transaction.RollbackAsync(token);
    }

    [Fact]
    public async Task TouchAccountQuietly_Runs_ReturnsNothing()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);

        await connection.TouchAccountQuietly(new TouchAccountQuietlyArgs(2), transaction, cancellationToken: token);

        (await UpdatedInThisTransactionAsync(connection, transaction, 2, token)).ShouldBeTrue();
        await transaction.RollbackAsync(token);
    }

    [Fact]
    public async Task GetAccountInRange_OneRow_ReadsIt()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var account = await connection.GetAccountInRange(new GetAccountInRangeArgs(2, 2), cancellationToken: token);

        account.ShouldBe(new GetAccountInRangeDto(2, "bob"));
    }

    [Fact]
    public async Task GetAccountInRange_NoRow_ThrowsUnexpectedRowCount()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var exception = await Should.ThrowAsync<UnexpectedRowCountException>(() =>
            connection.GetAccountInRange(new GetAccountInRangeArgs(4, 9), cancellationToken: token)
        );

        exception.QueryName.ShouldBe("GetAccountInRange");
        exception.Message.ShouldBe("The query 'GetAccountInRange' returned no rows.");
    }

    [Fact]
    public async Task GetAccountInRange_TwoRows_ThrowsUnexpectedRowCount()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var exception = await Should.ThrowAsync<UnexpectedRowCountException>(() =>
            connection.GetAccountInRange(new GetAccountInRangeArgs(1, 2), cancellationToken: token)
        );

        exception.QueryName.ShouldBe("GetAccountInRange");
        exception.Message.ShouldBe("The query 'GetAccountInRange' returned more than one row.");
    }

    [Fact]
    public async Task FindAccountInRange_OneRow_ReadsIt()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var account = await connection.FindAccountInRange(new FindAccountInRangeArgs(3, 5), cancellationToken: token);

        account.ShouldBe(new FindAccountInRangeDto(3, "carol"));
    }

    [Fact]
    public async Task FindAccountInRange_NoRow_ReturnsNull()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var account = await connection.FindAccountInRange(new FindAccountInRangeArgs(4, 9), cancellationToken: token);

        account.ShouldBeNull();
    }

    [Fact]
    public async Task FindAccountInRange_TwoRows_ThrowsUnexpectedRowCount()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var exception = await Should.ThrowAsync<UnexpectedRowCountException>(() =>
            connection.FindAccountInRange(new FindAccountInRangeArgs(2, 3), cancellationToken: token)
        );

        exception.QueryName.ShouldBe("FindAccountInRange");
        exception.Message.ShouldBe("The query 'FindAccountInRange' returned more than one row.");
    }

    [Fact]
    public async Task CountNotesOfAsync_Alice_Reads2()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var count = await connection.CountNotesOfAsync(new CountNotesOfArgs(1), cancellationToken: token);

        count.ShouldBe(new CountNotesOfDto(2));
    }

    [Fact]
    public async Task ListAccountsWhere_DefaultFilter_ReadsTheAccount()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var accounts = await connection.ListAccountsWhere(
            new ListAccountsWhereArgs(2),
            "id = @accountId",
            cancellationToken: token
        );

        accounts.ShouldBe([new ListAccountsWhereDto(2, "bob")]);
    }

    // The fragment leaves out the parameter that the query declares, which the method still sends, and the server
    // never reads.
    [Fact]
    public async Task ListAccountsWhere_AnotherFilterThatDropsTheParameter_ReadsItsRows()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var accounts = await connection.ListAccountsWhere(
            new ListAccountsWhereArgs(2),
            "status = 'active'",
            cancellationToken: token
        );

        accounts.ShouldBe([new ListAccountsWhereDto(1, "alice")]);
    }

    [Fact]
    public async Task AddNote_InsertReturning_Returns1()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);

        var count = await connection.AddNote(
            new AddNoteArgs(3, "Called about the suspension."),
            transaction,
            cancellationToken: token
        );

        count.ShouldBe(1);
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM notes WHERE account_id = 3 AND body = 'Called about the suspension.'",
            connection,
            transaction
        );
        (await command.ExecuteScalarAsync(token)).ShouldBe(1L);
        await transaction.RollbackAsync(token);
    }

    [Fact]
    public async Task ListAccountsAsRowCount_Select_ReturnsMinus1()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var count = await connection.ListAccountsAsRowCount(cancellationToken: token);

        count.ShouldBe(-1);
    }

    // The WITH updates a row, and the count is still that of its SELECT.
    [Fact]
    public async Task RenameInCte_UpdateInsideAWith_ReturnsMinus1()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);

        var count = await connection.RenameInCte(new RenameInCteArgs(3), transaction, cancellationToken: token);

        count.ShouldBe(-1);
        (await UpdatedInThisTransactionAsync(connection, transaction, 3, token)).ShouldBeTrue();
        await transaction.RollbackAsync(token);
    }

    // Whether the transaction wrote the account's row: an update that changes no value still writes a new version of
    // the row, whose xmin is the transaction's id.  An xmin is the low 32 bits of the transaction's 64-bit id.
    private static async Task<bool> UpdatedInThisTransactionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int accountId,
        CancellationToken token
    )
    {
        await using var command = new NpgsqlCommand(
            "SELECT xmin::text::bigint = pg_current_xact_id()::text::bigint % 4294967296 FROM accounts WHERE id = $1",
            connection,
            transaction
        );
        _ = command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = accountId });
        return (bool)(await command.ExecuteScalarAsync(token))!;
    }
}
