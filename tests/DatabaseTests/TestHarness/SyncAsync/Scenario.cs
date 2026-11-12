using System;
using System.Collections.Generic;
using System.Data.Common;

namespace SqlSource.DatabaseTests.TestHarness.SyncAsync;

// Whether the method takes one input model (a query) or the items of a collection (a batch).
internal enum MethodKind
{
    Query,
    Collection,
}

// Whether the connection the call receives is open or closed when it is made.
internal enum ConnectionStart
{
    Closed,
    Open,
}

// Whether the caller begins a transaction on the connection before the call, and whether it has ended by then.
internal enum CallerTransaction
{
    None,
    Begun,

    // Begun and rolled back before the call, which is given a transaction that has completed.
    Completed,
}

// What a scenario shows of its method; a collection method must have the last three of Empty, ChunkBoundary and
// LaterChunkFailure.
internal enum ScenarioTag
{
    Ordinary,
    NoRow,
    TooManyRows,
    DatabaseError,
    Empty,
    ChunkBoundary,
    LaterChunkFailure,
    Other,
}

// What happens around a call, before and after it.
internal sealed record Setup(
    ConnectionStart Connection = ConnectionStart.Open,
    CallerTransaction Transaction = CallerTransaction.None,
    // The engine's name of a data source: "default", "multiplexing", "nodatime", "gis", "billing".
    string Source = "default",
    // A fresh database for each run.
    bool Writes = false,
    // SELECTs whose rows the outcome records, each with its ORDER BY.
    IReadOnlyList<string>? Tables = null,
    // SQL run on the connection before the call.
    string? Prepare = null,
    // Whether the call is given the caller's transaction: false gives it null while the transaction stays begun on the
    // connection, as a caller who forgets to pass it does.
    bool PassTransaction = true
);

// One call of a method pair, with what surrounds it.
internal sealed record Scenario(
    // "<Host>.<Method>: <what>", unique per engine; the theory's row.
    string Name,
    // The type that declares both methods.
    Type Host,
    // The synchronous method's name; the asynchronous one is Method + "Async".
    string Method,
    MethodKind Kind,
    ScenarioTag Tag,
    Setup Setup,
    // The input model, or the items of a collection method.
    object? Parameters = null,
    IReadOnlyDictionary<string, string>? Tokens = null,
    int? CommandTimeout = null,
    // Makes the instance of a repository's method (a pair with IsInstance).
    Func<DbConnection, DbDataSource, object>? Instance = null
);
