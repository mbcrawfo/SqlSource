using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
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

    private static DescribeRequest Query(params SqlQueryParameter[] parameters) =>
        new("Q", "SELECT 1", new EquatableArray<SqlQueryParameter>([.. parameters]));

    private static SqlQueryParameter Declared(string name, string type) => new(name, type, null, true);

    [Fact]
    public void SeveralStatements_Query_IsSqlsrc230WithItsHelp()
    {
        var failure = PostgresErrors.SeveralStatements(Query());

        failure.Descriptor.Id.ShouldBe("SQLSRC230");
        failure.Arguments.ShouldBe(["Q"]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.SeveralStatementsHelp);
    }

    // The type as the marker wrote it, and the parameter as the SQL writes one, with its prefix.
    [Fact]
    public void TypeRefused_Parameter_IsSqlsrc228ThatSaysItIsNoTypeName()
    {
        var parameter = Declared("tenant", "int) AS SELECT 1");

        var failure = PostgresErrors.TypeRefused(Query(parameter), parameter);

        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments.ShouldBe(["int) AS SELECT 1", "@tenant", "Q", "it is not written as a type name"]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.NotATypeNameHelp);
    }
}
