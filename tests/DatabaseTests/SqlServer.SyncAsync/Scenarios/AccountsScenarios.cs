using System;
using System.Collections.Generic;
using SqlSource.DatabaseTests.SqlServer.SyncAsync.Reporting;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios.Calls;
using Reports = SqlSource.DatabaseTests.SqlServer.SyncAsync.Reporting;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios;

// Queries/Accounts.sql, through both of its claims, AccountQueries and Reporting.AccountReports, which each get their
// own models: the reads of the seed's accounts, an id with no account, and a range of days that takes DATEADD past the
// first date, which the server refuses.
internal static class AccountsScenarios
{
    private static readonly DateTimeOffset AfterBobClosed = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly AsOf = new(2024, 2, 15);

    public static IEnumerable<Scenario> All => [.. OfClaim(reports: false), .. OfClaim(reports: true)];

    private static List<Scenario> OfClaim(bool reports)
    {
        var host = reports ? typeof(AccountReportsExtensions) : typeof(AccountQueriesExtensions);
        object Account(int id) => reports ? new Reports.GetAccountArgs(id) : new GetAccountArgs(id);
        object Name(int id) => reports ? new Reports.GetAccountNameArgs(id) : new GetAccountNameArgs(id);
        object Email(string email) => reports ? new Reports.FindAccountNameArgs(email) : new FindAccountNameArgs(email);
        object ClosedBefore(DateTimeOffset? closedBefore) =>
            reports ? new Reports.ListClosedBeforeArgs(closedBefore) : new ListClosedBeforeArgs(closedBefore);
        object Within(int days) =>
            reports ? new Reports.ListOpenedWithinArgs(days, AsOf) : new ListOpenedWithinArgs(days, AsOf);
        object InRange(int first, int last, string skipped) =>
            reports
                ? new Reports.ListAccountsInRangeArgs(first, last, skipped)
                : new ListAccountsInRangeArgs(first, last, skipped);

        var scenarios = new List<Scenario>();
        scenarios.AddRange(OpenAndClosed(host, "GetAccount", Account(1)));
        scenarios.Add(Of(host, "GetAccount", ScenarioTag.Other, "no account, closed", Closed, Account(99)));
        scenarios.Add(Of(host, "GetAccount", ScenarioTag.Ordinary, "in a transaction", InTransaction, Account(2)));
        foreach (var method in new[] { "ListAccounts", "ListAccountsInTableOrder", "ListAccountsUntabled" })
        {
            scenarios.AddRange(OpenAndClosed(host, method));
        }

        scenarios.AddRange(OpenAndClosed(host, "GetAccountName", Name(3)));
        scenarios.AddRange(OpenAndClosed(host, "FindAccountName", Email("bob@example.com")));
        scenarios.Add(Of(host, "ListClosedBefore", ScenarioTag.Ordinary, "open", Open, ClosedBefore(null)));
        scenarios.Add(
            Of(host, "ListClosedBefore", ScenarioTag.Ordinary, "closed", Closed, ClosedBefore(AfterBobClosed))
        );
        scenarios.AddRange(OpenAndClosed(host, "ListOpenedWithin", Within(45)));
        scenarios.Add(
            Of(
                host,
                "ListOpenedWithin",
                ScenarioTag.DatabaseError,
                "a date overflow, closed",
                Closed,
                Within(3_000_000)
            )
        );
        scenarios.AddRange(OpenAndClosed(host, "ListAccountsInRange", InRange(1, 3, "bob")));
        return scenarios;
    }
}
