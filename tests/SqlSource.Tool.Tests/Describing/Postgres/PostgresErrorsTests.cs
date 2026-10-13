using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// A server's error, or a refusal of the tool, as the failure the run reports at the query.
public class PostgresErrorsTests
{
    [Fact]
    public void Open_ErrorWithASqlState_IsSqlsrc224AtNoStepWithTheServersLine()
    {
        var error = new PostgresServerError(
            "28P01",
            "password authentication failed for user \"app\"",
            null,
            null,
            null
        );

        var failure = PostgresErrors.Open(error, "billing");

        failure.Descriptor.Id.ShouldBe("SQLSRC224");
        failure.Arguments.ShouldBe(["billing"]);
        failure.Step.ShouldBeNull();
        failure.ServerLines.ShouldBe(["28P01: password authentication failed for user \"app\""]);
        failure.Help.ShouldBe(PostgresErrors.OpenHelp);
    }

    [Fact]
    public void Open_ErrorWithoutASqlState_HasTheMessageAlone() =>
        PostgresErrors
            .Open(new PostgresServerError(null, "Failed to connect to 127.0.0.1:1", null, null, null), "app")
            .ServerLines.ShouldBe(["Failed to connect to 127.0.0.1:1"]);

    // Each line the server gave, in the order the epic fixes, and a position counted from where the SQL starts.
    [Fact]
    public void ServerLines_ErrorWithEveryPart_GivesEachInOrder() =>
        PostgresErrors
            .ServerLines(new PostgresServerError("42883", "operator does not exist", "a detail", "a hint", 63), 31)
            .ShouldBe(["42883: operator does not exist", "detail: a detail", "hint: a hint", "at character 32"]);

    [Theory]
    [InlineData(null)]
    [InlineData(31)]
    [InlineData(7)]
    public void ServerLines_NoPositionOrOneBeforeTheSql_HasNoPositionLine(int? position) =>
        PostgresErrors
            .ServerLines(new PostgresServerError("42601", "syntax error", null, null, position), 31)
            .ShouldBe(["42601: syntax error"]);
}
