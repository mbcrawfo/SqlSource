using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The queries of Queries/Accounts.sql, run on the seed of "app" through the methods that AccountQueries gets, with
// their input models as the parameters, into their output models, and two of them through Reporting.AccountReports',
// which are of a second namespace.  Every query is many, so each method returns an IReadOnlyList, the project's
// collection type.  Half the tests give the method an open connection and half a closed one, which the method opens.
[Collection(UsesPostgres.Name)]
public sealed class AccountQueryTests(PostgresContainer postgres)
{
    private static readonly DateTimeOffset AliceCreatedAt = new(2024, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset BobCreatedAt = new(2024, 2, 1, 9, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset BobClosedAt = new(2024, 9, 1, 17, 45, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CarolCreatedAt = new(2024, 3, 1, 10, 0, 0, TimeSpan.Zero);

    // The seed's three accounts, with the generated name_length.
    private static readonly AccountsDto[] Accounts =
    [
        new(1, "alice", "alice@example.com", "active", AliceCreatedAt, null, 5),
        new(2, "bob", "bob@example.com", "closed", BobCreatedAt, BobClosedAt, 3),
        new(3, "carol", "carol@example.com", "suspended", CarolCreatedAt, null, 5),
    ];

    [Fact]
    public async Task GetAccount_OfBob_ReadsTheAccount()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var accounts = await connection.GetAccount(new GetAccountArgs(2), cancellationToken: token);

        accounts.ShouldBe([new GetAccountDto("bob", 2, "bob@example.com")]);
    }

    [Fact]
    public async Task ListAccounts_EveryAccount_ReadsTheTableModel()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var accounts = await connection.ListAccounts(cancellationToken: token);

        accounts.ShouldBe(Accounts);
    }

    [Fact]
    public async Task ListAccountsInTableOrder_EveryAccount_ReadsTheSameRows()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var accounts = await connection.ListAccountsInTableOrder(cancellationToken: token);

        accounts.ShouldBe(Accounts);
    }

    [Fact]
    public async Task ListAccountsUntabled_EveryAccount_ReadsTheModelNamedAfterTheQuery()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var accounts = await connection.ListAccountsUntabled(cancellationToken: token);

        accounts.ShouldBe(
            Accounts.Select(static account => new ListAccountsUntabledDto(
                account.Id,
                account.Name,
                account.Email,
                account.Status,
                account.CreatedAt,
                account.ClosedAt,
                account.NameLength
            ))
        );
    }

    [Fact]
    public async Task GetAccountName_OfAlice_ReadsTheSharedModel()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var names = await connection.GetAccountName(new GetAccountNameArgs(1), cancellationToken: token);

        names.ShouldBe([new AccountName(1, "alice")]);
    }

    [Fact]
    public async Task FindAccountName_OfCarol_ReadsTheSharedModel()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var names = await connection.FindAccountName(
            new FindAccountNameArgs("carol@example.com"),
            cancellationToken: token
        );

        names.ShouldBe([new AccountName(3, "carol")]);
    }

    [Fact]
    public async Task ListClosedBefore_NullParameter_ReadsEveryAccount()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var accounts = await connection.ListClosedBefore(new ListClosedBeforeArgs(null), cancellationToken: token);

        accounts.ShouldBe([
            new ListClosedBeforeDto(1, "alice", null),
            new ListClosedBeforeDto(2, "bob", BobClosedAt),
            new ListClosedBeforeDto(3, "carol", null),
        ]);
    }

    [Fact]
    public async Task ListClosedBefore_ADate_ReadsTheClosedAccount()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var accounts = await connection.ListClosedBefore(
            new ListClosedBeforeArgs(new DateTimeOffset(2024, 10, 1, 0, 0, 0, TimeSpan.Zero)),
            cancellationToken: token
        );

        accounts.ShouldBe([new ListClosedBeforeDto(2, "bob", BobClosedAt)]);
    }

    // The second namespace's methods are in Reporting.AccountReportsExtensions, which this namespace does not import,
    // so they are called as static methods.
    [Fact]
    public async Task GetAccount_OfTheSecondNamespace_ReadsItsOwnModel()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var accounts = await Reporting.AccountReportsExtensions.GetAccount(
            connection,
            new Reporting.GetAccountArgs(3),
            cancellationToken: token
        );

        accounts.ShouldBe([new Reporting.GetAccountDto("carol", 3, "carol@example.com")]);
    }

    [Fact]
    public async Task ListAccounts_OfTheSecondNamespace_ReadsItsOwnTableModel()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var accounts = await Reporting.AccountReportsExtensions.ListAccounts(connection, cancellationToken: token);

        accounts.ShouldBe(
            Accounts.Select(static account => new Reporting.AccountsDto(
                account.Id,
                account.Name,
                account.Email,
                account.Status,
                account.CreatedAt,
                account.ClosedAt,
                account.NameLength
            ))
        );
    }
}
