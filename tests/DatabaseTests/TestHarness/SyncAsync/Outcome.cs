using System.Collections.Generic;

namespace SqlSource.DatabaseTests.TestHarness.SyncAsync;

// What a call threw, as text that two runs can be compared by.
internal sealed record Failure(string Type, string? InnerType, string? Code, int? ItemIndex, string Message);

// Everything a call did that the two runs of a pair are compared by.
internal sealed record Outcome(
    // Rendering.Of of the returned value, "<void>" for none; null when the call threw.
    string? Result,
    Failure? Failure,
    // DbConnection.State after the call, before disposal.
    string ConnectionState,
    // "none" without a caller's transaction, else the engine's text.
    string TransactionState,
    // One rendered table per Setup.Tables entry, in order.
    IReadOnlyList<string> Tables
);
