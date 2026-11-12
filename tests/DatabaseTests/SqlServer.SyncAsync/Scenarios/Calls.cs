using System;
using System.Collections.Generic;
using System.Data.Common;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios;

// What the scenario files share: the setups they use most and the scenario named "<Host>.<Method>: <what>".
internal static class Calls
{
    public static Setup Open { get; } = new();

    public static Setup Closed { get; } = new(ConnectionStart.Closed);

    public static Setup InTransaction { get; } = new(Transaction: CallerTransaction.Begun);

    // A transaction the caller began on the connection and did not pass, which SqlClient refuses the command for.
    public static Setup TransactionNotPassed { get; } = InTransaction with { PassTransaction = false };

    // A transaction the caller began and rolled back before the call, which the method refuses.
    public static Setup CompletedTransaction { get; } = new(Transaction: CallerTransaction.Completed);

    // The accounts of "app", without created_at, which the seed writes out but an insert sets to now.
    public const string Accounts = "SELECT id, name, email, status, opened_on, closed_at FROM dbo.accounts ORDER BY id";

    public const string Notes = "SELECT id, account_id, kind, body, written_at, remind_at FROM dbo.notes ORDER BY id";

    // A setup that writes, on a copy of the seed, and records the tables.
    public static Setup Writes(params string[] tables) => new(Writes: true, Tables: tables);

    public static Scenario Of(
        Type host,
        string method,
        ScenarioTag tag,
        string what,
        Setup setup,
        object? parameters = null,
        IReadOnlyDictionary<string, string>? tokens = null
    ) => new($"{host.Name}.{method}: {what}", host, method, MethodKind.Query, tag, setup, parameters, tokens);

    // A call of the collection method, "<Host>.<Method>, collection: <what>", which a query's method of the same name
    // overloads.
    public static Scenario OfCollection(
        Type host,
        string method,
        ScenarioTag tag,
        string what,
        Setup setup,
        object items,
        Func<DbConnection, DbDataSource, object>? instance = null
    ) =>
        new(
            $"{host.Name}.{method}, collection: {what}",
            host,
            method,
            MethodKind.Collection,
            tag,
            setup,
            items,
            Instance: instance
        );

    // An Ordinary call on an open connection and the same call on a closed one.
    public static IEnumerable<Scenario> OpenAndClosed(
        Type host,
        string method,
        object? parameters = null,
        IReadOnlyDictionary<string, string>? tokens = null
    ) =>
        [
            Of(host, method, ScenarioTag.Ordinary, "open", Open, parameters, tokens),
            Of(host, method, ScenarioTag.Ordinary, "closed", Closed, parameters, tokens),
        ];
}
