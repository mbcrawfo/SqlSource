using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// The queries of Queries/Accounts.sql, run through Dapper on the seed of "app" with their input models as the
// parameters, into their output models.  AccountQueries keeps its SQL private, so the SQL is Reporting.AccountReports',
// which is the same file's.
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
    public async Task QuerySingleAsync_GetAccount_ReadsTheAccount()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var account = await connection.QuerySingleAsync<GetAccountDto>(
            Reporting.AccountReports.GetAccount,
            new GetAccountArgs(2)
        );

        account.ShouldBe(new GetAccountDto("bob", 2, "bob@example.com"));
    }

    [Fact]
    public async Task QueryAsync_ListAccounts_ReadsEveryAccountIntoTheTableModel()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var accounts = await connection.QueryAsync<AccountsDto>(Reporting.AccountReports.ListAccounts);

        accounts.ShouldBe(Accounts);
    }

    [Fact]
    public async Task QueryAsync_ListAccountsInTableOrder_ReadsTheSameRows()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var accounts = await connection.QueryAsync<AccountsDto>(Reporting.AccountReports.ListAccountsInTableOrder);

        accounts.ShouldBe(Accounts);
    }

    [Fact]
    public async Task QueryAsync_ListAccountsUntabled_ReadsEveryAccount()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var accounts = await connection.QueryAsync<ListAccountsUntabledDto>(
            Reporting.AccountReports.ListAccountsUntabled
        );

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
    public async Task QuerySingleAsync_SharedModel_ReadsBothQueries()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var byId = await connection.QuerySingleAsync<AccountName>(
            Reporting.AccountReports.GetAccountName,
            new GetAccountNameArgs(1)
        );
        var byEmail = await connection.QuerySingleAsync<AccountName>(
            Reporting.AccountReports.FindAccountName,
            new FindAccountNameArgs("carol@example.com")
        );

        byId.ShouldBe(new AccountName(1, "alice"));
        byEmail.ShouldBe(new AccountName(3, "carol"));
    }

    [Fact]
    public async Task QueryAsync_ListClosedBeforeWithANullParameter_ReadsEveryAccount()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var accounts = await connection.QueryAsync<ListClosedBeforeDto>(
            Reporting.AccountReports.ListClosedBefore,
            new ListClosedBeforeArgs(null)
        );

        accounts.ShouldBe([
            new ListClosedBeforeDto(1, "alice", null),
            new ListClosedBeforeDto(2, "bob", BobClosedAt),
            new ListClosedBeforeDto(3, "carol", null),
        ]);
    }

    // SQL Server takes a datetimeoffset at any offset: 2024-10-01 at +02:00 is still after bob's closing.
    [Fact]
    public async Task QueryAsync_ListClosedBeforeADate_ReadsTheClosedAccount()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var accounts = await connection.QueryAsync<ListClosedBeforeDto>(
            Reporting.AccountReports.ListClosedBefore,
            new ListClosedBeforeArgs(new DateTimeOffset(2024, 10, 1, 0, 0, 0, TimeSpan.FromHours(2)))
        );

        accounts.ShouldBe([new ListClosedBeforeDto(2, "bob", BobClosedAt)]);
    }

    // The 31 days up to 2024-03-01 take bob, opened on 2024-02-01, and carol, opened on that day, and not alice.
    [Fact]
    public async Task QueryAsync_ListOpenedWithin_ReadsTheAccountsOfThoseDays()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var accounts = await connection.QueryAsync<ListOpenedWithinDto>(
            Reporting.AccountReports.ListOpenedWithin,
            new ListOpenedWithinArgs(31, new DateOnly(2024, 3, 1))
        );

        accounts.ShouldBe([
            new ListOpenedWithinDto(2, "bob", new DateOnly(2024, 2, 1)),
            new ListOpenedWithinDto(3, "carol", new DateOnly(2024, 3, 1)),
        ]);
    }

    [Fact]
    public async Task QuerySingleAsync_SecondNamespace_ReadsTheSameAccount()
    {
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var account = await connection.QuerySingleAsync<Reporting.GetAccountDto>(
            Reporting.AccountReports.GetAccount,
            new Reporting.GetAccountArgs(3)
        );
        var accounts = await connection.QueryAsync<Reporting.AccountsDto>(Reporting.AccountReports.ListAccounts);

        account.ShouldBe(new Reporting.GetAccountDto("carol", 3, "carol@example.com"));
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
