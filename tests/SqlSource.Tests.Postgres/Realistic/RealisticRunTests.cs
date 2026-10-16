using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Snapshot;
using SqlSource.Tool;
using SqlSource.Tool.Describing.Describers;
using Xunit;
using Xunit.Sdk;

namespace SqlSource.Tests.Postgres.Realistic;

// The rules a run of a case shares that need no server: which majors a case runs and is recorded on, which files its
// folder holds, that a description has the columns of the truth, and the text a description is recorded as.  A case
// described against the server is in RealisticDatabaseTests.
public sealed class RealisticRunTests
{
    [Theory]
    [InlineData(true, 14, Expectation.Record)]
    [InlineData(true, 18, Expectation.Record)]
    [InlineData(true, 16, Expectation.Refuse)]
    [InlineData(false, 14, Expectation.Compare)]
    [InlineData(false, 18, Expectation.Compare)]
    [InlineData(false, 17, Expectation.Skip)]
    public void ExpectationOn_RecordingAndMajor_IsWhatTheTheoryDoes(bool recording, int major, Expectation expected) =>
        RealisticRun.ExpectationOn(recording, major).ShouldBe(expected);

    [Theory]
    [InlineData(null, 14, true)]
    [InlineData(17, 14, false)]
    [InlineData(17, 17, true)]
    [InlineData(17, 18, true)]
    public void RunsOn_MinimumMajorAndServer_IsWhetherTheCaseRuns(int? minimum, int major, bool runs) =>
        RealisticRun.RunsOn(Case() with { MinimumMajor = minimum }, major).ShouldBe(runs);

    [Theory]
    [InlineData(null, new[] { "query.sql", "case.json", "expected-pg14.txt", "expected-pg18.txt" })]
    [InlineData(17, new[] { "query.sql", "case.json", "expected-pg18.txt" })]
    public void FilesOf_MinimumMajor_IsTheQueryTheTruthAndAnExpectationOfEachRecordedMajorItRunsOn(
        int? minimum,
        string[] files
    ) => RealisticRun.FilesOf(Case() with { MinimumMajor = minimum }).ShouldBe(files);

    [Fact]
    public void ColumnsOf_ColumnNamedOtherwiseThanItsTruth_FailsNamingTheCaseAndThePosition()
    {
        var found = Case() with
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

        var thrown = Should.Throw<FailException>(() => RealisticRun.ColumnsOf(found, described));

        thrown.Message.ShouldStartWith("realistic/keyed column 1 is described as account_id");
    }

    [Fact]
    public void Render_Description_WritesALineForEachFact()
    {
        var integer = Type("integer", "int4");
        var description = new QueryDescription(
            SidecarResultKind.Rows,
            new EquatableArray<DescribedParameter>([
                new DescribedParameter("limit", Type("bigint", "int8"), SidecarValues.TypeSource.Inferred),
            ]),
            new EquatableArray<SidecarColumn>([
                new SidecarColumn(
                    0,
                    "id",
                    integer,
                    false,
                    SidecarValues.NullableSource.Catalog,
                    new SidecarOrigin("public", "accounts", "id"),
                    true,
                    false
                ),
                new SidecarColumn(1, "spent", Type("numeric", "numeric"), null, null, null, null, null),
            ]),
            null,
            SidecarValues.Plan.Walked,
            null
        );

        RealisticRun
            .Render(Outcome<QueryDescription, DescribeFailure>.Of(description))
            .ShouldBe(
                "kind: rows\n"
                    + "plan: walked\n"
                    + "table-match: -\n"
                    + "parameter @limit: bigint (inferred)\n"
                    + "column 0 id: integer | nullable false | catalog | public.accounts.id\n"
                    + "column 1 spent: numeric | nullable null | - | -\n"
            );
    }

    [Fact]
    public void Render_RefusalWithHelp_WritesTheHelpBetweenItsArgumentsAndTheServer() =>
        RealisticRun
            .Render(
                Outcome<QueryDescription, DescribeFailure>.Failed(
                    new DescribeFailure(
                        ToolDiagnostics.ServerRejectedQuery,
                        new EquatableArray<string>(["Q", "operator does not exist"]),
                        null,
                        new EquatableArray<string>(["error: operator does not exist", "hint: add casts"]),
                        "cast the parameter"
                    )
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

    private static RealisticCase Case() =>
        new("keyed", "keyed", null, null, [], [new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)]);

    private static SidecarColumn Column(int ordinal, string name) =>
        new(ordinal, name, Type("integer", "int4"), null, null, null, null, null);
}
