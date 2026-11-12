using System;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using Xunit;

namespace SqlSource.Tests.TestHarness.SyncAsync;

public sealed class VerdictsTests
{
    private static readonly Scenario Any = new(
        "Host.Method: what",
        typeof(VerdictsTests),
        "Method",
        MethodKind.Query,
        ScenarioTag.Ordinary,
        new Setup()
    );

    private static Outcome Outcome(string? message = null, string connection = "Open") =>
        new(
            message is null ? "1" : null,
            message is null ? null : new Failure("DbException", null, "42", null, message),
            connection,
            "none",
            []
        );

    private static KnownDifference Rewriting(string name, Func<Outcome, Outcome> rewriteSync) =>
        new(name, "because", (_, left, right) => (left, rewriteSync(right)));

    [Fact]
    public void Of_EqualOutcomes_IsSame() =>
        Verdicts.Of(Any, Outcome(), Outcome(), []).Agreement.ShouldBe(Agreement.Same);

    [Fact]
    public void Of_DifferentMessage_IsDifferent()
    {
        var verdict = Verdicts.Of(Any, Outcome("async text"), Outcome("sync text"), []);

        verdict.Agreement.ShouldBe(Agreement.Different);
        verdict.Report.ShouldContain("async text");
        verdict.Report.ShouldContain("sync text");
    }

    [Fact]
    public void Of_DifferenceAKnownRewriteRemoves_IsKnown()
    {
        var known = Rewriting("states", sync => sync with { ConnectionState = "Open" });

        var verdict = Verdicts.Of(Any, Outcome(), Outcome(connection: "Closed"), [known]);

        verdict.Agreement.ShouldBe(Agreement.Known);
        verdict.Applied.ShouldBe([known]);
        verdict.Report.ShouldBeEmpty();
    }

    [Fact]
    public void Of_KnownRewriteThatLeavesADifference_IsDifferent()
    {
        var known = Rewriting("partial", sync => sync with { ConnectionState = "Broken" });

        var verdict = Verdicts.Of(Any, Outcome(), Outcome("x", "Closed"), [known]);

        verdict.Agreement.ShouldBe(Agreement.Different);
        verdict.Applied.ShouldBe([known]);
        verdict.Report.ShouldContain("Broken");
    }

    [Fact]
    public void Of_KnownRewriteThatChangesNothing_IsNotApplied()
    {
        var known = Rewriting("idle", sync => sync);

        var verdict = Verdicts.Of(Any, Outcome(), Outcome(connection: "Closed"), [known]);

        verdict.Agreement.ShouldBe(Agreement.Different);
        verdict.Applied.ShouldBeEmpty();
    }

    // An outcome with every field set, which each row of the theory changes in one field alone.
    private static readonly Outcome Full = new(
        "result-a",
        new Failure("type-a", "inner-a", "code-a", 1, "message-a"),
        "state-a",
        "transaction-a",
        ["table-a"]
    );

    private static Outcome Changed(string field) =>
        field switch
        {
            "Result" => Full with { Result = "result-b" },
            "ConnectionState" => Full with { ConnectionState = "state-b" },
            "TransactionState" => Full with { TransactionState = "transaction-b" },
            "Tables" => Full with { Tables = ["table-b"] },
            "Type" => Full with { Failure = Full.Failure! with { Type = "type-b" } },
            "InnerType" => Full with { Failure = Full.Failure! with { InnerType = "inner-b" } },
            "Code" => Full with { Failure = Full.Failure! with { Code = "code-b" } },
            "ItemIndex" => Full with { Failure = Full.Failure! with { ItemIndex = 2 } },
            "Message" => Full with { Failure = Full.Failure! with { Message = "message-b" } },
            _ => throw new ArgumentException($"No field '{field}'.", nameof(field)),
        };

    // Each field is compared, and the report shows both of its values.
    [Theory]
    [InlineData("Result", "result-a", "result-b")]
    [InlineData("ConnectionState", "state-a", "state-b")]
    [InlineData("TransactionState", "transaction-a", "transaction-b")]
    [InlineData("Tables", "table-a", "table-b")]
    [InlineData("Type", "type-a", "type-b")]
    [InlineData("InnerType", "inner-a", "inner-b")]
    [InlineData("Code", "code-a", "code-b")]
    [InlineData("ItemIndex", "item 1)", "item 2)")]
    [InlineData("Message", "message-a", "message-b")]
    public void Of_OutcomesDifferingInOneField_IsDifferent(string field, string asyncText, string syncText)
    {
        var verdict = Verdicts.Of(Any, Full, Changed(field), []);

        verdict.Agreement.ShouldBe(Agreement.Different);
        verdict.Report.ShouldContain(asyncText);
        verdict.Report.ShouldContain(syncText);
    }
}
