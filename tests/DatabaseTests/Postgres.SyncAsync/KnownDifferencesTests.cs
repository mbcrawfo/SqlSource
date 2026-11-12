using System;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync;

// The known differences on outcomes written here, so that no test needs the server.
public sealed class KnownDifferencesTests
{
    private const string Refused = "System.NotSupportedException";

    private const string Multiplexing = "Synchronous command execution is not supported when multiplexing is on";

    private static readonly Outcome Ran = new("1", null, "Open", "none", []);

    // The transaction by its name, since a public theory cannot take the harness's internal enum.
    private static Scenario ScenarioOf(string source, string transaction) =>
        new(
            "Host.Method: what",
            typeof(KnownDifferencesTests),
            "Method",
            MethodKind.Query,
            ScenarioTag.Ordinary,
            new Setup(Transaction: Enum.Parse<CallerTransaction>(transaction), Source: source)
        );

    private static Outcome Threw(string type, string message) =>
        new(null, new Failure(type, null, null, null, message), "Open", "none", []);

    // A connection with no transaction open, whether none was begun or the one begun has completed.
    [Theory]
    [InlineData("None")]
    [InlineData("Completed")]
    public void Of_SyncRefusedByMultiplexingWithoutATransaction_IsKnown(string transaction)
    {
        var verdict = Verdicts.Of(
            ScenarioOf("multiplexing", transaction),
            Ran,
            Threw(Refused, Multiplexing),
            KnownDifferences.All
        );

        verdict.Agreement.ShouldBe(Agreement.Known);
        verdict.Applied.ShouldBe([KnownDifferences.NpgsqlMultiplexingRefusesSyncIo]);
    }

    // The synchronous method runs in a transaction the caller has begun, on another data source, and Npgsql refuses
    // nothing else of it for multiplexing, so each of these is a difference of the generated methods.
    [Theory]
    [InlineData("multiplexing", "Begun", Refused, Multiplexing)]
    [InlineData("default", "None", Refused, Multiplexing)]
    [InlineData("multiplexing", "None", "System.InvalidOperationException", Multiplexing)]
    [InlineData("multiplexing", "None", Refused, "Explicit preparation not supported with multiplexing")]
    public void Of_SyncRefusalOutsideTheLimit_IsDifferent(
        string source,
        string transaction,
        string type,
        string message
    )
    {
        var verdict = Verdicts.Of(ScenarioOf(source, transaction), Ran, Threw(type, message), KnownDifferences.All);

        verdict.Agreement.ShouldBe(Agreement.Different);
        verdict.Applied.ShouldBeEmpty();
    }
}
