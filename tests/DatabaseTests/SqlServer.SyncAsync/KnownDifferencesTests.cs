using Shouldly;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync;

// The known differences on outcomes written here, so that no test needs the server.
public sealed class KnownDifferencesTests
{
    private const string Refused = "System.InvalidOperationException";

    private const string Pending =
        " requires the command to have a transaction when the connection assigned to the command is in a pending "
        + "local transaction.  The Transaction property of the command has not been initialized.";

    private static readonly Scenario Any = new(
        "Host.Method: what",
        typeof(KnownDifferencesTests),
        "Method",
        MethodKind.Query,
        ScenarioTag.Ordinary,
        new Setup(Transaction: CallerTransaction.Begun, PassTransaction: false)
    );

    private static Outcome Threw(string type, string call) =>
        new(null, new Failure(type, null, null, null, call + Pending), "Open", "active", []);

    // SqlClient names the call that refused the command: the asynchronous method's runs as BeginExecuteReader or
    // BeginExecuteNonQuery, and ExecuteScalarAsync as BeginExecuteReader too.
    [Theory]
    [InlineData("BeginExecuteReader", "ExecuteReader")]
    [InlineData("BeginExecuteReader", "ExecuteScalar")]
    [InlineData("BeginExecuteNonQuery", "ExecuteNonQuery")]
    public void Of_PendingTransactionRefusedByEachCall_IsKnown(string asyncCall, string syncCall)
    {
        var verdict = Verdicts.Of(Any, Threw(Refused, asyncCall), Threw(Refused, syncCall), KnownDifferences.All);

        verdict.Agreement.ShouldBe(Agreement.Known);
        verdict.Applied.ShouldBe([KnownDifferences.SqlClientPendingTransactionMessage]);
    }

    // Another exception from either method, or a synchronous call that is not the asynchronous one's, is a difference
    // of the generated methods.
    [Theory]
    [InlineData("System.Exception", Refused, "BeginExecuteReader", "ExecuteReader")]
    [InlineData(Refused, "System.Exception", "BeginExecuteReader", "ExecuteReader")]
    [InlineData(Refused, Refused, "BeginExecuteReader", "ExecuteNonQuery")]
    [InlineData(Refused, Refused, "BeginExecuteNonQuery", "ExecuteScalar")]
    public void Of_RefusalOutsideTheLimit_IsDifferent(
        string asyncType,
        string syncType,
        string asyncCall,
        string syncCall
    )
    {
        var verdict = Verdicts.Of(Any, Threw(asyncType, asyncCall), Threw(syncType, syncCall), KnownDifferences.All);

        verdict.Agreement.ShouldBe(Agreement.Different);
        verdict.Applied.ShouldBeEmpty();
    }
}
