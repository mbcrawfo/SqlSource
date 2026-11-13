using System;
using System.Collections.Generic;
using SqlSource.DatabaseTests.Postgres.SyncAsync.Reporting;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios.Calls;
using Reports = SqlSource.DatabaseTests.Postgres.SyncAsync.Reporting;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios;

// Queries/Accounts.sql, through both of its claims, AccountQueries and Reporting.AccountReports, which each get their
// own models: the reads of the seed's accounts, an id with no account, and a time that Npgsql refuses to send.
internal static class AccountsScenarios
{
    private static readonly DateTimeOffset AfterBobClosed = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // timestamptz takes only a DateTimeOffset at offset zero; Npgsql throws for this one before it sends anything.
    private static readonly DateTimeOffset NotAtUtc = new(2025, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));

    public static IEnumerable<Scenario> All => [.. OfClaim(reports: false), .. OfClaim(reports: true)];

    private static List<Scenario> OfClaim(bool reports)
    {
        var host = reports ? typeof(AccountReportsExtensions) : typeof(AccountQueriesExtensions);
        object Account(int id) => reports ? new Reports.GetAccountArgs(id) : new GetAccountArgs(id);
        object Name(int id) => reports ? new Reports.GetAccountNameArgs(id) : new GetAccountNameArgs(id);
        object Email(string email) => reports ? new Reports.FindAccountNameArgs(email) : new FindAccountNameArgs(email);
        object ClosedBefore(DateTimeOffset? closedBefore) =>
            reports ? new Reports.ListClosedBeforeArgs(closedBefore) : new ListClosedBeforeArgs(closedBefore);

        var scenarios = new List<Scenario>();
        scenarios.AddRange(OpenAndClosed(host, "GetAccount", Account(1)));
        scenarios.Add(Of(host, "GetAccount", ScenarioTag.Other, "no account, closed", Closed, Account(99)));
        scenarios.Add(Of(host, "GetAccount", ScenarioTag.Ordinary, "in a transaction", InTransaction, Account(2)));
        foreach (
            var method in new[]
            {
                "ListAccounts",
                "ListAccountsUnpooled",
                "ListAccountsInTableOrder",
                "ListAccountsUntabled",
            }
        )
        {
            scenarios.AddRange(OpenAndClosed(host, method));
        }

        foreach (var method in new[] { "GetAccountName", "GetAccountNameUnpooled" })
        {
            scenarios.AddRange(OpenAndClosed(host, method, Name(3)));
        }

        scenarios.AddRange(OpenAndClosed(host, "FindAccountName", Email("bob@example.com")));
        foreach (var method in new[] { "ListClosedBefore", "ListClosedBeforeUnpooled" })
        {
            scenarios.Add(Of(host, method, ScenarioTag.Ordinary, "open", Open, ClosedBefore(null)));
            scenarios.Add(Of(host, method, ScenarioTag.Ordinary, "closed", Closed, ClosedBefore(AfterBobClosed)));
            scenarios.Add(
                Of(host, method, ScenarioTag.Other, "a time not at UTC, closed", Closed, ClosedBefore(NotAtUtc))
            );
        }

        return scenarios;
    }
}
