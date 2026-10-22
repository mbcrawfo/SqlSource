using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing.Realistic;
using Xunit;
using Xunit.Sdk;

namespace SqlSource.Tool.Tests.Realistic;

// The rules of a case's description that need no server: that a description has the columns of the truth, and the
// text a failure is shown as.
public sealed class RealisticDescriptionsTests
{
    [Fact]
    public void ColumnsOf_ColumnNamedOtherwiseThanItsTruth_FailsNamingTheCaseAndThePosition()
    {
        var found = RealisticTestCases.Keyed() with
        {
            Columns = [new TruthColumn("id", false, null, null), new TruthColumn("id", false, null, null)],
        };
        var described = Outcome<QueryDescription, DescribeFailure>.Of(
            new QueryDescription(
                SidecarResultKind.Rows,
                new EquatableArray<DescribedParameter>([]),
                new EquatableArray<SidecarColumn>([Column(0, "id"), Column(1, "account_id")]),
                null,
                null,
                null
            )
        );

        var thrown = Should.Throw<FailException>(() => RealisticDescriptions.ColumnsOf(found, described));

        thrown.Message.ShouldStartWith("realistic/keyed column 1 is described as account_id");
    }

    [Fact]
    public void RenderFailure_RefusalWithHelp_WritesTheHelpBetweenItsArgumentsAndTheServer() =>
        RealisticDescriptions
            .RenderFailure(
                new DescribeFailure(
                    ToolDiagnostics.ServerRejectedQuery,
                    new EquatableArray<string>(["Q", "operator does not exist"]),
                    null,
                    new EquatableArray<string>(["error: operator does not exist", "hint: add casts"]),
                    "cast the parameter"
                )
            )
            .ShouldBe(
                "failure: SQLSRC225\n"
                    + "argument: Q\n"
                    + "argument: operator does not exist\n"
                    + "help: cast the parameter\n"
                    + "server: error: operator does not exist\n"
                    + "server: hint: add casts\n"
            );

    private static PostgresType Type(string name, string internalName) =>
        new(name, SidecarValues.Kind.Base, "pg_catalog", internalName, null, null, null, null, null, null, null);

    private static SidecarColumn Column(int ordinal, string name) =>
        new(ordinal, name, Type("integer", "int4"), null, null, null, null, null);
}
