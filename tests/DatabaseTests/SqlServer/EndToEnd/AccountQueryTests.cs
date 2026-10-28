using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The queries of Queries/Accounts.sql, run through their generated methods on the seed of "app" with their input models
// as the parameters, into their output models: AccountQueries' methods, extensions of the connection in this
// namespace, and Reporting.AccountReports', in the extension class of its own namespace.  Each method opens the
// connection it is given and closes it again.
[Collection(UsesSqlServer.Name)]
public sealed class AccountQueryTests(SqlServerContainer sqlServer)
{
    private static readonly DateTimeOffset AliceCreatedAt = new(2024, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset BobCreatedAt = new(2024, 2, 1, 9, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset BobClosedAt = new(2024, 9, 1, 17, 45, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CarolCreatedAt = new(2024, 3, 1, 10, 0, 0, TimeSpan.Zero);

    // The seed's three accounts, with the date each was opened, and the generated name_length.
    private static readonly AccountsDto[] Accounts =
    [
        new(1, "alice", "alice@example.com", "active", new DateOnly(2024, 1, 1), AliceCreatedAt, null, 5),
        new(2, "bob", "bob@example.com", "closed", new DateOnly(2024, 2, 1), BobCreatedAt, BobClosedAt, 3),
        new(3, "carol", "carol@example.com", "suspended", new DateOnly(2024, 3, 1), CarolCreatedAt, null, 5),
    ];

    [Fact]
    public async Task GetAccount_ById_ReadsTheAccount()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.GetAccount(
            new GetAccountArgs(2),
            cancellationToken: TestContext.Current.CancellationToken
        );

        accounts.ShouldBe([new GetAccountDto("bob", 2, "bob@example.com")]);
    }

    [Fact]
    public async Task ListAccounts_Seed_ReadsEveryAccountIntoTheTableModel()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListAccounts(cancellationToken: TestContext.Current.CancellationToken);

        accounts.ShouldBe(Accounts);
    }

    [Fact]
    public async Task ListAccountsInTableOrder_Seed_ReadsTheSameRows()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListAccountsInTableOrder(
            cancellationToken: TestContext.Current.CancellationToken
        );

        accounts.ShouldBe(Accounts);
    }

    [Fact]
    public async Task ListAccountsUntabled_Seed_ReadsEveryAccount()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListAccountsUntabled(cancellationToken: TestContext.Current.CancellationToken);

        accounts.ShouldBe(
            Accounts.Select(static account => new ListAccountsUntabledDto(
                account.Id,
                account.Name,
                account.Email,
                account.Status,
                account.OpenedOn,
                account.CreatedAt,
                account.ClosedAt,
                account.NameLength
            ))
        );
    }

    // The email is a parameter of the alias type dbo.Email, which the input model has as a string.
    [Fact]
    public async Task FindAccountName_SharedModel_ReadsTheSameModelAsGetAccountName()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var byId = await connection.GetAccountName(new GetAccountNameArgs(1), cancellationToken: token);
        var byEmail = await connection.FindAccountName(
            new FindAccountNameArgs("carol@example.com"),
            cancellationToken: token
        );

        byId.ShouldBe([new AccountName(1, "alice")]);
        byEmail.ShouldBe([new AccountName(3, "carol")]);
    }

    [Fact]
    public async Task ListClosedBefore_NullParameter_ReadsEveryAccount()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListClosedBefore(
            new ListClosedBeforeArgs(null),
            cancellationToken: TestContext.Current.CancellationToken
        );

        accounts.ShouldBe([
            new ListClosedBeforeDto(1, "alice", null),
            new ListClosedBeforeDto(2, "bob", BobClosedAt),
            new ListClosedBeforeDto(3, "carol", null),
        ]);
    }

    // SQL Server takes a datetimeoffset at any offset: 2024-10-01 at +02:00 is still after bob's closing.
    [Fact]
    public async Task ListClosedBefore_ADate_ReadsTheClosedAccount()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListClosedBefore(
            new ListClosedBeforeArgs(new DateTimeOffset(2024, 10, 1, 0, 0, 0, TimeSpan.FromHours(2))),
            cancellationToken: TestContext.Current.CancellationToken
        );

        accounts.ShouldBe([new ListClosedBeforeDto(2, "bob", BobClosedAt)]);
    }

    // The 31 days up to 2024-03-01 take bob, opened on 2024-02-01, and carol, opened on that day, and not alice.
    [Fact]
    public async Task ListOpenedWithin_ThirtyOneDays_ReadsTheAccountsOfThoseDays()
    {
        await using var connection = EndToEndSources.App(sqlServer);

        var accounts = await connection.ListOpenedWithin(
            new ListOpenedWithinArgs(31, new DateOnly(2024, 3, 1)),
            cancellationToken: TestContext.Current.CancellationToken
        );

        accounts.ShouldBe([
            new ListOpenedWithinDto(2, "bob", new DateOnly(2024, 2, 1)),
            new ListOpenedWithinDto(3, "carol", new DateOnly(2024, 3, 1)),
        ]);
    }

    // The second namespace's methods are in its own extension class, which this namespace does not import, so they
    // are called as static methods.
    [Fact]
    public async Task GetAccount_SecondNamespace_ReadsTheSameAccount()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var account = await Reporting.AccountReportsExtensions.GetAccount(
            connection,
            new Reporting.GetAccountArgs(3),
            cancellationToken: token
        );
        var accounts = await Reporting.AccountReportsExtensions.ListAccounts(connection, cancellationToken: token);

        account.ShouldBe([new Reporting.GetAccountDto("carol", 3, "carol@example.com")]);
        accounts.ShouldBe(
            Accounts.Select(static account => new Reporting.AccountsDto(
                account.Id,
                account.Name,
                account.Email,
                account.Status,
                account.OpenedOn,
                account.CreatedAt,
                account.ClosedAt,
                account.NameLength
            ))
        );
    }
}
