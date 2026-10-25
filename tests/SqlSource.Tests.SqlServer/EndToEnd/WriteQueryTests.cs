using System;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// The queries of Queries/Writes.sql, each run through its generated method on the seed of "app" in a transaction that
// is rolled back, with the transaction given to every call, since SqlClient refuses a command on a connection whose
// transaction it is not given.  Each reads the row of its OUTPUT into its model, and the seed is as it was after the
// rollback.  The test's own statements, which read the next id and the seed, run through Dapper.
[Collection(UsesSqlServer.Name)]
public sealed class WriteQueryTests(SqlServerContainer sqlServer)
{
    // The notes of the seed, and the name and status of each account, which no write may leave changed.
    private static readonly SeedNote[] SeedNotes =
    [
        new(1, 1, "call", "Asked for a larger plan."),
        new(2, 1, "email", "Sent the quote."),
        new(3, 2, "visit", "Closed the account on site."),
    ];

    private static readonly SeedAccount[] SeedAccounts =
    [
        new(1, "alice", "active"),
        new(2, "bob", "closed"),
        new(3, "carol", "suspended"),
    ];

    [Fact]
    public async Task AddNote_InTransaction_ReturnsTheNewNote() =>
        await InTransactionAsync(
            async (connection, transaction) =>
            {
                var next = await connection.ExecuteScalarAsync<int>(
                    "SELECT CAST(IDENT_CURRENT('dbo.notes') + IDENT_INCR('dbo.notes') AS int)",
                    transaction: transaction
                );

                var notes = await connection.AddNote(
                    new AddNoteArgs(
                        3,
                        "call",
                        "Asked to reopen.",
                        new DateTime(2024, 10, 1, 9, 0, 0, DateTimeKind.Unspecified)
                    ),
                    transaction: transaction,
                    cancellationToken: TestContext.Current.CancellationToken
                );

                notes.ShouldBe([new AddNoteDto(next, "call")]);
            }
        );

    [Fact]
    public async Task RenameAccount_InTransaction_ReturnsTheOldAndNewNames() =>
        await InTransactionAsync(
            async (connection, transaction) =>
            {
                var names = await connection.RenameAccount(
                    new RenameAccountArgs("caroline", 3),
                    transaction: transaction,
                    cancellationToken: TestContext.Current.CancellationToken
                );

                names.ShouldBe([new RenameAccountDto("carol", "caroline")]);
            }
        );

    [Fact]
    public async Task MergeAccountStatus_InTransaction_ReturnsTheUpdate() =>
        await InTransactionAsync(
            async (connection, transaction) =>
            {
                var changes = await connection.MergeAccountStatus(
                    new MergeAccountStatusArgs(2, "active"),
                    transaction: transaction,
                    cancellationToken: TestContext.Current.CancellationToken
                );

                changes.ShouldBe([new MergeAccountStatusDto("UPDATE", 2)]);
            }
        );

    // Runs the write in a transaction that is rolled back, then reads the notes and the accounts on another
    // connection, as the seed has them.
    private async Task InTransactionAsync(Func<SqlConnection, DbTransaction, Task> write)
    {
        var token = TestContext.Current.CancellationToken;
        await using (var connection = EndToEndSources.App(sqlServer))
        {
            await connection.OpenAsync(token);
            await using var transaction = await connection.BeginTransactionAsync(token);
            try
            {
                await write(connection, transaction);
            }
            finally
            {
                await transaction.RollbackAsync(token);
            }
        }

        await using var after = EndToEndSources.App(sqlServer);
        await after.OpenAsync(token);
        (await after.QueryAsync<SeedNote>("SELECT id, account_id, kind, body FROM dbo.notes ORDER BY id")).ShouldBe(
            SeedNotes
        );
        (await after.QueryAsync<SeedAccount>("SELECT id, name, status FROM dbo.accounts ORDER BY id")).ShouldBe(
            SeedAccounts
        );
    }

    private sealed record SeedNote(int Id, int AccountId, string Kind, string Body);

    private sealed record SeedAccount(int Id, string Name, string Status);
}
